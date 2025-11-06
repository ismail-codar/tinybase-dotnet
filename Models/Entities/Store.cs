using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TinyBasePostgresPersister.Models.Entities;

/// <summary>
/// Represents a TinyBase store that can be persisted to PostgreSQL
/// </summary>
public class Store
{
    /// <summary>
    /// Unique identifier for the store
    /// </summary>
    [Key]
    [MaxLength(255)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Store name for display purposes
    /// </summary>
    [MaxLength(500)]
    public string? Name { get; set; }

    /// <summary>
    /// Whether this is a mergeable store
    /// </summary>
    public bool IsMergeable { get; set; }

    /// <summary>
    /// Store configuration as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Configuration { get; set; } = "{}";

    /// <summary>
    /// When the store was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the store was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Configuration hash for change detection
    /// </summary>
    [MaxLength(64)]
    public string ConfigHash { get; set; } = string.Empty;

    /// <summary>
    /// Whether persistence is enabled
    /// </summary>
    public bool IsPersisted { get; set; }

    /// <summary>
    /// Navigation property to tables belonging to this store
    /// </summary>
    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
}