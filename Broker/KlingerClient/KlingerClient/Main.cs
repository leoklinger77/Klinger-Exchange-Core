using KlingerClient.Docking;
using KlingerClient.Docking.Charts;
using KlingerClient.Docking.Marketwatch;
using KlingerClient.Docking.MyOrders;
using KlingerClient.Docking.OrderBook;
using KlingerClient.Docking.OrderEntry;
using KlingerClient.Docking.VerticalBook;
using KlingerClient.Services;
using WeifenLuo.WinFormsUI.Docking;

namespace KlingerClient;

public partial class Main : Form
{
    private readonly WebSocket.WebSocketClient _wsClient;
    private readonly IOrderRouter _router;
    private readonly MarketDataService _marketData;

    private readonly VS2015DarkTheme _dockTheme = new();

    private readonly List<OrderEntryDock> _orderEntryDocks = new();
    private readonly List<OrderBookDock> _orderBookDocks = new();
    private readonly List<VerticalBookDock> _verticalBookDocks = new();
    private readonly List<MyOrdersDock> _MyOrdersrDocks = new();
    private readonly List<MarketwatchDock> _marketwatchDocks = new();
    private readonly List<ChartsDock> _chartsDocks = new();
    private readonly List<ExecutionReportEvent> _executionReportHistory = new();
    private int _nextDockId = 1;

    private readonly DeserializeDockContent _deserializeDockContent;
    private readonly string _layoutPath;

    public Main()
    {
        InitializeComponent();

        // Apply dark theme to main form
        this.BackColor = Color.FromArgb(37, 37, 38);
        this.ForeColor = Color.White;

        // Apply dark theme to menu strip
        if (_menuStrip != null)
        {
            _menuStrip.BackColor = Color.FromArgb(45, 45, 48);
            _menuStrip.ForeColor = Color.White;
            _menuStrip.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
        }

        // Create WebSocket client and services
        _wsClient = new WebSocket.WebSocketClient("ws://localhost:8080/");
        _router = new WebSocketOrderRouter(_wsClient, SynchronizationContext.Current);
        _marketData = new MarketDataService(_wsClient);
        
        _dockPanel.Theme = _dockTheme;

        _layoutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KlingerClient",
            "layout.xml");

        _deserializeDockContent = DeserializeDockContent;

        _viewOrderEntry.Click += (_, _) => ShowOrderEntry();
        _viewOrderBook.Click += (_, _) => ShowOrderBook();
        _viewVerticalBook.Click += (_, _) => ShowVerticalBook();
        _viewMyOrders.Click += (_, _) => ShowMyOrders();
        _viewMarketwatch.Click += (_, _) => ShowMarketwatch();
        _viewCharts.Click += (_, _) => ShowCharts();

        // Subscribe to router status
        _router.StatusChanged += (_, e) =>
        {
            Text = e.IsConnected ? "Klinger Trader [Connected]" : "Klinger Trader [Disconnected]";
        };

