using Microsoft.AspNetCore.Mvc;
using Wcs.Bs.Domain;
using Wcs.Bs.Services;
using TaskStatus = Wcs.Bs.Domain.TaskStatus;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly TaskService _taskService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(TaskService taskService, ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrentTasks([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var tasks = await _taskService.GetCurrentTasksAsync(page, pageSize);
        return Ok(tasks);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] TaskStatus? status,
        [FromQuery] TaskType? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _taskService.GetHistoryTasksAsync(startDate, endDate, status, type, page, pageSize);
        return Ok(new { items, total, page, pageSize });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        try
        {
            var task = await _taskService.CreateTaskAsync(request);
            _logger.LogInformation("[API] Task {TaskCode} created", task.TaskCode);
            return Ok(task);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
