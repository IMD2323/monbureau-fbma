using System;
using System.Windows.Controls;
using MonBureau.UI.Features.Cases;
using MonBureau.UI.ViewModels;

namespace MonBureau.UI.Features.Cases
{
    public partial class CasesPage : Page, IDisposable
    {
        private CasesViewModel? _viewModel;
        private bool _disposed;

        public CasesPage()
        {
            InitializeComponent();
            _viewModel = App.GetService<CasesViewModel>();
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