using System.Reactive.Linq;
using KlingerClient.Services;
using KlingerBroker.Ui.Docking.OrderEntry;
using Serilog;
using WeifenLuo.WinFormsUI.Docking;

namespace KlingerClient.Docking.OrderEntry;

/// <summary>
/// Dock container for Order Entry UserControl
/// </summary>
public sealed class OrderEntryDock : DockContent
{
    private readonly ILogger _logger = Log.ForContext<OrderEntryDock>();
    private readonly OrderEntryControl _control;
    private readonly IOrderRouter _router;
    private readonly MarketDataService _marketData;
    private readonly int _id;

    public OrderEntryDock(IOrderRouter router, MarketDataService marketData, int id = 0)
    {
        _id = id;
        _router = router;
        _marketData = marketData;
        _control = new OrderEntryControl();

        Name = $"{DockContentIds.OrderEntry}_{_id}";
        Text = _id > 0 ? $"Order Entry #{_id}" : "Order Entry";
        HideOnClose = true;

        _control.Dock = DockStyle.Fill;
        Controls.Add(_control);

        _control.SendClicked += OnSendClick;
        _logger.Information("OrderEntryDock {Id} subscribing to InstrumentListStream", _id);
        
        // Subscribe to instrument list
        _marketData.InstrumentListStream
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(instruments =>
            {
                _logger.Information("OrderEntryDock {Id} received {Count} instruments from stream", _id, instruments.Length);
                
                if (InvokeRequired)
                {
                    Invoke(() => LoadInstruments(instruments));
                }
                else
                {
                    LoadInstruments(instruments);
                }
            });
        
        // If already loaded, populate immediately
        if (_marketData.Instruments != null && _marketData.Instruments.Length > 0)
        {
            _logger.Information("OrderEntryDock {Id} loading {Count} instruments that were already cached", 
                _id, _marketData.Instruments.Length);
            LoadInstruments(_marketData.Instruments);
        }
        else
        {
            _logger.Information("OrderEntryDock {Id} no instruments cached yet, waiting for stream", _id);
        }
    }

    private void LoadInstruments(InstrumentInfo[] instruments)
    {
        var symbolNames = instruments
            .OrderBy(i => i.Symbol)
            .ToDictionary(i => i.SymbolIndex, i => i.Symbol);
        
        _logger.Information("OrderEntryDock {Id} loading {Count} symbols into selector: {Symbols}", 
            _id, symbolNames.Count, string.Join(", ", symbolNames.Values));
        
        _control.LoadSymbols(symbolNames);
    }

    private void OnSendClick(object? sender, EventArgs e)
    {
        var symbol = _control.SymbolSelector.SelectedSymbol;
        if (string.IsNullOrWhiteSpace(symbol))
        {
            MessageBox.Show(this, "Symbol is required.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_router.IsConnected)
        {
            MessageBox.Show(this, "Not connected to exchange.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var side = _control.SideCombo.SelectedIndex == 0 ? Side.Buy : Side.Sell;
        var qty = (int)_control.QuantityInput.Value;
        var price = _control.PriceInput.Value;

        try
        {
            var clOrdId = _router.SendNewOrder(new NewOrderRequest(symbol, side, qty, price));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to send order:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override string GetPersistString() => $"{DockContentIds.OrderEntry}:{_id}";
}
