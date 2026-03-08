using Microsoft.AspNetCore.Mvc;
using Wcs.Bs.Services;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly PathConfigService _pathConfigService;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(PathConfigService pathConfigService, ILogger<ConfigController> logger)
    {
        _pathConfigService = pathConfigService;
        _logger = logger;
    }

    [HttpPost("import-paths")]
    public async Task<IActionResult> ImportPaths([FromBody] object json)
    {
        try
        {
            await _pathConfigService.ImportFromJsonAsync(json.ToString()!);
            _logger.LogInformation("[Config] Path config imported successfully");
            return Ok(new { message = "Import successful" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
