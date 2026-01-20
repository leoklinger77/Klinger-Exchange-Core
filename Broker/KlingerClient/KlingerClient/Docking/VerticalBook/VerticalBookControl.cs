using System.Diagnostics;
using KlingerClient.Services;
using KlingerClient.Docking.VerticalBook;
using KlingerClient.Components;
using Serilog;

namespace KlingerBroker.Ui.Docking;

/// <summary>
/// High-performance DOM Ladder using GDI+ rendering
/// LTP always centered, prices flow within fixed visible rows
/// Optimized for 60fps with minimal latency
/// </summary>
public partial class VerticalBookControl : UserControl {
    private readonly ILogger _logger = Log.ForContext<VerticalBookControl>();
    private readonly VerticalBookService _service;
    private readonly IOrderRouter _router;
    private readonly System.Windows.Forms.Timer _renderTimer;
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();

    // Rendering state
    private BufferedGraphicsContext _bufferContext;
    private BufferedGraphics? _buffer;
    private bool _needsRedraw = true;

    // Ladder configuration
    private int _visibleRows = 21;
    private int _centerRow = 10;
    private int _rowHeight = 20;
    private decimal _tickSize = 0.01m;
    private decimal? _centerPrice = null;

    // Column widths
    private const int COL_WORK = 50;
    private const int COL_BID = 70;
    private const int COL_PRICE = 80;
    private const int COL_LTP = 25;
    private const int COL_ASK = 70;
    private const int COL_WORK2 = 50;
    private const int HEADER_HEIGHT = 22;

    // Cached row data for rendering
    private RowData[] _rowCache = Array.Empty<RowData>();
    private bool _dataDirty = true;

    // Mouse state for click-to-trade
    private int _hoverRow = -1;
    private int _hoverCol = -1;

    // Performance
    private const int TARGET_FPS = 60;
    private const int FRAME_INTERVAL_MS = 1000 / TARGET_FPS;

    // Pre-cached brushes and pens for performance
    private static readonly Color BgDefault = Color.FromArgb(37, 37, 38);
    private static readonly Color BgBid = Color.FromArgb(40, 80, 140);
    private static readonly Color BgAsk = Color.FromArgb(120, 40, 40);
    private static readonly Color BgBestBid = Color.FromArgb(30, 140, 200);
    private static readonly Color BgBestAsk = Color.FromArgb(200, 50, 50);
    private static readonly Color BgLtp = Color.FromArgb(80, 80, 40);
    private static readonly Color BgPrice = Color.FromArgb(45, 45, 48);
    private static readonly Color BgHeader = Color.FromArgb(27, 27, 28);
    private static readonly Color BgHover = Color.FromArgb(60, 60, 65);
    private static readonly Color FgWhite = Color.White;
    private static readonly Color FgBid = Color.FromArgb(150, 200, 255);
    private static readonly Color FgAsk = Color.FromArgb(255, 180, 180);
    private static readonly Color FgLtp = Color.FromArgb(255, 215, 0);
    private static readonly Color GridLine = Color.FromArgb(50, 50, 52);

    private readonly SolidBrush _brushDefault = new(BgDefault);
    private readonly SolidBrush _brushBid = new(BgBid);
    private readonly SolidBrush _brushAsk = new(BgAsk);
    private readonly SolidBrush _brushBestBid = new(BgBestBid);
    private readonly SolidBrush _brushBestAsk = new(BgBestAsk);
    private readonly SolidBrush _brushLtp = new(BgLtp);
    private readonly SolidBrush _brushPrice = new(BgPrice);
    private readonly SolidBrush _brushHeader = new(BgHeader);
    private readonly SolidBrush _brushHover = new(BgHover);
    private readonly SolidBrush _brushWhite = new(FgWhite);
    private readonly SolidBrush _brushFgBid = new(FgBid);
    private readonly SolidBrush _brushFgAsk = new(FgAsk);
    private readonly SolidBrush _brushFgLtp = new(FgLtp);
    private readonly Pen _penGrid = new(GridLine);
    private readonly Font _fontHeader = new("Segoe UI", 8f, FontStyle.Bold);
    private readonly Font _fontCell = new("Consolas", 10f, FontStyle.Bold);
    private readonly Font _fontPrice = new("Consolas", 11f, FontStyle.Bold);
    private readonly Font _fontLtp = new("Consolas", 9f, FontStyle.Bold);

