using KlingerClient.Components;

namespace KlingerClient.Docking.Marketwatch
{
    partial class MarketwatchControl
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
        private void InitializeComponent() {
            _topPanel = new Panel();
            _addAllButton = new Button();
            _addLabel = new Label();
            selectInstrumentControl1 = new SelectInstrumentControl();
            dataGridView1 = new DataGridView();
            _topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // _topPanel
            // 
            _topPanel.BackColor = Color.FromArgb(37, 37, 38);
            _topPanel.Controls.Add(_addAllButton);
            _topPanel.Controls.Add(_addLabel);
            _topPanel.Controls.Add(selectInstrumentControl1);
            _topPanel.Dock = DockStyle.Top;
            _topPanel.Location = new Point(0, 0);
            _topPanel.Name = "_topPanel";
            _topPanel.Padding = new Padding(5);
            _topPanel.Size = new Size(520, 38);
            _topPanel.TabIndex = 0;
            // 
            // _addAllButton
            // 
            _addAllButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _addAllButton.BackColor = Color.FromArgb(45, 45, 48);
            _addAllButton.FlatStyle = FlatStyle.Flat;
            _addAllButton.ForeColor = Color.White;
            _addAllButton.Location = new Point(400, 5);
            _addAllButton.Name = "_addAllButton";
            _addAllButton.Size = new Size(90, 24);
            _addAllButton.TabIndex = 2;
            _addAllButton.Text = "Add All";
            _addAllButton.UseVisualStyleBackColor = false;
            // 
            // _addLabel
            // 
            _addLabel.AutoSize = true;
            _addLabel.ForeColor = Color.White;
            _addLabel.Location = new Point(8, 11);
            _addLabel.Name = "_addLabel";
            _addLabel.Size = new Size(32, 15);
            _addLabel.TabIndex = 0;
            _addLabel.Text = "Add:";
            // 
            // selectInstrumentControl1
            // 
            selectInstrumentControl1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            selectInstrumentControl1.BackColor = Color.FromArgb(37, 37, 38);
            selectInstrumentControl1.ForeColor = Color.White;
            selectInstrumentControl1.Location = new Point(45, 5);
            selectInstrumentControl1.MinimumSize = new Size(200, 28);
            selectInstrumentControl1.Name = "selectInstrumentControl1";
            selectInstrumentControl1.Size = new Size(345, 28);
            selectInstrumentControl1.TabIndex = 1;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(0, 38);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.Size = new Size(520, 362);
            dataGridView1.TabIndex = 1;
            // 
            // MarketwatchControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 30);
            Controls.Add(dataGridView1);
            Controls.Add(_topPanel);
            MinimumSize = new Size(520, 200);
            Name = "MarketwatchControl";
            Size = new Size(520, 400);
            _topPanel.ResumeLayout(false);
            _topPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel _topPanel;
        private Button _addAllButton;
        private Label _addLabel;
        private DataGridView dataGridView1;
        private SelectInstrumentControl selectInstrumentControl1;
    }
}
