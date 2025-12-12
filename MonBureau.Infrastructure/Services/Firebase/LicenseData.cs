using System;
using Google.Cloud.Firestore;

namespace MonBureau.Infrastructure.Services.Firebase
{
    /// <summary>
    /// License data model for Firestore storage
    /// FIXED: Proper camelCase to PascalCase mapping
    /// </summary>
    [FirestoreData]
    public class LicenseData
    {
        [FirestoreProperty("licenseKey")]
        public string LicenseKey { get; set; } = string.Empty;

        [FirestoreProperty("email")]
        public string Email { get; set; } = string.Empty;

        [FirestoreProperty("deviceId")]
        public string? DeviceId { get; set; }

        [FirestoreProperty("activationDate")]
        public DateTime? ActivationDate { get; set; }

        [FirestoreProperty("expirationDate")]
        public DateTime? ExpirationDate { get; set; }

        [FirestoreProperty("isActive")]
        public bool IsActive { get; set; }

        [FirestoreProperty("type")]
        public LicenseType Type { get; set; } = LicenseType.Full;

        [FirestoreProperty("trialStartDate")]
        public DateTime? TrialStartDate { get; set; }

        [FirestoreProperty("trialEndDate")]
        public DateTime? TrialEndDate { get; set; }

        [FirestoreProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty("lastValidation")]
        public DateTime? LastValidation { get; set; }

        [FirestoreProperty("isLifetime")]
        public bool IsLifetime { get; set; }

        // Computed properties (not stored in Firestore)

        /// <summary>
        /// FIXED: Lifetime licenses never expire
        /// </summary>
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
                    return false; // Don't block on errors
                }
            }
        }

        public bool IsTrialExpired => Type == LicenseType.Trial &&
                                      TrialEndDate.HasValue &&
                                      DateTime.UtcNow > TrialEndDate.Value;

        public bool CanActivate => string.IsNullOrEmpty(DeviceId);

        /// <summary>
        /// FIXED: Lifetime licenses show "∞" for days remaining
        /// </summary>
        public int DaysRemaining
        {
            get
            {
                if (IsLifetime || !ExpirationDate.HasValue)
                    return int.MaxValue; // Infinite

                var remaining = (ExpirationDate.Value - DateTime.UtcNow).Days;
                return remaining > 0 ? remaining : 0;
            }
        }

        /// <summary>
        /// User-friendly expiration message
        /// </summary>
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