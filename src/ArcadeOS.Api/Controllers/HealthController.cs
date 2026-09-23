using Microsoft.AspNetCore.Mvc;

namespace ArcadeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "ArcadeOS.Api",
            timestamp = DateTime.UtcNow
        });
    }
}
