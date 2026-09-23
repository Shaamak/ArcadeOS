using ArcadeOS.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ArcadeOS.UnitTests;

public class HealthControllerTests
{
    [Fact]
    public void GetHealth_ReturnsOkObjectResult_WithHealthyStatus()
    {
        // Arrange
        var controller = new HealthController();

        // Act
        var result = controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Reflect properties of anonymous object
        var statusProp = okResult.Value.GetType().GetProperty("status");
        var statusValue = statusProp?.GetValue(okResult.Value, null);

        Assert.Equal("Healthy", statusValue);
    }
}
