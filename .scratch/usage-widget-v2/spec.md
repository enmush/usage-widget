Status: done (v0.2.0)

# Spec: Usage Widget v2 (sticky-note window and Account management)

## Problem Statement

v1 floats above every window and can only be hidden from the tray icon, which the user finds awkward; they want it to behave like Sticky Notes: a normal desktop window with its own minimize control and a taskbar button that brings it to the front. The Account list can only be changed by editing a JSON file and ships with this PC's Accounts hard-coded, so the user cannot hide a Row from the panel, and someone else installing the widget cannot add their own Accounts.

## Solution

The widget becomes a normal, borderless desktop window (not always on top) with a taskbar button and a minimize button on its own header; clicking the taskbar button restores it and brings it to the front. The tray icon is removed. A ⚙ button on the header flips the same panel into an Accounts view, where each Account has a show/hide checkbox and a remove button, and where the user can add an Account (name, Provider, Login folder) or detect the logins present on the PC. On a first run with no settings file, the widget detects the Accounts on the PC instead of using a fixed list.

## User Stories

1. As the user, I want the widget to sit on the desktop like a sticky note instead of above every window, so that it does not cover my work.
2. As the user, I want a minimize button on the widget's own header, so that I can put it away without the tray.
3. As the user, I want the widget to have a taskbar button, so that one click brings it to the front.
4. As the user, I want the widget to come back at the same place and size after minimizing, so that it never jumps.
5. As the user, I want a ⚙ button on the header that switches the panel to an Accounts view, so that I manage Accounts without another window.
6. As the user, I want a checkbox per Account to show or hide its Row, so that I only see what matters now.
7. As the user, I want a Hidden account to keep its settings, so that showing it again needs no re-entry.
8. As the user, I want to remove an Account from the list, so that old logins disappear.
9. As the user, I want to add an Account by name, Provider and Login folder, so that I can track another login.
10. As the user, I want the Login folder to have a Browse button, so that I don't type paths.
11. As the user, I want the Provider list to offer Claude, Codex and Antigravity, so that I pick from what the widget supports.
12. As the user, I want the Login folder field disabled for Antigravity, so that I am not asked for something it cannot use.
13. As the user, I want an error message when the name is empty or the folder does not exist, so that I don't save a broken Account.
14. As the user, I want a "Detect logins" button that adds Accounts found on this PC, so that setup takes one click.
15. As the user, I want detection to skip Accounts already in the list, so that it never creates duplicates.
16. As a new user on another PC, I want the widget to detect my Accounts on first run, so that it works without editing files.
17. As a new user with no logins found, I want the Accounts view to open with a hint, so that I know what to do.
18. As the user, I want Done to return to the usage view and refresh, so that new Accounts show at once.
19. As the user, I want every change in the Accounts view saved immediately, so that nothing is lost on exit.
20. As the user, I want controls in the panel to never start a window drag, so that clicking a checkbox does not move the widget.
21. As the user, I want the right-click menu to offer Refresh, Accounts…, Start with Windows and Exit, so that every action is reachable.

## Implementation Decisions

- **Window behaviour:** not topmost, shown in the taskbar with the app icon, borderless with rounded corners. The minimize button sets the window minimized; the native minimize style bit is added so the taskbar button minimizes and restores it. Alt+F4 closes the app normally now that the taskbar button exists. The tray icon is removed.
- **Account settings:** each Account gains a `hidden` flag (default false, so v1 files load unchanged). The Login folder is stored through the existing per-Account environment overrides: `CLAUDE_CONFIG_DIR` for Claude, `CODEX_HOME` for Codex (both honoured by Win-CodexBar 0.70.0); the default folders (`~\.claude`, `~\.codex`) need no override. Antigravity has no Login folder.
- **Account catalog (core, pure):** builds an Account from (name, Provider, Login folder), validating name and folder, and reports an Account's Provider and Login folder for display.
- **Login detector (core):** given the user's home folder, finds `.claude*` folders (the default `.claude` if it exists; others only when they contain `.credentials.json`), `.codex*` folders containing `auth.json`, and Antigravity when `agy` is found on PATH or in its winget package folder. It returns Accounts named after the Provider and folder suffix ("Claude", "Claude acct2", "Codex", "Antigravity") and merges into an existing list by Provider plus Login folder, never removing or renaming.
- **First run:** a missing settings file is created from the detector's result; the hard-coded v1 list is removed.
- **Rows:** Hidden accounts are skipped when reading and rendering, so they cost no codexbar run.
- **Panel views:** one window with two views (usage, Accounts) switched by ⚙ and Done; the add form is inline at the bottom of the Accounts view.
- **Drag:** a press anywhere except a control starts a drag, including labels in the Accounts list (user decision, 2026-10-25: blocking moves there is unnecessary); presses on buttons, checkboxes and text fields do not. Click-to-refresh stays on usage Rows.

## Testing Decisions

- Test seam stays the core library: the Account catalog (validation, env mapping per Provider, folder display) and the Login detector against a temporary home folder with fake `.claude*`/`.codex*` folders, plus merge rules (no duplicates, existing names kept, hidden flag kept).
- Settings loading test: a v1 settings JSON without `hidden` loads with every Account shown.
- Window, view switching and dialogs are verified by running the app and capturing it.

## Out of Scope

- Reordering Accounts, multiple Antigravity Accounts, signing in from the widget, packaging an installer for other users (they still install Win-CodexBar and copy the exe), Dreamina and API budgets.

## Further Notes

- Win-CodexBar's Claude Code credential consent must be enabled for each user (`codexbar-cli config claude-code-credentials allow`); the Accounts view hint mentions it.
