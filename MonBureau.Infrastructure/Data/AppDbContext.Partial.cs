using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;

namespace MonBureau.Infrastructure.Data
{
    /// <summary>
    /// Partial class containing new entity configurations for Expenses, Appointments, and Documents
    /// This keeps the main AppDbContext.cs clean and organized
    /// </summary>
    public partial class AppDbContext
    {
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
    }
}