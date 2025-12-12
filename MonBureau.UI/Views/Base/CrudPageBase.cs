using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MonBureau.UI.Views.Base
{
    /// <summary>
    /// Generic CRUD page base for MonBureau
    /// Handles initialization and ViewModel lifecycle
    /// </summary>
    public abstract class CrudPageBase<TViewModel> : Page
        where TViewModel : ViewModels.Base.ViewModelBase
    {
        protected TViewModel ViewModel { get; }
        private bool _initialized;

        protected CrudPageBase()
        {
            ViewModel = App.GetService<TViewModel>();
            DataContext = ViewModel;

            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // Prevent multiple initializations
            if (_initialized) return;

            Loaded -= OnPageLoaded;
            _initialized = true;

            await InitializeAsync();
        }

        /// <summary>
        /// Override to add custom initialization logic
        /// </summary>
        protected virtual async Task InitializeAsync()
        {
            try
            {
                // Call ViewModel's InitializeAsync if it has the method
                var initMethod = typeof(TViewModel).GetMethod("InitializeAsync");
                if (initMethod != null)
                {
                    var task = initMethod.Invoke(ViewModel, null) as Task;
                    if (task != null)
                    {
                        await task;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur d'initialisation: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}