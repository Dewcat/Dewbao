using System.Runtime.InteropServices;

namespace DewBao;

/// <summary>
/// Win32 API P/Invoke 声明
/// </summary>
internal static class NativeMethods
{
    // 窗口置顶相关
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;

    // 窗口样式
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    // 全局热键消息
    public const int WM_HOTKEY = 0x0312;
    public const int WM_NCHITTEST = 0x0084;

    // NCHITTEST 返回值（用于窗口拖拽/缩放）
    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;

    // 鼠标穿透
    public const int LWA_ALPHA = 0x02;
    public const int LWA_COLORKEY = 0x01;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    // 枚举子窗口（用于沉浸模式时对 WebView2 子窗口也应用穿透属性）
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    // 热键修饰键
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // 热键 ID 枚举
    public const int HOTKEY_TOGGLE_WINDOW = 1;   // 显示/隐藏窗口
    public const int HOTKEY_PLAY_PAUSE = 2;   // 播放/暂停
    public const int HOTKEY_FORWARD = 3;   // 快进 5s
    public const int HOTKEY_BACKWARD = 4;   // 快退 5s
    public const int HOTKEY_VOL_UP = 5;   // 音量+
    public const int HOTKEY_VOL_DOWN = 6;   // 音量-
    public const int HOTKEY_OPACITY_UP = 7;   // 透明度+
    public const int HOTKEY_OPACITY_DOWN = 8;   // 透明度-
    public const int HOTKEY_IMMERSIVE_MODE = 9;   // 沉浸模式
    public const int HOTKEY_CLICK_THROUGH = 10;   // 鼠标穿透

    // ── 低级键盘钩子 ────────────────────────────────────
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;

    // 虚拟键码（修饰键）
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;   // Alt
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(
        int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(
        IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
}
