
using System;
using System.Windows.Controls;
namespace MonBureau.UI.Views.Pages
{
    /// <summary>
    /// FIXED: Added partial modifier
    /// </summary>
    public partial class ExpensesPage : Page, IDisposable
    {
        private Features.Expenses.ExpensesViewModel? _viewModel;
        private bool _disposed;
        public ExpensesPage()
        {
            InitializeComponent();
            _viewModel = App.GetService<Features.Expenses.ExpensesViewModel>();
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