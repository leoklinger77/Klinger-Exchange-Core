using System.ComponentModel;

namespace KlingerClient.Components;

/// <summary>
/// UserControl para seleção de instrumentos com ComboBox estilizado
/// Exibe Symbol + Nome do instrumento
/// </summary>
public partial class SelectInstrumentControl : UserControl {
    private Dictionary<short, InstrumentItem> _instruments = new();

    [Category("Appearance")]
    [Description("Texto do label")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string LabelText {
        get => _label.Text;
        set => _label.Text = value;
    }

    [Category("Appearance")]
    [Description("Mostra ou esconde o label")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowLabel {
        get => _label.Visible;
        set => _label.Visible = value;
    }

    [Category("Behavior")]
    [Description("SymbolIndex selecionado")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public short SelectedSymbolIndex {
        get {
            if (_comboBox.SelectedItem is InstrumentItem item)
                return item.SymbolIndex;
            return -1;
        }
        set {
            foreach (InstrumentItem item in _comboBox.Items) {
                if (item.SymbolIndex == value) {
                    _comboBox.SelectedItem = item;
                    break;
                }
            }
        }
    }

    [Category("Behavior")]
    [Description("Symbol selecionado")]
    public string SelectedSymbol {
        get {
            if (_comboBox.SelectedItem is InstrumentItem item)
                return item.Symbol;
            return string.Empty;
        }
    }

    [Category("Behavior")]
    [Description("Item selecionado completo")]
    public InstrumentItem? SelectedInstrument {
        get => _comboBox.SelectedItem as InstrumentItem;
    }

    public event EventHandler? SelectionChanged;

    public SelectInstrumentControl() {
        InitializeComponent();
        SetupControls();
    }

    private void SetupControls() {
        // ComboBox custom drawing
        _comboBox.SelectedIndexChanged += (s, e) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        _comboBox.DrawItem += ComboBox_DrawItem;
    }

    private void ComboBox_DrawItem(object? sender, DrawItemEventArgs e) {
        if (e.Index < 0) return;

        var combo = sender as ComboBox;
        if (combo == null) return;

        var item = combo.Items[e.Index] as InstrumentItem;
        if (item == null) return;

        e.DrawBackground();

        // Colors
        var bgColor = (e.State & DrawItemState.Selected) != 0
            ? Color.FromArgb(51, 153, 255)
            : Color.FromArgb(45, 45, 48);

        var textColor = Color.White;
        var nameColor = Color.FromArgb(180, 180, 180);

        using (var bgBrush = new SolidBrush(bgColor)) {
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
        }

        // Symbol (bold)
        var symbolFont = new Font("Segoe UI", 9F, FontStyle.Bold);
        var symbolSize = e.Graphics.MeasureString(item.Symbol, symbolFont);
        using (var textBrush = new SolidBrush(textColor)) {
            e.Graphics.DrawString(item.Symbol, symbolFont, textBrush,
                new PointF(e.Bounds.X + 5, e.Bounds.Y + (e.Bounds.Height - symbolSize.Height) / 2));
        }

        // Name (regular, cinza)
        if (!string.IsNullOrEmpty(item.Name)) {
            var nameFont = new Font("Segoe UI", 8F, FontStyle.Regular);
            using (var nameBrush = new SolidBrush(nameColor)) {
                e.Graphics.DrawString($" - {item.Name}", nameFont, nameBrush,
                    new PointF(e.Bounds.X + 5 + symbolSize.Width, e.Bounds.Y + (e.Bounds.Height - symbolSize.Height) / 2 + 1));
            }
        }

        e.DrawFocusRectangle();
    }

    /// <summary>
    /// Carrega instrumentos no ComboBox
    /// </summary>
    public void LoadInstruments(IEnumerable<InstrumentItem> instruments) {
        if (InvokeRequired) {
            Invoke(new Action<IEnumerable<InstrumentItem>>(LoadInstruments), instruments);
            return;
        }

        _instruments.Clear();
        _comboBox.Items.Clear();

        foreach (var instrument in instruments.OrderBy(x => x.Symbol)) {
            _instruments[instrument.SymbolIndex] = instrument;
            _comboBox.Items.Add(instrument);
        }

        if (_comboBox.Items.Count > 0)
            _comboBox.SelectedIndex = 0;
    }

    /// <summary>
    /// Carrega instrumentos a partir de um dicionário Symbol -> SymbolIndex
    /// </summary>
    public void LoadInstruments(IReadOnlyDictionary<short, string> symbolNames) {
        var instruments = symbolNames.Select(kvp => new InstrumentItem {
            SymbolIndex = kvp.Key,
            Symbol = kvp.Value,
            Name = string.Empty // Nome vazio por enquanto
        });

        LoadInstruments(instruments);
    }

    /// <summary>
    /// Atualiza a lista de instrumentos mantendo a seleção atual
    /// </summary>
    public void RefreshInstruments(IEnumerable<InstrumentItem> instruments) {
        var currentSelection = SelectedSymbolIndex;
        LoadInstruments(instruments);

        if (currentSelection >= 0)
            SelectedSymbolIndex = currentSelection;
    }

    /// <summary>
    /// Limpa a seleção atual
    /// </summary>
    public void ClearSelection() {
        if (InvokeRequired) {
            Invoke(ClearSelection);
            return;
        }
        _comboBox.SelectedIndex = -1;
    }
}

/// <summary>
/// Item do instrumento para o ComboBox
/// </summary>
public class InstrumentItem
{
    public short SymbolIndex { get; set; }
    public required string Symbol { get; set; }
    public string Name { get; set; } = string.Empty;

    public override string ToString()
    {
        return string.IsNullOrEmpty(Name) ? Symbol : $"{Symbol} - {Name}";
    }
}
