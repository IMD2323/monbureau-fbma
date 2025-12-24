using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MonBureau.Core.Entities;
using MonBureau.Core.Interfaces;
using MonBureau.UI.ViewModels.Base;
using MonBureau.UI.Views.Dialogs;

namespace MonBureau.UI.Features.Expenses
{
    /// <summary>
    /// FIXED: Properly inherits from CrudViewModelBase with all required properties
    /// </summary>
    public partial class ExpensesViewModel : CrudViewModelBase<Expense>
    {
        [ObservableProperty]
        private decimal _totalExpenses;

        [ObservableProperty]
        private decimal _paidExpenses;

        [ObservableProperty]
        private decimal _unpaidExpenses;

        [ObservableProperty]
        private int _expenseCount;

        public ExpensesViewModel(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
        }

        protected override IRepository<Expense> GetRepository()
            => _unitOfWork.Expenses;

        /// <summary>
        /// FIXED: Database-level filtering for expenses
        /// </summary>
        protected override Expression<Func<Expense, bool>>? BuildFilterExpression(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return null;

            var lowerSearch = searchText.ToLowerInvariant();

            return e =>
                (e.Description != null && e.Description.ToLower().Contains(lowerSearch)) ||
                (e.Recipient != null && e.Recipient.ToLower().Contains(lowerSearch)) ||
                (e.Notes != null && e.Notes.ToLower().Contains(lowerSearch)) ||
                (e.Case != null && e.Case.Number != null && e.Case.Number.ToLower().Contains(lowerSearch)) ||
                (e.Case != null && e.Case.Client != null &&
                    ((e.Case.Client.FirstName != null && e.Case.Client.FirstName.ToLower().Contains(lowerSearch)) ||
                     (e.Case.Client.LastName != null && e.Case.Client.LastName.ToLower().Contains(lowerSearch))));
        }

        /// <summary>
        /// FIXED: Override to calculate statistics after loading
        /// </summary>
        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await CalculateStatisticsAsync();
        }

        /// <summary>
        /// Calculate statistics from current page items
        /// Note: This only shows stats for current page - you may want to calculate from all items
        /// </summary>
        private async Task CalculateStatisticsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var expenses = Items;

                    // Use the backing field names (lowercase first letter)
                    _totalExpenses = expenses.Sum(e => e.Amount);
                    _paidExpenses = expenses.Where(e => e.IsPaid).Sum(e => e.Amount);
                    _unpaidExpenses = expenses.Where(e => !e.IsPaid).Sum(e => e.Amount);
                    _expenseCount = expenses.Count;

                    // Manually trigger property change notifications
                    OnPropertyChanged(nameof(TotalExpenses));
                    OnPropertyChanged(nameof(PaidExpenses));
                    OnPropertyChanged(nameof(UnpaidExpenses));
                    OnPropertyChanged(nameof(ExpenseCount));

                    System.Diagnostics.Debug.WriteLine(
                        $"[ExpensesViewModel] Stats - Total: {TotalExpenses:C}, Paid: {PaidExpenses:C}, Unpaid: {UnpaidExpenses:C}"
                    );
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExpensesViewModel] Error calculating statistics: {ex.Message}");
            }
        }

        /// <summary>
        /// FIXED: Create dialog with proper ViewModel initialization
        /// </summary>
        protected override Window CreateAddDialog()
        {
            var dialog = new ExpenseDialog();
            var viewModel = App.GetService<ExpenseDialogViewModel>();
            dialog.DataContext = viewModel;
            return dialog;
        }

        /// <summary>
        /// FIXED: Create edit dialog with entity
        /// </summary>
        protected override Window CreateEditDialog(Expense entity)
        {
            var dialog = new ExpenseDialog();

            // Create ViewModel with existing expense
            var viewModel = new ExpenseDialogViewModel(_unitOfWork, entity);
            dialog.DataContext = viewModel;

            return dialog;
        }

        protected override string GetEntityName()
            => "Dépense";

        protected override string GetEntityPluralName()
            => "Dépenses";

        protected override string GetEntityDisplayName(Expense entity)
            => entity.Description;

        /// <summary>
        /// FIXED: Refresh statistics after any CRUD operation
        /// </summary>
        protected override async Task RefreshAsync()
        {
            await base.RefreshAsync();
            await CalculateStatisticsAsync();
        }
    }
}