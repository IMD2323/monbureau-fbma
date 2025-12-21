using System;
using System.Text.Json.Serialization;

namespace MonBureau.Infrastructure.Services.Firebase
{
    /// <summary>
    /// License data model for Firestore REST API
    /// Uses System.Text.Json instead of Firestore attributes
    /// </summary>
    public class LicenseData
    {
        [JsonPropertyName("licenseKey")]
        public string LicenseKey { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("deviceId")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("activationDate")]
        public DateTime? ActivationDate { get; set; }

        [JsonPropertyName("expirationDate")]
        public DateTime? ExpirationDate { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("type")]
        public LicenseType Type { get; set; } = LicenseType.Full;

        [JsonPropertyName("trialStartDate")]
        public DateTime? TrialStartDate { get; set; }

        [JsonPropertyName("trialEndDate")]
        public DateTime? TrialEndDate { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("lastValidation")]
        public DateTime? LastValidation { get; set; }

        [JsonPropertyName("isLifetime")]
        public bool IsLifetime { get; set; }

        // Computed properties (not stored in Firestore)

        /// <summary>
        /// Lifetime licenses never expire
        /// </summary>
        [JsonIgnore]
        public bool IsExpired
        {
            get
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[LicenseData] IsExpired check:");
                    System.Diagnostics.Debug.WriteLine($"[LicenseData]    IsLifetime: {IsLifetime}");
                    System.Diagnostics.Debug.WriteLine($"[LicenseData]    ExpirationDate: {ExpirationDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "NULL"}");

                    // Lifetime licenses never expire
                    if (IsLifetime)
                    {
                        System.Diagnostics.Debug.WriteLine("[LicenseData] ✅ Lifetime license - never expires");
                        return false;
                    }

                    // No expiration date set = lifetime license
                    if (!ExpirationDate.HasValue)
                    {
                        System.Diagnostics.Debug.WriteLine("[LicenseData] ✅ No expiration date - treating as lifetime license");
                        return false;
                    }

                    // Check if expired
                    var isExpired = DateTime.UtcNow > ExpirationDate.Value;

                    System.Diagnostics.Debug.WriteLine($"[LicenseData]    Now (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                    System.Diagnostics.Debug.WriteLine($"[LicenseData]    Expires:   {ExpirationDate.Value:yyyy-MM-dd HH:mm:ss}");
                    System.Diagnostics.Debug.WriteLine($"[LicenseData]    IsExpired: {isExpired}");

                    return isExpired;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LicenseData] ⚠️ Error checking expiration: {ex.Message}");
                    return false;
                }
            }
        }

        [JsonIgnore]
        public bool IsTrialExpired => Type == LicenseType.Trial &&
                                      TrialEndDate.HasValue &&
                                      DateTime.UtcNow > TrialEndDate.Value;

        [JsonIgnore]
        public bool CanActivate => string.IsNullOrEmpty(DeviceId);

        /// <summary>
        /// Lifetime licenses show infinite days remaining
        /// </summary>
        [JsonIgnore]
        public int DaysRemaining
        {
            get
            {
                if (IsLifetime || !ExpirationDate.HasValue)
                    return int.MaxValue;

                var remaining = (ExpirationDate.Value - DateTime.UtcNow).Days;
                return remaining > 0 ? remaining : 0;
            }
        }

        /// <summary>
        /// User-friendly expiration message
        /// </summary>
        [JsonIgnore]
        public string ExpirationMessage
        {
            get
            {
                if (IsLifetime || !ExpirationDate.HasValue)
                    return "Licence à vie";

                if (IsExpired)
                    return $"Expirée le {ExpirationDate.Value:dd/MM/yyyy}";

                var days = DaysRemaining;
                if (days > 365)
                    return $"Expire le {ExpirationDate.Value:dd/MM/yyyy}";

                if (days > 30)
                    return $"Expire dans {days} jours";

                if (days > 0)
                    return $"⚠️ Expire dans {days} jours";

                return "⚠️ Expire aujourd'hui";
            }
        }
    }

    public enum LicenseType
    {
        Trial = 0,
        Full = 1,
        Professional = 2,
        Enterprise = 3,
        Lifetime = 4
    }
}