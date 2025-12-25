using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Core.Interfaces;

namespace MonBureau.UI.Features.Rdvs
{
    public partial class AppointmentDialogViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private int? _existingAppointmentId;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string? _description;

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today;

        [ObservableProperty]
        private string _startTime = "09:00";

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        [ObservableProperty]
        private string _endTime = "10:00";

        [ObservableProperty]
        private string? _location;

        [ObservableProperty]
        private AppointmentType _selectedType;

        [ObservableProperty]
        private AppointmentStatus _selectedStatus = AppointmentStatus.Scheduled;

        [ObservableProperty]
        private bool _reminderEnabled = true;

        [ObservableProperty]
        private int _reminderMinutesBefore = 30;

        [ObservableProperty]
        private string? _attendees;

        [ObservableProperty]
        private Case? _selectedCase;

        [ObservableProperty]
        private ObservableCollection<Case> _cases = new();

        [ObservableProperty]
        private string? _validationError;

        public bool IsEditMode => _existingAppointmentId.HasValue;

        public Array AppointmentTypes => Enum.GetValues(typeof(AppointmentType));
        public Array AppointmentStatuses => Enum.GetValues(typeof(AppointmentStatus));
        public int[] ReminderOptions => new[] { 5, 15, 30, 60, 120, 1440 };

        public AppointmentDialogViewModel(IUnitOfWork unitOfWork, Appointment? appointment = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

            if (appointment != null)
            {
                _existingAppointmentId = appointment.Id;
            }

            _ = LoadDataAsync(appointment);
        }

        private async Task LoadDataAsync(Appointment? appointment)
        {
            try
            {
                var cases = await _unitOfWork.Cases.GetPagedAsync(0, 1000);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Cases = new ObservableCollection<Case>(cases.OrderByDescending(c => c.OpeningDate));

                    if (appointment != null)
                    {
                        LoadAppointmentData(appointment);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentDialog] Error loading data: {ex.Message}");
            }
        }

        private void LoadAppointmentData(Appointment appointment)
        {
            Title = appointment.Title;
            Description = appointment.Description;
            StartDate = appointment.StartTime.Date;
            StartTime = appointment.StartTime.ToString("HH:mm");
            EndDate = appointment.EndTime.Date;
            EndTime = appointment.EndTime.ToString("HH:mm");
            Location = appointment.Location;
            SelectedType = appointment.Type;
            SelectedStatus = appointment.Status;
            ReminderEnabled = appointment.ReminderEnabled;
            ReminderMinutesBefore = appointment.ReminderMinutesBefore;
            Attendees = appointment.Attendees;

            // Find and select case
            SelectedCase = Cases.FirstOrDefault(c => c.Id == appointment.CaseId);
        }

        [RelayCommand]
        private void SetQuickDuration(string minutesStr)
        {
            if (!int.TryParse(minutesStr, out int minutes))
            {
                return;
            }

            if (TimeSpan.TryParse(StartTime, out var startTimeSpan))
            {
                var endTimeSpan = startTimeSpan.Add(TimeSpan.FromMinutes(minutes));

                if (endTimeSpan >= TimeSpan.FromDays(1))
                {
                    EndDate = StartDate.AddDays(1);
                    endTimeSpan = endTimeSpan.Subtract(TimeSpan.FromDays(1));
                }
                else
                {
                    EndDate = StartDate;
                }

                EndTime = endTimeSpan.ToString(@"HH\:mm");
            }
        }

        [RelayCommand]
        private async Task Save(Window window)
        {
            ValidationError = null;

            if (string.IsNullOrWhiteSpace(Title))
            {
                ValidationError = "Le titre est obligatoire";
                return;
            }

            if (SelectedCase == null)
            {
                ValidationError = "Veuillez sélectionner un dossier";
                return;
            }

            if (!TimeSpan.TryParse(StartTime, out var startTimeSpan))
            {
                ValidationError = "Heure de début invalide (format: HH:mm)";
                return;
            }

            if (!TimeSpan.TryParse(EndTime, out var endTimeSpan))
            {
                ValidationError = "Heure de fin invalide (format: HH:mm)";
                return;
            }

            var startDateTime = StartDate.Date + startTimeSpan;
            var endDateTime = EndDate.Date + endTimeSpan;

            if (endDateTime <= startDateTime)
            {
                ValidationError = "L'heure de fin doit être après l'heure de début";
                return;
            }

            if ((endDateTime - startDateTime).TotalHours > 12)
            {
                ValidationError = "La durée ne peut pas dépasser 12 heures";
                return;
            }

            try
            {
                Appointment appointmentToSave;

                if (_existingAppointmentId.HasValue)
                {
                    // Edit mode - load fresh entity
                    appointmentToSave = await _unitOfWork.Appointments.GetByIdAsync(_existingAppointmentId.Value);
                    if (appointmentToSave == null)
                    {
                        ValidationError = "Rendez-vous introuvable";
                        return;
                    }

                    // Update properties
                    appointmentToSave.Title = Title;
                    appointmentToSave.Description = Description;
                    appointmentToSave.StartTime = startDateTime;
                    appointmentToSave.EndTime = endDateTime;
                    appointmentToSave.Location = Location;
                    appointmentToSave.Type = SelectedType;
                    appointmentToSave.Status = SelectedStatus;
                    appointmentToSave.ReminderEnabled = ReminderEnabled;
                    appointmentToSave.ReminderMinutesBefore = ReminderMinutesBefore;
                    appointmentToSave.Attendees = Attendees;
                    appointmentToSave.CaseId = SelectedCase.Id;

                    await _unitOfWork.Appointments.UpdateAsync(appointmentToSave);
                }
                else
                {
                    // Create mode
                    appointmentToSave = new Appointment
                    {
                        Title = Title,
                        Description = Description,
                        StartTime = startDateTime,
                        EndTime = endDateTime,
                        Location = Location,
                        Type = SelectedType,
                        Status = SelectedStatus,
                        ReminderEnabled = ReminderEnabled,
                        ReminderMinutesBefore = ReminderMinutesBefore,
                        Attendees = Attendees,
                        CaseId = SelectedCase.Id
                    };

                    await _unitOfWork.Appointments.AddAsync(appointmentToSave);
                }

                await _unitOfWork.SaveChangesAsync();

                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                ValidationError = $"Erreur: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[AppointmentDialog] Save error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[AppointmentDialog] Stack trace: {ex.StackTrace}");
            }
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}