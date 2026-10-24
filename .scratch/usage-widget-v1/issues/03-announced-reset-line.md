# 03: Announced reset line under the Codex Row

**What to build:** When codex-resets.com reports a pending scheduled reset, a line under the Codex Row reads "reset announced · <local date>, in <countdown>"; it disappears when nothing is pending or the feed fails.

**Blocked by:** 01

**Status:** done (v0.1.0)

- [ ] Tests cover scheduled reset present, absent (null) and malformed feed
- [ ] With today's live feed (no scheduled reset) the line is hidden
