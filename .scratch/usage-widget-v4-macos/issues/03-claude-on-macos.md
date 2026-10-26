# 03: Claude Rows on macOS from Claude Code's Keychain sign-in

**What to build:** On a Mac, each Claude Account is read from the sign-in Claude Code keeps in the Keychain for its Login folder, falling back to the credentials file. An expired sign-in is refreshed and saved back to the same item, but only after the widget has proved it can write that item. Detect logins finds Claude Accounts that have a Keychain item or a credentials file.

**Blocked by:** 02

**Status:** done

- [x] Claude reading goes through a sign-in store; the file store keeps today's tests green
- [x] Keychain service name follows Claude Code's rule (default, overridden, NFC) with tests on known values
- [x] The Keychain store is tested through a fake `security` runner: found, not found, write refused
- [x] Refresh happens only after a successful no-change write; otherwise the Row says to open Claude Code
- [x] Detection counts Keychain items on macOS
- [x] The release notes text explains the possible first-time Keychain prompt
