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
        WcsModelConfiguration.Configure(modelBuilder);
    }
}
