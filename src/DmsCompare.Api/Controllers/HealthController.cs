using Microsoft.AspNetCore.Mvc;

namespace DmsCompare.Api.Controllers;

/// <summary>A simple "is the API running?" check.</summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        // An anonymous object, like returning { status: "ok" } in Express.
        return Ok(new { status = "ok" });
    }
}
