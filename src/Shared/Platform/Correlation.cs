using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace RestaurantDelivery.Platform;

/// <summary>Names used to carry the correlation id across HTTP, messages, and logs (ADR-004).</summary>
public static class CorrelationContext
{
    public const string HeaderName = "X-Correlation-ID";
    public const string LogPropertyName = "CorrelationId";
}

/// <summary>
/// Reads (or generates) a correlation id per request, echoes it on the response, stores it on the
/// request, and pushes it onto the Serilog LogContext so every log line in the request is tagged.
/// Register with <c>AddPlatformCore</c> and enable with <c>UsePlatform</c>.
/// </summary>
public sealed class CorrelationIdMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[CorrelationContext.LogPropertyName] = correlationId;
        context.Response.Headers[CorrelationContext.HeaderName] = correlationId;

        using (LogContext.PushProperty(CorrelationContext.LogPropertyName, correlationId))
        {
            await next(context);
        }
    }

    /// <summary>Returns the incoming correlation id, or a new GUID when none was supplied.</summary>
    public static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationContext.HeaderName, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
