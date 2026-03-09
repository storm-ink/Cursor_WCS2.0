using Wcs.Bs.Domain;

namespace Wcs.Bs.Services.Pipeline;

/// <summary>
/// 子任务下发前过滤器：判断设备任务是否允许立即下发给 PLC。
/// 返回 (canDispatch, reason)。reason 仅在 canDispatch=false 时有意义。
/// </summary>
public interface IDeviceTaskDispatchFilter
{
    Task<(bool CanDispatch, string? Reason)> CheckAsync(DeviceTaskEntity deviceTask, TaskEntity mainTask);
}
