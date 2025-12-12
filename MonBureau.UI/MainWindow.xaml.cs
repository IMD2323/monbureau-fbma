using System;
using System.Windows;
using System.Windows.Input;
using MonBureau.UI.Services;
using MonBureau.UI.ViewModels;

namespace MonBureau.UI.Views
{
    /// <summary>
    /// MainWindow - FIXED with complete navigation
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private MainViewModel? _viewModel;
        private INavigationService? _navigationService;
        private bool _disposed;

        public MainWindow()
        {
            InitializeComponent();

            System.Diagnostics.Debug.WriteLine("[MainWindow] Initializing...");

            // Get services from DI
            _viewModel = App.GetService<MainViewModel>();
            _navigationService = App.GetService<INavigationService>();

            DataContext = _viewModel;

            // Subscribe to navigation events
            if (_navigationService != null)
            {
                _navigationService.Navigated += OnNavigated;
                _navigationService.PageTitleChanged += OnPageTitleChanged;
            }

            // Navigate to dashboard on startup
            Loaded += OnWindowLoaded;
            Closed += OnWindowClosed;

            System.Diagnostics.Debug.WriteLine("[MainWindow] Initialized successfully");
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Window loaded, navigating to dashboard");

            if (_navigationService != null)
            {
                _navigationService.NavigateToDashboard();
                HighlightButton(DashboardButton);
            }
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Window closed, disposing resources");
            Dispose();
        }

        private void OnNavigated(object? sender, System.Windows.Controls.Page page)
        {
            if (_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Already disposed, ignoring navigation");
                return;
            }

            try
            {
                // FIXED: Dispose previous page properly
                if (MainContent.Content is IDisposable disposablePage && MainContent.Content != page)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Disposing previous page: {disposablePage.GetType().Name}");

                    // Use Dispatcher to ensure thread safety
                    Dispatcher.Invoke(() =>
                    {
                        disposablePage.Dispose();
                    });
                }

                // Navigate to new page
                MainContent.Navigate(page);
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Navigated to {page.GetType().Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Navigation error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[MainWindow] StackTrace: {ex.StackTrace}");
            }
        }

        private void OnPageTitleChanged(object? sender, string title)
        {
            if (_disposed) return;

            if (_viewModel != null)
            {
                _viewModel.CurrentPageTitle = title;
            }
        }

        #region Window Controls

        private void HeaderBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // Ignore drag errors
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Navigation Events - FIXED

        private void NavigateToDashboard_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Navigate to Dashboard clicked");
            _navigationService?.NavigateToDashboard();
            HighlightButton(DashboardButton);
        }

        private void NavigateToClients_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Navigate to Clients clicked");
            _navigationService?.NavigateToClients();
            HighlightButton(ClientsButton);
        }

        private void NavigateToCases_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Navigate to Cases clicked");
            _navigationService?.NavigateToCases();
            HighlightButton(CasesButton);
        }

        private void NavigateToDocuments_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Navigate to Documents clicked");
            _navigationService?.NavigateToDocuments();
            HighlightButton(DocumentsButton);
        }

        private void OpenBackup_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Navigate to Backup clicked");
            _navigationService?.NavigateToBackup();
            HighlightButton(BackupButton);
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Navigate to Settings clicked");
            _navigationService?.NavigateToSettings();
            HighlightButton(SettingsButton);
        }

        #endregion

        #region Button Highlighting

        /// <summary>
        /// Highlights the active navigation button
        /// </summary>
        private void HighlightButton(System.Windows.Controls.Button activeButton)
        {
            try
            {
                // Reset all buttons to inactive style
                DashboardButton.Style = (Style)FindResource("NavButton");
                ClientsButton.Style = (Style)FindResource("NavButton");
                CasesButton.Style = (Style)FindResource("NavButton");
                DocumentsButton.Style = (Style)FindResource("NavButton");
                BackupButton.Style = (Style)FindResource("NavButton");
                SettingsButton.Style = (Style)FindResource("NavButton");

                // Highlight the active button
                if (activeButton != null)
                {
                    activeButton.Style = (Style)FindResource("ActiveNavButton");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error highlighting button: {ex.Message}");
            }
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine("[MainWindow] Starting disposal...");

            // FIXED: Use Dispatcher for thread safety
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(Dispose);
                return;
            }

            try
            {
                // Unsubscribe from window events
                Loaded -= OnWindowLoaded;
                Closed -= OnWindowClosed;

                // Unsubscribe from navigation service events
                if (_navigationService != null)
                {
                    _navigationService.Navigated -= OnNavigated;
                    _navigationService.PageTitleChanged -= OnPageTitleChanged;
                    _navigationService = null;
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Unsubscribed from NavigationService");
                }

                // Dispose current page
                if (MainContent?.Content is IDisposable disposablePage)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Disposing current page: {disposablePage.GetType().Name}");
                    disposablePage.Dispose();
                }

                // Clear MainContent
                if (MainContent != null)
                {
                    MainContent.Content = null;
                }

                // Clear DataContext
                DataContext = null;
                _viewModel = null;

                _disposed = true;
                GC.SuppressFinalize(this);

                System.Diagnostics.Debug.WriteLine("[MainWindow] Disposal complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error during disposal: {ex.Message}");
            }
        }

        ~MainWindow()
        {
            // Don't call Dispose from finalizer - just mark as disposed
            if (!_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] ⚠️ Finalizer called - resource leak detected");
                _disposed = true;
            }
        }

        #endregion
    }
}