using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MonBureau.Infrastructure.Data;

namespace MonBureau.Infrastructure.Services
{
    public class BackupService
    {
        private readonly string _dbPath;
        private readonly string _defaultBackupPath;
        private readonly IDbContextFactory<AppDbContext>? _contextFactory;

        // Constructor for with DbContextFactory (preferred)
        public BackupService(string dbPath, IDbContextFactory<AppDbContext> contextFactory)
        {
            _dbPath = dbPath;
            _contextFactory = contextFactory;
            _defaultBackupPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MonBureau",
                "Backups"
            );

            Directory.CreateDirectory(_defaultBackupPath);
        }

        // Constructor for backward compatibility
        public BackupService(string dbPath) : this(dbPath, null!)
        {
        }

        public async Task<(bool Success, string Message, string? FilePath)> CreateBackupAsync(string? customPath = null)
        {
            try
            {
                // Close all connections if contextFactory is available
                if (_contextFactory != null)
                {
                    await using var context = await _contextFactory.CreateDbContextAsync();
                    await context.Database.CloseConnectionAsync();
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                var fileName = $"MonBureau_Backup_{timestamp}.zip";
                var backupPath = customPath ?? Path.Combine(_defaultBackupPath, fileName);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create compressed backup with metadata
                using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    // Add database file
                    archive.CreateEntryFromFile(_dbPath, "monbureau.db", CompressionLevel.Optimal);

                    // Add metadata
                    var metadataEntry = archive.CreateEntry("backup-info.json");
                    await using var metadataStream = metadataEntry.Open();

                    var metadata = new BackupMetadata
                    {
                        CreatedAt = DateTime.UtcNow,
                        Version = "1.0.0",
                        DatabasePath = _dbPath,
                        FileSize = new FileInfo(_dbPath).Length
                    };

                    // Add record count if context factory is available
                    if (_contextFactory != null)
                    {
                        await using var context = await _contextFactory.CreateDbContextAsync();
                        metadata.ClientCount = await context.Clients.CountAsync();
                        metadata.CaseCount = await context.Cases.CountAsync();
                        metadata.ItemCount = await context.CaseItems.CountAsync();
                    }

                    await JsonSerializer.SerializeAsync(metadataStream, metadata, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }

                var fileInfo = new FileInfo(backupPath);
                var sizeMB = fileInfo.Length / (1024.0 * 1024.0);

                return (true, $"Sauvegarde créée avec succès ({sizeMB:F2} MB)", backupPath);
            }
            catch (Exception ex)
            {
                return (false, $"Erreur lors de la sauvegarde: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message)> RestoreBackupAsync(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    return (false, "Le fichier de sauvegarde n'existe pas");
                }

                // Close all connections if contextFactory is available
                if (_contextFactory != null)
                {
                    await using var context = await _contextFactory.CreateDbContextAsync();
                    await context.Database.CloseConnectionAsync();
                }

                // Create a backup of current database before restoring
                var tempBackup = Path.Combine(_defaultBackupPath, $"Pre-Restore_{DateTime.Now:yyyy-MM-dd_HHmmss}.db");
                await Task.Run(() => File.Copy(_dbPath, tempBackup, overwrite: true));

                // Extract and restore from zip
                var tempExtractPath = Path.Combine(Path.GetTempPath(), $"MonBureau_Restore_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempExtractPath);

                try
                {
                    // Extract zip
                    ZipFile.ExtractToDirectory(backupPath, tempExtractPath);

                    // Verify database file exists in backup
                    var extractedDbPath = Path.Combine(tempExtractPath, "monbureau.db");
                    if (!File.Exists(extractedDbPath))
                    {
                        return (false, "Le fichier de sauvegarde ne contient pas de base de données valide");
                    }

                    // Read metadata if available
                    var metadataPath = Path.Combine(tempExtractPath, "backup-info.json");
                    if (File.Exists(metadataPath))
                    {
                        var metadataJson = await File.ReadAllTextAsync(metadataPath);
                        var metadata = JsonSerializer.Deserialize<BackupMetadata>(metadataJson);
                        // Could display metadata to user for verification
                    }

                    // Restore the database
                    await Task.Run(() => File.Copy(extractedDbPath, _dbPath, overwrite: true));

                    return (true, "Base de données restaurée avec succès");
                }
                finally
                {
                    // Cleanup temp directory
                    if (Directory.Exists(tempExtractPath))
                    {
                        Directory.Delete(tempExtractPath, recursive: true);
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Erreur lors de la restauration: {ex.Message}");
            }
        }

        public async Task<string[]> GetBackupHistoryAsync()
        {
            try
            {
                return await Task.Run(() =>
                    Directory.GetFiles(_defaultBackupPath, "MonBureau_Backup_*.zip")
                );
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public async Task<BackupMetadata?> GetBackupMetadataAsync(string backupPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(backupPath);
                var metadataEntry = archive.GetEntry("backup-info.json");

                if (metadataEntry == null)
                    return null;

                await using var stream = metadataEntry.Open();
                return await JsonSerializer.DeserializeAsync<BackupMetadata>(stream);
            }
            catch
            {
                return null;
            }
        }

        public string DefaultBackupPath => _defaultBackupPath;
    }

    public class BackupMetadata
    {
        public DateTime CreatedAt { get; set; }
        public string Version { get; set; } = string.Empty;
        public string DatabasePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int ClientCount { get; set; }
        public int CaseCount { get; set; }
        public int ItemCount { get; set; }
    }
}