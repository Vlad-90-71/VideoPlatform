using LessonService.Models;
using Microsoft.EntityFrameworkCore;

namespace LessonService.Data;

public class LessonDbContext(DbContextOptions<LessonDbContext> options) : DbContext(options)
{
    public DbSet<Lesson> Lessons => Set<Lesson>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);
            
            entity.Property(e => e.Description)
                .HasMaxLength(2000);
            
            entity.Property(e => e.VideoUrl)
                .HasMaxLength(500);
            
            entity.Property(e => e.CreatedAt)
                .IsRequired();
            
            entity.Property(e => e.UpdatedAt)
                .IsRequired();
            
            entity.HasIndex(e => e.VideoId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<Lesson>();
        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        
        return base.SaveChangesAsync(cancellationToken);
    }
}
