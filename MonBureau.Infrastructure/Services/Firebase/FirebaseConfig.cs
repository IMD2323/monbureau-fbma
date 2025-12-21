using System;
using System.Diagnostics;
using System.Text;
using MonBureau.Infrastructure.Security;

namespace MonBureau.Infrastructure.Services.Firebase
{
    /// <summary>
    /// Firebase Web SDK configuration using REST API
    /// Much simpler than Admin SDK and better suited for desktop clients
    /// </summary>
    public static class FirebaseConfig
    {
        private static bool _initialized = false;
        private static string? _initializationError = null;
        private static FirebaseWebConfig? _config = null;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets whether Firebase has been successfully initialized
        /// </summary>
        public static bool IsInitialized => _initialized && _config != null;

        /// <summary>
        /// Gets the initialization error message if initialization failed
        /// </summary>
        public static string? InitializationError => _initializationError;

        /// <summary>
        /// Gets the current Firebase configuration
        /// </summary>
        public static FirebaseWebConfig? Config => _config;

        /// <summary>
        /// Initializes Firebase with credentials from Windows Credential Manager
        /// </summary>
        public static bool Initialize()
        {
            if (_initialized && _config != null)
                return true;

            lock (_lock)
            {
                if (_initialized && _config != null)
                    return true;

                try
                {
                    Debug.WriteLine("[FirebaseConfig] Starting Web SDK initialization...");

                    // Retrieve credentials from secure storage
                    string? apiKey = SecureCredentialManager.GetSecureValue("Firebase_ApiKey", "FIREBASE_API_KEY");
                    string? projectId = SecureCredentialManager.GetSecureValue("Firebase_ProjectId", "FIREBASE_PROJECT_ID");
                    string? databaseUrl = SecureCredentialManager.GetSecureValue("Firebase_DatabaseUrl", "FIREBASE_DATABASE_URL");

                    // Validate credentials
                    if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(projectId))
                    {
                        _initializationError = "Firebase credentials not found. Run SetupCredentials.ps1 first.";
                        Debug.WriteLine($"[FirebaseConfig] ❌ {_initializationError}");
                        _initialized = false;
                        return false;
                    }

                    Debug.WriteLine($"[FirebaseConfig] Credentials retrieved:");
                    Debug.WriteLine($"[FirebaseConfig]   API Key: {(apiKey.Length > 10 ? apiKey.Substring(0, 10) + "..." : "Invalid")}");
                    Debug.WriteLine($"[FirebaseConfig]   Project ID: {projectId}");
                    Debug.WriteLine($"[FirebaseConfig]   Database URL: {databaseUrl ?? "Not set"}");

                    // Create configuration
                    _config = new FirebaseWebConfig
                    {
                        ApiKey = apiKey,
                        ProjectId = projectId,
                        DatabaseUrl = databaseUrl ?? $"https://{projectId}.firebaseio.com"
                    };

                    _initialized = true;
                    _initializationError = null;

                    Debug.WriteLine("[FirebaseConfig] ✅ Firebase Web SDK initialized successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    _initializationError = $"Firebase initialization failed: {ex.Message}";
                    Debug.WriteLine($"[FirebaseConfig] ❌ {_initializationError}");
                    Debug.WriteLine($"[FirebaseConfig] StackTrace: {ex.StackTrace}");
                    _initialized = false;
                    return false;
                }
            }
        }

        /// <summary>
        /// Validates that Firebase credentials are configured
        /// </summary>
        public static bool AreCredentialsConfigured()
        {
            var hasApiKey = SecureCredentialManager.CredentialExists("Firebase_ApiKey");
            var hasProjectId = SecureCredentialManager.CredentialExists("Firebase_ProjectId");

            Debug.WriteLine($"[FirebaseConfig] Credentials check:");
            Debug.WriteLine($"[FirebaseConfig]   API Key: {hasApiKey}");
            Debug.WriteLine($"[FirebaseConfig]   Project ID: {hasProjectId}");

            return hasApiKey && hasProjectId;
        }

        /// <summary>
        /// Gets diagnostic information about Firebase initialization
        /// </summary>
        public static string GetDiagnosticInfo()
        {
            var info = new StringBuilder();
            info.AppendLine("=== Firebase Web SDK Configuration ===");
            info.AppendLine($"Initialized: {IsInitialized}");
            info.AppendLine($"Credentials Configured: {AreCredentialsConfigured()}");

            if (!string.IsNullOrEmpty(_initializationError))
            {
                info.AppendLine($"Error: {_initializationError}");
            }

            if (_config != null)
            {
                info.AppendLine($"Project ID: {_config.ProjectId}");
                info.AppendLine($"Database URL: {_config.DatabaseUrl}");
            }

            info.AppendLine("=====================================");
            return info.ToString();
        }

        /// <summary>
        /// Resets Firebase initialization (for testing purposes)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _config = null;
                _initialized = false;
                _initializationError = null;
                Debug.WriteLine("[FirebaseConfig] Reset completed");
            }
        }
    }

    /// <summary>
    /// Firebase Web SDK configuration model
    /// </summary>
    public class FirebaseWebConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string DatabaseUrl { get; set; } = string.Empty;

        // Firestore REST API endpoint
        public string FirestoreEndpoint => $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";
    }
}