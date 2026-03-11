using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;
using Wcs.Bs.Infrastructure;
using Wcs.Bs.Services.Pipeline;
using TaskStatus = Wcs.Bs.Domain.TaskStatus;

namespace Wcs.Bs.Services;

public class TaskService
{
    private readonly WcsDbContext _db;
    private readonly PathConfigService _pathService;
    private readonly ILogger<TaskService> _logger;
    private readonly PipelineConfig _pipelineConfig;
    private readonly IServiceProvider _serviceProvider;

    public TaskService(
        WcsDbContext db,
        PathConfigService pathService,
        ILogger<TaskService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _db = db;
        _pathService = pathService;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _pipelineConfig = configuration.GetSection("Pipeline").Get<PipelineConfig>() ?? new PipelineConfig();
    }

    // ── 任务编号：日期 + GUID 短码，天然唯一 ──
    private static string GenerateTaskCode()
    {
        return $"T{DateTime.Now:yyyyMMdd}{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    public async Task<TaskEntity> CreateTaskAsync(CreateTaskRequest request)
    {
        var taskCode = request.TaskCode;
        if (string.IsNullOrWhiteSpace(taskCode))
            taskCode = GenerateTaskCode();

        var existing = await _db.Tasks.AnyAsync(t => t.TaskCode == taskCode);
        if (existing)
            throw new InvalidOperationException($"任务编号 {taskCode} 已存在");

        var pathConfig = await _pathService.MatchPathAsync(request.StartLocationCode, request.EndLocationCode);
        if (pathConfig == null)
            throw new InvalidOperationException($"未找到 {request.StartLocationCode} → {request.EndLocationCode} 的路径配置");

        var craneSteps = pathConfig.Steps.Where(s => s.DeviceType == "Crane").ToList();
        foreach (var cs in craneSteps)
        {
            var reachable = await _pathService.IsDestinationReachableAsync(cs.DeviceCode, request.EndLocationCode);
            if (!reachable)
                throw new InvalidOperationException($"终点 {request.EndLocationCode} 不在堆垛机 {cs.DeviceCode} 可达范围内");
        }

        var task = new TaskEntity
        {
            TaskCode = taskCode,
            Source = request.Source,
            Type = request.Type,
            PalletCode = request.PalletCode,
            StartLocationCode = request.StartLocationCode,
            StartLocationDeviceName = request.StartLocationDeviceName,
            EndLocationCode = request.EndLocationCode,
            EndLocationDeviceName = request.EndLocationDeviceName,
            CurrentLocationCode = request.StartLocationCode,
            CurrentLocationDeviceName = request.StartLocationDeviceName,
            PathCode = pathConfig.PathCode,
            CurrentStepOrder = 1,
            TotalSteps = pathConfig.Steps.Count,
            Priority = request.Priority,
            Status = TaskStatus.Created,
            Description = request.Description
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();
        await CreateDeviceTaskForStepAsync(task, pathConfig, 1);

        _logger.LogInformation("[Task] Created {TaskCode} from {Source} to {Dest}, path={Path}",
            taskCode, request.StartLocationCode, request.EndLocationCode, pathConfig.PathCode);

        return task;
    }

    public async Task CreateDeviceTaskForStepAsync(TaskEntity task, PathConfigJson pathConfig, int stepOrder)
    {
        var exists = await _db.DeviceTasks.AnyAsync(d => d.TaskId == task.Id && d.StepOrder == stepOrder);
        if (exists)
        {
            _logger.LogWarning("[Task] Device task step {Step} for {TaskCode} already exists, skip", stepOrder, task.TaskCode);
            return;
        }

        var step = pathConfig.Steps.FirstOrDefault(s => s.StepOrder == stepOrder);
        if (step == null) return;

        var segSource = step.SegmentSource
            .Replace("{Source}", task.StartLocationCode)
            .Replace("{Destination}", task.EndLocationCode);
        var segDest = step.SegmentDest
            .Replace("{Source}", task.StartLocationCode)
            .Replace("{Destination}", task.EndLocationCode);

        _db.DeviceTasks.Add(new DeviceTaskEntity
        {
            TaskId = task.Id,
            TaskCode = task.TaskCode,
            StepOrder = stepOrder,
            DeviceType = step.DeviceType == "Crane" ? DeviceType.Crane : DeviceType.Conveyor,
            DeviceCode = step.DeviceCode,
            SegmentSource = segSource,
            SegmentDest = segDest,
            Status = DeviceTaskStatus.Waiting,
            RoutingNo = step.RoutingNo
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("[Task] Created device task step {Step} for {TaskCode}: {Device} {Source}->{Dest}",
            stepOrder, task.TaskCode, step.DeviceCode, segSource, segDest);
    }

    public async Task OnDeviceTaskCompletedAsync(long deviceTaskId)
    {
        var deviceTask = await _db.DeviceTasks
            .Include(d => d.Task)
            .FirstOrDefaultAsync(d => d.Id == deviceTaskId);

        if (deviceTask?.Task == null) return;
        if (deviceTask.Status == DeviceTaskStatus.Finished) return;

        deviceTask.Status = DeviceTaskStatus.Finished;
        deviceTask.FinishedAt = DateTime.Now;
        var task = deviceTask.Task;

        if (_pipelineConfig.EnableDeviceTaskCompletedHandler)
        {
            try
            {
                var handler = _serviceProvider.GetService<IDeviceTaskCompletedHandler>();
                if (handler != null) await handler.HandleAsync(deviceTask, task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Pipeline] DeviceTaskCompletedHandler error for {TaskCode}-{Step}",
                    deviceTask.TaskCode, deviceTask.StepOrder);
            }
        }

        task.CurrentStepOrder = deviceTask.StepOrder + 1;

        if (deviceTask.StepOrder >= task.TotalSteps)
        {
            task.Status = TaskStatus.Finished;
            task.FinishedAt = DateTime.Now;
            _logger.LogInformation("[Task] Task {TaskCode} completed", task.TaskCode);

            if (_pipelineConfig.EnableTaskCompletedHandler)
            {
                try
                {
                    var handler = _serviceProvider.GetService<ITaskCompletedHandler>();
                    if (handler != null) await handler.HandleAsync(task);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Pipeline] TaskCompletedHandler error for {TaskCode}", task.TaskCode);
                }
            }
        }
        else
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var pathJson = await _db.PathConfigs
                .Where(p => p.PathCode == task.PathCode)
                .Select(p => p.ConfigJson)
                .FirstOrDefaultAsync();

            if (pathJson != null)
            {
                var config = JsonSerializer.Deserialize<PathConfigJson>(pathJson, options);
                if (config != null)
                    await CreateDeviceTaskForStepAsync(task, config, deviceTask.StepOrder + 1);
            }
        }

        await _db.SaveChangesAsync();
    }

    // ── 取消任务 ──
    public async Task<string?> CancelTaskAsync(long taskId)
    {
        var task = await _db.Tasks.FindAsync(taskId);
        if (task == null) return "任务不存在";
        if (task.Status == TaskStatus.Finished) return "任务已完成，无法取消";
        if (task.Status == TaskStatus.Cancelled) return "任务已取消";

        task.Status = TaskStatus.Cancelled;
        task.FinishedAt = DateTime.Now;
        task.ErrorMessage = "手动取消";

        var deviceTasks = await _db.DeviceTasks
            .Where(d => d.TaskId == taskId && d.Status != DeviceTaskStatus.Finished)
            .ToListAsync();

        foreach (var dt in deviceTasks)
        {
            dt.Status = DeviceTaskStatus.Error;
            dt.ErrorMessage = "主任务已取消";
            dt.FinishedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("[Task] Task {TaskCode} cancelled", task.TaskCode);
        return null;
    }

    // ── 重试 Error 任务 ──
    public async Task<string?> RetryTaskAsync(long taskId)
    {
        var task = await _db.Tasks.FindAsync(taskId);
        if (task == null) return "任务不存在";
        if (task.Status != TaskStatus.Error) return "只能重试异常状态的任务";

        var errorDeviceTasks = await _db.DeviceTasks
            .Where(d => d.TaskId == taskId && d.Status == DeviceTaskStatus.Error)
            .ToListAsync();

        foreach (var dt in errorDeviceTasks)
        {
            dt.Status = DeviceTaskStatus.Waiting;
            dt.SendCount = 0;
            dt.ErrorMessage = null;
            dt.FinishedAt = null;
        }

        task.Status = TaskStatus.Created;
        task.ErrorMessage = null;

        await _db.SaveChangesAsync();
        _logger.LogInformation("[Task] Task {TaskCode} retried", task.TaskCode);
        return null;
    }

    // ── 查询 ──
    public async Task<List<TaskEntity>> GetCurrentTasksAsync(int page = 1, int pageSize = 20)
    {
        return await _db.Tasks
            .Where(t => t.Status != TaskStatus.Finished && t.Status != TaskStatus.Cancelled)
            .OrderByDescending(t => t.Priority)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<(List<TaskEntity> Items, int Total)> GetHistoryTasksAsync(
        DateTime? startDate, DateTime? endDate, TaskStatus? status, TaskType? type, int page = 1, int pageSize = 20)
    {
        var query = _db.Tasks.AsQueryable();
        if (startDate.HasValue) query = query.Where(t => t.CreatedAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(t => t.CreatedAt <= endDate.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (type.HasValue) query = query.Where(t => t.Type == type.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<List<DeviceTaskEntity>> GetDeviceTasksAsync(string taskCode)
    {
        return await _db.DeviceTasks
            .Where(d => d.TaskCode == taskCode)
            .OrderBy(d => d.StepOrder)
            .ToListAsync();
    }

    // ── 数据归档清理 ──
    public async Task<int> CleanupOldTasksAsync(int retainDays)
    {
        var cutoff = DateTime.Now.AddDays(-retainDays);
        var oldTasks = await _db.Tasks
            .Where(t => (t.Status == TaskStatus.Finished || t.Status == TaskStatus.Cancelled)
                        && t.FinishedAt != null && t.FinishedAt < cutoff)
            .ToListAsync();

        if (oldTasks.Count == 0) return 0;

        var taskIds = oldTasks.Select(t => t.Id).ToList();
        var oldDeviceTasks = await _db.DeviceTasks
            .Where(d => taskIds.Contains(d.TaskId))
            .ToListAsync();

        _db.DeviceTasks.RemoveRange(oldDeviceTasks);
        _db.Tasks.RemoveRange(oldTasks);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[Cleanup] Removed {TaskCount} old tasks and {DeviceTaskCount} device tasks from current database (>{Days} days)",
            oldTasks.Count, oldDeviceTasks.Count, retainDays);

        return oldTasks.Count;
    }

    // ── 重启恢复 ──
    public async Task RecoverOnRestartAsync()
    {
        var sendingTasks = await _db.Tasks
            .Where(t => t.Status == TaskStatus.SendingToPlc || t.Status == TaskStatus.Running)
            .ToListAsync();
        foreach (var task in sendingTasks) task.Status = TaskStatus.Created;

        var deviceTasks = await _db.DeviceTasks
            .Where(d => d.Status == DeviceTaskStatus.SendingToPlc || d.Status == DeviceTaskStatus.Running)
            .ToListAsync();
        foreach (var dt in deviceTasks) { dt.Status = DeviceTaskStatus.Waiting; dt.SendCount = 0; }

        await _db.SaveChangesAsync();
        _logger.LogInformation("[Task] Recovered {TaskCount} tasks and {DeviceTaskCount} device tasks on restart",
            sendingTasks.Count, deviceTasks.Count);
    }
}

public class CreateTaskRequest
{
    public string? TaskCode { get; set; }
    public TaskSource Source { get; set; } = TaskSource.Manual;
    public TaskType Type { get; set; } = TaskType.Inbound;
    public string PalletCode { get; set; } = string.Empty;
    public string StartLocationCode { get; set; } = string.Empty;
    public string? StartLocationDeviceName { get; set; }
    public string EndLocationCode { get; set; } = string.Empty;
    public string? EndLocationDeviceName { get; set; }
    public int Priority { get; set; }
    public string? Description { get; set; }
}
