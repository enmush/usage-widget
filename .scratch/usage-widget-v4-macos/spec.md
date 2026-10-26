Status: done (v0.4.0)

# Usage Widget on macOS (v0.4.0)

## Problem Statement

The user also works on a Mac and wants the same at-a-glance view of every Account's Windows, Reset tickets, Announced resets and API usage there. The widget only runs on Windows, so on the Mac they have to open each Provider's own app to see how much allowance is left.

## Solution

One widget for Windows and macOS, built from the same code. On the Mac it is the same sticky-note window with a Dock icon, it reads the same four Providers with the sign-ins already on the Mac, and it is downloaded as a zipped `.app` for Apple Silicon or Intel. Windows users get the same widget they have now. Both platforms are built, tested and packaged by GitHub Actions when a release tag is pushed.

## User Stories

1. As a Mac user, I want to download a zip from Releases and open a `.app`, so that installing takes one step.
2. As a Mac user with Apple Silicon, I want a native arm64 build, so that it runs without Rosetta.
3. As a Mac user with an Intel Mac, I want an x64 build, so that I can use the widget too.
4. As a Mac user, I want the release notes to tell me how to open an unsigned app the first time, so that Gatekeeper does not stop me.
5. As a Mac user, I want a first run to detect my Accounts automatically, so that Rows appear without setup.
6. As a Mac user, I want my default Claude Account read from the sign-in Claude Code keeps on the Mac, so that its Row shows live Windows.
7. As a Mac user with a second Claude Account in its own Login folder, I want that Account read from its own sign-in, so that the two Claude Rows differ.
8. As a Mac user, I want an expired Claude sign-in refreshed and saved where Claude Code keeps it, so that both the widget and Claude Code stay signed in.
9. As a Mac user, I want the widget never to refresh a Claude sign-in it cannot save back, so that Claude Code is never signed out by the widget.
10. As a Mac user, I want a clear cause on a Claude Row that cannot be read, such as no sign-in found, so that I know what to do.
11. As a Mac user, I want my Codex Accounts read from their Login folders, so that their Windows and Reset tickets appear.
12. As a Mac user whose Codex sign-in is kept in the system credential store instead of the Login folder, I want the Row to say so, so that I know why it has no data.
13. As a Mac user, I want Antigravity read through agy wherever I installed it, so that its Row works even though apps opened from Finder do not get my terminal's PATH.
14. As a Mac user, I want the Gemini API Row read through gcloud wherever I installed the Google Cloud SDK, so that it works without editing PATH for GUI apps.
15. As a Mac user, I want Detect logins to find my Gemini API key's project even though the key is set only in my shell profile, so that the Row is added for me.
16. As a Mac user, I want the window to stay where I dragged it after a restart, so that it behaves like a sticky note.
17. As a Mac user, I want the minimize button and the Dock icon to hide and bring back the widget, so that it is out of the way when I want.
18. As a Mac user, I want ⌃⌥⇧U by default to bring the widget to the front or minimize it, so that I can check usage without the mouse.
19. As a Mac user, I want to record a different shortcut in the Accounts view with the Mac's own key names, so that the shortcut reads naturally.
20. As a Mac user, I want a clear note when the shortcut cannot be registered, so that I pick another.
21. As a Mac user, I want "Start at login" in the right-click menu, so that the widget opens when I sign in.
22. As a Mac user, I want my settings in the Mac's usual application-support folder, so that the file is where Mac apps keep theirs.
23. As a Mac user, I want only one widget running even if I open the app twice, so that I never see duplicate windows.
24. As a Mac user, I want Quit (and ⌘Q) to close the widget and leave no agy or gcloud process running, so that nothing lingers.
25. As a Mac user, I want the Balance link to open AI Studio billing in my default browser, so that I can see the prepaid balance.
26. As a Mac user, I want Browse… to pick a Login folder with the Mac's folder picker, so that adding a second Account is easy.
27. As a Mac user, I want hover details on each Row, so that I see exact percentages, reset times and the plan.
28. As a Mac user, I want the folder help text to use Mac-style paths, so that the examples match my system.
29. As a Windows user, I want the widget to look and behave as it does in v0.3.0, so that the rebuild changes nothing for me.
30. As a Windows user, I want the taskbar button to minimize and restore the widget in place, so that it still behaves like Sticky Notes.
31. As a Windows user, I want Ctrl+Alt+Shift+U and my recorded shortcut to keep working, so that my settings carry over.
32. As a Windows user, I want "Start with Windows" to keep the same entry it uses now, so that an existing startup setting keeps working.
33. As a Windows user, I want my existing settings file to load unchanged, so that my Accounts, position and hidden flags survive the update.
34. As a Windows user, I want the same two Windows zips in each release, so that my download choice is the same.
35. As the maintainer, I want every push built and tested on Windows and macOS, so that a platform break shows before a release.
36. As the maintainer, I want a pushed version tag to produce all packages and checksums and attach them to a draft GitHub release, so that releasing does not need a Mac.
37. As the maintainer, I want the Mac packages signed ad hoc during the build, so that Apple Silicon Macs will run them.
38. As the maintainer, I want the CI run to start both packaged apps and confirm a window appears and exits cleanly, so that a package that does not start is never released.
39. As the maintainer, I want a local Windows build script to keep working, so that I can build Windows packages without CI.

