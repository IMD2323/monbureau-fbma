using System;
using System.ComponentModel.DataAnnotations;
using MonBureau.Core.Enums;

namespace MonBureau.Core.Entities
{
    /// <summary>
    /// Expense entity for tracking case-related expenses
    /// </summary>
    public class Expense : EntityBase
    {
        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required]
        public ExpenseCategory Category { get; set; }

        [MaxLength(100)]
        public string? PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? Recipient { get; set; }

        public string? Notes { get; set; }

        // File attachment for receipt/invoice
        [MaxLength(500)]
        public string? ReceiptPath { get; set; }

        public bool IsPaid { get; set; } = false;

        // Relationships
        public int CaseId { get; set; }
        public virtual Case Case { get; set; } = null!;

        // Added by which client (optional - for tracking who submitted expense)
        public int? AddedByClientId { get; set; }
        public virtual Client? AddedByClient { get; set; }

        // Computed property
        public string DisplayName => $"{Category} - {Amount:C} - {Date:dd/MM/yyyy}";
    }
}