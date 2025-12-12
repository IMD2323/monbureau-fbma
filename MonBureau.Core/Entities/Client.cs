using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MonBureau.Core.Entities
{
    public class Client : EntityBase
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(200)]
        [EmailAddress]
        public string? ContactEmail { get; set; }

        [MaxLength(50)]
        public string? ContactPhone { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        public string? Notes { get; set; }

        // Navigation
        public virtual ICollection<Case> Cases { get; set; } = new List<Case>();

        // Computed
        public string FullName => $"{FirstName} {LastName}";
    }
}