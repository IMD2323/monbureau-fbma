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
    /// FIXED: Proper entity deletion without tracking conflicts
    /// </summary>
    public abstract partial class CrudViewModelBase<TEntity> : ViewModelBase, IDisposable
        where TEntity : class
    {
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

        public virtual async Task InitializeAsync()
        {
            if (_disposed) return;

            _loadCancellation?.Cancel();
            _loadCancellation = new CancellationTokenSource();

            IsBusy = true;
            BusyMessage = $"Chargement {GetEntityPluralName()}...";

            try
            {
                await LoadPageAsync(_loadCancellation.Token);
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

        protected virtual async Task LoadPageAsync(CancellationToken ct)
        {
            var filterExpression = BuildFilterExpression(SearchFilter);

            TotalItems = await GetRepository().CountAsync(filterExpression);
            TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);

            if (CurrentPage > TotalPages && TotalPages > 0)
            {
                CurrentPage = TotalPages;
            }

            var skip = (CurrentPage - 1) * PageSize;
            var pageData = await GetRepository().GetPagedAsync(skip, PageSize, filterExpression);

            if (ct.IsCancellationRequested) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Items.Clear();
                foreach (var item in pageData)
                {
                    Items.Add(item);
                }
            });
        }

        partial void OnSearchFilterChanged(string value)
        {
            CurrentPage = 1;
            _ = LoadPageAsync(new CancellationToken());
        }

        partial void OnCurrentPageChanged(int value)
        {
            _ = LoadPageAsync(new CancellationToken());
        }

        protected abstract Expression<Func<TEntity, bool>>? BuildFilterExpression(string searchText);

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
                // Get the entity ID before deletion
                var idProperty = typeof(TEntity).GetProperty("Id");
                var entityId = (int)(idProperty?.GetValue(entity) ?? 0);

                if (entityId > 0)
                {
                    // Load fresh entity from database
                    var entityToDelete = await GetRepository().GetByIdAsync(entityId);

                    if (entityToDelete != null)
                    {
                        await GetRepository().DeleteAsync(entityToDelete);
                        await _unitOfWork.SaveChangesAsync();
                        await RefreshAsync();
                        ShowSuccess("Supprimé avec succès");
                    }
                    else
                    {
                        ShowError("Élément introuvable");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Erreur: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CrudViewModel] Delete error: {ex.Message}");
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

        protected abstract IRepository<TEntity> GetRepository();
        protected abstract Window CreateAddDialog();
        protected abstract Window CreateEditDialog(TEntity entity);
        protected abstract string GetEntityName();
        protected abstract string GetEntityPluralName();
        protected abstract string GetEntityDisplayName(TEntity entity);

        protected bool? ShowDialog(Window dialog) => dialog.ShowDialog();

        protected virtual void ShowSuccess(string message)
        {
            MessageBox.Show(message, "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected virtual void ShowError(string message)
        {
            MessageBox.Show(message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}