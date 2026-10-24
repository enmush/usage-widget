using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace UsageWidget;

/// <summary>Win32 window styles WPF does not expose.</summary>
static class NativeWindow
{
    const int GwlStyle = -16;
    const nint WsMinimizeBox = 0x00020000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern nint SetWindowLongPtr(nint window, int index, nint value);

    /// <summary>
    /// A borderless WPF window lacks the minimize style, so its taskbar button would only restore it, never minimize.
    /// Call once the window handle exists (SourceInitialized).
    /// </summary>
    public static void AddMinimizeBox(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        SetWindowLongPtr(handle, GwlStyle, GetWindowLongPtr(handle, GwlStyle) | WsMinimizeBox);
    }
}
