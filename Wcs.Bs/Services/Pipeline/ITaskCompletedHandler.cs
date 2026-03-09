using Wcs.Bs.Domain;

namespace Wcs.Bs.Services.Pipeline;

/// <summary>
/// 主任务完成后的处理钩子。
/// 在主任务标记 Finished 之后调用。
/// </summary>
public interface ITaskCompletedHandler
{
    Task HandleAsync(TaskEntity task);
}
