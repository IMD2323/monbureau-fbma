using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MonBureau.Infrastructure.Services;

namespace MonBureau.Infrastructure.Services
{
    /// <summary>
    /// Automatic backup service with configurable intervals
    /// </summary>
    public class AutoBackupService : IDisposable
    {
        private readonly BackupService _backupService;
        private Timer? _timer;
        private readonly string _settingsPath;
        private BackupSettings _settings;
        private bool _disposed;

        public AutoBackupService(BackupService backupService)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "MonBureau");
            Directory.CreateDirectory(folder);
            _settingsPath = Path.Combine(folder, "backup_settings.json");

            _settings = LoadSettings();

            if (_settings.Enabled)
            {
                StartAutoBackup();
            }
        }

        /// <summary>
        /// Starts automatic backup timer
        /// </summary>
        public void StartAutoBackup()
        {
            StopAutoBackup();

            if (!_settings.Enabled)
            {
                return;
            }

            var interval = GetIntervalTimeSpan();

            // Schedule first backup
            var timeUntilFirstBackup = CalculateTimeUntilNextBackup();

            _timer = new Timer(
                OnTimerElapsed,
                null,
                timeUntilFirstBackup,
                interval
            );

            System.Diagnostics.Debug.WriteLine($"Auto-backup started. Next backup in {timeUntilFirstBackup.TotalMinutes:F1} minutes");
        }

        /// <summary>
        /// Stops automatic backup timer
        /// </summary>
        public void StopAutoBackup()
        {
            _timer?.Dispose();
            _timer = null;
        }

        /// <summary>
        /// Updates backup settings
        /// </summary>
        public void UpdateSettings(BackupSettings settings)
        {
            _settings = settings;
            SaveSettings();

            if (_settings.Enabled)
            {
                StartAutoBackup();
            }
            else
            {
                StopAutoBackup();
            }
        }

        /// <summary>
        /// Gets current backup settings
        /// </summary>
        public BackupSettings GetSettings()
        {
            return _settings;
        }

        /// <summary>
        /// Gets last backup information
        /// </summary>
        public async Task<BackupInfo?> GetLastBackupInfoAsync()
        {
            try
            {
                var backups = await _backupService.GetBackupHistoryAsync();

                if (backups.Length == 0)
                {
                    return null;
                }

                var latestBackup = backups[0];
                var fileInfo = new FileInfo(latestBackup);

                var metadata = await _backupService.GetBackupMetadataAsync(latestBackup);

                return new BackupInfo
                {
                    FilePath = latestBackup,
                    CreatedAt = fileInfo.CreationTime,
                    Size = fileInfo.Length,
                    ClientCount = metadata?.ClientCount ?? 0,
                    CaseCount = metadata?.CaseCount ?? 0,
                    ItemCount = metadata?.ItemCount ?? 0
                };
            }
            catch
            {
                return null;
            }
        }

        #region Private Methods

        private async void OnTimerElapsed(object? state)
        {
            try
            {
                if (!ShouldBackup())
                {
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Auto-backup triggered");

                var (success, message, filePath) = await _backupService.CreateBackupAsync();

                if (success)
                {
                    _settings.LastBackupDate = DateTime.UtcNow;
                    SaveSettings();

                    // Clean up old backups if max count exceeded
                    await CleanupOldBackupsAsync();

                    System.Diagnostics.Debug.WriteLine($"Auto-backup completed: {message}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-backup failed: {message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-backup error: {ex.Message}");
            }
        }

        private bool ShouldBackup()
        {
            if (!_settings.Enabled)
            {
                return false;
            }

            // Check if enough time has passed since last backup
            var timeSinceLastBackup = DateTime.UtcNow - _settings.LastBackupDate;
            var interval = GetIntervalTimeSpan();

            return timeSinceLastBackup >= interval;
        }

        private TimeSpan GetIntervalTimeSpan()
        {
            return _settings.IntervalType switch
            {
                BackupInterval.Daily => TimeSpan.FromDays(1),
                BackupInterval.Weekly => TimeSpan.FromDays(7),
                BackupInterval.EveryThreeDays => TimeSpan.FromDays(3),
                BackupInterval.Hourly => TimeSpan.FromHours(1), // For testing
                _ => TimeSpan.FromDays(1)
            };
        }

        private TimeSpan CalculateTimeUntilNextBackup()
        {
            var interval = GetIntervalTimeSpan();
            var timeSinceLastBackup = DateTime.UtcNow - _settings.LastBackupDate;

            if (timeSinceLastBackup >= interval)
            {
                // Overdue - backup immediately
                return TimeSpan.FromSeconds(30);
            }

            return interval - timeSinceLastBackup;
        }

        private async Task CleanupOldBackupsAsync()
        {
            try
            {
                if (_settings.MaxBackupCount <= 0)
                {
                    return; // No limit
                }

                var backups = await _backupService.GetBackupHistoryAsync();

                if (backups.Length <= _settings.MaxBackupCount)
                {
                    return; // Under limit
                }

                // Sort by creation time (oldest first)
                Array.Sort(backups, (a, b) =>
                    File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));

                // Delete oldest backups
                var toDelete = backups.Length - _settings.MaxBackupCount;
                for (int i = 0; i < toDelete; i++)
                {
                    try
                    {
                        File.Delete(backups[i]);
                        System.Diagnostics.Debug.WriteLine($"Deleted old backup: {Path.GetFileName(backups[i])}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete backup: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

        private BackupSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<BackupSettings>(json);
                    return settings ?? CreateDefaultSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading backup settings: {ex.Message}");
            }

            return CreateDefaultSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving backup settings: {ex.Message}");
            }
        }

        private BackupSettings CreateDefaultSettings()
        {
            return new BackupSettings
            {
                Enabled = true,
                IntervalType = BackupInterval.Daily,
                LastBackupDate = DateTime.UtcNow.AddDays(-1), // Trigger backup soon
                MaxBackupCount = 30, // Keep last 30 backups
                BackupPath = _backupService.DefaultBackupPath
            };
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;

            StopAutoBackup();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    #region Data Classes

    public class BackupSettings
    {
        public bool Enabled { get; set; }
        public BackupInterval IntervalType { get; set; }
        public DateTime LastBackupDate { get; set; }
        public int MaxBackupCount { get; set; }
        public string BackupPath { get; set; } = string.Empty;
    }

    public enum BackupInterval
    {
        Hourly = 0,      // For testing
        Daily = 1,
        EveryThreeDays = 2,
        Weekly = 3
    }

    public class BackupInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public long Size { get; set; }
        public int ClientCount { get; set; }
        public int CaseCount { get; set; }
        public int ItemCount { get; set; }

        public string FormattedSize
        {
            get
            {
                if (Size < 1024)
                    return $"{Size} B";
                if (Size < 1024 * 1024)
                    return $"{Size / 1024.0:F2} KB";
                return $"{Size / (1024.0 * 1024.0):F2} MB";
            }
        }
    }

    #endregion
}