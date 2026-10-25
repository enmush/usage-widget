# 03: Codex Rows read directly, with Reset tickets

**What to build:** Codex Accounts show their Windows, plan and Reset tickets (count and nearest expiry) from Codex's usage and reset-credits endpoints using the Login folder's `auth.json`.

**Blocked by:** 01

**Status:** done

- [x] Windows are mapped by length (about 5 hours → session, about 7 days → weekly)
- [x] Ticket count is the available credits; expiry is the earliest `expires_at`
- [x] Expired or missing sign-in gives a stale Row saying to sign in to Codex again
- [x] Parsing tested from captured, scrubbed fixtures
