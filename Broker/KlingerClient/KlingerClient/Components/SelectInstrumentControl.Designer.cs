namespace KlingerClient.Components
{
    partial class SelectInstrumentControl
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
        private ComboBox _comboBox;
        private Label _label;

        private void InitializeComponent() {
            _label = new Label();
            _comboBox = new ComboBox();
            SuspendLayout();
            // 
            // _label
            // 
            _label.AutoSize = true;
            _label.Dock = DockStyle.Left;
            _label.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _label.ForeColor = Color.FromArgb(204, 204, 204);
            _label.Location = new Point(0, 0);
            _label.Name = "_label";
            _label.Padding = new Padding(0, 0, 8, 0);
            _label.Size = new Size(59, 15);
            _label.TabIndex = 0;
            _label.Text = "Symbol:";
            _label.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _comboBox
            // 
            _comboBox.BackColor = Color.FromArgb(45, 45, 48);
            _comboBox.Dock = DockStyle.Fill;
            _comboBox.DrawMode = DrawMode.OwnerDrawFixed;
            _comboBox.DropDownHeight = 400;
            _comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _comboBox.FlatStyle = FlatStyle.Flat;
            _comboBox.Font = new Font("Segoe UI", 9F);
            _comboBox.ForeColor = Color.White;
            _comboBox.FormattingEnabled = true;
            _comboBox.IntegralHeight = false;
            _comboBox.Location = new Point(59, 0);
            _comboBox.Name = "_comboBox";
            _comboBox.Size = new Size(191, 24);
            _comboBox.TabIndex = 1;
            // 
            // SelectInstrumentControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(37, 37, 38);
            Controls.Add(_comboBox);
            Controls.Add(_label);
            ForeColor = Color.White;
            MinimumSize = new Size(200, 30);
            Name = "SelectInstrumentControl";
            Size = new Size(250, 30);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
