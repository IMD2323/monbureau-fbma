using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Core.Interfaces;
using MonBureau.Core.Validation;
using MonBureau.Core.Validators;
using ValidationResult = MonBureau.Core.Validation.ValidationResult;

namespace MonBureau.UI.Views.Dialogs
{
    /// <summary>
    /// FIXED: Proper control type handling for DatePicker vs TextBox
    /// </summary>
    public partial class EntityDialog : Window
    {
        private readonly object _entity;
        private readonly string _entityType;
        private readonly IUnitOfWork _unitOfWork;
        private readonly Dictionary<string, Control> _controls = new();

        public bool SavedSuccessfully { get; private set; }

        // Constructor for Client
        public EntityDialog(Client? client = null)
        {
            InitializeComponent();
            _unitOfWork = App.GetService<IUnitOfWork>();
            _entity = client ?? new Client();
            _entityType = "Client";
            InitializeForm();
        }

        // Constructor for Case
        public EntityDialog(Case? caseEntity = null)
        {
            InitializeComponent();
            _unitOfWork = App.GetService<IUnitOfWork>();
            _entity = caseEntity ?? new Case();
            _entityType = "Case";
            InitializeForm();
        }

        // Constructor for CaseItem
        public EntityDialog(CaseItem? item = null)
        {
            InitializeComponent();
            _unitOfWork = App.GetService<IUnitOfWork>();
            _entity = item ?? new CaseItem();
            _entityType = "CaseItem";
            InitializeForm();
        }

        private void InitializeForm()
        {
            Title = _entity is { } && GetEntityId() > 0
                ? $"Modifier {GetEntityDisplayName()}"
                : $"Nouveau {GetEntityDisplayName()}";

            TitleText.Text = Title;

            switch (_entityType)
            {
                case "Client":
                    BuildClientForm();
                    break;
                case "Case":
                    BuildCaseForm();
                    break;
                case "CaseItem":
                    BuildItemForm();
                    break;
            }
        }

        #region Form Builders

        private void BuildClientForm()
        {
            var client = (Client)_entity;

            AddTextField("FirstName", "Prénom *", client.FirstName);
            AddTextField("LastName", "Nom *", client.LastName);
            AddTextField("ContactEmail", "Email", client.ContactEmail ?? "");
            AddTextField("ContactPhone", "Téléphone", client.ContactPhone ?? "");
            AddTextField("Address", "Adresse", client.Address ?? "", multiline: true);
            AddTextField("Notes", "Notes", client.Notes ?? "", multiline: true);
        }

        private void BuildCaseForm()
        {
            var caseEntity = (Case)_entity;

            AddTextField("Number", "Numéro *", caseEntity.Number);
            AddTextField("Title", "Titre *", caseEntity.Title);
            AddTextField("Description", "Description", caseEntity.Description ?? "", multiline: true);

            AddClientSelector("ClientId", "Client *", caseEntity.ClientId);

            AddComboBox("Status", "Statut",
                Enum.GetValues<CaseStatus>().Cast<object>().ToArray(),
                (int)caseEntity.Status,
                item => ((CaseStatus)item).ToString());

            AddDatePicker("OpeningDate", "Date d'ouverture", caseEntity.OpeningDate);
            AddDatePicker("ClosingDate", "Date de clôture", caseEntity.ClosingDate);

            AddTextField("CourtName", "Tribunal", caseEntity.CourtName ?? "");
            AddTextField("CourtRoom", "Salle", caseEntity.CourtRoom ?? "");
            AddTextField("CourtAddress", "Adresse du tribunal", caseEntity.CourtAddress ?? "", multiline: true);
            AddTextField("CourtContact", "Contact tribunal", caseEntity.CourtContact ?? "");
        }

        private void BuildItemForm()
        {
            var item = (CaseItem)_entity;

            AddCaseSelector("CaseId", "Dossier *", item.CaseId);

            AddComboBox("Type", "Type *",
                Enum.GetValues<ItemType>().Cast<object>().ToArray(),
                (int)item.Type,
                itemType => ((ItemType)itemType).ToString());

            AddTextField("Name", "Nom *", item.Name);
            AddTextField("Description", "Description", item.Description ?? "", multiline: true);
            AddDatePicker("Date", "Date", item.Date);
            AddTextField("Amount", "Montant (DA)", item.Amount?.ToString() ?? "");
            AddTextField("FilePath", "Chemin du fichier", item.FilePath ?? "");
        }

        private void AddTextField(string name, string label, string value, bool multiline = false)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush")
            };

