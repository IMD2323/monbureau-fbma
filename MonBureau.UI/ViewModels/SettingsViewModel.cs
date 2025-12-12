using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace MonBureau.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly string _settingsPath;

        [ObservableProperty]
        private string _officeName = "Cabinet d'Avocat";

        [ObservableProperty]
        private string _officeAddress = string.Empty;

        [ObservableProperty]
        private string _officePhone = string.Empty;

        [ObservableProperty]
        private string _officeEmail = string.Empty;

        [ObservableProperty]
        private string _defaultCourtName = string.Empty;

        [ObservableProperty]
        private string _defaultCourtAddress = string.Empty;

        [ObservableProperty]
        private string _currencySymbol = "DA";

        [ObservableProperty]
        private string _dateFormat = "dd/MM/yyyy";

        [ObservableProperty]
        private int _defaultPageSize = 50;

        [ObservableProperty]
        private bool _enableAnimations = true;

        [ObservableProperty]
        private bool _showTips = true;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public SettingsViewModel()
        {
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonBureau",
                "settings.json"
            );
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = await File.ReadAllTextAsync(_settingsPath);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings != null)
                    {
                        OfficeName = settings.OfficeName;
                        OfficeAddress = settings.OfficeAddress;
                        OfficePhone = settings.OfficePhone;
                        OfficeEmail = settings.OfficeEmail;
                        DefaultCourtName = settings.DefaultCourtName;
                        DefaultCourtAddress = settings.DefaultCourtAddress;
                        CurrencySymbol = settings.CurrencySymbol;
                        DateFormat = settings.DateFormat;
                        DefaultPageSize = settings.DefaultPageSize;
                        EnableAnimations = settings.EnableAnimations;
                        ShowTips = settings.ShowTips;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur de chargement: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    OfficeName = OfficeName,
                    OfficeAddress = OfficeAddress,
                    OfficePhone = OfficePhone,
                    OfficeEmail = OfficeEmail,
                    DefaultCourtName = DefaultCourtName,
                    DefaultCourtAddress = DefaultCourtAddress,
                    CurrencySymbol = CurrencySymbol,
                    DateFormat = DateFormat,
                    DefaultPageSize = DefaultPageSize,
                    EnableAnimations = EnableAnimations,
                    ShowTips = ShowTips
                };

                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_settingsPath, json);

                MessageBox.Show(
                    "Paramètres enregistrés avec succès!",
                    "Succès",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ResetToDefaults()
        {
            var result = MessageBox.Show(
                "Réinitialiser tous les paramètres aux valeurs par défaut?",
                "Confirmer",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            OfficeName = "Cabinet d'Avocat";
            OfficeAddress = string.Empty;
            OfficePhone = string.Empty;
            OfficeEmail = string.Empty;
            DefaultCourtName = string.Empty;
            DefaultCourtAddress = string.Empty;
            CurrencySymbol = "DA";
            DateFormat = "dd/MM/yyyy";
            DefaultPageSize = 50;
            EnableAnimations = true;
            ShowTips = true;

            await SaveSettings();
        }

        [RelayCommand]
        private void ExportSettings()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Exporter les paramètres",
                Filter = "Fichiers JSON (*.json)|*.json",
                FileName = $"MonBureau_Settings_{DateTime.Now:yyyy-MM-dd}.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(_settingsPath, saveFileDialog.FileName, true);
                    MessageBox.Show(
                        "Paramètres exportés avec succès!",
                        "Succès",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Erreur: {ex.Message}",
                        "Erreur",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task ImportSettings()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Importer les paramètres",
                Filter = "Fichiers JSON (*.json)|*.json"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(openFileDialog.FileName, _settingsPath, true);
                    await LoadSettingsAsync();

                    MessageBox.Show(
                        "Paramètres importés avec succès!",
                        "Succès",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Erreur: {ex.Message}",
                        "Erreur",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private class AppSettings
        {
            public string OfficeName { get; set; } = "Cabinet d'Avocat";
            public string OfficeAddress { get; set; } = string.Empty;
            public string OfficePhone { get; set; } = string.Empty;
            public string OfficeEmail { get; set; } = string.Empty;
            public string DefaultCourtName { get; set; } = string.Empty;
            public string DefaultCourtAddress { get; set; } = string.Empty;
            public string CurrencySymbol { get; set; } = "DA";
            public string DateFormat { get; set; } = "dd/MM/yyyy";
            public int DefaultPageSize { get; set; } = 50;
            public bool EnableAnimations { get; set; } = true;
            public bool ShowTips { get; set; } = true;
        }
    }
}