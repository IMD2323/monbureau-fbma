using System.Windows;
using System.Windows.Controls;
using MonBureau.UI.Services;

namespace MonBureau.UI.Controls
{
    public partial class LanguageSelector : UserControl
    {
        private readonly LocalizationService _localizationService;

        public LanguageSelector()
        {
            InitializeComponent();
            _localizationService = LocalizationService.Instance;

            // Update current language display
            UpdateCurrentLanguageDisplay();

            // Subscribe to language changes
            _localizationService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LocalizationService.CurrentLanguage))
                {
                    UpdateCurrentLanguageDisplay();
                }
            };
        }

        private void LanguageMenuButton_Click(object sender, RoutedEventArgs e)
        {
            LanguagePopup.IsOpen = !LanguagePopup.IsOpen;
        }

        private void SelectFrench_Click(object sender, RoutedEventArgs e)
        {
            _localizationService.ChangeLanguage("fr");
            LanguagePopup.IsOpen = false;
            RequestRestartIfNeeded();
        }

        private void SelectEnglish_Click(object sender, RoutedEventArgs e)
        {
            _localizationService.ChangeLanguage("en");
            LanguagePopup.IsOpen = false;
            RequestRestartIfNeeded();
        }

        private void SelectArabic_Click(object sender, RoutedEventArgs e)
        {
            _localizationService.ChangeLanguage("ar");
            LanguagePopup.IsOpen = false;
            RequestRestartIfNeeded();
        }

        private void UpdateCurrentLanguageDisplay()
        {
            CurrentLanguageText.Text = _localizationService.CurrentLanguage switch
            {
                "fr" => "Français",
                "en" => "English",
                "ar" => "العربية",
                _ => "Français"
            };
        }

        private void RequestRestartIfNeeded()
        {
            var result = MessageBox.Show(
                _localizationService.GetString("Message_RestartRequired") ??
                "Language changed. Please restart the application for all changes to take effect.\n\nRestart now?",
                _localizationService.GetString("Message_LanguageChanged") ?? "Language Changed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information
            );

            if (result == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(
                    Environment.ProcessPath ?? Application.ResourceAssembly.Location
                );
                Application.Current.Shutdown();
            }
        }
    }
}