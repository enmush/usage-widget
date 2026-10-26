using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using UsageWidget.Core;

namespace UsageWidget;

// The show/hide shortcut: registering it, and recording a new one in the Accounts view.
public partial class MainWindow
{
    const string HotkeyHint = "Click the box and press a new shortcut. It brings the widget to the front, or hides it when it is already in front.";

    void ApplyHotkey()
    {
        var parsed = Hotkey.Parse(settings.Hotkey);
        hotkeyProblem = parsed is null ? null
            : !platform.Supports(parsed) ? $"{platform.Describe(parsed)} cannot be used on this computer; pick another."
            : !platform.SetHotkey(parsed) ? $"{platform.Describe(parsed)} is already used by another app; pick another."
            : null;
        if (parsed is null) platform.SetHotkey(null);
    }

    void RenderHotkey(string? note = null)
    {
        HotkeyBox.Text = Hotkey.Parse(settings.Hotkey) is { } hotkey ? platform.Describe(hotkey) : "None";
        var problem = note ?? hotkeyProblem;
        HotkeyHelp.Text = problem ?? HotkeyHint;
        HotkeyHelp.Foreground = (IBrush)this.FindResource(problem is null ? "TextSecondary" : "BandWarning")!;
    }

    // While recording, the current shortcut is released so pressing it again reaches the box instead of hiding the widget.
    void OnHotkeyFocus(object? sender, FocusChangedEventArgs e)
    {
        platform.SetHotkey(null);
        HotkeyBox.Text = "Press a shortcut…";
    }

    void OnHotkeyBlur(object? sender, FocusChangedEventArgs e)
    {
        ApplyHotkey();
        RenderHotkey();
    }

    void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab) return;
        e.Handled = true;
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin)
            return;
        if (e.Key == Key.Escape)
        {
            Focus();
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= HotkeyModifiers.Ctrl;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= HotkeyModifiers.Win;

        if (modifiers == HotkeyModifiers.None && e.Key is Key.Back or Key.Delete)
        {
            SetHotkey(null);
            return;
        }

        if (KeyName(e.Key) is not { } name || Hotkey.KeyCode(name) is not { } code)
        {
            RenderHotkey("That key cannot be used; try a letter, digit or F1–F24 with Ctrl, Alt or Shift.");
            HotkeyBox.Text = "Press a shortcut…";
            return;
        }
        var candidate = new Hotkey(modifiers, code);
        if (!platform.Supports(candidate))
        {
            RenderHotkey("That key cannot be used on this computer; try a letter, digit or F1–F20.");
            HotkeyBox.Text = "Press a shortcut…";
            return;
        }
        if (!candidate.IsUsable)
        {
            RenderHotkey("Add Ctrl, Alt or Shift, so typing that key elsewhere still works.");
            HotkeyBox.Text = "Press a shortcut…";
            return;
        }
        SetHotkey(candidate);
    }

    /// <summary>The shortcut key name used in settings, or null for a key the widget does not support.</summary>
    static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => $"Num{(int)(key - Key.NumPad0)}",
        >= Key.F1 and <= Key.F24 => $"F{(int)(key - Key.F1) + 1}",
        Key.Space => "Space",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        Key.End => "End",
        Key.Home => "Home",
        Key.Left => "Left",
        Key.Up => "Up",
        Key.Right => "Right",
        Key.Down => "Down",
        Key.Insert => "Insert",
        Key.Delete => "Delete",
        Key.Pause => "Pause",
        Key.OemTilde => "`",
        _ => null,
    };

    void OnClearHotkeyClick(object? sender, RoutedEventArgs e) => SetHotkey(null);

    void SetHotkey(Hotkey? value)
    {
        // Empty rather than null, so a cleared shortcut is saved and the default does not come back.
        settings.Hotkey = value?.ToString() ?? "";
        SaveSettings();
        ApplyHotkey();
        // Leaving the box registers the shortcut; until then it stays released so it can be recorded again.
        if (HotkeyBox.IsFocused) platform.SetHotkey(null);
        RenderHotkey();
    }
}
