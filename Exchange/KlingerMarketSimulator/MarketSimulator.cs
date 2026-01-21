using QuickFix;
using QuickFix.Store;
using QuickFix.Logger;
using QuickFix.Transport;
using Serilog;
using KlingerSimulator.UI;

namespace KlingerSimulator;

public class MarketSimulator {
    private readonly ILogger _log = Log.ForContext<MarketSimulator>();
    private SocketInitiator? _initiator;
    private MarketSimulatorApplication? _application;
    private MarketDataGenerator? _generator;
    private MarketDataListener? _marketDataListener;
    private InteractiveConsole? _console;

    public void Start() {
        _log.Information("Starting Market Data Listener");
        _marketDataListener = new MarketDataListener();

        _log.Information("Initializing FIX connection");

        // Try to detect if Exchange is running in Docker
        var settingsFile = "manifest/settings.cfg";
        var exchangeHost = "127.0.0.1";
        var fixPort = 9824;

        // Check if we can reach the API on localhost (Docker)
        try {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);
            var response = httpClient.GetAsync("http://localhost:5000/health").Result;
            if (response.IsSuccessStatusCode) {
                _log.Information("Detected Exchange running on localhost:5000 (Docker mode)");
                exchangeHost = "localhost";
            }
        } catch {
            _log.Information("Using localhost connection (127.0.0.1)");
        }

        // Test FIX port connectivity
        try {
            using var tcpClient = new System.Net.Sockets.TcpClient();
            var connectTask = tcpClient.ConnectAsync(exchangeHost, fixPort);
            if (connectTask.Wait(TimeSpan.FromSeconds(2)) && tcpClient.Connected) {
                _log.Information($"FIX port {fixPort} is reachable on {exchangeHost}");
                tcpClient.Close();
            } else {
                _log.Warning($"FIX port {fixPort} is NOT reachable on {exchangeHost}");
                _log.Warning("Make sure the Exchange is running: .\\start-docker.ps1");
            }
        } catch (Exception ex) {
            _log.Warning($"Cannot reach FIX port {fixPort}: {ex.Message}");
            _log.Warning("Make sure the Exchange is running: .\\start-docker.ps1");
        }

        var settings = new SessionSettings(settingsFile);
        var storeFactory = new FileStoreFactory(settings);
        var logFactory = new NullLogFactory();

        _application = new MarketSimulatorApplication();
        _initiator = new SocketInitiator(_application, storeFactory, settings, logFactory);

        _log.Information("Starting FIX initiator - connecting to {Host}:{Port}", exchangeHost, fixPort);
        _initiator.Start();

        // Wait for logon
        Thread.Sleep(3000);

        if (!_application.IsConnected) {
            _log.Warning("⚠️  FIX connection not established after 3 seconds");
            _log.Warning("Check if Exchange is accepting connections on port {Port}", fixPort);
        } else {
            _log.Information("✅ FIX connection established successfully");
        }

        _log.Information("Starting market data generator with live price feeds");
        _generator = new MarketDataGenerator(_application, _marketDataListener);

        // Start interactive console
        _console = new InteractiveConsole(this);
        _console.Start();

        _generator.Start(_console);
    }

    public void Stop() {
        _console?.Stop();
        _generator?.Stop();
        _initiator?.Stop();
        _marketDataListener?.Dispose();
        _log.Information("Market Simulator stopped");
    }
}
