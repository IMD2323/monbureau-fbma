using System;
using System.ComponentModel.DataAnnotations;

namespace MonBureau.Core.Entities
{
    /// <summary>
    /// Base class for all entities - eliminates duplicate CreatedAt/UpdatedAt properties
    /// </summary>
    public abstract class EntityBase
    {
        [Key]
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}