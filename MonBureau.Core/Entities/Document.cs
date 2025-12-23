using System;
using System.ComponentModel.DataAnnotations;

namespace MonBureau.Core.Entities
{
    /// <summary>
    /// Enhanced Document entity with client tracking
    /// Replaces/extends CaseItem for document management
    /// </summary>
    public class Document : EntityBase
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? FileExtension { get; set; }

        public long FileSizeBytes { get; set; }

        [MaxLength(200)]
        public string? MimeType { get; set; }

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        // Relationships
        [Required]
        public int CaseId { get; set; }
        public virtual Case Case { get; set; } = null!;

        // Track who uploaded the document
        public int? UploadedByClientId { get; set; }
        public virtual Client? UploadedByClient { get; set; }

        // Document categorization
        [MaxLength(100)]
        public string? Category { get; set; }

        public bool IsConfidential { get; set; } = false;

        // Computed properties
        public string FileSizeFormatted
        {
            get
            {
                if (FileSizeBytes < 1024)
                    return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024)
                    return $"{FileSizeBytes / 1024.0:F2} KB";
                return $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB";
            }
        }

        public string DisplayName => $"{Name} ({FileSizeFormatted})";
    }
}