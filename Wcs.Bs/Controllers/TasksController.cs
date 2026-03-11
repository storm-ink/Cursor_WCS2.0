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
    private readonly DataArchiveService _archiveService;
    private readonly IHubContext<WcsHub> _hub;
    private readonly ILogger<TasksController> _logger;
    private readonly DataArchiveConfig _archiveConfig;

    public TasksController(
        TaskService taskService,
        DataArchiveService archiveService,
        IHubContext<WcsHub> hub,
        ILogger<TasksController> logger,
        IConfiguration configuration)
    {
        _taskService = taskService;
        _archiveService = archiveService;
        _hub = hub;
        _logger = logger;
        _archiveConfig = configuration.GetSection("DataArchive").Get<DataArchiveConfig>() ?? new DataArchiveConfig();
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

    /// <summary>
    /// 手动完成任务
    /// </summary>
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteTask(long id)
    {
        var error = await _taskService.CompleteTaskAsync(id);
        if (error != null) return BadRequest(new { error });
        await NotifyTasksChanged();
        return Ok(new { message = "任务已完成" });
    }

    /// <summary>
    /// 从历史库查询历史任务（仅已完成/已取消）
    /// </summary>
    [HttpGet("history/archive")]
    public async Task<IActionResult> GetHistoryArchive(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate,
        [FromQuery] TaskStatus? status, [FromQuery] TaskType? type,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;
        var (items, total) = await _taskService.GetHistoryTasksFromArchiveAsync(startDate, endDate, status, type, page, pageSize);
        return Ok(new { items, total, page, pageSize });
    }

    [HttpDelete("cleanup")]
    public async Task<IActionResult> Cleanup([FromQuery] int retainDays = 30)
    {
        if (retainDays < 1) retainDays = 1;
        var count = await _taskService.CleanupOldTasksAsync(retainDays);
        return Ok(new { removed = count, retainDays });
    }

    /// <summary>
    /// 手动触发归档：将当前库中已完成/已取消的旧任务归档到历史库和备份库
    /// </summary>
    [HttpPost("archive")]
    public async Task<IActionResult> Archive([FromQuery] int retainDays = 30)
    {
        if (retainDays < 1) retainDays = 1;
        try
        {
            var count = await _archiveService.ArchiveOldTasksAsync(retainDays);
            _logger.LogInformation("[API] Archived {Count} tasks (retainDays={Days})", count, retainDays);
            await NotifyTasksChanged();
            return Ok(new { archived = count, retainDays });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API] Archive failed");
            return StatusCode(500, new { error = "归档操作失败: " + ex.Message });
        }
    }

    /// <summary>
    /// 重置当前表数据库（需在配置中启用 EnableCurrentReset）
    /// </summary>
    [HttpPost("reset/current")]
    public async Task<IActionResult> ResetCurrentDatabase()
    {
        if (!_archiveConfig.EnableCurrentReset)
            return BadRequest(new { error = "当前表重置未启用，请在配置中设置 DataArchive:EnableCurrentReset = true" });

        try
        {
            await _archiveService.ResetCurrentDatabaseAsync();
            _logger.LogWarning("[API] Current database reset by user");
            await NotifyTasksChanged();
            return Ok(new { message = "当前表数据库已重置" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API] Current database reset failed");
            return StatusCode(500, new { error = "重置当前表失败: " + ex.Message });
        }
    }

    /// <summary>
    /// 重置历史表数据库（需在配置中启用 EnableHistoryReset）
    /// </summary>
    [HttpPost("reset/history")]
    public async Task<IActionResult> ResetHistoryDatabase()
    {
        if (!_archiveConfig.EnableHistoryReset)
            return BadRequest(new { error = "历史表重置未启用，请在配置中设置 DataArchive:EnableHistoryReset = true" });

        try
        {
            await _archiveService.ResetHistoryDatabaseAsync();
            _logger.LogWarning("[API] History database reset by user");
            return Ok(new { message = "历史表数据库已重置" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API] History database reset failed");
            return StatusCode(500, new { error = "重置历史表失败: " + ex.Message });
        }
    }

    /// <summary>
    /// 获取当前归档配置
    /// </summary>
    [HttpGet("archive/config")]
    public IActionResult GetArchiveConfig()
    {
        return Ok(_archiveConfig);
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
