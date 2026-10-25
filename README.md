# Usage Widget

A small Windows desktop widget that shows the usage limits of all your AI subscription accounts at once: Claude, Codex and Antigravity (Gemini), several accounts each, with reset countdowns, Codex reset tickets and announced Codex resets. It can also show how many Gemini API requests a Google Cloud project made today and this month.

It sits on the desktop like a sticky note. Other windows can cover it, the **—** button on its header minimizes it, and its taskbar button brings it back to the same place.

![Usage view](docs/images/usage.png)

Each Row is one Account: the 5-hour and weekly windows (or, for a Gemini API Account, request counts and a **Balance ↗** link that opens AI Studio billing in your browser, since Google shows the prepaid balance only there) (bars turn amber at 70 % and red at 90 %), the time left in the 5-hour window (above its bar), the time to the weekly reset, and reset tickets with the nearest expiry. Hover a Row for details. A `!` means the last read failed; the Row then keeps its last known values, dimmed.

## Requirements

- Windows 10 or 11, x64.
- Signed in with the providers' own CLIs: `claude` (Claude Code), `codex` (Codex CLI, with a ChatGPT sign-in), and `agy` (Antigravity CLI 1.1.11 or later, for Gemini usage).
- For a Gemini API Account: the [Google Cloud SDK](https://cloud.google.com/sdk/docs/install) signed in with `gcloud auth login`, and the Cloud Monitoring API enabled on the project.
- The small download also needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). The `standalone` download does not.

## Install

1. Download a zip from [Releases](../../releases) and extract it anywhere, for example `%LOCALAPPDATA%\Programs\UsageWidget`.
2. Run `UsageWidget.exe`. On first run it detects the Accounts signed in on your PC.
3. Optional: right-click the widget and choose **Start with Windows**.

Windows SmartScreen may warn about an unsigned app; choose **More info → Run anyway**.

## Accounts

Click **⚙** to manage Accounts:

![Accounts view](docs/images/accounts.png)

- **Checkbox** shows or hides an Account's Row. A hidden Account keeps its settings and is not read at all.
- **✕** removes an Account.
- **+ Add account** adds one by name, Provider and Login folder (the folder where that Provider's CLI keeps the sign-in), or for the Gemini API, the Google Cloud project id.
- **Detect logins** adds Accounts found on the PC and skips ones already listed. If `GEMINI_API_KEY` is set, it also adds the project that key belongs to.

### A second Claude or Codex Account

Each CLI keeps one sign-in per folder. To add a second Account, sign in with the folder variable set, then add that folder in the widget:

```
set CLAUDE_CONFIG_DIR=%USERPROFILE%\.claude-acct2
claude
```

```
set CODEX_HOME=%USERPROFILE%\.codex-work
codex login
```

Detect logins finds `.claude*` folders that contain `.credentials.json` and `.codex*` folders that contain `auth.json` in your home folder.

## Settings

Settings are stored in `%APPDATA%\UsageWidget\settings.json` and can be edited by hand while the widget is closed:

| Key | Default | Meaning |
| --- | --- | --- |
| `refreshMinutes` | `5` | How often every shown Account is read |
| `showAnnouncements` | `true` | Show announced Codex resets under Codex Rows |
| `accounts` | detected | Each Account's `name`, `provider`, optional `env` and `hidden` |

If the file cannot be read, the widget starts with detected Accounts and keeps the broken file as `settings.json.broken`. Startup errors are written to `%APPDATA%\UsageWidget\error.log`.

Click the widget's background to refresh now; drag it to move it.

## Data sources and privacy

- The widget reads usage itself, with the sign-ins already on your PC, from the same places the providers' own apps use (see [ADR 0003](docs/adr/0003-read-providers-directly.md)): Claude's and ChatGPT's usage endpoints, `agy`'s built-in `/usage` report, and Google Cloud Monitoring. Your tokens go only to the provider that issued them. Except Cloud Monitoring, these endpoints are undocumented and may change; an Account whose read fails keeps its last values, dimmed.
- When a Claude sign-in has expired, the widget refreshes it and saves the new tokens in the same file, exactly as Claude Code does, so Claude Code stays signed in.
- Announced Codex resets come from the unofficial public feed `https://codex-resets.com/api/v1/status`. Turn this off with `showAnnouncements: false`.

This project is not affiliated with Anthropic, OpenAI or Google.

## Build from source

Requires the .NET 10 SDK.

```
dotnet test tests/UsageWidget.Tests
dotnet run --project src/UsageWidget
```

`powershell -File tools/publish.ps1` runs the tests and builds both release zips with `SHA256SUMS.txt` into `artifacts/release`.

Domain terms are defined in [GLOSSARY.md](GLOSSARY.md) and decisions in [docs/adr](docs/adr).

## License

[MIT](LICENSE)
