using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;

namespace MonBureau.UI.Services
{
    /// <summary>
    /// FIXED: Proper localization service with resource manager integration
    /// Supports French, English, and Arabic with RTL layout
    /// </summary>
    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        private string _currentLanguage;
        private readonly string _settingsPath;
        private System.Resources.ResourceManager? _resourceManager;

        public event PropertyChangedEventHandler? PropertyChanged;

        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public string CurrentLanguage
        {
            get => _currentLanguage;
            private set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnPropertyChanged(nameof(CurrentLanguage));
                    OnPropertyChanged(nameof(IsRightToLeft));
                    OnPropertyChanged(nameof(FlowDirection));
                }
            }
        }

        public bool IsRightToLeft => CurrentLanguage == "ar";

        public FlowDirection FlowDirection => IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        private LocalizationService()
        {
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonBureau",
                "language.json"
            );

            // Initialize resource manager
            _resourceManager = MonBureau.UI.Resources.Localization.Strings.ResourceManager;

            _currentLanguage = LoadSavedLanguage();
            ApplyLanguage(_currentLanguage);
        }

        /// <summary>
        /// Changes the application language
        /// </summary>
        public void ChangeLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[Localization] Changing language to: {languageCode}");

                ApplyLanguage(languageCode);
                CurrentLanguage = languageCode;
                SaveLanguagePreference(languageCode);

                System.Diagnostics.Debug.WriteLine($"[Localization] ✅ Language changed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Localization] ❌ Error changing language: {ex.Message}");
                MessageBox.Show(
                    $"Error changing language: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// Gets a localized string by key
        /// </summary>
        public string GetString(string key)
        {
            try
            {
                if (_resourceManager == null)
                    return key;

                var value = _resourceManager.GetString(key, CultureInfo.CurrentUICulture);
                return value ?? key;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Localization] Error getting string '{key}': {ex.Message}");
                return key;
            }
        }

        /// <summary>
        /// Applies the specified language to the application
        /// </summary>
        private void ApplyLanguage(string languageCode)
        {
            try
            {
                var culture = new CultureInfo(languageCode);

                // Set culture for all threads
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                // Set thread culture
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

                System.Diagnostics.Debug.WriteLine($"[Localization] Applied culture: {culture.Name}");
                System.Diagnostics.Debug.WriteLine($"[Localization] CurrentUICulture: {CultureInfo.CurrentUICulture.Name}");

                // Update all windows
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        // Set language
                        window.Language = XmlLanguage.GetLanguage(culture.Name);

                        // Set flow direction
                        window.FlowDirection = IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

                        System.Diagnostics.Debug.WriteLine($"[Localization] Updated window: {window.GetType().Name}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Localization] Error applying language: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the saved language preference
        /// </summary>
        private string LoadSavedLanguage()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<LanguageSettings>(json);

                    if (settings != null && !string.IsNullOrEmpty(settings.Language))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Localization] Loaded saved language: {settings.Language}");
                        return settings.Language;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Localization] Error loading language: {ex.Message}");
            }

            // Default to French
            return "fr";
        }

        /// <summary>
        /// Saves the language preference to disk
        /// </summary>
        private void SaveLanguagePreference(string languageCode)
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var settings = new LanguageSettings { Language = languageCode };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_settingsPath, json);
                System.Diagnostics.Debug.WriteLine($"[Localization] Saved language preference: {languageCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Localization] Error saving language: {ex.Message}");
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private class LanguageSettings
        {
            public string Language { get; set; } = "fr";
        }
    }
}