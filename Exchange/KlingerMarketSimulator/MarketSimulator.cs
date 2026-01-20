using QuickFix;
using QuickFix.Store;
using QuickFix.Logger;
using QuickFix.Transport;
using Serilog;

namespace KlingerSimulator;

public class MarketSimulator
{
    private readonly ILogger _log = Log.ForContext<MarketSimulator>();
    private SocketInitiator? _initiator;
    private MarketSimulatorApplication? _application;
    private MarketDataGenerator? _generator;
    private MarketDataListener? _marketDataListener;

    public void Start()
    {
        _log.Information("Starting Market Data Listener");
        _marketDataListener = new MarketDataListener();
        
        _log.Information("Initializing FIX connection");
        
        var settings = new SessionSettings("manifest/settings.cfg");
        var storeFactory = new MemoryStoreFactory();
        var logFactory = new NullLogFactory();
        
        _application = new MarketSimulatorApplication();
        _initiator = new SocketInitiator(_application, storeFactory, settings, logFactory);
        
        _log.Information("Starting FIX initiator");
        _initiator.Start();
        
        // Wait for logon
        Thread.Sleep(2000);
        
        _log.Information("Starting market data generator with live price feeds");
        _generator = new MarketDataGenerator(_application, _marketDataListener);
        _generator.Start();
    }

    public void Stop()
    {
        _generator?.Stop();
        _initiator?.Stop();
        _marketDataListener?.Dispose();
        _log.Information("Market Simulator stopped");
    }
}