## Implementation Decisions

- **UI framework.** The WPF app is replaced by one Avalonia (12.1 line, pinned to an exact version) desktop app for both platforms. The existing look is kept: dark surface, the same colours, columns, bars, pills, flat buttons and hover details. Fonts fall back to each platform's system UI font, and icon glyphs come from a font available on both platforms or from drawn paths. A new ADR records the choice of Avalonia.
- **Core stays the domain.** Readers, Row building, Formatter, settings, Account catalog and detection stay in the core library. The display mapping from a Row to its cells (texts, fractions, bands, visibility) moves into a UI-independent view model, so it can be tested without a window.
- **Platform services module.** One small interface covers what differs between Windows and macOS: settings folder, default shortcut and its display names, global shortcut registration, start-at-login, finding a Provider tool (agy, gcloud), the environment a tool runs with, and minimize/restore behaviour. A Windows and a macOS implementation are chosen at start-up by the running OS. Pure decisions inside it (candidate tool locations, settings path, key name mapping) are plain functions of the OS and home folder.
- **Claude sign-in store.** Claude reading goes through a sign-in store with two operations: load the sign-in for a Login folder, and save new tokens for it. The file store keeps today's behaviour (the Login folder's credentials file, saved atomically, only token fields changed). The macOS store reads the system Keychain item Claude Code uses and falls back to the credentials file when no item exists.
  - Keychain service name: `Claude Code-credentials` when the Account uses the default Login folder without an override; otherwise `Claude Code-credentials-` plus the first 8 hex digits of the SHA-256 of the Login folder path (NFC-normalised). Account name `claude-code-user`. Read from Claude Code 2.1.310's own code on 2026-10-26.
  - Save writes back the full JSON with only the token fields changed, to the same service and account the item was read from.
  - Before refreshing, the macOS store first proves it can write the item by saving the unchanged JSON. If that fails, the Row shows "Sign-in expired: open Claude Code" and no refresh happens.
  - Keychain access goes through the system `security` tool, run with the existing CLI runner.
