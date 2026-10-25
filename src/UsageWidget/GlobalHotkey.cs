using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using UsageWidget.Core;

namespace UsageWidget;

/// <summary>One system-wide shortcut registered for a window; pressing it raises <see cref="Pressed"/>.</summary>
sealed class GlobalHotkey : IDisposable
{
    const int WmHotkey = 0x0312;
    const int Id = 0x5557;
    const uint ModNoRepeat = 0x4000;

    readonly HwndSource source;
    bool registered;

    public event Action? Pressed;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(nint window, int id);

    /// <summary>Call once the window handle exists (SourceInitialized).</summary>
    public GlobalHotkey(Window window)
    {
        source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)!;
        source.AddHook(OnMessage);
    }

    /// <summary>Replaces the current shortcut; false when another app already owns this one.</summary>
    public bool Set(Hotkey? hotkey)
    {
        Clear();
        if (hotkey is null) return true;
        registered = RegisterHotKey(source.Handle, Id, (uint)hotkey.Modifiers | ModNoRepeat, (uint)hotkey.Key);
        return registered;
    }

    void Clear()
    {
        if (registered) UnregisterHotKey(source.Handle, Id);
        registered = false;
    }

    nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam == Id)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return 0;
    }

    public void Dispose()
    {
        Clear();
        source.RemoveHook(OnMessage);
    }
}
