using System;
using System.Windows.Controls;
namespace MonBureau.UI.Features.Rdvs
{
    /// <summary>
    /// FIXED: Added partial modifier
    /// </summary>
    public partial class AppointmentsPage : Page, IDisposable
    {
        private AppointmentsViewModel? _viewModel;
        private bool _disposed;
        public AppointmentsPage()
        {
            InitializeComponent();
            _viewModel = App.GetService<AppointmentsViewModel>();
            DataContext = _viewModel;
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_disposed) return;
            Loaded -= OnPageLoaded;

            if (_viewModel != null)
            {
                await _viewModel.InitializeAsync();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_viewModel != null)
            {
                _viewModel.Dispose();
                _viewModel = null;
            }

            DataContext = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
