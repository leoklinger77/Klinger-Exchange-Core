using KlingerClient.Components;

namespace KlingerBroker.Ui.Docking.OrderBook
{
    partial class OrderBookControl
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

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private DataGridView _grid;
        private SelectInstrumentControl _symbolSelector;
        private Panel _topPanel;

        private void InitializeComponent()
        {
            _grid = new DataGridView();
            _symbolSelector = new SelectInstrumentControl();
            _topPanel = new Panel();
            ((System.ComponentModel.ISupportInitialize)_grid).BeginInit();
            _topPanel.SuspendLayout();
            SuspendLayout();
            // 
            // _topPanel
            // 
            _topPanel.Controls.Add(_symbolSelector);
            _topPanel.Dock = DockStyle.Top;
            _topPanel.Location = new Point(0, 0);
            _topPanel.Name = "_topPanel";
            _topPanel.Padding = new Padding(5);
            _topPanel.Size = new Size(600, 35);
            _topPanel.TabIndex = 0;
            _topPanel.BackColor = Color.FromArgb(37, 37, 38);
            // 
            // _symbolSelector
            // 
            _symbolSelector.Dock = DockStyle.Fill;
            _symbolSelector.Location = new Point(5, 5);
            _symbolSelector.Name = "_symbolSelector";
            _symbolSelector.Size = new Size(590, 25);
            _symbolSelector.TabIndex = 0;
            // 
            // _grid
            // 
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _grid.Dock = DockStyle.Fill;
            _grid.Location = new Point(0, 35);
            _grid.Name = "_grid";
            _grid.RowHeadersWidth = 51;
            _grid.Size = new Size(600, 365);
            _grid.TabIndex = 1;
            // 
            // OrderBookControl
            // 
            Controls.Add(_grid);
            Controls.Add(_topPanel);
            Name = "OrderBookControl";
            Size = new Size(600, 400);
            ((System.ComponentModel.ISupportInitialize)_grid).EndInit();
            _topPanel.ResumeLayout(false);
            _topPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
    }
}
