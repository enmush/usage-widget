# 06: Remove codexbar entirely

**What to build:** The widget no longer references Win-CodexBar anywhere: the codexbar runner, JSON parser, path setting and consent hint are gone, and the docs no longer require it.

**Blocked by:** 02, 03, 04, 05

**Status:** ready-for-agent

- [ ] An old settings file with the codexbar path still loads
- [ ] README, AGENTS.md and release notes drop the Win-CodexBar requirement and mention gcloud for the Gemini API Row
- [ ] All tests pass; a live run on this PC shows every Account with codexbar-cli not used (no codexbar process starts)
