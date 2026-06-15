using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DewBao;

public partial class MainForm : Form
{
    // ── 控件 ──────────────────────────────────────────────
    private WebView2 _webView = null!;
    private Panel _titleBar = null!;
    private TextBox _urlBox = null!;
    private Button _btnGo = null!;
    private Button _btnClose = null!;
    private Button _btnMin = null!;
    private Button _btnSettings = null!;
    private Label _lblTitle = null!;
    private NotifyIcon _trayIcon = null!;

    // ── 状态 ──────────────────────────────────────────────
    private AppSettings _settings;
    private byte _currentOpacity;
    private Point _dragStart;
    private bool _isDragging = false;
    private bool _isResizing = false;
    private string _resizeEdge = "";  // "TL", "TR", "BL", "BR", "L", "R", "T", "B"
    private const int ResizeBorder = 8;
    private System.Windows.Forms.Timer? _repaintTimer = null;
    private double _aspectRatio = 16.0 / 9.0;  // 视频区域宽高比（16:9）
    private bool _isImmersiveMode = false;  // 沉浸模式
    private bool _isClickThrough = false;  // 鼠标穿透

    // ── 独立线程键盘钩子 ─────────────────────────────────
    private readonly GlobalKeyboardHook _keyHook = new();

    // ── 颜色主题 ──────────────────────────────────────────
    private static readonly Color ThemeDark = Color.FromArgb(20, 20, 30);
    private static readonly Color ThemeAccent = Color.FromArgb(0, 161, 214);   // B站蓝
    private static readonly Color ThemeText = Color.White;

    public MainForm()
    {
        _settings = AppSettings.Load();
        _currentOpacity = _settings.Opacity;
        InitializeComponents();
        SetupTrayIcon();
        SetupWebView();
    }

    // ══════════════════════════════════════════════════════
    //  初始化 UI 组件
    // ══════════════════════════════════════════════════════
    private void InitializeComponents()
    {
        SuspendLayout();

        // ── 窗口属性 ──────────────────────────────────────
        Text = "DewBao";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(_settings.WindowX, _settings.WindowY);
        Size = new Size(_settings.WindowW, _settings.WindowH);
        MinimumSize = new Size(400, 280);
        BackColor = ThemeDark;
        Opacity = _currentOpacity / 255.0;
        TopMost = true;
        DoubleBuffered = true;

        // ── 标题栏 ────────────────────────────────────────
        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = ThemeDark,
            Cursor = Cursors.SizeAll
        };
        _titleBar.MouseDown += TitleBar_MouseDown;
        _titleBar.MouseMove += TitleBar_MouseMove;
        _titleBar.MouseUp += TitleBar_MouseUp;
        _titleBar.Paint += TitleBar_Paint;

        // 标题标签
        _lblTitle = new Label
        {
            Text = "🐱 DewBao",
            ForeColor = ThemeAccent,
            Font = new Font("微软雅黑", 9.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 9)
        };
        _lblTitle.MouseDown += TitleBar_MouseDown;
        _lblTitle.MouseMove += TitleBar_MouseMove;
        _lblTitle.MouseUp += TitleBar_MouseUp;

