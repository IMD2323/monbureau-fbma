using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Core.Interfaces;

namespace MonBureau.UI.Features.Expenses
{
    /// <summary>
    /// FIXED: Proper decimal binding and validation
    /// </summary>
    public partial class ExpenseDialogViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Expense? _existingExpense;

        [ObservableProperty]
        private string _description = string.Empty;

        // FIXED: Use string for amount to handle decimal input properly
        [ObservableProperty]
        private string _amountText = "0";

        [ObservableProperty]
        private DateTime _date = DateTime.Today;

        [ObservableProperty]
        private ExpenseCategory _selectedCategory;

        [ObservableProperty]
        private string? _paymentMethod;

        [ObservableProperty]
        private string? _recipient;

        [ObservableProperty]
        private string? _notes;

        [ObservableProperty]
        private string? _receiptPath;

        [ObservableProperty]
        private bool _isPaid;

        [ObservableProperty]
        private Case? _selectedCase;

        [ObservableProperty]
        private Client? _addedByClient;

        [ObservableProperty]
        private ObservableCollection<Case> _cases = new();

        [ObservableProperty]
        private ObservableCollection<Client> _clients = new();

        [ObservableProperty]
        private string? _validationError;

        public bool IsEditMode => _existingExpense != null;

        public Array ExpenseCategories => Enum.GetValues(typeof(ExpenseCategory));

        public ExpenseDialogViewModel(IUnitOfWork unitOfWork, Expense? expense = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _existingExpense = expense;

            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var cases = await _unitOfWork.Cases.GetPagedAsync(0, 1000);
                var clients = await _unitOfWork.Clients.GetPagedAsync(0, 1000);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Cases = new ObservableCollection<Case>(cases.OrderByDescending(c => c.OpeningDate));
                    Clients = new ObservableCollection<Client>(clients.OrderBy(c => c.FullName));

                    if (_existingExpense != null)
                    {
                        LoadExpenseData();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Error loading data: {ex.Message}");
            }
        }

        private void LoadExpenseData()
        {
            if (_existingExpense == null) return;

            Description = _existingExpense.Description;
            AmountText = _existingExpense.Amount.ToString("F2");
            Date = _existingExpense.Date;
            SelectedCategory = _existingExpense.Category;
            PaymentMethod = _existingExpense.PaymentMethod;
            Recipient = _existingExpense.Recipient;
            Notes = _existingExpense.Notes;
            ReceiptPath = _existingExpense.ReceiptPath;
            IsPaid = _existingExpense.IsPaid;

            SelectedCase = Cases.FirstOrDefault(c => c.Id == _existingExpense.CaseId);
            AddedByClient = Clients.FirstOrDefault(c => c.Id == _existingExpense.AddedByClientId);
        }

        [RelayCommand]
        private void BrowseReceipt()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Sélectionner le reçu/facture",
                Filter = "Images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|PDF (*.pdf)|*.pdf|Tous les fichiers (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ReceiptPath = openFileDialog.FileName;
            }
        }

        [RelayCommand]
        private async Task Save(Window window)
        {
            ValidationError = null;

            // Validate
            if (string.IsNullOrWhiteSpace(Description))
            {
                ValidationError = "La description est obligatoire";
                return;
            }

            // FIXED: Parse amount from string
            if (!decimal.TryParse(AmountText, out var amount) || amount <= 0)
            {
                ValidationError = "Le montant doit être un nombre valide supérieur à 0";
                return;
            }

            if (SelectedCase == null)
            {
                ValidationError = "Veuillez sélectionner un dossier";
                return;
            }

            try
            {
                if (_existingExpense != null)
                {
                    // Update existing
                    _existingExpense.Description = Description;
                    _existingExpense.Amount = amount;
                    _existingExpense.Date = Date;
                    _existingExpense.Category = SelectedCategory;
                    _existingExpense.PaymentMethod = PaymentMethod;
                    _existingExpense.Recipient = Recipient;
                    _existingExpense.Notes = Notes;
                    _existingExpense.ReceiptPath = ReceiptPath;
                    _existingExpense.IsPaid = IsPaid;
                    _existingExpense.CaseId = SelectedCase.Id;
                    _existingExpense.AddedByClientId = AddedByClient?.Id;

                    await _unitOfWork.Expenses.UpdateAsync(_existingExpense);
                }
                else
                {
                    // Create new
                    var expense = new Expense
                    {
                        Description = Description,
                        Amount = amount,
                        Date = Date,
                        Category = SelectedCategory,
                        PaymentMethod = PaymentMethod,
                        Recipient = Recipient,
                        Notes = Notes,
                        ReceiptPath = ReceiptPath,
                        IsPaid = IsPaid,
                        CaseId = SelectedCase.Id,
                        AddedByClientId = AddedByClient?.Id
                    };

                    await _unitOfWork.Expenses.AddAsync(expense);
                }

                await _unitOfWork.SaveChangesAsync();
                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                ValidationError = $"Erreur: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Save error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}