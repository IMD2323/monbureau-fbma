using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Infrastructure.Data;

namespace MonBureau.UI.ViewModels
{
    /// <summary>
    /// FIXED: Paginated document loading
    /// No more loading all documents from all cases
    /// </summary>
    public partial class DocumentsViewModel : ObservableObject, IDisposable
    {
        private readonly AppDbContext _context;
        private CancellationTokenSource? _loadCancellation;
        private bool _disposed;

        private const int PAGE_SIZE = 50; // Documents per page

        #region Observable Properties

        [ObservableProperty]
        private ObservableCollection<CaseItem> documents = new();

        [ObservableProperty]
        private string searchFilter = string.Empty;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private int totalDocuments;

        [ObservableProperty]
        private int currentPage = 1;

        [ObservableProperty]
        private int totalPages;

        [ObservableProperty]
        private CaseItem? selectedDocument;

        #endregion

        #region Constructor

        public DocumentsViewModel(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Created");
        }

        #endregion

        #region Initialization

        public async Task InitializeAsync()
        {
            if (_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Already disposed");
                return;
            }

            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var ct = _loadCancellation.Token;

            try
            {
                IsLoading = true;
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Loading documents...");

                await LoadDocumentsPageAsync(ct);

                System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] ✅ Loaded page {CurrentPage}/{TotalPages}");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Load cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] ❌ Load error: {ex.Message}");
                MessageBox.Show(
                    $"Erreur lors du chargement des documents:\n\n{ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// FIXED: Loads only one page of documents
        /// Applies filter in database
        /// </summary>
        private async Task LoadDocumentsPageAsync(CancellationToken ct)
        {
            // Build base query
            var query = _context.CaseItems
                .AsNoTracking()
                .Include(i => i.Case)
                    .ThenInclude(c => c.Client)
                .Where(i => i.Type == ItemType.Document);

            // Apply search filter in database
            if (!string.IsNullOrWhiteSpace(SearchFilter))
            {
                var lowerFilter = SearchFilter.ToLowerInvariant();
                query = query.Where(i =>
                    (i.Name != null && i.Name.ToLower().Contains(lowerFilter)) ||
                    (i.Description != null && i.Description.ToLower().Contains(lowerFilter)) ||
                    (i.Case != null && i.Case.Number != null && i.Case.Number.ToLower().Contains(lowerFilter)) ||
                    (i.Case != null && i.Case.Title != null && i.Case.Title.ToLower().Contains(lowerFilter)) ||
                    (i.Case != null && i.Case.Client != null &&
                        ((i.Case.Client.FirstName != null && i.Case.Client.FirstName.ToLower().Contains(lowerFilter)) ||
                         (i.Case.Client.LastName != null && i.Case.Client.LastName.ToLower().Contains(lowerFilter)))));
            }

            // Count total (in database)
            TotalDocuments = await query.CountAsync(ct);
            TotalPages = (int)Math.Ceiling((double)TotalDocuments / PAGE_SIZE);

            // Validate current page
            if (CurrentPage > TotalPages && TotalPages > 0)
            {
                CurrentPage = TotalPages;
            }

            // Load current page (in database)
            var skip = (CurrentPage - 1) * PAGE_SIZE;
            var documents = await query
                .OrderByDescending(i => i.Date)
                .ThenByDescending(i => i.CreatedAt)
                .Skip(skip)
                .Take(PAGE_SIZE) // ← CRITICAL FIX
                .ToListAsync(ct);

            if (ct.IsCancellationRequested) return;

            // Update UI
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Documents = new ObservableCollection<CaseItem>(documents);

                System.Diagnostics.Debug.WriteLine(
                    $"[DocumentsViewModel] Page {CurrentPage}/{TotalPages} - {Documents.Count} documents"
                );
            }, System.Windows.Threading.DispatcherPriority.Background, ct);
        }

        #endregion

        #region Search & Filter

        /// <summary>
        /// FIXED: Triggers database query instead of client-side filter
        /// </summary>
        partial void OnSearchFilterChanged(string value)
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] Search filter changed: {value}");

            CurrentPage = 1; // Reset to first page
            _ = InitializeAsync(); // Reload with filter
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task Refresh()
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Refresh requested");
            await InitializeAsync();
        }

        [RelayCommand]
        private void NextPage()
        {
            if (_disposed || CurrentPage >= TotalPages) return;

            CurrentPage++;
            _ = InitializeAsync();
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (_disposed || CurrentPage <= 1) return;

            CurrentPage--;
            _ = InitializeAsync();
        }

        [RelayCommand]
        private void OpenDocument(CaseItem? document)
        {
            if (_disposed || document == null)
            {
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Cannot open document");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] Opening document: {document.Name}");

            if (string.IsNullOrEmpty(document.FilePath))
            {
                MessageBox.Show(
                    "Aucun fichier associé à ce document",
                    "Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                if (System.IO.File.Exists(document.FilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = document.FilePath,
                        UseShellExecute = true
                    });
                    System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] ✅ Document opened");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] ❌ File not found: {document.FilePath}");
                    MessageBox.Show(
                        $"Fichier introuvable:\n{document.FilePath}",
                        "Erreur",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] ❌ Error opening document: {ex.Message}");
                MessageBox.Show(
                    $"Erreur lors de l'ouverture du fichier:\n\n{ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ViewCaseDetails(CaseItem? document)
        {
            if (_disposed || document?.Case == null)
            {
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Cannot view case");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] Viewing case: {document.Case.Number}");

            try
            {
                var dialog = new Views.Dialogs.CaseDialog(document.Case);
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] ❌ Error viewing case: {ex.Message}");
                MessageBox.Show(
                    $"Erreur lors de l'ouverture du dossier:\n\n{ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Disposing...");

            try
            {
                if (_loadCancellation != null)
                {
                    _loadCancellation.Cancel();
                    _loadCancellation.Dispose();
                    _loadCancellation = null;
                }

                Documents.Clear();

                _disposed = true;
                GC.SuppressFinalize(this);

                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] ✅ Disposal complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] ⚠️ Error during disposal: {ex.Message}");
            }
        }

        ~DocumentsViewModel()
        {
            if (!_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] ⚠️ WARNING: Finalizer called!");
            }
        }

        #endregion
    }
}