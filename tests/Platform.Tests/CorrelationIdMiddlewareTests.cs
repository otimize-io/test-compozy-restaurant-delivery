using Microsoft.AspNetCore.Http;
using RestaurantDelivery.Platform;
using Serilog;

namespace Platform.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Propagates_incoming_header_to_response_and_log_context()
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        var previous = Log.Logger;
        Log.Logger = logger;
        try
        {
            var context = new DefaultHttpContext();
            context.Request.Headers[CorrelationContext.HeaderName] = "abc-123";
            var middleware = new CorrelationIdMiddleware();

            await middleware.InvokeAsync(context, _ =>
            {
                Log.Information("inside the request");
                return Task.CompletedTask;
            });

            Assert.Equal("abc-123", context.Response.Headers[CorrelationContext.HeaderName].ToString());
            Assert.Contains(sink.Events, e =>
                e.Properties.TryGetValue(CorrelationContext.LogPropertyName, out var p)
                && p.ToString().Contains("abc-123"));
        }
        finally
        {
            Log.Logger = previous;
            logger.Dispose();
        }
    }

    [Fact]
    public async Task Generates_a_guid_when_header_is_absent()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        var header = context.Response.Headers[CorrelationContext.HeaderName].ToString();
        Assert.True(Guid.TryParse(header, out _));
        Assert.Equal(header, context.Items[CorrelationContext.LogPropertyName]);
    }
}
