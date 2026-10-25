# 05: Gemini API usage Row

**What to build:** A new Gemini API Provider whose Account is a Google Cloud project. Its Row shows API usage: requests today and this month (local calendar) from Cloud Monitoring, authenticated through gcloud. It can be added in the Accounts view (project field) and found by "Detect logins" from `GEMINI_API_KEY`.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Row shows "N today · M this month" for the project, matching a manual Cloud Monitoring query
- [ ] Missing or signed-out gcloud gives a stale Row naming the cause
- [ ] Add form takes a project id for this Provider; detection adds the key's project once
- [ ] A Row with API usage and no Windows counts as having data for the stale logic
- [ ] Catalog, detection and parsing covered by tests
