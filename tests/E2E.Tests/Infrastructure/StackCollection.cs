namespace E2E.Tests.Infrastructure;

/// <summary>
/// xUnit collection that shares one <see cref="StackFixture"/> (the containers) across the integration tests
/// that need the composed stack, so the (slow) RabbitMQ/Postgres/Redis containers start once for the whole
/// E2E suite rather than per test class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class StackCollection : ICollectionFixture<StackFixture>
{
    public const string Name = "composed-stack";
}