        // URL 输入框
        _urlBox = new TextBox
        {
            Text = _settings.LastUrl,
            Font = new Font("微软雅黑", 9f),
            BackColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            Width = 0,   // 动态计算
            Height = 20,
        };
        _urlBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) Navigate(_urlBox.Text); };

        // 跳转按钮
        _btnGo = CreateIconButton("▶", ThemeAccent, 32, 32);
        _btnGo.Click += (s, e) => Navigate(_urlBox.Text);
        _btnGo.Font = new Font("Arial", 9f, FontStyle.Bold);

        // 设置按钮
        _btnSettings = CreateIconButton("⚙", Color.FromArgb(160, 160, 180), 32, 32);
        _btnSettings.Click += (s, e) => ShowSettings();

        // 最小化按钮
        _btnMin = CreateIconButton("—", Color.FromArgb(200, 200, 200), 32, 32);
        _btnMin.Click += (s, e) => { WindowState = FormWindowState.Minimized; };

        // 关闭按钮
        _btnClose = CreateIconButton("✕", Color.FromArgb(255, 80, 80), 32, 32);
        _btnClose.Click += (s, e) => HideToTray();

        // 布局标题栏控件
        _titleBar.Controls.AddRange(new Control[] { _lblTitle, _urlBox, _btnGo, _btnSettings, _btnMin, _btnClose });

        // ── WebView2 占位（稍后异步初始化）─────────────────
        _webView = new WebView2
        {
            BackColor = ThemeDark,
            Dock = DockStyle.None,  // 不使用 Dock 以便在 Resize 中手动调整大小
        };

        // ── 组装窗口 ──────────────────────────────────────
        Controls.Add(_webView);
        Controls.Add(_titleBar);     // Add AFTER webview so it's on top

        ResumeLayout(false);
        PerformLayout();

        // 监听 Resize 重新布局标题栏和 WebView
        Resize += (s, e) => LayoutTitleBarAndWebView();
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
        // 窗口激活时始终把焦点交给 WebView2，防止按钮意外响应 Space
        Activated += (s, e) => BeginInvoke(() => { try { _webView.Focus(); } catch { } });

        // 窗口边框绘制和大小调节
        Paint += MainForm_Paint;
        MouseMove += MainForm_MouseMove;
        MouseDown += MainForm_MouseDown;
        MouseUp += MainForm_MouseUp;

        // 启动定时器持续刷新窗口边框
        _repaintTimer = new System.Windows.Forms.Timer
        {
            Interval = 50  // 每50ms刷新一次
        };
        _repaintTimer.Tick += (s, e) => Invalidate();
        _repaintTimer.Start();

        LayoutTitleBarAndWebView();
    }

    private static Button CreateIconButton(string text, Color fg, int w, int h)
    {
        return new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            ForeColor = fg,
            BackColor = Color.Transparent,
            Width = w,
            Height = h,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Symbol", 11f),
            FlatAppearance = { BorderSize = 0 },
            TabStop = false,          // 禁止 Tab 聚焦，防止 Space 误触发            FlatAppearance = { BorderSize = 0 }
        };
    }

    private void LayoutTitleBarAndWebView()
    {
        // 布局标题栏
        LayoutTitleBar();

        // 布局 WebView2：隐藏外框时直接从顶部边框开始，否则留出标题栏高度
        // 注意：不能用 _titleBar.Visible，窗口未显示时 Visible 恒返回 false
        int titleH = IsChromeHidden ? 0 : _titleBar.Height;
        int webX = ResizeBorder;
        int webY = titleH + ResizeBorder;
        int webW = ClientSize.Width - ResizeBorder * 2;
        int webH = ClientSize.Height - titleH - ResizeBorder * 2;

        _webView.SetBounds(webX, webY, Math.Max(webW, 0), Math.Max(webH, 0));
    }

    private void LayoutTitleBar()
    {
        const int btnW = 32;
        const int margin = 4;
        int right = _titleBar.Width - margin;

        _btnClose.Location = new Point(right - btnW, 2); right -= btnW + margin;
        _btnMin.Location = new Point(right - btnW, 2); right -= btnW + margin;
        _btnSettings.Location = new Point(right - btnW, 2); right -= btnW + margin;
        _btnGo.Location = new Point(right - btnW, 2); right -= btnW + margin;

        int urlLeft = _lblTitle.Right + 10;
        int urlWidth = right - urlLeft - margin;
        _urlBox.SetBounds(urlLeft, 9, Math.Max(urlWidth, 100), 20);
    }

    private void TitleBar_Paint(object? sender, PaintEventArgs e)
    {
        // 底部边框线
        using var pen = new Pen(ThemeAccent, 1);
        e.Graphics.DrawLine(pen, 0, _titleBar.Height - 1, _titleBar.Width, _titleBar.Height - 1);
    }

    private void MainForm_Paint(object? sender, PaintEventArgs e)
    {
        // 隐藏外框时不绘制窗口边框
        if (IsChromeHidden) return;

        int borderSize = ResizeBorder;
        using var pen = new Pen(ThemeAccent, 2)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };

        // 绘制外边框
        e.Graphics.DrawRectangle(pen, borderSize - 2, borderSize - 2,
            ClientSize.Width - borderSize * 2 + 4, ClientSize.Height - borderSize * 2 + 4);
    }

    private void MainForm_MouseMove(object? sender, MouseEventArgs e)
    {
        // 改变鼠标光标，提示可调整大小的区域
        int x = e.X, y = e.Y, w = ClientSize.Width, h = ClientSize.Height;
        int b = ResizeBorder;

        // 处理拖拽调整大小（在启用锁定宽高比时保持视频区域 16:9）
        if (_isResizing && e.Button == MouseButtons.Left && !IsChromeHidden)
        {
            int dx = e.X - _dragStart.X;
            int dy = e.Y - _dragStart.Y;

            var newLoc = Location;
            var newSize = Size;

            if (_settings.LockAspectRatio)
            {
                // 视频区域 = 窗口 - 标题栏 - 上下边框（高）/ 左右边框（宽）
                int chromaW = ResizeBorder * 2;
                int chromaH = _titleBar.Height + ResizeBorder * 2;

                if (_resizeEdge.Contains("T") || _resizeEdge.Contains("B"))
                {
                    int heightDelta = _resizeEdge.Contains("B") ? dy : -dy;
                    newSize.Height = Math.Max(MinimumSize.Height, newSize.Height + heightDelta);
                    // 由视频区域高度推算宽度
                    int videoH = Math.Max(1, newSize.Height - chromaH);
                    newSize.Width = (int)(videoH * _aspectRatio) + chromaW;
                    if (_resizeEdge.Contains("T")) newLoc.Y += e.Y - _dragStart.Y;
                }
                else if (_resizeEdge.Contains("L") || _resizeEdge.Contains("R"))
                {
                    int widthDelta = _resizeEdge.Contains("R") ? dx : -dx;
                    newSize.Width = Math.Max(MinimumSize.Width, newSize.Width + widthDelta);
                    // 由视频区域宽度推算高度
                    int videoW = Math.Max(1, newSize.Width - chromaW);
                    newSize.Height = (int)(videoW / _aspectRatio) + chromaH;
                    if (_resizeEdge.Contains("L")) newLoc.X += e.X - _dragStart.X;
                }
            }
            else
            {
                // 自由调整大小，不保持宽高比
                if (_resizeEdge.Contains("L"))
                {
                    newSize.Width = Math.Max(MinimumSize.Width, newSize.Width - dx);
                    newLoc.X += e.X - _dragStart.X;
                }
                if (_resizeEdge.Contains("R"))
                    newSize.Width = Math.Max(MinimumSize.Width, newSize.Width + dx);

                if (_resizeEdge.Contains("T"))
                {
                    newSize.Height = Math.Max(MinimumSize.Height, newSize.Height - dy);
                    newLoc.Y += e.Y - _dragStart.Y;
                }
                if (_resizeEdge.Contains("B"))
                    newSize.Height = Math.Max(MinimumSize.Height, newSize.Height + dy);
            }

            Location = newLoc;
            Size = newSize;
            _dragStart = e.Location;
            return;
        }

        // 更新光标提示（隐藏外框时不显示）
        if (IsChromeHidden)
        {
            Cursor = Cursors.Default;
            _resizeEdge = "";
            return;
        }

        _resizeEdge = "";
        if (x <= b && y <= b) { Cursor = Cursors.SizeNWSE; _resizeEdge = "TL"; }
        else if (x >= w - b && y <= b) { Cursor = Cursors.SizeNESW; _resizeEdge = "TR"; }
        else if (x <= b && y >= h - b) { Cursor = Cursors.SizeNESW; _resizeEdge = "BL"; }
        else if (x >= w - b && y >= h - b) { Cursor = Cursors.SizeNWSE; _resizeEdge = "BR"; }
        else if (x <= b) { Cursor = Cursors.SizeWE; _resizeEdge = "L"; }
        else if (x >= w - b) { Cursor = Cursors.SizeWE; _resizeEdge = "R"; }
        else if (y <= b) { Cursor = Cursors.SizeNS; _resizeEdge = "T"; }
        else if (y >= h - b) { Cursor = Cursors.SizeNS; _resizeEdge = "B"; }
        else { Cursor = Cursors.Default; _resizeEdge = ""; }
    }

    private void MainForm_MouseDown(object? sender, MouseEventArgs e)
    {
        if (!string.IsNullOrEmpty(_resizeEdge) && e.Button == MouseButtons.Left)
        {
            _isResizing = true;
            _dragStart = e.Location;
        }
    }

    private void MainForm_MouseUp(object? sender, MouseEventArgs e)
    {
        _isResizing = false;
    }

    // ══════════════════════════════════════════════════════
    //  标题栏拖拽
    // ══════════════════════════════════════════════════════
    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragStart = e.Location;
        }
    }

    private void TitleBar_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging && e.Button == MouseButtons.Left)
        {
            var screen = (sender as Control)!.PointToScreen(e.Location);
            Location = new Point(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
        }
    }

    private void TitleBar_MouseUp(object? sender, MouseEventArgs e)
    {
        _isDragging = false;
    }

    // ══════════════════════════════════════════════════════
    //  消息处理
    // ══════════════════════════════════════════════════════
    protected override void WndProc(ref Message m)
    {
        // 热键消息处理
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            HandleHotKey((int)m.WParam);
            return;
        }

        base.WndProc(ref m);
    }

    // ══════════════════════════════════════════════════════
    //  WebView2 初始化
    // ══════════════════════════════════════════════════════
    private async void SetupWebView()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DewBao", "WebView2Cache"));
            await _webView.EnsureCoreWebView2Async(env);

            // 关闭弹出式上下文菜单中不必要的项
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // 拦截导航，更新 URL 栏
            _webView.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                _urlBox.Text = _webView.Source?.ToString() ?? "";
            };

            Navigate(_settings.LastUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WebView2 初始化失败：\n{ex.Message}\n\n请确保已安装 WebView2 运行时。\n下载地址：https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                "初始化错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            url = "https://" + url;
        try { _webView.CoreWebView2?.Navigate(url); }
        catch { /* WebView2 未就绪 */ }
    }

    // ══════════════════════════════════════════════════════
    //  独立线程键盘钩子管理
    // ══════════════════════════════════════════════════════
    private void InstallKeyboardHook()
    {
        _keyHook.UpdateHotkeys(_settings);
        _keyHook.HotkeyTriggered += id => BeginInvoke(() => HandleHotKey(id));
        _keyHook.Start();
    }

    private void UninstallKeyboardHook()
    {
        _keyHook.Stop();
    }

    private void HandleHotKey(int id)
    {
        switch (id)
        {
            case NativeMethods.HOTKEY_TOGGLE_WINDOW:
                if (Visible && WindowState != FormWindowState.Minimized)
                    HideToTray();
                else
                    ShowFromTray();
                break;

            case NativeMethods.HOTKEY_PLAY_PAUSE:
                ExecuteVideoScript("var v=document.querySelector('video'); if(v){if(v.paused)v.play();else v.pause();}");
                break;

            case NativeMethods.HOTKEY_FORWARD:
                ExecuteVideoScript("var v=document.querySelector('video'); if(v) v.currentTime+=5;");
                break;

            case NativeMethods.HOTKEY_BACKWARD:
                ExecuteVideoScript("var v=document.querySelector('video'); if(v) v.currentTime-=5;");
                break;

            case NativeMethods.HOTKEY_VOL_UP:
                ExecuteVideoScript("var v=document.querySelector('video'); if(v) v.volume=Math.min(1,v.volume+0.1);");
                break;

            case NativeMethods.HOTKEY_VOL_DOWN:
                ExecuteVideoScript("var v=document.querySelector('video'); if(v) v.volume=Math.max(0,v.volume-0.1);");
                break;

            case NativeMethods.HOTKEY_OPACITY_UP:
                AdjustOpacity(+20);
                break;

            case NativeMethods.HOTKEY_OPACITY_DOWN:
                AdjustOpacity(-20);
                break;

            case NativeMethods.HOTKEY_IMMERSIVE_MODE:
                ToggleImmersiveMode();
                break;

            case NativeMethods.HOTKEY_CLICK_THROUGH:
                ToggleClickThrough();
                break;
        }
    }

    private void ExecuteVideoScript(string script)
    {
        if (_webView.CoreWebView2 != null)
        {
            _ = _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }

    private void AdjustOpacity(int delta)
    {
        int newVal = Math.Clamp(_currentOpacity + delta, 30, 255);
        _currentOpacity = (byte)newVal;
        Opacity = _currentOpacity / 255.0;
    }

    private void ToggleImmersiveMode()
    {
        _isImmersiveMode = !_isImmersiveMode;
        _settings.ImmersiveMode = _isImmersiveMode;

        if (_isImmersiveMode)
        {
            // 进入沉浸模式：隐藏标题栏、启用鼠标穿透、锁定窗口
            ApplyWindowInteractionState();

            _trayIcon.ShowBalloonTip(1500, "DewBao", "进入沉浸模式，按 Ctrl+Alt+Shift+I 退出", ToolTipIcon.Info);
        }
        else
        {
            // 退出沉浸模式：按鼠标穿透状态恢复交互外框
            ApplyWindowInteractionState();
            _trayIcon.ShowBalloonTip(1500, "DewBao", "退出沉浸模式", ToolTipIcon.Info);
        }

        SaveSettings();
    }

    private void ToggleClickThrough()
    {
        _isClickThrough = !_isClickThrough;
        _settings.ClickThrough = _isClickThrough;
        ApplyWindowInteractionState();

        var message = _isClickThrough
            ? $"已开启鼠标穿透，按 {_settings.ClickThrough_Hotkey.DisplayText} 关闭"
            : "已关闭鼠标穿透";
        _trayIcon.ShowBalloonTip(1500, "DewBao", message, ToolTipIcon.Info);

        SaveSettings();
    }

    private bool IsChromeHidden => _isImmersiveMode || _isClickThrough;

    private void ApplyWindowInteractionState()
    {
        bool hideChrome = IsChromeHidden;

        ApplyMouseTransparency();
        _titleBar.Visible = !hideChrome;
        _titleBar.Enabled = !hideChrome;

        if (hideChrome)
        {
            _isDragging = false;
            _isResizing = false;
        }
        else
        {
            _titleBar.SetBounds(0, 0, ClientSize.Width, _titleBar.Height);
            LayoutTitleBar();
            _titleBar.BringToFront();
            _webView.SendToBack();
        }

        PerformLayout();
        LayoutTitleBarAndWebView();
        Cursor = Cursors.Default;
        _resizeEdge = "";
        RefreshChrome();

        if (!hideChrome && IsHandleCreated)
            BeginInvoke(RefreshChrome);
    }

    private void ApplyMouseTransparency()
    {
        SetWindowTransparent(Handle, _isImmersiveMode || _isClickThrough);
    }

    private void RefreshChrome()
    {
        LayoutTitleBarAndWebView();
        Invalidate(true);
        _titleBar.Invalidate(true);
        _webView.Invalidate();
        Update();
    }

    /// <summary>
    /// 对主窗口设置或清除 WS_EX_TRANSPARENT（鼠标穿透）标志。
    /// WebView2 的子窗口参与渲染合成，给子 HWND 打透明标志会导致画面消失。
    /// </summary>
    private static void SetWindowTransparent(IntPtr hWnd, bool transparent)
    {
        ApplyTransparent(hWnd, transparent);
    }

    private static void ApplyTransparent(IntPtr hWnd, bool transparent)
    {
        int exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        if (transparent)
            exStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;
        else
            exStyle &= ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE, exStyle);
        NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE |
            NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_FRAMECHANGED);
    }

    // ══════════════════════════════════════════════════════
    //  窗口置顶 & 透明度
    // ══════════════════════════════════════════════════════
    private void EnsureTopMost()
    {
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    // ══════════════════════════════════════════════════════
    //  托盘图标
    // ══════════════════════════════════════════════════════
    private void SetupTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示/隐藏", null, (s, e) => ToggleVisibility());
        menu.Items.Add("打开设置", null, (s, e) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (s, e) => ExitApp());

        _trayIcon = new NotifyIcon
        {
            Text = "DewBao",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (s, e) => ToggleVisibility();
    }

    private void ToggleVisibility()
    {
        if (Visible && WindowState != FormWindowState.Minimized)
            HideToTray();
        else
            ShowFromTray();
    }

    private void HideToTray()
    {
        Hide();
        _repaintTimer?.Stop();  // 隐藏时停止刷新
        _trayIcon.ShowBalloonTip(1500, "DewBao", $"已隐藏，按 {_settings.ToggleWindow.DisplayText} 重新显示", ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        _repaintTimer?.Start();  // 显示时重启刷新
        EnsureTopMost();
        Activate();
    }

    private void ExitApp()
    {
        SaveSettings();
        _trayIcon.Visible = false;
        UninstallKeyboardHook();
        Application.Exit();
    }

    // ══════════════════════════════════════════════════════
    //  设置窗口
    // ══════════════════════════════════════════════════════
    private void ShowSettings()
    {
        using var dlg = new SettingsForm(_settings);
        // 计算设置窗口位置：放在主窗口右侧
        int settingsX = Location.X + Width + 10;
        int settingsY = Location.Y;
        // 如果超出屏幕右边界，移到左侧
        var screen = Screen.FromPoint(Location);
        if (settingsX + dlg.Width > screen.WorkingArea.Right)
        {
            settingsX = Location.X - dlg.Width - 10;
        }
        // 确保Y坐标在屏幕范围内
        if (settingsY + dlg.Height > screen.WorkingArea.Bottom)
        {
            settingsY = screen.WorkingArea.Bottom - dlg.Height;
        }
        dlg.Location = new Point(settingsX, settingsY);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _settings = dlg.Result;
            // 热键配置变更：仅更新映射表，不重启钩子线程
            _keyHook.UpdateHotkeys(_settings);
            _currentOpacity = _settings.Opacity;
            Opacity = _currentOpacity / 255.0;
            SaveSettings();
        }
    }

    private void SaveSettings()
    {
        if (WindowState == FormWindowState.Normal)
        {
            _settings.WindowX = Location.X;
            _settings.WindowY = Location.Y;
            _settings.WindowW = Width;
            _settings.WindowH = Height;
        }
        _settings.LastUrl = _webView.Source?.ToString() ?? _settings.LastUrl;
        _settings.Opacity = _currentOpacity;
        _settings.Save();
    }

    // ══════════════════════════════════════════════════════
    //  窗口事件
    // ══════════════════════════════════════════════════════
    private void MainForm_Load(object? sender, EventArgs e)
    {
        // 始终以非沉浸模式启动，不恢复上次保存的状态
        _isImmersiveMode = false;
        _settings.ImmersiveMode = false;
        _isClickThrough = false;
        _settings.ClickThrough = false;
        ApplyWindowInteractionState();
        SaveSettings();

        EnsureTopMost();
        InstallKeyboardHook();
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        _repaintTimer?.Stop();
        _repaintTimer?.Dispose();
        SaveSettings();
        UninstallKeyboardHook();
        _trayIcon.Visible = false;
    }
}
