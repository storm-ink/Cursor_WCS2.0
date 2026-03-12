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
    public DbSet<UserEntity> Users => Set<UserEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        WcsModelConfiguration.Configure(modelBuilder);

        // UserEntity 仅属于主库，历史库/备份库不需要 Users 表
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
        });
    }
}
