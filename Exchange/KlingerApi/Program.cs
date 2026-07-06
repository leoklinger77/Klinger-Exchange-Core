
using System.Diagnostics;
using System.Reflection;
using KlingerExchange.Config;
using KlingerShared.Config;
using Scalar.AspNetCore;

namespace KlingerApi;

public class Program {
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();
        builder.Services.AddCors(options => {
            options.AddDefaultPolicy(policy => {
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment()) {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseCors();

        // ── Health ──────────────────────────────────────────────────
        app.MapGet("/health", () => {
            var process = Process.GetCurrentProcess();
            return Results.Json(new {
                Status = "Healthy",
                UptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds,
                TimestampUtc = DateTime.UtcNow,
                MemoryMb = process.WorkingSet64 / (1024 * 1024),
                Environment = app.Environment.EnvironmentName
            });
        })
        .WithName("HealthCheck")
        .WithDescription("Returns API health status and basic diagnostics")
        .WithSummary("Health check endpoint for monitoring and orchestration");

        // ── Instruments (all) ───────────────────────────────────────
        app.MapGet("/instruments", () => {
            var instrumentsConfig = ConfigBase<InstrumentsConfig>.LoadConfig();
            var sessionConfig = ConfigBase<TradingSessionConfig>.LoadConfig();
            
            var instrumentsWithPrices = instrumentsConfig.Instruments.Select(i => {
                var session = sessionConfig.Instruments.FirstOrDefault(s => s.SymbolIndex == i.SymbolIndex);
                return BuildInstrumentResponse(i, session);
            }).ToArray();
            
            return Results.Json(new { instruments = instrumentsWithPrices, channels = instrumentsConfig.Channels });
        })
        .WithName("GetInstruments")
        .WithDescription("Get all trading instruments with reference prices")
        .WithSummary("Returns the complete list of available instruments with their trading parameters and reference prices");

        // ── Instrument by symbol ────────────────────────────────────
        app.MapGet("/instruments/{symbol}", (string symbol) => {
            var instrumentsConfig = ConfigBase<InstrumentsConfig>.LoadConfig();
            var sessionConfig = ConfigBase<TradingSessionConfig>.LoadConfig();

            var instrument = instrumentsConfig.Instruments
                .FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

            if (instrument is null)
                return Results.NotFound(new { Error = $"Instrument '{symbol}' not found" });

            var session = sessionConfig.Instruments
                .FirstOrDefault(s => s.SymbolIndex == instrument.SymbolIndex);

            return Results.Json(BuildInstrumentResponse(instrument, session));
        })
        .WithName("GetInstrumentBySymbol")
        .WithDescription("Get a specific instrument by its symbol")
        .WithSummary("Returns instrument details and session data for a single symbol");

        // ── Brokers ─────────────────────────────────────────────────
        app.MapGet("/brokers", () => {
            var brokers = BrokerConfig.LoadConfig();
            return Results.Json(new { Brokers = brokers.Broker.Values.ToList() });
        })
        .WithName("GetBrokers")
        .WithDescription("Get all brokers")
        .WithSummary("Returns the complete list of available brokers.");

        // ── Trading session info ────────────────────────────────────
        app.MapGet("/session", () => {
            var sessionConfig = ConfigBase<TradingSessionConfig>.LoadConfig();
            return Results.Json(new {
                sessionConfig.TradingDate,
                sessionConfig.SessionPhase,
                InstrumentCount = sessionConfig.Instruments.Length,
                Instruments = sessionConfig.Instruments.Select(s => new {
                    s.Symbol,
                    s.Status,
                    s.ReferencePrice,
                    s.PreviousClose,
                    s.UpperLimit,
                    s.LowerLimit
                })
            });
        })
        .WithName("GetTradingSession")
        .WithDescription("Get current trading session configuration")
        .WithSummary("Returns trading date, session phase, and per-instrument session parameters including price limits");

        // ── Exchange configuration ──────────────────────────────────
        app.MapGet("/exchange", () => {
            var exchangeConfig = ConfigBase<ExchangeConfig>.LoadConfig();
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            return Results.Json(new {
                Name = "Klinger Exchange",
                Version = version,
                exchangeConfig.EnableEventStore,
                Protocol = "FIX 4.1",
                MatchingAlgorithm = "Price-Time Priority (FIFO)",
                FixPorts = new { Client = 9823, Simulator = 9824 },
                MarketData = new { Type = "UDP Multicast", Address = "239.1.1.100:9900" }
            });
        })
        .WithName("GetExchangeInfo")
        .WithDescription("Get exchange configuration and capabilities")
        .WithSummary("Returns exchange metadata including protocol version, matching algorithm, and connectivity details");

        var port = app.Configuration["ASPNETCORE_HTTP_PORTS"] ?? "5000";
        app.Logger.LogInformation($"KlingerApi starting on http://0.0.0.0:{port}");
        app.Logger.LogInformation($"Swagger UI available at http://localhost:{port}/scalar/v1");
        app.Run($"http://0.0.0.0:{port}");
    }

    private static object BuildInstrumentResponse(InstrumentDto i, InstrumentSessionDto? session) {
        return new {
            i.SymbolIndex,
            i.Symbol,
            i.Name,
            i.Channel,
            i.Sector,
            i.TickSize,
            i.LotSize,
            i.IsFractional,
            ReferencePrice = session?.ReferencePrice ?? 0m,
            PreviousClose = session?.PreviousClose ?? 0m,
            PreviousHigh = session?.PreviousHigh ?? 0m,
            PreviousLow = session?.PreviousLow ?? 0m,
            UpperLimit = session?.UpperLimit ?? 0m,
            LowerLimit = session?.LowerLimit ?? 0m,
            Status = session?.Status ?? "Unknown"
        };
    }
}
