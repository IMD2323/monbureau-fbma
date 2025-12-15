using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonBureau.Core.Interfaces;

namespace MonBureau.UI.ViewModels.Base
{
    /// <summary>
    /// FIXED: True server-side pagination and filtering
    /// No more loading entire tables into memory
    /// </summary>
    public abstract partial class CrudViewModelBase<TEntity> : ViewModelBase, IDisposable
        where TEntity : class
    {
        #region Fields & Properties

        protected readonly IUnitOfWork _unitOfWork;
        private CancellationTokenSource? _loadCancellation;
        private bool _disposed;

        [ObservableProperty]
        private ObservableCollection<TEntity> _items = new();

        [ObservableProperty]
        private TEntity? _selectedItem;

        [ObservableProperty]
        private string _searchFilter = string.Empty;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _pageSize = 50;

        [ObservableProperty]
        private int _totalItems;

        [ObservableProperty]
        private int _totalPages;

        // REMOVED: No more _allItems list (memory leak)
        // All filtering happens in database

        #endregion

        #region Constructor & Disposal

        protected CrudViewModelBase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public virtual void Dispose()
        {
            if (_disposed) return;

            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;

            Items.Clear();

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Initialization

        public virtual async Task InitializeAsync()
        {
            if (_disposed) return;

            _loadCancellation?.Cancel();
            _loadCancellation = new CancellationTokenSource();

            IsBusy = true;
            BusyMessage = $"Chargement {GetEntityPluralName()}...";

            try
            {
                await LoadPageAsync(GetTotalPages(), _loadCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Cancelled, ignore
            }
            catch (Exception ex)
            {
                ShowError($"Erreur: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected virtual int GetTotalPages()
        {
            return TotalPages;
        }

        /// <summary>
        /// FIXED: Loads single page from database
        /// No in-memory filtering
        /// </summary>
        protected virtual async Task LoadPageAsync(int totalPages, CancellationToken ct)
        {
            // Build filter expression from search text
            var filterExpression = BuildFilterExpression(SearchFilter);

            // Count total matching records (in database)
            TotalItems = await GetRepository().CountAsync(filterExpression);
            totalPages = (int)Math.Ceiling((double)TotalItems / PageSize);

            // Ensure current page is valid
            if (CurrentPage > TotalPages && TotalPages > 0)
            {
                CurrentPage = TotalPages;
            }

            // Load current page (in database)
            var skip = (CurrentPage - 1) * PageSize;
            var pageData = await GetRepository().GetPagedAsync(
                skip,
                PageSize,
                filterExpression
            );

            if (ct.IsCancellationRequested) return;

            // Update UI
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Items.Clear();
                foreach (var item in pageData)
                {
                    Items.Add(item);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[CrudViewModel] Loaded page {CurrentPage}/{TotalPages} ({Items.Count} items)"
                );
            });
        }

        #endregion

        #region Search & Filter

        /// <summary>
        /// FIXED: Triggers database query instead of in-memory filter
        /// </summary>
        partial void OnSearchFilterChanged(string value)
        {
            CurrentPage = 1; // Reset to first page
            _ = LoadPageAsync(GetTotalPages(), new CancellationToken()); // Trigger reload
        }

        /// <summary>
        /// FIXED: Navigates to different page (database query)
        /// </summary>
        partial void OnCurrentPageChanged(int value)
        {
            _ = LoadPageAsync(GetTotalPages(), new CancellationToken());
        }

        /// <summary>
        /// Override to build entity-specific filter expressions
        /// This executes in DATABASE, not in-memory
        /// </summary>
        protected abstract Expression<Func<TEntity, bool>>? BuildFilterExpression(string searchText);

        // REMOVED: ApplyFilter() - no longer needed
        // REMOVED: FilterByProperties() - replaced by BuildFilterExpression()

        #endregion

        #region CRUD Commands

        [RelayCommand]
        protected virtual async Task AddAsync()
        {
            if (_disposed) return;

            try
            {
                var dialog = CreateAddDialog();
                var result = ShowDialog(dialog);

                if (result == true)
                {
                    await RefreshAsync();
                    ShowSuccess($"{GetEntityName()} ajouté!");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Erreur: {ex.Message}");
            }
        }

        [RelayCommand]
        protected virtual async Task EditAsync(TEntity? entity)
        {
            if (_disposed || entity == null) return;

            try
            {
                var dialog = CreateEditDialog(entity);
                var result = ShowDialog(dialog);

                if (result == true)
                {
                    await RefreshAsync();
                    ShowSuccess($"{GetEntityName()} modifié!");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Erreur: {ex.Message}");
            }
        }

        [RelayCommand]
        protected virtual async Task DeleteAsync(TEntity? entity)
        {
            if (_disposed || entity == null) return;

            var confirmed = MessageBox.Show(
                $"Supprimer {GetEntityDisplayName(entity)}?",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (confirmed != MessageBoxResult.Yes) return;

            IsBusy = true;

            try
            {
                await GetRepository().DeleteAsync(entity);
                await _unitOfWork.SaveChangesAsync();
                await RefreshAsync();
                ShowSuccess("Supprimé avec succès");
            }
            catch (Exception ex)
            {
                ShowError($"Erreur: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        protected virtual async Task RefreshAsync()
        {
            await InitializeAsync();
        }

        [RelayCommand]
        protected virtual void NextPage()
        {
            if (CurrentPage < TotalPages)
                CurrentPage++;
        }

        [RelayCommand]
        protected virtual void PreviousPage()
        {
            if (CurrentPage > 1)
                CurrentPage--;
        }

        #endregion

        #region Abstract Methods

        protected abstract IRepository<TEntity> GetRepository();
        protected abstract Window CreateAddDialog();
        protected abstract Window CreateEditDialog(TEntity entity);
        protected abstract string GetEntityName();
        protected abstract string GetEntityPluralName();
        protected abstract string GetEntityDisplayName(TEntity entity);

        #endregion

        #region Helper Methods

        protected bool? ShowDialog(Window dialog) => dialog.ShowDialog();

        protected virtual void ShowSuccess(string message)
        {
            MessageBox.Show(message, "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected virtual void ShowError(string message)
        {
            MessageBox.Show(message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion
    }
}