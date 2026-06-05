using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace RestaurantDelivery.Platform;

/// <summary>Service-registration helpers shared by every microservice.</summary>
public static class PlatformServiceCollectionExtensions
{
    /// <summary>
    /// Registers MassTransit over RabbitMQ with immediate retry and automatic endpoint configuration.
    /// MassTransit also registers its bus health check, surfaced through <c>AddPlatformHealthChecks</c>.
    /// </summary>
    public static IServiceCollection AddPlatformMessaging(
        this IServiceCollection services,
        string rabbitConnectionString,
        Action<IBusRegistrationConfigurator>? configure = null)
    {
        services.AddMassTransit(x =>
        {
            configure?.Invoke(x);
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitConnectionString));
                cfg.UseMessageRetry(r => r.Immediate(3));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>Registers the health-check service (MassTransit adds its bus check automatically).</summary>
    public static IServiceCollection AddPlatformHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }

    /// <summary>Registers the correlation middleware and a default in-memory idempotency store.</summary>
    public static IServiceCollection AddPlatformCore(this IServiceCollection services)
    {
        services.AddTransient<CorrelationIdMiddleware>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        return services;
    }
}
