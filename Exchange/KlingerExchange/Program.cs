using Serilog;
using Serilog.Events;

namespace KlingerExchange;

internal class Program {
    static int Main(string[] args) {
        AddSerilogConfiguration();
        try {
            Log.Information("Starting OMS acceptor");

            var oms = new OmsAcceptors();
            oms.Initialize();

            Log.Information("OMS acceptor stopped");
            return 0;
        } catch (Exception ex) {
            Log.Fatal(ex, "Unhandled exception");
            return 1;
        } finally {
            Log.CloseAndFlush();
        }
    }

    public static void AddSerilogConfiguration() {
        var baseDirectory = AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(baseDirectory);

        var logDirectory = @"D:\Logs\Exchange";
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Information)  // Production: avoid Debug overhead
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "KlingerExchange")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logDirectory, "klingerexchange-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Serilog configured; logs at {LogDirectory}", logDirectory);
    }
}