    private readonly struct RowData {
        public readonly decimal Price;
        public readonly long BidQty;
        public readonly long AskQty;
        public readonly long BuyWork;
        public readonly long SellWork;
        public readonly bool IsLtp;
        public readonly bool IsBestBid;
        public readonly bool IsBestAsk;

        public RowData(decimal price, long bidQty, long askQty, long buyWork, long sellWork,
                       bool isLtp, bool isBestBid, bool isBestAsk) {
            Price = price;
            BidQty = bidQty;
            AskQty = askQty;
            BuyWork = buyWork;
            SellWork = sellWork;
            IsLtp = isLtp;
            IsBestBid = isBestBid;
            IsBestAsk = isBestAsk;
        }
    }

    public VerticalBookControl(VerticalBookService service, IOrderRouter router) {
        _service = service;
        _router = router;

        // Enable optimal rendering
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
        // Ladder panel events
        _ladderPanel.Paint += LadderPanel_Paint;
        _ladderPanel.Resize += (s, e) => { RecreateBuffer(); RecalculateVisibleRows(); };
        _ladderPanel.MouseMove += LadderPanel_MouseMove;
        _ladderPanel.MouseLeave += (s, e) => { _hoverRow = -1; _hoverCol = -1; InvalidateLadder(); };
        _ladderPanel.MouseClick += LadderPanel_MouseClick;

        // Quantity buttons
        _qty1Btn.Click += (s, e) => AddQuantity(1);
        _qty5Btn.Click += (s, e) => AddQuantity(5);
        _qty10Btn.Click += (s, e) => AddQuantity(10);
        _qty25Btn.Click += (s, e) => AddQuantity(25);
        _qty50Btn.Click += (s, e) => AddQuantity(50);
        _qty100Btn.Click += (s, e) => AddQuantity(100);

        // Cancel buttons
        _cancelBuysBtn.Click += (s, e) => CancelOrders(true, false);
        _cancelSellsBtn.Click += (s, e) => CancelOrders(false, true);
        _cancelAllBtn.Click += (s, e) => CancelOrders(true, true);

        // Symbol selector
        _symbolSelector.SelectionChanged += (s, e) => {
            _service.CurrentSymbol = _symbolSelector.SelectedSymbolIndex;
            _dataDirty = true;
            InvalidateLadder();
        };

        // Initial setup after load
        this.Load += (s, e) => {
            RecreateBuffer();
            RecalculateVisibleRows();
        };
    }

    private void BindToService() {
        _symbolSelector.LoadInstruments(_service.SymbolNames);

        // Subscribe to book updates - just mark dirty, render timer handles redraw
        _service.BookUpdated += (_, _) => {
            _dataDirty = true;
        };
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

        _dataDirty = true;
        InvalidateLadder();
    }

    private void RecreateBuffer() {
        if (_ladderPanel.Width <= 0 || _ladderPanel.Height <= 0) return;

        _buffer?.Dispose();
        _buffer = _bufferContext.Allocate(_ladderPanel.CreateGraphics(), _ladderPanel.ClientRectangle);
        _needsRedraw = true;
    }

    private void RecalculateVisibleRows() {
        int availableHeight = _ladderPanel.ClientSize.Height - HEADER_HEIGHT;
        int newVisibleRows = Math.Max(11, availableHeight / _rowHeight);

        // Make odd for true center
        if (newVisibleRows % 2 == 0) newVisibleRows--;

        if (newVisibleRows != _visibleRows) {
            _visibleRows = newVisibleRows;
            _centerRow = _visibleRows / 2;
            _rowCache = new RowData[_visibleRows];
            _dataDirty = true;
            _needsRedraw = true;
        }
    }

    private void InvalidateLadder() {
        _needsRedraw = true;
    }

    private void OnRenderTick(object? sender, EventArgs e) {
        if (_dataDirty) {
            UpdateRowData();
            _dataDirty = false;
            _needsRedraw = true;
        }

        if (_needsRedraw && _buffer != null) {
            RenderLadder(_buffer.Graphics);
            _buffer.Render();
            _needsRedraw = false;
        }
    }

