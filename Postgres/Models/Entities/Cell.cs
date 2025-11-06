using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TinyBasePostgresPersister.Models.Entities;

/// <summary>
/// Represents a cell containing data within a TinyBase table
/// </summary>
public class Cell
{
    /// <summary>
    /// Reference to the parent table
    /// </summary>
    [MaxLength(255)]
    public string TableId { get; set; } = string.Empty;

    /// <summary>
    /// Row identifier within the table
    /// </summary>
    [MaxLength(255)]
    public string RowId { get; set; } = string.Empty;

    /// <summary>
    /// Column identifier within the table
    /// </summary>
    [MaxLength(255)]
    public string ColumnId { get; set; } = string.Empty;

    /// <summary>
    /// The actual value stored in the cell
    /// </summary>
    [Column(TypeName = "text")]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// When the cell was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the cell was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Composite primary key combining table, row, and column identifiers
    /// </summary>
    [Key]
    [Column(Order = 1)]
    [MaxLength(255)]
    public string Id => $"{TableId}_{RowId}_{ColumnId}";

    /// <summary>
    /// Navigation property to the parent table
    /// </summary>
    [ForeignKey(nameof(TableId))]
    public virtual Table Table { get; set; } = null!;
}