# 04: Antigravity and Gemini API Rows on macOS

**What to build:** On a Mac, the widget finds agy and gcloud even when opened from Finder with a minimal PATH, runs them with the resolved PATH, and Detect logins finds the Gemini API key's project when the key is set only in the shell profile.

**Blocked by:** 02

**Status:** in-progress

- [x] Tool lookup tries the process PATH, the usual install folders, then the login shell once, with tests on the order and fallback
- [x] agy and gcloud run with the resolved PATH on macOS; Windows lookup is unchanged
- [x] `GEMINI_API_KEY` comes from the process environment or the login shell
- [ ] The macOS CI smoke run shows the "not found" causes cleanly when the tools are absent (CI has no Accounts, so no Rows; needs a real Mac)
