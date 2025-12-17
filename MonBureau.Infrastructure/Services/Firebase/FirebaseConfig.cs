using System;
using System.Diagnostics;
using System.Text;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using MonBureau.Infrastructure.Security;
using Newtonsoft.Json;

namespace MonBureau.Infrastructure.Services.Firebase
{
    /// <summary>
    /// Secure Firebase configuration using Windows Credential Manager
    /// Supports Base64-encoded private key for easier setup
    /// </summary>
    public static class FirebaseConfig
    {
        private static FirebaseApp? _firebaseApp;
        private static readonly object _lock = new object();
        private static bool _initialized = false;
        private static string? _initializationError = null;

        /// <summary>
        /// Gets whether Firebase has been successfully initialized
        /// </summary>
        public static bool IsInitialized => _initialized && _firebaseApp != null;

        /// <summary>
        /// Gets the initialization error message if initialization failed
        /// </summary>
        public static string? InitializationError => _initializationError;

        /// <summary>
        /// Initializes Firebase with credentials from Windows Credential Manager
        /// </summary>
        public static FirebaseApp Initialize()
        {
            if (_firebaseApp != null && _initialized)
                return _firebaseApp;

            lock (_lock)
            {
                if (_firebaseApp != null && _initialized)
                    return _firebaseApp;

                try
                {
                    Debug.WriteLine("[FirebaseConfig] Starting initialization...");

                    // Retrieve credentials from secure storage
                    string? projectId = SecureCredentialManager.GetSecureValue("Firebase_ProjectId", "FIREBASE_PROJECT_ID");
                    string? privateKeyBase64 = SecureCredentialManager.GetSecureValue("Firebase_PrivateKey", "FIREBASE_PRIVATE_KEY");
                    string? clientEmail = SecureCredentialManager.GetSecureValue("Firebase_ClientEmail", "FIREBASE_CLIENT_EMAIL");

                    // Validate credentials
                    if (string.IsNullOrEmpty(projectId) ||
                        string.IsNullOrEmpty(privateKeyBase64) ||
                        string.IsNullOrEmpty(clientEmail))
                    {
                        _initializationError = "Firebase credentials not found in Credential Manager. Run SetupCredentials.ps1 first.";
                        Debug.WriteLine($"[FirebaseConfig] ❌ {_initializationError}");
                        _initialized = false;
                        return null!;
                    }

                    Debug.WriteLine($"[FirebaseConfig] Credentials retrieved:");
                    Debug.WriteLine($"[FirebaseConfig]   Project ID: {projectId}");
                    Debug.WriteLine($"[FirebaseConfig]   Client Email: {clientEmail}");
                    Debug.WriteLine($"[FirebaseConfig]   Private Key: {(privateKeyBase64.Length > 50 ? "Present" : "Too short")}");

                    // Decode Base64 private key
                    string privateKey = Encoding.UTF8.GetString(Convert.FromBase64String(privateKeyBase64));

                    // Build service account JSON
                    var serviceAccount = new
                    {
                        type = "service_account",
                        project_id = projectId,
                        private_key = privateKey,
                        client_email = clientEmail,
                        token_uri = "https://oauth2.googleapis.com/token"
                    };

                    string jsonCredentials = JsonConvert.SerializeObject(serviceAccount);

                    // Initialize Firebase
                    _firebaseApp = FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromJson(jsonCredentials),
                        ProjectId = projectId
                    });

                    _initialized = true;
                    _initializationError = null;

                    Debug.WriteLine("[FirebaseConfig] ✅ Firebase initialized successfully");
                    return _firebaseApp;
                }
                catch (Exception ex)
                {
                    _initializationError = $"Firebase initialization failed: {ex.Message}";
                    Debug.WriteLine($"[FirebaseConfig] ❌ {_initializationError}");
                    Debug.WriteLine($"[FirebaseConfig] StackTrace: {ex.StackTrace}");
                    _initialized = false;
                    return null!;
                }
            }
        }

        /// <summary>
        /// Gets the initialized Firebase app instance
        /// </summary>
        public static FirebaseApp? GetApp()
        {
            if (!_initialized || _firebaseApp == null)
            {
                Debug.WriteLine("[FirebaseConfig] ⚠️ Firebase not initialized");
                return null;
            }
            return _firebaseApp;
        }

        /// <summary>
        /// Validates that Firebase credentials are configured
        /// </summary>
        public static bool AreCredentialsConfigured()
        {
            var hasProjectId = SecureCredentialManager.CredentialExists("Firebase_ProjectId");
            var hasPrivateKey = SecureCredentialManager.CredentialExists("Firebase_PrivateKey");
            var hasClientEmail = SecureCredentialManager.CredentialExists("Firebase_ClientEmail");

            Debug.WriteLine($"[FirebaseConfig] Credentials check:");
            Debug.WriteLine($"[FirebaseConfig]   Project ID: {hasProjectId}");
            Debug.WriteLine($"[FirebaseConfig]   Private Key: {hasPrivateKey}");
            Debug.WriteLine($"[FirebaseConfig]   Client Email: {hasClientEmail}");

            return hasProjectId && hasPrivateKey && hasClientEmail;
        }

        /// <summary>
        /// Gets diagnostic information about Firebase initialization
        /// </summary>
        public static string GetDiagnosticInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== Firebase Configuration Diagnostic ===");
            info.AppendLine($"Initialized: {IsInitialized}");
            info.AppendLine($"Credentials Configured: {AreCredentialsConfigured()}");

            if (!string.IsNullOrEmpty(_initializationError))
            {
                info.AppendLine($"Error: {_initializationError}");
            }

            if (_firebaseApp != null)
            {
                info.AppendLine($"App Name: {_firebaseApp.Name}");
                info.AppendLine($"Project ID: {_firebaseApp.Options.ProjectId}");
            }

            info.AppendLine("=========================================");
            return info.ToString();
        }

        /// <summary>
        /// Resets Firebase initialization (for testing purposes)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                if (_firebaseApp != null)
                {
                    try
                    {
                        _firebaseApp.Delete();
                    }
                    catch { }
                    _firebaseApp = null;
                }
                _initialized = false;
                _initializationError = null;
                Debug.WriteLine("[FirebaseConfig] Reset completed");
            }
        }
    }
}
