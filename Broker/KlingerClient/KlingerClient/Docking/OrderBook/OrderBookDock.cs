using KlingerClient.Services;
using KlingerBroker.Ui.Docking.OrderBook;
using KlingerClient.Docking;
using Serilog;
using WeifenLuo.WinFormsUI.Docking;

namespace KlingerClient.Docking.OrderBook;

public sealed class OrderBookDock : DockContent
{
    private readonly ILogger _logger = Log.ForContext<OrderBookDock>();
    private readonly OrderBookControl _control;
    private readonly OrderBookService _service;
    private readonly int _id;
    private readonly SynchronizationContext? _syncContext;

    public OrderBookDock(MarketDataService marketData, int id = 0)
    {
        _id = id;
        _syncContext = SynchronizationContext.Current;
        _service = new OrderBookService(marketData);
        _control = new OrderBookControl(_service);

        Name = $"{DockContentIds.OrderBook}_{_id}";
        Text = _id > 0 ? $"Order Book #{_id}" : "Order Book";
        HideOnClose = true;

        _control.Dock = DockStyle.Fill;
        Controls.Add(_control);

        _logger.Information("OrderBookDock {Id} initialized", _id);

        // Subscribe to symbols initialization
        _service.SymbolsInitialized += (_, _) =>
        {
            _logger.Information("OrderBookDock {Id} symbols initialized, refreshing control", _id);
            _control.RefreshSymbols();
        };

        // Update title with message count
        _service.TradesUpdated += (_, _) =>
        {
            var currentSymbol = _service.CurrentSymbol;
            var symbolName = _service.SymbolNames.TryGetValue(currentSymbol, out var name)
                ? name
                : $"#{currentSymbol}";
            var symbolTradeCount = _service.GetTradeCount(currentSymbol);
            var prefix = _id > 0 ? $"Order Book #{_id}" : "Order Book";
            var newText = $"{prefix} - {_service.MessagesReceived:N0} trades - {symbolName} ({symbolTradeCount})";

            if (_syncContext != null)
                _syncContext.Post(_ => Text = newText, null);
            else
                Text = newText;
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _service?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override string GetPersistString() => $"{DockContentIds.OrderBook}:{_id}";
}
