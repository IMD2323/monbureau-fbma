using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Grpc.Auth;

namespace MonBureau.Infrastructure.Services.Firebase
{
    /// <summary>
    /// UPDATED: Firestore-based license service with lifetime license support
    /// </summary>
    public class FirestoreLicenseService
    {
        private readonly FirestoreDb? _firestore;
        private const int MAX_ACTIVATION_ATTEMPTS = 5;
        private const int GRACE_PERIOD_DAYS = 7;
        private readonly string _projectId;

        public FirestoreLicenseService(string projectId = "monbureau-licenses")
        {
            _projectId = projectId;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] Initializing for project: {projectId}");

                if (!FirebaseConfig.IsInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] ⚠️ Firebase NOT initialized");
                    _firestore = null;
                    return;
                }

                var credential = FirebaseAdmin.FirebaseApp.DefaultInstance.Options.Credential;

                var firestoreClientBuilder = new Google.Cloud.Firestore.V1.FirestoreClientBuilder
                {
                    ChannelCredentials = credential.ToChannelCredentials()
                };

                var firestoreClient = firestoreClientBuilder.Build();
                _firestore = FirestoreDb.Create(projectId, firestoreClient);

                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ✅ Firestore connected");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ❌ Init failed: {ex.Message}");
                _firestore = null;
            }
        }

        public bool IsOnline => _firestore != null;

        /// <summary>
        /// UPDATED: Validates license with lifetime support
        /// </summary>
        public async Task<(bool IsValid, string Message)> ValidateLicenseAsync(string licenseKey, string deviceId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ValidateLicenseAsync called");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    License: {licenseKey}");

                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    return (false, "Clé de licence invalide");
                }

                if (_firestore == null)
                {
                    System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] ⚠️ Offline mode");
                    return ValidateOffline(licenseKey, deviceId);
                }

                var docRef = _firestore.Collection("licenses").Document(SanitizeKey(licenseKey));
                var snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] ❌ License NOT FOUND");
                    return (false, "Clé de licence non reconnue");
                }

                var license = snapshot.ConvertTo<LicenseData>();

                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] License data:");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    IsActive: {license.IsActive}");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    IsLifetime: {license.IsLifetime}");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    ExpirationDate: {license.ExpirationDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    IsExpired: {license.IsExpired}");

                // Check if license is active
                if (!license.IsActive)
                {
                    return (false, "Cette licence a été désactivée");
                }

                // UPDATED: Check expiration (handles lifetime licenses)
                if (license.IsExpired)
                {
                    return (false, $"Licence expirée le {license.ExpirationDate:dd/MM/yyyy}");
                }

                // Check trial expiration
                if (license.IsTrialExpired)
                {
                    return (false, "Période d'essai expirée");
                }

                // Check device binding
                if (!string.IsNullOrEmpty(license.DeviceId) && license.DeviceId != deviceId)
                {
                    return (false, "Cette licence est déjà activée sur un autre appareil");
                }

                System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] ✅ License validation PASSED");

                // Update last validation timestamp
                await UpdateLastValidation(licenseKey);

                // UPDATED: Better message for lifetime licenses
                string message;
                if (license.IsLifetime || !license.ExpirationDate.HasValue)
                {
                    message = "Licence à vie validée avec succès";
                }
                else
                {
                    var daysRemaining = license.DaysRemaining;
                    message = daysRemaining <= 30
                        ? $"Licence valide (expire dans {daysRemaining} jours)"
                        : "Licence validée avec succès";
                }

                return (true, message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ❌ Validation error: {ex.Message}");
                return ValidateOffline(licenseKey, deviceId);
            }
        }

        /// <summary>
        /// UPDATED: Activates license with lifetime support
        /// </summary>
        public async Task<(bool Success, string Message)> ActivateLicenseAsync(
            string licenseKey, string deviceId, string email)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ActivateLicenseAsync called");

                if (string.IsNullOrWhiteSpace(licenseKey) || string.IsNullOrWhiteSpace(email))
                {
                    return (false, "Informations d'activation incomplètes");
                }

                if (_firestore == null)
                {
                    return (false, "Impossible de se connecter au serveur de licences");
                }

                if (!await CheckRateLimit(deviceId))
                {
                    return (false, "Trop de tentatives d'activation");
                }

                var sanitizedKey = SanitizeKey(licenseKey);
                var docRef = _firestore.Collection("licenses").Document(sanitizedKey);
                var snapshot = await docRef.GetSnapshotAsync();

                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] Document exists: {snapshot.Exists}");

                if (!snapshot.Exists)
                {
                    await LogActivationAttempt(deviceId, licenseKey, false);
                    return (false, "Clé de licence invalide");
                }

                var license = snapshot.ConvertTo<LicenseData>();

                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] License status:");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    IsActive: {license.IsActive}");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    IsLifetime: {license.IsLifetime}");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    IsExpired: {license.IsExpired}");

                // Check if already activated on a different device
                if (!string.IsNullOrEmpty(license.DeviceId) && license.DeviceId != deviceId)
                {
                    return (false, $"Licence déjà activée sur un autre appareil");
                }

                // UPDATED: Check expiration (handles lifetime)
                if (license.IsExpired)
                {
                    return (false, "Cette licence a expiré");
                }

                // Check if active
                if (!license.IsActive)
                {
                    return (false, "Cette licence a été désactivée");
                }

                // Activate license
                var updates = new Dictionary<string, object>
                {
                    { "deviceId", deviceId },
                    { "email", email },
                    { "activationDate", Timestamp.FromDateTime(DateTime.UtcNow) },
                    { "lastValidation", Timestamp.FromDateTime(DateTime.UtcNow) }
                };

                await docRef.UpdateAsync(updates);

                System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] ✅ License activated");

                await LogActivationAttempt(deviceId, licenseKey, true);

                // UPDATED: Better message
                var message = license.IsLifetime || !license.ExpirationDate.HasValue
                    ? "Licence à vie activée avec succès!"
                    : "Licence activée avec succès!";

                return (true, message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ❌ Activation error: {ex.Message}");
                return (false, $"Erreur d'activation: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeactivateLicenseAsync(string licenseKey, string deviceId)
        {
            try
            {
                if (_firestore == null)
                {
                    return (false, "Connexion au serveur impossible");
                }

                var sanitizedKey = SanitizeKey(licenseKey);
                var docRef = _firestore.Collection("licenses").Document(sanitizedKey);
                var snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return (false, "Licence introuvable");
                }

                var license = snapshot.ConvertTo<LicenseData>();

                if (license.DeviceId != deviceId)
                {
                    return (false, "Cette licence n'est pas activée sur cet appareil");
                }

                var updates = new Dictionary<string, object>
                {
                    { "deviceId", FieldValue.Delete },
                    { "isActive", false }
                };

                await docRef.UpdateAsync(updates);

                return (true, "Licence désactivée avec succès");
            }
            catch (Exception ex)
            {
                return (false, $"Erreur: {ex.Message}");
            }
        }

        public async Task<LicenseData?> GetLicenseInfoAsync(string licenseKey)
        {
            try
            {
                if (_firestore == null) return null;

                var docRef = _firestore.Collection("licenses").Document(SanitizeKey(licenseKey));
                var snapshot = await docRef.GetSnapshotAsync();

                return snapshot.Exists ? snapshot.ConvertTo<LicenseData>() : null;
            }
            catch
            {
                return null;
            }
        }

        #region Private Helper Methods

        private (bool IsValid, string Message) ValidateOffline(string licenseKey, string deviceId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] Offline validation");

                var cacheFile = GetCacheFilePath();
                if (!System.IO.File.Exists(cacheFile))
                {
                    return (false, "Connexion Internet requise pour la première validation");
                }

                var cacheJson = System.IO.File.ReadAllText(cacheFile);
                var cache = System.Text.Json.JsonSerializer.Deserialize<LicenseCache>(cacheJson);

                if (cache == null || cache.LicenseKey != licenseKey || cache.DeviceId != deviceId)
                {
                    return (false, "Cache de licence invalide");
                }

                var daysSinceLastValidation = (DateTime.UtcNow - cache.LastValidation).Days;
                if (daysSinceLastValidation > GRACE_PERIOD_DAYS)
                {
                    return (false, $"Connexion requise (hors ligne depuis {daysSinceLastValidation} jours)");
                }

                // UPDATED: Check if lifetime
                if (cache.IsLifetime)
                {
                    return (true, "Licence à vie - Mode hors ligne");
                }

                return (true, $"Mode hors ligne - {GRACE_PERIOD_DAYS - daysSinceLastValidation} jours restants");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] Offline validation error: {ex.Message}");
                return (false, "Erreur de validation hors ligne");
            }
        }

        private async Task UpdateLastValidation(string licenseKey)
        {
            try
            {
                if (_firestore == null) return;

                var docRef = _firestore.Collection("licenses").Document(SanitizeKey(licenseKey));
                var updates = new Dictionary<string, object>
                {
                    { "lastValidation", Timestamp.FromDateTime(DateTime.UtcNow) }
                };

                await docRef.UpdateAsync(updates);
                UpdateLocalCache(licenseKey);
            }
            catch
            {
                // Ignore
            }
        }

        private async Task<bool> CheckRateLimit(string deviceId)
        {
            try
            {
                if (_firestore == null) return true;

                var docRef = _firestore.Collection("activation_attempts").Document(deviceId);
                var snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists) return true;

                var attempts = snapshot.ConvertTo<ActivationAttempts>();

                if ((DateTime.UtcNow - attempts.LastAttempt).TotalHours > 1)
                {
                    return true;
                }

                return attempts.Count < MAX_ACTIVATION_ATTEMPTS;
            }
            catch
            {
                return true;
            }
        }

        private async Task LogActivationAttempt(string deviceId, string licenseKey, bool success)
        {
            try
            {
                if (_firestore == null) return;

                var docRef = _firestore.Collection("activation_attempts").Document(deviceId);
                var snapshot = await docRef.GetSnapshotAsync();

                int count;
                if (snapshot.Exists)
                {
                    var attempts = snapshot.ConvertTo<ActivationAttempts>();
                    count = success ? 0 : attempts.Count + 1;
                }
                else
                {
                    count = success ? 0 : 1;
                }

                var data = new Dictionary<string, object>
                {
                    { "count", count },
                    { "lastAttempt", Timestamp.FromDateTime(DateTime.UtcNow) },
                    { "lastLicenseKey", licenseKey }
                };

                await docRef.SetAsync(data, SetOptions.MergeAll);
            }
            catch
            {
                // Ignore
            }
        }

        private string SanitizeKey(string key)
        {
            return key.Replace(".", "_")
                     .Replace("$", "_")
                     .Replace("#", "_")
                     .Replace("[", "_")
                     .Replace("]", "_")
                     .Replace("/", "_")
                     .Replace("-", "_");
        }

        private string GetCacheFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = System.IO.Path.Combine(appData, "MonBureau");
            System.IO.Directory.CreateDirectory(folder);
            return System.IO.Path.Combine(folder, "license_cache.dat");
        }

        private void UpdateLocalCache(string licenseKey)
        {
            try
            {
                var deviceIdentifier = new DeviceIdentifier();
                var cache = new LicenseCache
                {
                    LicenseKey = licenseKey,
                    DeviceId = deviceIdentifier.GenerateDeviceId(),
                    LastValidation = DateTime.UtcNow,
                    IsLifetime = true // Assume lifetime for cache
                };

                var json = System.Text.Json.JsonSerializer.Serialize(cache);
                System.IO.File.WriteAllText(GetCacheFilePath(), json);
            }
            catch
            {
                // Ignore
            }
        }

        #endregion

        #region Helper Classes

        private class LicenseCache
        {
            public string LicenseKey { get; set; } = string.Empty;
            public string DeviceId { get; set; } = string.Empty;
            public DateTime LastValidation { get; set; }
            public bool IsLifetime { get; set; } // NEW
        }

        private class ActivationAttempts
        {
            [FirestoreProperty]
            public int Count { get; set; }

            [FirestoreProperty]
            public DateTime LastAttempt { get; set; }

            [FirestoreProperty]
            public string LastLicenseKey { get; set; } = string.Empty;
        }

        #endregion
    }
}