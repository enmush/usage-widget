Status: ready-for-agent

# Spec: Usage Widget v1

## Problem Statement

The user pays for several AI subscriptions (two Claude accounts, one Codex/ChatGPT Pro account, one Antigravity account for Gemini) and needs to know, without starting an agent, how much of each account's 5-hour and weekly window is used, when each window resets, whether a reset ticket is available and when it expires, and whether Codex has announced an upcoming reset. Win-CodexBar shows this data but as a tray popup: the popup's top edge moves whenever its menu changes, and it shows one provider at a time, so the user cannot see everything at a glance.

## Solution

A small, borderless, always-on-top window that stays where the user put it and shows one Row per Account, all at once. Each Row shows the Account name, a 5-hour Window bar with percent, a weekly Window bar with percent, a countdown to the nearest Reset, and a Reset ticket badge with the nearest expiry. Below the Codex Row, an Announced reset line appears when one is pending. Data refreshes every 5 minutes and on demand. A tray icon shows and hides the window; a right-click menu offers refresh, hide, start with Windows and exit.

## User Stories

1. As the user, I want every Account on one small window, so that I see all remaining allowance at a glance.
2. As the user, I want the window to stay above other windows, so that I can check usage while working.
3. As the user, I want to drag the window anywhere, so that it does not cover what I am working on.
4. As the user, I want the window to reopen where I left it, so that its position never jumps.
5. As the user, I want the window's size to stay fixed when data changes, so that its top edge never moves.
6. As the user, I want one Row per Account, so that two Claude Accounts are not merged.
7. As the user, I want each Row labelled with a short Account name (Claude 1, Claude 2, Codex, Antigravity), so that I know which login it is.
8. As the user, I want the plan name (for example Max 5x, Pro) available on hover, so that the Row stays short.
9. As the user, I want a 5-hour Window bar with percent used, so that I know how close I am to the session limit.
10. As the user, I want a weekly Window bar with percent used, so that I can pace the week.
11. As the user, I want a dash instead of a 5-hour bar when the provider reports no active session, so that an empty window is not mistaken for 0 % of a running one.
12. As the user, I want bars to change colour as usage passes 70 % and 90 %, so that danger stands out.
13. As the user, I want the countdown to the nearest Reset in days/hours/minutes, so that I know when allowance returns.
14. As the user, I want the exact local reset time on hover, so that I can plan around it.
15. As the user, I want a Reset ticket badge with the count and nearest expiry date, so that I use tickets before they expire.
16. As the user, I want no badge when an Account has no Reset tickets, so that the Row stays clean.
17. As the user, I want an Announced reset line under the Codex Row ("reset announced · Oct 24, in 5h") when Codex has announced one, so that I can save usage before it.
18. As the user, I want the Announced reset line to disappear when nothing is pending, so that stale news does not linger.
19. As the user, I want Antigravity's Gemini weekly Window shown in its Row and its Claude/GPT weekly Window on hover, so that the Row stays one line.
20. As the user, I want data refreshed every 5 minutes, so that numbers stay current without action.
21. As the user, I want to click a Row (or use the menu) to refresh now, so that I can check right after heavy use.
22. As the user, I want a "last updated" time, so that I know how fresh the numbers are.
23. As the user, I want a failing Account to keep its last good values marked stale with the error on hover, so that one broken login does not blank the widget.
24. As the user, I want the other Accounts to keep updating when one fails, so that failures are isolated.
25. As the user, I want a tray icon that shows and hides the window, so that I can clear the screen quickly.
26. As the user, I want a right-click menu with Refresh, Hide, Start with Windows and Exit, so that every control is one click away.
27. As the user, I want the widget to start with Windows when I enable it, so that it is always there.
28. As the user, I want a dark, opaque look with rounded corners that matches Windows 11, so that it does not distract and stays readable over bright windows (translucency was tried and let text behind it show through).
29. As the user, I want the Account list to come from a small settings file, so that a later third Claude Account is a one-line change.
30. As the user, I want refreshing to cost no model usage, so that checking allowance does not consume it.

## Implementation Decisions

