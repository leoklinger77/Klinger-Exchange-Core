
using KlingerExchange.Config;
using KlingerShared.Config;
using Scalar.AspNetCore;

namespace KlingerApi;

public class Program {
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

        // Health check endpoint
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "KlingerApi" }))
            .WithName("HealthCheck")
            .WithDescription("Health check endpoint")
            .ExcludeFromDescription();

        // Endpoint to get instruments list (basic)
        app.MapGet("/instruments", () => {
            var instrumentsConfig = ConfigBase<InstrumentsConfig>.LoadConfig();
            var sessionConfig = ConfigBase<TradingSessionConfig>.LoadConfig();
            
            var instrumentsWithPrices = instrumentsConfig.Instruments.Select(i => {
                var session = sessionConfig.Instruments.FirstOrDefault(s => s.SymbolIndex == i.SymbolIndex);
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
                    PreviousClose = session?.PreviousClose ?? 0m
                };
            }).ToArray();
            
            return Results.Json(new { instruments = instrumentsWithPrices, channels = instrumentsConfig.Channels });
        })
        .WithName("GetInstruments")
        .WithDescription("Get all trading instruments with reference prices")
        .WithSummary("Returns the complete list of available instruments with their trading parameters and reference prices");

        var port = app.Configuration["ASPNETCORE_HTTP_PORTS"] ?? "5000";
        app.Logger.LogInformation($"KlingerApi starting on http://0.0.0.0:{port}");
        app.Logger.LogInformation($"Swagger UI available at http://localhost:{port}/scalar/v1");
        app.Run($"http://0.0.0.0:{port}");
    }
}
