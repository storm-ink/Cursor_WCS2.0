using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Wcs.Bs.Domain;
using Wcs.Bs.Hubs;
using Wcs.Bs.Services;
using TaskStatus = Wcs.Bs.Domain.TaskStatus;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly TaskService _taskService;
    private readonly IHubContext<WcsHub> _hub;
    private readonly ILogger<TasksController> _logger;

    public TasksController(TaskService taskService, IHubContext<WcsHub> hub, ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _hub = hub;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrentTasks([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;
        return Ok(await _taskService.GetCurrentTasksAsync(page, pageSize));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate,
        [FromQuery] TaskStatus? status, [FromQuery] TaskType? type,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;
        var (items, total) = await _taskService.GetHistoryTasksAsync(startDate, endDate, status, type, page, pageSize);
        return Ok(new { items, total, page, pageSize });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PalletCode))
            return BadRequest(new { error = "托盘号不能为空" });
        if (string.IsNullOrWhiteSpace(request.StartLocationCode))
            return BadRequest(new { error = "起点位置不能为空" });
        if (string.IsNullOrWhiteSpace(request.EndLocationCode))
            return BadRequest(new { error = "终点位置不能为空" });
        if (request.Priority < 0 || request.Priority > 99)
            return BadRequest(new { error = "优先级范围 0-99" });

        try
        {
            var task = await _taskService.CreateTaskAsync(request);
            _logger.LogInformation("[API] Task {TaskCode} created", task.TaskCode);
            await NotifyTasksChanged();
            return Ok(task);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelTask(long id)
    {
        var error = await _taskService.CancelTaskAsync(id);
        if (error != null) return BadRequest(new { error });
        await NotifyTasksChanged();
        return Ok(new { message = "任务已取消" });
    }

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> RetryTask(long id)
    {
        var error = await _taskService.RetryTaskAsync(id);
        if (error != null) return BadRequest(new { error });
        await NotifyTasksChanged();
        return Ok(new { message = "任务已重试" });
    }

    [HttpDelete("cleanup")]
    public async Task<IActionResult> Cleanup([FromQuery] int retainDays = 30)
    {
        if (retainDays < 1) retainDays = 1;
        var count = await _taskService.CleanupOldTasksAsync(retainDays);
        return Ok(new { removed = count, retainDays });
    }

    private async Task NotifyTasksChanged()
    {
        try
        {
            await _hub.Clients.Group("view:tasks").SendAsync("TasksChanged");
        }
        catch { }
    }
}
