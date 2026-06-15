using System.Text.Json;
using System.Text.Json.Serialization;

namespace DewBao;

/// <summary>
/// 热键配置模型（可持久化到 JSON）
/// </summary>
[Serializable]
public class HotkeyConfig
{
    public uint Modifier { get; set; }
    public uint VirtualKey { get; set; }

    [JsonIgnore]
    public string DisplayText => BuildDisplay();

    private string BuildDisplay()
    {
        var parts = new List<string>();
        if ((Modifier & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((Modifier & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((Modifier & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((Modifier & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(((Keys)VirtualKey).ToString());
        return string.Join("+", parts);
    }
}

public class AppSettings
{
    public HotkeyConfig ToggleWindow { get; set; } = new() { Modifier = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VirtualKey = (uint)Keys.H };
    public HotkeyConfig PlayPause { get; set; } = new() { Modifier = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VirtualKey = (uint)Keys.P };
    public HotkeyConfig Forward { get; set; } = new() { Modifier = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VirtualKey = (uint)Keys.Right };
    public HotkeyConfig Backward { get; set; } = new() { Modifier = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VirtualKey = (uint)Keys.Left };
    public HotkeyConfig VolumeUp { get; set; } = new() { Modifier = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VirtualKey = (uint)Keys.Up };
    public HotkeyConfig VolumeDown { get; set; } = new() { Modifier = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VirtualKey = (uint)Keys.Down };
    public HotkeyConfig OpacityUp { get; set; } = new() { Modifier = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VirtualKey = (uint)Keys.OemCloseBrackets };
    public HotkeyConfig OpacityDown { get; set; } = new() { Modifier = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VirtualKey = (uint)Keys.OemOpenBrackets };
    public HotkeyConfig ClickThrough_Hotkey { get; set; } = new() { Modifier = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT, VirtualKey = (uint)Keys.T };

    public string LastUrl { get; set; } = "https://www.bilibili.com";
    public int WindowX { get; set; } = 100;
    public int WindowY { get; set; } = 100;
    public int WindowW { get; set; } = 800;
    public int WindowH { get; set; } = 493;  // 视频784x441（16:9）+ 标题栏36 + 边框16
    public byte Opacity { get; set; } = 230;
    public bool ClickThrough { get; set; } = false;
    public bool LockAspectRatio { get; set; } = true;  // 锁定16:9宽高比（视频区域）
    public bool ImmersiveMode { get; set; } = false;  // 沉浸模式
    public HotkeyConfig ImmersiveMode_Hotkey { get; set; } = new() { Modifier = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT, VirtualKey = (uint)Keys.I };

    private static readonly string ConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DewBao", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { /* 静默失败，使用默认值 */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { /* 静默失败 */ }
    }
}
