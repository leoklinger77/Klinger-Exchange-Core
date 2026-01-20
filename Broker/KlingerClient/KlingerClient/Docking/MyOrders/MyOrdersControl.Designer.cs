namespace KlingerBroker.Ui.Docking.MyOrders
{
    partial class MyOrdersControl
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

        private void InitializeComponent() {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            _grid = new DataGridView();
            _colTime = new DataGridViewTextBoxColumn();
            _colSymbol = new DataGridViewTextBoxColumn();
            _colClOrdId = new DataGridViewTextBoxColumn();
            _colOrderId = new DataGridViewTextBoxColumn();
            _colExecType = new DataGridViewTextBoxColumn();
            _colOrdStatus = new DataGridViewTextBoxColumn();
            _colCumQty = new DataGridViewTextBoxColumn();
            _colLeavesQty = new DataGridViewTextBoxColumn();
            _colLastPx = new DataGridViewTextBoxColumn();
            _colLastQty = new DataGridViewTextBoxColumn();
            _colText = new DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)_grid).BeginInit();
            SuspendLayout();
            // 
            // _grid
            // 
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.BackgroundColor = Color.FromArgb(30, 30, 30);
            _grid.BorderStyle = BorderStyle.None;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(27, 27, 28);
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = Color.FromArgb(204, 204, 204);
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            _grid.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            _grid.ColumnHeadersHeight = 22;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.Columns.AddRange(new DataGridViewColumn[] { _colTime, _colSymbol, _colClOrdId, _colOrderId, _colExecType, _colOrdStatus, _colCumQty, _colLeavesQty, _colLastPx, _colLastQty, _colText });
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(37, 37, 38);
            dataGridViewCellStyle2.Font = new Font("Consolas", 9F);
            dataGridViewCellStyle2.ForeColor = Color.White;
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(51, 153, 255);
            dataGridViewCellStyle2.SelectionForeColor = Color.White;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            _grid.DefaultCellStyle = dataGridViewCellStyle2;
            _grid.Dock = DockStyle.Fill;
            _grid.EnableHeadersVisualStyles = false;
            _grid.GridColor = Color.FromArgb(45, 45, 48);
            _grid.Location = new Point(0, 0);
            _grid.MultiSelect = false;
            _grid.Name = "_grid";
            _grid.ReadOnly = true;
            _grid.RowHeadersVisible = false;
            _grid.RowHeadersWidth = 51;
            _grid.RowTemplate.Height = 22;
            _grid.Size = new Size(907, 400);
            _grid.TabIndex = 0;
            // 
            // _colTime
            // 
            _colTime.HeaderText = "Time";
            _colTime.Name = "_colTime";
            _colTime.ReadOnly = true;
            _colTime.Width = 90;
            // 
            // _colSymbol
            // 
            _colSymbol.HeaderText = "Symbol";
            _colSymbol.Name = "_colSymbol";
            _colSymbol.ReadOnly = true;
            _colSymbol.Width = 70;
            // 
            // _colClOrdId
            // 
            _colClOrdId.HeaderText = "ClOrdID";
            _colClOrdId.Name = "_colClOrdId";
            _colClOrdId.ReadOnly = true;
            _colClOrdId.Width = 120;
            // 
            // _colOrderId
            // 
            _colOrderId.HeaderText = "OrderID";
            _colOrderId.Name = "_colOrderId";
            _colOrderId.ReadOnly = true;
            _colOrderId.Width = 80;
            // 
            // _colExecType
            // 
            _colExecType.HeaderText = "ExecType";
            _colExecType.Name = "_colExecType";
            _colExecType.ReadOnly = true;
            _colExecType.Width = 70;
            // 
            // _colOrdStatus
            // 
            _colOrdStatus.HeaderText = "Status";
            _colOrdStatus.Name = "_colOrdStatus";
            _colOrdStatus.ReadOnly = true;
            _colOrdStatus.Width = 70;
            // 
            // _colCumQty
            // 
            _colCumQty.HeaderText = "Filled";
            _colCumQty.Name = "_colCumQty";
            _colCumQty.ReadOnly = true;
            _colCumQty.Width = 60;
            // 
            // _colLeavesQty
            // 
            _colLeavesQty.HeaderText = "Leaves";
            _colLeavesQty.Name = "_colLeavesQty";
            _colLeavesQty.ReadOnly = true;
            _colLeavesQty.Width = 60;
            // 
            // _colLastPx
            // 
            _colLastPx.HeaderText = "LastPx";
            _colLastPx.Name = "_colLastPx";
            _colLastPx.ReadOnly = true;
            _colLastPx.Width = 70;
            // 
            // _colLastQty
            // 
            _colLastQty.HeaderText = "LastQty";
            _colLastQty.Name = "_colLastQty";
            _colLastQty.ReadOnly = true;
            _colLastQty.Width = 60;
            // 
            // _colText
            // 
            _colText.HeaderText = "Text";
            _colText.Name = "_colText";
            _colText.ReadOnly = true;
            _colText.Width = 150;
            // 
            // MyOrdersControl
            // 
            BackColor = Color.FromArgb(37, 37, 38);
            Controls.Add(_grid);
            ForeColor = Color.White;
            Name = "MyOrdersControl";
            Size = new Size(907, 400);
            ((System.ComponentModel.ISupportInitialize)_grid).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private DataGridViewTextBoxColumn _colTime;
        private DataGridViewTextBoxColumn _colSymbol;
        private DataGridViewTextBoxColumn _colClOrdId;
        private DataGridViewTextBoxColumn _colOrderId;
        private DataGridViewTextBoxColumn _colExecType;
        private DataGridViewTextBoxColumn _colOrdStatus;
        private DataGridViewTextBoxColumn _colCumQty;
        private DataGridViewTextBoxColumn _colLeavesQty;
        private DataGridViewTextBoxColumn _colLastPx;
        private DataGridViewTextBoxColumn _colLastQty;
        private DataGridViewTextBoxColumn _colText;
    }
}
