using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wcs.Bs.Domain;
using Wcs.Bs.Services;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,user")]
public class WmsController : ControllerBase
{
    private readonly TaskService _taskService;
    private readonly ILogger<WmsController> _logger;

    public WmsController(TaskService taskService, ILogger<WmsController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    [HttpPost("inbound-orders")]
    public async Task<IActionResult> InboundOrder([FromBody] WmsOrderRequest request)
    {
        try
        {
            var task = await _taskService.CreateTaskAsync(new CreateTaskRequest
            {
                TaskCode = request.OrderCode,
                Source = TaskSource.Wms,
                Type = TaskType.Inbound,
                PalletCode = request.PalletCode,
                StartLocationCode = request.StartLocationCode,
                StartLocationDeviceName = request.StartLocationDeviceName,
                EndLocationCode = request.EndLocationCode,
                EndLocationDeviceName = request.EndLocationDeviceName,
                Priority = request.Priority,
                Description = request.Description
            });

            _logger.LogInformation("[WMS] Inbound order {OrderCode} accepted as task {TaskCode}",
                request.OrderCode, task.TaskCode);
            return Ok(new { taskCode = task.TaskCode, status = "Created" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("outbound-orders")]
    public async Task<IActionResult> OutboundOrder([FromBody] WmsOrderRequest request)
    {
        try
        {
            var task = await _taskService.CreateTaskAsync(new CreateTaskRequest
            {
                TaskCode = request.OrderCode,
                Source = TaskSource.Wms,
                Type = TaskType.Outbound,
                PalletCode = request.PalletCode,
                StartLocationCode = request.StartLocationCode,
                StartLocationDeviceName = request.StartLocationDeviceName,
                EndLocationCode = request.EndLocationCode,
                EndLocationDeviceName = request.EndLocationDeviceName,
                Priority = request.Priority,
                Description = request.Description
            });

            _logger.LogInformation("[WMS] Outbound order {OrderCode} accepted as task {TaskCode}",
                request.OrderCode, task.TaskCode);
            return Ok(new { taskCode = task.TaskCode, status = "Created" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class WmsOrderRequest
{
    public string? OrderCode { get; set; }
    public string PalletCode { get; set; } = string.Empty;
    public string StartLocationCode { get; set; } = string.Empty;
    public string? StartLocationDeviceName { get; set; }
    public string EndLocationCode { get; set; } = string.Empty;
    public string? EndLocationDeviceName { get; set; }
    public int Priority { get; set; }
    public string? Description { get; set; }
}
