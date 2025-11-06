using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TinyBasePostgresPersister.Models.Entities;

/// <summary>
/// Represents a table within a TinyBase store
/// </summary>
public class Table
{
    /// <summary>
    /// Unique identifier for the table
    /// </summary>
    [Key]
    [MaxLength(255)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the parent store
    /// </summary>
    [MaxLength(255)]
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// Table name for display purposes
    /// </summary>
    [MaxLength(500)]
    public string? Name { get; set; }

    /// <summary>
    /// Whether the table is managed by this persister
    /// </summary>
    public bool IsManaged { get; set; }

    /// <summary>
    /// Table schema information as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Schema { get; set; } = "{}";

    /// <summary>
    /// When the table was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the table was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this is a json-based table or tabular
    /// </summary>
    public bool IsJsonTable { get; set; }

    /// <summary>
    /// Navigation property to cells belonging to this table
    /// </summary>
    public virtual ICollection<Cell> Cells { get; set; } = new List<Cell>();

    /// <summary>
    /// Navigation property to the parent store
    /// </summary>
    [ForeignKey(nameof(StoreId))]
    public virtual Store Store { get; set; } = null!;
}