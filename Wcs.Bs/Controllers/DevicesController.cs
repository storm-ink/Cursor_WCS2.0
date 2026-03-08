using Microsoft.AspNetCore.Mvc;
using Wcs.Bs.Services;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly DeviceService _deviceService;

    public DevicesController(DeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    [HttpGet]
    public IActionResult GetDevices()
    {
        var statuses = _deviceService.GetAllStatuses();
        return Ok(statuses);
    }

    [HttpGet("{code}/messages")]
    public IActionResult GetMessages(string code)
    {
        var messages = _deviceService.GetMessages(code);
        return Ok(messages);
    }
}
