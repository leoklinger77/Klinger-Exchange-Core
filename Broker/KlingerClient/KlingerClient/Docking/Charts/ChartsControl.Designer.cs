using KlingerClient.Components;

namespace KlingerClient.Docking.Charts
{
    partial class ChartsControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Component Designer generated code

        private Panel _topPanel;
        private Panel _chartPanel;
        private SelectInstrumentControl _symbolSelector;
        private Button _tf1sBtn;
        private Button _tf5sBtn;
        private Button _tf15sBtn;
        private Button _tf1mBtn;
        private Button _tf5mBtn;
        private Button _candleBtn;
        private Button _lineBtn;
        private Button _areaBtn;
        private Label _separatorLabel;
        private Label _separatorLabel2;

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            _topPanel = new Panel();
            _chartPanel = new Panel();
            _symbolSelector = new SelectInstrumentControl();
            _tf1sBtn = new Button();
            _tf5sBtn = new Button();
            _tf15sBtn = new Button();
            _tf1mBtn = new Button();
            _tf5mBtn = new Button();
            _candleBtn = new Button();
            _lineBtn = new Button();
            _areaBtn = new Button();
            _separatorLabel = new Label();
            _separatorLabel2 = new Label();
            _topPanel.SuspendLayout();
            SuspendLayout();
            // 
            // _topPanel
            // 
            _topPanel.BackColor = Color.FromArgb(37, 37, 38);
            _topPanel.Controls.Add(_areaBtn);
            _topPanel.Controls.Add(_lineBtn);
            _topPanel.Controls.Add(_candleBtn);
            _topPanel.Controls.Add(_separatorLabel2);
            _topPanel.Controls.Add(_tf5mBtn);
            _topPanel.Controls.Add(_tf1mBtn);
            _topPanel.Controls.Add(_tf15sBtn);
            _topPanel.Controls.Add(_tf5sBtn);
            _topPanel.Controls.Add(_tf1sBtn);
            _topPanel.Controls.Add(_separatorLabel);
            _topPanel.Controls.Add(_symbolSelector);
            _topPanel.Dock = DockStyle.Top;
            _topPanel.Location = new Point(0, 0);
            _topPanel.Name = "_topPanel";
            _topPanel.Padding = new Padding(5);
            _topPanel.Size = new Size(800, 38);
            _topPanel.TabIndex = 0;
            // 
            // _symbolSelector
            // 
            _symbolSelector.BackColor = Color.FromArgb(37, 37, 38);
            _symbolSelector.ForeColor = Color.White;
            _symbolSelector.Location = new Point(5, 5);
            _symbolSelector.MinimumSize = new Size(140, 28);
            _symbolSelector.Name = "_symbolSelector";
            _symbolSelector.Size = new Size(160, 28);
            _symbolSelector.TabIndex = 0;
            // 
            // _separatorLabel
            // 
            _separatorLabel.AutoSize = true;
            _separatorLabel.ForeColor = Color.FromArgb(80, 80, 80);
            _separatorLabel.Location = new Point(170, 10);
            _separatorLabel.Name = "_separatorLabel";
            _separatorLabel.Size = new Size(10, 15);
            _separatorLabel.TabIndex = 1;
            _separatorLabel.Text = "|";
            // 
            // _tf1sBtn
            // 
            _tf1sBtn.BackColor = Color.FromArgb(45, 45, 48);
            _tf1sBtn.FlatAppearance.BorderSize = 0;
            _tf1sBtn.FlatStyle = FlatStyle.Flat;
            _tf1sBtn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _tf1sBtn.ForeColor = Color.FromArgb(180, 180, 180);
            _tf1sBtn.Location = new Point(185, 5);
            _tf1sBtn.Name = "_tf1sBtn";
            _tf1sBtn.Size = new Size(35, 26);
            _tf1sBtn.TabIndex = 2;
            _tf1sBtn.Text = "1s";
            _tf1sBtn.UseVisualStyleBackColor = false;
            // 
            // _tf5sBtn
            // 
            _tf5sBtn.BackColor = Color.FromArgb(45, 45, 48);
            _tf5sBtn.FlatAppearance.BorderSize = 0;
            _tf5sBtn.FlatStyle = FlatStyle.Flat;
            _tf5sBtn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _tf5sBtn.ForeColor = Color.FromArgb(180, 180, 180);
            _tf5sBtn.Location = new Point(222, 5);
            _tf5sBtn.Name = "_tf5sBtn";
            _tf5sBtn.Size = new Size(35, 26);
            _tf5sBtn.TabIndex = 3;
            _tf5sBtn.Text = "5s";
            _tf5sBtn.UseVisualStyleBackColor = false;
            // 
            // _tf15sBtn
            // 
            _tf15sBtn.BackColor = Color.FromArgb(45, 45, 48);
            _tf15sBtn.FlatAppearance.BorderSize = 0;
            _tf15sBtn.FlatStyle = FlatStyle.Flat;
            _tf15sBtn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _tf15sBtn.ForeColor = Color.FromArgb(180, 180, 180);
            _tf15sBtn.Location = new Point(259, 5);
            _tf15sBtn.Name = "_tf15sBtn";
            _tf15sBtn.Size = new Size(38, 26);
            _tf15sBtn.TabIndex = 4;
            _tf15sBtn.Text = "15s";
            _tf15sBtn.UseVisualStyleBackColor = false;
            // 
            // _tf1mBtn
            // 
            _tf1mBtn.BackColor = Color.FromArgb(45, 45, 48);
            _tf1mBtn.FlatAppearance.BorderSize = 0;
            _tf1mBtn.FlatStyle = FlatStyle.Flat;
            _tf1mBtn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _tf1mBtn.ForeColor = Color.FromArgb(180, 180, 180);
            _tf1mBtn.Location = new Point(299, 5);
            _tf1mBtn.Name = "_tf1mBtn";
            _tf1mBtn.Size = new Size(38, 26);
            _tf1mBtn.TabIndex = 5;
            _tf1mBtn.Text = "1m";
            _tf1mBtn.UseVisualStyleBackColor = false;
            // 
            // _tf5mBtn
            // 
            _tf5mBtn.BackColor = Color.FromArgb(45, 45, 48);
            _tf5mBtn.FlatAppearance.BorderSize = 0;
            _tf5mBtn.FlatStyle = FlatStyle.Flat;
            _tf5mBtn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _tf5mBtn.ForeColor = Color.FromArgb(180, 180, 180);
            _tf5mBtn.Location = new Point(339, 5);
            _tf5mBtn.Name = "_tf5mBtn";
            _tf5mBtn.Size = new Size(38, 26);
            _tf5mBtn.TabIndex = 6;
            _tf5mBtn.Text = "5m";
            _tf5mBtn.UseVisualStyleBackColor = false;
            // 
            // _separatorLabel2
            // 
            _separatorLabel2.AutoSize = true;
            _separatorLabel2.ForeColor = Color.FromArgb(80, 80, 80);
            _separatorLabel2.Location = new Point(382, 10);
            _separatorLabel2.Name = "_separatorLabel2";
            _separatorLabel2.Size = new Size(10, 15);
            _separatorLabel2.TabIndex = 7;
            _separatorLabel2.Text = "|";
            // 
            // _candleBtn
            // 
            _candleBtn.BackColor = Color.FromArgb(60, 60, 65);
            _candleBtn.FlatAppearance.BorderSize = 0;
            _candleBtn.FlatStyle = FlatStyle.Flat;
            _candleBtn.Font = new Font("Segoe UI", 8F);
            _candleBtn.ForeColor = Color.FromArgb(180, 180, 180);
            _candleBtn.Location = new Point(397, 5);
            _candleBtn.Name = "_candleBtn";
            _candleBtn.Size = new Size(55, 26);
            _candleBtn.TabIndex = 8;
            _candleBtn.Text = "Candle";
            _candleBtn.UseVisualStyleBackColor = false;
            // 
            // _lineBtn
            // 
            _lineBtn.BackColor = Color.FromArgb(45, 45, 48);
            _lineBtn.FlatAppearance.BorderSize = 0;
            _lineBtn.FlatStyle = FlatStyle.Flat;
            _lineBtn.Font = new Font("Segoe UI", 8F);
            _lineBtn.ForeColor = Color.FromArgb(180, 180, 180);
            _lineBtn.Location = new Point(454, 5);
            _lineBtn.Name = "_lineBtn";
            _lineBtn.Size = new Size(45, 26);
            _lineBtn.TabIndex = 9;
            _lineBtn.Text = "Line";
            _lineBtn.UseVisualStyleBackColor = false;
            // 
            // _areaBtn
            // 
            _areaBtn.BackColor = Color.FromArgb(45, 45, 48);
            _areaBtn.FlatAppearance.BorderSize = 0;
            _areaBtn.FlatStyle = FlatStyle.Flat;
            _areaBtn.Font = new Font("Segoe UI", 8F);
            _areaBtn.ForeColor = Color.FromArgb(180, 180, 180);
            _areaBtn.Location = new Point(501, 5);
            _areaBtn.Name = "_areaBtn";
            _areaBtn.Size = new Size(45, 26);
            _areaBtn.TabIndex = 10;
            _areaBtn.Text = "Area";
            _areaBtn.UseVisualStyleBackColor = false;
            // 
            // _chartPanel
            // 
            _chartPanel.BackColor = Color.FromArgb(22, 22, 26);
            _chartPanel.Dock = DockStyle.Fill;
            _chartPanel.Location = new Point(0, 38);
            _chartPanel.Name = "_chartPanel";
            _chartPanel.Size = new Size(800, 462);
            _chartPanel.TabIndex = 1;
            // 
            // ChartsControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(22, 22, 26);
            Controls.Add(_chartPanel);
            Controls.Add(_topPanel);
            ForeColor = Color.White;
            Name = "ChartsControl";
            Size = new Size(800, 500);
            _topPanel.ResumeLayout(false);
            _topPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
    }
}
