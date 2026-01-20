using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;
using Serilog;
using KlingerExchange.MarketData.Core;
using KlingerExchange.MarketData.Publisher;
using KlingerExchange.Config;
using KlingerExchange.Matching.Domain;
using KlingerShared.Config;
using System.Linq;

namespace KlingerExchange {
    internal class OmsAcceptors {

        public void Initialize() {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            // Load configuration files
            Log.Information("Loading configuration files...");
            var instrumentsConfig = ConfigBase<InstrumentsConfig>.LoadConfig();
            var tradingSessionConfig = ConfigBase<TradingSessionConfig>.LoadConfig();

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
            var myApp = new OmsApplication();
            myApp.InjectMarketDataPublisher(ringBuffer, symbolMapper);
            
            // Initialize validation AFTER stores are loaded
            myApp.InitializeValidation();
            
            IMessageStoreFactory storeFactory = new MemoryStoreFactory();
            ILogFactory logFactory = new NullLogFactory();
            ThreadedSocketAcceptor acceptor = new ThreadedSocketAcceptor(
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
            Console.CancelKeyPress += (_, e) => {
                e.Cancel = true;
                stopping = true;
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
        }
        
        private InstrumentInfo[] BuildInstrumentInfoList(InstrumentsConfig instrumentsConfig, TradingSessionConfig sessionConfig)
        {
            var instruments = new InstrumentInfo[instrumentsConfig.Instruments.Length];
            
            for (int i = 0; i < instrumentsConfig.Instruments.Length; i++)
            {
                var dto = instrumentsConfig.Instruments[i];
                var session = sessionConfig.Instruments.FirstOrDefault(s => s.SymbolIndex == dto.SymbolIndex);
                
                instruments[i] = new InstrumentInfo
                {
                    SymbolIndex = dto.SymbolIndex,
                    Channel = dto.Channel,
                    TickSizeFixed = (long)(dto.TickSize * 100_000m),
                    LotSize = dto.LotSize,
                    ReferencePriceFixed = session != null ? (long)(session.ReferencePrice * 100_000m) : 0,
                    PreviousCloseFixed = session != null ? (long)(session.PreviousClose * 100_000m) : 0,
                    Flags = (byte)(dto.IsFractional ? 1 : 0)
                };
                
                instruments[i].SetSymbol(dto.Symbol);
            }
            
            return instruments;
        }
    }
}
