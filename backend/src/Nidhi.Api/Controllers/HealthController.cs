using Microsoft.AspNetCore.Mvc;

namespace Nidhi.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse("ok"));
}

public sealed record HealthResponse(string Status);
