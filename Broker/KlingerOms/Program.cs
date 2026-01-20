using KlingerBroker.MarketData;
using KlingerBroker.Oms;
using KlingerBroker.Oms.Service;
using KlingerBroker.WebSocket;
using Serilog;
using Serilog.Events;

namespace KlingerBroker;

internal static class Program
{
    static void Main()
    {
        AddSerilogConfiguration();

        Log.Information("Starting Klinger OMS...");

        try
        {
            // Create components
            var marketData = new MarketDataService();
            var router = new FixOrderRouter();
            var initiator = new FixInitiator(router);

            // Create WebSocket server
            var wsServer = new WebSocketServer("http://localhost:8080/");
            var bridge = new OmsWebSocketBridge(wsServer, router, marketData);

            // Start services
            initiator.Start();
            wsServer.Start();

            Log.Information("OMS started successfully");
            Log.Information("WebSocket server listening on ws://localhost:8080/");
            Log.Information("Press Ctrl+C to stop...");

            // Keep running until Ctrl+C
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                exitEvent.Set();
            };

            exitEvent.WaitOne();

            Log.Information("Shutting down OMS...");

            // Cleanup
            bridge.Dispose();
            wsServer.Dispose();
            initiator.Dispose();
            marketData.Dispose();

            Log.Information("OMS stopped");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error in OMS");
            Environment.Exit(1);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void AddSerilogConfiguration()
    {
        var baseDirectory = AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(baseDirectory);

        var logDirectory = @"D:\Logs\Broker";
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "KlingerOms")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logDirectory, "klingeroms-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Serilog configured; logs at {LogDirectory}", logDirectory);
    }
}