using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MonBureau.UI.Services
{
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error,
        Reminder
    }

    /// <summary>
    /// FIXED: Fallback notification service using MessageBox
    /// Windows Toast Notifications require Windows 10/11 and proper UWP registration
    /// This version works on all Windows versions
    /// </summary>
    public class NotificationService
    {
        private readonly Queue<NotificationMessage> _notificationQueue = new();
        private const int MAX_QUEUE_SIZE = 10;

        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Showing notification: {title}");

                // Add to queue for history
                _notificationQueue.Enqueue(new NotificationMessage
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.Now
                });

                // Keep queue size manageable
                while (_notificationQueue.Count > MAX_QUEUE_SIZE)
                {
                    _notificationQueue.Dequeue();
                }

                // Show notification on UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var icon = GetMessageBoxImage(type);
                    MessageBox.Show(message, title, MessageBoxButton.OK, icon);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Error: {ex.Message}");
            }
        }

        public void ShowNotificationWithActions(
            string title,
            string message,
            string actionText,
            Action onActionClicked,
            NotificationType type = NotificationType.Info)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var result = MessageBox.Show(
                        $"{message}\n\nVoulez-vous {actionText.ToLower()}?",
                        title,
                        MessageBoxButton.YesNo,
                        GetMessageBoxImage(type)
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        onActionClicked?.Invoke();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Error: {ex.Message}");
            }
        }

        public void ShowAppointmentReminder(string title, DateTime startTime, string location)
        {
            try
            {
                var timeUntil = startTime - DateTime.Now;
                var timeText = timeUntil.TotalMinutes < 60
                    ? $"Dans {timeUntil.TotalMinutes:F0} minutes"
                    : $"Dans {timeUntil.TotalHours:F1} heures";

                var message = $"📅 {title}\n⏰ {startTime:HH:mm} - {timeText}\n📍 {location}";

                ShowNotification("🔔 Rappel de Rendez-vous", message, NotificationType.Reminder);

                System.Diagnostics.Debug.WriteLine($"[NotificationService] Appointment reminder shown: {title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Error: {ex.Message}");
            }
        }

        public void ClearAllNotifications()
        {
            _notificationQueue.Clear();
        }

        public IEnumerable<NotificationMessage> GetRecentNotifications()
        {
            return _notificationQueue.ToList();
        }

        private MessageBoxImage GetMessageBoxImage(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => MessageBoxImage.Information,
                NotificationType.Warning => MessageBoxImage.Warning,
                NotificationType.Error => MessageBoxImage.Error,
                NotificationType.Reminder => MessageBoxImage.Information,
                _ => MessageBoxImage.Information
            };
        }

        public class NotificationMessage
        {
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public NotificationType Type { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}