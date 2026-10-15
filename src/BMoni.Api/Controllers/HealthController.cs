using Microsoft.AspNetCore.Mvc;

namespace BMoni.Api.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("api/health")]
    [HttpGet("health")]
    public IActionResult Get() => Ok(new { status = "ok", version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0" });
}
