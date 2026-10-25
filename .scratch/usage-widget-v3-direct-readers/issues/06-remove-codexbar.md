# 06: Remove codexbar entirely

**What to build:** The widget no longer references Win-CodexBar anywhere: the codexbar runner, JSON parser, path setting and consent hint are gone, and the docs no longer require it.

**Blocked by:** 02, 03, 04, 05

**Status:** done

- [x] An old settings file with the codexbar path still loads
- [x] README and AGENTS.md drop the Win-CodexBar requirement and mention gcloud for the Gemini API Row
- [ ] Release notes for 0.3.0 (written at release time; the local 0.2.0 notes are untracked and left as they are)
- [x] All tests pass; a live run of the readers on this PC (2026-10-25) showed all five Accounts with no codexbar process started
- [ ] The packaged widget UI itself was not started: the user's installed v0.2.0 was running and holds the single-instance lock
