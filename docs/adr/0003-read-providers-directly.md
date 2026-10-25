# Read each provider directly instead of through codexbar-cli

Supersedes ADR 0001. The widget now reads usage itself, so Win-CodexBar no longer has to be installed. Verified on 2026-10-25 on this PC:

- Claude: `GET https://api.anthropic.com/api/oauth/usage` with the Login folder's OAuth token (`.credentials.json`) and `anthropic-beta: oauth-2025-04-20`.
- Codex: `GET https://chatgpt.com/backend-api/wham/usage` and `/wham/rate-limit-reset-credits` with the Login folder's token and account id (`auth.json`).
- Antigravity: `agy -p /usage --output-format json`, a built-in slash command that spends no model tokens.
- Gemini API: Cloud Monitoring request counts for one Google Cloud project, authenticated through `gcloud auth print-access-token`.

All of these except Cloud Monitoring are undocumented, so the widget now owns their breakage, the cost ADR 0001 avoided. The user accepted that to drop the third-party dependency; parsing stays lenient and a failing Account still shows as stale.

## Consequences

- Claude access tokens last about 8 hours. When one has expired the widget refreshes it and writes the new tokens back to the Login folder (atomically), as codexbar did, because refreshing in memory only would rotate the refresh token and sign Claude Code out. It re-reads the file first and uses a token Claude Code has already refreshed, but it cannot prevent Claude Code and the widget refreshing with the same refresh token at the same moment; that rare race, which may sign Claude Code out once, is accepted.
- Codex tokens last about 10 days and are left for Codex to refresh.
- The Gemini API row needs the Google Cloud SDK signed in; it shows request counts only, since Cloud Monitoring has no cost figure.
- Accounts no longer share codexbar state files, so every Account is read in parallel.
