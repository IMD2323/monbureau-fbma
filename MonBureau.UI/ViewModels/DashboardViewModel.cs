using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Core.Interfaces;
using MonBureau.Infrastructure.Data;
using MonBureau.Infrastructure.Services;

namespace MonBureau.UI.ViewModels
{
    /// <summary>
    /// FIXED: Proper disposal with CancellationToken cleanup and collection clearing
    /// </summary>
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private const string StatsCacheKey = "dashboard:stats";
        private readonly IUnitOfWork _unitOfWork;
        private readonly AppDbContext _context;
        private readonly CacheService _cache;
        private CancellationTokenSource? _loadCancellation;
        private bool _disposed;

        [ObservableProperty]
        private ObservableCollection<Client> _recentClients = new();

        [ObservableProperty]
        private ObservableCollection<Case> _recentCases = new();

        [ObservableProperty]
        private ObservableCollection<CaseItem> _recentItems = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ItemType? _selectedItemTypeFilter;

        [ObservableProperty]
        private int _totalClients;

        [ObservableProperty]
        private int _totalCases;

        [ObservableProperty]
        private int _openCases;

        [ObservableProperty]
        private bool _isLoading;

        public DashboardViewModel(IUnitOfWork unitOfWork, AppDbContext context, CacheService cache)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Created");
        }

        public async Task LoadDataAsync()
        {
            if (_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Already disposed, skipping load");
                return;
            }

            // FIXED: Cancel any existing load operation
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var ct = _loadCancellation.Token;

            try
            {
                IsLoading = true;
                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Loading data...");

                // Fetch statistics immediately (cached), lazy-load heavy panels in background
                await LoadStatisticsAsync(ct);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await LoadPanelsAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Panel load cancelled");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] Panel load error: {ex.Message}");
                    }
                }, ct);

                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Statistics loaded; panels loading lazily");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Load cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] Error loading: {ex.Message}");
                MessageBox.Show($"Erreur lors du chargement: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadStatisticsAsync(CancellationToken ct)
        {
            if (_cache.Get<DashboardStatistics>(StatsCacheKey) is { } cached)
            {
                TotalClients = cached.TotalClients;
                TotalCases = cached.TotalCases;
                OpenCases = cached.OpenCases;
                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Stats served from cache");
                return;
            }

            var totalClientsTask = _context.Clients.AsNoTracking().CountAsync(ct);
            var totalCasesTask = _context.Cases.AsNoTracking().CountAsync(ct);
            var openCasesTask = _context.Cases
                .AsNoTracking()
                .CountAsync(c => c.Status == CaseStatus.Open || c.Status == CaseStatus.InProgress, ct);

            await Task.WhenAll(totalClientsTask, totalCasesTask, openCasesTask);

            if (ct.IsCancellationRequested) return;

            TotalClients = totalClientsTask.Result;
            TotalCases = totalCasesTask.Result;
            OpenCases = openCasesTask.Result;

            _cache.Set(StatsCacheKey, new DashboardStatistics
            {
                TotalClients = TotalClients,
                TotalCases = TotalCases,
                OpenCases = OpenCases
            }, TimeSpan.FromMinutes(2));
        }

        private async Task LoadRecentClientsAsync(CancellationToken ct)
        {
            var clients = await _context.Clients
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Take(6)
                .ToListAsync(ct);

            if (ct.IsCancellationRequested) return;

            // FIXED: Always update collections on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RecentClients = new ObservableCollection<Client>(clients);
            }, System.Windows.Threading.DispatcherPriority.Background, ct);
        }

        private async Task LoadRecentCasesAsync(CancellationToken ct)
        {
            var cases = await _context.Cases
                .AsNoTracking()
                .Include(c => c.Client)
                .OrderByDescending(c => c.CreatedAt)
                .Take(6)
                .ToListAsync(ct);

            if (ct.IsCancellationRequested) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RecentCases = new ObservableCollection<Case>(cases);
            }, System.Windows.Threading.DispatcherPriority.Background, ct);
        }

        private async Task LoadRecentItemsAsync(CancellationToken ct)
        {
            var items = await _context.CaseItems
                .AsNoTracking()
                .Include(i => i.Case)
                    .ThenInclude(c => c.Client)
                .OrderByDescending(i => i.CreatedAt)
                .Take(6)
                .ToListAsync(ct);

            if (ct.IsCancellationRequested) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RecentItems = new ObservableCollection<CaseItem>(items);
            }, System.Windows.Threading.DispatcherPriority.Background, ct);
        }

        private async Task LoadPanelsAsync(CancellationToken ct)
        {
            var clientsTask = LoadRecentClientsAsync(ct);
            var casesTask = LoadRecentCasesAsync(ct);
            var itemsTask = LoadRecentItemsAsync(ct);

            await Task.WhenAll(clientsTask, casesTask, itemsTask);
        }

        #region Commands

        [RelayCommand]
        private async Task AddClient()
        {
            if (_disposed) return;

            try
            {
                var dialog = new Views.Dialogs.ClientDialog();
                if (dialog.ShowDialog() == true)
                {
                    InvalidateStatsCache();
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task AddCase()
        {
            if (_disposed) return;

            try
            {
                var dialog = new Views.Dialogs.CaseDialog();
                if (dialog.ShowDialog() == true)
                {
                    InvalidateStatsCache();
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task AddItem()
        {
            if (_disposed) return;

            try
            {
                var dialog = new Views.Dialogs.ItemDialog();
                if (dialog.ShowDialog() == true)
                {
                    InvalidateStatsCache();
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void EditClient(Client? client)
        {
            if (_disposed || client == null) return;

            try
            {
                var dialog = new Views.Dialogs.ClientDialog(client);
                if (dialog.ShowDialog() == true)
                {
                    InvalidateStatsCache();
                    _ = LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void EditCase(Case? caseEntity)
        {
            if (_disposed || caseEntity == null) return;

            try
            {
                var dialog = new Views.Dialogs.CaseDialog(caseEntity);
                if (dialog.ShowDialog() == true)
                {
                    InvalidateStatsCache();
                    _ = LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void EditItem(CaseItem? item)
        {
            if (_disposed || item == null) return;

            try
            {
                var dialog = new Views.Dialogs.ItemDialog(item);
                if (dialog.ShowDialog() == true)
                {
                    InvalidateStatsCache();
                    _ = LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteClient(Client? client)
        {
            if (_disposed || client == null) return;

            try
            {
                var result = MessageBox.Show(
                    $"Êtes-vous sûr de vouloir supprimer le client '{client.FullName}' ?\n\nTous les dossiers associés seront également supprimés.",
                    "Confirmer la suppression",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await _unitOfWork.Clients.DeleteAsync(client);
                    await _unitOfWork.SaveChangesAsync();
                    InvalidateStatsCache();
                    await LoadDataAsync();

                    MessageBox.Show("Client supprimé avec succès", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la suppression: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteCase(Case? caseEntity)
        {
            if (_disposed || caseEntity == null) return;

            try
            {
                var result = MessageBox.Show(
                    $"Êtes-vous sûr de vouloir supprimer le dossier '{caseEntity.Number}' ?\n\nTous les documents et dépenses associés seront également supprimés.",
                    "Confirmer la suppression",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await _unitOfWork.Cases.DeleteAsync(caseEntity);
                    await _unitOfWork.SaveChangesAsync();
                    InvalidateStatsCache();
                    await LoadDataAsync();

                    MessageBox.Show("Dossier supprimé avec succès", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la suppression: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteItem(CaseItem? item)
        {
            if (_disposed || item == null) return;

            try
            {
                var result = MessageBox.Show(
                    $"Êtes-vous sûr de vouloir supprimer '{item.Name}' ?",
                    "Confirmer la suppression",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await _unitOfWork.CaseItems.DeleteAsync(item);
                    await _unitOfWork.SaveChangesAsync();
                    InvalidateStatsCache();
                    await LoadDataAsync();

                    MessageBox.Show("Élément supprimé avec succès", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la suppression: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task Search()
        {
            if (_disposed || string.IsNullOrWhiteSpace(SearchText))
            {
                await LoadDataAsync();
                return;
            }

            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var ct = _loadCancellation.Token;

            try
            {
                IsLoading = true;
                var searchLower = SearchText.ToLower();

                var clientsTask = _context.Clients
                    .AsNoTracking()
                    .Where(c => c.FirstName.ToLower().Contains(searchLower) ||
                               c.LastName.ToLower().Contains(searchLower))
                    .ToListAsync(ct);

                var casesTask = _context.Cases
                    .AsNoTracking()
                    .Include(c => c.Client)
                    .Where(c => c.Number.ToLower().Contains(searchLower) ||
                               c.Title.ToLower().Contains(searchLower))
                    .ToListAsync(ct);

                var itemsTask = _context.CaseItems
                    .AsNoTracking()
                    .Include(i => i.Case)
                        .ThenInclude(c => c.Client)
                    .Where(i => i.Name.ToLower().Contains(searchLower))
                    .ToListAsync(ct);

                await Task.WhenAll(clientsTask, casesTask, itemsTask);

                if (ct.IsCancellationRequested) return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    RecentClients = new ObservableCollection<Client>(clientsTask.Result);
                    RecentCases = new ObservableCollection<Case>(casesTask.Result);
                    RecentItems = new ObservableCollection<CaseItem>(itemsTask.Result);
                }, System.Windows.Threading.DispatcherPriority.Background, ct);
            }
            catch (OperationCanceledException)
            {
                // Cancelled, ignore
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la recherche: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Disposal

        /// <summary>
        /// FIXED: Proper disposal to prevent memory leaks
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Disposing...");

            // FIXED: Cancel and dispose CancellationTokenSource
            if (_loadCancellation != null)
            {
                _loadCancellation.Cancel();
                _loadCancellation.Dispose();
                _loadCancellation = null;
                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] CancellationToken cancelled and disposed");
            }

            // FIXED: Clear all observable collections
            RecentClients.Clear();
            RecentCases.Clear();
            RecentItems.Clear();
            System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Collections cleared");

            _disposed = true;
            GC.SuppressFinalize(this);

            System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Disposal complete");
        }

        /// <summary>
        /// Finalizer to catch missed disposals
        /// </summary>
        ~DashboardViewModel()
        {
            if (!_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] ⚠️ WARNING: Finalizer called - Dispose() was not called!");
                Dispose();
            }
        }

        #endregion

        private void InvalidateStatsCache() => _cache.Invalidate(StatsCacheKey);

        private sealed class DashboardStatistics
        {
            public int TotalClients { get; set; }
            public int TotalCases { get; set; }
            public int OpenCases { get; set; }
        }
    }
}