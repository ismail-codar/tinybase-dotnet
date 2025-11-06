using Microsoft.EntityFrameworkCore;
using TinyBasePostgresPersister.Models.Entities;

namespace TinyBasePostgresPersister.Data.Contexts;

/// <summary>
/// Entity Framework Core DbContext for TinyBase PostgreSQL persister
/// </summary>
public class TinyBaseDbContext : DbContext
{
    public TinyBaseDbContext(DbContextOptions<TinyBaseDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Stores collection
    /// </summary>
    public DbSet<Store> Stores { get; set; } = null!;

    /// <summary>
    /// Tables collection
    /// </summary>
    public DbSet<Table> Tables { get; set; } = null!;

    /// <summary>
    /// Cells collection
    /// </summary>
    public DbSet<Cell> Cells { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Store configuration
        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(500);
            entity.Property(e => e.ConfigHash).HasMaxLength(64);
            entity.Property(e => e.Configuration).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasMany(e => e.Tables)
                  .WithOne(t => t.Store)
                  .HasForeignKey(t => t.StoreId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Table configuration
        modelBuilder.Entity<Table>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(500);
            entity.Property(e => e.Schema).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasMany(e => e.Cells)
                  .WithOne(c => c.Table)
                  .HasForeignKey(c => c.TableId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.StoreId);
            entity.HasIndex(e => e.IsManaged);
        });

        // Cell configuration
        modelBuilder.Entity<Cell>(entity =>
        {
            // Composite key using TableId, RowId, and ColumnId
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Value).HasColumnType("text");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.TableId);
            entity.HasIndex(e => new { e.TableId, e.RowId });
            entity.HasIndex(e => new { e.TableId, e.ColumnId });
        });

        // Indexes for performance
        modelBuilder.Entity<Store>()
            .HasIndex(e => e.ConfigHash)
            .IsUnique();

        modelBuilder.Entity<Table>()
            .HasIndex(e => e.IsManaged);
    }
}