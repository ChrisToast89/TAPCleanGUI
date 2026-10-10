// SPDX-License-Identifier: GPL-2.0-or-later
// TAPClean GUI - unofficial front end for TAPClean. See LICENSE and NOTICE.md.

using TAPCleanGui.Core;

namespace TAPCleanGui;

internal sealed class SettingsForm : Form
{
    readonly TextBox _tap = new();
    readonly TextBox _vice = new();

    public string TapCleanExePath => _tap.Text.Trim();
    public string ViceExePath => _vice.Text.Trim();

    public SettingsForm(string tapCleanExe, string viceExe)
    {
        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 5,
            Padding = Dip(new Padding(12)),
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var row = 0; row < 5; row++)
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _tap.Text = tapCleanExe;
        _vice.Text = viceExe;
        _tap.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _vice.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _tap.Margin = Dip(new Padding(0, 6, 8, 6));
        _vice.Margin = Dip(new Padding(0, 6, 8, 6));

        var tapBrowse = BrowseButton();
        var viceBrowse = BrowseButton();
        tapBrowse.Click += (_, _) => BrowseExe(_tap, "Select TAPClean.exe");
        viceBrowse.Click += (_, _) => BrowseExe(_vice, "Select VICE x64sc.exe");

        table.Controls.Add(Caption("TAPClean.exe"), 0, 0);
        table.Controls.Add(_tap, 1, 0);
        table.Controls.Add(tapBrowse, 2, 0);
        table.Controls.Add(DetectionHint(ToolLocator.FindTapClean()), 0, 1);
        table.SetColumnSpan(table.GetControlFromPosition(0, 1)!, 3);
        table.Controls.Add(Caption("VICE x64sc.exe"), 0, 2);
        table.Controls.Add(_vice, 1, 2);
        table.Controls.Add(viceBrowse, 2, 2);
        table.Controls.Add(DetectionHint(ToolLocator.FindVice()), 0, 3);
        table.SetColumnSpan(table.GetControlFromPosition(0, 3)!, 3);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            Margin = new Padding(0),
            Padding = new Padding(0, Dip(4), 0, 0),
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        ApplyButtonChrome(ok);
        ApplyButtonChrome(cancel);
        ok.Margin = new Padding(Dip(8), Dip(8), 0, 0);
        cancel.Margin = new Padding(0, Dip(8), 0, 0);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        table.Controls.Add(buttons, 0, 4);
        table.SetColumnSpan(buttons, 3);

        Controls.Add(table);
        var width = Dip(700);
        var preferred = table.GetPreferredSize(new Size(width, 0));
        ClientSize = new Size(width, preferred.Height);
        MinimumSize = SizeFromClientSize(new Size(Dip(560), preferred.Height));
        AcceptButton = ok;
        CancelButton = cancel;
    }

    Label DetectionHint(string? detected)
    {
        return new Label
        {
            Text = string.IsNullOrEmpty(detected) ? "" : "Auto-detected: " + detected,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            MaximumSize = new Size(Dip(660), 0),
            ForeColor = Color.FromArgb(80, 80, 80),
            Margin = new Padding(0, 0, 0, Dip(4)),
        };
    }

    Label Caption(string text) => new()
    {
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = Dip(new Padding(0, 6, 8, 6)),
    };

    Button BrowseButton()
    {
        var button = new Button { Text = "Browse..." };
        ApplyButtonChrome(button);
        return button;
    }

    void ApplyButtonChrome(Button button)
    {
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Padding = new Padding(Dip(10), Dip(4), Dip(10), Dip(6));
        button.Margin = Dip(new Padding(0, 6, 0, 6));
        button.Anchor = AnchorStyles.Left;
        button.MinimumSize = new Size(0, 0);
    }

    int Dip(int logicalPixels) => LogicalToDeviceUnits(logicalPixels);

    Padding Dip(Padding logical) => new(Dip(logical.Left), Dip(logical.Top), Dip(logical.Right), Dip(logical.Bottom));

    void BrowseExe(TextBox target, string title)
    {
        using var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        var current = target.Text.Trim();
        if (File.Exists(current))
        {
            dialog.FileName = Path.GetFileName(current);
            dialog.InitialDirectory = Path.GetDirectoryName(current);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
            target.Text = dialog.FileName;
    }
}
