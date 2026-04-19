using Microsoft.Win32;

namespace CornWatch.Core;

/// <summary>
/// Manages Windows startup registration via the Run registry key.
/// Uses HKCU (current user) so it does NOT require admin rights
/// and appears in Task Manager → Startup tab, where the user can
/// disable it just like any other startup entry.
/// </summary>
public static class StartupManager
{
    private const string RunKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "CornWatch";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) is not null;
        }
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
            ?? throw new InvalidOperationException("Could not open Run registry key.");
        // --minimized flag tells MainForm to start in tray instead of showing the window
        key.SetValue(AppName, $"\"{AppContext.BaseDirectory}CornWatch.exe\" --minimized");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    public static void Toggle() { if (IsEnabled) Disable(); else Enable(); }
}
