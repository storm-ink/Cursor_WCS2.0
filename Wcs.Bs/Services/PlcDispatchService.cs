using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;
using Wcs.Bs.Hubs;
using Wcs.Bs.Infrastructure;
using Wcs.Bs.Plc;
using Wcs.Bs.Services.Pipeline;

namespace Wcs.Bs.Services;

public class PlcDispatchService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DeviceService _deviceService;
    private readonly ILogger<PlcDispatchService> _logger;
    private readonly PlcConfig _plcConfig;
    private readonly CvPlcConfig _cvPlcConfig;
    private readonly PipelineConfig _pipelineConfig;
    private readonly SemaphoreSlim _reportLock = new(1, 1);

    public PlcDispatchService(
        IServiceProvider serviceProvider,
        DeviceService deviceService,
        ILogger<PlcDispatchService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _deviceService = deviceService;
        _logger = logger;
        _plcConfig = configuration.GetSection("Plc").Get<PlcConfig>() ?? new PlcConfig();
        _cvPlcConfig = configuration.GetSection("CvPlc").Get<CvPlcConfig>() ?? new CvPlcConfig();
        _pipelineConfig = configuration.GetSection("Pipeline").Get<PipelineConfig>() ?? new PipelineConfig();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);

        using var scope = _serviceProvider.CreateScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        await taskService.RecoverOnRestartAsync();

        await InitPlcConnectionsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingTasksAsync();
                await CheckTimeoutsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Dispatch] Error in dispatch loop");
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task InitPlcConnectionsAsync(CancellationToken token)
    {
        var configs = _deviceService.GetAllConfigs();
        foreach (var config in configs)
        {
            var client = new PlcClient(config.Code, config.PlcIp, config.PlcPort,
                _serviceProvider.GetRequiredService<ILogger<PlcClient>>());

            client.OnMessageReceived += (deviceCode, raw) =>
            {
                _deviceService.AddMessage(deviceCode, "接收", raw);
                _ = PushMessageToSignalR(deviceCode, raw);
            };

            client.OnReportReceived += (deviceCode, msg) =>
            {
                _ = HandleReportAsync(deviceCode, msg);
            };

            client.OnConnectionChanged += (deviceCode, connected) =>
            {
                _deviceService.UpdateStatus(deviceCode, s => s.IsConnected = connected);
                _ = PushDeviceStatusToSignalR();
            };

            _deviceService.SetClient(config.Code, client);

            try
            {
                await client.ConnectAsync(token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[PLC] Failed to connect to {Device}: {Msg}", config.Code, ex.Message);
            }
        }
    }

    // ────────────────────────────────────────────────
    // 下发调度：过滤器 + 发送
    // ────────────────────────────────────────────────
    private async Task DispatchPendingTasksAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WcsDbContext>();

        var waitingTasks = await db.DeviceTasks
            .Include(d => d.Task)
            .Where(d => d.Status == DeviceTaskStatus.Waiting)
            .OrderByDescending(d => d.Task!.Priority)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync();

        foreach (var dt in waitingTasks)
        {
            if (dt.Task == null) continue;

            var busyCheck = await db.DeviceTasks
                .AnyAsync(d => d.DeviceCode == dt.DeviceCode &&
                    (d.Status == DeviceTaskStatus.SendingToPlc || d.Status == DeviceTaskStatus.Running) &&
                    d.Id != dt.Id);

            if (busyCheck) continue;

            // ── 设备启用检查 ──
            if (!_deviceService.IsEnabled(dt.DeviceCode)) continue;

            var client = _deviceService.GetClient(dt.DeviceCode);
            if (client == null || !client.IsConnected) continue;

            // ── 过滤器：子任务下发前检查 ──
            if (_pipelineConfig.EnableDispatchFilter)
            {
                try
                {
                    var filter = scope.ServiceProvider.GetService<IDeviceTaskDispatchFilter>();
                    if (filter != null)
                    {
                        var (canDispatch, reason) = await filter.CheckAsync(dt, dt.Task);
                        if (!canDispatch)
                        {
                            _logger.LogInformation("[Filter] Blocked {TaskCode}-{Step}: {Reason}",
                                dt.TaskCode, dt.StepOrder, reason);
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Filter] Error checking dispatch filter for {TaskCode}-{Step}",
                        dt.TaskCode, dt.StepOrder);
                    continue;
                }
            }

            string command;
            if (dt.DeviceType == DeviceType.Conveyor)
                command = ConveyorProtocol.BuildTaskCommand(dt, dt.Task.PalletCode ?? "");
            else
                command = CraneProtocol.BuildTaskCommand(dt);

            await client.SendAsync(command);
            dt.Status = DeviceTaskStatus.SendingToPlc;
            dt.SendCount++;
            dt.LastSendTime = DateTime.Now;
            dt.StartedAt ??= DateTime.Now;

            if (dt.Task != null)
            {
                dt.Task.Status = Domain.TaskStatus.SendingToPlc;
                dt.Task.StartedAt ??= DateTime.Now;
            }

            _deviceService.AddMessage(dt.DeviceCode, "发送", command);

            await db.SaveChangesAsync();
            _logger.LogInformation("[Dispatch] Sent task {TaskNo} step {Step} to {Device}",
                dt.TaskCode, dt.StepOrder, dt.DeviceCode);

            await NotifyTasksChanged(scope);
        }
    }

    // ────────────────────────────────────────────────
    // 超时检查：SendingToPlc 超时回退重发
    // ────────────────────────────────────────────────
    private async Task CheckTimeoutsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WcsDbContext>();

        var sendingTasks = await db.DeviceTasks
            .Include(d => d.Task)
            .Where(d => d.Status == DeviceTaskStatus.SendingToPlc && d.LastSendTime != null)
            .ToListAsync();

        foreach (var dt in sendingTasks)
        {
            var timeout = dt.TimeoutSeconds > 0 ? dt.TimeoutSeconds : _plcConfig.TaskTimeoutSeconds;
            if (dt.LastSendTime!.Value.AddSeconds(timeout) > DateTime.Now) continue;

            if (dt.SendCount >= _plcConfig.MaxRetryCount)
            {
                dt.Status = DeviceTaskStatus.Error;
                dt.ErrorMessage = $"Timeout after {dt.SendCount} retries, PLC未确认";
                if (dt.Task != null)
                {
                    dt.Task.Status = Domain.TaskStatus.Error;
                    dt.Task.ErrorMessage = $"Device task step {dt.StepOrder} 发送超时";
                }
                _logger.LogWarning("[Dispatch] Task {TaskNo} step {Step} send confirmation timed out",
                    dt.TaskCode, dt.StepOrder);
            }
            else
            {
                dt.Status = DeviceTaskStatus.Waiting;
                _logger.LogInformation("[Dispatch] Retrying task {TaskNo} step {Step} (PLC未确认, 第{Count}次重发)",
                    dt.TaskCode, dt.StepOrder, dt.SendCount);
            }
        }

        await db.SaveChangesAsync();

        if (sendingTasks.Count > 0)
            await NotifyTasksChanged(scope);
    }

    // ────────────────────────────────────────────────
    // PLC 报文处理：发送确认 + 任务完成
    // ────────────────────────────────────────────────
    private async Task HandleReportAsync(string deviceCode, PlcMessage msg)
    {
        var cmd = msg.GetField("CMD");

        await _reportLock.WaitAsync();
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WcsDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<WcsHub>>();

            if (cmd == "CV_REPORT")
            {
                await HandleCvReportAsync(deviceCode, msg, db, scope);
            }
            else if (cmd == "CRANE_REPORT")
            {
                await HandleCraneReportAsync(deviceCode, msg, db, scope);
            }

            await hubContext.Clients.Group("view:devices").SendAsync("DeviceStatusUpdated",
                _deviceService.GetAllStatuses());
            await NotifyTasksChanged(scope);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Dispatch] Error handling report from {Device}", deviceCode);
        }
        finally
        {
            _reportLock.Release();
        }
    }

    private async Task HandleCvReportAsync(string deviceCode, PlcMessage msg, WcsDbContext db, IServiceScope scope)
    {
        var report = ConveyorProtocol.ParseReport(msg, _cvPlcConfig.TaskReportCount, _cvPlcConfig.LocationCount);
        _deviceService.UpdateStatus(deviceCode, s => s.IsConnected = true);

        foreach (var tr in report.Tasks)
        {
            if (string.IsNullOrEmpty(tr.TaskNo)) continue;

            if (tr.HandShake == "1")
            {
                // PLC 回报中包含此任务且 HandShake=1 → 发送确认成功, 标记为 Running
                await ConfirmSendSuccess(db, tr.TaskNo);
            }
            else if (tr.HandShake == "2")
            {
                // HandShake=2 → 任务完成
                await CompleteDeviceTaskByTaskNo(db, tr.TaskNo, scope);
            }
        }
    }

    private async Task HandleCraneReportAsync(string deviceCode, PlcMessage msg, WcsDbContext db, IServiceScope scope)
    {
        var report = CraneProtocol.ParseReport(msg);
        _deviceService.UpdateStatus(deviceCode, s =>
        {
            s.IsConnected = true;
            s.State = report.DeviceState;
            s.CurrentTaskNo = report.EquipmentTaskId;
        });

        if (string.IsNullOrEmpty(report.EquipmentTaskId)) return;

        if (report.TaskState == "1")
        {
            // PLC 回报含此任务且 TaskState=1 → 发送确认成功, 标记为 Running
            await ConfirmSendSuccess(db, report.EquipmentTaskId);
        }
        else if (report.TaskState == "2")
        {
            // TaskState=2 → 任务完成
            await CompleteDeviceTaskByTaskNo(db, report.EquipmentTaskId, scope);
        }
    }

    /// <summary>
    /// 发送确认：PLC 回报中包含匹配的任务ID，确认 PLC 已接收指令。
    /// SendingToPlc → Running
    /// </summary>
    private async Task ConfirmSendSuccess(WcsDbContext db, string taskNo)
    {
        var (taskCode, stepOrder) = ParseTaskNo(taskNo);
        if (taskCode == null) return;

        var deviceTask = await db.DeviceTasks
            .Include(d => d.Task)
            .FirstOrDefaultAsync(d => d.TaskCode == taskCode && d.StepOrder == stepOrder);

        if (deviceTask == null) return;

        if (deviceTask.Status != DeviceTaskStatus.SendingToPlc) return;

        deviceTask.Status = DeviceTaskStatus.Running;

        if (deviceTask.Task != null && deviceTask.Task.Status == Domain.TaskStatus.SendingToPlc)
        {
            deviceTask.Task.Status = Domain.TaskStatus.Running;
        }

        await db.SaveChangesAsync();

        _logger.LogInformation("[Dispatch] Send confirmed by PLC: {TaskNo}, status → Running", taskNo);
    }

    private async Task CompleteDeviceTaskByTaskNo(WcsDbContext db, string taskNo, IServiceScope scope)
    {
        var (taskCode, stepOrder) = ParseTaskNo(taskNo);
        if (taskCode == null) return;

        var deviceTask = await db.DeviceTasks
            .Include(d => d.Task)
            .FirstOrDefaultAsync(d => d.TaskCode == taskCode && d.StepOrder == stepOrder);

        if (deviceTask == null) return;

        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        await taskService.OnDeviceTaskCompletedAsync(deviceTask.Id);

        _logger.LogInformation("[Dispatch] Device task completed: {TaskNo}", taskNo);
    }

    private static (string? TaskCode, int StepOrder) ParseTaskNo(string taskNo)
    {
        var parts = taskNo.Split('-');
        if (parts.Length < 2) return (null, 0);

        var taskCode = string.Join("-", parts.Take(parts.Length - 1));
        if (!int.TryParse(parts.Last(), out var stepOrder)) return (null, 0);

        return (taskCode, stepOrder);
    }

    private async Task PushDeviceStatusToSignalR()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<WcsHub>>();
            await hub.Clients.Group("view:devices").SendAsync("DeviceStatusUpdated",
                _deviceService.GetAllStatuses());
        }
        catch { }
    }

    private async Task NotifyTasksChanged(IServiceScope scope)
    {
        try
        {
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<WcsHub>>();
            await hub.Clients.Group("view:tasks").SendAsync("TasksChanged");
        }
        catch { }
    }

    private async Task PushMessageToSignalR(string deviceCode, string rawData)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<WcsHub>>();
            await hubContext.Clients.Group($"view:messages:{deviceCode}").SendAsync("DeviceMessage", new
            {
                deviceCode,
                direction = "接收",
                rawData,
                timestamp = DateTime.Now
            });
        }
        catch { }
    }
}
