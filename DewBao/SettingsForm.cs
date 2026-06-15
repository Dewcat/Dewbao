namespace DewBao;

/// <summary>
/// 设置界面：可视化配置热键与透明度
/// </summary>
public class SettingsForm : Form
{
    public AppSettings Result { get; private set; }

    private readonly AppSettings _orig;
    private readonly Dictionary<int, (Label lblKey, HotkeyConfig cfg, string name)> _rows = new();
    private TrackBar _opacityTrack = null!;
    private Label _opacityLabel = null!;
    private int _capturingId = -1;   // 当前正在捕获热键的行

    private static readonly Color BgColor = Color.FromArgb(25, 25, 35);
    private static readonly Color RowBg = Color.FromArgb(35, 35, 50);
    private static readonly Color AccentC = Color.FromArgb(0, 161, 214);

    public SettingsForm(AppSettings settings)
    {
        _orig = settings;
        Result = CloneSettings(settings);
        InitUI();
    }

    private void InitUI()
    {
        Text = "设置 - DewBao";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = BgColor;
        ForeColor = Color.White;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(540, 480);
        Font = new Font("微软雅黑", 9.5f);

        int y = 12;

        // ── 标题 ──────────────────────────────────────────
        AddLabel("热键配置（点击按钮后按新组合键）", 14, 12, ref y, bold: true, color: AccentC);
        y += 4;

        // ── 热键行 ────────────────────────────────────────
        var hotkeys = new (int id, string name, HotkeyConfig cfg)[]
        {
            (NativeMethods.HOTKEY_TOGGLE_WINDOW, "显示/隐藏窗口",  Result.ToggleWindow),
            (NativeMethods.HOTKEY_PLAY_PAUSE,    "播放 / 暂停",    Result.PlayPause),
            (NativeMethods.HOTKEY_FORWARD,       "快进 5 秒",      Result.Forward),
            (NativeMethods.HOTKEY_BACKWARD,      "快退 5 秒",      Result.Backward),
            (NativeMethods.HOTKEY_VOL_UP,        "音量 +10%",      Result.VolumeUp),
            (NativeMethods.HOTKEY_VOL_DOWN,      "音量 -10%",      Result.VolumeDown),
            (NativeMethods.HOTKEY_OPACITY_UP,    "透明度 +",       Result.OpacityUp),
            (NativeMethods.HOTKEY_OPACITY_DOWN,  "透明度 -",       Result.OpacityDown),
            (NativeMethods.HOTKEY_CLICK_THROUGH, "鼠标穿透",       Result.ClickThrough_Hotkey),
            (NativeMethods.HOTKEY_IMMERSIVE_MODE, "沉浸模式",      Result.ImmersiveMode_Hotkey),
        };

        foreach (var (id, name, cfg) in hotkeys)
        {
            AddHotkeyRow(id, name, cfg, ref y);
            y += 4;
        }

        y += 8;
        // ── 透明度滑块 ────────────────────────────────────
        AddLabel("窗口透明度", 12, 14, ref y, bold: true);
        _opacityTrack = new TrackBar
        {
            Minimum = 30,
            Maximum = 255,
            Value = Result.Opacity,
            TickFrequency = 25,
            Location = new Point(14, y),
            Width = 380,
            BackColor = BgColor,
        };
        _opacityTrack.ValueChanged += (s, e) =>
        {
            Result.Opacity = (byte)_opacityTrack.Value;
            _opacityLabel.Text = $"{(int)(_opacityTrack.Value / 255.0 * 100)}%";
        };
        _opacityLabel = new Label
        {
            Text = $"{(int)(Result.Opacity / 255.0 * 100)}%",
            Location = new Point(400, y + 10),
            ForeColor = Color.White,
            AutoSize = true,
        };
        Controls.Add(_opacityTrack);
        Controls.Add(_opacityLabel);
        y += _opacityTrack.Height + 12;

        // ── 宽高比锁定 ────────────────────────────────────
        var chkLockAspectRatio = new CheckBox
        {
            Text = "锁定 16:9 宽高比（视频区域）",
            Checked = Result.LockAspectRatio,
            Location = new Point(14, y),
            ForeColor = Color.White,
            BackColor = BgColor,
            AutoSize = true,
            Cursor = Cursors.Hand,
        };
        chkLockAspectRatio.CheckedChanged += (s, e) => Result.LockAspectRatio = chkLockAspectRatio.Checked;
        Controls.Add(chkLockAspectRatio);
        y += 24;

        // ── 底部按钮 ──────────────────────────────────────
        var btnOk = new Button
        {
            Text = "确 定",
            DialogResult = DialogResult.OK,
            Location = new Point(ClientSize.Width - 200, y),
            Size = new Size(90, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentC,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };
        btnOk.Click += (s, e) => Close();

        var btnCancel = new Button
        {
            Text = "取 消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(ClientSize.Width - 105, y),
            Size = new Size(90, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 75),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };

        Controls.Add(btnOk);
        Controls.Add(btnCancel);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        // 调整窗口高度
        ClientSize = new Size(ClientSize.Width, y + 50);
    }

    private void AddHotkeyRow(int id, string name, HotkeyConfig cfg, ref int y)
    {
        var pnl = new Panel
        {
            Location = new Point(10, y),
            Size = new Size(ClientSize.Width - 20, 34),
            BackColor = RowBg,
        };

        var lbl = new Label
        {
            Text = name,
            Location = new Point(8, 9),
            AutoSize = true,
            ForeColor = Color.White,
        };

        var lblKey = new Label
        {
            Text = cfg.DisplayText,
            Location = new Point(180, 9),
            Width = 160,
            ForeColor = Color.FromArgb(255, 220, 100),
            BackColor = Color.FromArgb(50, 50, 65),
        };

        var btnCapture = new Button
        {
            Text = "更改",
            Location = new Point(350, 4),
            Size = new Size(60, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 160),
            ForeColor = Color.White,
            Tag = id,
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };
        btnCapture.Click += BtnCapture_Click;

        pnl.Controls.Add(lbl);
        pnl.Controls.Add(lblKey);
        pnl.Controls.Add(btnCapture);
        Controls.Add(pnl);

        _rows[id] = (lblKey, cfg, name);
        y += 38;
    }

    private void BtnCapture_Click(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int id) return;
        _capturingId = id;
        var (lblKey, _, name) = _rows[id];
        lblKey.Text = "请按新热键...";
        lblKey.ForeColor = Color.LightGreen;
        KeyPreview = true;
        ActiveControl = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_capturingId < 0) { base.OnKeyDown(e); return; }

        e.Handled = true;
        e.SuppressKeyPress = true;

        // 忽略单独的修饰键
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return;

        uint mod = 0;
        if (e.Control) mod |= NativeMethods.MOD_CONTROL;
        if (e.Alt) mod |= NativeMethods.MOD_ALT;
        if (e.Shift) mod |= NativeMethods.MOD_SHIFT;

        var (lblKey, cfg, _) = _rows[_capturingId];
        cfg.Modifier = mod;
        cfg.VirtualKey = (uint)e.KeyCode;
        lblKey.Text = cfg.DisplayText;
        lblKey.ForeColor = Color.FromArgb(255, 220, 100);
        _capturingId = -1;
        KeyPreview = false;
    }

    private void AddLabel(string text, float size, int x, ref int y, bool bold = false, Color? color = null)
    {
        var lbl = new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = color ?? Color.White,
            Font = new Font("微软雅黑", size, bold ? FontStyle.Bold : FontStyle.Regular)
        };
        Controls.Add(lbl);
        y += lbl.PreferredHeight + 4;
    }

    // 深克隆（避免修改原始引用）
    private static AppSettings CloneSettings(AppSettings src)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(src);
        return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)!;
    }
}
