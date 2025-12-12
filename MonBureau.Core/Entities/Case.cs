using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MonBureau.Core.Enums;

namespace MonBureau.Core.Entities
{
    public class Case : EntityBase
    {
        [Required]
        [MaxLength(50)]
        public string Number { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public CaseStatus Status { get; set; } = CaseStatus.Open;

        // Client reference
        public int ClientId { get; set; }
        public virtual Client Client { get; set; } = null!;

        // Court data (inline)
        [MaxLength(200)]
        public string? CourtName { get; set; }

        // NEW: Court Room/Chamber field
        [MaxLength(100)]
        public string? CourtRoom { get; set; }

        [MaxLength(500)]
        public string? CourtAddress { get; set; }

        [MaxLength(100)]
        public string? CourtContact { get; set; }

        // Dates
        public DateTime OpeningDate { get; set; } = DateTime.Today;
        public DateTime? ClosingDate { get; set; }

        // Navigation
        public virtual ICollection<CaseItem> Items { get; set; } = new List<CaseItem>();
    }
}