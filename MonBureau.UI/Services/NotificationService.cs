using System;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;

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
    /// Service for showing Windows toast notifications
    /// Uses Windows 10/11 notification center
    /// </summary>
    public class NotificationService
    {
        private const string APP_ID = "MonBureau.LawOffice";

        public NotificationService()
        {
            try
            {
                // Register app for notifications
                ToastNotificationManagerCompat.OnActivated += OnNotificationActivated;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Initialization error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a Windows toast notification
        /// </summary>
        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            try
            {
                var builder = new ToastContentBuilder()
                    .AddText(title, hintMaxLines: 1)
                    .AddText(message)
                    .SetToastDuration(ToastDuration.Long);

                // Add icon based on type
                var iconPath = GetIconPath(type);
                if (!string.IsNullOrEmpty(iconPath))
                {
                    builder.AddAppLogoOverride(new Uri(iconPath), ToastGenericAppLogoCrop.Circle);
                }

                // Add audio for reminders
                if (type == NotificationType.Reminder)
                {
                    builder.AddAudio(new Uri("ms-winsoundevent:Notification.Reminder"));
                }

                // Show notification
                builder.Show();

                System.Diagnostics.Debug.WriteLine($"[NotificationService] Notification shown: {title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Error showing notification: {ex.Message}");

                // Fallback to MessageBox
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, GetMessageBoxImage(type));
                });
            }
        }

        /// <summary>
        /// Shows a notification with action buttons
        /// </summary>
        public void ShowNotificationWithActions(
            string title,
            string message,
            string actionText,
            Action onActionClicked,
            NotificationType type = NotificationType.Info)
        {
            try
            {
                var builder = new ToastContentBuilder()
                    .AddText(title, hintMaxLines: 1)
                    .AddText(message)
                    .AddButton(new ToastButton()
                        .SetContent(actionText)
                        .AddArgument("action", "clicked"))
                    .AddButton(new ToastButtonDismiss("Fermer"))
                    .SetToastDuration(ToastDuration.Long);

                var iconPath = GetIconPath(type);
                if (!string.IsNullOrEmpty(iconPath))
                {
                    builder.AddAppLogoOverride(new Uri(iconPath), ToastGenericAppLogoCrop.Circle);
                }

                builder.Show();

                // Store action for later execution
                _pendingActions[title] = onActionClicked;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Error showing action notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows appointment reminder notification
        /// </summary>
        public void ShowAppointmentReminder(string title, DateTime startTime, string location)
        {
            try
            {
                var timeUntil = startTime - DateTime.Now;
                var timeText = timeUntil.TotalMinutes < 60
                    ? $"Dans {timeUntil.TotalMinutes:F0} minutes"
                    : $"Dans {timeUntil.TotalHours:F1} heures";

                var builder = new ToastContentBuilder()
                    .AddText("🔔 Rappel de Rendez-vous")
                    .AddText(title, hintMaxLines: 1)
                    .AddText($"⏰ {startTime:HH:mm} - {timeText}")
                    .AddText($"📍 {location}")
                    .AddButton(new ToastButton()
                        .SetContent("Voir détails")
                        .AddArgument("action", "view_appointment")
                        .AddArgument("appointmentId", title))
                    .AddButton(new ToastButtonSnooze("Rappeler plus tard"))
                    .AddButton(new ToastButtonDismiss("Fermer"))
                    .SetToastScenario(ToastScenario.Reminder)
                    .AddAudio(new Uri("ms-winsoundevent:Notification.Reminder"));

                builder.Show();

                System.Diagnostics.Debug.WriteLine($"[NotificationService] Appointment reminder shown: {title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Error showing appointment reminder: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears all notifications
        /// </summary>
        public void ClearAllNotifications()
        {
            try
            {
                ToastNotificationManagerCompat.History.Clear();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Error clearing notifications: {ex.Message}");
            }
        }

        private void OnNotificationActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            try
            {
                var args = ToastArguments.Parse(e.Argument);

                if (args.Contains("action"))
                {
                    var action = args["action"];

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (action == "clicked" && _pendingActions.Count > 0)
                        {
                            // Execute pending action
                            var firstAction = _pendingActions.Values.FirstOrDefault();
                            firstAction?.Invoke();
                            _pendingActions.Clear();
                        }
                        else if (action == "view_appointment")
                        {
                            // Navigate to appointments page
                            System.Diagnostics.Debug.WriteLine("[NotificationService] View appointment action triggered");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Error handling notification activation: {ex.Message}");
            }
        }

        private string? GetIconPath(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => "pack://application:,,,/Resources/Icons/success.png",
                NotificationType.Warning => "pack://application:,,,/Resources/Icons/warning.png",
                NotificationType.Error => "pack://application:,,,/Resources/Icons/error.png",
                NotificationType.Reminder => "pack://application:,,,/Resources/Icons/reminder.png",
                _ => null
            };
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

        private readonly Dictionary<string, Action> _pendingActions = new();
    }
}