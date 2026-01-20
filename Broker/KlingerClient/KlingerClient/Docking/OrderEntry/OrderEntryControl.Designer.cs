using KlingerClient.Components;

namespace KlingerBroker.Ui.Docking.OrderEntry
{
    partial class OrderEntryControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code
        private Label _symbolLabel;

        private void InitializeComponent() {
            _symbolLabel = new Label();
            _sideLabel = new Label();
            _sideCombo = new ComboBox();
            _symbolSelector = new SelectInstrumentControl();
            _panel = new TableLayoutPanel();
            _qtyLabel = new Label();
            _qty = new NumericUpDown();
            _priceLabel = new Label();
            _price = new NumericUpDown();
            _send = new Button();
            _panel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_qty).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_price).BeginInit();
            SuspendLayout();
            // 
            // _symbolLabel
            // 
            _symbolLabel.Anchor = AnchorStyles.Left;
            _symbolLabel.AutoSize = true;
            _symbolLabel.ForeColor = Color.White;
            _symbolLabel.Location = new Point(11, 16);
            _symbolLabel.Name = "_symbolLabel";
            _symbolLabel.Size = new Size(60, 20);
            _symbolLabel.TabIndex = 0;
            _symbolLabel.Text = "Symbol";
            // 
            // _sideLabel
            // 
            _sideLabel.Anchor = AnchorStyles.Left;
            _sideLabel.AutoSize = true;
            _sideLabel.ForeColor = Color.White;
            _sideLabel.Location = new Point(264, 18);
            _sideLabel.Name = "_sideLabel";
            _sideLabel.Size = new Size(29, 15);
            _sideLabel.TabIndex = 2;
            _sideLabel.Text = "Side";
            // 
            // _sideCombo
            // 
            _sideCombo.Location = new Point(402, 11);
            _sideCombo.Name = "_sideCombo";
            _sideCombo.Size = new Size(112, 23);
            _sideCombo.TabIndex = 3;
            // 
            // _symbolSelector
            // 
            _symbolSelector.BackColor = Color.FromArgb(37, 37, 38);
            _panel.SetColumnSpan(_symbolSelector, 2);
            _symbolSelector.ForeColor = Color.White;
            _symbolSelector.Location = new Point(11, 11);
            _symbolSelector.MinimumSize = new Size(200, 30);
            _symbolSelector.Name = "_symbolSelector";
            _symbolSelector.Size = new Size(201, 30);
            _symbolSelector.TabIndex = 0;
            // 
            // _panel
            // 
            _panel.BackColor = Color.FromArgb(37, 37, 38);
            _panel.ColumnCount = 5;
            _panel.ColumnStyles.Add(new ColumnStyle());
            _panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 43F));
            _panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138F));
            _panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
            _panel.Controls.Add(_symbolSelector, 0, 0);
            _panel.Controls.Add(_qtyLabel, 0, 2);
            _panel.Controls.Add(_qty, 1, 2);
            _panel.Controls.Add(_priceLabel, 2, 2);
            _panel.Controls.Add(_price, 3, 2);
            _panel.Controls.Add(_send, 4, 2);
            _panel.Controls.Add(_sideCombo, 4, 0);
            _panel.Controls.Add(_sideLabel, 3, 0);
            _panel.Dock = DockStyle.Fill;
            _panel.Location = new Point(0, 0);
            _panel.Name = "_panel";
            _panel.Padding = new Padding(8);
            _panel.RowCount = 5;
            _panel.RowStyles.Add(new RowStyle());
            _panel.RowStyles.Add(new RowStyle());
            _panel.RowStyles.Add(new RowStyle());
            _panel.RowStyles.Add(new RowStyle());
            _panel.RowStyles.Add(new RowStyle());
            _panel.Size = new Size(525, 87);
            _panel.TabIndex = 0;
            // 
            // _qtyLabel
            // 
            _qtyLabel.Anchor = AnchorStyles.Left;
            _qtyLabel.AutoSize = true;
            _qtyLabel.ForeColor = Color.White;
            _qtyLabel.Location = new Point(11, 51);
            _qtyLabel.Name = "_qtyLabel";
            _qtyLabel.Size = new Size(53, 15);
            _qtyLabel.TabIndex = 4;
            _qtyLabel.Text = "Quantity";
            // 
            // _qty
            // 
            _qty.Location = new Point(70, 47);
            _qty.Name = "_qty";
            _qty.Size = new Size(142, 23);
            _qty.TabIndex = 5;
            // 
            // _priceLabel
            // 
            _priceLabel.Anchor = AnchorStyles.Left;
            _priceLabel.AutoSize = true;
            _priceLabel.ForeColor = Color.White;
            _priceLabel.Location = new Point(221, 51);
            _priceLabel.Name = "_priceLabel";
            _priceLabel.Size = new Size(33, 15);
            _priceLabel.TabIndex = 6;
            _priceLabel.Text = "Price";
            // 
            // _price
            // 
            _price.Location = new Point(264, 47);
            _price.Name = "_price";
            _price.Size = new Size(120, 23);
            _price.TabIndex = 7;
            // 
            // _send
            // 
            _send.Location = new Point(402, 47);
            _send.Name = "_send";
            _send.Size = new Size(66, 23);
            _send.TabIndex = 9;
            // 
            // OrderEntryControl
            // 
            Controls.Add(_panel);
            Name = "OrderEntryControl";
            Size = new Size(525, 87);
            _panel.ResumeLayout(false);
            _panel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)_qty).EndInit();
            ((System.ComponentModel.ISupportInitialize)_price).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Label _sideLabel;
        private ComboBox _sideCombo;
        private SelectInstrumentControl _symbolSelector;
        private TableLayoutPanel _panel;
        private Label _qtyLabel;
        private NumericUpDown _qty;
        private Label _priceLabel;
        private NumericUpDown _price;
        private Button _send;
    }
}
