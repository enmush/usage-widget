# 01: Live Rows for every Account in a fixed, always-on-top window

**What to build:** Starting the app opens a borderless always-on-top window at its saved position (centre-right on first run) showing one Row per Account from the settings file (defaults: Claude 1, Claude 2, Codex, Antigravity). Each Row shows the Account name, the 5-hour Window bar and percent (a dash when no session is running), the weekly Window bar and percent, and the countdown to the nearest Reset, read once at start through codexbar-cli. Dragging moves the window and the new position is remembered across restarts; the window size never changes with data.

**Blocked by:** None (can start immediately).

**Status:** done (v0.1.0)

- [ ] Snapshot builder and Formatter tests pass on recorded fixtures for both Claude Accounts, Codex and Antigravity
- [ ] Running the app shows four Rows with values matching `codexbar-cli usage` for each Account
- [ ] Second Claude Account is read with its own config dir
- [ ] Window stays above other windows, can be dragged, and reopens at the same place
- [ ] Bars use colour bands at 70 % and 90 %
