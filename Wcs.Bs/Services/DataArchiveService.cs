using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;
using Wcs.Bs.Infrastructure;
using TaskStatus = Wcs.Bs.Domain.TaskStatus;

namespace Wcs.Bs.Services;

/// <summary>
/// 数据归档服务：将当前库中已完成/已取消的任务归档到历史库和备份库，
/// 并根据保留策略清理历史库和备份库中的过期数据。
/// </summary>
public class DataArchiveService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataArchiveService> _logger;

    public DataArchiveService(IServiceProvider serviceProvider, ILogger<DataArchiveService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 归档当前库中已完成/已取消且超过 retainDays 的任务到历史库和备份库，然后从当前库删除
    /// </summary>
    public async Task<int> ArchiveOldTasksAsync(int retainDays)
    {
        using var scope = _serviceProvider.CreateScope();
        var currentDb = scope.ServiceProvider.GetRequiredService<WcsDbContext>();
        var historyDb = scope.ServiceProvider.GetRequiredService<WcsHistoryDbContext>();
        var backupDb = scope.ServiceProvider.GetRequiredService<WcsBackupDbContext>();

        var cutoff = DateTime.Now.AddDays(-retainDays);
        var oldTasks = await currentDb.Tasks
            .Where(t => (t.Status == TaskStatus.Finished || t.Status == TaskStatus.Cancelled)
                        && t.FinishedAt != null && t.FinishedAt < cutoff)
            .ToListAsync();

        if (oldTasks.Count == 0) return 0;

        var taskIds = oldTasks.Select(t => t.Id).ToList();
        var oldDeviceTasks = await currentDb.DeviceTasks
            .Where(d => taskIds.Contains(d.TaskId))
            .ToListAsync();

        // 归档到历史库
        await CopyTasksToContextAsync(historyDb, oldTasks, oldDeviceTasks);
        _logger.LogInformation("[Archive] Copied {TaskCount} tasks and {DeviceTaskCount} device tasks to history database",
            oldTasks.Count, oldDeviceTasks.Count);

        // 归档到备份库
        await CopyTasksToContextAsync(backupDb, oldTasks, oldDeviceTasks);
        _logger.LogInformation("[Archive] Copied {TaskCount} tasks and {DeviceTaskCount} device tasks to backup database",
            oldTasks.Count, oldDeviceTasks.Count);

        // 从当前库删除
        currentDb.DeviceTasks.RemoveRange(oldDeviceTasks);
        currentDb.Tasks.RemoveRange(oldTasks);
        await currentDb.SaveChangesAsync();

        _logger.LogInformation("[Archive] Removed {TaskCount} tasks and {DeviceTaskCount} device tasks from current database (>{Days} days)",
            oldTasks.Count, oldDeviceTasks.Count, retainDays);

        return oldTasks.Count;
    }

    /// <summary>
    /// 清理历史库中超过保留月数的数据
    /// </summary>
    public async Task<int> CleanupHistoryAsync(int retentionMonths)
    {
        using var scope = _serviceProvider.CreateScope();
        var historyDb = scope.ServiceProvider.GetRequiredService<WcsHistoryDbContext>();
        return await CleanupByRetentionAsync(historyDb, retentionMonths, "History");
    }

    /// <summary>
    /// 清理备份库中超过保留月数的数据
    /// </summary>
    public async Task<int> CleanupBackupAsync(int retentionMonths)
    {
        using var scope = _serviceProvider.CreateScope();
        var backupDb = scope.ServiceProvider.GetRequiredService<WcsBackupDbContext>();
        return await CleanupByRetentionAsync(backupDb, retentionMonths, "Backup");
    }

    /// <summary>
    /// 重置当前表数据库（清空所有任务数据）
    /// </summary>
    public async Task ResetCurrentDatabaseAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var currentDb = scope.ServiceProvider.GetRequiredService<WcsDbContext>();
        await ResetTaskDataAsync(currentDb, "Current");
    }

    /// <summary>
    /// 重置历史表数据库（清空所有历史数据）
    /// </summary>
    public async Task ResetHistoryDatabaseAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var historyDb = scope.ServiceProvider.GetRequiredService<WcsHistoryDbContext>();
        await ResetTaskDataAsync(historyDb, "History");
    }

    private async Task ResetTaskDataAsync(DbContext db, string dbName)
    {
        if (db is WcsDbContext currentDb)
        {
            var deviceTasks = await currentDb.DeviceTasks.ToListAsync();
            currentDb.DeviceTasks.RemoveRange(deviceTasks);
            var tasks = await currentDb.Tasks.ToListAsync();
            currentDb.Tasks.RemoveRange(tasks);
            await currentDb.SaveChangesAsync();
        }
        else if (db is WcsHistoryDbContext historyDb)
        {
            var deviceTasks = await historyDb.DeviceTasks.ToListAsync();
            historyDb.DeviceTasks.RemoveRange(deviceTasks);
            var tasks = await historyDb.Tasks.ToListAsync();
            historyDb.Tasks.RemoveRange(tasks);
            await historyDb.SaveChangesAsync();
        }

        _logger.LogWarning("[Archive] Reset {DbName} database — all task data cleared", dbName);
    }

    private async Task<int> CleanupByRetentionAsync(DbContext db, int retentionMonths, string dbName)
    {
        var cutoff = DateTime.Now.AddMonths(-retentionMonths);

        DbSet<TaskEntity> tasks;
        DbSet<DeviceTaskEntity> deviceTasks;

        if (db is WcsHistoryDbContext historyDb)
        {
            tasks = historyDb.Tasks;
            deviceTasks = historyDb.DeviceTasks;
        }
        else if (db is WcsBackupDbContext backupDb)
        {
            tasks = backupDb.Tasks;
            deviceTasks = backupDb.DeviceTasks;
        }
        else
        {
            return 0;
        }

        var expiredTasks = await tasks
            .Where(t => t.FinishedAt != null && t.FinishedAt < cutoff)
            .ToListAsync();

        if (expiredTasks.Count == 0) return 0;

        var taskIds = expiredTasks.Select(t => t.Id).ToList();
        var expiredDeviceTasks = await deviceTasks
            .Where(d => taskIds.Contains(d.TaskId))
            .ToListAsync();

        deviceTasks.RemoveRange(expiredDeviceTasks);
        tasks.RemoveRange(expiredTasks);
        await db.SaveChangesAsync();

        _logger.LogInformation("[Archive] Cleaned {TaskCount} expired tasks and {DeviceTaskCount} device tasks from {DbName} database (>{Months} months)",
            expiredTasks.Count, expiredDeviceTasks.Count, dbName, retentionMonths);

        return expiredTasks.Count;
    }

    private static async Task CopyTasksToContextAsync(DbContext targetDb, List<TaskEntity> tasks, List<DeviceTaskEntity> deviceTasks)
    {
        DbSet<TaskEntity> targetTasks;
        DbSet<DeviceTaskEntity> targetDeviceTasks;

        if (targetDb is WcsHistoryDbContext historyDb)
        {
            targetTasks = historyDb.Tasks;
            targetDeviceTasks = historyDb.DeviceTasks;
        }
        else if (targetDb is WcsBackupDbContext backupDb)
        {
            targetTasks = backupDb.Tasks;
            targetDeviceTasks = backupDb.DeviceTasks;
        }
        else
        {
            return;
        }

        // 批量查询已存在的 TaskCode，避免 N+1 查询
        var taskCodes = tasks.Select(t => t.TaskCode).ToList();
        var existingTaskCodes = await targetTasks
            .Where(t => taskCodes.Contains(t.TaskCode))
            .Select(t => t.TaskCode)
            .ToHashSetAsync();

        foreach (var task in tasks)
        {
            if (existingTaskCodes.Contains(task.TaskCode)) continue;

            targetTasks.Add(new TaskEntity
            {
                Id = task.Id,
                TaskCode = task.TaskCode,
                Source = task.Source,
                Type = task.Type,
                PalletCode = task.PalletCode,
                StartLocationCode = task.StartLocationCode,
                StartLocationDeviceName = task.StartLocationDeviceName,
                EndLocationCode = task.EndLocationCode,
                EndLocationDeviceName = task.EndLocationDeviceName,
                CurrentLocationCode = task.CurrentLocationCode,
                CurrentLocationDeviceName = task.CurrentLocationDeviceName,
                Status = task.Status,
                PathCode = task.PathCode,
                CurrentStepOrder = task.CurrentStepOrder,
                TotalSteps = task.TotalSteps,
                Priority = task.Priority,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                FinishedAt = task.FinishedAt,
                ErrorMessage = task.ErrorMessage,
                Description = task.Description
            });
        }

        await targetDb.SaveChangesAsync();

        // 批量查询已存在的设备任务，避免 N+1 查询
        var archivedTaskIds = tasks.Select(t => t.Id).ToHashSet();
        var existingDeviceTaskKeys = await targetDeviceTasks
            .Where(d => archivedTaskIds.Contains(d.TaskId))
            .Select(d => new { d.TaskId, d.StepOrder })
            .ToListAsync();
        var existingDeviceTaskSet = existingDeviceTaskKeys
            .Select(k => (k.TaskId, k.StepOrder))
            .ToHashSet();

        foreach (var dt in deviceTasks)
        {
            if (!archivedTaskIds.Contains(dt.TaskId)) continue;
            if (existingDeviceTaskSet.Contains((dt.TaskId, dt.StepOrder))) continue;

            targetDeviceTasks.Add(new DeviceTaskEntity
            {
                Id = dt.Id,
                TaskId = dt.TaskId,
                TaskCode = dt.TaskCode,
                StepOrder = dt.StepOrder,
                DeviceType = dt.DeviceType,
                DeviceCode = dt.DeviceCode,
                SegmentSource = dt.SegmentSource,
                SegmentDest = dt.SegmentDest,
                Status = dt.Status,
                SendCount = dt.SendCount,
                LastSendTime = dt.LastSendTime,
                TimeoutSeconds = dt.TimeoutSeconds,
                CreatedAt = dt.CreatedAt,
                StartedAt = dt.StartedAt,
                FinishedAt = dt.FinishedAt,
                ErrorMessage = dt.ErrorMessage,
                RoutingNo = dt.RoutingNo
            });
        }

        await targetDb.SaveChangesAsync();
    }
}
