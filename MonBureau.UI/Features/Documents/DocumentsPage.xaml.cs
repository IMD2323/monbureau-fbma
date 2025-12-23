using System;
using System.Windows.Controls;
using MonBureau.UI.ViewModels;

namespace MonBureau.UI.Views.Pages
{
    /// <summary>
    /// DocumentsPage - Clean separation following app architecture
    /// Simple page that lists all documents from all cases
    /// </summary>
    public partial class DocumentsPage : Page, IDisposable
    {
        private DocumentsViewModel? _viewModel;
        private bool _disposed;
        private bool _initialized;

        public DocumentsPage()
        {
            try
            {
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("[DocumentsPage] Initializing...");

                _viewModel = App.GetService<DocumentsViewModel>();
                DataContext = _viewModel;

                Loaded += OnPageLoaded;
                Unloaded += OnPageUnloaded;

                System.Diagnostics.Debug.WriteLine("[DocumentsPage] Initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DocumentsPage] ❌ Constructor error: {ex.Message}");
                throw;
            }
        }

        private async void OnPageLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_disposed || _initialized)
            {
                System.Diagnostics.Debug.WriteLine("[DocumentsPage] Already disposed or initialized");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[DocumentsPage] Page loaded, initializing data...");
                _initialized = true;
                Loaded -= OnPageLoaded; // Prevent multiple calls

                if (_viewModel != null)
                {
                    await _viewModel.InitializeAsync();
                    System.Diagnostics.Debug.WriteLine("[DocumentsPage] ✅ Data loaded successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DocumentsPage] ❌ Load error: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Erreur lors du chargement des documents:\n\n{ex.Message}",
                    "Erreur",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void OnPageUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[DocumentsPage] Page unloaded");
            // Cleanup handled in Dispose
        }

        public void Dispose()
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine("[DocumentsPage] Disposing...");

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
                    System.Diagnostics.Debug.WriteLine("[DocumentsPage] ViewModel disposed");
                }

                // Clear DataContext
                DataContext = null;

                _disposed = true;
                GC.SuppressFinalize(this);

                System.Diagnostics.Debug.WriteLine("[DocumentsPage] ✅ Disposal complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DocumentsPage] ⚠️ Error during disposal: {ex.Message}");
            }
        }

        ~DocumentsPage()
        {
            if (!_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[DocumentsPage] ⚠️ Finalizer called - Dispose() not called!");
            }
        }
    }
}