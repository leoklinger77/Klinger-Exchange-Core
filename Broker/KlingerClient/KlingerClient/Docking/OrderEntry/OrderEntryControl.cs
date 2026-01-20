using KlingerClient.Components;

namespace KlingerBroker.Ui.Docking.OrderEntry;

/// <summary>
/// UserControl for Order Entry - can be edited in Designer
/// </summary>
public partial class OrderEntryControl : UserControl
{
    public SelectInstrumentControl SymbolSelector => _symbolSelector;
    public ComboBox SideCombo => _sideCombo;
    public NumericUpDown QuantityInput => _qty;
    public NumericUpDown PriceInput => _price;
    public Button SendButton => _send;

    public event EventHandler? SendClicked;

    public OrderEntryControl()
    {
        InitializeComponent();
        SetupControls();
    }

    private void SetupControls()
    {
        // Apply dark theme
        this.BackColor = Color.FromArgb(37, 37, 38);
        this.ForeColor = Color.White;

        // SelectInstrumentControl já vem configurado
        _symbolSelector.LabelText = "Symbol";
        _symbolSelector.Dock = DockStyle.Fill;

        _sideCombo.Dock = DockStyle.Fill;
        _sideCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _sideCombo.Items.AddRange(["Buy", "Sell"]);
        _sideCombo.SelectedIndex = 0;
        _sideCombo.BackColor = Color.FromArgb(45, 45, 48);
        _sideCombo.ForeColor = Color.White;
        _sideCombo.FlatStyle = FlatStyle.Flat;
        _sideCombo.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        _qty.Dock = DockStyle.Left;
        _qty.Minimum = 1;
        _qty.Maximum = 1_000_000;
        _qty.Value = 100;
        _qty.BackColor = Color.FromArgb(45, 45, 48);
        _qty.ForeColor = Color.White;
        _qty.Font = new Font("Consolas", 10F, FontStyle.Bold);

        _price.Dock = DockStyle.Left;
        _price.Minimum = 0;
        _price.Maximum = 1_000_000;
        _price.DecimalPlaces = 2;
        _price.Increment = 0.01m;
        _price.Value = 10.00m;
        _price.BackColor = Color.FromArgb(45, 45, 48);
        _price.ForeColor = Color.White;
        _price.Font = new Font("Consolas", 10F, FontStyle.Bold);

        _send.Dock = DockStyle.Right;
        _send.Text = "Send";
        _send.BackColor = Color.FromArgb(0, 122, 204);
        _send.ForeColor = Color.White;
        _send.FlatStyle = FlatStyle.Flat;
        _send.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _send.FlatAppearance.BorderSize = 0;
        _send.Click += (s, e) => SendClicked?.Invoke(s, e);
    }
    
    public void LoadSymbols(IReadOnlyDictionary<short, string> symbolNames)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<IReadOnlyDictionary<short, string>>(LoadSymbols), symbolNames);
            return;
        }
        
        _symbolSelector.LoadInstruments(symbolNames);
    }
}
