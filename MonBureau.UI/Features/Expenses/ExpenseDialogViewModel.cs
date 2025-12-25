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
    public partial class ExpenseDialogViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private int? _existingExpenseId;

        [ObservableProperty]
        private string _description = string.Empty;

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

        public bool IsEditMode => _existingExpenseId.HasValue;

        public Array ExpenseCategories => Enum.GetValues(typeof(ExpenseCategory));

        public ExpenseDialogViewModel(IUnitOfWork unitOfWork, Expense? expense = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

            if (expense != null)
            {
                _existingExpenseId = expense.Id;
            }

            _ = LoadDataAsync(expense);
        }

        private async Task LoadDataAsync(Expense? expense)
        {
            try
            {
                // Load cases and clients
                var casesTask = _unitOfWork.Cases.GetPagedAsync(0, 1000);
                var clientsTask = _unitOfWork.Clients.GetPagedAsync(0, 1000);

                await Task.WhenAll(casesTask, clientsTask);

                var cases = casesTask.Result;
                var clients = clientsTask.Result;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Cases = new ObservableCollection<Case>(cases.OrderByDescending(c => c.OpeningDate));
                    Clients = new ObservableCollection<Client>(clients.OrderBy(c => c.FullName));

                    if (expense != null)
                    {
                        LoadExpenseData(expense);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Error loading data: {ex.Message}");
            }
        }

        private void LoadExpenseData(Expense expense)
        {
            Description = expense.Description;
            AmountText = expense.Amount.ToString("F2");
            Date = expense.Date;
            SelectedCategory = expense.Category;
            PaymentMethod = expense.PaymentMethod;
            Recipient = expense.Recipient;
            Notes = expense.Notes;
            ReceiptPath = expense.ReceiptPath;
            IsPaid = expense.IsPaid;

            // Find and select case
            SelectedCase = Cases.FirstOrDefault(c => c.Id == expense.CaseId);

            // Find and select client
            if (expense.AddedByClientId.HasValue)
            {
                AddedByClient = Clients.FirstOrDefault(c => c.Id == expense.AddedByClientId.Value);
            }
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
                Expense expenseToSave;

                if (_existingExpenseId.HasValue)
                {
                    // Edit mode - load fresh entity
                    expenseToSave = await _unitOfWork.Expenses.GetByIdAsync(_existingExpenseId.Value);
                    if (expenseToSave == null)
                    {
                        ValidationError = "Dépense introuvable";
                        return;
                    }

                    // Update properties
                    expenseToSave.Description = Description;
                    expenseToSave.Amount = amount;
                    expenseToSave.Date = Date;
                    expenseToSave.Category = SelectedCategory;
                    expenseToSave.PaymentMethod = PaymentMethod;
                    expenseToSave.Recipient = Recipient;
                    expenseToSave.Notes = Notes;
                    expenseToSave.ReceiptPath = ReceiptPath;
                    expenseToSave.IsPaid = IsPaid;
                    expenseToSave.CaseId = SelectedCase.Id;
                    expenseToSave.AddedByClientId = AddedByClient?.Id;

                    await _unitOfWork.Expenses.UpdateAsync(expenseToSave);
                }
                else
                {
                    // Create mode
                    expenseToSave = new Expense
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

                    await _unitOfWork.Expenses.AddAsync(expenseToSave);
                }

                await _unitOfWork.SaveChangesAsync();

                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                ValidationError = $"Erreur: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Save error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Stack trace: {ex.StackTrace}");
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