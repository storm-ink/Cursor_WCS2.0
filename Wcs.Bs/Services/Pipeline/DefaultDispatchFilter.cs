using Wcs.Bs.Domain;

namespace Wcs.Bs.Services.Pipeline;

/// <summary>
/// 默认下发过滤器示例。
/// 实际项目可替换为：检查货位锁定、检查设备维护状态、检查物料匹配等。
/// </summary>
public class DefaultDispatchFilter : IDeviceTaskDispatchFilter
{
    private readonly ILogger<DefaultDispatchFilter> _logger;

    public DefaultDispatchFilter(ILogger<DefaultDispatchFilter> logger)
    {
        _logger = logger;
    }

    public Task<(bool CanDispatch, string? Reason)> CheckAsync(DeviceTaskEntity deviceTask, TaskEntity mainTask)
    {
        // 示例：可在此处添加业务校验逻辑
        // if (deviceTask.DeviceCode == "CV01" && 某些条件)
        //     return Task.FromResult((false, "CV01 暂时不可用"));

        _logger.LogDebug("[Filter] Device task {TaskCode}-{Step} passed dispatch filter",
            deviceTask.TaskCode, deviceTask.StepOrder);

        return Task.FromResult<(bool, string?)>((true, null));
    }
}
