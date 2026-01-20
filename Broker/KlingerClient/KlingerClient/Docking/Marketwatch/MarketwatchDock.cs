using KlingerClient.Services;
using Serilog;
using WeifenLuo.WinFormsUI.Docking;

namespace KlingerClient.Docking.Marketwatch;

/// <summary>
/// Dock container for the Marketwatch UserControl
/// </summary>
public sealed class MarketwatchDock : DockContent
{
    private readonly ILogger _logger = Log.ForContext<MarketwatchDock>();
    private readonly MarketwatchControl _control;
    private readonly MarketwatchService _service;
    private readonly int _id;

    public MarketwatchDock(MarketDataService marketData, int id = 0)
    {
        _id = id;
        _service = new MarketwatchService(marketData);
        _control = new MarketwatchControl(_service);

        Name = $"{DockContentIds.Marketwatch}_{_id}";
        Text = _id > 0 ? $"Marketwatch #{_id}" : "Marketwatch";
        HideOnClose = true;

        _control.Dock = DockStyle.Fill;
        Controls.Add(_control);

        _logger.Information("MarketwatchDock {Id} initialized", _id);

        // Subscribe to symbols initialization
        _service.SymbolsInitialized += (_, _) =>
        {
            _logger.Information("MarketwatchDock {Id} symbols initialized", _id);
            _control.RefreshSymbols();
        };

        // Update title with count
        _service.QuotesUpdated += (_, _) =>
        {
            var count = _service.WatchedQuotes.Count;
            Text = $"Marketwatch - {count} symbols";
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

    protected override string GetPersistString() => $"{DockContentIds.Marketwatch}:{_id}";
}

