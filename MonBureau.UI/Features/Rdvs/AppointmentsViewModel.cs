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
using MonBureau.Infrastructure.Data;
using MonBureau.UI.Features.Rdvs;

namespace MonBureau.UI.Features.Rdvs
{
    /// <summary>
    /// FIXED: Properly inherits from CrudViewModelBase with dashboard statistics
    /// </summary>
    public partial class AppointmentsViewModel : CrudViewModelBase<Appointment>
    {
        private readonly NotificationService _notificationService;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

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
        private string _viewMode = "List";

        public AppointmentsViewModel(
            IUnitOfWork unitOfWork,
            NotificationService notificationService,
            IDbContextFactory<AppDbContext> contextFactory)
            : base(unitOfWork)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        protected override IRepository<Appointment> GetRepository()
            => _unitOfWork.Appointments;

        /// <summary>
        /// FIXED: Database-level filtering for appointments
        /// </summary>
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

        /// <summary>
        /// FIXED: Load dashboard panels after base initialization
        /// </summary>
        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await LoadTodayAppointmentsAsync();
            await LoadUpcomingAppointmentsAsync();
            await CheckPendingRemindersAsync();
        }

        /// <summary>
        /// Load today's appointments with separate context
        /// </summary>
        private async Task LoadTodayAppointmentsAsync()
        {
            try
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                await using var context = await _contextFactory.CreateDbContextAsync();

                var appointments = await context.Appointments
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
                System.Diagnostics.Debug.WriteLine($"[AppointmentsViewModel] Error loading today: {ex.Message}");
            }
        }

        /// <summary>
        /// Load upcoming appointments (next 7 days)
        /// </summary>
        private async Task LoadUpcomingAppointmentsAsync()
        {
            try
            {
                var tomorrow = DateTime.Today.AddDays(1);
                var nextWeek = DateTime.Today.AddDays(7);

                await using var context = await _contextFactory.CreateDbContextAsync();

                var appointments = await context.Appointments
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
                System.Diagnostics.Debug.WriteLine($"[AppointmentsViewModel] Error loading upcoming: {ex.Message}");
            }
        }

        /// <summary>
        /// Check and send pending reminders
        /// </summary>
        private async Task CheckPendingRemindersAsync()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();

                var appointments = await context.Appointments
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

        /// <summary>
        /// Send reminder notification
        /// </summary>
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentsViewModel] Error sending reminder: {ex.Message}");
            }
        }

        #region Commands

        [RelayCommand]
        private async Task ChangeViewMode(string mode)
        {
            ViewMode = mode;
            await RefreshAsync();
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
                ShowError($"Erreur: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// FIXED: Create dialog properly
        /// </summary>
        protected override Window CreateAddDialog()
        {
            var dialog = new AppointmentDialog();
            return dialog;
        }

        /// <summary>
        /// FIXED: Create edit dialog with entity
        /// </summary>
        protected override Window CreateEditDialog(Appointment entity)
        {
            var dialog = new AppointmentDialog(entity);
            return dialog;
        }

        protected override string GetEntityName()
            => "Rendez-vous";

        protected override string GetEntityPluralName()
            => "Rendez-vous";

        protected override string GetEntityDisplayName(Appointment entity)
            => $"{entity.Title} - {entity.StartTime:dd/MM/yyyy HH:mm}";

        /// <summary>
        /// FIXED: Refresh dashboard panels after CRUD operations
        /// </summary>
        protected override async Task RefreshAsync()
        {
            await base.RefreshAsync();
            await LoadTodayAppointmentsAsync();
            await LoadUpcomingAppointmentsAsync();
        }
    }
}