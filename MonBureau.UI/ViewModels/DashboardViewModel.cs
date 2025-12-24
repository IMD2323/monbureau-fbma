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
using MonBureau.UI.Features;
using MonBureau.UI.Features.Clients;
using MonBureau.UI.Features.Cases;
using MonBureau.UI.Features.Expenses;
using MonBureau.UI.Features.Rdvs;
using MonBureau.UI.Features.Documents;


namespace MonBureau.UI.ViewModels
{
    /// <summary>
    /// FIXED: Uses DbContextFactory to prevent concurrent access issues
    /// Each operation gets its own DbContext instance
    /// </summary>
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private const string StatsCacheKey = "dashboard:stats";
        private const int RECENT_ITEMS_LIMIT = 6;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
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
        private int _totalClients;

        [ObservableProperty]
        private int _totalCases;

        [ObservableProperty]
        private int _openCases;

        [ObservableProperty]
        private bool _isLoading;

        public DashboardViewModel(
            IUnitOfWork unitOfWork,
            IDbContextFactory<AppDbContext> contextFactory,
            CacheService cache)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Created");
        }

        public async Task LoadDataAsync()
        {
            if (_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Already disposed");
                return;
            }

            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var ct = _loadCancellation.Token;

            try
            {
                IsLoading = true;
                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Loading data...");

                // Load statistics (fast, cached)
                await LoadStatisticsAsync(ct);

                // Load panels in parallel with separate contexts
                await Task.WhenAll(
                    LoadRecentClientsAsync(ct),
                    LoadRecentCasesAsync(ct),
                    LoadRecentItemsAsync(ct)
                );

                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] ✅ All data loaded");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Load cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] ❌ Error: {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Erreur de chargement: {ex.Message}", "Erreur",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// FIXED: Uses separate DbContext for statistics
        /// </summary>
        private async Task LoadStatisticsAsync(CancellationToken ct)
        {
            if (_cache.Get<DashboardStatistics>(StatsCacheKey) is { } cached)
            {
                TotalClients = cached.TotalClients;
                TotalCases = cached.TotalCases;
                OpenCases = cached.OpenCases;
                System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Stats from cache");
                return;
            }

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var totalClientsTask = context.Clients.AsNoTracking().CountAsync(ct);
            var totalCasesTask = context.Cases.AsNoTracking().CountAsync(ct);
            var openCasesTask = context.Cases
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

        /// <summary>
        /// FIXED: Uses separate DbContext
        /// </summary>
        private async Task LoadRecentClientsAsync(CancellationToken ct)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var clients = await context.Clients
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Take(RECENT_ITEMS_LIMIT)
                .ToListAsync(ct);

            if (ct.IsCancellationRequested) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RecentClients = new ObservableCollection<Client>(clients);
            }, System.Windows.Threading.DispatcherPriority.Background, ct);
        }

        /// <summary>
        /// FIXED: Uses separate DbContext
        /// </summary>
        private async Task LoadRecentCasesAsync(CancellationToken ct)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var cases = await context.Cases
                .AsNoTracking()
                .Include(c => c.Client)
                .OrderByDescending(c => c.CreatedAt)
                .Take(RECENT_ITEMS_LIMIT)
                .ToListAsync(ct);

            if (ct.IsCancellationRequested) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RecentCases = new ObservableCollection<Case>(cases);
            }, System.Windows.Threading.DispatcherPriority.Background, ct);
        }

        /// <summary>
        /// FIXED: Uses separate DbContext
        /// </summary>
        private async Task LoadRecentItemsAsync(CancellationToken ct)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var items = await context.CaseItems
                .AsNoTracking()
                .Include(i => i.Case)
                    .ThenInclude(c => c.Client)
                .OrderByDescending(i => i.CreatedAt)
                .Take(RECENT_ITEMS_LIMIT)
                .ToListAsync(ct);

            if (ct.IsCancellationRequested) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RecentItems = new ObservableCollection<CaseItem>(items);
            }, System.Windows.Threading.DispatcherPriority.Background, ct);
        }

        #region Commands

        [RelayCommand]
        private async Task AddClient()
        {
            if (_disposed) return;

            try
            {
                var dialog = new ClientDialog();
                if (dialog.ShowDialog() == true)
                {
                    _cache.Invalidate(StatsCacheKey);
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
                var dialog = new CaseDialog();
                if (dialog.ShowDialog() == true)
                {
                    _cache.Invalidate(StatsCacheKey);
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
                var dialog = new ItemDialog();
                if (dialog.ShowDialog() == true)
                {
                    _cache.Invalidate(StatsCacheKey);
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
                var dialog = new ClientDialog(client);
                if (dialog.ShowDialog() == true)
                {
                    _cache.Invalidate(StatsCacheKey);
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
                var dialog = new CaseDialog(caseEntity);
                if (dialog.ShowDialog() == true)
                {
                    _cache.Invalidate(StatsCacheKey);
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
                var dialog = new ItemDialog(item);
                if (dialog.ShowDialog() == true)
                {
                    _cache.Invalidate(StatsCacheKey);
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
                    $"Supprimer '{client.FullName}' ?\n\nTous les dossiers associés seront supprimés.",
                    "Confirmer",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await _unitOfWork.Clients.DeleteAsync(client);
                    await _unitOfWork.SaveChangesAsync();
                    _cache.Invalidate(StatsCacheKey);
                    await LoadDataAsync();

                    MessageBox.Show("Client supprimé", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
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
                    $"Supprimer '{caseEntity.Number}' ?\n\nTous les documents seront supprimés.",
                    "Confirmer",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await _unitOfWork.Cases.DeleteAsync(caseEntity);
                    await _unitOfWork.SaveChangesAsync();
                    _cache.Invalidate(StatsCacheKey);
                    await LoadDataAsync();

                    MessageBox.Show("Dossier supprimé", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
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
                    $"Supprimer '{item.Name}' ?",
                    "Confirmer",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await _unitOfWork.CaseItems.DeleteAsync(item);
                    await _unitOfWork.SaveChangesAsync();
                    _cache.Invalidate(StatsCacheKey);
                    await LoadDataAsync();

                    MessageBox.Show("Élément supprimé", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// FIXED: Search uses separate DbContext
        /// </summary>
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

                await using var context = await _contextFactory.CreateDbContextAsync(ct);

                var clientsTask = context.Clients
                    .AsNoTracking()
                    .Where(c => c.FirstName.ToLower().Contains(searchLower) ||
                               c.LastName.ToLower().Contains(searchLower))
                    .Take(20)
                    .ToListAsync(ct);

                var casesTask = context.Cases
                    .AsNoTracking()
                    .Include(c => c.Client)
                    .Where(c => c.Number.ToLower().Contains(searchLower) ||
                               c.Title.ToLower().Contains(searchLower))
                    .Take(20)
                    .ToListAsync(ct);

                var itemsTask = context.CaseItems
                    .AsNoTracking()
                    .Include(i => i.Case)
                        .ThenInclude(c => c.Client)
                    .Where(i => i.Name.ToLower().Contains(searchLower))
                    .Take(20)
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
                // Cancelled
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine("[DashboardViewModel] Disposing...");

            if (_loadCancellation != null)
            {
                _loadCancellation.Cancel();
                _loadCancellation.Dispose();
                _loadCancellation = null;
            }

            RecentClients.Clear();
            RecentCases.Clear();
            RecentItems.Clear();

            _disposed = true;
            GC.SuppressFinalize(this);

            System.Diagnostics.Debug.WriteLine("[DashboardViewModel] ✅ Disposed");
        }

        #endregion

        private sealed class DashboardStatistics
        {
            public int TotalClients { get; set; }
            public int TotalCases { get; set; }
            public int OpenCases { get; set; }
        }
    }
}