        _router.ExecutionReportReceived += (_, er) =>
        {
            _executionReportHistory.Add(er);
            if (_executionReportHistory.Count > 500)
                _executionReportHistory.RemoveAt(0);

            foreach (var dock in _MyOrdersrDocks)
                dock.AddExecutionReport(er);
        };
    }

    private OrderEntryDock CreateOrderEntryDock()
    {
        var dock = new OrderEntryDock(_router, _marketData, _nextDockId++);
        _orderEntryDocks.Add(dock);
        dock.FormClosed += (_, _) => _orderEntryDocks.Remove(dock);
        return dock;
    }

    private OrderBookDock CreateOrderBookDock()
    {
        var dock = new OrderBookDock(_marketData, _nextDockId++);
        _orderBookDocks.Add(dock);
        dock.FormClosed += (_, _) => _orderBookDocks.Remove(dock);
        return dock;
    }

    private VerticalBookDock CreateVerticalBookDock()
    {
        var dock = new VerticalBookDock(_marketData, _router, _nextDockId++);
        _verticalBookDocks.Add(dock);
        dock.FormClosed += (_, _) => _verticalBookDocks.Remove(dock);
        return dock;
    }

    private MyOrdersDock CreateMyOrdersDock()
    {
        var dock = new MyOrdersDock(_nextDockId++);
        _MyOrdersrDocks.Add(dock);
        dock.FormClosed += (_, _) => _MyOrdersrDocks.Remove(dock);

        foreach (var er in _executionReportHistory)
            dock.AddExecutionReport(er);

        return dock;
    }

    private MarketwatchDock CreateMarketwatchDock()
    {
        var dock = new MarketwatchDock(_marketData, _nextDockId++);
        _marketwatchDocks.Add(dock);
        dock.FormClosed += (_, _) => _marketwatchDocks.Remove(dock);
        return dock;
    }

    private ChartsDock CreateChartsDock()
    {
        var dock = new ChartsDock(_marketData, _nextDockId++);
        _chartsDocks.Add(dock);
        dock.FormClosed += (_, _) => _chartsDocks.Remove(dock);
        return dock;
    }

    private IDockContent? DeserializeDockContent(string persistString)
    {
        // Parse format: "Type:Id" or just "Type" for backward compatibility
        var parts = persistString.Split(':');
        var type = parts[0];

        return type switch
        {
            DockContentIds.OrderEntry => CreateOrderEntryDock(),
            DockContentIds.OrderBook => CreateOrderBookDock(),
            DockContentIds.VerticalBook => CreateVerticalBookDock(),
            DockContentIds.MyOrders => CreateMyOrdersDock(),
            DockContentIds.Marketwatch => CreateMarketwatchDock(),
            DockContentIds.Charts => CreateChartsDock(),
            _ => null
        };
    }

    private void EnsureDefaultLayout()
    {
        CreateOrderEntryDock().Show(_dockPanel, DockState.DockLeft);
        CreateOrderBookDock().Show(_dockPanel, DockState.DockRight);
        CreateMyOrdersDock().Show(_dockPanel, DockState.DockBottomAutoHide);
    }

    private void ShowOrderEntry() => CreateOrderEntryDock().Show(_dockPanel);
    private void ShowOrderBook() => CreateOrderBookDock().Show(_dockPanel);
    private void ShowVerticalBook() => CreateVerticalBookDock().Show(_dockPanel);
    private void ShowMyOrders() => CreateMyOrdersDock().Show(_dockPanel);
    private void ShowMarketwatch() => CreateMarketwatchDock().Show(_dockPanel);
    private void ShowCharts() => CreateChartsDock().Show(_dockPanel);

    private void LoadLayoutOrDefault()
    {
        try
        {
            var layoutDir = Path.GetDirectoryName(_layoutPath);
            if (!string.IsNullOrWhiteSpace(layoutDir))
                Directory.CreateDirectory(layoutDir);

            if (File.Exists(_layoutPath))
            {
                _dockPanel.LoadFromXml(_layoutPath, _deserializeDockContent);
                return;
            }
        }
        catch
        {
            // If layout is corrupted, fall back to defaults.
        }

        EnsureDefaultLayout();
    }

    private void SaveLayout()
    {
        try
        {
            var layoutDir = Path.GetDirectoryName(_layoutPath);
            if (!string.IsNullOrWhiteSpace(layoutDir))
                Directory.CreateDirectory(layoutDir);

            _dockPanel.SaveAsXml(_layoutPath);
        }
        catch
        {
            // ignore on shutdown
        }
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            await _wsClient.ConnectAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "WebSocket Connection", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        try
        {
            LoadLayoutOrDefault();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Layout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            EnsureDefaultLayout();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveLayout();
        _wsClient.DisconnectAsync().Wait(TimeSpan.FromSeconds(2));
        _wsClient.Dispose();
        _marketData.Dispose();
        (_router as IDisposable)?.Dispose();

        _dockTheme.Dispose();
        base.OnFormClosing(e);
    }
}

// Dark color table for menu strip
internal class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => Color.FromArgb(62, 62, 64);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(62, 62, 64);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(62, 62, 64);
    public override Color MenuItemBorder => Color.FromArgb(62, 62, 64);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(0, 122, 204);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(0, 122, 204);
    public override Color MenuBorder => Color.FromArgb(45, 45, 48);
    public override Color ImageMarginGradientBegin => Color.FromArgb(37, 37, 38);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(37, 37, 38);
    public override Color ImageMarginGradientEnd => Color.FromArgb(37, 37, 38);
    public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 48);
}

