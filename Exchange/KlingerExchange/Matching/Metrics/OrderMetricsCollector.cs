using KlingerExchange.Matching.Domain.Events;
using KlingerExchange.Matching.Engine.Latency;
using System.Diagnostics;

namespace KlingerExchange.Matching.Metrics;

public sealed class OrderMetricsCollector {
    private readonly MetricsEventBus _eventBus;
    private readonly Stopwatch _totalTimer;

    // Captured data
    private string _msgType = string.Empty;
    private string _clOrdId = string.Empty;
    private string _symbol = string.Empty;
    private string _side = string.Empty;
    private decimal _quantity;
    private decimal _price;
    private long _orderId;
    private int _fillCount;

    // Timing breakdown
    private long _parseNs;
    private long _matchNs;
    private long _reportNs;
    private long _sendNs;

    public OrderMetricsCollector(MetricsEventBus eventBus) {
        _eventBus = eventBus;
        _totalTimer = new Stopwatch();
    }

    public void StartOrder(string msgType) {
        _msgType = msgType;
        _totalTimer.Restart();
    }

    public void CaptureOrderData(string clOrdId, string symbol, string side, decimal quantity, decimal price, long orderId) {
        _clOrdId = clOrdId;
        _symbol = symbol;
        _side = side;
        _quantity = quantity;
        _price = price;
        _orderId = orderId;
    }

    public void RecordTiming(long parseNs, long matchNs, long reportNs, long sendNs, int fillCount) {
        _parseNs = parseNs;
        _matchNs = matchNs;
        _reportNs = reportNs;
        _sendNs = sendNs;
        _fillCount = fillCount;
    }

    public void PublishMetrics() {
        _totalTimer.Stop();
        var totalNs = TicksToNs(_totalTimer.ElapsedTicks);

        _eventBus.Events.OnNext(new OrderMetricsEvent {
            MsgType = _msgType,
            ClOrdId = _clOrdId,
            Symbol = _symbol,
            Side = _side,
            Quantity = _quantity,
            Price = _price,
            OrderId = _orderId,
            FillCount = _fillCount,
            Metrics = new DetailedLatencyMetrics {
                ParseTimeNs = _parseNs,
                MatchTimeNs = _matchNs,
                ReportBuildTimeNs = _reportNs,
                SendTimeNs = _sendNs,
                TotalTimeNs = totalNs,
                MsgType = _msgType,
                FillCount = _fillCount
            }
        });
    }

    private static long TicksToNs(long ticks) {
        return (long)(ticks * (1_000_000_000.0 / Stopwatch.Frequency));
    }
}
