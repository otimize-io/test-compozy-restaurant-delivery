using System.Collections.Concurrent;
using MassTransit;

namespace Catalog.Tests;

/// <summary>
/// Minimal recording <see cref="IPublishEndpoint"/> for unit tests: captures messages published via the
/// generic <c>Publish&lt;T&gt;(T, CancellationToken)</c> overload the seeder uses, so a test can assert
/// exactly what was published without a broker. The broker-backed publish path is covered separately by
/// the MassTransit in-memory harness integration test. Overloads the seeder does not use throw.
/// </summary>
public sealed class RecordingPublishEndpoint : IPublishEndpoint
{
    private readonly ConcurrentQueue<object> _published = new();

    public IReadOnlyCollection<object> Published => _published.ToArray();

    public Task Publish<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        _published.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default)
        where T : class => Publish(message, cancellationToken);

    public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
        where T : class => Publish(message, cancellationToken);

    public Task Publish(object message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish<T>(object values, CancellationToken cancellationToken = default)
        where T : class => throw new NotSupportedException();

    public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default)
        where T : class => throw new NotSupportedException();

    public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
        where T : class => throw new NotSupportedException();

    public ConnectHandle ConnectPublishObserver(IPublishObserver observer) =>
        throw new NotSupportedException();
}
