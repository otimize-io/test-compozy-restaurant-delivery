using Microsoft.Extensions.Options;

namespace RestaurantDelivery.Dispatch.Drivers;

/// <summary>Options controlling driver seeding (bound from the <c>Dispatch</c> configuration section).</summary>
public sealed class DispatchOptions
{
    public const string SectionName = "Dispatch";

    /// <summary>
    /// When <c>true</c> (default), seed drivers into Redis at startup. Set to <c>false</c> to leave the
    /// store empty and deterministically exercise the "no driver available" compensation path (task_09).
    /// </summary>
    public bool SeedDrivers { get; init; } = true;
}

/// <summary>
/// Seeds <see cref="DriverSeedData.Drivers"/> into the <see cref="IDriverStore"/> at startup when
/// <see cref="DispatchOptions.SeedDrivers"/> is enabled (ADR-006: seeded availability).
/// </summary>
public sealed class DriverSeeder(IDriverStore store, IOptions<DispatchOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SeedDrivers)
        {
            return;
        }

        foreach (var driver in DriverSeedData.Drivers)
        {
            await store.UpsertAsync(driver, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
