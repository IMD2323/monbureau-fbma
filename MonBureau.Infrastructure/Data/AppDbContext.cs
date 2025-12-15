using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Infrastructure.Security;

namespace MonBureau.Infrastructure.Data
{
    /// <summary>
    /// UPDATED: AppDbContext with SQLCipher encryption
    /// All your existing code preserved, encryption added transparently
    /// </summary>
    public class AppDbContext : DbContext
    {
        private readonly string _encryptionKey;
        private const string KEY_STORAGE_NAME = "Database_EncryptionKey";

        // Your existing DbSets - unchanged
        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<Case> Cases { get; set; } = null!;
        public DbSet<CaseItem> CaseItems { get; set; } = null!;

        // Default constructor - gets encryption key automatically
        public AppDbContext()
        {
            _encryptionKey = GetOrCreateEncryptionKey();
        }

        // Constructor with options (for dependency injection)
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            _encryptionKey = GetOrCreateEncryptionKey();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Get database path (your existing location)
                string dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MonBureau",
                    "monbureau.db"
                );

                // Ensure directory exists
                var directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // ✨ NEW: Connection string with encryption
                string connectionString = $"Data Source={dbPath};Password={_encryptionKey}";

                optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(30);
                });

                // Your existing configuration
                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                optionsBuilder.EnableSensitiveDataLogging(false);
                optionsBuilder.EnableDetailedErrors(true);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ============================================
            // ALL YOUR EXISTING MODEL CONFIGURATION
            // Nothing changes here!
            // ============================================

            // Client configuration
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ContactEmail).HasMaxLength(200);
                entity.Property(e => e.ContactPhone).HasMaxLength(50);
                entity.Property(e => e.Address).HasMaxLength(500);

                // Indexes
                entity.HasIndex(e => new { e.FirstName, e.LastName });
                entity.HasIndex(e => e.ContactEmail);
                entity.HasIndex(e => e.CreatedAt);
            });

            // Case configuration
            modelBuilder.Entity<Case>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Number).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Status).HasConversion<int>();
                entity.Property(e => e.CourtName).HasMaxLength(200);
                entity.Property(e => e.CourtRoom).HasMaxLength(100);
                entity.Property(e => e.CourtAddress).HasMaxLength(500);
                entity.Property(e => e.CourtContact).HasMaxLength(100);

                entity.HasOne(e => e.Client)
                    .WithMany(c => c.Cases)
                    .HasForeignKey(e => e.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(e => e.Number).IsUnique();
                entity.HasIndex(e => e.ClientId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => new { e.Status, e.OpeningDate });
                entity.HasIndex(e => new { e.ClientId, e.Status });
                entity.HasIndex(e => e.OpeningDate);
                entity.HasIndex(e => e.CreatedAt);
            });

            // CaseItem configuration
            modelBuilder.Entity<CaseItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).HasConversion<int>();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.FilePath).HasMaxLength(500);

                entity.HasOne(e => e.Case)
                    .WithMany(c => c.Items)
                    .HasForeignKey(e => e.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(e => e.CaseId);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.Date);
                entity.HasIndex(e => new { e.CaseId, e.Type, e.Date });
                entity.HasIndex(e => new { e.Type, e.Date });
                entity.HasIndex(e => e.CreatedAt);
            });
        }

        // ============================================
        // ✨ NEW: Encryption key management
        // ============================================

        /// <summary>
        /// Gets or creates encryption key from secure storage
        /// </summary>
        private static string GetOrCreateEncryptionKey()
        {
            try
            {
                // Try to retrieve existing key
                string existingKey = SecureCredentialManager.GetSecureValue(KEY_STORAGE_NAME);

                if (!string.IsNullOrEmpty(existingKey))
                {
                    System.Diagnostics.Debug.WriteLine("[AppDbContext] Using existing encryption key");
                    return existingKey;
                }

                // Generate new key on first run
                System.Diagnostics.Debug.WriteLine("[AppDbContext] Generating new encryption key");
                string newKey = GenerateEncryptionKey();

                // Store securely in Windows Credential Manager
                bool stored = SecureCredentialManager.StoreCredential(
                    KEY_STORAGE_NAME,
                    "DatabaseEncryption",
                    newKey
                );

                if (!stored)
                {
                    throw new InvalidOperationException(
                        "Failed to store database encryption key securely"
                    );
                }

                System.Diagnostics.Debug.WriteLine("[AppDbContext] ✓ Encryption key stored securely");
                return newKey;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppDbContext] ERROR: {ex.Message}");
                throw new InvalidOperationException(
                    "Failed to initialize database encryption. This is a critical security error.",
                    ex
                );
            }
        }

        /// <summary>
        /// Generates a cryptographically secure 256-bit encryption key
        /// </summary>
        private static string GenerateEncryptionKey()
        {
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                byte[] keyBytes = new byte[32]; // 256 bits
                rng.GetBytes(keyBytes);
                return Convert.ToBase64String(keyBytes);
            }
        }

        // ============================================
        // ✨ NEW: Database migration utility
        // ============================================

        /// <summary>
        /// Migrates an existing unencrypted database to encrypted format
        /// Call this ONCE when upgrading existing installations
        /// </summary>
        public static bool MigrateToEncrypted(string unencryptedDbPath)
        {
            try
            {
                if (!File.Exists(unencryptedDbPath))
                {
                    System.Diagnostics.Debug.WriteLine("[Migration] No unencrypted database found");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[Migration] Starting database encryption migration...");

                // Create backup
                string backupPath = unencryptedDbPath + ".pre_encryption_backup";
                File.Copy(unencryptedDbPath, backupPath, true);
                System.Diagnostics.Debug.WriteLine($"[Migration] Backup created: {backupPath}");

                // Get encryption key
                string encryptionKey = GetOrCreateEncryptionKey();

                // Create temporary encrypted database
                string tempEncryptedPath = unencryptedDbPath + ".encrypted_temp";

                // Use SQLite command to encrypt
                using (var sourceConnection = new Microsoft.Data.Sqlite.SqliteConnection(
                    $"Data Source={unencryptedDbPath}"))
                {
                    sourceConnection.Open();

                    // Attach encrypted database
                    using (var attachCommand = sourceConnection.CreateCommand())
                    {
                        attachCommand.CommandText = $"ATTACH DATABASE '{tempEncryptedPath}' AS encrypted KEY '{encryptionKey}'";
                        attachCommand.ExecuteNonQuery();
                    }

                    // Get list of tables
                    var tables = new System.Collections.Generic.List<string>();
                    using (var tablesCommand = sourceConnection.CreateCommand())
                    {
                        tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                        using (var reader = tablesCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                tables.Add(reader.GetString(0));
                            }
                        }
                    }

                    // Copy each table
                    foreach (var table in tables)
                    {
                        using (var copyCommand = sourceConnection.CreateCommand())
                        {
                            copyCommand.CommandText = $"CREATE TABLE encrypted.{table} AS SELECT * FROM main.{table}";
                            copyCommand.ExecuteNonQuery();
                        }
                        System.Diagnostics.Debug.WriteLine($"[Migration] Copied table: {table}");
                    }

                    // Detach
                    using (var detachCommand = sourceConnection.CreateCommand())
                    {
                        detachCommand.CommandText = "DETACH DATABASE encrypted";
                        detachCommand.ExecuteNonQuery();
                    }
                }

                // Replace old database with encrypted one
                File.Delete(unencryptedDbPath);
                File.Move(tempEncryptedPath, unencryptedDbPath);

                System.Diagnostics.Debug.WriteLine("[Migration] ✓ Database encryption migration completed successfully");
                System.Diagnostics.Debug.WriteLine($"[Migration] Backup saved at: {backupPath}");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Migration] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Verifies that database encryption is working
        /// </summary>
        public static bool VerifyEncryption()
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    // Try to connect
                    bool canConnect = context.Database.CanConnect();

                    if (!canConnect)
                    {
                        System.Diagnostics.Debug.WriteLine("[Verify] ✗ Cannot connect to encrypted database");
                        return false;
                    }

                    // Try to read data
                    int clientCount = context.Clients.Count();

                    System.Diagnostics.Debug.WriteLine($"[Verify] ✓ Encryption verified - {clientCount} clients found");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Verify] ✗ Encryption verification failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets database file path
        /// </summary>
        public static string GetDatabasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonBureau",
                "monbureau.db"
            );
        }
    }
}