using KlingerSimulator;
using Serilog;

var logDirectory = @"D:\Logs\MarketSimulator";
Directory.CreateDirectory(logDirectory);
Thread.Sleep(TimeSpan.FromSeconds(2));
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
    .WriteTo.File(Path.Combine(logDirectory, "market-simulator-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
    .Enrich.WithProperty("Application", "MarketSimulator")
    .CreateLogger();

Log.Information("Starting Market Simulator");

try {
    var simulator = new MarketSimulator();
    simulator.Start();

    // Wait for console to signal quit
    while (true) {
        Thread.Sleep(100);
    }
} catch (Exception ex) {
    Log.Fatal(ex, "Market Simulator crashed");
} finally {
    Log.CloseAndFlush();
}