- **Codex.** Reads `auth.json` in the Login folder on both platforms. When the file is missing but Codex is set to keep its sign-in in the system credential store, the Row fails with "Codex keeps this sign-in in the system credential store, which the widget cannot read yet".
- **Finding tools on macOS.** A Mac app opened from Finder has a minimal PATH. Tool lookup tries the process PATH, then the usual folders (Homebrew on both architectures, `/usr/local/bin`, the user's `~/.local/bin`, `~/google-cloud-sdk/bin`, agy's own install folder), then asks the user's login shell once and caches the answer. Tools run with that resolved PATH. `GEMINI_API_KEY` for Detect logins is taken from the process environment, or else from the login shell.
- **Login detection.** Claude detection on macOS counts a Login folder as signed in when its Keychain item or its credentials file exists. Codex detection is unchanged.
- **Settings.** Same JSON format and fields on both platforms. Windows path unchanged; macOS path `~/Library/Application Support/UsageWidget/settings.json`. The error log sits next to it.
- **Shortcut.** Settings store the shortcut as text with the same modifier names (`Ctrl`, `Alt`, `Shift`, `Win`), shown on macOS as ⌃ ⌥ ⇧ ⌘. The key stays a platform-neutral key name; each platform maps it to its own key code (Win32 virtual key; macOS Carbon key code). Default `Ctrl+Alt+Shift+U` on both. Windows registers through RegisterHotKey on the window handle; macOS through Carbon's RegisterEventHotKey, which needs no accessibility permission.
- **Start at login.** Windows keeps the current user's Run value with the same name. macOS writes a LaunchAgent plist in the user's LaunchAgents folder that opens the `.app`; the menu item reads "Start at login".
- **Window behaviour.** Borderless, not always on top, draggable, sized to content, position saved and clamped on screen. Windows keeps the taskbar button minimize/restore (the minimize style bit on the native handle). macOS shows a Dock icon; a borderless window cannot be miniaturized, so minimizing hides the window and clicking the Dock icon shows it again. Close (Alt+F4, ⌘Q, Quit) exits the app and kills tool processes.
- **Single instance.** A per-user lock on both platforms; a second launch exits.
- **Packaging.** Windows: the same two zips. macOS: `UsageWidget-<version>-macos-arm64.zip` and `-macos-x64.zip`, each holding `Usage Widget.app` (self-contained, Info.plist with the version, an icon, ad-hoc signed on a macOS runner). The minimum is macOS 15, the oldest macOS .NET 10 supports. One `SHA256SUMS.txt` covers all four zips.
- **CI.** One GitHub Actions workflow: on push and pull request, build and test on Windows and macOS. On a `v*` tag, build all packages, ad-hoc sign and smoke-start the Mac apps on macOS, smoke-start the Windows exe on Windows, and attach the packages and checksums to a draft release. The local Windows build script keeps producing the two Windows zips.
- **Version.** 0.4.0.

## Testing Decisions

- Good tests exercise behaviour through public seams with recorded fixtures and fakes, never private details; the existing reader, builder, formatter, accounts and hotkey tests are the prior art.
- **Claude reader with a sign-in store fake:** load, refresh-and-save, already-refreshed-by-Claude-Code, cannot-save-so-no-refresh, missing sign-in. The current file behaviour keeps its tests through the file store.
- **Keychain service name:** default folder, overridden folder, overridden default folder, NFC normalisation; checked against the value Claude Code computes for a known path.
- **macOS Keychain store:** through a fake runner for the `security` tool (item found, not found, write refused), so it is tested on any OS.
- **Tool lookup and settings path:** candidate folder order and the login-shell fallback as pure functions given an OS, home and a fake file check.
- **Shortcut:** text round-trips on both platforms, Mac display names, and key-name to Carbon key code mapping.
- **Row display mapping:** the moved view model keeps the current cell texts (countdowns, percent, tickets, API counts, stale marker).
- **CI smoke run:** each package starts, shows a window within a timeout, and exits cleanly on request, on its own OS.
- **Manual check:** on Windows, minimize/restore from the taskbar, drag, shortcut, Start with Windows and the Accounts view, compared with v0.3.0. On a real Mac, the user confirms live Rows, the shortcut and Start at login.

## Out of Scope

- Developer ID signing and notarization.
- A menu bar mode on macOS.
- Reading Codex sign-ins kept in the macOS Keychain.
- Linux packages.
- Universal (single fat) Mac binaries.

## Further Notes

- The Keychain naming rule comes from Claude Code's own code and may change with Claude Code; a mismatch shows as a stale or empty Claude Row with "Not signed in", not as a crash.
- Reading a Keychain item through `security` may show a macOS permission prompt the first time, depending on how Claude Code created the item; the release notes say to choose "Always Allow".
