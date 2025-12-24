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
    /// <summary>
    /// FIXED: String parameter for quick duration command
    /// </summary>
    public partial class AppointmentDialogViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Appointment? _existingAppointment;

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

        public bool IsEditMode => _existingAppointment != null;

        public Array AppointmentTypes => Enum.GetValues(typeof(AppointmentType));
        public Array AppointmentStatuses => Enum.GetValues(typeof(AppointmentStatus));
        public int[] ReminderOptions => new[] { 5, 15, 30, 60, 120, 1440 };

        public AppointmentDialogViewModel(IUnitOfWork unitOfWork, Appointment? appointment = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _existingAppointment = appointment;
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // FIXED: Use GetPagedAsync instead of GetAllAsync
                var cases = await _unitOfWork.Cases.GetPagedAsync(0, 1000);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Cases = new ObservableCollection<Case>(cases.OrderByDescending(c => c.OpeningDate));

                    if (_existingAppointment != null)
                    {
                        LoadAppointmentData();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentDialog] Error loading data: {ex.Message}");
            }
        }

        private void LoadAppointmentData()
        {
            if (_existingAppointment == null) return;

            Title = _existingAppointment.Title;
            Description = _existingAppointment.Description;
            StartDate = _existingAppointment.StartTime.Date;
            StartTime = _existingAppointment.StartTime.ToString("HH:mm");
            EndDate = _existingAppointment.EndTime.Date;
            EndTime = _existingAppointment.EndTime.ToString("HH:mm");
            Location = _existingAppointment.Location;
            SelectedType = _existingAppointment.Type;
            SelectedStatus = _existingAppointment.Status;
            ReminderEnabled = _existingAppointment.ReminderEnabled;
            ReminderMinutesBefore = _existingAppointment.ReminderMinutesBefore;
            Attendees = _existingAppointment.Attendees;
            SelectedCase = Cases.FirstOrDefault(c => c.Id == _existingAppointment.CaseId);
        }

        /// <summary>
        /// FIXED: Accept string parameter from XAML CommandParameter
        /// </summary>
        [RelayCommand]
        private void SetQuickDuration(string minutesStr)
        {
            // Parse string to int
            if (!int.TryParse(minutesStr, out int minutes))
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentDialog] Invalid minutes: {minutesStr}");
                return;
            }

            // Parse start time and add minutes
            if (TimeSpan.TryParse(StartTime, out var startTimeSpan))
            {
                var endTimeSpan = startTimeSpan.Add(TimeSpan.FromMinutes(minutes));

                // Handle day overflow
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
                if (_existingAppointment != null)
                {
                    _existingAppointment.Title = Title;
                    _existingAppointment.Description = Description;
                    _existingAppointment.StartTime = startDateTime;
                    _existingAppointment.EndTime = endDateTime;
                    _existingAppointment.Location = Location;
                    _existingAppointment.Type = SelectedType;
                    _existingAppointment.Status = SelectedStatus;
                    _existingAppointment.ReminderEnabled = ReminderEnabled;
                    _existingAppointment.ReminderMinutesBefore = ReminderMinutesBefore;
                    _existingAppointment.Attendees = Attendees;
                    _existingAppointment.CaseId = SelectedCase.Id;

                    await _unitOfWork.Appointments.UpdateAsync(_existingAppointment);
                }
                else
                {
                    var appointment = new Appointment
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

                    await _unitOfWork.Appointments.AddAsync(appointment);
                }

                await _unitOfWork.SaveChangesAsync();
                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                ValidationError = $"Erreur: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[AppointmentDialog] Save error: {ex.Message}");
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