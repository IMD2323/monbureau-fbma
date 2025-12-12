using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MonBureau.Infrastructure.Services
{
    /// <summary>
    /// Secure license storage with tampering detection
    /// </summary>
    public class SecureLicenseStorage
    {
        private readonly string _storagePath;
        private readonly DeviceIdentifier _deviceIdentifier;

        public SecureLicenseStorage()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "MonBureau");
            Directory.CreateDirectory(folder);
            _storagePath = Path.Combine(folder, "license.dat");
            _deviceIdentifier = new DeviceIdentifier();
        }

        /// <summary>
        /// Saves license with encryption and integrity check
        /// </summary>
        public bool SaveLicense(string licenseKey, string deviceId)
        {
            try
            {
                var data = new LicenseStorageData
                {
                    LicenseKey = licenseKey,
                    DeviceId = deviceId,
                    LastValidation = DateTime.UtcNow,
                    StoredAt = DateTime.UtcNow
                };

                // Serialize to JSON
                var json = JsonSerializer.Serialize(data);

                // Generate integrity hash
                var hash = ComputeHash(json, deviceId);
                var packagedData = new PackagedLicense
                {
                    Data = json,
                    Hash = hash,
                    Version = 1
                };

                var packagedJson = JsonSerializer.Serialize(packagedData);

                // Encrypt with DPAPI
                var encrypted = ProtectData(packagedJson, GetEntropy(deviceId));

                // Save to file
                File.WriteAllBytes(_storagePath, encrypted);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving license: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads license with integrity verification
        /// </summary>
        public (bool Success, string? LicenseKey, string? DeviceId, DateTime? LastValidation) LoadLicense()
        {
            try
            {
                if (!File.Exists(_storagePath))
                {
                    return (false, null, null, null);
                }

                var deviceId = _deviceIdentifier.GenerateDeviceId();

                // Read and decrypt
                var encrypted = File.ReadAllBytes(_storagePath);
                var decrypted = UnprotectData(encrypted, GetEntropy(deviceId));

                if (string.IsNullOrEmpty(decrypted))
                {
                    return (false, null, null, null);
                }

                // Deserialize package
                var package = JsonSerializer.Deserialize<PackagedLicense>(decrypted);
                if (package == null)
                {
                    return (false, null, null, null);
                }

                // Verify integrity
                var expectedHash = ComputeHash(package.Data, deviceId);
                if (package.Hash != expectedHash)
                {
                    System.Diagnostics.Debug.WriteLine("License tampering detected!");
                    DeleteLicense(); // Remove tampered license
                    return (false, null, null, null);
                }

                // Deserialize data
                var data = JsonSerializer.Deserialize<LicenseStorageData>(package.Data);
                if (data == null)
                {
                    return (false, null, null, null);
                }

                // Verify device ID matches
                if (data.DeviceId != deviceId)
                {
                    System.Diagnostics.Debug.WriteLine("Device ID mismatch!");
                    return (false, null, null, null);
                }

                return (true, data.LicenseKey, data.DeviceId, data.LastValidation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading license: {ex.Message}");
                return (false, null, null, null);
            }
        }

        /// <summary>
        /// Updates last validation timestamp
        /// </summary>
        public bool UpdateLastValidation()
        {
            try
            {
                var (success, licenseKey, deviceId, _) = LoadLicense();
                if (!success || licenseKey == null || deviceId == null)
                {
                    return false;
                }

                return SaveLicense(licenseKey, deviceId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes stored license
        /// </summary>
        public bool DeleteLicense()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    File.Delete(_storagePath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if license file exists
        /// </summary>
        public bool LicenseExists()
        {
            return File.Exists(_storagePath);
        }

        #region Private Methods

        /// <summary>
        /// Encrypts data using DPAPI
        /// </summary>
        private byte[] ProtectData(string plainText, byte[] entropy)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            return ProtectedData.Protect(plainBytes, entropy, DataProtectionScope.CurrentUser);
        }

        /// <summary>
        /// Decrypts data using DPAPI
        /// </summary>
        private string UnprotectData(byte[] encrypted, byte[] entropy)
        {
            try
            {
                var decrypted = ProtectedData.Unprotect(encrypted, entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Generates entropy based on device characteristics
        /// </summary>
        private byte[] GetEntropy(string deviceId)
        {
            var combined = $"{deviceId}_{Environment.MachineName}_{Environment.UserName}";
            return SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        }

        /// <summary>
        /// Computes integrity hash
        /// </summary>
        private string ComputeHash(string data, string deviceId)
        {
            var combined = $"{data}|{deviceId}|{Environment.MachineName}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
            return Convert.ToBase64String(hash);
        }

        #endregion

        #region Data Classes

        private class LicenseStorageData
        {
            public string LicenseKey { get; set; } = string.Empty;
            public string DeviceId { get; set; } = string.Empty;
            public DateTime LastValidation { get; set; }
            public DateTime StoredAt { get; set; }
        }

        private class PackagedLicense
        {
            public string Data { get; set; } = string.Empty;
            public string Hash { get; set; } = string.Empty;
            public int Version { get; set; }
        }

        #endregion
    }
}