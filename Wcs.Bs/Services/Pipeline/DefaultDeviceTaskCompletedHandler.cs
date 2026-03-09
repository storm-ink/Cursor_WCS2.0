using Wcs.Bs.Domain;

namespace Wcs.Bs.Services.Pipeline;

/// <summary>
/// 默认设备子任务完成后处理器示例。
/// 实际项目可替换为：WMS 回报、货位状态更新、AGV 调度等。
/// </summary>
public class DefaultDeviceTaskCompletedHandler : IDeviceTaskCompletedHandler
{
    private readonly ILogger<DefaultDeviceTaskCompletedHandler> _logger;

    public DefaultDeviceTaskCompletedHandler(ILogger<DefaultDeviceTaskCompletedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(DeviceTaskEntity deviceTask, TaskEntity mainTask)
    {
        _logger.LogInformation("[Pipeline] Device task completed hook: {TaskCode}-{Step} on {Device}",
            deviceTask.TaskCode, deviceTask.StepOrder, deviceTask.DeviceCode);

        // 示例：可在此处添加后续业务
        // await wmsApi.ReportSegmentComplete(mainTask.TaskCode, deviceTask.SegmentDest);
        // await locationService.UnlockLocation(deviceTask.SegmentSource);

        return Task.CompletedTask;
    }
}
