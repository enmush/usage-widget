using Avalonia.Controls;
using UsageWidget.Core;

namespace UsageWidget.Platform;

/// <summary>What differs between Windows and macOS for the widget window: the shortcut, start-at-login and minimizing.</summary>
interface IPlatform : IDisposable
{
    /// <summary>The right-click menu label for starting the widget when the user signs in.</summary>
    string StartAtLoginLabel { get; }
    bool StartsAtLogin { get; }
    void SetStartAtLogin(bool enabled);

    /// <summary>Call once the window is open; the shortcut works only after this.</summary>
    void Attach(Window window);

    /// <summary>Replaces the current shortcut; false when another app already owns this one.</summary>
    bool SetHotkey(Hotkey? hotkey);
    event Action? HotkeyPressed;

    /// <summary>The shortcut as this platform writes it, such as "Ctrl+Alt+Shift+U" or "⌃⌥⇧U".</summary>
    string Describe(Hotkey hotkey);

    void Minimize(Window window);
    bool IsMinimized(Window window);
    void Restore(Window window);

    public static IPlatform Current { get; } = OperatingSystem.IsWindows() ? new WindowsPlatform() : new MacPlatform();
}
