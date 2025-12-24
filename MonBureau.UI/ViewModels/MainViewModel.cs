using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonBureau.UI.Services;

namespace MonBureau.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private string _currentPageTitle = "Tableau de Bord";

        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _userName = "Utilisateur";

        public MainViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // Initialize with dashboard  
            NavigateToDashboard();
        }

        [RelayCommand]
        private void NavigateToDashboard()
        {
            CurrentPageTitle = "Tableau de Bord";
            // In actual implementation, set CurrentView to DashboardPage  
        }

        [RelayCommand]
        private void OpenSettings()
        {
            // Open settings flyout  
        }

        [RelayCommand]
        private void OpenBackup()
        {
            // Open backup dialog  
        }

        [RelayCommand]
        private void Logout()
        {
            // Handle logout  
        }

        [RelayCommand]
        private void NavigateToExpenses()
        {
            _navigationService.NavigateToExpenses();
        }

        [RelayCommand]
        private void NavigateToAppointments()
        {
            _navigationService.NavigateToAppointments();
        }
    }
}