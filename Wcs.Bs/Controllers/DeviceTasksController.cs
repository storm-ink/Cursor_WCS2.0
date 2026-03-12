using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Wcs.Bs.Hubs;
using Wcs.Bs.Services;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,user")]
public class DeviceTasksController : ControllerBase
{
    private readonly TaskService _taskService;
    private readonly IHubContext<WcsHub> _hub;

    public DeviceTasksController(TaskService taskService, IHubContext<WcsHub> hub)
    {
        _taskService = taskService;
        _hub = hub;
    }

    /// <summary>
    /// 按任务号查询当前库设备子任务
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDeviceTasks([FromQuery] string taskCode)
    {
        if (string.IsNullOrEmpty(taskCode))
            return BadRequest(new { error = "taskCode is required" });

        var tasks = await _taskService.GetDeviceTasksAsync(taskCode);
        return Ok(tasks);
    }

    /// <summary>
    /// 按任务号查询历史库设备子任务
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistoryDeviceTasks([FromQuery] string taskCode)
    {
        if (string.IsNullOrEmpty(taskCode))
            return BadRequest(new { error = "taskCode is required" });

        var tasks = await _taskService.GetHistoryDeviceTasksAsync(taskCode);
        return Ok(tasks);
    }

    /// <summary>
    /// 按设备编号查询当前设备任务
    /// </summary>
    [HttpGet("bydevice/{code}")]
    public async Task<IActionResult> GetByDevice(string code)
    {
        var tasks = await _taskService.GetDeviceTasksByDeviceAsync(code);
        return Ok(tasks);
    }

    /// <summary>
    /// 按设备编号查询历史设备任务
    /// </summary>
    [HttpGet("bydevice/{code}/history")]
    public async Task<IActionResult> GetHistoryByDevice(string code)
    {
        var tasks = await _taskService.GetHistoryDeviceTasksByDeviceAsync(code);
        return Ok(tasks);
    }

    /// <summary>
    /// 重发设备任务
    /// </summary>
    [HttpPost("{id}/resend")]
    public async Task<IActionResult> Resend(long id)
    {
        var error = await _taskService.ResendDeviceTaskAsync(id);
        if (error != null) return BadRequest(new { error });
        await NotifyTasksChanged();
        return Ok(new { message = "设备任务已重发" });
    }

    /// <summary>
    /// 取消设备任务
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(long id)
    {
        var error = await _taskService.CancelDeviceTaskAsync(id);
        if (error != null) return BadRequest(new { error });
        await NotifyTasksChanged();
        return Ok(new { message = "设备任务已取消" });
    }

    /// <summary>
    /// 手动完成设备任务
    /// </summary>
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(long id)
    {
        var error = await _taskService.CompleteDeviceTaskAsync(id);
        if (error != null) return BadRequest(new { error });
        await NotifyTasksChanged();
        return Ok(new { message = "设备任务已完成" });
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
