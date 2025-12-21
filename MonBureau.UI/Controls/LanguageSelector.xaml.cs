using System.Windows;
using System.Windows.Controls;
using MonBureau.UI.Services;

namespace MonBureau.UI.Controls
{
    /// <summary>
    /// FIXED: Language selector with proper restart handling
    /// </summary>
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
            ChangeLanguage("fr");
        }

        private void SelectEnglish_Click(object sender, RoutedEventArgs e)
        {
            ChangeLanguage("en");
        }

        private void SelectArabic_Click(object sender, RoutedEventArgs e)
        {
            ChangeLanguage("ar");
        }

        private void ChangeLanguage(string languageCode)
        {
            if (_localizationService.CurrentLanguage == languageCode)
            {
                LanguagePopup.IsOpen = false;
                return; // Already using this language
            }

            _localizationService.ChangeLanguage(languageCode);
            LanguagePopup.IsOpen = false;

            // Show restart prompt
            var currentLang = _localizationService.CurrentLanguage;
            var message = currentLang switch
            {
                "ar" => "تم تغيير اللغة. يرجى إعادة تشغيل التطبيق لتطبيق جميع التغييرات.\n\nإعادة التشغيل الآن؟",
                "en" => "Language changed. Please restart the application for all changes to take effect.\n\nRestart now?",
                _ => "Langue modifiée. Veuillez redémarrer l'application pour appliquer tous les changements.\n\nRedémarrer maintenant?"
            };

            var title = currentLang switch
            {
                "ar" => "تم تغيير اللغة",
                "en" => "Language Changed",
                _ => "Langue Modifiée"
            };

            var result = MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Information
            );

            if (result == MessageBoxResult.Yes)
            {
                RestartApplication();
            }
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

        private void RestartApplication()
        {
            try
            {
                var exePath = Environment.ProcessPath ?? Application.ResourceAssembly.Location;
                System.Diagnostics.Process.Start(exePath);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageSelector] Error restarting: {ex.Message}");
                MessageBox.Show(
                    "Impossible de redémarrer automatiquement. Veuillez redémarrer manuellement.",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}