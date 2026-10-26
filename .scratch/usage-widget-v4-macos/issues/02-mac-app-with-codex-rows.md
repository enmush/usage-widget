# 02: A Mac `.app` starts and shows Codex Rows, built by CI

**What to build:** A Mac user opens a zipped `Usage Widget.app` (arm64 or x64) and sees the sticky-note window with a Dock icon, Codex Rows from the Login folders, settings kept in Application Support, one instance only, minimize to the Dock and restore from it. GitHub Actions builds and tests on Windows and macOS on every push, and on macOS packages, ad-hoc signs and smoke-starts both Mac apps.

**Blocked by:** 01

**Status:** in-progress

- [ ] Platform services have a macOS implementation chosen at start-up (settings path, single instance, open URL, folder picker, window behaviour)
- [ ] Settings path is `~/Library/Application Support/UsageWidget/settings.json` on macOS and unchanged on Windows, covered by tests
- [ ] Codex Rows read on macOS; a missing `auth.json` with Keychain storage gives the stated cause
- [ ] The workflow builds and tests on both OSes on push and pull request
- [ ] The macOS job produces arm64 and x64 `.app` zips with the version in Info.plist and an icon, ad-hoc signed
- [ ] The macOS job starts the packaged app, sees its window and quits it cleanly
- [ ] Folder help text and displayed paths use `/` on macOS
