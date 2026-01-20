using KlingerClient.Services;
using KlingerBroker.Ui.Docking;
using Serilog;
using WeifenLuo.WinFormsUI.Docking;

namespace KlingerClient.Docking.VerticalBook;

/// <summary>
/// Dock container for the Vertical Book UserControl
/// </summary>
public sealed class VerticalBookDock : DockContent
{
    private readonly ILogger _logger = Log.ForContext<VerticalBookDock>();
    private readonly VerticalBookControl _control;
    private readonly VerticalBookService _service;
    private readonly int _id;

    public VerticalBookDock(MarketDataService marketData, IOrderRouter router, int id = 0)
    {
        _id = id;
        _service = new VerticalBookService(marketData);
        _control = new VerticalBookControl(_service, router);

        Name = $"{DockContentIds.VerticalBook}_{_id}";
        Text = _id > 0 ? $"Vertical Book #{_id}" : "Vertical Book (DOM)";
        HideOnClose = true;

        _control.Dock = DockStyle.Fill;
        Controls.Add(_control);

        _logger.Information("VerticalBookDock {Id} initialized", _id);

        // Subscribe to symbols initialization
        _service.SymbolsInitialized += (_, _) =>
        {
            _logger.Information("VerticalBookDock {Id} symbols initialized, refreshing control", _id);
            _control.RefreshSymbols();
        };

        // Update title with message count
        _service.BookUpdated += (_, _) =>
        {
            Text = $"Vertical Book (DOM) - {_service.MessagesReceived:N0} updates";
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

    protected override string GetPersistString() => $"{DockContentIds.VerticalBook}:{_id}";
}
