# 01: Claude Rows read directly (reader seam)

**What to build:** Claude Accounts get their session and weekly Windows, plan and model-scoped hover windows straight from Claude's OAuth usage endpoint using the Login folder's credentials, while other Providers still go through codexbar. This introduces the per-Provider reader shape and runs all Accounts in parallel.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] Claude Row values match what codexbar showed for the same Account (session, weekly, nearest reset, plan, scoped hover windows)
- [x] A missing or unreadable credentials file gives a stale Row with a cause, not a crash
- [x] Parsing is a pure function tested from a captured, scrubbed fixture
- [x] Accounts of all Providers are read in parallel; existing stale-Row tests pass
