using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;

namespace MonBureau.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<Case> Cases { get; set; } = null!;
        public DbSet<CaseItem> CaseItems { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

                // ⚡ OPTIMIZED INDEXES
                entity.HasIndex(e => new { e.FirstName, e.LastName }); // Existing
                entity.HasIndex(e => e.ContactEmail); // NEW: For email lookups
                entity.HasIndex(e => e.CreatedAt); // NEW: For sorting recent clients
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

                // ⚡ OPTIMIZED INDEXES
                entity.HasIndex(e => e.Number).IsUnique(); // Existing
                entity.HasIndex(e => e.ClientId); // Existing
                entity.HasIndex(e => e.Status); // Existing

                // NEW: Composite indexes for common query patterns
                entity.HasIndex(e => new { e.Status, e.OpeningDate }); // Status + Date filtering
                entity.HasIndex(e => new { e.ClientId, e.Status }); // Client's cases by status
                entity.HasIndex(e => e.OpeningDate); // Sorting by date
                entity.HasIndex(e => e.CreatedAt); // Recent cases
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

                // ⚡ OPTIMIZED INDEXES
                entity.HasIndex(e => e.CaseId); // Existing
                entity.HasIndex(e => e.Type); // Existing

                // NEW: Composite indexes for common queries
                entity.HasIndex(e => e.Date); // Date range queries
                entity.HasIndex(e => new { e.CaseId, e.Type, e.Date }); // Case items by type and date
                entity.HasIndex(e => new { e.Type, e.Date }); // Items by type across cases
                entity.HasIndex(e => e.CreatedAt); // Recent items
            });
        }
    }
}