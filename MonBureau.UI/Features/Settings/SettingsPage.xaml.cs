using System;
using System.Windows.Controls;
using MonBureau.UI.ViewModels;

namespace MonBureau.UI.Views.Pages
{
    public partial class SettingsPage : Page, IDisposable
    {
        private SettingsViewModel? _viewModel;
        private bool _disposed;

        public SettingsPage()
        {
            InitializeComponent();
            _viewModel = App.GetService<SettingsViewModel>();
            DataContext = _viewModel;
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_disposed) return;
            Loaded -= OnPageLoaded;

            if (_viewModel != null)
            {
                await _viewModel.LoadSettingsAsync();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _viewModel = null;
            DataContext = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}