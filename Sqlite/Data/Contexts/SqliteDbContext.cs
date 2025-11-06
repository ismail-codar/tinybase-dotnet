using Microsoft.EntityFrameworkCore;
using TinyBaseSqlitePersister.Models.Entities;

namespace TinyBaseSqlitePersister.Data.Contexts;

/// <summary>
/// Entity Framework Core DbContext for TinyBase SQLite persister
/// </summary>
public class SqliteDbContext : DbContext
{
    public SqliteDbContext(DbContextOptions<SqliteDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Stores collection
    /// </summary>
    public DbSet<SqliteStore> Stores { get; set; } = null!;

    /// <summary>
    /// Tables collection
    /// </summary>
    public DbSet<SqliteTable> Tables { get; set; } = null!;

    /// <summary>
    /// Cells collection
    /// </summary>
    public DbSet<SqliteCell> Cells { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Store configuration
        modelBuilder.Entity<SqliteStore>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(500);
            entity.Property(e => e.ConfigHash).HasMaxLength(64);
            entity.Property(e => e.Configuration).HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasMany(e => e.Tables)
                  .WithOne(t => t.Store)
                  .HasForeignKey(t => t.StoreId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Table configuration
        modelBuilder.Entity<SqliteTable>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(500);
            entity.Property(e => e.Schema).HasColumnType("TEXT");
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
        modelBuilder.Entity<SqliteCell>(entity =>
        {
            // Composite key using TableId, RowId, and ColumnId
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Value).HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.TableId);
            entity.HasIndex(e => new { e.TableId, e.RowId });
            entity.HasIndex(e => new { e.TableId, e.ColumnId });
        });

        // Indexes for performance
        modelBuilder.Entity<SqliteStore>()
            .HasIndex(e => e.ConfigHash)
            .IsUnique();

        modelBuilder.Entity<SqliteTable>()
            .HasIndex(e => e.IsManaged);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Default to in-memory database for development
            optionsBuilder.UseSqlite("Data Source=tinybase.db");
        }
    }
}