    private void UpdateRowData() {
        var currentSymbol = _service.CurrentSymbol;
        var consolidatedBook = _service.GetConsolidatedBook(currentSymbol);
        var lastTradedPrice = _service.GetLastTradedPrice(currentSymbol);

        _tickSize = _service.GetTickSize(currentSymbol);
        if (_tickSize <= 0) _tickSize = 0.01m;

        // Determine center price
        decimal newCenterPrice;
        if (lastTradedPrice.HasValue && lastTradedPrice.Value > 0) {
            newCenterPrice = lastTradedPrice.Value;
        } else if (consolidatedBook.Any()) {
            var bestBidPrice = consolidatedBook.Where(x => x.bidQty > 0).Select(x => x.price).DefaultIfEmpty(0).Max();
            var bestAskPrice = consolidatedBook.Where(x => x.askQty > 0).Select(x => x.price).DefaultIfEmpty(0).Min();
            if (bestBidPrice > 0 && bestAskPrice > 0) {
                newCenterPrice = Math.Round((bestBidPrice + bestAskPrice) / 2 / _tickSize) * _tickSize;
            } else if (consolidatedBook.Any()) {
                newCenterPrice = consolidatedBook.First().price;
            } else {
                return;
            }
        } else {
            return;
        }

        _centerPrice = newCenterPrice;

        // Build lookup
        var bookLookup = new Dictionary<decimal, (long bidQty, long askQty)>();
        foreach (var item in consolidatedBook) {
            var roundedPrice = Math.Round(item.price / _tickSize) * _tickSize;
            bookLookup[roundedPrice] = (item.bidQty, item.askQty);
        }

        // Find best bid/ask
        decimal bestBid = consolidatedBook.Where(x => x.bidQty > 0).Select(x => x.price).DefaultIfEmpty(0).Max();
        decimal bestAsk = consolidatedBook.Where(x => x.askQty > 0).Select(x => x.price).DefaultIfEmpty(decimal.MaxValue).Min();
        if (bestAsk == decimal.MaxValue) bestAsk = 0;

        // Update spread label
        if (bestBid > 0 && bestAsk > 0 && bestAsk < decimal.MaxValue) {
            var spread = bestAsk - bestBid;
            var ltpText = lastTradedPrice.HasValue ? $"LTP: {lastTradedPrice.Value:F2}  |  " : "";
            _spreadLabel.Text = $"{ltpText}Spread: {spread:F2}  |  Bid: {bestBid:F2}  |  Ask: {bestAsk:F2}";
        }

        // Build row data
        if (_rowCache.Length != _visibleRows) {
            _rowCache = new RowData[_visibleRows];
        }

        for (int i = 0; i < _visibleRows; i++) {
            int offsetFromCenter = _centerRow - i;
            decimal priceAtRow = Math.Round((_centerPrice.Value + (offsetFromCenter * _tickSize)) / _tickSize) * _tickSize;

            long bidQty = 0, askQty = 0;
            if (bookLookup.TryGetValue(priceAtRow, out var bookData)) {
                bidQty = bookData.bidQty;
                askQty = bookData.askQty;
            }

            var (buyWrk, sellWrk) = _service.GetWorkingQty(currentSymbol, priceAtRow);

            bool isLtp = lastTradedPrice.HasValue && Math.Abs(priceAtRow - lastTradedPrice.Value) < _tickSize * 0.5m;
            bool isBestBid = bestBid > 0 && Math.Abs(priceAtRow - bestBid) < _tickSize * 0.5m;
            bool isBestAsk = bestAsk > 0 && Math.Abs(priceAtRow - bestAsk) < _tickSize * 0.5m;

            _rowCache[i] = new RowData(priceAtRow, bidQty, askQty, buyWrk, sellWrk, isLtp, isBestBid, isBestAsk);
        }
    }

