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
    private static long _taskCounter = 0;

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

    public async Task<TaskEntity> CreateTaskAsync(CreateTaskRequest request)
    {
        var taskCode = request.TaskCode ?? $"T{DateTime.Now:yyyyMMddHHmmss}{Interlocked.Increment(ref _taskCounter):D4}";

        var existing = await _db.Tasks.AnyAsync(t => t.TaskCode == taskCode);
        if (existing)
            throw new InvalidOperationException($"Task code {taskCode} already exists");

        var pathConfig = await _pathService.MatchPathAsync(request.StartLocationCode, request.EndLocationCode);
        if (pathConfig == null)
            throw new InvalidOperationException($"No path found from {request.StartLocationCode} to {request.EndLocationCode}");

        var craneSteps = pathConfig.Steps.Where(s => s.DeviceType == "Crane").ToList();
        foreach (var cs in craneSteps)
        {
            var reachable = await _pathService.IsDestinationReachableAsync(cs.DeviceCode, request.EndLocationCode);
            if (!reachable)
                throw new InvalidOperationException($"Destination {request.EndLocationCode} is not reachable by crane {cs.DeviceCode}");
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

        _logger.LogInformation("[Task] Created task {TaskCode} from {Source} to {Dest}, path={Path}",
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

        var deviceTask = new DeviceTaskEntity
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
        };

        _db.DeviceTasks.Add(deviceTask);
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

        if (deviceTask.Status == DeviceTaskStatus.Finished)
        {
            _logger.LogDebug("[Task] Device task {Id} already finished, skip", deviceTaskId);
            return;
        }

        deviceTask.Status = DeviceTaskStatus.Finished;
        deviceTask.FinishedAt = DateTime.Now;

        var task = deviceTask.Task;

        // ── 钩子：设备子任务完成后处理 ──
        if (_pipelineConfig.EnableDeviceTaskCompletedHandler)
        {
            try
            {
                var handler = _serviceProvider.GetService<IDeviceTaskCompletedHandler>();
                if (handler != null)
                    await handler.HandleAsync(deviceTask, task);
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

            // ── 钩子：主任务完成后处理 ──
            if (_pipelineConfig.EnableTaskCompletedHandler)
            {
                try
                {
                    var handler = _serviceProvider.GetService<ITaskCompletedHandler>();
                    if (handler != null)
                        await handler.HandleAsync(task);
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
            var pathConfig = await _db.PathConfigs
                .Where(p => p.PathCode == task.PathCode)
                .Select(p => p.ConfigJson)
                .FirstOrDefaultAsync();

            if (pathConfig != null)
            {
                var config = JsonSerializer.Deserialize<PathConfigJson>(pathConfig, options);
                if (config != null)
                {
                    await CreateDeviceTaskForStepAsync(task, config, deviceTask.StepOrder + 1);
                }
            }
        }

        await _db.SaveChangesAsync();
    }

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

        if (startDate.HasValue)
            query = query.Where(t => t.CreatedAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(t => t.CreatedAt <= endDate.Value);
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        if (type.HasValue)
            query = query.Where(t => t.Type == type.Value);

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

    public async Task RecoverOnRestartAsync()
    {
        var sendingTasks = await _db.Tasks
            .Where(t => t.Status == TaskStatus.SendingToPlc || t.Status == TaskStatus.Running)
            .ToListAsync();

        foreach (var task in sendingTasks)
        {
            task.Status = TaskStatus.Created;
        }

        var deviceTasks = await _db.DeviceTasks
            .Where(d => d.Status == DeviceTaskStatus.SendingToPlc || d.Status == DeviceTaskStatus.Running)
            .ToListAsync();

        foreach (var dt in deviceTasks)
        {
            dt.Status = DeviceTaskStatus.Waiting;
            dt.SendCount = 0;
        }

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
