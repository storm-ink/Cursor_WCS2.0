using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;

namespace Wcs.Bs.Infrastructure;

/// <summary>
/// 共享的 EF Core 模型配置，供当前库、历史库、备份库共用
/// </summary>
public static class WcsModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
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
