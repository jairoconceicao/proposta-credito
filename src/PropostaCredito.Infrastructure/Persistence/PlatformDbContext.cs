using Microsoft.EntityFrameworkCore;

namespace PropostaCredito.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public const string Owner = "PC_OWNER";
    public DbSet<PlatformMetadata> Metadata => Set<PlatformMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Owner);
        modelBuilder.Entity<PlatformMetadata>(entity =>
        {
            entity.ToTable("PLATFORM_METADATA");
            entity.HasKey(row => row.Key);
            entity.Property(row => row.Key).HasColumnName("KEY").HasMaxLength(100);
            entity.Property(row => row.Value).HasColumnName("VALUE").HasMaxLength(200).IsRequired();
            entity.HasData(new PlatformMetadata { Key = "schema-version", Value = "1" });
        });
    }
}

public sealed class PlatformMetadata
{
    public required string Key { get; init; }
    public required string Value { get; set; }
}
