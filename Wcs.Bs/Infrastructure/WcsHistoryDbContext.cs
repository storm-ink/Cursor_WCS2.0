using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;

namespace Wcs.Bs.Infrastructure;

/// <summary>
/// 历史表数据库上下文：保留最近 N 个月的已归档数据
/// </summary>
public class WcsHistoryDbContext : DbContext
{
    public WcsHistoryDbContext(DbContextOptions<WcsHistoryDbContext> options) : base(options) { }

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<DeviceTaskEntity> DeviceTasks => Set<DeviceTaskEntity>();
    public DbSet<PathConfigEntity> PathConfigs => Set<PathConfigEntity>();
    public DbSet<CraneReachableConfigEntity> CraneReachableConfigs => Set<CraneReachableConfigEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        WcsModelConfiguration.Configure(modelBuilder);
    }
}
