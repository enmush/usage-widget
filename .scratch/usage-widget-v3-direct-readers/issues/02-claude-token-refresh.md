# 02: Refresh expired Claude sign-ins

**What to build:** A Claude Account whose access token has expired keeps updating: the widget refreshes the token and writes it back into the Login folder so Claude Code stays signed in.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Expired token (or a 401) triggers one refresh; new access token, refresh token and expiry are written back atomically with every other field kept
- [ ] A token already refreshed by Claude Code since the last read is used without calling the token endpoint
- [ ] Refresh failure gives a stale Row saying to sign in to Claude Code again
- [ ] Covered by tests with a fake HTTP handler and a temp Login folder
