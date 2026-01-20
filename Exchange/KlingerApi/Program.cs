
using KlingerExchange.Config;
using KlingerShared.Config;
using Scalar.AspNetCore;

namespace KlingerApi;

public class Program {
    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        // Add services        
        builder.Services.AddOpenApi();
        builder.Services.AddCors(options => {
            options.AddDefaultPolicy(policy => {
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment()) {
            app.MapOpenApi();
            app.MapScalarApiReference(); // Swagger UI at /scalar/v1
        }

        app.UseCors();

        // Endpoint to get instruments list (basic)
        app.MapGet("/instruments", () => {
            var instrumentsConfig = ConfigBase<InstrumentsConfig>.LoadConfig();
            var sessionConfig = ConfigBase<TradingSessionConfig>.LoadConfig();
            
            // Merge instruments with session data to include reference prices
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

        app.Logger.LogInformation("KlingerApi starting on http://0.0.0.0:5000");
        app.Logger.LogInformation("Swagger UI available at http://localhost:5000/scalar/v1");
        app.Run("http://0.0.0.0:5000");
    }
}
