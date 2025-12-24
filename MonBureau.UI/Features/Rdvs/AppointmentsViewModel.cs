using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Core.Interfaces;
using MonBureau.UI.Services;
using MonBureau.UI.ViewModels.Base;
using MonBureau.UI.Views.Dialogs;
using MonBureau.UI.Features.Rdvs;
using MonBureau.UI.Features.Cases;
using MonBureau.Infrastructure.Data;

namespace MonBureau.UI.Features.Rdvs
{
    /// <summary>
    /// FIXED: Added missing IsLoading property and fixed repository usage
    /// </summary>
    public partial class AppointmentsViewModel : CrudViewModelBase<Appointment>
    {
        private readonly NotificationService _notificationService;
        private readonly AppDbContext _context;

        [ObservableProperty]
        private ObservableCollection<Appointment> _todayAppointments = new();

        [ObservableProperty]
        private ObservableCollection<Appointment> _upcomingAppointments = new();

        [ObservableProperty]
        private int _todayCount;

        [ObservableProperty]
        private int _upcomingCount;

        [ObservableProperty]
        private DateTime _selectedDate = DateTime.Today;

        [ObservableProperty]
        private string _viewMode = "List"; // List, Calendar, Day

        public AppointmentsViewModel(
            IUnitOfWork unitOfWork,
            NotificationService notificationService,
            AppDbContext context)
            : base(unitOfWork)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        protected override IRepository<Appointment> GetRepository()
            => _unitOfWork.Appointments;

        protected override Expression<Func<Appointment, bool>>? BuildFilterExpression(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return null;

            var lowerSearch = searchText.ToLowerInvariant();

            return a =>
                (a.Title != null && a.Title.ToLower().Contains(lowerSearch)) ||
                (a.Description != null && a.Description.ToLower().Contains(lowerSearch)) ||
                (a.Location != null && a.Location.ToLower().Contains(lowerSearch)) ||
                (a.Case != null && a.Case.Number != null && a.Case.Number.ToLower().Contains(lowerSearch)) ||
                (a.Case != null && a.Case.Client != null &&
                    ((a.Case.Client.FirstName != null && a.Case.Client.FirstName.ToLower().Contains(lowerSearch)) ||
                     (a.Case.Client.LastName != null && a.Case.Client.LastName.ToLower().Contains(lowerSearch))));
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await LoadTodayAppointmentsAsync();
            await LoadUpcomingAppointmentsAsync();
            await CheckPendingRemindersAsync();
        }

        /// <summary>
        /// FIXED: Use DbContext directly instead of deprecated repository methods
        /// </summary>
        private async Task LoadTodayAppointmentsAsync()
        {
            try
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var appointments = await _context.Appointments
                    .AsNoTracking()
                    .Include(a => a.Case)
                        .ThenInclude(c => c.Client)
                    .Where(a =>
                        a.StartTime >= today &&
                        a.StartTime < tomorrow &&
                        a.Status != AppointmentStatus.Cancelled)
                    .OrderBy(a => a.StartTime)
                    .ToListAsync();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    TodayAppointments = new ObservableCollection<Appointment>(appointments);
                    TodayCount = TodayAppointments.Count;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentsViewModel] Error loading today's appointments: {ex.Message}");
            }
        }

        /// <summary>
        /// FIXED: Use DbContext directly instead of deprecated repository methods
        /// </summary>
        private async Task LoadUpcomingAppointmentsAsync()
        {
            try
            {
                var tomorrow = DateTime.Today.AddDays(1);
                var nextWeek = DateTime.Today.AddDays(7);

                var appointments = await _context.Appointments
                    .AsNoTracking()
                    .Include(a => a.Case)
                        .ThenInclude(c => c.Client)
                    .Where(a =>
                        a.StartTime >= tomorrow &&
                        a.StartTime < nextWeek &&
                        a.Status != AppointmentStatus.Cancelled)
                    .OrderBy(a => a.StartTime)
                    .ToListAsync();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpcomingAppointments = new ObservableCollection<Appointment>(appointments);
                    UpcomingCount = UpcomingAppointments.Count;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentsViewModel] Error loading upcoming appointments: {ex.Message}");
            }
        }

        /// <summary>
        /// FIXED: Use DbContext directly
        /// </summary>
        private async Task CheckPendingRemindersAsync()
        {
            try
            {
                var appointments = await _context.Appointments
                    .AsNoTracking()
                    .Where(a =>
                        a.ReminderEnabled &&
                        !a.ReminderSentAt.HasValue &&
                        a.Status == AppointmentStatus.Scheduled)
                    .ToListAsync();

                foreach (var appointment in appointments)
                {
                    if (appointment.NeedsReminder)
                    {
                        await SendReminderAsync(appointment);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentsViewModel] Error checking reminders: {ex.Message}");
            }
        }

        private async Task SendReminderAsync(Appointment appointment)
        {
            try
            {
                _notificationService.ShowNotification(
                    "Rappel de Rendez-vous",
                    $"{appointment.Title}\n{appointment.StartTime:HH:mm} - {appointment.Location}",
                    NotificationType.Reminder
                );

                appointment.ReminderSentAt = DateTime.UtcNow;
                await GetRepository().UpdateAsync(appointment);
                await _unitOfWork.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"[AppointmentsViewModel] Reminder sent for: {appointment.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentsViewModel] Error sending reminder: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ChangeViewMode(string mode)
        {
            ViewMode = mode;
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task SelectDate(DateTime date)
        {
            SelectedDate = date;
            await LoadAppointmentsForDateAsync(date);
        }

        /// <summary>
        /// FIXED: Use DbContext directly
        /// </summary>
        private async Task LoadAppointmentsForDateAsync(DateTime date)
        {
            try
            {
                var nextDay = date.AddDays(1);

                var appointments = await _context.Appointments
                    .AsNoTracking()
                    .Include(a => a.Case)
                        .ThenInclude(c => c.Client)
                    .Where(a => a.StartTime >= date && a.StartTime < nextDay)
                    .OrderBy(a => a.StartTime)
                    .ToListAsync();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Items = new ObservableCollection<Appointment>(appointments);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentsViewModel] Error loading appointments for date: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task MarkAsCompleted(Appointment? appointment)
        {
            if (appointment == null) return;

            try
            {
                appointment.Status = AppointmentStatus.Completed;
                await GetRepository().UpdateAsync(appointment);
                await _unitOfWork.SaveChangesAsync();

                await RefreshAsync();

                _notificationService.ShowNotification(
                    "Rendez-vous Terminé",
                    $"{appointment.Title} a été marqué comme terminé",
                    NotificationType.Success
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentsViewModel] Error marking as completed: {ex.Message}");
                ShowError("Erreur lors de la mise à jour du rendez-vous");
            }
        }

        protected override Window CreateAddDialog()
            => new AppointmentDialog();

        protected override Window CreateEditDialog(Appointment entity)
            => new AppointmentDialog(entity);

        protected override string GetEntityName()
            => "Rendez-vous";

        protected override string GetEntityPluralName()
            => "Rendez-vous";

        protected override string GetEntityDisplayName(Appointment entity)
            => $"{entity.Title} - {entity.StartTime:dd/MM/yyyy HH:mm}";
    }
}