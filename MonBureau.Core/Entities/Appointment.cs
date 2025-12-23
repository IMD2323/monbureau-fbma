using System;
using System.ComponentModel.DataAnnotations;
using MonBureau.Core.Enums;

namespace MonBureau.Core.Entities
{
    /// <summary>
    /// Appointment (RDV) entity for scheduling meetings
    /// </summary>
    public class Appointment : EntityBase
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [MaxLength(300)]
        public string? Location { get; set; }

        [Required]
        public AppointmentType Type { get; set; }

        [Required]
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

        // Reminder settings
        public bool ReminderEnabled { get; set; } = true;
        public int ReminderMinutesBefore { get; set; } = 30;
        public DateTime? ReminderSentAt { get; set; }

        // Relationships
        public int CaseId { get; set; }
        public virtual Case Case { get; set; } = null!;

        // Meeting with clients (optional)
        [MaxLength(500)]
        public string? Attendees { get; set; }

        // Computed properties
        public int DurationMinutes => (int)(EndTime - StartTime).TotalMinutes;
        public bool IsUpcoming => StartTime > DateTime.Now && Status == AppointmentStatus.Scheduled;
        public bool IsPast => EndTime < DateTime.Now;
        public bool IsToday => StartTime.Date == DateTime.Today;
        public bool NeedsReminder => ReminderEnabled && !ReminderSentAt.HasValue &&
                                     StartTime.Subtract(TimeSpan.FromMinutes(ReminderMinutesBefore)) <= DateTime.Now;
    }
}