    private void RenderLadder(Graphics g) {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int width = _ladderPanel.ClientSize.Width;
        int height = _ladderPanel.ClientSize.Height;

        // Background
        g.FillRectangle(_brushDefault, 0, 0, width, height);

        // Calculate column positions
        int x0 = 0;
        int x1 = x0 + COL_WORK;
        int x2 = x1 + COL_BID;
        int x3 = x2 + COL_PRICE;
        int x4 = x3 + COL_LTP;
        int x5 = x4 + COL_ASK;

        // Draw header
        g.FillRectangle(_brushHeader, 0, 0, width, HEADER_HEIGHT);
        DrawCenteredText(g, "Work", _fontHeader, _brushWhite, x0, 0, COL_WORK, HEADER_HEIGHT);
        DrawCenteredText(g, "Bid", _fontHeader, _brushWhite, x1, 0, COL_BID, HEADER_HEIGHT);
        DrawCenteredText(g, "Price", _fontHeader, _brushWhite, x2, 0, COL_PRICE, HEADER_HEIGHT);
        DrawCenteredText(g, "Lst", _fontHeader, _brushWhite, x3, 0, COL_LTP, HEADER_HEIGHT);
        DrawCenteredText(g, "Ask", _fontHeader, _brushWhite, x4, 0, COL_ASK, HEADER_HEIGHT);
        DrawCenteredText(g, "Work", _fontHeader, _brushWhite, x5, 0, COL_WORK2, HEADER_HEIGHT);

        // Header line
        g.DrawLine(_penGrid, 0, HEADER_HEIGHT, width, HEADER_HEIGHT);

        // Draw rows
        for (int i = 0; i < _visibleRows && i < _rowCache.Length; i++) {
            int y = HEADER_HEIGHT + (i * _rowHeight);
            ref readonly var row = ref _rowCache[i];

            bool isHoverRow = (i == _hoverRow);

            // Work column (buy side)
            if (isHoverRow && _hoverCol == 0) {
                g.FillRectangle(_brushHover, x0, y, COL_WORK, _rowHeight);
            }
            if (row.BuyWork > 0) {
                DrawCenteredText(g, row.BuyWork.ToString(), _fontCell, _brushWhite, x0, y, COL_WORK, _rowHeight);
            }

            // Bid column
            SolidBrush bidBg, bidFg;
            if (row.IsBestBid && row.BidQty > 0) {
                bidBg = _brushBestBid; bidFg = _brushWhite;
            } else if (row.BidQty > 0) {
                bidBg = _brushBid; bidFg = _brushFgBid;
            } else {
                bidBg = isHoverRow && _hoverCol == 1 ? _brushHover : _brushDefault;
                bidFg = _brushWhite;
            }
            g.FillRectangle(bidBg, x1, y, COL_BID, _rowHeight);
            if (row.BidQty > 0) {
                DrawCenteredText(g, row.BidQty.ToString("N0"), _fontCell, bidFg, x1, y, COL_BID, _rowHeight);
            }

            // Price column
            var priceBg = row.IsLtp ? _brushLtp : _brushPrice;
            var priceFg = row.IsLtp ? _brushFgLtp : _brushWhite;
            g.FillRectangle(priceBg, x2, y, COL_PRICE, _rowHeight);
            DrawCenteredText(g, row.Price.ToString("F2"), _fontPrice, priceFg, x2, y, COL_PRICE, _rowHeight);

            // LTP marker column
            g.FillRectangle(row.IsLtp ? _brushLtp : _brushDefault, x3, y, COL_LTP, _rowHeight);
            if (row.IsLtp) {
                DrawCenteredText(g, "◄", _fontLtp, _brushFgLtp, x3, y, COL_LTP, _rowHeight);
            }

            // Ask column
            SolidBrush askBg, askFg;
            if (row.IsBestAsk && row.AskQty > 0) {
                askBg = _brushBestAsk; askFg = _brushWhite;
            } else if (row.AskQty > 0) {
                askBg = _brushAsk; askFg = _brushFgAsk;
            } else {
                askBg = isHoverRow && _hoverCol == 4 ? _brushHover : _brushDefault;
                askFg = _brushWhite;
            }
            g.FillRectangle(askBg, x4, y, COL_ASK, _rowHeight);
            if (row.AskQty > 0) {
                DrawCenteredText(g, row.AskQty.ToString("N0"), _fontCell, askFg, x4, y, COL_ASK, _rowHeight);
            }

            // Work column (sell side)
            if (isHoverRow && _hoverCol == 5) {
                g.FillRectangle(_brushHover, x5, y, COL_WORK2, _rowHeight);
            }
            if (row.SellWork > 0) {
                DrawCenteredText(g, row.SellWork.ToString(), _fontCell, _brushWhite, x5, y, COL_WORK2, _rowHeight);
            }

            // Row separator
            g.DrawLine(_penGrid, 0, y + _rowHeight, width, y + _rowHeight);
        }

        // Column separators
        g.DrawLine(_penGrid, x1, 0, x1, height);
        g.DrawLine(_penGrid, x2, 0, x2, height);
        g.DrawLine(_penGrid, x3, 0, x3, height);
        g.DrawLine(_penGrid, x4, 0, x4, height);
        g.DrawLine(_penGrid, x5, 0, x5, height);
    }

