using System;
using System.Windows;
using System.Windows.Controls;
using MonBureau.UI.Features.Backup;
using MonBureau.UI.Features.Cases;
using MonBureau.UI.Features.Clients;
using MonBureau.UI.Features.Documents;
using MonBureau.UI.Features.Expenses;
using MonBureau.UI.Features.Rdvs;
using MonBureau.UI.ViewModels;
using MonBureau.UI.Views.Pages;

namespace MonBureau.UI.Services
{
    public interface INavigationService
    {
        void NavigateToDashboard();
        void NavigateToClients();
        void NavigateToCases();
        void NavigateToDocuments();
        void NavigateToExpenses();
        void NavigateToAppointments();
        void NavigateToBackup();
        void NavigateToSettings();

        event EventHandler<Page>? Navigated;
        event EventHandler<string>? PageTitleChanged;
    }

    /// <summary>
    /// FIXED: Complete navigation service with all pages including Expenses and Appointments
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
                ShowNavigationError("Tableau de Bord", ex);
            }
        }

        public void NavigateToClients()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationService] Navigating to Clients");

            try
            {
                var viewModel = App.GetService<ClientsViewModel>();
                var page = new Features.Clients.ClientsPage { DataContext = viewModel };

                _ = viewModel.InitializeAsync();

                OnPageTitleChanged("Clients");
                OnNavigated(page);
            }
            catch (Exception ex)
            {
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
                ShowNavigationError("Dossiers", ex);
            }
        }

        public void NavigateToDocuments()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationService] Navigating to Documents");

            try
            {
                var viewModel = App.GetService<DocumentsViewModel>();
                var page = new Features.Documents.DocumentsPage { DataContext = viewModel };

                _ = viewModel.InitializeAsync();

                OnPageTitleChanged("Documents");
                OnNavigated(page);
            }
            catch (Exception ex)
            {
                ShowNavigationError("Documents", ex);
            }
        }

        /// <summary>
        /// FIXED: Added missing NavigateToExpenses method
        /// </summary>
        public void NavigateToExpenses()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationService] Navigating to Expenses");

            try
            {
                var viewModel = App.GetService<ExpensesViewModel>();
                var page = new ExpensesPage { DataContext = viewModel };

                _ = viewModel.InitializeAsync();

                OnPageTitleChanged("Dépenses");
                OnNavigated(page);
            }
            catch (Exception ex)
            {
                ShowNavigationError("Dépenses", ex);
            }
        }

        /// <summary>
        /// FIXED: Added missing NavigateToAppointments method
        /// </summary>
        public void NavigateToAppointments()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationService] Navigating to Appointments");

            try
            {
                var viewModel = App.GetService<AppointmentsViewModel>();
                var page = new Features.Rdvs.AppointmentsPage { DataContext = viewModel };

                _ = viewModel.InitializeAsync();

                OnPageTitleChanged("Rendez-vous");
                OnNavigated(page);
            }
            catch (Exception ex)
            {
                ShowNavigationError("Rendez-vous", ex);
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
                ShowNavigationError("Sauvegarde", ex);
            }
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
            System.Diagnostics.Debug.WriteLine($"[NavigationService] ❌ Error navigating to {pageName}: {ex.Message}");

            MessageBox.Show(
                $"Erreur lors de la navigation vers {pageName}:\n\n{ex.Message}\n\nConsultez les logs pour plus de détails.",
                "Erreur de Navigation",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}