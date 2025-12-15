using System;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;

namespace YourApp.Configuration
{
    /// <summary>
    /// Secure Firebase configuration using Windows Credential Manager
    /// </summary>
    public class FirebaseConfig
    {
        private static FirebaseApp _firebaseApp;
        private static readonly object _lock = new object();

        public static FirebaseApp Initialize()
        {
            if (_firebaseApp != null)
            {
                return _firebaseApp;
            }

            lock (_lock)
            {
                if (_firebaseApp != null)
                {
                    return _firebaseApp;
                }

                try
                {
                    // Retrieve credentials from secure storage
                    string projectId = SecureCredentialManager.GetSecureValue(
                        "Firebase_ProjectId",
                        "FIREBASE_PROJECT_ID"
                    );

                    string privateKey = SecureCredentialManager.GetSecureValue(
                        "Firebase_PrivateKey",
                        "FIREBASE_PRIVATE_KEY"
                    );

                    string clientEmail = SecureCredentialManager.GetSecureValue(
                        "Firebase_ClientEmail",
                        "FIREBASE_CLIENT_EMAIL"
                    );

                    if (string.IsNullOrEmpty(projectId) ||
                        string.IsNullOrEmpty(privateKey) ||
                        string.IsNullOrEmpty(clientEmail))
                    {
                        throw new InvalidOperationException(
                            "Firebase credentials not found. Please run the installer to configure credentials."
                        );
                    }

                    // Build service account JSON
                    var serviceAccount = new
                    {
                        type = "service_account",
                        project_id = projectId,
                        private_key = privateKey.Replace("\\n", "\n"), // Handle escaped newlines
                        client_email = clientEmail,
                        token_uri = "https://oauth2.googleapis.com/token"
                    };

                    string jsonCredentials = JsonConvert.SerializeObject(serviceAccount);

                    // Initialize Firebase
                    _firebaseApp = FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromJson(jsonCredentials)
                    });

                    Console.WriteLine("Firebase initialized successfully");
                    return _firebaseApp;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to initialize Firebase: {ex.Message}");
                    throw new InvalidOperationException(
                        "Firebase initialization failed. Check credentials configuration.",
                        ex
                    );
                }
            }
        }

        /// <summary>
        /// Gets the initialized Firebase app instance
        /// </summary>
        public static FirebaseApp GetApp()
        {
            if (_firebaseApp == null)
            {
                throw new InvalidOperationException("Firebase has not been initialized");
            }
            return _firebaseApp;
        }

        /// <summary>
        /// Validates that Firebase credentials are configured
        /// </summary>
        public static bool AreCredentialsConfigured()
        {
            return SecureCredentialManager.CredentialExists("Firebase_ProjectId") &&
                   SecureCredentialManager.CredentialExists("Firebase_PrivateKey") &&
                   SecureCredentialManager.CredentialExists("Firebase_ClientEmail");
        }
    }
}