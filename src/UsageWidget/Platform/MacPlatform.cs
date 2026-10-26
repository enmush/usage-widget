using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Threading;
using UsageWidget.Core;

namespace UsageWidget.Platform;

/// <summary>
/// macOS: the shortcut through Carbon's RegisterEventHotKey, which needs no accessibility permission;
/// "Start at login" as a LaunchAgent; and minimizing as hiding, since a borderless window cannot go to the Dock
/// (the Dock icon brings it back).
/// </summary>
[SupportedOSPlatform("macos")]
sealed unsafe class MacPlatform : IPlatform
{
    const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";
    const uint KeyboardEventClass = 0x6B657962; // 'keyb'
    const uint HotKeyPressedKind = 5;
    const uint Signature = 0x55576774; // 'UWgt'

    [StructLayout(LayoutKind.Sequential)]
    struct EventHotKeyId { public uint Signature; public uint Id; }

    [StructLayout(LayoutKind.Sequential)]
    struct EventTypeSpec { public uint EventClass; public uint EventKind; }

    [DllImport(Carbon)]
    static extern nint GetEventDispatcherTarget();

    [DllImport(Carbon)]
    static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyId id, nint target, uint options, out nint hotKey);

    [DllImport(Carbon)]
    static extern int UnregisterEventHotKey(nint hotKey);

    [DllImport(Carbon)]
    static extern int InstallEventHandler(
        nint target, delegate* unmanaged<nint, nint, nint, int> handler, nuint typeCount, EventTypeSpec* types, nint userData, out nint handlerRef);

    // Carbon calls back into a static function, so the one platform instance is reached through this field.
    static MacPlatform? current;
    nint handlerRef;
    nint hotKeyRef;

    public event Action? HotkeyPressed;

    readonly string agentPath = MacLaunchAgent.PathFor(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public string StartAtLoginLabel => "Start at login";

    public bool StartsAtLogin => File.Exists(agentPath);

    public void SetStartAtLogin(bool enabled)
    {
        if (!enabled)
        {
            File.Delete(agentPath);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(agentPath)!);
        File.WriteAllText(agentPath, MacLaunchAgent.Plist(Environment.ProcessPath!));
    }

    public void Attach(Window window)
    {
        current = this;
        var pressed = new EventTypeSpec { EventClass = KeyboardEventClass, EventKind = HotKeyPressedKind };
        InstallEventHandler(GetEventDispatcherTarget(), &OnHotKey, 1, &pressed, 0, out handlerRef);
    }

    public bool SetHotkey(Hotkey? hotkey)
    {
        if (hotKeyRef != 0) UnregisterEventHotKey(hotKeyRef);
        hotKeyRef = 0;
        if (hotkey is null) return true;
        if (handlerRef == 0 || hotkey.MacKeyCode is not { } key) return false;
        var id = new EventHotKeyId { Signature = Signature, Id = 1 };
        return RegisterEventHotKey(key, hotkey.MacModifiers, id, GetEventDispatcherTarget(), 0, out hotKeyRef) == 0;
    }

    [UnmanagedCallersOnly]
    static int OnHotKey(nint handlerCall, nint carbonEvent, nint userData)
    {
        Dispatcher.UIThread.Post(() => current?.HotkeyPressed?.Invoke());
        return 0;
    }

    public string Describe(Hotkey hotkey) => hotkey.Describe(mac: true);

    public void Minimize(Window window) => window.Hide();

    public bool IsMinimized(Window window) => !window.IsVisible;

    public void Restore(Window window)
    {
        window.Show();
        window.Activate();
    }

    public void Dispose() => SetHotkey(null);
}
