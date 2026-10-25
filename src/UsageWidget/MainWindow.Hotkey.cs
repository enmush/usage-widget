using System.Windows;
using System.Windows.Input;
using UsageWidget.Core;

namespace UsageWidget;

// The show/hide shortcut: registering it, and recording a new one in the Accounts view.
public partial class MainWindow
{
    const string HotkeyHint = "Click the box and press a new shortcut. It brings the widget to the front, or minimizes it when it is already in front.";

    void ApplyHotkey()
    {
        var parsed = Hotkey.Parse(settings.Hotkey);
        hotkeyProblem = parsed is not null && hotkey?.Set(parsed) == false ? $"{parsed} is already used by another app; pick another." : null;
        if (parsed is null) hotkey?.Set(null);
    }

    void RenderHotkey(string? note = null)
    {
        HotkeyBox.Text = Hotkey.Parse(settings.Hotkey)?.ToString() ?? "None";
        var problem = note ?? hotkeyProblem;
        HotkeyHelp.Text = problem ?? HotkeyHint;
        HotkeyHelp.Foreground = (System.Windows.Media.Brush)FindResource(problem is null ? "TextSecondary" : "BandWarning");
    }

    // While recording, the current shortcut is released so pressing it again reaches the box instead of hiding the widget.
    void OnHotkeyFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        hotkey?.Set(null);
        HotkeyBox.Text = "Press a shortcut…";
    }

    void OnHotkeyBlur(object sender, KeyboardFocusChangedEventArgs e)
    {
        ApplyHotkey();
        RenderHotkey();
    }

    void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab) return;
        e.Handled = true;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;
        if (key == Key.Escape)
        {
            Keyboard.ClearFocus();
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Ctrl;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Win;

        if (modifiers == HotkeyModifiers.None && key is Key.Back or Key.Delete)
        {
            SetHotkey(null);
            return;
        }

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (Hotkey.KeyName(vk) is null)
        {
            RenderHotkey("That key cannot be used; try a letter, digit or F1–F24 with Ctrl, Alt or Shift.");
            HotkeyBox.Text = "Press a shortcut…";
            return;
        }
        var candidate = new Hotkey(modifiers, vk);
        if (!candidate.IsUsable)
        {
            RenderHotkey("Add Ctrl, Alt or Shift, so typing that key elsewhere still works.");
            HotkeyBox.Text = "Press a shortcut…";
            return;
        }
        SetHotkey(candidate);
    }

    void OnClearHotkeyClick(object sender, RoutedEventArgs e) => SetHotkey(null);

    void SetHotkey(Hotkey? value)
    {
        // Empty rather than null, so a cleared shortcut is saved and the default does not come back.
        settings.Hotkey = value?.ToString() ?? "";
        SaveSettings();
        ApplyHotkey();
        // Leaving the box registers the shortcut; until then it stays released so it can be recorded again.
        if (HotkeyBox.IsKeyboardFocused) hotkey?.Set(null);
        RenderHotkey();
    }
}
