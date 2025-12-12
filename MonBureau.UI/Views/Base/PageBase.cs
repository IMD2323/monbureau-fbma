using System;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MonBureau.UI.Views.Base
{
    /// <summary>
    /// Universal page base with automatic lifecycle management
    /// Eliminates repetitive disposal and initialization code
    /// </summary>
    public abstract class PageBase<TViewModel> : Page, IDisposable
        where TViewModel : class
    {
        protected TViewModel? ViewModel { get; private set; }
        private bool _disposed;
        private bool _initialized;

        protected PageBase()
        {
            ViewModel = App.GetService<TViewModel>();
            DataContext = ViewModel;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_initialized || _disposed) return;

            _initialized = true;
            Loaded -= OnLoaded; // Prevent multiple calls

            try
            {
                await OnInitializeAsync();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Page unloaded - cleanup will happen in Dispose()
        }

        /// <summary>
        /// Override to add custom initialization logic
        /// </summary>
        protected virtual async Task OnInitializeAsync()
        {
            // Call ViewModel's InitializeAsync if it exists
            var initMethod = typeof(TViewModel).GetMethod("InitializeAsync");
            if (initMethod != null)
            {
                var task = initMethod.Invoke(ViewModel, null) as Task;
                if (task != null) await task;
            }

            // Call ViewModel's LoadDataAsync if it exists
            var loadMethod = typeof(TViewModel).GetMethod("LoadDataAsync");
            if (loadMethod != null)
            {
                var task = loadMethod.Invoke(ViewModel, null) as Task;
                if (task != null) await task;
            }
        }

        /// <summary>
        /// Override to add custom error handling
        /// </summary>
        protected virtual void HandleError(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] Error: {ex.Message}");
            System.Windows.MessageBox.Show(
                $"Erreur: {ex.Message}",
                "Erreur",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }

        /// <summary>
        /// Automatic disposal - no need to override in derived classes
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Only unsubscribe if on UI thread
                if (Dispatcher.CheckAccess())
                {
                    Loaded -= OnLoaded;
                    Unloaded -= OnUnloaded;
                }

                // Dispose ViewModel if it implements IDisposable
                if (ViewModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                ViewModel = null;

                if (Dispatcher.CheckAccess())
                {
                    DataContext = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] Disposal error: {ex.Message}");
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~PageBase()
        {
            if (!_disposed)
            {
                System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] ⚠️ Finalizer called - Dispose() not called!");
            }
        }
    }
}