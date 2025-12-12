using System;
using System.Windows.Controls;
using MonBureau.UI.ViewModels;

namespace MonBureau.UI.Views.Pages
{
    public partial class DashboardPage : Page, IDisposable
    {
        private DashboardViewModel? _viewModel;
        private bool _disposed;

        public DashboardPage()
        {
            InitializeComponent();
            _viewModel = App.GetService<DashboardViewModel>();
            DataContext = _viewModel;
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_disposed) return;
            Loaded -= OnPageLoaded;

            if (_viewModel != null)
            {
                await _viewModel.LoadDataAsync();
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