using DomainCopilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence;

public sealed class DomainCopilotDbContext : DbContext
{
    public DomainCopilotDbContext(
        DbContextOptions<DomainCopilotDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.Source)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.Version)
                .HasMaxLength(100);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.FailureReason)
                .HasMaxLength(4000);
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Content)
                .IsRequired();

            entity.Property(x => x.Section)
                .HasMaxLength(500);

            entity.Property(x => x.Clause)
                .HasMaxLength(500);

            entity.Property(x => x.Embedding);

            entity.HasOne<Document>()
                .WithMany()
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new
            {
                x.DocumentId,
                x.ChunkIndex
            })
            .IsUnique();
        });
    }
}
