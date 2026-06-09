namespace InventoryStore.Tray;

public class ResetPasswordForm : Form
{
    private readonly TextBox _passwordBox;
    private readonly TextBox _confirmBox;
    private readonly Button  _okButton;
    private readonly Button  _cancelButton;

    public string NewPassword => _passwordBox.Text;

    public ResetPasswordForm()
    {
        Text            = "Reset Admin Password";
        Size            = new Size(360, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel
        {
            Dock      = DockStyle.Fill,
            Padding   = new Padding(16),
            RowCount  = 4,
            ColumnCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "New Password:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
        _passwordBox = new TextBox { PasswordChar = '●', Dock = DockStyle.Fill };
        layout.Controls.Add(_passwordBox, 1, 0);

        layout.Controls.Add(new Label { Text = "Confirm:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
        _confirmBox = new TextBox { PasswordChar = '●', Dock = DockStyle.Fill };
        layout.Controls.Add(_confirmBox, 1, 1);

        var buttonPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        _cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        _okButton     = new Button { Text = "Reset",  DialogResult = DialogResult.None };
        _okButton.Click += OnOkClick;
        buttonPanel.Controls.Add(_cancelButton);
        buttonPanel.Controls.Add(_okButton);

        layout.SetColumnSpan(buttonPanel, 2);
        layout.Controls.Add(buttonPanel, 0, 3);

        Controls.Add(layout);
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        if (_passwordBox.Text.Length < 8)
        {
            MessageBox.Show("Password must be at least 8 characters.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_passwordBox.Text != _confirmBox.Text)
        {
            MessageBox.Show("Passwords do not match.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
