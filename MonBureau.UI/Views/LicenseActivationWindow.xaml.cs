using System;
using System.Text.RegularExpressions;
using System.Windows;
using MonBureau.Infrastructure.Services;
using MonBureau.Infrastructure.Services.Firebase;

namespace MonBureau.UI.Views
{
    /// <summary>
    /// License activation window with enhanced diagnostics
    /// </summary>
    public partial class LicenseActivationWindow : Window
    {
        private readonly SecureLicenseStorage _licenseStorage;
        private readonly DeviceIdentifier _deviceIdentifier;
        private readonly FirestoreLicenseService _licenseService;
        private string _deviceId;

        public LicenseActivationWindow()
        {
            InitializeComponent();

            _licenseStorage = new SecureLicenseStorage();
            _deviceIdentifier = new DeviceIdentifier();
            _licenseService = new FirestoreLicenseService("monbureau-licenses");
            _deviceId = string.Empty;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[LicenseActivationWindow] Window loaded, initializing...");

                _deviceId = _deviceIdentifier.GenerateDeviceId();
                DeviceIdText.Text = $"ID Appareil: {_deviceId.Substring(0, 16)}...";

                // ✅ DIAGNOSTIC FIREBASE
                System.Diagnostics.Debug.WriteLine("=== FIREBASE DIAGNOSTIC ===");
                System.Diagnostics.Debug.WriteLine($"Firebase initialized: {FirebaseConfig.IsInitialized}");
                System.Diagnostics.Debug.WriteLine($"Firestore service available: {_licenseService != null}");
                System.Diagnostics.Debug.WriteLine($"Firestore online: {_licenseService?.IsOnline ?? false}");
                System.Diagnostics.Debug.WriteLine(FirebaseConfig.GetDiagnosticInfo());
                System.Diagnostics.Debug.WriteLine("===========================");

                // Show warning if Firebase is not initialized
                if (!FirebaseConfig.IsInitialized)
                {
                    ShowStatus(
                        "⚠️ Firebase non initialisé. La validation en ligne n'est pas disponible.",
                        true
                    );
                }
                else if (!_licenseService.IsOnline)
                {
                    ShowStatus(
                        "⚠️ Service de licences hors ligne. Certaines fonctionnalités sont limitées.",
                        true
                    );
                }

                // Focus on email field
                EmailTextBox.Focus();

                System.Diagnostics.Debug.WriteLine("[LicenseActivationWindow] Initialization complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseActivationWindow] ❌ Initialization error: {ex}");
                ShowStatus($"Erreur d'initialisation: {ex.Message}", true);
            }
        }

        private async void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[LicenseActivationWindow] Activate button clicked");

                // Validate input
                if (!ValidateInput(out string? errorMessage))
                {
                    ShowStatus(errorMessage!, true);
                    return;
                }

                var email = EmailTextBox.Text.Trim();
                var licenseKey = LicenseKeyTextBox.Text.Trim().ToUpperInvariant();

                System.Diagnostics.Debug.WriteLine($"[LicenseActivationWindow] Attempting activation...");
                System.Diagnostics.Debug.WriteLine($"[LicenseActivationWindow]    Email: {email}");
                System.Diagnostics.Debug.WriteLine($"[LicenseActivationWindow]    License: {licenseKey}");

                // Check if Firebase is available
                if (!FirebaseConfig.IsInitialized)
                {
                    ShowStatus(
                        "❌ Firebase non disponible.\n\n" +
                        "Vérifiez que le fichier de configuration Firebase est présent dans le dossier Config.",
                        true
                    );
                    return;
                }

                if (!_licenseService.IsOnline)
                {
                    ShowStatus(
                        "❌ Service de licences hors ligne.\n\n" +
                        "Impossible d'activer une licence sans connexion au serveur.",
                        true
                    );
                    return;
                }

                // Disable button during activation
                ActivateButton.IsEnabled = false;
                ActivateButton.Content = "Activation en cours...";
                StatusMessage.Visibility = Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine("[LicenseActivationWindow] Calling ActivateLicenseAsync...");

                // Attempt activation
                var (success, message) = await _licenseService.ActivateLicenseAsync(
                    licenseKey,
                    _deviceId,
                    email
                );

