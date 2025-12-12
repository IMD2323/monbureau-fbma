using System;
using System.IO;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

namespace MonBureau.Infrastructure.Services.Firebase
{
    public static class FirebaseConfig
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initialize Firebase with proper error handling and diagnostics
        /// </summary>
        public static void Initialize(string credentialsPath)
        {
            lock (_lock)
            {
                if (_initialized)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Firebase already initialized");
                    return;
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig] Starting initialization...");
                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig] Credentials path: {credentialsPath}");

                    // Verify file exists
                    if (!File.Exists(credentialsPath))
                    {
                        throw new FileNotFoundException(
                            $"Firebase credentials file not found at: {credentialsPath}",
                            credentialsPath
                        );
                    }

                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig] Credentials file found");
                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig] File size: {new FileInfo(credentialsPath).Length} bytes");

                    // Check if Firebase is already initialized
                    if (FirebaseApp.DefaultInstance != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[FirebaseConfig] ℹ️ Using existing FirebaseApp instance");
                        _initialized = true;
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine("[FirebaseConfig] Creating new FirebaseApp instance...");

                    // Create Firebase app
                    var credential = GoogleCredential.FromFile(credentialsPath);
                    System.Diagnostics.Debug.WriteLine("[FirebaseConfig] ✅ Credentials loaded successfully");

                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = credential,
                        ProjectId = "monbureau-licenses" // Your Firebase project ID
                    });

                    System.Diagnostics.Debug.WriteLine("[FirebaseConfig] ✅ FirebaseApp created successfully");

                    _initialized = true;
                    System.Diagnostics.Debug.WriteLine("[FirebaseConfig] ✅✅✅ Firebase initialization COMPLETE");
                }
                catch (FileNotFoundException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig] ❌ FILE NOT FOUND");
                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig]    Path: {ex.FileName}");
                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig]    Message: {ex.Message}");
                    throw new InvalidOperationException(
                        $"Firebase credentials file not found. Please ensure the file exists at: {credentialsPath}",
                        ex
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig] ❌ INITIALIZATION FAILED");
                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig]    Exception Type: {ex.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig]    Message: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[FirebaseConfig]    StackTrace: {ex.StackTrace}");

                    if (ex.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FirebaseConfig]    Inner Exception: {ex.InnerException.Message}");
                    }

                    throw new InvalidOperationException("Failed to initialize Firebase. See inner exception for details.", ex);
                }
            }
        }

        /// <summary>
        /// Check if Firebase has been initialized
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// Get diagnostic information about Firebase status
        /// </summary>
        public static string GetDiagnosticInfo()
        {
            var info = $"Firebase Initialized: {_initialized}\n";

            try
            {
                if (FirebaseApp.DefaultInstance != null)
                {
                    info += "FirebaseApp Instance: EXISTS\n";
                    info += $"Project ID: {FirebaseApp.DefaultInstance.Options.ProjectId ?? "NOT SET"}\n";
                }
                else
                {
                    info += "FirebaseApp Instance: NULL\n";
                }
            }
            catch (Exception ex)
            {
                info += $"Error getting diagnostic info: {ex.Message}\n";
            }

            return info;
        }
    }
}