using System;
using System.Diagnostics;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using MonBureau.Infrastructure.Security;
using Newtonsoft.Json;

namespace MonBureau.Infrastructure.Services.Firebase
{
    /// <summary>
    /// Secure Firebase configuration using Windows Credential Manager
    /// FIXED: Added IsInitialized property and diagnostic methods
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
            {
                return _firebaseApp;
            }

            lock (_lock)
            {
                if (_firebaseApp != null && _initialized)
                {
                    return _firebaseApp;
                }

                try
                {
                    Debug.WriteLine("[FirebaseConfig] Starting initialization...");

                    // Retrieve credentials from secure storage
                    string? projectId = SecureCredentialManager.GetSecureValue(
                        "Firebase_ProjectId",
                        "FIREBASE_PROJECT_ID"
                    );

                    string? privateKey = SecureCredentialManager.GetSecureValue(
                        "Firebase_PrivateKey",
                        "FIREBASE_PRIVATE_KEY"
                    );

                    string? clientEmail = SecureCredentialManager.GetSecureValue(
                        "Firebase_ClientEmail",
                        "FIREBASE_CLIENT_EMAIL"
                    );

                    // Validate credentials
                    if (string.IsNullOrEmpty(projectId) ||
                        string.IsNullOrEmpty(privateKey) ||
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
                    Debug.WriteLine($"[FirebaseConfig]   Private Key: {(privateKey.Length > 50 ? "Present" : "Too short")}");

                    // Build service account JSON
                    var serviceAccount = new
                    {
                        type = "service_account",
                        project_id = projectId,
                        private_key = privateKey.Replace("\\n", "\n"),
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
            var hasProjectId = SecureCredentialManager.CredentialExists("monbureau-licenses");
            var hasPrivateKey = SecureCredentialManager.CredentialExists("-----BEGIN PRIVATE KEY-----\nMIIEuwIBADANBgkqhkiG9w0BAQEFAASCBKUwggShAgEAAoIBAQCRGDjgJVBSTHB0\nTuDK5Rlmc045QtQ6ajOBLvaDGZ+SrdRA/cyd6SvRYsvstUwyjj1j4+wwZg2Za7D/\nI++ir+w1xtm2gV6yqkionBKK+5Uo1ExNZo351CeEVZSSKzZRp31Hae/QcuHBBn0g\nNhWw91dsTU4BqenQDtJWVg2H9H8cnVUo94dh/HF52+f2daRcAqOeRIS7cJ93gho+\nx8APyyr+KjhrG83IcdlVDEruQihk7Oe0kFPT2yVmXai3/SqRECLEL+uxlFgJPANU\nOLy+GexDpRz6tQ6X5qoYZpM7SnzL8mB/sHQ9GpWT5WOoiorO2g8EjPyi2j6KqE6R\nVNuSmbuPAgMBAAECgf9yCJd3kqjBOOv1sG771EYiPuOiHUpInsiovwz/L4qg8GPh\nFsqG6rsfCIbWtgzGe9D3F14jeHgEgp7/gNhvaqEHotqsLwELA/1KIJLYqHtfNRTC\n83CiHBtZGYNEkRgf4YL7A+EQWTnhR56pS9iNPQsSE0k7nxu9nLVMvyHjUd77aeMl\nFNj3N9oOPL136fSaFi5VpRAcBE+qN6Ox79yuTfVpSvWe2r5GbNYL2yP8heJ3Nt4G\nwUcPyM1v0UoPqoeSGQAZbemL2tsYXfyRIHiZFzZhuw8W93Pdd70dze1J/g6YTb09\n9WeNqZsj6xNF/GLYRI7ZWQccP8aHmffOTERjqxECgYEAyFYtTYqgvGmk8LUT0L58\n0wdIm3g9E6F8+rA32kXLoZexQxYk/BjC1YbS9qK3zAIKEhkcRyiQFy3Waf+5SI1B\n0T/sozlk8f8Ciws15TY9ahk7/PvNWNjUPdW92YmvtNErWgo8NX2qG5GhaFJZ/4mk\nBaMH21AM93MNNw3YrMZWh7sCgYEAuWi25UvSx0qhV0/Rgoun75vJRjQWAEfcRlkn\nG/gZsnpBhTgw4pI0eMW1h2Pjly8aI+7kZPM68S8WWltwbWtXhCkfpF2B2FylAgoD\nDE1hlar98KNDI6NvjiAfglqtbSYWgS8YUjMjrWio/N/IImNsvqvyM5JSgyzBpcE8\n+wdh7D0CgYEArC4UxZ4tw4Fwt0iJ/VCaa6zI5IYUyDh09+hYOIrgFsQPH796jgih\n+27jBgKXwQjHqwJV4XqlTKair3uPvSFavgMY2LhNYAdyIhrCeXuCkRubCTVJKeFB\nmNuJTdweXWOgxMQjNz4H46XoeYa9vviHNikGaaGFY29InlaSMPxOBl0CgYA2x4R6\nHLvq29bteAy7mE2G0q1WC5+Qd6rSMhcHAXd+LvbayG5REsdkA24N0Wp1yZnckgFy\n/hYlGjdtfOSrv3I8/vV1V5c8eKrb/l9GLDqvwLSEe4gjqG8WO9Fzbx0cSYuOoX57\nEtbwriJ3jBqSZnPDpgPKTuoIDZotQfmlZVn+NQKBgEoCrbe9XBExy1kDR4MHvmSN\njVJBh4dPxeB1ruW4KrrrneLyKOWBmIQpsgKBHNwhwUMeCalWMrONdc0PhwujwxDG\nL+ahREJh2zY7e8OrMKMawvnLtYbRciv4qZkl23LDUrUxjmeS2r/FyTLUqTOEdZmd\nF7MLRLgJdvm5wAJPupv1\n-----END PRIVATE KEY-----\n");
            var hasClientEmail = SecureCredentialManager.CredentialExists("firebase-adminsdk-fbsvc@monbureau-licenses.iam.gserviceaccount.com");

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