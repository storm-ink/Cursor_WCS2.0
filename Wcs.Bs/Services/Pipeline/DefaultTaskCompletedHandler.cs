using Wcs.Bs.Domain;

namespace Wcs.Bs.Services.Pipeline;

/// <summary>
/// 默认主任务完成后处理器示例。
/// 实际项目可替换为：WMS 完工回报、生成下游任务、库存更新等。
/// </summary>
public class DefaultTaskCompletedHandler : ITaskCompletedHandler
{
    private readonly ILogger<DefaultTaskCompletedHandler> _logger;

    public DefaultTaskCompletedHandler(ILogger<DefaultTaskCompletedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(TaskEntity task)
    {
        _logger.LogInformation("[Pipeline] Task completed hook: {TaskCode}, type={Type}",
            task.TaskCode, task.Type);

        // 示例：可在此处添加后续业务
        // await wmsApi.ReportTaskComplete(task.TaskCode, task.EndLocationCode);
        // await inventoryService.ConfirmInbound(task.PalletCode, task.EndLocationCode);

        return Task.CompletedTask;
    }
}
