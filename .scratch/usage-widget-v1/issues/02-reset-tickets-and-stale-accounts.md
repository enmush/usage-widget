# 02: Reset ticket badges, hover details and stale Accounts

**What to build:** A Row shows a Reset ticket badge with count and nearest expiry when the Account has tickets, and no badge otherwise. Hovering a Row shows the plan name, exact local reset times and any extra Window (Antigravity's Claude/GPT weekly). When an Account's read fails, its Row keeps the last good values, is visibly marked stale and shows the error on hover, while other Rows still update.

**Blocked by:** 01

**Status:** done (v0.1.0)

- [ ] Codex Row shows "1" ticket badge with the 11/7 expiry from live data
- [ ] Tests cover ticket parsing, missing tickets, error objects and malformed JSON
- [ ] A forced failure (bad provider id in settings) marks only that Row stale
