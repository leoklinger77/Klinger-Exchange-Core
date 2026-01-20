using WeifenLuo.WinFormsUI.Docking;

namespace KlingerClient {
    partial class Main {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private MenuStrip _menuStrip;
        private ToolStripMenuItem _viewMenu;
        private ToolStripMenuItem _viewOrderEntry;
        private ToolStripMenuItem _viewOrderBook;
        private ToolStripMenuItem _viewVerticalBook;
        private ToolStripMenuItem _viewMyOrders;
        private ToolStripMenuItem _viewMarketwatch;
        private ToolStripMenuItem _viewCharts;
        private DockPanel _dockPanel;

        private void InitializeComponent() {
            _menuStrip = new MenuStrip();
            _viewMenu = new ToolStripMenuItem();
            _viewOrderEntry = new ToolStripMenuItem();
            _viewOrderBook = new ToolStripMenuItem();
            _viewVerticalBook = new ToolStripMenuItem();
            _viewMyOrders = new ToolStripMenuItem();
            _viewMarketwatch = new ToolStripMenuItem();
            _viewCharts = new ToolStripMenuItem();
            _dockPanel = new DockPanel();
            _menuStrip.SuspendLayout();
            SuspendLayout();
            // 
            // _menuStrip
            // 
            _menuStrip.ImageScalingSize = new Size(20, 20);
            _menuStrip.Items.AddRange(new ToolStripItem[] { _viewMenu });
            _menuStrip.Location = new Point(0, 0);
            _menuStrip.Name = "_menuStrip";
            _menuStrip.Size = new Size(1542, 24);
            _menuStrip.TabIndex = 0;
            _menuStrip.Text = "menuStrip";
            // 
            // _viewMenu
            // 
            _viewMenu.DropDownItems.AddRange(new ToolStripItem[] { _viewOrderEntry, _viewOrderBook, _viewVerticalBook, _viewMyOrders, _viewMarketwatch, _viewCharts });
            _viewMenu.Name = "_viewMenu";
            _viewMenu.Size = new Size(44, 20);
            _viewMenu.Text = "View";
            // 
            // _viewOrderEntry
            // 
            _viewOrderEntry.Name = "_viewOrderEntry";
            _viewOrderEntry.Size = new Size(181, 22);
            _viewOrderEntry.Text = "Order Entry";
            // 
            // _viewOrderBook
            // 
            _viewOrderBook.Name = "_viewOrderBook";
            _viewOrderBook.Size = new Size(181, 22);
            _viewOrderBook.Text = "Order Book";
            // 
            // _viewVerticalBook
            // 
            _viewVerticalBook.Name = "_viewVerticalBook";
            _viewVerticalBook.Size = new Size(181, 22);
            _viewVerticalBook.Text = "Vertical Book (DOM)";
            // 
            // _viewMyOrders
            // 
            _viewMyOrders.Name = "_viewMyOrders";
            _viewMyOrders.Size = new Size(181, 22);
            _viewMyOrders.Text = "MyOrders";
            // 
            // _viewMarketwatch
            // 
            _viewMarketwatch.Name = "_viewMarketwatch";
            _viewMarketwatch.Size = new Size(181, 22);
            _viewMarketwatch.Text = "Marketwatch";
            // 
            // _viewCharts
            // 
            _viewCharts.Name = "_viewCharts";
            _viewCharts.Size = new Size(181, 22);
            _viewCharts.Text = "Charts";
            // 
            // _dockPanel
            // 
            _dockPanel.Dock = DockStyle.Fill;
            _dockPanel.Location = new Point(0, 24);
            _dockPanel.Name = "_dockPanel";
            _dockPanel.Size = new Size(1542, 799);
            _dockPanel.TabIndex = 1;
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1542, 823);
            Controls.Add(_dockPanel);
            Controls.Add(_menuStrip);
            MainMenuStrip = _menuStrip;
            Name = "Main";
            Text = "Klinger Trader";
            _menuStrip.ResumeLayout(false);
            _menuStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
