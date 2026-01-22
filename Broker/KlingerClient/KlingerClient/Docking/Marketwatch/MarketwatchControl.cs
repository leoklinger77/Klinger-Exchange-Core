using KlingerClient.Services;
using KlingerClient.Components;
using Serilog;

namespace KlingerClient.Docking.Marketwatch;

public partial class MarketwatchControl : UserControl
{
    private readonly ILogger _logger = Log.ForContext<MarketwatchControl>();
    private readonly MarketwatchService _service;
    private bool _isUpdating;

    public MarketwatchControl(MarketwatchService service)
    {
        _service = service;
        InitializeComponent();
        SetupGrid();
        BindToService();
    }

    private void SetupGrid()
    {
        // Configure grid
        dataGridView1.Dock = DockStyle.Fill;
        dataGridView1.AutoGenerateColumns = false;
        dataGridView1.AllowUserToAddRows = false;
        dataGridView1.AllowUserToDeleteRows = false;
        dataGridView1.ReadOnly = true;
        dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dataGridView1.RowHeadersVisible = false;
        dataGridView1.BackgroundColor = Color.FromArgb(30, 30, 30);
        dataGridView1.GridColor = Color.FromArgb(50, 50, 50);
        dataGridView1.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
        dataGridView1.DefaultCellStyle.ForeColor = Color.White;
        dataGridView1.DefaultCellStyle.SelectionBackColor = Color.FromArgb(60, 60, 60);
        dataGridView1.DefaultCellStyle.SelectionForeColor = Color.White;
        dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
        dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        dataGridView1.EnableHeadersVisualStyles = false;

        // Enable double buffering
        typeof(DataGridView).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
            null, dataGridView1, new object[] { true });

        // Add columns
        dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Symbol",
            HeaderText = "Symbol",
            Width = 80,
            DataPropertyName = "Symbol"
        });

        dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "LastPrice",
            HeaderText = "Last",
            Width = 80,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        });

        dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Change",
            HeaderText = "Chg",
            Width = 70,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "+0.00;-0.00;0.00" }
        });

        dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ChangePercent",
            HeaderText = "%",
            Width = 60,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "+0.00;-0.00;0.00" }
        });

        dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Volume",
            HeaderText = "Volume",
            Width = 90,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
        });

        dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Trades",
            HeaderText = "Trades",
            Width = 60,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
        });

        // Add Remove button column with better styling
        var removeColumn = new DataGridViewButtonColumn
        {
            Name = "Remove",
            HeaderText = "",
            Text = "✕",
            UseColumnTextForButtonValue = true,
            Width = 50,
            FlatStyle = FlatStyle.Flat
        };
        removeColumn.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
        removeColumn.DefaultCellStyle.ForeColor = Color.White;
        removeColumn.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 50, 50);
        removeColumn.DefaultCellStyle.SelectionForeColor = Color.White;
        removeColumn.DefaultCellStyle.Font = new Font(dataGridView1.Font.FontFamily, 10, FontStyle.Bold);
        dataGridView1.Columns.Add(removeColumn);

        // Handle remove button click
        dataGridView1.CellClick += DataGridView1_CellClick;

        // Context menu for removing
        dataGridView1.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete && dataGridView1.SelectedRows.Count > 0)
            {
                var row = dataGridView1.SelectedRows[0];
                if (row.Tag is short symbolIndex)
                {
                    _service.RemoveSymbol(symbolIndex);
                }
            }
        };
    }

    private void DataGridView1_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        // Check if remove button clicked
        if (dataGridView1.Columns[e.ColumnIndex].Name == "Remove")
        {
            var row = dataGridView1.Rows[e.RowIndex];
            if (row.Tag is short symbolIndex)
            {
                _service.RemoveSymbol(symbolIndex);
            }
        }
    }

    private void BindToService()
    {
        // Subscribe to service events
        _service.QuotesUpdated += (_, _) => UpdateGrid();
        _service.SymbolsInitialized += (_, _) => RefreshSymbols();

        _addAllButton.Click += (_, _) => AddAllSymbols();

        // Handle symbol selection
        selectInstrumentControl1.SelectionChanged += (_, _) =>
        {
            var symbolIndex = selectInstrumentControl1.SelectedSymbolIndex;
            if (symbolIndex > 0)
            {
                _service.AddSymbol(symbolIndex);
                // Clear selection after adding
                selectInstrumentControl1.ClearSelection();
            }
        };

        // Initial load
        if (_service.SymbolNames.Any())
        {
            RefreshSymbols();
        }
    }

    private void AddAllSymbols()
    {
        foreach (var symbolIndex in _service.SymbolNames.Keys.OrderBy(x => x))
        {
            _service.AddSymbol(symbolIndex);
        }

        selectInstrumentControl1.ClearSelection();
    }

    public void RefreshSymbols()
    {
        if (InvokeRequired)
        {
            Invoke(RefreshSymbols);
            return;
        }

        var instruments = _service.SymbolNames.Select(kvp => new InstrumentItem
        {
            SymbolIndex = kvp.Key,
            Symbol = kvp.Value,
            Name = string.Empty
        }).ToList();

        selectInstrumentControl1.RefreshInstruments(instruments);
        _logger.Information("MarketwatchControl refreshed with {Count} symbols", instruments.Count);
    }

    private void UpdateGrid()
    {
        if (_isUpdating) return;
        if (InvokeRequired)
        {
            Invoke(UpdateGrid);
            return;
        }

        _isUpdating = true;

        try
        {
            var quotes = _service.WatchedQuotes.OrderBy(q => q.Symbol).ToList();

            // Add or update rows
            for (int i = 0; i < quotes.Count; i++)
            {
                var quote = quotes[i];
                DataGridViewRow row;

                if (i < dataGridView1.Rows.Count)
                {
                    row = dataGridView1.Rows[i];
                }
                else
                {
                    var idx = dataGridView1.Rows.Add();
                    row = dataGridView1.Rows[idx];
                }

                row.Tag = quote.SymbolIndex;
                row.Cells["Symbol"].Value = quote.Symbol;
                row.Cells["LastPrice"].Value = quote.LastPrice;
                row.Cells["Change"].Value = quote.Change;
                row.Cells["ChangePercent"].Value = quote.ChangePercent;
                row.Cells["Volume"].Value = quote.Volume;
                row.Cells["Trades"].Value = quote.TradeCount;

                // Color change cells
                var changeColor = quote.Change > 0 ? Color.LimeGreen 
                    : quote.Change < 0 ? Color.Tomato 
                    : Color.White;
                row.Cells["Change"].Style.ForeColor = changeColor;
                row.Cells["ChangePercent"].Style.ForeColor = changeColor;
            }

            // Remove extra rows
            while (dataGridView1.Rows.Count > quotes.Count)
            {
                dataGridView1.Rows.RemoveAt(dataGridView1.Rows.Count - 1);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }
}

