extern alias GatewayApp;
extern alias OrderApp;
extern alias PaymentApp;
extern alias DispatchApp;
extern alias TrackingApp;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace E2E.Tests.Infrastructure;

// Entry-point aliases: each referenced service/gateway declares its own global-namespace `Program`. The
// extern aliases above let us name each one unambiguously as the WebApplicationFactory<TEntryPoint> generic
// argument, so all five hosts can run in the one test process over the shared broker.
using OrderProgram = OrderApp::Program;
using PaymentProgram = PaymentApp::Program;
using DispatchProgram = DispatchApp::Program;
using TrackingProgram = TrackingApp::Program;
using GatewayProgram = GatewayApp::Program;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> that hosts one service's actual <c>Program.cs</c>
/// pipeline on a REAL Kestrel port (not the in-memory <c>TestServer</c>) and overrides its configuration to
/// point at the shared <see cref="StackFixture"/> containers. A real port is required because the gateway's
/// YARP proxy forwards over real HTTP, and a SignalR client connects over a real socket — so the E2E
/// exercises the genuine cross-process wiring (saga, consumers, EF schema, broker) rather than mocks.
/// </summary>
public abstract class ConfiguredFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly IReadOnlyDictionary<string, string?> _settings;

    protected ConfiguredFactory(IReadOnlyDictionary<string, string?> settings)
    {
        _settings = settings;
        // Run on a REAL Kestrel server on an OS-assigned port (not the in-memory TestServer). A real port is
        // required because the gateway's YARP proxy forwards over real HTTP and the SignalR client connects
        // over a real socket — so the E2E exercises the genuine cross-process wiring, not mocks.
        UseKestrel(0);
    }

    /// <summary>The real base address the host bound to (e.g. <c>http://127.0.0.1:49xxx/</c>).</summary>
    public string BaseAddress { get; private set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // The hosted services have no static/content files; anchor the content root at the test output dir
        // so WAF does not fail trying to locate the original project's content root.
        builder.UseContentRoot(AppContext.BaseDirectory);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_settings));
    }

    /// <summary>
    /// Starts the real Kestrel host (and therefore the broker connection, the saga, the consumers, and any
    /// seeders) and returns an <see cref="HttpClient"/> bound to the host's dynamically assigned address. It
    /// waits for the host to report Healthy first, which — because MassTransit registers a bus health check —
    /// guarantees the broker connection is up and the consumer endpoints are bound BEFORE the flow publishes,
    /// so no early lifecycle event is missed (otherwise the gateway's hub fan-out can race the first events).
    /// </summary>
    public async Task<HttpClient> StartAsync()
    {
        // CreateClient on a Kestrel-backed factory starts the server and points the client at the bound port.
        var client = CreateClient();
        BaseAddress = ClientOptions.BaseAddress.ToString();
        await WaitUntilHealthyAsync();
        return client;
    }

    private async Task WaitUntilHealthyAsync()
    {
        var health = Services.GetRequiredService<HealthCheckService>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var report = await health.CheckHealthAsync();
            if (report.Status == HealthStatus.Healthy)
            {
                return;
            }

            await Task.Delay(150);
        }
    }
}

/// <summary>Hosts the Order service (placement, restaurant/driver transitions, the saga) over the shared stack.</summary>
public sealed class OrderFactory(StackFixture stack) : ConfiguredFactory<OrderProgram>(new Dictionary<string, string?>
{
    ["ConnectionStrings:RabbitMq"] = stack.RabbitMqConnectionString,
    ["ConnectionStrings:Postgres"] = stack.OrderPostgresConnectionString,
});

/// <summary>Hosts the Payment service (capture consumer + the settlement callback) over the shared stack.</summary>
public sealed class PaymentFactory(StackFixture stack) : ConfiguredFactory<PaymentProgram>(new Dictionary<string, string?>
{
    ["ConnectionStrings:RabbitMq"] = stack.RabbitMqConnectionString,
    ["ConnectionStrings:Postgres"] = stack.PaymentPostgresConnectionString,
});

/// <summary>Hosts the Dispatch service (driver matcher + seeded drivers) over the shared stack.</summary>
public sealed class DispatchFactory(StackFixture stack) : ConfiguredFactory<DispatchProgram>(new Dictionary<string, string?>
{
    ["ConnectionStrings:RabbitMq"] = stack.RabbitMqConnectionString,
    ["ConnectionStrings:Redis"] = stack.DispatchRedisConnectionString,
    // Seed the deterministic drivers so the happy path auto-assigns one (Dispatch publishes DriverAssigned).
    ["Dispatch:SeedDrivers"] = "true",
});

/// <summary>Hosts the Tracking service (event projection + the status read) over the shared stack.</summary>
public sealed class TrackingFactory(StackFixture stack) : ConfiguredFactory<TrackingProgram>(new Dictionary<string, string?>
{
    ["ConnectionStrings:RabbitMq"] = stack.RabbitMqConnectionString,
    ["ConnectionStrings:Redis"] = stack.TrackingRedisConnectionString,
});

/// <summary>
/// Hosts the Gateway (YARP + role switcher + SignalR hub + the status-fan-out consumers) over the shared
/// broker. The YARP cluster destinations are overridden to the in-process service base addresses so the
/// happy path can be driven through the gateway's HTTP surface (the gateway IS in the HTTP path).
/// </summary>
public sealed class GatewayFactory : ConfiguredFactory<GatewayProgram>
{
    public GatewayFactory(StackFixture stack, string orderBaseAddress, string paymentBaseAddress, string trackingBaseAddress)
        : base(new Dictionary<string, string?>
        {
            ["ConnectionStrings:RabbitMq"] = stack.RabbitMqConnectionString,
            ["ReverseProxy:Clusters:order:Destinations:primary:Address"] = orderBaseAddress,
            ["ReverseProxy:Clusters:payment:Destinations:primary:Address"] = paymentBaseAddress,
            ["ReverseProxy:Clusters:tracking:Destinations:primary:Address"] = trackingBaseAddress,
        })
    {
    }
}
