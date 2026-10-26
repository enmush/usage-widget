# 05: Shortcut and Start at login on macOS

**What to build:** On a Mac, ⌃⌥⇧U (or a recorded shortcut) brings the widget to the front or minimizes it, the Accounts view records and shows shortcuts with Mac key names, and "Start at login" in the right-click menu adds or removes a LaunchAgent that opens the app.

**Blocked by:** 02

**Status:** in-progress

- [x] Shortcut text round-trips on both platforms; Mac display uses ⌃ ⌥ ⇧ ⌘; key names map to Carbon key codes, with tests
- [x] The shortcut registers through Carbon without an accessibility prompt; a taken shortcut shows the note
- [ ] Start at login writes and removes the LaunchAgent and the menu shows its state (plist unit-tested; menu toggle needs a real Mac)
