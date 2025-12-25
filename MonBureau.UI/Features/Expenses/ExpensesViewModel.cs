using System;
using System.Linq;
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

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await CalculateStatisticsAsync();
        }

        private async Task CalculateStatisticsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var expenses = Items.ToList();

                    TotalExpenses = expenses.Sum(e => e.Amount);
                    PaidExpenses = expenses.Where(e => e.IsPaid).Sum(e => e.Amount);
                    UnpaidExpenses = expenses.Where(e => !e.IsPaid).Sum(e => e.Amount);
                    ExpenseCount = expenses.Count;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExpensesViewModel] Error calculating statistics: {ex.Message}");
            }
        }

        protected override Window CreateAddDialog()
        {
            var dialog = new ExpenseDialog();
            var viewModel = new ExpenseDialogViewModel(_unitOfWork);
            dialog.DataContext = viewModel;
            return dialog;
        }

        protected override Window CreateEditDialog(Expense entity)
        {
            var dialog = new ExpenseDialog();
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

        protected override async Task RefreshAsync()
        {
            await base.RefreshAsync();
            await CalculateStatisticsAsync();
        }
    }
}