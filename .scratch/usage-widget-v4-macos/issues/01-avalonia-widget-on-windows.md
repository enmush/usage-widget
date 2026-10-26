# 01: The widget runs on Avalonia on Windows, unchanged for the user

**What to build:** The Windows widget is rebuilt on Avalonia and behaves as it does in v0.3.0: same Rows, columns, bars, hover details, Accounts view, add form, Detect logins, shortcut recording, right-click menu, drag-to-move, click-to-refresh, taskbar minimize/restore, Start with Windows, single instance and clean exit. The Row-to-cells mapping lives in a UI-independent view model, and the Windows-only pieces sit behind the platform services interface.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] The WPF project is gone; one Avalonia desktop project builds for Windows
- [x] The Row display mapping is tested without a window and keeps the current cell texts
- [x] An existing settings file loads unchanged; position, hidden flags and shortcut carry over
- [x] Taskbar button minimizes and restores the widget in place; the minimize button works
- [x] Ctrl+Alt+Shift+U and a recorded shortcut bring the widget to the front or minimize it
- [x] Start with Windows writes the same Run value
- [x] A second launch exits; Exit and Alt+F4 leave no agy or gcloud process
- [x] All tests pass and the published Windows exe starts and shows live Rows on this PC
- [x] An ADR records Avalonia as the UI framework
