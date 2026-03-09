using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Wcs.Bs.Hubs;
using Wcs.Bs.Services;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly DeviceService _deviceService;
    private readonly IHubContext<WcsHub> _hub;

    public DevicesController(DeviceService deviceService, IHubContext<WcsHub> hub)
    {
        _deviceService = deviceService;
        _hub = hub;
    }

    [HttpGet]
    public IActionResult GetDevices()
    {
        return Ok(_deviceService.GetAllStatuses());
    }

    [HttpGet("{code}/messages")]
    public IActionResult GetMessages(string code)
    {
        return Ok(_deviceService.GetMessages(code));
    }

    [HttpPost("{code}/enable")]
    public async Task<IActionResult> Enable(string code)
    {
        if (!_deviceService.SetEnabled(code, true))
            return NotFound(new { error = $"设备 {code} 不存在" });

        await _hub.Clients.Group("view:devices").SendAsync("DeviceStatusUpdated", _deviceService.GetAllStatuses());
        return Ok(new { message = $"设备 {code} 已启用" });
    }

    [HttpPost("{code}/disable")]
    public async Task<IActionResult> Disable(string code)
    {
        if (!_deviceService.SetEnabled(code, false))
            return NotFound(new { error = $"设备 {code} 不存在" });

        await _hub.Clients.Group("view:devices").SendAsync("DeviceStatusUpdated", _deviceService.GetAllStatuses());
        return Ok(new { message = $"设备 {code} 已禁用" });
    }
}
