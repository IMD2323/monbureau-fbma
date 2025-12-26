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
using MonBureau.UI.Services;

namespace MonBureau.UI.Features.Expenses
{
    /// <summary>
    /// FIXED: Ensures PaymentMethod is never null and properly loads detached entities
    /// </summary>
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
        private string _paymentMethod = string.Empty; // FIXED: Never null

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

        [ObservableProperty]
        private bool _isLoading;

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
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("[ExpenseDialog] Loading cases and clients...");

                // Load cases and clients as detached entities (AsNoTracking)
                var casesTask = _unitOfWork.Cases.GetPagedAsync(0, 1000);
                var clientsTask = _unitOfWork.Clients.GetPagedAsync(0, 1000);

                await Task.WhenAll(casesTask, clientsTask);

                var cases = casesTask.Result;
                var clients = clientsTask.Result;

                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Loaded {cases.Count()} cases and {clients.Count()} clients");

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Cases = new ObservableCollection<Case>(cases.OrderByDescending(c => c.OpeningDate));
                    Clients = new ObservableCollection<Client>(clients.OrderBy(c => c.LastName));

                    if (expense != null)
                    {
                        LoadExpenseData(expense);
                    }
                    else
                    {
                        // Set default values for new expense
                        if (Cases.Any())
                        {
                            SelectedCase = Cases.First();
                        }
                    }
                });

                if (Cases.Count == 0)
                {
                    ErrorHandler.ShowWarning(
                        "Aucun dossier disponible.\n\n" +
                        "Veuillez d'abord créer un dossier avant d'ajouter une dépense.");
                }
            }
            catch (Exception ex)
            {
                var error = ErrorHandler.Handle(ex, "le chargement des données");
                ErrorHandler.ShowError(error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// FIXED: Ensures PaymentMethod is never null
        /// </summary>
        private void LoadExpenseData(Expense expense)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Loading expense: {expense.Description}");

                Description = expense.Description;
                AmountText = expense.Amount.ToString("F2");
                Date = expense.Date;
                SelectedCategory = expense.Category;

                // FIXED: Ensure PaymentMethod is never null for binding
                PaymentMethod = expense.PaymentMethod ?? string.Empty;

                Recipient = expense.Recipient;
                Notes = expense.Notes;
                ReceiptPath = expense.ReceiptPath;
                IsPaid = expense.IsPaid;

                // FIXED: Find matching case by ID (not by reference)
                SelectedCase = Cases.FirstOrDefault(c => c.Id == expense.CaseId);

                if (SelectedCase != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Selected case: {SelectedCase.Number}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] WARNING: Case with ID {expense.CaseId} not found");
                }

                // FIXED: Find matching client by ID (not by reference)
                if (expense.AddedByClientId.HasValue)
                {
                    AddedByClient = Clients.FirstOrDefault(c => c.Id == expense.AddedByClientId.Value);

                    if (AddedByClient != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Selected client: {AddedByClient.FirstName} {AddedByClient.LastName}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Payment Method loaded: '{PaymentMethod}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Error loading expense data: {ex.Message}");
            }
        }

        [RelayCommand]
        private void BrowseReceipt()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Sélectionner le reçu/facture",
                    Filter = "Images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|PDF (*.pdf)|*.pdf|Tous les fichiers (*.*)|*.*"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    ReceiptPath = openFileDialog.FileName;
                    System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Receipt selected: {ReceiptPath}");
                }
            }
            catch (Exception ex)
            {
                var error = ErrorHandler.Handle(ex, "la sélection du fichier");
                ErrorHandler.ShowError(error);
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
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Saving expense: {Description}, Amount: {amount}, Case: {SelectedCase.Number}");
                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Payment Method: '{PaymentMethod}'");

                if (_existingExpenseId.HasValue)
                {
                    // Update mode - create detached entity
                    System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Updating existing expense ID: {_existingExpenseId.Value}");

                    var expenseToSave = new Expense
                    {
                        Id = _existingExpenseId.Value,
                        Description = Description,
                        Amount = amount,
                        Date = Date,
                        Category = SelectedCategory,
                        PaymentMethod = string.IsNullOrWhiteSpace(PaymentMethod) ? null : PaymentMethod,
                        Recipient = Recipient,
                        Notes = Notes,
                        ReceiptPath = ReceiptPath,
                        IsPaid = IsPaid,
                        CaseId = SelectedCase.Id,
                        AddedByClientId = AddedByClient?.Id,
                        // Don't set navigation properties
                        Case = null,
                        AddedByClient = null
                    };

                    await _unitOfWork.Expenses.UpdateAsync(expenseToSave);
                }
                else
                {
                    // Create mode
                    System.Diagnostics.Debug.WriteLine("[ExpenseDialog] Creating new expense");

                    var expenseToSave = new Expense
                    {
                        Description = Description,
                        Amount = amount,
                        Date = Date,
                        Category = SelectedCategory,
                        PaymentMethod = string.IsNullOrWhiteSpace(PaymentMethod) ? null : PaymentMethod,
                        Recipient = Recipient,
                        Notes = Notes,
                        ReceiptPath = ReceiptPath,
                        IsPaid = IsPaid,
                        CaseId = SelectedCase.Id,
                        AddedByClientId = AddedByClient?.Id,
                        // Don't set navigation properties
                        Case = null,
                        AddedByClient = null
                    };

                    await _unitOfWork.Expenses.AddAsync(expenseToSave);
                }

                await _unitOfWork.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine("[ExpenseDialog] ✅ Expense saved successfully");

                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                var error = ErrorHandler.Handle(ex, "la sauvegarde de la dépense");
                ValidationError = error.UserMessage;
                ErrorHandler.ShowDetailedError(error);

                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Save error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ExpenseDialog] Inner exception: {ex.InnerException.Message}");
                }
            }
            finally
            {
                IsLoading = false;
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