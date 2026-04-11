using Microsoft.EntityFrameworkCore;

namespace CheapClerk.Data;

public sealed class ClerkDbContext(DbContextOptions<ClerkDbContext> options) : DbContext(options)
{
    public DbSet<CachedExtraction> CachedExtractions => Set<CachedExtraction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CachedExtraction>(entity =>
        {
            entity.HasKey(e => e.DocumentId);
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.HasIndex(e => e.ExpiryDate);
            entity.HasIndex(e => e.Category);
        });
    }
}
