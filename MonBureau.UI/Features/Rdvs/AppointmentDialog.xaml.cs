// MonBureau.UI/Features/Rdvs/AppointmentDialog.xaml.cs
using System.Windows;
using MonBureau.Core.Entities;

namespace MonBureau.UI.Features.Rdvs
{
    public partial class AppointmentDialog : Window
    {
        public AppointmentDialog(Appointment? appointment = null)
        {
            InitializeComponent();
            var viewModel = App.GetService<AppointmentDialogViewModel>();
            DataContext = viewModel;
        }
    }
}