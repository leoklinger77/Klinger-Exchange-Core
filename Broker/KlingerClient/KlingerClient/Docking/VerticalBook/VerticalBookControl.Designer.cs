using KlingerClient.Components;

namespace KlingerBroker.Ui.Docking
{
    partial class VerticalBookControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Component Designer generated code

        private Panel _ladderPanel;
        private SelectInstrumentControl _symbolSelector;
        private Label _spreadLabel;
        private Panel _topPanel;
        private Panel _leftPanel;
        private TextBox _qtyTextBox;
        private Button _qty1Btn;
        private Button _qty5Btn;
        private Button _qty10Btn;
        private Button _qty25Btn;
        private Button _qty50Btn;
        private Button _qty100Btn;
        private Button _cancelBuysBtn;
        private Button _cancelSellsBtn;
        private Button _cancelAllBtn;
        private Label _qtyLabel;

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            _ladderPanel = new Panel();
            _symbolSelector = new SelectInstrumentControl();
            _spreadLabel = new Label();
            _topPanel = new Panel();
            _leftPanel = new Panel();
            _cancelAllBtn = new Button();
            _cancelSellsBtn = new Button();
            _cancelBuysBtn = new Button();
            _qty100Btn = new Button();
            _qty50Btn = new Button();
            _qty25Btn = new Button();
            _qty10Btn = new Button();
            _qty5Btn = new Button();
            _qty1Btn = new Button();
            _qtyTextBox = new TextBox();
            _qtyLabel = new Label();
            _topPanel.SuspendLayout();
            _leftPanel.SuspendLayout();
            SuspendLayout();
            // 
            // _ladderPanel
            // 
            _ladderPanel.BackColor = Color.FromArgb(30, 30, 30);
            _ladderPanel.Dock = DockStyle.Fill;
            _ladderPanel.Location = new Point(95, 55);
            _ladderPanel.Name = "_ladderPanel";
            _ladderPanel.Size = new Size(405, 777);
            _ladderPanel.TabIndex = 2;
            // 
            // _symbolSelector
            // 
            _symbolSelector.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _symbolSelector.BackColor = Color.FromArgb(37, 37, 38);
            _symbolSelector.ForeColor = Color.White;
            _symbolSelector.Location = new Point(5, 25);
            _symbolSelector.MinimumSize = new Size(140, 25);
            _symbolSelector.Name = "_symbolSelector";
            _symbolSelector.Size = new Size(490, 25);
            _symbolSelector.TabIndex = 1;
            // 
            // _spreadLabel
            // 
            _spreadLabel.AutoSize = true;
            _spreadLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _spreadLabel.ForeColor = Color.FromArgb(180, 180, 180);
            _spreadLabel.Location = new Point(5, 5);
            _spreadLabel.Name = "_spreadLabel";
            _spreadLabel.Size = new Size(62, 15);
            _spreadLabel.TabIndex = 0;
            _spreadLabel.Text = "Spread: --";
            // 
            // _topPanel
            // 
            _topPanel.BackColor = Color.FromArgb(37, 37, 38);
            _topPanel.Controls.Add(_symbolSelector);
            _topPanel.Controls.Add(_spreadLabel);
            _topPanel.Dock = DockStyle.Top;
            _topPanel.Location = new Point(0, 0);
            _topPanel.Name = "_topPanel";
            _topPanel.Padding = new Padding(5);
            _topPanel.Size = new Size(500, 55);
            _topPanel.TabIndex = 0;
            // 
            // _leftPanel
            // 
            _leftPanel.BackColor = Color.FromArgb(45, 45, 48);
            _leftPanel.Controls.Add(_cancelAllBtn);
            _leftPanel.Controls.Add(_cancelSellsBtn);
            _leftPanel.Controls.Add(_cancelBuysBtn);
            _leftPanel.Controls.Add(_qty100Btn);
            _leftPanel.Controls.Add(_qty50Btn);
            _leftPanel.Controls.Add(_qty25Btn);
            _leftPanel.Controls.Add(_qty10Btn);
            _leftPanel.Controls.Add(_qty5Btn);
            _leftPanel.Controls.Add(_qty1Btn);
            _leftPanel.Controls.Add(_qtyTextBox);
            _leftPanel.Controls.Add(_qtyLabel);
            _leftPanel.Dock = DockStyle.Left;
            _leftPanel.Location = new Point(0, 55);
            _leftPanel.Name = "_leftPanel";
            _leftPanel.Size = new Size(95, 777);
            _leftPanel.TabIndex = 1;
            // 
            // _cancelAllBtn
            // 
            _cancelAllBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _cancelAllBtn.BackColor = Color.FromArgb(180, 100, 0);
            _cancelAllBtn.FlatAppearance.BorderSize = 0;
            _cancelAllBtn.FlatStyle = FlatStyle.Flat;
            _cancelAllBtn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _cancelAllBtn.ForeColor = Color.White;
            _cancelAllBtn.Location = new Point(5, 767);
            _cancelAllBtn.Name = "_cancelAllBtn";
            _cancelAllBtn.Size = new Size(85, 26);
            _cancelAllBtn.TabIndex = 10;
            _cancelAllBtn.Text = "Cancel All";
            _cancelAllBtn.UseVisualStyleBackColor = false;
            // 
            // _cancelSellsBtn
            // 
            _cancelSellsBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _cancelSellsBtn.BackColor = Color.FromArgb(120, 40, 40);
            _cancelSellsBtn.FlatAppearance.BorderSize = 0;
            _cancelSellsBtn.FlatStyle = FlatStyle.Flat;
            _cancelSellsBtn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _cancelSellsBtn.ForeColor = Color.White;
            _cancelSellsBtn.Location = new Point(5, 737);
            _cancelSellsBtn.Name = "_cancelSellsBtn";
            _cancelSellsBtn.Size = new Size(85, 26);
            _cancelSellsBtn.TabIndex = 9;
            _cancelSellsBtn.Text = "Cancel Sells";
            _cancelSellsBtn.UseVisualStyleBackColor = false;
            // 
            // _cancelBuysBtn
            // 
            _cancelBuysBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _cancelBuysBtn.BackColor = Color.FromArgb(40, 80, 140);
            _cancelBuysBtn.FlatAppearance.BorderSize = 0;
            _cancelBuysBtn.FlatStyle = FlatStyle.Flat;
            _cancelBuysBtn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _cancelBuysBtn.ForeColor = Color.White;
            _cancelBuysBtn.Location = new Point(5, 707);
            _cancelBuysBtn.Name = "_cancelBuysBtn";
            _cancelBuysBtn.Size = new Size(85, 26);
            _cancelBuysBtn.TabIndex = 8;
            _cancelBuysBtn.Text = "Cancel Buys";
            _cancelBuysBtn.UseVisualStyleBackColor = false;
            // 
            // _qty100Btn
            // 
            _qty100Btn.BackColor = Color.FromArgb(60, 60, 65);
            _qty100Btn.FlatAppearance.BorderSize = 0;
            _qty100Btn.FlatStyle = FlatStyle.Flat;
            _qty100Btn.ForeColor = Color.White;
            _qty100Btn.Location = new Point(50, 126);
            _qty100Btn.Name = "_qty100Btn";
            _qty100Btn.Size = new Size(40, 26);
            _qty100Btn.TabIndex = 7;
            _qty100Btn.Text = "+100";
            _qty100Btn.UseVisualStyleBackColor = false;
            // 
            // _qty50Btn
            // 
            _qty50Btn.BackColor = Color.FromArgb(60, 60, 65);
            _qty50Btn.FlatAppearance.BorderSize = 0;
            _qty50Btn.FlatStyle = FlatStyle.Flat;
            _qty50Btn.ForeColor = Color.White;
            _qty50Btn.Location = new Point(5, 126);
            _qty50Btn.Name = "_qty50Btn";
            _qty50Btn.Size = new Size(40, 26);
            _qty50Btn.TabIndex = 6;
            _qty50Btn.Text = "+50";
            _qty50Btn.UseVisualStyleBackColor = false;
            // 
            // _qty25Btn
            // 
            _qty25Btn.BackColor = Color.FromArgb(60, 60, 65);
            _qty25Btn.FlatAppearance.BorderSize = 0;
            _qty25Btn.FlatStyle = FlatStyle.Flat;
            _qty25Btn.ForeColor = Color.White;
            _qty25Btn.Location = new Point(50, 94);
            _qty25Btn.Name = "_qty25Btn";
            _qty25Btn.Size = new Size(40, 26);
            _qty25Btn.TabIndex = 5;
            _qty25Btn.Text = "+25";
            _qty25Btn.UseVisualStyleBackColor = false;
            // 
            // _qty10Btn
            // 
            _qty10Btn.BackColor = Color.FromArgb(60, 60, 65);
            _qty10Btn.FlatAppearance.BorderSize = 0;
            _qty10Btn.FlatStyle = FlatStyle.Flat;
            _qty10Btn.ForeColor = Color.White;
            _qty10Btn.Location = new Point(5, 94);
            _qty10Btn.Name = "_qty10Btn";
            _qty10Btn.Size = new Size(40, 26);
            _qty10Btn.TabIndex = 4;
            _qty10Btn.Text = "+10";
            _qty10Btn.UseVisualStyleBackColor = false;
            // 
            // _qty5Btn
            // 
            _qty5Btn.BackColor = Color.FromArgb(60, 60, 65);
            _qty5Btn.FlatAppearance.BorderSize = 0;
            _qty5Btn.FlatStyle = FlatStyle.Flat;
            _qty5Btn.ForeColor = Color.White;
            _qty5Btn.Location = new Point(50, 62);
            _qty5Btn.Name = "_qty5Btn";
            _qty5Btn.Size = new Size(40, 26);
            _qty5Btn.TabIndex = 3;
            _qty5Btn.Text = "+5";
            _qty5Btn.UseVisualStyleBackColor = false;
            // 
            // _qty1Btn
            // 
            _qty1Btn.BackColor = Color.FromArgb(60, 60, 65);
            _qty1Btn.FlatAppearance.BorderSize = 0;
            _qty1Btn.FlatStyle = FlatStyle.Flat;
            _qty1Btn.ForeColor = Color.White;
            _qty1Btn.Location = new Point(5, 62);
            _qty1Btn.Name = "_qty1Btn";
            _qty1Btn.Size = new Size(40, 26);
            _qty1Btn.TabIndex = 2;
            _qty1Btn.Text = "+1";
            _qty1Btn.UseVisualStyleBackColor = false;
            // 
            // _qtyTextBox
            // 
            _qtyTextBox.BackColor = Color.FromArgb(30, 30, 30);
            _qtyTextBox.BorderStyle = BorderStyle.FixedSingle;
            _qtyTextBox.Font = new Font("Consolas", 12F, FontStyle.Bold);
            _qtyTextBox.ForeColor = Color.White;
            _qtyTextBox.Location = new Point(5, 28);
            _qtyTextBox.Name = "_qtyTextBox";
            _qtyTextBox.Size = new Size(85, 26);
            _qtyTextBox.TabIndex = 1;
            _qtyTextBox.Text = "1";
            _qtyTextBox.TextAlign = HorizontalAlignment.Center;
            // 
            // _qtyLabel
            // 
            _qtyLabel.AutoSize = true;
            _qtyLabel.ForeColor = Color.FromArgb(204, 204, 204);
            _qtyLabel.Location = new Point(5, 8);
            _qtyLabel.Name = "_qtyLabel";
            _qtyLabel.Size = new Size(53, 15);
            _qtyLabel.TabIndex = 0;
            _qtyLabel.Text = "Quantity";
            // 
            // VerticalBookControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 30);
            Controls.Add(_ladderPanel);
            Controls.Add(_leftPanel);
            Controls.Add(_topPanel);
            ForeColor = Color.White;
            Name = "VerticalBookControl";
            Size = new Size(500, 832);
            _topPanel.ResumeLayout(false);
            _topPanel.PerformLayout();
            _leftPanel.ResumeLayout(false);
            _leftPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
    }
}
