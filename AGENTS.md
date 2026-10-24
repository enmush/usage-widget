# Usage Widget

A small sticky-note Windows widget (WPF, .NET 10; a normal desktop window with a taskbar button) that shows every AI subscription account's usage windows, reset tickets and announced resets in one place. Domain terms are in `GLOSSARY.md`; decisions are in `docs/adr/`.

## Build and run

- Build: `dotnet build src/UsageWidget`
- Test: `dotnet test tests/UsageWidget.Tests`
- Run: `dotnet run --project src/UsageWidget`
- Requires Win-CodexBar installed (`codexbar-cli.exe` under `%LOCALAPPDATA%\Programs\CodexBar`); see ADR 0001.

## Agent skills

### Issue tracker

Issues and specs live as local markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five-role vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: root `GLOSSARY.md` plus `docs/adr/`. See `docs/agents/domain.md`.
