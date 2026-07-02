using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Encryptum.Services;

public interface ISettingsService
{
    bool RunAtStartup { get; set; }
    bool MinimizeToTray { get; set; }
    bool UseVirtualKeyboard { get; set; }
    bool IsLightTheme { get; set; }
    bool IsListView { get; set; }
    double WindowWidth { get; set; }
    double WindowHeight { get; set; }
    event Action<string>? SettingChanged;
}

public class SettingsService : ISettingsService
{
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "settings.cfg");

    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public event Action<string>? SettingChanged;

    public SettingsService()
    {
        Load();
    }

    public bool RunAtStartup
    {
        get => ReadBool(nameof(RunAtStartup));
        set { WriteBool(nameof(RunAtStartup), value); SetStartup(value); SettingChanged?.Invoke(nameof(RunAtStartup)); }
    }

    public bool MinimizeToTray
    {
        get => ReadBool(nameof(MinimizeToTray), true);
        set { WriteBool(nameof(MinimizeToTray), value); SettingChanged?.Invoke(nameof(MinimizeToTray)); }
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

    private bool ReadBool(string name, bool defaultValue = false) =>
        _values.TryGetValue(name, out var v) ? v == "1" : defaultValue;

    private void WriteBool(string name, bool value) => WriteValue(name, value ? "1" : "0");

    private int ReadInt(string name, int defaultValue = 0) =>
        _values.TryGetValue(name, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
            ? i
            : defaultValue;

    private void WriteInt(string name, int value) => WriteValue(name, value.ToString(CultureInfo.InvariantCulture));

    private void WriteValue(string name, string value)
    {
        _values[name] = value;
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            foreach (var line in File.ReadAllLines(ConfigPath))
            {
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                _values[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void Save()
    {
        try
        {
            var lines = new List<string>(_values.Count);
            foreach (var (key, value) in _values)
                lines.Add($"{key}={value}");
            lines.Sort(StringComparer.Ordinal);
            File.WriteAllLines(ConfigPath, lines);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void SetStartup(bool enable)
    {
        if (OperatingSystem.IsWindows())
            SetStartupWindows(enable);
        else if (OperatingSystem.IsLinux())
            SetStartupLinux(enable);
    }

    private static void SetStartupWindows(bool enable)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
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

    private static void SetStartupLinux(bool enable)
    {
        try
        {
            var autostartDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "autostart");
            var desktopPath = Path.Combine(autostartDir, "encryptum.desktop");

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (exePath is null) return;
                Directory.CreateDirectory(autostartDir);
                File.WriteAllText(desktopPath,
                    $"""
                     [Desktop Entry]
                     Type=Application
                     Name=Encryptum
                     Exec="{exePath}"
                     X-GNOME-Autostart-enabled=true

                     """);
            }
            else
            {
                File.Delete(desktopPath);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
