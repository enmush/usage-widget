using Avalonia.Controls;
using UsageWidget.Core;

namespace UsageWidget.Platform;

/// <summary>macOS: a borderless window cannot go to the Dock, so minimizing hides it and the Dock icon brings it back.</summary>
sealed class MacPlatform : IPlatform
{
    public event Action? HotkeyPressed { add { } remove { } }

    public string StartAtLoginLabel => "Start at login";
    public bool StartsAtLogin => false;
    public void SetStartAtLogin(bool enabled) { }

    public void Attach(Window window) { }

    public bool SetHotkey(Hotkey? hotkey) => hotkey is null;

    public string Describe(Hotkey hotkey) => hotkey.ToString();

    public void Minimize(Window window) => window.Hide();

    public bool IsMinimized(Window window) => !window.IsVisible;

    public void Restore(Window window)
    {
        window.Show();
        window.Activate();
    }

    public void Dispose() { }
}
