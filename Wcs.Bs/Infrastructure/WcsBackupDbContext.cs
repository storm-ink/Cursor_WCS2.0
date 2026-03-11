using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;

namespace Wcs.Bs.Infrastructure;

/// <summary>
/// 备份表数据库上下文：长期保留 N 个月的备份数据
/// </summary>
public class WcsBackupDbContext : DbContext
{
    public WcsBackupDbContext(DbContextOptions<WcsBackupDbContext> options) : base(options) { }

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<DeviceTaskEntity> DeviceTasks => Set<DeviceTaskEntity>();
    public DbSet<PathConfigEntity> PathConfigs => Set<PathConfigEntity>();
    public DbSet<CraneReachableConfigEntity> CraneReachableConfigs => Set<CraneReachableConfigEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        WcsModelConfiguration.Configure(modelBuilder);
    }
}
