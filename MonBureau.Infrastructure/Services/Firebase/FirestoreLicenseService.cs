using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MonBureau.Infrastructure.Services.Firebase
{
    /// <summary>
    /// Firestore license service using Firebase Web SDK REST API
    /// More secure and appropriate for desktop applications than Admin SDK
    /// </summary>
    public class FirestoreLicenseService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly FirebaseWebConfig? _config;
        private const int MAX_ACTIVATION_ATTEMPTS = 5;
        private const int GRACE_PERIOD_DAYS = 7;
        private bool _disposed;

        public FirestoreLicenseService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            if (!FirebaseConfig.IsInitialized)
            {
                System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] ⚠️ Firebase NOT initialized");
                _config = null;
                return;
            }

            _config = FirebaseConfig.Config;
            System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ✅ Initialized with project: {_config?.ProjectId}");
        }

        public bool IsOnline => _config != null;

        /// <summary>
        /// Validates license with lifetime support
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

                if (_config == null)
                {
                    System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] ⚠️ Offline mode");
                    return ValidateOffline(licenseKey, deviceId);
                }

                // Get license document from Firestore
                var license = await GetLicenseAsync(licenseKey);

                if (license == null)
                {
                    System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] ❌ License NOT FOUND");
                    return (false, "Clé de licence non reconnue");
                }

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

                // Check expiration (handles lifetime licenses)
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
                await UpdateLastValidationAsync(licenseKey);

                // Better message for lifetime licenses
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
        /// Activates license with lifetime support
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

                if (_config == null)
                {
                    return (false, "Impossible de se connecter au serveur de licences");
                }

                if (!await CheckRateLimitAsync(deviceId))
                {
                    return (false, "Trop de tentatives d'activation");
                }

                var license = await GetLicenseAsync(licenseKey);

                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] Document exists: {license != null}");

                if (license == null)
                {
                    await LogActivationAttemptAsync(deviceId, licenseKey, false);
                    return (false, "Clé de licence invalide");
                }

                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] License status:");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    IsActive: {license.IsActive}");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    IsLifetime: {license.IsLifetime}");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService]    IsExpired: {license.IsExpired}");

                // Check if already activated on a different device
                if (!string.IsNullOrEmpty(license.DeviceId) && license.DeviceId != deviceId)
                {
                    return (false, $"Licence déjà activée sur un autre appareil");
                }

                // Check expiration (handles lifetime)
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
                license.DeviceId = deviceId;
                license.Email = email;
                license.ActivationDate = DateTime.UtcNow;
                license.LastValidation = DateTime.UtcNow;

                await UpdateLicenseAsync(licenseKey, license);

                System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] ✅ License activated");

                await LogActivationAttemptAsync(deviceId, licenseKey, true);

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
                if (_config == null)
                {
                    return (false, "Connexion au serveur impossible");
                }

                var license = await GetLicenseAsync(licenseKey);

                if (license == null)
                {
                    return (false, "Licence introuvable");
                }

                if (license.DeviceId != deviceId)
                {
                    return (false, "Cette licence n'est pas activée sur cet appareil");
                }

                license.DeviceId = null;
                license.IsActive = false;

                await UpdateLicenseAsync(licenseKey, license);

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
                if (_config == null) return null;
                return await GetLicenseAsync(licenseKey);
            }
            catch
            {
                return null;
            }
        }

        #region Private Helper Methods

        private async Task<LicenseData?> GetLicenseAsync(string licenseKey)
        {
            try
            {
                var sanitizedKey = SanitizeKey(licenseKey);
                var url = $"{_config!.FirestoreEndpoint}/licenses/{sanitizedKey}?key={_config.ApiKey}";

                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] Fetching license from: {url.Replace(_config.ApiKey, "***")}");

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] License document not found");
                        return null;
                    }

                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] Error getting license: {response.StatusCode}");
                    System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] Error details: {error}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] Raw response: {json.Substring(0, Math.Min(200, json.Length))}...");

                var doc = JsonSerializer.Deserialize<FirestoreDocument>(json);

                if (doc?.Fields == null)
                {
                    System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] No fields in document");
                    return null;
                }

                var license = ConvertFromFirestore(doc.Fields);
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ✅ License converted successfully");
                return license;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ❌ Exception in GetLicenseAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] StackTrace: {ex.StackTrace}");
                return null;
            }
        }

        private async Task UpdateLicenseAsync(string licenseKey, LicenseData license)
        {
            var sanitizedKey = SanitizeKey(licenseKey);
            var url = $"{_config!.FirestoreEndpoint}/licenses/{sanitizedKey}?key={_config.ApiKey}";

            var firestoreDoc = ConvertToFirestore(license);
            var json = JsonSerializer.Serialize(firestoreDoc);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PatchAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to update license: {error}");
            }
        }

        private async Task UpdateLastValidationAsync(string licenseKey)
        {
            try
            {
                if (_config == null) return;

                var sanitizedKey = SanitizeKey(licenseKey);
                var url = $"{_config.FirestoreEndpoint}/licenses/{sanitizedKey}?key={_config.ApiKey}&updateMask.fieldPaths=lastValidation";

                var update = new
                {
                    fields = new Dictionary<string, object>
                    {
                        ["lastValidation"] = new { timestampValue = DateTime.UtcNow.ToString("o") }
                    }
                };

                var json = JsonSerializer.Serialize(update);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    UpdateLocalCache(licenseKey);
                    System.Diagnostics.Debug.WriteLine("[FirestoreLicenseService] ✅ Last validation updated");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ⚠️ Failed to update validation: {error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ⚠️ Error updating validation: {ex.Message}");
                // Ignore - not critical
            }
        }

        private async Task<bool> CheckRateLimitAsync(string deviceId)
        {
            try
            {
                if (_config == null) return true;

                var sanitizedId = SanitizeKey(deviceId);
                var url = $"{_config.FirestoreEndpoint}/activation_attempts/{sanitizedId}?key={_config.ApiKey}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return true;

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonSerializer.Deserialize<FirestoreDocument>(json);

                if (doc?.Fields == null)
                    return true;

                var countField = doc.Fields.GetValueOrDefault("count");
                var lastAttemptField = doc.Fields.GetValueOrDefault("lastAttempt");

                if (countField == null || lastAttemptField == null)
                    return true;

                var count = (countField as JsonElement?)?.GetProperty("integerValue").GetInt32() ?? 0;
                var lastAttemptStr = (lastAttemptField as JsonElement?)?.GetProperty("timestampValue").GetString();

                if (DateTime.TryParse(lastAttemptStr, out var lastAttempt))
                {
                    if ((DateTime.UtcNow - lastAttempt).TotalHours > 1)
                        return true;

                    return count < MAX_ACTIVATION_ATTEMPTS;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        private async Task LogActivationAttemptAsync(string deviceId, string licenseKey, bool success)
        {
            try
            {
                if (_config == null) return;

                var sanitizedId = SanitizeKey(deviceId);
                var url = $"{_config.FirestoreEndpoint}/activation_attempts/{sanitizedId}?key={_config.ApiKey}";

                var currentCount = 0;
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = JsonSerializer.Deserialize<FirestoreDocument>(json);
                    if (doc?.Fields != null)
                    {
                        currentCount = GetIntValue(doc.Fields, "count");
                    }
                }

                var newCount = success ? 0 : currentCount + 1;

                var update = new
                {
                    fields = new Dictionary<string, object>
                    {
                        ["count"] = new { integerValue = newCount.ToString() }, // Firestore REST API expects string
                        ["lastAttempt"] = new { timestampValue = DateTime.UtcNow.ToString("o") },
                        ["lastLicenseKey"] = new { stringValue = licenseKey }
                    }
                };

                var updateJson = JsonSerializer.Serialize(update);
                var content = new StringContent(updateJson, Encoding.UTF8, "application/json");

                await _httpClient.PatchAsync(url, content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirestoreLicenseService] ⚠️ Error logging attempt: {ex.Message}");
                // Ignore
            }
        }

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
                var cache = JsonSerializer.Deserialize<LicenseCache>(cacheJson);

                if (cache == null || cache.LicenseKey != licenseKey || cache.DeviceId != deviceId)
                {
                    return (false, "Cache de licence invalide");
                }

                var daysSinceLastValidation = (DateTime.UtcNow - cache.LastValidation).Days;
                if (daysSinceLastValidation > GRACE_PERIOD_DAYS)
                {
                    return (false, $"Connexion requise (hors ligne depuis {daysSinceLastValidation} jours)");
                }

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
                    IsLifetime = true
                };

                var json = JsonSerializer.Serialize(cache);
                System.IO.File.WriteAllText(GetCacheFilePath(), json);
            }
            catch
            {
                // Ignore
            }
        }

        #endregion

        #region Firestore Conversion

        private LicenseData ConvertFromFirestore(Dictionary<string, object> fields)
        {
            return new LicenseData
            {
                LicenseKey = GetStringValue(fields, "licenseKey"),
                Email = GetStringValue(fields, "email"),
                DeviceId = GetStringValue(fields, "deviceId"),
                ActivationDate = GetDateTimeValue(fields, "activationDate"),
                ExpirationDate = GetDateTimeValue(fields, "expirationDate"),
                IsActive = GetBoolValue(fields, "isActive"),
                Type = (LicenseType)GetIntValue(fields, "type"),
                TrialStartDate = GetDateTimeValue(fields, "trialStartDate"),
                TrialEndDate = GetDateTimeValue(fields, "trialEndDate"),
                CreatedAt = GetDateTimeValue(fields, "createdAt") ?? DateTime.UtcNow,
                LastValidation = GetDateTimeValue(fields, "lastValidation"),
                IsLifetime = GetBoolValue(fields, "isLifetime")
            };
        }

        private FirestoreDocument ConvertToFirestore(LicenseData license)
        {
            var fields = new Dictionary<string, object>
            {
                ["licenseKey"] = new { stringValue = license.LicenseKey },
                ["email"] = new { stringValue = license.Email },
                ["isActive"] = new { booleanValue = license.IsActive },
                ["type"] = new { integerValue = ((int)license.Type).ToString() }, // Firestore REST API expects string
                ["createdAt"] = new { timestampValue = license.CreatedAt.ToUniversalTime().ToString("o") },
                ["isLifetime"] = new { booleanValue = license.IsLifetime }
            };

            if (!string.IsNullOrEmpty(license.DeviceId))
                fields["deviceId"] = new { stringValue = license.DeviceId };

            if (license.ActivationDate.HasValue)
                fields["activationDate"] = new { timestampValue = license.ActivationDate.Value.ToUniversalTime().ToString("o") };

            if (license.ExpirationDate.HasValue)
                fields["expirationDate"] = new { timestampValue = license.ExpirationDate.Value.ToUniversalTime().ToString("o") };

            if (license.TrialStartDate.HasValue)
                fields["trialStartDate"] = new { timestampValue = license.TrialStartDate.Value.ToUniversalTime().ToString("o") };

            if (license.TrialEndDate.HasValue)
                fields["trialEndDate"] = new { timestampValue = license.TrialEndDate.Value.ToUniversalTime().ToString("o") };

            if (license.LastValidation.HasValue)
                fields["lastValidation"] = new { timestampValue = license.LastValidation.Value.ToUniversalTime().ToString("o") };

            return new FirestoreDocument { Fields = fields };
        }

        private string GetStringValue(Dictionary<string, object> fields, string key)
        {
            if (!fields.TryGetValue(key, out var value))
                return string.Empty;

            if (value is JsonElement element)
            {
                // Try stringValue first
                if (element.TryGetProperty("stringValue", out var stringValue))
                    return stringValue.GetString() ?? string.Empty;

                // Fallback: try to get raw string value
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private bool GetBoolValue(Dictionary<string, object> fields, string key)
        {
            if (!fields.TryGetValue(key, out var value))
                return false;

            if (value is JsonElement element)
            {
                // Try booleanValue first
                if (element.TryGetProperty("booleanValue", out var boolValue))
                    return boolValue.GetBoolean();

                // Fallback: try to get raw boolean value
                if (element.ValueKind == JsonValueKind.True)
                    return true;
                if (element.ValueKind == JsonValueKind.False)
                    return false;

                // Try parsing string as boolean
                if (element.ValueKind == JsonValueKind.String)
                {
                    var str = element.GetString();
                    if (bool.TryParse(str, out var result))
                        return result;
                }
            }

            return false;
        }

        private int GetIntValue(Dictionary<string, object> fields, string key)
        {
            if (!fields.TryGetValue(key, out var value))
                return 0;

            if (value is JsonElement element)
            {
                // Try integerValue first (as string in Firestore REST API)
                if (element.TryGetProperty("integerValue", out var intValue))
                {
                    // Firestore REST API returns integers as strings
                    if (intValue.ValueKind == JsonValueKind.String)
                    {
                        var str = intValue.GetString();
                        if (int.TryParse(str, out var result))
                            return result;
                    }
                    // Try as number
                    else if (intValue.ValueKind == JsonValueKind.Number)
                    {
                        return intValue.GetInt32();
                    }
                }

                // Fallback: try to get raw number value
                if (element.ValueKind == JsonValueKind.Number)
                    return element.GetInt32();

                // Try parsing string as int
                if (element.ValueKind == JsonValueKind.String)
                {
                    var str = element.GetString();
                    if (int.TryParse(str, out var result))
                        return result;
                }
            }

            return 0;
        }

        private DateTime? GetDateTimeValue(Dictionary<string, object> fields, string key)
        {
            if (!fields.TryGetValue(key, out var value))
                return null;

            if (value is JsonElement element)
            {
                // Try timestampValue first
                if (element.TryGetProperty("timestampValue", out var timestamp))
                {
                    var dateStr = timestamp.GetString();
                    if (DateTime.TryParse(dateStr, out var date))
                        return date.ToUniversalTime();
                }

                // Fallback: try to get raw string value
                if (element.ValueKind == JsonValueKind.String)
                {
                    var dateStr = element.GetString();
                    if (DateTime.TryParse(dateStr, out var date))
                        return date.ToUniversalTime();
                }
            }

            return null;
        }

        #endregion

        #region Helper Classes

        private class FirestoreDocument
        {
            [JsonPropertyName("fields")]
            public Dictionary<string, object>? Fields { get; set; }
        }

        private class LicenseCache
        {
            public string LicenseKey { get; set; } = string.Empty;
            public string DeviceId { get; set; } = string.Empty;
            public DateTime LastValidation { get; set; }
            public bool IsLifetime { get; set; }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;

            _httpClient?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}