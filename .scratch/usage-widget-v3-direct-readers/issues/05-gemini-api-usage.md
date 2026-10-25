# 05: Gemini API usage Row

**What to build:** A new Gemini API Provider whose Account is a Google Cloud project. Its Row shows API usage: requests today and this month (local calendar) from Cloud Monitoring, authenticated through gcloud. It can be added in the Accounts view (project field) and found by "Detect logins" from `GEMINI_API_KEY`.

**Blocked by:** 01

**Status:** done

- [x] Row shows "N today · M this month" for the project, matching a manual Cloud Monitoring query
- [x] Missing or signed-out gcloud gives a stale Row naming the cause
- [x] Add form takes a project id for this Provider; detection adds the key's project once
- [x] A Row with API usage and no Windows counts as having data for the stale logic
- [x] Catalog, detection and parsing covered by tests
