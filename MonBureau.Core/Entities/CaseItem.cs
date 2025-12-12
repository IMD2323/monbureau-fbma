using System;
using System.ComponentModel.DataAnnotations;
using MonBureau.Core.Enums;

namespace MonBureau.Core.Entities
{
    public class CaseItem
    {
        public int Id { get; set; }

        public int CaseId { get; set; }
        public virtual Case Case { get; set; } = null!;

        public ItemType Type { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        // For expenses only
        [Range(0, double.MaxValue)]
        public decimal? Amount { get; set; }

        // For documents only
        [MaxLength(500)]
        public string? FilePath { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}