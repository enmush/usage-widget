# 03: Add an Account and detect logins

**What to build:** In the Accounts view, "+ Add account" opens an inline form (name, Provider, Login folder with Browse) that validates and adds the Account. "Detect logins" adds Accounts found on the PC without duplicates. A first run with no settings file uses detection instead of a fixed list, and shows a hint when nothing is found.

**Blocked by:** 02

**Status:** done (v0.2.0)

- [x] Catalog and detector tests pass (env mapping, validation, temp-home detection, merge without duplicates)
- [x] On this PC detection finds Claude, Claude acct2, Codex and Antigravity
- [x] Adding a Codex Account with a CODEX_HOME folder produces a Row
