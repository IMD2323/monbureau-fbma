using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonBureau.Core.Interfaces;
using MonBureau.UI.Services;

namespace MonBureau.UI.ViewModels.Base
{
    /// <summary>
    /// FIXED: Complete error handling for all CRUD operations
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
            StatusMessage = string.Empty;

            try
            {
                await LoadPageAsync(_loadCancellation.Token);
                StatusMessage = $"{TotalItems} {GetEntityPluralName()} chargé(s)";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Chargement annulé";
            }
            catch (Exception ex)
            {
                var error = ErrorHandler.Handle(ex, $"le chargement des {GetEntityPluralName()}");
                ErrorHandler.ShowError(error);
                StatusMessage = "Erreur de chargement";
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
            _ = SafeExecuteAsync(async () => await LoadPageAsync(new CancellationToken()),
                "la recherche");
        }

        partial void OnCurrentPageChanged(int value)
        {
            _ = SafeExecuteAsync(async () => await LoadPageAsync(new CancellationToken()),
                "le changement de page");
        }

        protected abstract Expression<Func<TEntity, bool>>? BuildFilterExpression(string searchText);

        [RelayCommand]
        protected virtual async Task AddAsync()
        {
            if (_disposed) return;

            await SafeExecuteAsync(async () =>
            {
                var dialog = CreateAddDialog();
                var result = ShowDialog(dialog);

                if (result == true)
                {
                    await RefreshAsync();
                    ErrorHandler.ShowSuccess($"{GetEntityName()} ajouté avec succès!");
                    StatusMessage = $"{GetEntityName()} ajouté";
                }
            }, $"l'ajout de {GetEntityName()}");
        }

        [RelayCommand]
        protected virtual async Task EditAsync(TEntity? entity)
        {
            if (_disposed || entity == null) return;

            await SafeExecuteAsync(async () =>
            {
                var dialog = CreateEditDialog(entity);
                var result = ShowDialog(dialog);

                if (result == true)
                {
                    await RefreshAsync();
                    ErrorHandler.ShowSuccess($"{GetEntityName()} modifié avec succès!");
                    StatusMessage = $"{GetEntityName()} modifié";
                }
            }, $"la modification de {GetEntityName()}");
        }

        [RelayCommand]
        protected virtual async Task DeleteAsync(TEntity? entity)
        {
            if (_disposed || entity == null) return;

            var displayName = GetEntityDisplayName(entity);

            var confirmed = ErrorHandler.Confirm(
                $"Êtes-vous sûr de vouloir supprimer {displayName}?\n\n" +
                "Cette action est irréversible.",
                "Confirmer la suppression");

            if (!confirmed) return;

            await SafeExecuteAsync(async () =>
            {
                IsBusy = true;
                BusyMessage = "Suppression en cours...";

                // Get the entity ID
                var idProperty = typeof(TEntity).GetProperty("Id");
                var entityId = (int)(idProperty?.GetValue(entity) ?? 0);

                if (entityId > 0)
                {
                    // Load fresh entity from database to avoid tracking conflicts
                    var entityToDelete = await GetRepository().GetByIdAsync(entityId);

                    if (entityToDelete != null)
                    {
                        await GetRepository().DeleteAsync(entityToDelete);
                        await _unitOfWork.SaveChangesAsync();
                        await RefreshAsync();

                        ErrorHandler.ShowSuccess($"{GetEntityName()} supprimé avec succès!");
                        StatusMessage = $"{GetEntityName()} supprimé";
                    }
                    else
                    {
                        ErrorHandler.ShowWarning("Élément introuvable. Il a peut-être déjà été supprimé.");
                        await RefreshAsync();
                    }
                }
            }, $"la suppression de {GetEntityName()}");
        }

        [RelayCommand]
        protected virtual async Task RefreshAsync()
        {
            StatusMessage = "Actualisation...";
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

        /// <summary>
        /// Safe execution wrapper with error handling
        /// </summary>
        protected async Task SafeExecuteAsync(Func<Task> action, string operationName)
        {
            if (_disposed) return;

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                var error = ErrorHandler.Handle(ex, operationName);
                ErrorHandler.ShowError(error);
                StatusMessage = $"Erreur : {operationName}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected abstract IRepository<TEntity> GetRepository();
        protected abstract Window CreateAddDialog();
        protected abstract Window CreateEditDialog(TEntity entity);
        protected abstract string GetEntityName();
        protected abstract string GetEntityPluralName();
        protected abstract string GetEntityDisplayName(TEntity entity);

        protected bool? ShowDialog(Window dialog) => dialog.ShowDialog();
    }
}