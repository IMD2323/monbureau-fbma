using System;
using System.Windows.Controls;
using MonBureau.UI.ViewModels;

namespace MonBureau.UI.Views.Pages
{
    /// <summary>
    /// FIXED: Proper error handling and async initialization
    /// </summary>
    public partial class BackupPage : Page, IDisposable
    {
        private BackupViewModel? _viewModel;
        private bool _disposed;
        private bool _initialized;

        public BackupPage()
        {
            try
            {
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("[BackupPage] InitializeComponent completed");

                _viewModel = App.GetService<BackupViewModel>();
                System.Diagnostics.Debug.WriteLine("[BackupPage] ViewModel obtained");

                DataContext = _viewModel;
                System.Diagnostics.Debug.WriteLine("[BackupPage] DataContext set");

                Loaded += OnPageLoaded;
                Unloaded += OnPageUnloaded;
                System.Diagnostics.Debug.WriteLine("[BackupPage] Event handlers attached");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupPage] ❌ Constructor error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[BackupPage] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        private async void OnPageLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_disposed || _initialized)
            {
                System.Diagnostics.Debug.WriteLine("[BackupPage] Already disposed or initialized, skipping load");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[BackupPage] Page loaded, initializing...");
                _initialized = true;
                Loaded -= OnPageLoaded; // Prevent multiple calls

                if (_viewModel != null)
                {
                    await _viewModel.InitializeAsync();
                    System.Diagnostics.Debug.WriteLine("[BackupPage] ✅ Initialization complete");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupPage] ❌ Load error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[BackupPage] StackTrace: {ex.StackTrace}");

                System.Windows.MessageBox.Show(
                    $"Erreur lors du chargement de la page de sauvegarde:\n\n{ex.Message}",
                    "Erreur",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void OnPageUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[BackupPage] Page unloaded");
            // Don't dispose here - wait for explicit Dispose call
        }

        public void Dispose()
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine("[BackupPage] Disposing...");

            try
            {
                // Use Dispatcher for thread safety
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(Dispose);
                    return;
                }

                // Unsubscribe from events
                Loaded -= OnPageLoaded;
                Unloaded -= OnPageUnloaded;

                // Dispose ViewModel
                if (_viewModel != null)
                {
                    _viewModel.Dispose();
                    _viewModel = null;
                    System.Diagnostics.Debug.WriteLine("[BackupPage] ViewModel disposed");
                }

                // Clear DataContext
                DataContext = null;

                _disposed = true;
                GC.SuppressFinalize(this);

                System.Diagnostics.Debug.WriteLine("[BackupPage] ✅ Disposal complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupPage] ⚠️ Error during disposal: {ex.Message}");
            }
        }

        ~BackupPage()
        {
            if (!_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[BackupPage] ⚠️ Finalizer called - Dispose() not called!");
            }
        }
    }
}