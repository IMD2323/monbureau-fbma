using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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
    /// DocumentsViewModel - Manages document listing and operations
    /// Follows same architecture as DashboardViewModel, ClientsViewModel, etc.
    /// </summary>
    public partial class DocumentsViewModel : ObservableObject, IDisposable
    {
        private readonly AppDbContext _context;
        private CancellationTokenSource? _loadCancellation;
        private bool _disposed;

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
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Already disposed, skipping initialization");
                return;
            }

            // Cancel any existing load operation
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var ct = _loadCancellation.Token;

            try
            {
                IsLoading = true;
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Loading documents...");

                await LoadDocumentsAsync(ct);

                System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] ✅ Loaded {TotalDocuments} documents");
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

        private async Task LoadDocumentsAsync(CancellationToken ct)
        {
            // Load all documents with related case and client info
            var documents = await _context.CaseItems
                .AsNoTracking()
                .Include(i => i.Case)
                    .ThenInclude(c => c.Client)
                .Where(i => i.Type == ItemType.Document)
                .OrderByDescending(i => i.Date)
                .ThenByDescending(i => i.CreatedAt)
                .ToListAsync(ct);

            if (ct.IsCancellationRequested) return;

            // Update UI on dispatcher thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Documents = new ObservableCollection<CaseItem>(documents);
                TotalDocuments = documents.Count;
            }, System.Windows.Threading.DispatcherPriority.Background, ct);
        }

        #endregion

        #region Search & Filter

        partial void OnSearchFilterChanged(string value)
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] Search filter changed: {value}");

            if (string.IsNullOrWhiteSpace(value))
            {
                // Reload all documents
                _ = InitializeAsync();
                return;
            }

            try
            {
                var lowerFilter = value.ToLowerInvariant();

                // Client-side filtering for better performance
                var allDocs = Documents.ToList();
                var filtered = allDocs
                    .Where(d =>
                        (d.Name?.ToLowerInvariant().Contains(lowerFilter) ?? false) ||
                        (d.Description?.ToLowerInvariant().Contains(lowerFilter) ?? false) ||
                        (d.Case?.Number?.ToLowerInvariant().Contains(lowerFilter) ?? false) ||
                        (d.Case?.Title?.ToLowerInvariant().Contains(lowerFilter) ?? false) ||
                        (d.Case?.Client?.FullName?.ToLowerInvariant().Contains(lowerFilter) ?? false))
                    .ToList();

                Documents = new ObservableCollection<CaseItem>(filtered);
                TotalDocuments = filtered.Count;
                System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] Filtered to {TotalDocuments} documents");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] Filter error: {ex.Message}");
            }
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task Refresh()
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Refresh requested");
            SearchFilter = string.Empty; // Clear filter
            await InitializeAsync();
        }

        [RelayCommand]
        private void OpenDocument(CaseItem? document)
        {
            if (_disposed || document == null)
            {
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Cannot open document - disposed or null");
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
                    System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] ✅ Document opened successfully");
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
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Cannot view case - disposed or null");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] Viewing case: {document.Case.Number}");

            // Open case dialog for viewing/editing
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
                // Cancel and dispose CancellationTokenSource
                if (_loadCancellation != null)
                {
                    _loadCancellation.Cancel();
                    _loadCancellation.Dispose();
                    _loadCancellation = null;
                    System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] CancellationToken disposed");
                }

                // Clear collections
                Documents.Clear();
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] Collections cleared");

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
                System.Diagnostics.Debug.WriteLine("[DocumentsViewModel] ⚠️ WARNING: Finalizer called - Dispose() not called!");
                Dispose();
            }
        }

        #endregion
    }
}