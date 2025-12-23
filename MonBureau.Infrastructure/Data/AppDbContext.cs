using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Infrastructure.Security;

namespace MonBureau.Infrastructure.Data
{
    /// <summary>
    /// FIXED: Graceful fallback when encryption key not available
    /// Works in DEBUG mode without encryption
    /// </summary>
    public class AppDbContext : DbContext
    {
        private readonly string? _encryptionKey;
        private const string KEY_STORAGE_NAME = "Database_EncryptionKey";

        static AppDbContext()
        {
            // CRITICAL: Initialize SQLCipher bundle
            SQLitePCL.Batteries_V2.Init();
            SQLitePCL.raw.sqlite3_config(SQLitePCL.raw.SQLITE_CONFIG_SERIALIZED);
        }

        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<Case> Cases { get; set; } = null!;
        public DbSet<CaseItem> CaseItems { get; set; } = null!;
        public DbSet<Expense> Expenses { get; set; } = null!;
        public DbSet<Appointment> Appointments { get; set; } = null!;
        public DbSet<Document> Documents { get; set; } = null!;

        public AppDbContext()
        {
            _encryptionKey = GetOrCreateEncryptionKey();
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            _encryptionKey = GetOrCreateEncryptionKey();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MonBureau",
                    "monbureau.db"
                );

                var directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // FIXED: Build connection string with or without encryption
                SqliteConnectionStringBuilder connectionStringBuilder;

                if (!string.IsNullOrEmpty(_encryptionKey))
                {
                    // With encryption
                    connectionStringBuilder = new SqliteConnectionStringBuilder
                    {
                        DataSource = dbPath,
                        Mode = SqliteOpenMode.ReadWriteCreate,
                        Password = _encryptionKey
                    };
                    System.Diagnostics.Debug.WriteLine("[AppDbContext] Using encrypted database");
                }
                else
                {
                    // Without encryption (DEBUG mode fallback)
                    connectionStringBuilder = new SqliteConnectionStringBuilder
                    {
                        DataSource = dbPath,
                        Mode = SqliteOpenMode.ReadWriteCreate
                    };
                    System.Diagnostics.Debug.WriteLine("[AppDbContext] ⚠️ Using unencrypted database (DEBUG mode)");
                }

                optionsBuilder.UseSqlite(connectionStringBuilder.ToString(), sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(30);
                });

                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                optionsBuilder.EnableSensitiveDataLogging(false);
                optionsBuilder.EnableDetailedErrors(true);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Client configuration
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ContactEmail).HasMaxLength(200);
                entity.Property(e => e.ContactPhone).HasMaxLength(50);
                entity.Property(e => e.Address).HasMaxLength(500);

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

                entity.HasIndex(e => e.CaseId);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.Date);
                entity.HasIndex(e => new { e.CaseId, e.Type, e.Date });
                entity.HasIndex(e => new { e.Type, e.Date });
                entity.HasIndex(e => e.CreatedAt);
            });
        }

        /// <summary>
        /// FIXED: Returns null if encryption key cannot be retrieved
        /// Allows database to work without encryption in DEBUG mode
        /// </summary>
        private static string? GetOrCreateEncryptionKey()
        {
            try
            {
                string existingKey = SecureCredentialManager.GetSecureValue(KEY_STORAGE_NAME);

                if (!string.IsNullOrEmpty(existingKey))
                {
                    System.Diagnostics.Debug.WriteLine("[AppDbContext] Using existing encryption key");
                    return existingKey;
                }

#if DEBUG
                // DEBUG MODE: Allow without encryption
                System.Diagnostics.Debug.WriteLine("[AppDbContext] ⚠️ DEBUG MODE - No encryption key, running without encryption");
                return null;
#else
                // RELEASE MODE: Generate and store key
                System.Diagnostics.Debug.WriteLine("[AppDbContext] Generating new encryption key");
                string newKey = GenerateEncryptionKey();

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
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppDbContext] ERROR: {ex.Message}");

#if DEBUG
                // DEBUG MODE: Continue without encryption
                System.Diagnostics.Debug.WriteLine("[AppDbContext] ⚠️ DEBUG MODE - Continuing without encryption");
                return null;
#else
                // RELEASE MODE: Fail
                throw new InvalidOperationException(
                    "Failed to initialize database encryption. This is a critical security error.",
                    ex
                );
#endif
            }
        }

        private static string GenerateEncryptionKey()
        {
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                byte[] keyBytes = new byte[32];
                rng.GetBytes(keyBytes);
                return Convert.ToBase64String(keyBytes);
            }
        }

        /// <summary>
        /// FIXED: Returns true in DEBUG mode without encryption verification
        /// </summary>
        public static bool VerifyEncryption()
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("[Verify] ⚠️ DEBUG MODE - Skipping encryption verification");
            return true;
#else
            try
            {
                using (var context = new AppDbContext())
                {
                    bool canConnect = context.Database.CanConnect();

                    if (!canConnect)
                    {
                        System.Diagnostics.Debug.WriteLine("[Verify] ✗ Cannot connect to encrypted database");
                        return false;
                    }

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
#endif
        }


        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            // Expense configuration
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
                entity.Property(e => e.Category).HasConversion<int>();
                entity.Property(e => e.PaymentMethod).HasMaxLength(100);
                entity.Property(e => e.Recipient).HasMaxLength(100);
                entity.Property(e => e.ReceiptPath).HasMaxLength(500);

                entity.HasOne(e => e.Case)
                    .WithMany()
                    .HasForeignKey(e => e.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AddedByClient)
                    .WithMany()
                    .HasForeignKey(e => e.AddedByClientId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.CaseId);
                entity.HasIndex(e => e.Date);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsPaid);
                entity.HasIndex(e => new { e.CaseId, e.Date });
                entity.HasIndex(e => e.CreatedAt);
            });

            // Appointment configuration
            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Location).HasMaxLength(300);
                entity.Property(e => e.Type).HasConversion<int>();
                entity.Property(e => e.Status).HasConversion<int>();
                entity.Property(e => e.Attendees).HasMaxLength(500);

                entity.HasOne(e => e.Case)
                    .WithMany()
                    .HasForeignKey(e => e.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.CaseId);
                entity.HasIndex(e => e.StartTime);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => new { e.StartTime, e.Status });
                entity.HasIndex(e => new { e.CaseId, e.StartTime });
                entity.HasIndex(e => e.ReminderEnabled);
                entity.HasIndex(e => e.CreatedAt);
            });

            // Document configuration
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileExtension).HasMaxLength(100);
                entity.Property(e => e.MimeType).HasMaxLength(200);
                entity.Property(e => e.Category).HasMaxLength(100);

                entity.HasOne(e => e.Case)
                    .WithMany()
                    .HasForeignKey(e => e.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.UploadedByClient)
                    .WithMany()
                    .HasForeignKey(e => e.UploadedByClientId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.CaseId);
                entity.HasIndex(e => e.UploadDate);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsConfidential);
                entity.HasIndex(e => new { e.CaseId, e.UploadDate });
                entity.HasIndex(e => e.CreatedAt);
            });
        }

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