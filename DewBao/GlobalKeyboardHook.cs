using System.Runtime.InteropServices;

namespace DewBao;

/// <summary>
/// 在独立后台线程 + 专属消息循环中安装 WH_KEYBOARD_LL 钩子。
/// 独立线程保证：即使主 UI 线程繁忙或全屏游戏占用输入，
/// 钩子回调也能在 Windows 300ms 超时窗口内及时响应。
/// </summary>
internal sealed class GlobalKeyboardHook : IDisposable
{
    // ── Win32 直接引入（不走 NativeMethods，避免循环依赖）────────
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);

    // PostMessage 用于向钩子线程发"退出"消息
    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX, ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const uint WM_QUIT = 0x0012;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    // ── 状态 ─────────────────────────────────────────────────────
    private Thread? _thread;
    private uint _threadId;
    private volatile bool _started;
    private readonly ManualResetEventSlim _ready = new(false);
    private LowLevelKeyboardProc? _proc;   // GC 根
    private IntPtr _hook;

    /// <summary>
    /// 匹配到热键时在<b>钩子线程</b>上触发，调用方应 BeginInvoke 切回 UI 线程。
    /// </summary>
    public event Action<int>? HotkeyTriggered;

    /// <summary>键码 → 热键 ID 的映射表，由调用方在 UI 线程更新。</summary>
    private volatile HotkeyTable _table = new();

    // ── 公开 API ──────────────────────────────────────────────────

    public void UpdateHotkeys(AppSettings s) => _table = HotkeyTable.Build(s);

    public void Start()
    {
        if (_started) return;
        _started = true;
        _thread = new Thread(HookThread)
        {
            IsBackground = true,
            Name = "GlobalKeyboardHookThread"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();   // 等钩子装好再返回
    }

    public void Stop()
    {
        if (_threadId != 0)
            PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(2000);
    }

    public void Dispose() => Stop();

    // ── 钩子线程 ──────────────────────────────────────────────────

    private void HookThread()
    {
        _threadId = (uint)NativeMethods.GetCurrentThreadId();

        // 安装钩子
        _proc = LLKeyboardProc;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                     GetModuleHandle(null), 0);

        _ready.Set();   // 通知 Start() 可以返回了

        // 纯消息循环（GetMessage / DispatchMessage）
        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        // 退出时卸钩
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr LLKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 &&
            ((int)wParam == WM_KEYDOWN || (int)wParam == WM_SYSKEYDOWN))
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            uint vk = kbd.vkCode;

            uint mod = 0;
            if ((GetKeyState(VK_CONTROL) & 0x8000) != 0) mod |= NativeMethods.MOD_CONTROL;
            if ((GetKeyState(VK_MENU) & 0x8000) != 0) mod |= NativeMethods.MOD_ALT;
            if ((GetKeyState(VK_SHIFT) & 0x8000) != 0) mod |= NativeMethods.MOD_SHIFT;
            if (((GetKeyState(VK_LWIN) | GetKeyState(VK_RWIN)) & 0x8000) != 0)
                mod |= NativeMethods.MOD_WIN;

            int? id = _table.Match(mod, vk);
            if (id.HasValue)
            {
                HotkeyTriggered?.Invoke(id.Value);
                return new IntPtr(1);
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    // ── 热键映射表（不可变 → 原子替换，无锁）─────────────────────

    private sealed class HotkeyTable
    {
        private readonly (uint mod, uint vk, int id)[] _entries;

        private HotkeyTable((uint, uint, int)[] entries) => _entries = entries;

        public HotkeyTable() => _entries = Array.Empty<(uint, uint, int)>();

        public static HotkeyTable Build(AppSettings s)
        {
            static uint C(uint m) => m & ~NativeMethods.MOD_NOREPEAT;
            return new HotkeyTable(new[]
            {
                (C(s.ToggleWindow.Modifier), s.ToggleWindow.VirtualKey, NativeMethods.HOTKEY_TOGGLE_WINDOW),
                (C(s.PlayPause.Modifier),    s.PlayPause.VirtualKey,    NativeMethods.HOTKEY_PLAY_PAUSE),
                (C(s.Forward.Modifier),      s.Forward.VirtualKey,      NativeMethods.HOTKEY_FORWARD),
                (C(s.Backward.Modifier),     s.Backward.VirtualKey,     NativeMethods.HOTKEY_BACKWARD),
                (C(s.VolumeUp.Modifier),     s.VolumeUp.VirtualKey,     NativeMethods.HOTKEY_VOL_UP),
                (C(s.VolumeDown.Modifier),   s.VolumeDown.VirtualKey,   NativeMethods.HOTKEY_VOL_DOWN),
                (C(s.OpacityUp.Modifier),    s.OpacityUp.VirtualKey,    NativeMethods.HOTKEY_OPACITY_UP),
                (C(s.OpacityDown.Modifier),  s.OpacityDown.VirtualKey,  NativeMethods.HOTKEY_OPACITY_DOWN),
                (C(s.ImmersiveMode_Hotkey.Modifier), s.ImmersiveMode_Hotkey.VirtualKey, NativeMethods.HOTKEY_IMMERSIVE_MODE),
                (C(s.ClickThrough_Hotkey.Modifier),  s.ClickThrough_Hotkey.VirtualKey,  NativeMethods.HOTKEY_CLICK_THROUGH),
            });
        }

        public int? Match(uint mod, uint vk)
        {
            foreach (var (m, k, id) in _entries)
                if (mod == m && vk == k) return id;
            return null;
        }
    }
}
