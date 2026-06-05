using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using RestaurantDelivery.Platform;
using Testcontainers.RabbitMq;

namespace Platform.Tests;

/// <summary>
/// Integration: a host wired with the platform messaging + health helpers reports healthy once it
/// connects to a real RabbitMQ (started via Testcontainers). Requires Docker.
/// </summary>
[Trait("Category", "Integration")]
public class PlatformMessagingHealthTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-management")
        .Build();

    public Task InitializeAsync() => _rabbitMq.StartAsync();

    public Task DisposeAsync() => _rabbitMq.DisposeAsync().AsTask();

    [Fact]
    public async Task Health_is_healthy_when_connected_to_rabbitmq()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddPlatformCore();
        builder.Services.AddPlatformHealthChecks();
        builder.Services.AddPlatformMessaging(_rabbitMq.GetConnectionString());

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var health = host.Services.GetRequiredService<HealthCheckService>();
            var report = await WaitForHealthyAsync(health);

            Assert.Equal(HealthStatus.Healthy, report.Status);
            Assert.NotEmpty(report.Entries); // MassTransit registers its bus health check
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task<HealthReport> WaitForHealthyAsync(HealthCheckService health)
    {
        var report = await health.CheckHealthAsync();
        for (var attempt = 0; attempt < 20 && report.Status != HealthStatus.Healthy; attempt++)
        {
            await Task.Delay(250);
            report = await health.CheckHealthAsync();
        }
        return report;
    }
}
