using System.ComponentModel;
using System.Diagnostics;
using KlingerClient.Components;
using Serilog;

namespace KlingerClient.Docking.Charts;

/// <summary>
/// High-performance real-time chart control using GDI+
/// Optimized for minimal latency and smooth rendering
/// </summary>
public partial class ChartsControl : UserControl {
    private readonly ILogger _logger = Log.ForContext<ChartsControl>();
    private readonly ChartsService _service;
    private readonly ChartRenderer _renderer;
    private readonly System.Windows.Forms.Timer _renderTimer;
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();

    // Rendering state
    private BufferedGraphicsContext _bufferContext;
    private BufferedGraphics? _buffer;
    private bool _needsRedraw = true;
    private Point? _mousePosition;

    // Chart state
    private ChartType _chartType = ChartType.Candlestick;
    private int _candleWidth = 8;
    private int _scrollOffset = 0;
    private readonly CandleData[] _candleCache = new CandleData[500];
    private int _cachedCandleCount = 0;
    private decimal? _lastPrice;

    // Performance metrics
    private int _frameCount;
    private double _avgFrameTime;

    private const int TARGET_FPS = 60;
    private const int FRAME_INTERVAL_MS = 1000 / TARGET_FPS;
    private const int MIN_CANDLE_WIDTH = 2;
    private const int MAX_CANDLE_WIDTH = 40;

