using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using MonBureau.UI.Features.Backup;
using MonBureau.UI.Features.Cases;
using MonBureau.UI.Features.Clients;
using MonBureau.UI.Features.Expenses;
using MonBureau.UI.Features.Rdvs;
using MonBureau.UI.ViewModels;
using MonBureau.UI.Views.Pages;
using MonBureau.UI.Features.Rdvs;
using MonBureau.UI.Features.Clients;

namespace MonBureau.UI.Services
{
    public interface INavigationService
    {
        void NavigateToDashboard();
        void NavigateToClients();
        void NavigateToCases();
        void NavigateToDocuments();
        void NavigateToBackup();
        void NavigateToSettings();
        event EventHandler<Page>? Navigated;
        event EventHandler<string>? PageTitleChanged;
    }

    /// <summary>
    /// FIXED: Proper Documents page navigation + better error handling
    /// </summary>
    public class NavigationService : INavigationService
    {
        public event EventHandler<Page>? Navigated;
        public event EventHandler<string>? PageTitleChanged;

        public void NavigateToDashboard()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationService] Navigating to Dashboard");

            try
            {
                var viewModel = App.GetService<DashboardViewModel>();
                var page = new DashboardPage { DataContext = viewModel };

                _ = viewModel.LoadDataAsync();

                OnPageTitleChanged("Tableau de Bord");
                OnNavigated(page);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Dashboard navigation error: {ex.Message}");
                ShowNavigationError("Tableau de Bord", ex);
            }
        }

        public void NavigateToClients()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationService] Navigating to Clients");

            try
            {
                var viewModel = App.GetService<ClientsViewModel>();
                var page = new ClientsPage { DataContext = viewModel };

                _ = viewModel.InitializeAsync();

                OnPageTitleChanged("Clients");
                OnNavigated(page);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Clients navigation error: {ex.Message}");
                ShowNavigationError("Clients", ex);
            }
        }

        public void NavigateToCases()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationService] Navigating to Cases");

            try
            {
                var viewModel = App.GetService<CasesViewModel>();
                var page = new CasesPage { DataContext = viewModel };

                _ = viewModel.InitializeAsync();

                OnPageTitleChanged("Dossiers");
                OnNavigated(page);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Cases navigation error: {ex.Message}");
                ShowNavigationError("Dossiers", ex);
            }
        }

        public void NavigateToDocuments()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationService] Navigating to Documents");

            try
            {
                // FIXED: Create actual DocumentsPage instead of placeholder
                var page = new DocumentsPage();

                OnPageTitleChanged("Documents");
                OnNavigated(page);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Documents navigation error: {ex.Message}");
                ShowNavigationError("Documents", ex);
            }
        }

        public void NavigateToBackup()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationService] Navigating to Backup");

            try
            {
                var viewModel = App.GetService<BackupViewModel>();
                var page = new BackupPage { DataContext = viewModel };

                _ = viewModel.InitializeAsync();

                OnPageTitleChanged("Sauvegarde");
                OnNavigated(page);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Backup navigation error: {ex.Message}");
                ShowNavigationError("Sauvegarde", ex);
            }
        }

        public void NavigateToExpenses()
        {
            var viewModel = App.GetService<ExpensesViewModel>();
            var page = new ExpensesPage { DataContext = viewModel };
            _ = viewModel.InitializeAsync();
            OnPageTitleChanged("Dépenses");
            OnNavigated(page);
        }

        public void NavigateToAppointments()
        {
            var viewModel = App.GetService<AppointmentsViewModel>();
            var page = new AppointmentsPage { DataContext = viewModel };
            _ = viewModel.InitializeAsync();
            OnPageTitleChanged("Rendez-vous");
            OnNavigated(page);
        }

        public void NavigateToSettings()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationService] Navigating to Settings");

            try
            {
                var viewModel = App.GetService<SettingsViewModel>();
                var page = new SettingsPage { DataContext = viewModel };

                _ = viewModel.LoadSettingsAsync();

                OnPageTitleChanged("Paramètres");
                OnNavigated(page);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Settings navigation error: {ex.Message}");
                ShowNavigationError("Paramètres", ex);
            }
        }

        protected virtual void OnNavigated(Page page)
        {
            try
            {
                Navigated?.Invoke(this, page);
                System.Diagnostics.Debug.WriteLine($"[NavigationService] ✅ Navigated to {page.GetType().Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Error raising Navigated event: {ex.Message}");
            }
        }

        protected virtual void OnPageTitleChanged(string title)
        {
            try
            {
                PageTitleChanged?.Invoke(this, title);
                System.Diagnostics.Debug.WriteLine($"[NavigationService] ✅ Title changed to: {title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Error raising PageTitleChanged event: {ex.Message}");
            }
        }

        private void ShowNavigationError(string pageName, Exception ex)
        {
            MessageBox.Show(
                $"Erreur lors de la navigation vers {pageName}:\n\n{ex.Message}\n\nConsultez les logs pour plus de détails.",
                "Erreur de Navigation",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}