using Microsoft.AspNetCore.SignalR;
using Serilog.Core;
using Serilog.Events;

namespace Wcs.Bs.Hubs;

public class SerilogSignalRSink : ILogEventSink
{
    private readonly IServiceProvider _serviceProvider;

    public SerilogSignalRSink(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<WcsHub>>();

            var category = "System";
            if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
            {
                var src = sourceContext.ToString().Trim('"');
                if (src.Contains("Plc")) category = "Plc";
                else if (src.Contains("Task")) category = "Task";
                else if (src.Contains("Device")) category = "Device";
                else if (src.Contains("Controller")) category = "Api";
            }

            var logEntry = new
            {
                timestamp = logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                level = logEvent.Level.ToString(),
                category,
                message = logEvent.RenderMessage(),
                exception = logEvent.Exception?.Message
            };

            hubContext.Clients.Group("view:logs")
                .SendAsync("LogReceived", logEntry)
                .ConfigureAwait(false);
        }
        catch
        {
            // Ignore SignalR push errors
        }
    }
}
