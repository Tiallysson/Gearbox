using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Gearbox.Core.Logging;

public static class GearboxLogging
{
    /// <summary>Logger Serilog em arquivo rolling diário (<c>logs\gearbox-AAAAMMDD.log</c>), 14 dias de retenção.</summary>
    public static Logger CreateLogger(GearboxPaths paths, LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        Directory.CreateDirectory(paths.LogsDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "gearbox-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
