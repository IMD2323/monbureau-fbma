using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace MonBureau.Infrastructure.Services
{
    /// <summary>
    /// Centralized error handling service
    /// Maps exceptions to user-friendly messages
    /// </summary>
    public class ErrorService
    {
        private readonly string _logPath;

        public ErrorService()
        {
            _logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonBureau",
                "Logs");

            Directory.CreateDirectory(_logPath);
        }

        /// <summary>
        /// Gets user-friendly error message from exception
        /// </summary>
        public string GetUserFriendlyMessage(Exception ex)
        {
            return ex switch
            {
                // Database errors (specific patterns first)
                DbUpdateConcurrencyException =>
                    "Les données ont été modifiées par un autre utilisateur. Veuillez actualiser et réessayer.",
                DbUpdateException dbEx =>
                    HandleDatabaseException(dbEx),
                InvalidOperationException invalidEx when invalidEx.Message.Contains("database") =>
                    "Erreur d'accès à la base de données. Vérifiez que l'application a les permissions nécessaires.",

                // Validation errors (ArgumentNullException before ArgumentException)
                ArgumentNullException argEx =>
                    $"Paramètre requis manquant: {argEx.ParamName}",
                ArgumentException argEx =>
                    $"Valeur invalide: {argEx.Message}",

                // File system errors (specific patterns first)
                UnauthorizedAccessException =>
                    "Accès refusé. L'application n'a pas les permissions nécessaires.",
                FileNotFoundException =>
                    "Fichier introuvable.",
                IOException ioEx when ioEx.Message.Contains("access") =>
                    "Accès au fichier refusé. Vérifiez les permissions.",
                IOException =>
                    "Erreur d'accès au fichier.",

                // Network errors
                TimeoutException =>
                    "L'opération a pris trop de temps. Veuillez réessayer.",
                System.Net.Http.HttpRequestException =>
                    "Erreur de connexion. Vérifiez votre connexion Internet.",

                // Default
                _ => "Une erreur inattendue s'est produite. Consultez les logs pour plus de détails."
            };
        }

        /// <summary>
        /// Handles database-specific exceptions
        /// </summary>
        private string HandleDatabaseException(DbUpdateException dbEx)
        {
            var innerMessage = dbEx.InnerException?.Message ?? string.Empty;

            // SQLite-specific errors
            if (innerMessage.Contains("UNIQUE constraint failed"))
            {
                // Extract which field failed
                if (innerMessage.Contains("Cases.Number"))
                    return "Ce numéro de dossier existe déjà.";
                if (innerMessage.Contains("Clients.ContactEmail"))
                    return "Cette adresse email est déjà utilisée.";

                return "Cette valeur existe déjà dans la base de données.";
            }

            if (innerMessage.Contains("FOREIGN KEY constraint failed"))
            {
                return "Cette action violerait les règles de cohérence des données. Vérifiez les dépendances.";
            }

            if (innerMessage.Contains("NOT NULL constraint failed"))
            {
                return "Un champ obligatoire n'a pas été rempli.";
            }

            if (innerMessage.Contains("database is locked"))
            {
                return "La base de données est temporairement verrouillée. Veuillez réessayer.";
            }

            return "Erreur lors de la sauvegarde des données.";
        }

        /// <summary>
        /// Logs exception details to file
        /// </summary>
        public void LogError(Exception ex, string context = "")
        {
            try
            {
                var logFile = Path.Combine(_logPath, $"error_{DateTime.Now:yyyy-MM-dd}.log");

                var logEntry = $"""
                    
                    ==========================================
                    Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                    Context: {context}
                    Exception: {ex.GetType().FullName}
                    Message: {ex.Message}
                    StackTrace:
                    {ex.StackTrace}
                    {(ex.InnerException != null ? $"Inner Exception: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}" : "")}
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
        /// Gets formatted error details for display
        /// </summary>
        public ErrorDetails GetErrorDetails(Exception ex, string context = "")
        {
            LogError(ex, context);

            return new ErrorDetails
            {
                UserMessage = GetUserFriendlyMessage(ex),
                TechnicalMessage = ex.Message,
                ExceptionType = ex.GetType().Name,
                Context = context,
                Timestamp = DateTime.Now,
                CanRetry = IsRetryableError(ex)
            };
        }

        /// <summary>
        /// Determines if an error is retryable
        /// </summary>
        private bool IsRetryableError(Exception ex)
        {
            return ex switch
            {
                DbUpdateConcurrencyException => true,
                TimeoutException => true,
                System.Net.Http.HttpRequestException => true,
                IOException ioEx when ioEx.Message.Contains("locked") => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets common error messages for validation
        /// </summary>
        public static class CommonMessages
        {
            public const string RequiredField = "Ce champ est obligatoire";
            public const string InvalidEmail = "L'adresse email n'est pas valide";
            public const string InvalidPhone = "Le numéro de téléphone n'est pas valide";
            public const string InvalidDate = "La date n'est pas valide";
            public const string InvalidFormat = "Le format n'est pas valide";
            public const string AlreadyExists = "Cette valeur existe déjà";
            public const string NotFound = "Élément introuvable";
            public const string Unauthorized = "Accès non autorisé";
            public const string ConcurrencyConflict = "Les données ont été modifiées par un autre utilisateur";
            public const string DatabaseError = "Erreur de base de données";
            public const string NetworkError = "Erreur de connexion";
            public const string UnexpectedError = "Erreur inattendue";
        }
    }

    /// <summary>
    /// Detailed error information
    /// </summary>
    public class ErrorDetails
    {
        public string UserMessage { get; set; } = string.Empty;
        public string TechnicalMessage { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool CanRetry { get; set; }

        public string FormattedMessage =>
            $"{UserMessage}\n\nDétails techniques: {TechnicalMessage}\n\nType: {ExceptionType}\nDate: {Timestamp:yyyy-MM-dd HH:mm:ss}";
    }
}