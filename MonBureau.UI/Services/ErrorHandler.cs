using System;
using System.Windows;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace MonBureau.UI.Services
{
    /// <summary>
    /// Centralized error handling service with user-friendly messages
    /// </summary>
    public class ErrorHandler
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MonBureau", "Logs");

        public enum ErrorSeverity
        {
            Info,
            Warning,
            Error,
            Critical
        }

        public class ErrorResult
        {
            public bool IsSuccess { get; set; }
            public string UserMessage { get; set; } = string.Empty;
            public string TechnicalMessage { get; set; } = string.Empty;
            public ErrorSeverity Severity { get; set; }
        }

        static ErrorHandler()
        {
            Directory.CreateDirectory(LogPath);
        }

        /// <summary>
        /// Handles exception and returns user-friendly error message
        /// </summary>
        public static ErrorResult Handle(Exception ex, string operation)
        {
            var result = new ErrorResult
            {
                IsSuccess = false,
                TechnicalMessage = ex.ToString(),
                Severity = DetermineSeverity(ex)
            };

            // Generate user-friendly message
            result.UserMessage = ex switch
            {
                DbUpdateException dbEx => HandleDatabaseException(dbEx, operation),
                UnauthorizedAccessException => "Accès refusé. Vérifiez vos permissions.",
                FileNotFoundException => "Fichier introuvable. Le fichier a peut-être été déplacé ou supprimé.",
                IOException ioEx => HandleIOException(ioEx, operation),
                InvalidOperationException invalidEx => HandleInvalidOperation(invalidEx, operation),
                ArgumentException argEx => $"Données invalides : {argEx.Message}",
                TimeoutException => "L'opération a pris trop de temps. Veuillez réessayer.",
                OutOfMemoryException => "Mémoire insuffisante. Fermez d'autres applications et réessayez.",
                _ => $"Une erreur inattendue s'est produite lors de {operation}."
            };

            // Log the error
            LogError(ex, operation, result.Severity);

            return result;
        }

        /// <summary>
        /// Shows error dialog based on severity
        /// </summary>
        public static void ShowError(ErrorResult error)
        {
            var icon = error.Severity switch
            {
                ErrorSeverity.Critical => MessageBoxImage.Error,
                ErrorSeverity.Error => MessageBoxImage.Error,
                ErrorSeverity.Warning => MessageBoxImage.Warning,
                _ => MessageBoxImage.Information
            };

            var title = error.Severity switch
            {
                ErrorSeverity.Critical => "Erreur Critique",
                ErrorSeverity.Error => "Erreur",
                ErrorSeverity.Warning => "Attention",
                _ => "Information"
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    error.UserMessage,
                    title,
                    MessageBoxButton.OK,
                    icon);
            });
        }

        /// <summary>
        /// Shows error dialog with details option
        /// </summary>
        public static void ShowDetailedError(ErrorResult error)
        {
            var result = MessageBox.Show(
                $"{error.UserMessage}\n\nVoulez-vous voir les détails techniques?",
                "Erreur",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show(
                    error.TechnicalMessage,
                    "Détails Techniques",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Database-specific error handling
        /// </summary>
        private static string HandleDatabaseException(DbUpdateException ex, string operation)
        {
            var innerMessage = ex.InnerException?.Message ?? ex.Message;

            if (innerMessage.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
            {
                return "Impossible de supprimer : cet élément est lié à d'autres données.\n\n" +
                       "Supprimez d'abord les éléments liés.";
            }

            if (innerMessage.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                return "Cet élément existe déjà dans la base de données.\n\n" +
                       "Vérifiez les doublons.";
            }

            if (innerMessage.Contains("locked", StringComparison.OrdinalIgnoreCase))
            {
                return "La base de données est verrouillée.\n\n" +
                       "Attendez quelques secondes et réessayez.";
            }

            return $"Erreur de base de données lors de {operation}.\n\n" +
                   "Vérifiez que la base de données est accessible.";
        }

        /// <summary>
        /// IO-specific error handling
        /// </summary>
        private static string HandleIOException(IOException ex, string operation)
        {
            if (ex.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase))
            {
                return "Le fichier est utilisé par un autre programme.\n\n" +
                       "Fermez les autres programmes et réessayez.";
            }

            if (ex.Message.Contains("disk full", StringComparison.OrdinalIgnoreCase))
            {
                return "Espace disque insuffisant.\n\n" +
                       "Libérez de l'espace et réessayez.";
            }

            return $"Erreur d'accès fichier lors de {operation}.\n\n" +
                   "Vérifiez les permissions et l'espace disque.";
        }

        /// <summary>
        /// Invalid operation error handling
        /// </summary>
        private static string HandleInvalidOperation(InvalidOperationException ex, string operation)
        {
            if (ex.Message.Contains("Sequence contains no elements"))
            {
                return "Aucun élément trouvé.\n\n" +
                       "L'élément recherché n'existe pas ou a été supprimé.";
            }

            return $"Opération invalide : {ex.Message}";
        }

        /// <summary>
        /// Determines error severity
        /// </summary>
        private static ErrorSeverity DetermineSeverity(Exception ex)
        {
            return ex switch
            {
                OutOfMemoryException => ErrorSeverity.Critical,
                UnauthorizedAccessException => ErrorSeverity.Critical,
                DbUpdateException => ErrorSeverity.Error,
                IOException => ErrorSeverity.Error,
                InvalidOperationException => ErrorSeverity.Warning,
                _ => ErrorSeverity.Error
            };
        }

        /// <summary>
        /// Logs error to file
        /// </summary>
        private static void LogError(Exception ex, string operation, ErrorSeverity severity)
        {
            try
            {
                var logFile = Path.Combine(LogPath, $"error_{DateTime.Now:yyyy-MM-dd}.log");
                var logEntry = $"""
                    
                    ==========================================
                    [{severity}] {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                    Operation: {operation}
                    Exception: {ex.GetType().FullName}
                    Message: {ex.Message}
                    StackTrace:
                    {ex.StackTrace}
                    {(ex.InnerException != null ? $"Inner Exception: {ex.InnerException.Message}" : "")}
                    ==========================================
                    
                    """;

                File.AppendAllText(logFile, logEntry);
            }
            catch
            {
                // Swallow logging errors
            }
        }

        /// <summary>
        /// Shows success message
        /// </summary>
        public static void ShowSuccess(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    message,
                    "Succès",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        }

        /// <summary>
        /// Shows confirmation dialog
        /// </summary>
        public static bool Confirm(string message, string title = "Confirmation")
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                return result == MessageBoxResult.Yes;
            });
        }

        /// <summary>
        /// Shows warning dialog
        /// </summary>
        public static void ShowWarning(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    message,
                    "Attention",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });
        }
    }
}