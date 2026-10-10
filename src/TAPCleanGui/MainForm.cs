// SPDX-License-Identifier: GPL-2.0-or-later
// TAPClean GUI - unofficial front end for TAPClean. See LICENSE and NOTICE.md.

using System.Diagnostics;
using TAPCleanGui.Core;

namespace TAPCleanGui;

public sealed class MainForm : Form
{
    readonly object _logGate = new();
    readonly List<string> _pendingLines = new();
    bool _flushPosted;
    bool _running;
    bool _suppressRecent;
    CancellationTokenSource? _runCancellation;
    AppSettings _settings = new();
    TapCleanResult? _lastResult;
    string? _lastCleanedPath;
    string? _boundTapPath;
    Font? _boldFont;

    readonly TextBox _fileBox = new();
    readonly ComboBox _recent = new();
    readonly CheckBox _sameAsInput = new();
    readonly TextBox _outputBox = new();
    readonly Button _browseOutput = new();
    readonly NumericUpDown _tolerance = new();
    readonly CheckBox _reckless = new();
    readonly Button _testButton = new();
    readonly Button _cleanButton = new();
    readonly Button _wavButton = new();
    readonly Button _listButton = new();
    readonly Button _cancelButton = new();
    readonly Panel _summary = new();
    readonly Label _overallValue = new();
    readonly Label _recognizedValue = new();
    readonly Label _readValue = new();
    readonly Label _checksumValue = new();
    readonly Label _bootableValue = new();
    readonly TextBox _cleanedBox = new();
    readonly TextBox _log = new();
    readonly ToolStripStatusLabel _status = new();

    public MainForm()
    {
        Text = "TAPClean GUI";
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        _boldFont = new Font(Font, FontStyle.Bold);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = Dip(new Size(920, 640));
        ClientSize = Dip(new Size(1040, 720));

        var menu = BuildMenu();
        var status = BuildStatus();
        var root = BuildRoot();
        Controls.Add(root);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        _settings = SettingsStore.Load();
        WireEvents();
        ApplySettings();
        EnableFileDrop(this);
        ClearSummary();
        _status.Text = "Ready";
    }

    MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        var open = new ToolStripMenuItem("Open TAP...", null, (_, _) => BrowseTap())
        {
            ShortcutKeys = Keys.Control | Keys.O,
        };
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => Close());
        file.DropDownItems.Add(open);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(exit);

        var settings = new ToolStripMenuItem("Settings", null, (_, _) => EditSettings());
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add(new ToolStripMenuItem("About", null, (_, _) => ShowAbout()));

        menu.Items.Add(file);
        menu.Items.Add(settings);
        menu.Items.Add(help);
        return menu;
    }

    StatusStrip BuildStatus()
    {
        var strip = new StatusStrip();
        _status.Spring = true;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        strip.Items.Add(_status);
        return strip;
    }

    Control BuildRoot()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = Dip(new Padding(10)),
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildOptions(), 0, 0);
        root.Controls.Add(BuildSummary(), 0, 1);
        root.Controls.Add(BuildLog(), 0, 2);
        return root;
    }

    Control BuildOptions()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 5,
            RowCount = 5,
            Margin = new Padding(0, 0, 0, Dip(4)),
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var row = 0; row < 5; row++)
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _fileBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _fileBox.Margin = FieldMargin();
        var browseTap = new Button { Text = "Browse..." };
        ApplyButtonChrome(browseTap);
        browseTap.Click += (_, _) => BrowseTap();

        _recent.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _recent.Margin = FieldMargin();
        _recent.MinimumSize = new Size(Dip(220), 0);
        _recent.DropDownStyle = ComboBoxStyle.DropDownList;
        _recent.DropDownWidth = Dip(520);

        _sameAsInput.Text = "Same as input";
        _sameAsInput.Checked = true;
        _sameAsInput.AutoSize = true;
        _sameAsInput.Anchor = AnchorStyles.Left;
        _sameAsInput.Margin = FieldMargin();
        _sameAsInput.TextAlign = ContentAlignment.MiddleLeft;

        _outputBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _outputBox.Margin = FieldMargin();
        _browseOutput.Text = "Browse...";
        ApplyButtonChrome(_browseOutput);

        var toleranceCaption = Caption("Tolerance");
        _tolerance.Minimum = 0;
        _tolerance.Maximum = 15;
        _tolerance.Value = 10;
        _tolerance.Width = Dip(72);
        _tolerance.Anchor = AnchorStyles.Left;
        _tolerance.Margin = FieldMargin();

        _reckless.Text = "Reckless (clean even with read errors)";
        _reckless.AutoSize = true;
        _reckless.Anchor = AnchorStyles.Left;
        _reckless.Margin = FieldMargin();
        _reckless.TextAlign = ContentAlignment.MiddleLeft;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0),
            Padding = new Padding(0, Dip(2), 0, 0),
        };
        ConfigureRunButton(_testButton, "Test");
        ConfigureRunButton(_cleanButton, "Clean");
        ConfigureRunButton(_wavButton, "Convert to WAV");
        ConfigureRunButton(_listButton, "List scanners");
        ConfigureRunButton(_cancelButton, "Cancel");
        _cancelButton.Enabled = false;
        actions.Controls.Add(_testButton);
        actions.Controls.Add(_cleanButton);
        actions.Controls.Add(_wavButton);
        actions.Controls.Add(_listButton);
        actions.Controls.Add(_cancelButton);

        var hint = new Label
        {
            Text = "Drop a .tap file on this window. A clean writes clean.<name>.tap into the output folder and leaves the original unchanged.",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(80, 80, 80),
            Margin = new Padding(0, Dip(2), 0, 0),
        };

        table.Controls.Add(Caption("TAP file"), 0, 0);
        table.Controls.Add(_fileBox, 1, 0);
        table.Controls.Add(browseTap, 2, 0);
        table.Controls.Add(Caption("Recent"), 3, 0);
        table.Controls.Add(_recent, 4, 0);
        table.Controls.Add(_sameAsInput, 0, 1);
        table.Controls.Add(_outputBox, 1, 1);
        table.Controls.Add(_browseOutput, 2, 1);
        table.Controls.Add(toleranceCaption, 0, 2);
        table.Controls.Add(_tolerance, 1, 2);
        table.Controls.Add(_reckless, 2, 2);
        table.SetColumnSpan(_reckless, 3);
        table.Controls.Add(actions, 0, 3);
        table.SetColumnSpan(actions, 5);
        table.Controls.Add(hint, 0, 4);
        table.SetColumnSpan(hint, 5);

        var tips = new ToolTip();
        tips.SetToolTip(_tolerance, "Pulse-width read tolerance, 0 to 15. TAPClean's default is 10.");
        tips.SetToolTip(_reckless, "Force a clean when the tape has read errors. The result may still not load.");
        tips.SetToolTip(_sameAsInput, "Write into the same folder as the selected tape.");
        return table;
    }

    Control BuildSummary()
    {
        _summary.Dock = DockStyle.Fill;
        _summary.AutoSize = true;
        _summary.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _summary.Margin = new Padding(0, Dip(4), 0, Dip(8));
        _summary.BorderStyle = BorderStyle.FixedSingle;
        _summary.BackColor = Color.FromArgb(245, 245, 245);
        _summary.Padding = Dip(new Padding(8, 4, 8, 4));

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 6,
            RowCount = 4,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        for (var row = 0; row < 4; row++)
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        PrepareValue(_overallValue, bold: true);
        PrepareValue(_recognizedValue, bold: false);
        PrepareValue(_readValue, bold: false);
        PrepareValue(_checksumValue, bold: false);
        PrepareValue(_bootableValue, bold: false);

        _cleanedBox.ReadOnly = true;
        _cleanedBox.BorderStyle = BorderStyle.None;
        _cleanedBox.Dock = DockStyle.Fill;
        _cleanedBox.Margin = new Padding(0, Dip(4), 0, 0);
        _cleanedBox.MinimumSize = new Size(0, Dip(22));
        _cleanedBox.TabStop = false;
        _cleanedBox.BackColor = _summary.BackColor;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0),
            Padding = new Padding(0, Dip(4), 0, Dip(2)),
        };
        buttons.Controls.Add(ActionButton("Open output folder", (_, _) => OpenOutputFolder()));
        buttons.Controls.Add(ActionButton("View report", (_, _) => ViewReport()));
        buttons.Controls.Add(ActionButton("Launch in VICE", (_, _) => LaunchOriginal()));
        buttons.Controls.Add(ActionButton("Launch cleaned in VICE", (_, _) => LaunchCleaned()));

        grid.Controls.Add(Caption("Overall"), 0, 0);
        grid.Controls.Add(_overallValue, 1, 0);
        grid.Controls.Add(Caption("Recognized"), 2, 0);
        grid.Controls.Add(_recognizedValue, 3, 0);
        grid.Controls.Add(Caption("Read errors"), 4, 0);
        grid.Controls.Add(_readValue, 5, 0);
        grid.Controls.Add(Caption("Checksum"), 0, 1);
        grid.Controls.Add(_checksumValue, 1, 1);
        grid.SetColumnSpan(_checksumValue, 3);
        grid.Controls.Add(Caption("Bootable"), 4, 1);
        grid.Controls.Add(_bootableValue, 5, 1);
        grid.Controls.Add(Caption("Cleaned"), 0, 2);
        grid.Controls.Add(_cleanedBox, 1, 2);
        grid.SetColumnSpan(_cleanedBox, 5);
        grid.Controls.Add(buttons, 0, 3);
        grid.SetColumnSpan(buttons, 6);

        _summary.Controls.Add(grid);
        return _summary;
    }

    Control BuildLog()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var caption = new Label
        {
            Text = "TAPClean output",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, Dip(2)),
        };
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Both;
        _log.WordWrap = false;
        _log.MaxLength = 2_000_000;
        _log.Font = new Font("Consolas", 10f);
        _log.BackColor = Color.FromArgb(28, 28, 28);
        _log.ForeColor = Color.FromArgb(230, 230, 230);
        _log.Dock = DockStyle.Fill;
        _log.Margin = new Padding(0);
        _log.BorderStyle = BorderStyle.FixedSingle;
        _log.HideSelection = false;

        host.Controls.Add(caption, 0, 0);
        host.Controls.Add(_log, 0, 1);
        return host;
    }

    void WireEvents()
    {
        _fileBox.TextChanged += (_, _) => OnTapTextChanged();
        _sameAsInput.CheckedChanged += (_, _) => UpdateOutputMode();
        _recent.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressRecent || _recent.SelectedItem is not string path)
                return;
            SetTapPath(path, remember: false);
        };
        _browseOutput.Click += (_, _) => BrowseOutput();
        _testButton.Click += (_, _) => RunOperation(TapCleanOperation.Test);
        _cleanButton.Click += (_, _) => RunOperation(TapCleanOperation.Clean);
        _wavButton.Click += (_, _) => RunOperation(TapCleanOperation.Wav);
        _listButton.Click += (_, _) => RunOperation(TapCleanOperation.ListScanners);
        _cancelButton.Click += (_, _) =>
        {
            _status.Text = "Cancelling…";
            _runCancellation?.Cancel();
        };
    }

    void ApplySettings()
    {
        _tolerance.Value = Math.Clamp(_settings.Tolerance, 0, 15);
        _reckless.Checked = _settings.Reckless;
        _sameAsInput.Checked = _settings.OutputSameAsInput;
        if (!_settings.OutputSameAsInput && !string.IsNullOrWhiteSpace(_settings.LastOutputFolder))
            _outputBox.Text = _settings.LastOutputFolder;
        UpdateOutputMode();
        BindRecent();
    }

    void SaveSettingsFromUi()
    {
        _settings.Tolerance = (int)_tolerance.Value;
        _settings.Reckless = _reckless.Checked;
        _settings.OutputSameAsInput = _sameAsInput.Checked;
        if (!_sameAsInput.Checked)
            _settings.LastOutputFolder = _outputBox.Text.Trim();
        SettingsStore.Save(_settings);
    }

    void EditSettings()
    {
        using var dialog = new SettingsForm(_settings.TapCleanExePath, _settings.ViceExePath);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _settings.TapCleanExePath = dialog.TapCleanExePath;
        _settings.ViceExePath = dialog.ViceExePath;
        try
        {
            SaveSettingsFromUi();
            _status.Text = "Settings saved";
        }
        catch (Exception ex)
        {
            Warn("Settings could not be saved. " + ex.Message);
        }
    }

    void BrowseTap()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a TAP file",
            Filter = "TAP images (*.tap)|*.tap|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        var current = _fileBox.Text.Trim();
        if (File.Exists(current))
        {
            dialog.FileName = Path.GetFileName(current);
            dialog.InitialDirectory = Path.GetDirectoryName(current);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
            SetTapPath(dialog.FileName, remember: true);
    }

    void BrowseOutput()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Folder TAPClean should write into",
            UseDescriptionForTitle = true,
        };
        if (Directory.Exists(_outputBox.Text))
            dialog.SelectedPath = _outputBox.Text;
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _outputBox.Text = dialog.SelectedPath;
        _settings.LastOutputFolder = dialog.SelectedPath;
        try
        {
            SaveSettingsFromUi();
        }
        catch (Exception ex)
        {
            Warn("Settings could not be saved. " + ex.Message);
        }
    }

    void SetTapPath(string path, bool remember)
    {
        _fileBox.Text = path;
        RefreshSameFolder();
        if (!remember)
            return;
        SettingsStore.RememberFile(_settings, path);
        BindRecent();
        try
        {
            SaveSettingsFromUi();
        }
        catch (Exception ex)
        {
            Warn("Settings could not be saved. " + ex.Message);
        }
    }

    void BindRecent()
    {
        _suppressRecent = true;
        _recent.BeginUpdate();
        _recent.Items.Clear();
        foreach (var path in _settings.RecentFiles)
            _recent.Items.Add(path);
        _recent.SelectedIndex = -1;
        _recent.EndUpdate();
        _suppressRecent = false;
    }

    void UpdateOutputMode()
    {
        var same = _sameAsInput.Checked;
        _browseOutput.Enabled = !same;
        _outputBox.ReadOnly = same;
        if (same)
        {
            RefreshSameFolder();
            _outputBox.BackColor = SystemColors.Control;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(_settings.LastOutputFolder))
                _outputBox.Text = _settings.LastOutputFolder;
            _outputBox.BackColor = SystemColors.Window;
        }
    }

    void OnTapTextChanged()
    {
        RefreshSameFolder();
        var next = NormalizeTapPath(_fileBox.Text);
        if (string.Equals(next, _boundTapPath, StringComparison.OrdinalIgnoreCase))
            return;

        // Buttons and the summary belong to the tape that was just run, not the previous one.
        _boundTapPath = next;
        _lastResult = null;
        _lastCleanedPath = null;
        ClearSummary();
        if (!_running)
            _status.Text = "Ready";
    }

    static string? NormalizeTapPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return path.Trim();
        }
    }

    void RefreshSameFolder()
    {
        if (!_sameAsInput.Checked)
            return;
        var tap = _fileBox.Text.Trim();
        var dir = "";
        if (tap.Length > 0)
        {
            try
            {
                dir = Path.GetDirectoryName(Path.GetFullPath(tap)) ?? "";
            }
            catch (Exception)
            {
                dir = "";
            }
        }

        if (!string.Equals(_outputBox.Text, dir, StringComparison.OrdinalIgnoreCase))
            _outputBox.Text = dir;
    }

    string? GetOutputDirectory()
    {
        if (_sameAsInput.Checked)
        {
            var tap = _fileBox.Text.Trim();
            if (tap.Length == 0)
                return null;
            try
            {
                return Path.GetDirectoryName(Path.GetFullPath(tap));
            }
            catch (Exception)
            {
                return null;
            }
        }

        var folder = _outputBox.Text.Trim();
        return folder.Length == 0 ? null : folder;
    }

    bool TryBuildRequest(TapCleanOperation operation, out TapCleanRequest? request)
    {
        request = null;
        var exe = ToolLocator.ResolveTapClean(_settings.TapCleanExePath);
        if (string.IsNullOrEmpty(exe))
        {
            OfferSettings(
                "TAPClean.exe was not found. Download TAPClean from https://sourceforge.net/projects/tapclean/ " +
                "and either put TAPCleanGUI.exe in the same folder as TAPClean.exe, or set the path in Settings.");
            return false;
        }

        string? input = _fileBox.Text.Trim();
        if (operation == TapCleanOperation.ListScanners)
        {
            input = input.Length > 0 && File.Exists(input) ? Path.GetFullPath(input) : null;
        }
        else
        {
            if (input.Length == 0)
            {
                Warn("Choose a .tap file first.");
                return false;
            }
            if (!File.Exists(input))
            {
                Warn("The tape file was not found:\n" + input);
                return false;
            }
            input = Path.GetFullPath(input);
        }

        var output = GetOutputDirectory();
        if ((string.IsNullOrEmpty(output) || !Directory.Exists(output)) && operation == TapCleanOperation.ListScanners)
            output = Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(output) || !Directory.Exists(output))
        {
            Warn("The output folder does not exist:\n" + output);
            return false;
        }

        request = new TapCleanRequest
        {
            TapCleanExePath = exe,
            InputFile = input,
            Operation = operation,
            Tolerance = (int)_tolerance.Value,
            Reckless = _reckless.Checked,
            WorkingDirectory = output,
            ReportDirectory = output,
        };
        return true;
    }

    async void RunOperation(TapCleanOperation operation)
    {
        if (_running)
            return;
        if (!TryBuildRequest(operation, out var request) || request is null)
            return;

        _running = true;
        SetRunning(true);
        _log.Clear();
        lock (_logGate)
        {
            _pendingLines.Clear();
            _flushPosted = false;
        }
        ClearSummary();
        _status.Text = "Running TAPClean…";

        if (!string.IsNullOrWhiteSpace(request.InputFile))
        {
            SettingsStore.RememberFile(_settings, request.InputFile);
            BindRecent();
        }

        try
        {
            SaveSettingsFromUi();
        }
        catch (Exception ex)
        {
            Warn("Settings could not be saved. " + ex.Message);
        }

        _runCancellation = new CancellationTokenSource();
        try
        {
            var result = await TapCleanRunner.RunAsync(request, OnRunnerLine, _runCancellation.Token);
            if (IsDisposed)
                return;
            DrainLog();
            if (!string.Equals(NormalizeTapPath(request.InputFile), _boundTapPath, StringComparison.OrdinalIgnoreCase))
                return;
            ShowResult(result);
            if (operation == TapCleanOperation.Clean && result.RefusedDueToReadErrors && !result.Cancelled)
                ExplainRefusal(result);
        }
        catch (Exception ex)
        {
            if (IsDisposed)
                return;
            DrainLog();
            _status.Text = "Error";
            MessageBox.Show(this, ex.Message, "TAPClean GUI", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _runCancellation.Dispose();
            _runCancellation = null;
            if (!IsDisposed)
                SetRunning(false);
        }
    }

    void SetRunning(bool running)
    {
        _running = running;
        _testButton.Enabled = !running;
        _cleanButton.Enabled = !running;
        _wavButton.Enabled = !running;
        _listButton.Enabled = !running;
        _cancelButton.Enabled = running;
    }

    void OnRunnerLine(string line)
    {
        if (IsDisposed)
            return;
        lock (_logGate)
        {
            _pendingLines.Add(line);
            if (_flushPosted)
                return;
            _flushPosted = true;
        }

        try
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(FlushLog);
        }
        catch (InvalidOperationException)
        {
            lock (_logGate)
                _flushPosted = false;
        }
    }

    void FlushLog()
    {
        if (IsDisposed)
            return;
        string[] lines;
        lock (_logGate)
        {
            lines = _pendingLines.ToArray();
            _pendingLines.Clear();
            _flushPosted = false;
        }
        AppendLines(lines);
    }

    void DrainLog()
    {
        while (true)
        {
            string[] lines;
            lock (_logGate)
            {
                if (_pendingLines.Count == 0)
                {
                    _flushPosted = false;
                    break;
                }
                lines = _pendingLines.ToArray();
                _pendingLines.Clear();
                _flushPosted = false;
            }
            AppendLines(lines);
        }
    }

    void AppendLines(string[] lines)
    {
        if (lines.Length == 0 || IsDisposed)
            return;
        _log.AppendText(string.Join(Environment.NewLine, lines) + Environment.NewLine);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    void ClearSummary()
    {
        PaintSummary(Color.FromArgb(245, 245, 245));
        _overallValue.Text = "—";
        _overallValue.ForeColor = Color.Black;
        _recognizedValue.Text = "—";
        _readValue.Text = "—";
        _readValue.ForeColor = Color.Black;
        _checksumValue.Text = "—";
        _bootableValue.Text = "—";
        _cleanedBox.Text = "—";
    }

    void ShowResult(TapCleanResult result)
    {
        _lastResult = result;
        if (!string.IsNullOrEmpty(result.CleanedFilePath))
            _lastCleanedPath = result.CleanedFilePath;

        if (result.Cancelled)
        {
            PaintSummary(Color.FromArgb(255, 248, 220));
            _overallValue.Text = "Cancelled";
            _overallValue.ForeColor = Color.DarkGoldenrod;
            _status.Text = "Cancelled";
        }
        else if (result.Overall == OverallResult.Pass)
        {
            PaintSummary(Color.FromArgb(214, 240, 214));
            _overallValue.Text = "PASS";
            _overallValue.ForeColor = Color.DarkGreen;
            _status.Text = "Passed";
        }
        else if (result.Overall == OverallResult.Fail)
        {
            PaintSummary(Color.FromArgb(255, 220, 220));
            _overallValue.Text = result.RefusedDueToReadErrors ? "FAIL (not cleaned)" : "FAIL";
            _overallValue.ForeColor = Color.DarkRed;
            _status.Text = result.RefusedDueToReadErrors ? "Not cleaned (read errors)" : "Failed";
        }
        else
        {
            PaintSummary(Color.FromArgb(245, 245, 245));
            _overallValue.Text = "—";
            _overallValue.ForeColor = Color.Black;
            _status.Text = "Finished";
        }

        _recognizedValue.Text = result.RecognizedPercent is { } percent
            ? percent.ToString("0.##") + "%"
            : "—";
        _readValue.Text = result.ReadErrors is { } errors ? errors.ToString() : "—";
        _readValue.ForeColor = result.ReadErrors switch
        {
            > 0 => Color.DarkRed,
            0 => Color.DarkGreen,
            _ => Color.Black,
        };
        _checksumValue.Text = string.IsNullOrWhiteSpace(result.Checksum) ? "—" : result.Checksum;
        _bootableValue.Text = string.IsNullOrWhiteSpace(result.Bootable) ? "—" : result.Bootable;
        _cleanedBox.Text = string.IsNullOrWhiteSpace(result.CleanedFilePath) ? "—" : result.CleanedFilePath;
    }

    void PaintSummary(Color color)
    {
        _summary.BackColor = color;
        _cleanedBox.BackColor = color;
    }

    void ExplainRefusal(TapCleanResult result)
    {
        var errors = result.ReadErrors is { } count ? count + " read error" + (count == 1 ? "" : "s") : "read errors";
        MessageBox.Show(
            this,
            "This tape has " + errors + ", so TAPClean refused to clean it.\n\n" +
            "Try a higher tolerance (for example 12-15) and run Test again. " +
            "If the errors remain, enable Reckless to force a cleaned file. " +
            "The output may still not load.",
            "TAPClean GUI",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    void OpenOutputFolder()
    {
        var cleaned = _lastResult?.CleanedFilePath;
        if (!string.IsNullOrEmpty(cleaned) && File.Exists(cleaned))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + cleaned + "\"",
                UseShellExecute = true,
            });
            return;
        }

        var dir = GetOutputDirectory();
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + dir + "\"",
                UseShellExecute = true,
            });
            return;
        }

        Warn("There is no output folder to open yet.");
    }

    void ViewReport()
    {
        var path = _lastResult?.ReportCopyPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Warn("No report copy is available yet. Run Test or Clean first.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception)
        {
            try
            {
                var notepad = new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    UseShellExecute = false,
                };
                notepad.ArgumentList.Add(path);
                Process.Start(notepad);
            }
            catch (Exception ex)
            {
                Warn("The report could not be opened. " + ex.Message);
            }
        }
    }

    void LaunchOriginal()
    {
        var tap = _fileBox.Text.Trim();
        LaunchVice(tap.Length == 0 ? null : tap, "Choose a tape file before launching VICE.");
    }

    void LaunchCleaned()
    {
        var cleaned = FindCleanedFile();
        LaunchVice(cleaned, "No cleaned file yet. Run Clean first. If the tape had read errors, try a higher tolerance or Reckless.");
    }

    string? FindCleanedFile()
    {
        if (!string.IsNullOrEmpty(_lastResult?.CleanedFilePath) && File.Exists(_lastResult.CleanedFilePath))
            return _lastResult.CleanedFilePath;
        if (!string.IsNullOrEmpty(_lastCleanedPath) && File.Exists(_lastCleanedPath))
            return _lastCleanedPath;

        var tap = _fileBox.Text.Trim();
        var dir = GetOutputDirectory();
        if (tap.Length == 0 || string.IsNullOrEmpty(dir))
            return null;
        var guess = Path.Combine(dir, "clean." + Path.GetFileName(tap));
        return File.Exists(guess) ? guess : null;
    }

    void LaunchVice(string? tap, string missingMessage)
    {
        var vice = ToolLocator.ResolveVice(_settings.ViceExePath);
        if (string.IsNullOrEmpty(vice))
        {
            OfferSettings("VICE (x64sc.exe) was not found. VICE is optional; set x64sc.exe in Settings.");
            return;
        }
        if (string.IsNullOrWhiteSpace(tap) || !File.Exists(tap))
        {
            Warn(missingMessage);
            return;
        }

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = vice,
                WorkingDirectory = Path.GetDirectoryName(vice) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("+autostart-warp");
            start.ArgumentList.Add("-autostart");
            start.ArgumentList.Add(tap);
            Process.Start(start);
            _status.Text = "Launched in VICE";
        }
        catch (Exception ex)
        {
            Warn("VICE could not be started. " + ex.Message);
        }
    }

    void ShowAbout()
    {
        const string aboutText =
            "TAPClean GUI 1.0.0 - an unofficial, AI-made graphical front end for TAPClean. " +
            "TAPClean (C) 2006-2019 TC Team, based on Final TAP (C) 2001-2006 Subchrist Software; " +
            "licensed under the GNU GPL v2 or later. This GUI only calls TAPClean.exe and is not affiliated " +
            "with or endorsed by the TC Team. Provided as-is with no warranty and no support. " +
            "Licensed under GPL-2.0-or-later.";

        using var dialog = new Form
        {
            Text = "About TAPClean GUI",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            Font = Font,
        };

        var text = new Label
        {
            Text = aboutText,
            AutoSize = true,
            MaximumSize = new Size(Dip(460), 0),
            Margin = new Padding(0),
        };
        var link = new LinkLabel
        {
            Text = "https://sourceforge.net/projects/tapclean/",
            AutoSize = true,
            Margin = new Padding(0, Dip(8), 0, 0),
        };
        link.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(link.Text) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Warn("The link could not be opened. " + ex.Message);
            }
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        ApplyButtonChrome(ok);
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0, Dip(8), 0, 0),
        };
        buttons.Controls.Add(ok);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Padding = Dip(new Padding(12)),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(text, 0, 0);
        table.Controls.Add(link, 0, 1);
        table.Controls.Add(buttons, 0, 2);

        dialog.Controls.Add(table);
        dialog.AcceptButton = ok;
        dialog.CancelButton = ok;
        var width = Dip(480);
        var preferred = table.GetPreferredSize(new Size(width, 0));
        dialog.ClientSize = new Size(width, preferred.Height);
        dialog.ShowDialog(this);
    }

    void OfferSettings(string message)
    {
        var answer = MessageBox.Show(
            this,
            message + "\n\nOpen Settings now?",
            "TAPClean GUI",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer == DialogResult.Yes)
            EditSettings();
    }

    void Warn(string message)
    {
        MessageBox.Show(this, message, "TAPClean GUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;
        var tap = files.FirstOrDefault(file => file.EndsWith(".tap", StringComparison.OrdinalIgnoreCase));
        if (tap == null)
        {
            Warn("Drop a .tap file.");
            return;
        }
        SetTapPath(tap, remember: true);
    }

    void EnableFileDrop(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += OnDragEnter;
        control.DragDrop += OnDragDrop;
        foreach (Control child in control.Controls)
            EnableFileDrop(child);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_running)
        {
            var answer = MessageBox.Show(
                this,
                "TAPClean is still running. Stop it and close?",
                "TAPClean GUI",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            _runCancellation?.Cancel();
        }

        try
        {
            SaveSettingsFromUi();
        }
        catch (Exception)
        {
            // Closing should not get stuck on a settings write.
        }

        base.OnFormClosing(e);
    }

    void PrepareValue(Label label, bool bold)
    {
        label.Text = "—";
        label.AutoSize = false;
        label.AutoEllipsis = true;
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Margin = new Padding(0, Dip(2), Dip(8), Dip(2));
        label.MinimumSize = new Size(0, Dip(22));
        if (bold && _boldFont != null)
            label.Font = _boldFont;
    }

    Label Caption(string text) => new()
    {
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, Dip(4), Dip(8), Dip(4)),
        ForeColor = Color.FromArgb(70, 70, 70),
    };

    Padding FieldMargin() => new(0, Dip(4), Dip(8), Dip(4));

    int Dip(int logicalPixels) => LogicalToDeviceUnits(logicalPixels);

    Size Dip(Size logical) => new(Dip(logical.Width), Dip(logical.Height));

    Padding Dip(Padding logical) => new(Dip(logical.Left), Dip(logical.Top), Dip(logical.Right), Dip(logical.Bottom));

    void ConfigureRunButton(Button button, string text)
    {
        button.Text = text;
        ApplyButtonChrome(button);
    }

    Button ActionButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text };
        ApplyButtonChrome(button);
        button.Click += onClick;
        return button;
    }

    void ApplyButtonChrome(Button button)
    {
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Padding = new Padding(Dip(10), Dip(4), Dip(10), Dip(6));
        button.Margin = new Padding(0, Dip(2), Dip(8), Dip(2));
        button.Anchor = AnchorStyles.Left;
        button.MinimumSize = new Size(0, 0);
    }
}
