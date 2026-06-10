using System;
using Microsoft.Win32;

namespace Encryptum.Services;

public interface ISettingsService
{
    bool RunAtStartup { get; set; }
    bool UseVirtualKeyboard { get; set; }
    bool IsLightTheme { get; set; }
    bool IsListView { get; set; }
    double WindowWidth { get; set; }
    double WindowHeight { get; set; }
    event Action<string>? SettingChanged;
}

public class SettingsService : ISettingsService
{
    private const string RegKey = @"SOFTWARE\Encryptum";

    public event Action<string>? SettingChanged;

    public bool RunAtStartup
    {
        get => ReadBool(nameof(RunAtStartup));
        set { WriteBool(nameof(RunAtStartup), value); SetStartup(value); SettingChanged?.Invoke(nameof(RunAtStartup)); }
    }

    public bool UseVirtualKeyboard
    {
        get => ReadBool(nameof(UseVirtualKeyboard), true);
        set { WriteBool(nameof(UseVirtualKeyboard), value); SettingChanged?.Invoke(nameof(UseVirtualKeyboard)); }
    }

    public bool IsLightTheme
    {
        get => ReadBool(nameof(IsLightTheme));
        set { WriteBool(nameof(IsLightTheme), value); SettingChanged?.Invoke(nameof(IsLightTheme)); }
    }

    public bool IsListView
    {
        get => ReadBool(nameof(IsListView));
        set { WriteBool(nameof(IsListView), value); SettingChanged?.Invoke(nameof(IsListView)); }
    }

    public double WindowWidth
    {
        get => ReadInt(nameof(WindowWidth), 1000);
        set { WriteInt(nameof(WindowWidth), (int)value); SettingChanged?.Invoke(nameof(WindowWidth)); }
    }

    public double WindowHeight
    {
        get => ReadInt(nameof(WindowHeight), 700);
        set { WriteInt(nameof(WindowHeight), (int)value); SettingChanged?.Invoke(nameof(WindowHeight)); }
    }

    private static bool ReadBool(string name, bool defaultValue = false)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegKey);
        var val = key?.GetValue(name);
        return val is int i ? i == 1 : defaultValue;
    }

    private static void WriteBool(string name, bool value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegKey);
        key?.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
    }

    private static int ReadInt(string name, int defaultValue = 0)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegKey);
        var val = key?.GetValue(name);
        return val is int i ? i : defaultValue;
    }

    private static void WriteInt(string name, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegKey);
        key?.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
        if (key is null) return;

        if (enable)
        {
            var exePath = Environment.ProcessPath;
            if (exePath is not null)
                key.SetValue("Encryptum", $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue("Encryptum", false);
        }
    }
}