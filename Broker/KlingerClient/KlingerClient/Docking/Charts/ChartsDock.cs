using KlingerClient.Services;
using Serilog;
using WeifenLuo.WinFormsUI.Docking;

namespace KlingerClient.Docking.Charts;

/// <summary>
/// Dock container for the Charts UserControl
/// High-performance real-time charting
/// </summary>
public sealed class ChartsDock : DockContent
{
    private readonly ILogger _logger = Log.ForContext<ChartsDock>();
    private readonly ChartsControl _control;
    private readonly ChartsService _service;
    private readonly int _id;

    public ChartsDock(MarketDataService marketData, int id = 0)
    {
        _id = id;
        _service = new ChartsService(marketData);
        _control = new ChartsControl(_service);

        Name = $"{DockContentIds.Charts}_{_id}";
        Text = _id > 0 ? $"Chart #{_id}" : "Chart";
        HideOnClose = true;
        
        // Set minimum size when floating - allow expansion in all directions
        DockAreas = DockAreas.Float | DockAreas.Document | DockAreas.DockLeft | DockAreas.DockRight | DockAreas.DockTop | DockAreas.DockBottom;
        FloatPane = null;

        BackColor = Color.FromArgb(22, 22, 26);
        _control.Dock = DockStyle.Fill;
        Controls.Add(_control);
        
        // Set the form to allow free resizing with minimum size when floating
        this.DockStateChanged += (s, e) =>
        {
            if (this.DockState == DockState.Float)
            {
                if (this.FloatPane?.FloatWindow != null)
                {
                    var window = this.FloatPane.FloatWindow;
                    window.ClientSize = new Size(800, 500);
                    window.MinimumSize = new Size(800, 400);
                    window.FormBorderStyle = FormBorderStyle.Sizable;
                    window.MaximizeBox = true;
                }
            }
        };

        _logger.Information("ChartsDock {Id} initialized", _id);

        // Subscribe to service events for title update
        _service.NewCandleCreated += (_, e) =>
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => UpdateTitle());
            }
            else
            {
                UpdateTitle();
            }
        };
    }

    private void UpdateTitle()
    {
        var symbol = _service.SymbolNames.TryGetValue(_service.CurrentSymbol, out var s) ? s : "---";
        var tf = _service.CurrentTimeframe.ToString();
        Text = $"Chart - {symbol} ({tf})";
    }

    public void RefreshSymbols()
    {
        _control.RefreshSymbols();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _service?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override string GetPersistString() => $"{DockContentIds.Charts}:{_id}";
}