- **Data source (ADR 0001):** each Account is read by running Win-CodexBar's `codexbar-cli usage -p <provider> --json`, with per-Account environment overrides (`CLAUDE_CONFIG_DIR` for the second Claude Account; `ANTIGRAVITY_CLI_PATH` when `agy` is not on PATH). Providers run in parallel, Accounts of one provider in sequence (ADR 0001), each with a timeout. Win-CodexBar's Claude Code credential consent is already enabled on this PC.
- **Announced reset:** fetched from `https://codex-resets.com/api/v1/status`; the `data.scheduled_reset` object (`scheduled_for`, `announced_at`, `reset_type`, `text`) drives the line. The source is unofficial; failures hide the line silently.
- **Modules:**
  - *Account settings*: a JSON file in the user's AppData listing Accounts (display name, provider id, environment overrides) plus window position; defaults to the four known Accounts. The start-with-Windows flag lives only in the Run registry value, so the menu and Windows never disagree.
  - *Usage reader*: runs the CLI for one Account and returns the raw JSON text or a failure.
  - *Snapshot builder* (pure): turns raw codexbar JSON per Account, the codex-resets status JSON, the previous snapshot and the current time into a list of Row models (name, 5-hour percent or none, weekly percent, nearest reset time, ticket count and nearest expiry, stale flag and error, hover details) plus an optional Announced reset model. All parsing is lenient: unknown or missing fields never throw.
  - *Formatter* (pure): countdown text ("32m", "2h 22m", "6d 4h"), local date text, colour band for a percent.
  - *Widget window*: borderless always-on-top WPF window, fixed width, drag to move, position saved on move; renders Rows; click to refresh; tray icon and context menu.
- **codexbar JSON contract used (observed 2026-10-24, Win-CodexBar 0.70.0):** an array with one object `{provider, source, usage:{primary, secondary, extra_rate_windows[], login_method, primary_label, secondary_label, updated_at}, error?, errorKind?}`. A window is `{used_percent, window_minutes, resets_at?, reset_description, is_informational}`; `is_informational: true` on the primary window with "No active 5h session" means no running session. Reset tickets appear as an `extra_rate_windows` entry with `id: "reset-credits"`, whose `reset_description` holds the count ("1 reset credit available") and `resets_at` the nearest expiry. Antigravity's extra window `id` contains `claude-gpt` for the Claude/GPT weekly Window.
- **Nearest reset:** the earliest future `resets_at` among the non-informational 5-hour and weekly Windows.
- **Refresh:** every 5 minutes, on click, and once at start; one refresh at a time.
- **Startup:** "Start with Windows" writes or removes a value under the current user's Run registry key.

## Testing Decisions

- One test seam: the Snapshot builder plus Formatter, exercised with recorded codexbar JSON fixtures (Claude with both windows, Claude second Account, Codex with a reset ticket, Antigravity with the extra window, an error object, malformed JSON) and codex-resets fixtures (scheduled reset present, absent, malformed). Tests assert only the Row models and texts a user would see, never internal helpers.
- The Usage reader, settings persistence and the WPF window are thin and verified by running the app and looking at it, not by unit tests.
- No prior tests exist in this new repo; xUnit is used.

## Out of Scope

- Dreamina credits and API key budgets (planned for v2).
- Claude Reset tickets: Win-CodexBar 0.70.0 reads them only through browser cookies, which Edge App-Bound Encryption blocks; they appear once Win-CodexBar reads them through OAuth.
- Claude announced resets: no source was found.
- Notifications and sounds.
- Editing Accounts in the UI (the settings file is edited by hand).
- Multiple Codex Accounts (only one Codex login exists on this PC).

## Further Notes

- Verified data on 2026-10-24: Claude 1 (5h 83 %, weekly 21 %), Claude 2 Max 5x (5h 0 %, weekly 4 %), Codex Pro (weekly 12 %, 1 Reset ticket expiring 2026-11-06T23:33Z), Antigravity (Gemini weekly 0 %).
- Running codexbar for Claude may refresh and rewrite Claude Code's OAuth file (consent given; backups of both credential files exist).
