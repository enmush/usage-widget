# Usage Widget

A small sticky-note desktop widget for Windows and macOS (Avalonia, .NET 10; a normal window with a taskbar button or Dock icon) that shows every AI subscription account's usage windows, reset tickets and announced resets in one place. Domain terms are in `GLOSSARY.md`; decisions are in `docs/adr/`.

## Build and run

- Build: `dotnet build src/UsageWidget`
- Test: `dotnet test tests/UsageWidget.Tests`
- Run: `dotnet run --project src/UsageWidget`
- Release packages: `powershell -File tools/publish.ps1` (tests, then both zips and `SHA256SUMS.txt` in `artifacts/release`)
- Reads each provider directly (ADR 0003); the Gemini API Row needs the Google Cloud SDK signed in. Never pass `/usage` to agy through Git Bash: it rewrites the argument into a path and agy sends it to the model.

## Agent skills

### Issue tracker

Issues and specs live as local markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five-role vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: root `GLOSSARY.md` plus `docs/adr/`. See `docs/agents/domain.md`.
