using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonBureau.Core.Interfaces;

namespace MonBureau.UI.ViewModels.Base
{
    /// <summary>
    /// SIMPLIFIED: Generic CRUD base ViewModel
    /// Eliminates 90% of repetitive CRUD code
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

        protected List<TEntity> _allItems = new();

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
            _allItems.Clear();

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
                await LoadDataAsync(_loadCancellation.Token);
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

        protected virtual async Task LoadDataAsync(CancellationToken ct)
        {
            var items = await Task.Run(async () =>
            {
                return (await GetRepository().GetAllAsync()).ToList();
            }, ct);

            if (ct.IsCancellationRequested) return;

            _allItems = items;
            TotalItems = _allItems.Count;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ApplyFilter();
            });
        }

        #endregion

        #region Search & Filter

        partial void OnSearchFilterChanged(string value)
        {
            CurrentPage = 1;
            ApplyFilter();
        }

        partial void OnCurrentPageChanged(int value)
        {
            ApplyFilter();
        }

        protected abstract void ApplyFilter();

        protected IEnumerable<TEntity> FilterByProperties(
            IEnumerable<TEntity> source,
            params Func<TEntity, string?>[] propertySelectors)
        {
            if (string.IsNullOrWhiteSpace(SearchFilter))
                return source;

            var lowerFilter = SearchFilter.ToLowerInvariant();

            return source.Where(item =>
                propertySelectors.Any(selector =>
                {
                    var value = selector(item);
                    return value?.ToLowerInvariant().Contains(lowerFilter) ?? false;
                })
            );
        }

        protected IEnumerable<TEntity> ApplyPagination(IEnumerable<TEntity> source)
        {
            var skip = (CurrentPage - 1) * PageSize;
            return source.Skip(skip).Take(PageSize);
        }

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
            if ((CurrentPage * PageSize) < TotalItems)
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

        protected void RefreshItemsCollection(IEnumerable<TEntity> filtered)
        {
            var paginated = ApplyPagination(filtered);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Items.Clear();
                foreach (var item in paginated)
                    Items.Add(item);
            });
        }

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