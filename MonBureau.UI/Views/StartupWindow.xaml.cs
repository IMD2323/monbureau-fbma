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
            _licenseService = new FirestoreLicenseService(); // ✅ FIXED: No constructor parameter

            Loaded += async (s, e) => await ValidateLicenseAsync();
        }

        private async Task ValidateLicenseAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[StartupWindow] Starting license validation...");

                StatusText.Text = "Vérification de la licence...";
                await Task.Delay(800);

                // ✅ DIAGNOSTIC FIREBASE
                System.Diagnostics.Debug.WriteLine("=== STARTUP FIREBASE DIAGNOSTIC ===");
                System.Diagnostics.Debug.WriteLine($"Firebase initialized: {FirebaseConfig.IsInitialized}");
                System.Diagnostics.Debug.WriteLine($"Firestore online: {_licenseService?.IsOnline ?? false}");
                System.Diagnostics.Debug.WriteLine("===================================");

                // Check if license file exists
                if (!_licenseStorage.LicenseExists())
                {
                    System.Diagnostics.Debug.WriteLine("[StartupWindow] No license file found");
                    StatusText.Text = "Aucune licence trouvée";
                    await Task.Delay(1000);
                    ShowActivationWindow();
                    return;
                }

                StatusText.Text = "Chargement de la licence...";
                var (success, licenseKey, deviceId, lastValidation) = _licenseStorage.LoadLicense();

                System.Diagnostics.Debug.WriteLine($"[StartupWindow] License load result: {success}");
                System.Diagnostics.Debug.WriteLine($"[StartupWindow] License key: {licenseKey?.Substring(0, Math.Min(10, licenseKey?.Length ?? 0))}...");

                if (!success || string.IsNullOrEmpty(licenseKey) || string.IsNullOrEmpty(deviceId))
                {
                    System.Diagnostics.Debug.WriteLine("[StartupWindow] License corrupted or invalid");
                    StatusText.Text = "Licence corrompue";
                    await Task.Delay(1000);
                    ShowActivationWindow();
                    return;
                }

                StatusText.Text = "Validation de l'appareil...";
                var currentDeviceId = _deviceIdentifier.GenerateDeviceId();

                // Verify device ID matches
                if (deviceId != currentDeviceId)
                {
                    System.Diagnostics.Debug.WriteLine("[StartupWindow] Device ID mismatch");
                    MessageBox.Show(
                        "Cette licence est liée à un autre appareil.\n\n" +
                        "Si vous avez changé de matériel, contactez le support pour réactiver votre licence.",
                        "Erreur de licence",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    ShowActivationWindow();
                    return;
                }

                // Check if Firebase is available for online validation
                if (!FirebaseConfig.IsInitialized || !_licenseService.IsOnline)
                {
                    System.Diagnostics.Debug.WriteLine("[StartupWindow] Firebase not available - attempting offline validation");
                    StatusText.Text = "Firebase non disponible - Mode hors ligne...";

                    if (lastValidation.HasValue)
                    {
                        var daysSinceValidation = (DateTime.UtcNow - lastValidation.Value).Days;

                        if (daysSinceValidation <= GRACE_PERIOD_DAYS)
                        {
                            // Within grace period - allow offline mode
                            var daysRemaining = GRACE_PERIOD_DAYS - daysSinceValidation;

                            System.Diagnostics.Debug.WriteLine($"[StartupWindow] Within grace period: {daysRemaining} days remaining");

                            var result = MessageBox.Show(
                                $"Mode hors ligne\n\n" +
                                $"Firebase n'est pas disponible, mais vous pouvez continuer en mode hors ligne.\n\n" +
                                $"Jours restants avant validation obligatoire: {daysRemaining}\n\n" +
                                $"Pour activer les fonctionnalités en ligne, configurez Firebase:\n" +
                                $"1. Exécutez SetupCredentials.ps1 en tant qu'administrateur\n" +
                                $"2. Redémarrez l'application\n\n" +
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
                            else
                            {
                                Application.Current.Shutdown();
                                return;
                            }
                        }
                        else
                        {
                            // Grace period expired
                            System.Diagnostics.Debug.WriteLine($"[StartupWindow] Grace period expired: {daysSinceValidation} days since last validation");

                            MessageBox.Show(
                                $"Période de grâce expirée\n\n" +
                                $"Vous n'avez pas validé votre licence en ligne depuis {daysSinceValidation} jours.\n\n" +
                                $"Une validation en ligne est requise pour continuer.\n\n" +
                                $"Pour configurer Firebase:\n" +
                                $"1. Exécutez SetupCredentials.ps1 en tant qu'administrateur\n" +
                                $"2. Redémarrez l'application",
                                "Validation Requise",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);

                            ShowActivationWindow();
                            return;
                        }
                    }
                    else
                    {
                        // No last validation date - require online validation
                        System.Diagnostics.Debug.WriteLine("[StartupWindow] No last validation date - online validation required");

                        MessageBox.Show(
                            "Validation en ligne requise\n\n" +
                            "Cette licence n'a jamais été validée en ligne.\n" +
                            "Firebase doit être configuré pour continuer.\n\n" +
                            "Pour configurer Firebase:\n" +
                            "1. Exécutez SetupCredentials.ps1 en tant qu'administrateur\n" +
                            "2. Redémarrez l'application",
                            "Configuration Requise",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        ShowActivationWindow();
                        return;
                    }
                }

                // Try online validation
                StatusText.Text = "Validation en ligne...";
                System.Diagnostics.Debug.WriteLine("[StartupWindow] Attempting online validation...");

                var (isValid, message) = await _licenseService.ValidateLicenseAsync(licenseKey, deviceId);

                System.Diagnostics.Debug.WriteLine($"[StartupWindow] Online validation result: {isValid}");
                System.Diagnostics.Debug.WriteLine($"[StartupWindow] Message: {message}");

                if (isValid)
                {
                    // Online validation successful
                    System.Diagnostics.Debug.WriteLine("[StartupWindow] ✅ Online validation successful");
                    _licenseStorage.UpdateLastValidation();
                    StatusText.Text = "Licence valide - Chargement...";
                    await Task.Delay(800);
                    ShowMainWindow();
                    return;
                }

                // Online validation failed - check grace period
                System.Diagnostics.Debug.WriteLine($"[StartupWindow] Online validation failed: {message}");

                if (lastValidation.HasValue)
                {
                    var daysSinceValidation = (DateTime.UtcNow - lastValidation.Value).Days;

                    if (daysSinceValidation <= GRACE_PERIOD_DAYS)
                    {
                        // Within grace period - allow offline mode
                        var daysRemaining = GRACE_PERIOD_DAYS - daysSinceValidation;

                        System.Diagnostics.Debug.WriteLine($"[StartupWindow] Within grace period: {daysRemaining} days remaining");

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
                        System.Diagnostics.Debug.WriteLine($"[StartupWindow] Grace period expired: {daysSinceValidation} days");

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
                    System.Diagnostics.Debug.WriteLine("[StartupWindow] No last validation date");

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
                System.Diagnostics.Debug.WriteLine($"[StartupWindow] ❌ Exception during validation: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[StartupWindow]    Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[StartupWindow]    StackTrace: {ex.StackTrace}");

                MessageBox.Show(
                    $"Erreur lors de la validation:\n\n{ex.Message}\n\n" +
                    $"L'application va maintenant afficher l'écran d'activation.",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ShowActivationWindow();
            }
        }

        private void ShowActivationWindow()
        {
            System.Diagnostics.Debug.WriteLine("[StartupWindow] Showing activation window");
            var activationWindow = new LicenseActivationWindow();
            activationWindow.Show();
            Close();
        }

        private void ShowMainWindow()
        {
            System.Diagnostics.Debug.WriteLine("[StartupWindow] Showing main window");
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
    }
}