    [Category("Chart")]
    [Description("Chart type (Candlestick, Line, Area)")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ChartType ChartType {
        get => _chartType;
        set { _chartType = value; InvalidateChart(); }
    }

    [Category("Chart")]
    [Description("Width of each candle in pixels")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CandleWidth {
        get => _candleWidth;
        set {
            _candleWidth = Math.Clamp(value, MIN_CANDLE_WIDTH, MAX_CANDLE_WIDTH);
            InvalidateChart();
        }
    }

    /// <summary>
    /// Designer-only constructor - DO NOT USE at runtime
    /// </summary>
    public ChartsControl() {
        _service = null!;
        _renderer = new ChartRenderer();
        _renderTimer = new System.Windows.Forms.Timer();
        _bufferContext = BufferedGraphicsManager.Current;
        InitializeComponent();
    }

    public ChartsControl(ChartsService service) {
        _service = service;
        _renderer = new ChartRenderer();

        // Enable optimal double buffering
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        InitializeComponent();

        _bufferContext = BufferedGraphicsManager.Current;

        SetupControls();
        BindToService();

        // High-performance render timer
        _renderTimer = new System.Windows.Forms.Timer { Interval = FRAME_INTERVAL_MS };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();
    }

    private void SetupControls() {
        // Chart panel events
        _chartPanel.Paint += ChartPanel_Paint;
        _chartPanel.Resize += (s, e) => RecreateBuffer();
        _chartPanel.MouseMove += ChartPanel_MouseMove;
        _chartPanel.MouseLeave += (s, e) => { _mousePosition = null; InvalidateChart(); };
        _chartPanel.MouseWheel += ChartPanel_MouseWheel;

        // Timeframe buttons
        _tf1sBtn.Click += (s, e) => SetTimeframe(ChartTimeframe.S1);
        _tf5sBtn.Click += (s, e) => SetTimeframe(ChartTimeframe.S5);
        _tf15sBtn.Click += (s, e) => SetTimeframe(ChartTimeframe.S15);
        _tf1mBtn.Click += (s, e) => SetTimeframe(ChartTimeframe.M1);
        _tf5mBtn.Click += (s, e) => SetTimeframe(ChartTimeframe.M5);

        // Chart type buttons
        _candleBtn.Click += (s, e) => { _chartType = ChartType.Candlestick; InvalidateChart(); UpdateTypeButtons(); };
        _lineBtn.Click += (s, e) => { _chartType = ChartType.Line; InvalidateChart(); UpdateTypeButtons(); };
        _areaBtn.Click += (s, e) => { _chartType = ChartType.Area; InvalidateChart(); UpdateTypeButtons(); };

        // Symbol selector
        _symbolSelector.SelectionChanged += (s, e) => {
            _service.CurrentSymbol = _symbolSelector.SelectedSymbolIndex;
            _scrollOffset = 0;
            InvalidateChart();
        };

        UpdateTimeframeButtons();
        UpdateTypeButtons();
    }

    private void BindToService() {
        // Load initial instruments
        RefreshSymbols();

        // Subscribe to instrument updates
        _service.OnSymbolsUpdated += (s, e) => RefreshSymbols();

        _service.CandleUpdated += (s, e) => InvalidateChart();
        _service.NewCandleCreated += (s, e) => InvalidateChart();
    }

    public void RefreshSymbols() {
        if (InvokeRequired) {
            Invoke(RefreshSymbols);
            return;
        }

        var instruments = _service.SymbolNames.Select(kvp => new InstrumentItem {
            SymbolIndex = kvp.Key,
            Symbol = kvp.Value,
            Name = string.Empty
        }).ToList();

        _symbolSelector.RefreshInstruments(instruments);

        if (instruments.Any() && _service.CurrentSymbol == 0) {
            var first = instruments.First();
            _service.CurrentSymbol = first.SymbolIndex;
            _symbolSelector.SelectedSymbolIndex = first.SymbolIndex;
        }
    }

    private void SetTimeframe(ChartTimeframe tf) {
        _service.CurrentTimeframe = tf;
        _scrollOffset = 0;
        UpdateTimeframeButtons();
        InvalidateChart();
    }

    private void UpdateTimeframeButtons() {
        var tf = _service.CurrentTimeframe;
        _tf1sBtn.BackColor = tf == ChartTimeframe.S1 ? Color.FromArgb(60, 60, 65) : Color.FromArgb(45, 45, 48);
        _tf5sBtn.BackColor = tf == ChartTimeframe.S5 ? Color.FromArgb(60, 60, 65) : Color.FromArgb(45, 45, 48);
        _tf15sBtn.BackColor = tf == ChartTimeframe.S15 ? Color.FromArgb(60, 60, 65) : Color.FromArgb(45, 45, 48);
        _tf1mBtn.BackColor = tf == ChartTimeframe.M1 ? Color.FromArgb(60, 60, 65) : Color.FromArgb(45, 45, 48);
        _tf5mBtn.BackColor = tf == ChartTimeframe.M5 ? Color.FromArgb(60, 60, 65) : Color.FromArgb(45, 45, 48);
    }

    private void UpdateTypeButtons() {
        _candleBtn.BackColor = _chartType == ChartType.Candlestick ? Color.FromArgb(60, 60, 65) : Color.FromArgb(45, 45, 48);
        _lineBtn.BackColor = _chartType == ChartType.Line ? Color.FromArgb(60, 60, 65) : Color.FromArgb(45, 45, 48);
        _areaBtn.BackColor = _chartType == ChartType.Area ? Color.FromArgb(60, 60, 65) : Color.FromArgb(45, 45, 48);
    }

    private void RecreateBuffer() {
        if (_chartPanel.Width <= 0 || _chartPanel.Height <= 0) return;

        _buffer?.Dispose();
        _buffer = _bufferContext.Allocate(_chartPanel.CreateGraphics(), _chartPanel.ClientRectangle);
        InvalidateChart();
    }

    private void InvalidateChart() {
        _needsRedraw = true;
    }

    private void OnRenderTick(object? sender, EventArgs e) {
        if (!_needsRedraw) return;
        _needsRedraw = false;

        var startTime = _frameTimer.ElapsedTicks;

        RenderChart();

        var frameTime = (_frameTimer.ElapsedTicks - startTime) * 1000.0 / Stopwatch.Frequency;
        _avgFrameTime = _avgFrameTime * 0.9 + frameTime * 0.1;
        _frameCount++;
    }

    private void RenderChart() {
        if (_buffer == null) {
            RecreateBuffer();
            if (_buffer == null) return;
        }

        var g = _buffer.Graphics;
        g.Clear(Color.FromArgb(22, 22, 26));

        // Get candles from service
        var buffer = _service.GetBuffer(_service.CurrentSymbol, _service.CurrentTimeframe);
        if (buffer != null && buffer.Count > 0) {
            _cachedCandleCount = buffer.GetVisibleCandles(_candleCache, _scrollOffset, _candleCache.Length);

            // Get last price
            if (_cachedCandleCount > 0) {
                _lastPrice = _candleCache[_cachedCandleCount - 1].Close;
            }
        } else {
            _cachedCandleCount = 0;
            _lastPrice = null;
        }

        // Render using cached renderer
        _renderer.RenderChart(
            g,
            _chartPanel.ClientRectangle,
            _candleCache.AsSpan(0, _cachedCandleCount),
            _cachedCandleCount,
            _chartType,
            _candleWidth,
            _mousePosition,
            _lastPrice);

        // Draw FPS counter (debug)
#if DEBUG
        g.DrawString($"FPS: {1000.0 / Math.Max(1, _avgFrameTime):F0} | Candles: {_cachedCandleCount}",
            SystemFonts.DefaultFont, Brushes.Yellow, 5, 5);
#endif

        // Render to screen
        _buffer.Render();
    }

    private void ChartPanel_Paint(object? sender, PaintEventArgs e) {
        _buffer?.Render(e.Graphics);
    }

    private void ChartPanel_MouseMove(object? sender, MouseEventArgs e) {
        _mousePosition = e.Location;
        InvalidateChart();
    }

    private void ChartPanel_MouseWheel(object? sender, MouseEventArgs e) {
        if (ModifierKeys.HasFlag(Keys.Control)) {
            // Zoom
            _candleWidth = Math.Clamp(_candleWidth + (e.Delta > 0 ? 1 : -1), MIN_CANDLE_WIDTH, MAX_CANDLE_WIDTH);
        } else {
            // Scroll
            var delta = e.Delta > 0 ? -5 : 5;
            _scrollOffset = Math.Max(0, _scrollOffset + delta);
        }
        InvalidateChart();
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _renderTimer?.Stop();
            _renderTimer?.Dispose();
            _buffer?.Dispose();
            _renderer?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }
}
