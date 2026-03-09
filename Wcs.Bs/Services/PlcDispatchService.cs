using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;
using Wcs.Bs.Hubs;
using Wcs.Bs.Infrastructure;
using Wcs.Bs.Plc;

namespace Wcs.Bs.Services;

public class PlcDispatchService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DeviceService _deviceService;
    private readonly ILogger<PlcDispatchService> _logger;
    private readonly PlcConfig _plcConfig;
    private readonly CvPlcConfig _cvPlcConfig;
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

            _deviceService.SetClient(config.Code, client);

            try
            {
                await client.ConnectAsync(token);
                _deviceService.UpdateStatus(config.Code, s => s.IsConnected = true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[PLC] Failed to connect to {Device}: {Msg}", config.Code, ex.Message);
            }
        }
    }

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
            var busyCheck = await db.DeviceTasks
                .AnyAsync(d => d.DeviceCode == dt.DeviceCode &&
                    (d.Status == DeviceTaskStatus.SendingToPlc || d.Status == DeviceTaskStatus.Running) &&
                    d.Id != dt.Id);

            if (busyCheck) continue;

            var client = _deviceService.GetClient(dt.DeviceCode);
            if (client == null || !client.IsConnected) continue;

            string command;
            if (dt.DeviceType == DeviceType.Conveyor)
            {
                command = ConveyorProtocol.BuildTaskCommand(dt, dt.Task?.PalletCode ?? "");
            }
            else
            {
                command = CraneProtocol.BuildTaskCommand(dt);
            }

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
        }
    }

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
                dt.ErrorMessage = $"Timeout after {dt.SendCount} retries";
                if (dt.Task != null)
                {
                    dt.Task.Status = Domain.TaskStatus.Error;
                    dt.Task.ErrorMessage = $"Device task step {dt.StepOrder} timeout";
                }
                _logger.LogWarning("[Dispatch] Task {TaskNo} step {Step} timed out", dt.TaskCode, dt.StepOrder);
            }
            else
            {
                dt.Status = DeviceTaskStatus.Waiting;
                _logger.LogInformation("[Dispatch] Retrying task {TaskNo} step {Step}", dt.TaskCode, dt.StepOrder);
            }
        }

        await db.SaveChangesAsync();
    }

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
                var report = ConveyorProtocol.ParseReport(msg, _cvPlcConfig.TaskReportCount, _cvPlcConfig.LocationCount);
                foreach (var tr in report.Tasks.Where(t => t.HandShake == "2"))
                {
                    await CompleteDeviceTaskByTaskNo(db, tr.TaskNo, scope);
                }
                _deviceService.UpdateStatus(deviceCode, s => s.IsConnected = true);
            }
            else if (cmd == "CRANE_REPORT")
            {
                var report = CraneProtocol.ParseReport(msg);
                _deviceService.UpdateStatus(deviceCode, s =>
                {
                    s.IsConnected = true;
                    s.State = report.DeviceState;
                    s.CurrentTaskNo = report.EquipmentTaskId;
                });

                if (report.TaskState == "2")
                {
                    await CompleteDeviceTaskByTaskNo(db, report.EquipmentTaskId, scope);
                }
            }

            await hubContext.Clients.Group("view:devices").SendAsync("DeviceStatusUpdated",
                _deviceService.GetAllStatuses());
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

    private async Task CompleteDeviceTaskByTaskNo(WcsDbContext db, string taskNo, IServiceScope scope)
    {
        var parts = taskNo.Split('-');
        if (parts.Length < 2) return;

        var taskCode = string.Join("-", parts.Take(parts.Length - 1));
        if (!int.TryParse(parts.Last(), out var stepOrder)) return;

        var deviceTask = await db.DeviceTasks
            .Include(d => d.Task)
            .FirstOrDefaultAsync(d => d.TaskCode == taskCode && d.StepOrder == stepOrder);

        if (deviceTask == null) return;

        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        await taskService.OnDeviceTaskCompletedAsync(deviceTask.Id);

        _logger.LogInformation("[Dispatch] Device task completed: {TaskNo}", taskNo);
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