    private void DrawCenteredText(Graphics g, string text, Font font, Brush brush, int x, int y, int width, int height) {
        var size = g.MeasureString(text, font);
        float tx = x + (width - size.Width) / 2;
        float ty = y + (height - size.Height) / 2;
        g.DrawString(text, font, brush, tx, ty);
    }

    private void LadderPanel_Paint(object? sender, PaintEventArgs e) {
        if (_buffer != null) {
            _buffer.Render(e.Graphics);
        }
    }

    private void LadderPanel_MouseMove(object? sender, MouseEventArgs e) {
        int newRow = (e.Y - HEADER_HEIGHT) / _rowHeight;
        int newCol = GetColumnFromX(e.X);

        if (newRow != _hoverRow || newCol != _hoverCol) {
            _hoverRow = (newRow >= 0 && newRow < _visibleRows) ? newRow : -1;
            _hoverCol = newCol;
            InvalidateLadder();
        }
    }

    private int GetColumnFromX(int x) {
        int x1 = COL_WORK;
        int x2 = x1 + COL_BID;
        int x3 = x2 + COL_PRICE;
        int x4 = x3 + COL_LTP;
        int x5 = x4 + COL_ASK;

        if (x < x1) return 0;
        if (x < x2) return 1;
        if (x < x3) return 2;
        if (x < x4) return 3;
        if (x < x5) return 4;
        return 5;
    }

    private void LadderPanel_MouseClick(object? sender, MouseEventArgs e) {
        int row = (e.Y - HEADER_HEIGHT) / _rowHeight;
        int col = GetColumnFromX(e.X);

        if (row < 0 || row >= _visibleRows || row >= _rowCache.Length) return;

        // Only trade on Bid (col 1) or Ask (col 4) clicks
        if (col != 1 && col != 4) return;

        if (!int.TryParse(_qtyTextBox.Text, out int qty) || qty <= 0) {
            MessageBox.Show("Invalid quantity", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_router.IsConnected) {
            MessageBox.Show("Not connected to exchange", "Order Entry", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var price = _rowCache[row].Price;
        var currentSymbol = _service.CurrentSymbol;
        var symbolName = _service.SymbolNames.TryGetValue(currentSymbol, out var name) ? name : $"#{currentSymbol}";

        // Col 1 = Buy, Col 4 = Sell
        Side side = col == 1 ? Side.Buy : Side.Sell;

        try {
            var clOrdId = _router.SendNewOrder(new NewOrderRequest(symbolName, side, qty, price));

            // Track working order
            _service.AddWorkingOrder(currentSymbol, price, side, qty);
            _dataDirty = true;
            InvalidateLadder();
        } catch (Exception ex) {
            MessageBox.Show($"Failed to send order:\n{ex.Message}", "Order Entry", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddQuantity(int amount) {
        if (int.TryParse(_qtyTextBox.Text, out int currentQty)) {
            _qtyTextBox.Text = (currentQty + amount).ToString();
        } else {
            _qtyTextBox.Text = amount.ToString();
        }
    }

    private void CancelOrders(bool buys, bool sells) {
        // TODO: Implement cancel orders via router
        var msg = (buys && sells) ? "all orders" : (buys ? "buy orders" : "sell orders");
        _logger.Information("Canceling {Orders}", msg);
    }  

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _renderTimer?.Stop();
            _renderTimer?.Dispose();
            _buffer?.Dispose();
            _brushDefault?.Dispose();
            _brushBid?.Dispose();
            _brushAsk?.Dispose();
            _brushBestBid?.Dispose();
            _brushBestAsk?.Dispose();
            _brushLtp?.Dispose();
            _brushPrice?.Dispose();
            _brushHeader?.Dispose();
            _brushHover?.Dispose();
            _brushWhite?.Dispose();
            _brushFgBid?.Dispose();
            _brushFgAsk?.Dispose();
            _brushFgLtp?.Dispose();
            _penGrid?.Dispose();
            _fontHeader?.Dispose();
            _fontCell?.Dispose();
            _fontPrice?.Dispose();
            _fontLtp?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }
}
