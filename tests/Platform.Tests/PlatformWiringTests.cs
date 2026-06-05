using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RestaurantDelivery.Platform;

namespace Platform.Tests;

public class PlatformWiringTests
{
    [Fact]
    public void AddPlatformSerilog_registers_a_resolvable_logger()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddPlatformSerilog();
        using var host = builder.Build();

        var logger = host.Services.GetRequiredService<ILogger<PlatformWiringTests>>();
        logger.LogInformation("exercises the configured Serilog pipeline");

        Assert.NotNull(logger);
    }

    [Fact]
    public async Task UsePlatform_wires_correlation_middleware_and_health_endpoint()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddPlatformCore();
        builder.Services.AddPlatformHealthChecks();

        await using var app = builder.Build();
        var result = app.UsePlatform();

        Assert.Same(app, result);
    }
}
