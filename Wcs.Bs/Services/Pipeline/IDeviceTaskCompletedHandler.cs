using Wcs.Bs.Domain;

namespace Wcs.Bs.Services.Pipeline;

/// <summary>
/// 设备子任务完成后的处理钩子。
/// 在子任务标记 Finished 之后、下一步拆分之前调用。
/// </summary>
public interface IDeviceTaskCompletedHandler
{
    Task HandleAsync(DeviceTaskEntity deviceTask, TaskEntity mainTask);
}
