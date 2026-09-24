using Microsoft.AspNetCore.Mvc;
using Nidhi.Api.Controllers;
using Xunit;

namespace Nidhi.UnitTests;

public sealed class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsOkWithLivenessStatus()
    {
        var response = new HealthController().Get();

        var result = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<HealthResponse>(result.Value);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("ok", body.Status);
    }
}
