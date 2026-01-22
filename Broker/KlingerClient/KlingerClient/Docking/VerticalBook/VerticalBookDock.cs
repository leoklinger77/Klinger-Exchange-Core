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
        
        // Set fixed width but variable height when floating
        DockAreas = DockAreas.Float | DockAreas.Document | DockAreas.DockLeft | DockAreas.DockRight | DockAreas.DockTop | DockAreas.DockBottom;
        FloatPane = null;

        _control.Dock = DockStyle.Fill;
        Controls.Add(_control);
        
        // Set the form to have fixed width but variable height when floating
        this.DockStateChanged += (s, e) =>
        {
            if (this.DockState == DockState.Float)
            {
                if (this.FloatPane?.FloatWindow != null)
                {
                    var window = this.FloatPane.FloatWindow;
                    window.ClientSize = new Size(500, 803);
                    window.MinimumSize = new Size(500, 400);
                    window.MaximumSize = new Size(500, 2000);
                    window.FormBorderStyle = FormBorderStyle.Sizable;
                    window.MaximizeBox = false;
                }
            }
        };

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
