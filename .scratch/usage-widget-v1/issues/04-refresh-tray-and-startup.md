# 04: Refresh loop, tray icon, menu and start with Windows

**What to build:** Data refreshes every 5 minutes and when a Row is clicked, one refresh at a time, with a "last updated" time shown. A tray icon toggles the window. Right-clicking the window or tray icon offers Refresh, Hide/Show, Start with Windows (checkable) and Exit.

**Blocked by:** 01

**Status:** done (v0.1.0)

- [ ] Clicking a Row triggers a refresh and updates "last updated"
- [ ] Tray icon left-click hides and shows the window at the same position
- [ ] Start with Windows toggles the current user's Run entry
- [ ] Exit closes the app and leaves no child process running