                System.Diagnostics.Debug.WriteLine($"[LicenseActivationWindow] Activation result: {success}");
                System.Diagnostics.Debug.WriteLine($"[LicenseActivationWindow] Message: {message}");

                if (success)
                {
                    System.Diagnostics.Debug.WriteLine("[LicenseActivationWindow] ✅ Activation successful, saving license...");

                    // Save license securely
                    if (_licenseStorage.SaveLicense(licenseKey, _deviceId))
                    {
                        System.Diagnostics.Debug.WriteLine("[LicenseActivationWindow] ✅ License saved locally");

                        MessageBox.Show(
                            "Licence activée avec succès!\n\n" +
                            "MonBureau va maintenant démarrer.",
                            "Activation Réussie",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );

                        var mainWindow = new MainWindow();
                        mainWindow.Show();
                        Close();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LicenseActivationWindow] ⚠️ Failed to save license locally");
                        ShowStatus("Licence activée mais erreur de sauvegarde locale", true);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[LicenseActivationWindow] ❌ Activation failed: {message}");
                    ShowStatus(message, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseActivationWindow] ❌ Exception during activation: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[LicenseActivationWindow]    Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[LicenseActivationWindow]    StackTrace: {ex.StackTrace}");

                ShowStatus($"Erreur: {ex.Message}", true);
            }
            finally
            {
                ActivateButton.IsEnabled = true;
                ActivateButton.Content = "Activer";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Êtes-vous sûr de vouloir quitter?\n\n" +
                "L'application nécessite une licence valide pour fonctionner.",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        private bool ValidateInput(out string? errorMessage)
        {
            errorMessage = null;

            var email = EmailTextBox.Text.Trim();
            var licenseKey = LicenseKeyTextBox.Text.Trim();

            // Validate email
            if (string.IsNullOrWhiteSpace(email))
            {
                errorMessage = "Veuillez entrer votre adresse email";
                EmailTextBox.Focus();
                return false;
            }

            if (!IsValidEmail(email))
            {
                errorMessage = "Adresse email invalide";
                EmailTextBox.Focus();
                return false;
            }

            // Validate license key
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                errorMessage = "Veuillez entrer votre clé de licence";
                LicenseKeyTextBox.Focus();
                return false;
            }

            // Validate license key format (MB-2025-XXXXX)
            if (!Regex.IsMatch(licenseKey, @"^MB-\d{4}-[A-Z0-9]{5,}$", RegexOptions.IgnoreCase))
            {
                errorMessage = "Format de clé invalide. Format attendu: MB-2025-XXXXX";
                LicenseKeyTextBox.Focus();
                return false;
            }

            return true;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusMessage.Text = message;
            StatusMessage.Foreground = isError
                ? (System.Windows.Media.Brush)FindResource("ErrorBrush")
                : (System.Windows.Media.Brush)FindResource("SuccessBrush");
            StatusMessage.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Auto-format license key as user types
        /// </summary>
        private void LicenseKeyTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            if (textBox == null) return;

            var text = textBox.Text.ToUpperInvariant().Replace("-", "");

            // Format as MB-YYYY-XXXXX
            if (text.Length >= 2 && !text.StartsWith("MB"))
            {
                if (text.StartsWith("M") && text.Length > 1 && text[1] != 'B')
                {
                    text = "MB" + text.Substring(1);
                }
            }

            // Add dashes
            var formatted = text;
            if (text.Length > 2)
            {
                formatted = text.Substring(0, 2);
                if (text.Length > 6)
                {
                    formatted += "-" + text.Substring(2, 4) + "-" + text.Substring(6);
                }
                else if (text.Length > 2)
                {
                    formatted += "-" + text.Substring(2);
                }
            }

            // Update without triggering event again
            if (formatted != textBox.Text)
            {
                var cursorPos = textBox.SelectionStart;
                textBox.TextChanged -= LicenseKeyTextBox_TextChanged;
                textBox.Text = formatted;
                textBox.SelectionStart = Math.Min(cursorPos + 1, formatted.Length);
                textBox.TextChanged += LicenseKeyTextBox_TextChanged;
            }
        }
    }
}