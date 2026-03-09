using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Infrastructure;
using Wcs.Bs.Services;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly WcsDbContext _db;
    private readonly DeviceService _deviceService;

    public HealthController(WcsDbContext db, DeviceService deviceService)
    {
        _db = db;
        _deviceService = deviceService;
    }

    [HttpGet]
    public async Task<IActionResult> Check()
    {
        var dbOk = false;
        try
        {
            dbOk = await _db.Database.CanConnectAsync();
        }
        catch { }

        var devices = _deviceService.GetAllStatuses();
        var plcStatuses = devices.Select(d => new
        {
            d.Code,
            d.Type,
            d.IsConnected,
            d.IsEnabled,
            d.State
        }).ToList();

        var allPlcOk = devices.All(d => d.IsConnected);

        var healthy = dbOk;
        var status = healthy ? "Healthy" : "Degraded";

        return Ok(new
        {
            status,
            timestamp = DateTime.Now,
            checks = new
            {
                database = dbOk ? "OK" : "FAIL",
                plcConnections = allPlcOk ? "OK" : "PARTIAL",
                devices = plcStatuses
            }
        });
    }
}
