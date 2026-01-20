using KlingerExchange.Matching.Engine;

namespace KlingerExchange.Matching.Events;

public sealed class OrderMetricsEvent
{
    public string MsgType { get; init; } = string.Empty;
    public string ClOrdId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public long OrderId { get; init; }
    public int FillCount { get; init; }
    public DetailedLatencyMetrics Metrics { get; init; } = default!;
}
