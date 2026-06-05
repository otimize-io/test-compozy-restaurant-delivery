using Serilog.Core;
using Serilog.Events;

namespace Platform.Tests;

/// <summary>A Serilog sink that captures emitted events for assertions.</summary>
internal sealed class CapturingSink : ILogEventSink
{
    private readonly List<LogEvent> _events = [];

    public IReadOnlyList<LogEvent> Events
    {
        get { lock (_events) return _events.ToArray(); }
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_events)
        {
            _events.Add(logEvent);
        }
    }
}
