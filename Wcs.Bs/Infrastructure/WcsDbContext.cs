using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;

namespace Wcs.Bs.Infrastructure;

public class WcsDbContext : DbContext
{
    public WcsDbContext(DbContextOptions<WcsDbContext> options) : base(options) { }

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<DeviceTaskEntity> DeviceTasks => Set<DeviceTaskEntity>();
    public DbSet<PathConfigEntity> PathConfigs => Set<PathConfigEntity>();
    public DbSet<CraneReachableConfigEntity> CraneReachableConfigs => Set<CraneReachableConfigEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskEntity>(entity =>
        {
            entity.HasIndex(e => e.TaskCode).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<DeviceTaskEntity>(entity =>
        {
            entity.HasIndex(e => new { e.TaskId, e.StepOrder });
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Task)
                  .WithMany(t => t.DeviceTasks)
                  .HasForeignKey(e => e.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PathConfigEntity>(entity =>
        {
            entity.HasIndex(e => e.PathCode);
        });

        modelBuilder.Entity<CraneReachableConfigEntity>(entity =>
        {
            entity.HasIndex(e => e.DeviceCode);
        });
    }
}
