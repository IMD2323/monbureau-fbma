using System;
using System.Threading.Tasks;
using System.Windows;
using MonBureau.Infrastructure.Services;
using MonBureau.Infrastructure.Services.Firebase;

namespace MonBureau.UI.Views
{
    /// <summary>
    /// Startup window with grace period for offline validation
    /// </summary>
    public partial class StartupWindow : Window
    {
        private readonly SecureLicenseStorage _licenseStorage;
        private readonly DeviceIdentifier _deviceIdentifier;
        private readonly FirestoreLicenseService _licenseService;
        private const int GRACE_PERIOD_DAYS = 7;

        public StartupWindow()
        {
            InitializeComponent();
            _licenseStorage = new SecureLicenseStorage();
            _deviceIdentifier = new DeviceIdentifier();
            _licenseService = new FirestoreLicenseService("monbureau-licenses");

            Loaded += async (s, e) => await ValidateLicenseAsync();
        }

        private async Task ValidateLicenseAsync()
        {
            try
            {
                StatusText.Text = "Vérification de la licence...";
                await Task.Delay(800);

                // Check if license file exists
                if (!_licenseStorage.LicenseExists())
                {
                    StatusText.Text = "Aucune licence trouvée";
                    await Task.Delay(1000);
                    ShowActivationWindow();
                    return;
                }

                StatusText.Text = "Chargement de la licence...";
                var (success, licenseKey, deviceId, lastValidation) = _licenseStorage.LoadLicense();

                if (!success || string.IsNullOrEmpty(licenseKey) || string.IsNullOrEmpty(deviceId))
                {
                    StatusText.Text = "Licence corrompue";
                    await Task.Delay(1000);
                    ShowActivationWindow();
                    return;
                }

                StatusText.Text = "Validation en ligne...";
                var currentDeviceId = _deviceIdentifier.GenerateDeviceId();

                // Verify device ID matches
                if (deviceId != currentDeviceId)
                {
                    MessageBox.Show(
                        "Cette licence est liée à un autre appareil.",
                        "Erreur de licence",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    ShowActivationWindow();
                    return;
                }

                // Try online validation
                var (isValid, message) = await _licenseService.ValidateLicenseAsync(licenseKey, deviceId);

                if (isValid)
                {
                    // Online validation successful
                    _licenseStorage.UpdateLastValidation();
                    StatusText.Text = "Licence valide - Chargement...";
                    await Task.Delay(800);
                    ShowMainWindow();
                    return;
                }

                // Online validation failed - check grace period
                if (lastValidation.HasValue)
                {
                    var daysSinceValidation = (DateTime.UtcNow - lastValidation.Value).Days;

                    if (daysSinceValidation <= GRACE_PERIOD_DAYS)
                    {
                        // Within grace period - allow offline mode
                        var daysRemaining = GRACE_PERIOD_DAYS - daysSinceValidation;

                        var result = MessageBox.Show(
                            $"Mode hors ligne activé\n\n" +
                            $"La validation en ligne a échoué, mais vous pouvez continuer en mode hors ligne.\n\n" +
                            $"Jours restants avant validation obligatoire: {daysRemaining}\n\n" +
                            $"Raison: {message}\n\n" +
                            $"Continuer en mode hors ligne?",
                            "Mode Hors Ligne",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            StatusText.Text = $"Mode hors ligne ({daysRemaining}j restants)...";
                            await Task.Delay(800);
                            ShowMainWindow();
                            return;
                        }
                    }
                    else
                    {
                        // Grace period expired
                        MessageBox.Show(
                            $"Période de grâce expirée\n\n" +
                            $"Vous n'avez pas validé votre licence en ligne depuis {daysSinceValidation} jours.\n" +
                            $"Une connexion Internet est requise pour continuer.\n\n" +
                            $"Raison: {message}",
                            "Validation Requise",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // No last validation date - require online validation
                    MessageBox.Show(
                        $"Validation en ligne requise\n\n{message}",
                        "Erreur de licence",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                ShowActivationWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur lors de la validation: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ShowActivationWindow();
            }
        }

        private void ShowActivationWindow()
        {
            var activationWindow = new LicenseActivationWindow();
            activationWindow.Show();
            Close();
        }

        private void ShowMainWindow()
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
    }
}