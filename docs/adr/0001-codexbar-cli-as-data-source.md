# Read usage through codexbar-cli instead of calling providers directly

The widget gets every account's windows and reset tickets by running Win-CodexBar's `codexbar-cli usage --json` (one run per account, with `CLAUDE_CONFIG_DIR` selecting the Claude account) rather than talking to Anthropic, OpenAI and Google itself. On 2026-10-24 this CLI already handled the hard parts on this PC: Claude OAuth refresh, Codex app-server, and Antigravity through `agy -p /usage`, while direct Gemini CLI access was blocked by Google (UNSUPPORTED_CLIENT). Re-implementing those undocumented, fast-changing endpoints would mean owning their breakage.

## Consequences

- Win-CodexBar must stay installed; the widget breaks if its JSON shape changes, so parsing is lenient and a failing account shows as stale rather than crashing.
- Things Win-CodexBar does not provide (announced resets from codex-resets.com, later Dreamina and API budgets) are fetched by the widget itself.
- Win-CodexBar 0.70.0 reads Claude reset tickets only through browser cookies, so Claude tickets stay hidden until it reads them through OAuth.
