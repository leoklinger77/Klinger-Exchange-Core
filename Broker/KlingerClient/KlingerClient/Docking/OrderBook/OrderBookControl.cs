using KlingerClient.Docking.OrderBook;
using KlingerClient.Components;

namespace KlingerBroker.Ui.Docking.OrderBook;

public partial class OrderBookControl : UserControl
{
    private readonly OrderBookService _service;
    private readonly SynchronizationContext? _syncContext;

    public OrderBookControl(OrderBookService service)
    {
        _service = service;
        _syncContext = SynchronizationContext.Current;
        InitializeComponent();
        SetupControls();
        BindToService();
    }

    private void BindToService()
    {
        // Initialize symbols
        _symbolSelector.LoadInstruments(_service.SymbolNames);

        // Subscribe to service events - ensure UI thread
        _service.TradesUpdated += (_, _) =>
        {
            if (_syncContext != null)
                _syncContext.Post(_ => UpdateGrid(), null);
            else
                UpdateGrid();
        };
        
        _symbolSelector.SelectionChanged += (_, _) =>
        {
            _service.CurrentSymbol = _symbolSelector.SelectedSymbolIndex;
        };
    }

    public void RefreshSymbols()
    {
        if (InvokeRequired)
        {
            Invoke(RefreshSymbols);
            return;
        }

        _symbolSelector.RefreshInstruments(_service.SymbolNames.Select(kvp => new InstrumentItem
        {
            SymbolIndex = kvp.Key,
            Symbol = kvp.Value,
            Name = string.Empty
        }));
    }

    private void SetupControls()
    {
        // Apply dark theme
        this.BackColor = Color.FromArgb(37, 37, 38);
        this.ForeColor = Color.White;

        _symbolSelector.LabelText = "Symbol:";
        _symbolSelector.ShowLabel = true;

        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.DefaultCellStyle.Font = new Font("Consolas", 9, FontStyle.Regular);
        _grid.RowTemplate.Height = 22;
        _grid.GridColor = Color.FromArgb(45, 45, 48);
        _grid.BackgroundColor = Color.FromArgb(30, 30, 30);
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(37, 37, 38);
        _grid.DefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(51, 153, 255);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(27, 27, 28);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(204, 204, 204);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.ColumnHeadersHeight = 22;
        
        // Enable double buffering
        typeof(DataGridView).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
            null, _grid, new object[] { true });

        // Columns for trade MyOrders
        _grid.Columns.Add("Time", "Time");
        _grid.Columns.Add("Symbol", "Symbol");
        _grid.Columns.Add("Price", "Price");
        _grid.Columns.Add("Qty", "Qty");
        _grid.Columns.Add("BuyId", "Buy ID");
        _grid.Columns.Add("SellId", "Sell ID");
        _grid.Columns.Add("Seq", "Seq");
    }

    private void UpdateGrid()
    {
        try
        {
            _grid.SuspendLayout();
            _grid.Rows.Clear();

            var currentSymbol = _service.CurrentSymbol;
            var symbolName = _service.SymbolNames.TryGetValue(currentSymbol, out var name)
                ? name
                : $"#{currentSymbol}";

            var trades = _service.GetTrades(currentSymbol, 50);

            foreach (var trade in trades)
            {
                var time = new DateTime(trade.TimestampNs / 100, DateTimeKind.Utc);

                _grid.Rows.Add(
                    time.ToString("HH:mm:ss.fff"),
                    symbolName,
                    trade.Price.ToString("N2"),
                    trade.Quantity,
                    trade.BuyOrderId,
                    trade.SellOrderId,
                    trade.SequenceNumber
                );
            }
        }
        finally
        {
            _grid.ResumeLayout();
        }
    }
}
