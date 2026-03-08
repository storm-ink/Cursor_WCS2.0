using Microsoft.AspNetCore.Mvc;
using Wcs.Bs.Services;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeviceTasksController : ControllerBase
{
    private readonly TaskService _taskService;

    public DeviceTasksController(TaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDeviceTasks([FromQuery] string taskCode)
    {
        if (string.IsNullOrEmpty(taskCode))
            return BadRequest(new { error = "taskCode is required" });

        var tasks = await _taskService.GetDeviceTasksAsync(taskCode);
        return Ok(tasks);
    }
}
