using KlingerExchange.Config;
using KlingerExchange.MarketData.Core;
using KlingerExchange.MarketData.Publisher;
using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Engine.Instrument;
using KlingerShared.Config;
using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;
using Serilog;

namespace KlingerExchange {
    internal class ExchangeAcceptors {

        public void Initialize() {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            // Load configuration files
            Log.Information("Loading configuration files...");
            var instrumentsConfig = InstrumentsConfig.LoadConfig();
            var tradingSessionConfig = TradingSessionConfig.LoadConfig();

            // Initialize static stores
            InstrumentMetadataStore.Initialize(instrumentsConfig);
            SessionStore.Initialize(tradingSessionConfig);
            Log.Information("Loaded {InstrumentCount} instruments from configuration", instrumentsConfig.Instruments.Length);

            var settingsPath = Path.Combine(AppContext.BaseDirectory, "Matching", "manifest", "settings.cfg");
            if (!File.Exists(settingsPath))
                throw new FileNotFoundException($"FIX settings.cfg not found at: {settingsPath}");

            SessionSettings settings = new SessionSettings(settingsPath);
            
            // Initialize market data infrastructure
            Log.Information("Initializing ultra-low latency market data system...");
            var symbolMapper = new SymbolMapper();
            symbolMapper.Initialize(instrumentsConfig.Instruments.Select(i => (i.Symbol, i.SymbolIndex)));
            var ringBuffer = new RingBuffer(65536); // 64K messages = 4MB
            var udpPublisher = new UdpMulticastPublisher(ringBuffer);
            
            // Create OMS with market data injection
            var myApp = new ExchangeApplication();
            myApp.InjectMarketDataPublisher(ringBuffer, symbolMapper);
            
            // Initialize validation AFTER stores are loaded
            myApp.InitializeValidation();
            
            IMessageStoreFactory storeFactory = new MemoryStoreFactory();
            ILogFactory logFactory = new NullLogFactory();
            ThreadedSocketAcceptor acceptor = new(
                myApp,
                storeFactory,
                settings,
                logFactory);

            Log.Information("Starting FIX acceptor");
            acceptor.Start();
            
            // Start market data publisher thread
            udpPublisher.Start();
            Log.Information("Market data publisher started on {Address}:{Port}", 
                UdpMulticastPublisher.MulticastAddress, UdpMulticastPublisher.MulticastPort);
            Log.Information("Instrument list available via KlingerApi REST endpoint - UDP dedicated to market data only");

            var stopping = false;
            
            // Handle Ctrl+C
            Console.CancelKeyPress += (_, e) => {
                e.Cancel = true;
                stopping = true;
                Log.Information("Ctrl+C received, shutting down gracefully...");
            };
            
            // Handle Visual Studio Stop / SIGTERM
            AppDomain.CurrentDomain.ProcessExit += (_, _) => {
                if (!stopping) {
                    stopping = true;
                    Log.Information("ProcessExit received (Visual Studio Stop?), flushing EventStore...");
                    myApp.Dispose();
                }
            };
            
            while (!stopping)
            {
                Thread.Sleep(5000);
                
                // Log statistics every 5 seconds
                var (available, capacity, isFull) = udpPublisher.GetStats();
                Log.Information("Market Data Stats: Buffer={Available}/{Capacity} Full={IsFull}", 
                    available, capacity, isFull);
            }

            Log.Information("Stopping market data publisher");
            udpPublisher.Stop();
            udpPublisher.Dispose();
            ringBuffer.Dispose();

            Log.Information("Stopping FIX acceptor");
            acceptor.Stop();
            
            Log.Information("Disposing ExchangeApplication (flush EventStore)");
            myApp.Dispose();
            
            Log.Information("Shutdown complete");
        }
    }
}
