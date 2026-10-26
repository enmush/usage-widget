using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Microsoft.Win32;
using UsageWidget.Core;

namespace UsageWidget.Platform;

/// <summary>
/// Windows: the shortcut through RegisterHotKey on the window handle, "Start with Windows" as a value under the
/// current user's Run key (the registry is the only source of truth), and a taskbar button that minimizes.
/// </summary>
[SupportedOSPlatform("windows")]
sealed class WindowsPlatform : IPlatform
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "UsageWidget";
    const uint WsMinimizeBox = 0x00020000;
    const uint WmHotkey = 0x0312;
    const int HotkeyId = 0x5557;
    const uint ModNoRepeat = 0x4000;

    nint handle;
    bool registered;

    public event Action? HotkeyPressed;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(nint window, int id);

    public string StartAtLoginLabel => "Start with Windows";

    public bool StartsAtLogin
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string;
        }
    }

    public void SetStartAtLogin(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(RunValue, throwOnMissingValue: false);
    }

    public void Attach(Window window)
    {
        // A borderless window lacks the minimize style, so its taskbar button would only restore it, never minimize.
        Win32Properties.AddWindowStylesCallback(window, (style, exStyle) => (style | WsMinimizeBox, exStyle));
        Win32Properties.AddWndProcHookCallback(window, OnMessage);
        handle = window.TryGetPlatformHandle()?.Handle ?? 0;
    }

    public bool SetHotkey(Hotkey? hotkey)
    {
        if (registered) UnregisterHotKey(handle, HotkeyId);
        registered = false;
        if (hotkey is null || handle == 0) return true;
        registered = RegisterHotKey(handle, HotkeyId, (uint)hotkey.Modifiers | ModNoRepeat, (uint)hotkey.Key);
        return registered;
    }

    nint OnMessage(nint hwnd, uint message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke();
        }
        return 0;
    }

    public string Describe(Hotkey hotkey) => hotkey.Describe(mac: false);

    public void Minimize(Window window) => window.WindowState = WindowState.Minimized;

    public bool IsMinimized(Window window) => window.WindowState == WindowState.Minimized;

    public void Restore(Window window)
    {
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public void Dispose() => SetHotkey(null);
}
