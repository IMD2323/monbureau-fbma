using CommunityToolkit.Mvvm.ComponentModel;

namespace MonBureau.UI.ViewModels.Base
{
    public abstract partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _busyMessage = string.Empty;
    }
}