            var textBox = new TextBox
            {
                Name = name,
                Text = value,
                Style = (Style)FindResource("ModernTextBox")
            };

            if (multiline)
            {
                textBox.MinHeight = 80;
                textBox.AcceptsReturn = true;
                textBox.TextWrapping = TextWrapping.Wrap;
            }

            _controls[name] = textBox;

            stack.Children.Add(labelBlock);
            stack.Children.Add(textBox);
            FormContainer.Children.Add(stack);
        }

        private void AddDatePicker(string name, string label, DateTime? value)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush")
            };

            var datePicker = new DatePicker
            {
                Name = name,
                SelectedDate = value,
                Padding = new Thickness(12, 10, 12, 10), // Fixed the issue by providing all four parameters  
                FontSize = 14,
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1)
            };

            _controls[name] = datePicker;

            stack.Children.Add(labelBlock);
            stack.Children.Add(datePicker);
            FormContainer.Children.Add(stack);
        }

        private void AddComboBox(string name, string label, object[] items, int selectedIndex, Func<object, string> displayFunc)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush")
            };

            var comboBox = new ComboBox
            {
                Name = name,
                ItemsSource = items,
                SelectedIndex = selectedIndex,
                Padding = new Thickness(12, 10, 12, 10), // Fixed the issue by providing all four parameters  
                FontSize = 14,
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1)
            };

            _controls[name] = comboBox;

            stack.Children.Add(labelBlock);
            stack.Children.Add(comboBox);
            FormContainer.Children.Add(stack);
        }

        private void AddClientSelector(string name, string label, int selectedClientId)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush")
            };

            var clients = _unitOfWork.Clients.GetAllAsync().Result.ToList();

            var comboBox = new ComboBox
            {
                Name = name,
                ItemsSource = clients,
                DisplayMemberPath = "FullName",
                SelectedValuePath = "Id",
                SelectedValue = selectedClientId,
                Padding = new Thickness(12, 10, 12, 10), // Fixed the issue by providing all four parameters  
                FontSize = 14,
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1)
            };

            _controls[name] = comboBox;

            stack.Children.Add(labelBlock);
            stack.Children.Add(comboBox);
            FormContainer.Children.Add(stack);
        }

        private void AddCaseSelector(string name, string label, int selectedCaseId)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush")
            };

            var cases = _unitOfWork.Cases.GetAllAsync().Result.ToList();

            var comboBox = new ComboBox
            {
                Name = name,
                ItemsSource = cases,
                DisplayMemberPath = "Number",
                SelectedValuePath = "Id",
                SelectedValue = selectedCaseId,
                Padding = new Thickness(12, 10, 12, 10), // Fixed the issue by providing all four parameters  
                FontSize = 14,
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1)
            };

            _controls[name] = comboBox;

            stack.Children.Add(labelBlock);
            stack.Children.Add(comboBox);
            FormContainer.Children.Add(stack);
        }

        #endregion

        #region Save Logic

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveButton.IsEnabled = false;
                ErrorMessageText.Visibility = Visibility.Collapsed;

                // Update entity from form
                UpdateEntityFromForm();

                // Validate
                var validationResult = ValidateEntity();
                if (!validationResult.IsValid)
                {
                    ShowValidationErrors(validationResult);
                    SaveButton.IsEnabled = true;
                    return;
                }

                // Save to database
                var entityId = GetEntityId();
                if (entityId == 0)
                {
                    // Add new
                    await AddEntityAsync();
                }
                else
                {
                    // Update existing
                    await UpdateEntityAsync();
                }

                await _unitOfWork.SaveChangesAsync();

                SavedSuccessfully = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur lors de la sauvegarde: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SaveButton.IsEnabled = true;
            }
        }

        private void UpdateEntityFromForm()
        {
            switch (_entityType)
            {
                case "Client":
                    UpdateClientFromForm();
                    break;
                case "Case":
                    UpdateCaseFromForm();
                    break;
                case "CaseItem":
                    UpdateItemFromForm();
                    break;
            }
        }

        private void UpdateClientFromForm()
        {
            var client = (Client)_entity;
            client.FirstName = GetTextValue("FirstName");
            client.LastName = GetTextValue("LastName");
            client.ContactEmail = GetTextValue("ContactEmail");
            client.ContactPhone = GetTextValue("ContactPhone");
            client.Address = GetTextValue("Address");
            client.Notes = GetTextValue("Notes");
        }

        private void UpdateCaseFromForm()
        {
            var caseEntity = (Case)_entity;
            caseEntity.Number = GetTextValue("Number");
            caseEntity.Title = GetTextValue("Title");
            caseEntity.Description = GetTextValue("Description");
            caseEntity.ClientId = GetComboBoxValue<int>("ClientId");
            caseEntity.Status = (CaseStatus)GetComboBoxSelectedIndex("Status");
            caseEntity.OpeningDate = GetDateValue("OpeningDate") ?? DateTime.Today;
            caseEntity.ClosingDate = GetDateValue("ClosingDate");
            caseEntity.CourtName = GetTextValue("CourtName");
            caseEntity.CourtRoom = GetTextValue("CourtRoom");
            caseEntity.CourtAddress = GetTextValue("CourtAddress");
            caseEntity.CourtContact = GetTextValue("CourtContact");
        }

        private void UpdateItemFromForm()
        {
            var item = (CaseItem)_entity;
            item.CaseId = GetComboBoxValue<int>("CaseId");
            item.Type = (ItemType)GetComboBoxSelectedIndex("Type");
            item.Name = GetTextValue("Name");
            item.Description = GetTextValue("Description");
            item.Date = GetDateValue("Date") ?? DateTime.Today;

            var amountText = GetTextValue("Amount");
            item.Amount = decimal.TryParse(amountText, out var amount) ? amount : (decimal?)null;

            item.FilePath = GetTextValue("FilePath");
        }

        private ValidationResult ValidateEntity()
        {
            return _entityType switch
            {
                "Client" => EntityValidator.ValidateClient((Client)_entity),
                "Case" => EntityValidator.ValidateCase((Case)_entity),
                "CaseItem" => EntityValidator.ValidateCaseItem((CaseItem)_entity),
                _ => new ValidationResult()
            };
        }

        private async System.Threading.Tasks.Task AddEntityAsync()
        {
            switch (_entityType)
            {
                case "Client":
                    await _unitOfWork.Clients.AddAsync((Client)_entity);
                    break;
                case "Case":
                    await _unitOfWork.Cases.AddAsync((Case)_entity);
                    break;
                case "CaseItem":
                    await _unitOfWork.CaseItems.AddAsync((CaseItem)_entity);
                    break;
            }
        }

        private async System.Threading.Tasks.Task UpdateEntityAsync()
        {
            switch (_entityType)
            {
                case "Client":
                    await _unitOfWork.Clients.UpdateAsync((Client)_entity);
                    break;
                case "Case":
                    await _unitOfWork.Cases.UpdateAsync((Case)_entity);
                    break;
                case "CaseItem":
                    await _unitOfWork.CaseItems.UpdateAsync((CaseItem)_entity);
                    break;
            }
        }

        #endregion

        #region Helpers - FIXED to handle DatePicker correctly

        /// <summary>
        /// FIXED: Get text value from TextBox only
        /// </summary>
        private string GetTextValue(string name)
        {
            if (_controls.TryGetValue(name, out var control))
            {
                if (control is TextBox textBox)
                {
                    return textBox.Text.Trim();
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// FIXED: Get date value from DatePicker only
        /// </summary>
        private DateTime? GetDateValue(string name)
        {
            if (_controls.TryGetValue(name, out var control))
            {
                if (control is DatePicker datePicker)
                {
                    return datePicker.SelectedDate;
                }
            }
            return null;
        }

        private T GetComboBoxValue<T>(string name)
        {
            if (_controls.TryGetValue(name, out var control) && control is ComboBox comboBox)
            {
                return (T)comboBox.SelectedValue;
            }
            return default!;
        }

        private int GetComboBoxSelectedIndex(string name)
        {
            return _controls.TryGetValue(name, out var control) && control is ComboBox comboBox
                ? comboBox.SelectedIndex
                : 0;
        }

        private int GetEntityId()
        {
            return _entity switch
            {
                Client client => client.Id,
                Case caseEntity => caseEntity.Id,
                CaseItem item => item.Id,
                _ => 0
            };
        }

        private string GetEntityDisplayName()
        {
            return _entityType switch
            {
                "Client" => "Client",
                "Case" => "Dossier",
                "CaseItem" => "Élément",
                _ => "Entité"
            };
        }

        private void ShowValidationErrors(ValidationResult result)
        {
            var errors = string.Join("\n", result.GetAllErrors());
            ErrorMessageText.Text = errors;

            // Find the error border in XAML and make it visible
            var errorBorder = this.FindName("ErrorMessageText") as TextBlock;
            if (errorBorder?.Parent is Border border)
            {
                border.Visibility = Visibility.Visible;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion
    }
}