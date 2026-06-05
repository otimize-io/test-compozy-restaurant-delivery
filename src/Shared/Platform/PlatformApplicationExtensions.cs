using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace RestaurantDelivery.Platform;

/// <summary>Host- and pipeline-level helpers shared by every microservice.</summary>
public static class PlatformApplicationExtensions
{
    /// <summary>Configures Serilog with LogContext enrichment (correlation id) and a console sink.</summary>
    public static TBuilder AddPlatformSerilog<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSerilog((_, loggerConfiguration) => loggerConfiguration
            .Enrich.FromLogContext()
            .WriteTo.Console());

        return builder;
    }

    /// <summary>
    /// Adds the correlation middleware to the request pipeline and maps the <c>/health</c> endpoint.
    /// Requires <c>AddPlatformCore</c> and <c>AddPlatformHealthChecks</c> at registration time.
    /// </summary>
    public static WebApplication UsePlatform(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.MapHealthChecks("/health");
        return app;
    }
}
