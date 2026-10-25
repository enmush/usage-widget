Status: ready-for-agent

# Spec: Usage Widget v3 (read providers directly, add Gemini API usage)

## Problem Statement

The widget only works when Win-CodexBar is installed, because every Row comes from running `codexbar-cli`. The user wants no third-party dependency for reading their own usage. They also pay for the Gemini API by usage in the Google Cloud project `jun-personal` and cannot see that next to their subscriptions.

## Solution

The widget reads each Provider itself (ADR 0003): Claude and Codex through the usage endpoints their own apps use, with the sign-in already stored in each Login folder; Antigravity through its CLI's built-in `/usage` report; and the Gemini API through Cloud Monitoring request counts for one Google Cloud project. Rows look and behave as before. A new kind of Row shows API usage: requests today and this month. Win-CodexBar can be uninstalled.

## User Stories

1. As the user, I want the widget to work without Win-CodexBar installed, so that I depend on one fewer third-party tool.
2. As the user, I want each Claude Account's session and weekly Windows to show as before, so that nothing I rely on is lost.
3. As the user, I want a Claude Account I have not used for hours to keep updating, so that an expired sign-in does not make its Row stale.
4. As the user, I want refreshing an expired Claude sign-in to keep Claude Code signed in, so that the widget never logs me out.
5. As the user, I want Claude's model-scoped weekly Windows (for example a Fable-only limit) on hover, so that I still see them.
6. As the user, I want each Claude Row to show its plan (for example Claude Max 5x), so that I can tell Accounts apart.
7. As the user, I want Codex's weekly Window and plan to show as before, so that nothing is lost.
8. As the user, I want Codex Reset tickets with their count and nearest expiry, so that I spend them before they expire.
9. As the user, I want Codex's 5-hour session shown only when Codex reports one, so that I am not shown a fake 0%.
10. As the user, I want Antigravity's Gemini weekly Window as the Row's weekly value, so that it reads like before.
11. As the user, I want Antigravity's Claude/GPT weekly Window on hover, so that I still see it.
12. As the user, I want reading Antigravity usage to spend no model tokens, so that checking usage costs nothing.
13. As the user, I want a Gemini API Row showing requests today and this month, so that I see my pay-as-you-go use next to subscriptions.
14. As the user, I want "today" and "this month" to follow my local calendar, so that the numbers match my day.
15. As the user, I want to add a Gemini API Account by giving its Google Cloud project, so that I can track the project I pay for.
16. As the user, I want "Detect logins" to find the Gemini API project that my `GEMINI_API_KEY` belongs to, so that I do not have to look it up.
17. As the user, I want a Gemini API Row to say what is wrong when the Google Cloud SDK is missing or signed out, so that I know how to fix it.
18. As the user, I want every Account read at the same time, so that a slow Provider does not delay the others.
19. As the user, I want a failing Account to stay stale with its last values and an error, as before, so that one broken endpoint does not blank the widget.
20. As the user, I want an error that names the cause (signed out, expired, endpoint changed, timed out), so that I know whether I need to act.
21. As the user, I want my existing settings file to keep working after the update, so that my Accounts, positions and hidden flags survive.
22. As the user, I want the Accounts view to stop telling me to run a codexbar consent command, so that its hints are current.
23. As the user, I want the README and release notes to stop requiring Win-CodexBar, so that installing is one step.
24. As the user, I want the announced-reset line to keep working, so that nothing else changes.

## Implementation Decisions

- **Readers.** One reader per Provider behind a common shape: given an Account, return the raw facts for one Row or a failure with a short cause. The service runs all Accounts in parallel (the codexbar per-provider serialization goes away), each with the existing timeout, and the existing stale-Row logic stays the single place that turns a failure into a stale Row.
- **Row building.** Each reader's parsing is a pure function from the Provider's own response (plus small sign-in facts such as the plan) to a Row, so it can be tested from fixtures. The codexbar JSON parser is removed.
- **Claude.** Read the Login folder's OAuth credentials; call the OAuth usage endpoint with the beta header. `five_hour` is the session Window, `seven_day` the weekly Window, and every other non-null `seven_day_*` object with a utilization becomes a hover-only Extra window, titled from a small known-name map (Opus, Sonnet, Fable) and otherwise skipped. The plan comes from the credentials' subscription type and rate-limit tier. If the access token has expired or the call returns 401, refresh it through the OAuth token endpoint, then write the new access token, refresh token and expiry back into the same credentials file atomically (write a temp file, then replace), keeping every other field; re-read the file just before refreshing so a token Claude Code already refreshed is used instead. The client id and token endpoint are taken from what Claude Code and codexbar use, verified at implementation.
- **Codex.** Read the Login folder's `auth.json` (access token and account id); call the usage endpoint and the reset-credits endpoint. Windows are mapped by their length (about 5 hours → session, about 7 days → weekly), not by primary/secondary position. Reset tickets are the credits with status `available`; the count is theirs and the expiry is the earliest `expires_at`. The plan comes from `plan_type`. Tokens are not refreshed; an expired one is reported as "sign in to Codex again".
- **Antigravity.** Run the agy CLI found the way detection already finds it, with `-p /usage --output-format json --print-timeout 90s`, from a fresh empty temp folder, launched directly (never through a shell that rewrites `/usage`). The bucket `gemini-weekly` is the weekly Window labelled "Gemini Weekly"; other weekly buckets become Extra windows titled by their group. Used percent is one minus `remaining_fraction`. Process tree kill on timeout or cancel is kept.
- **Gemini API.** A new Provider `gemini-api` whose Account holds a Google Cloud project id (stored as the Account's `GOOGLE_CLOUD_PROJECT` override, alongside the existing Login folder overrides). The access token comes from `gcloud auth print-access-token`. Two Cloud Monitoring time-series reads of `serviceruntime.googleapis.com/api/request_count` for service `generativelanguage.googleapis.com`, summed over local midnight to now and over the local first of the month to now, billed to the same project. The Row has no Windows; it carries API usage instead, and the row view shows "N today · M this month".
- **Row model.** Row gains optional API usage (requests today, requests this month). A Row with API usage and no Windows counts as having data for the stale logic.
- **Accounts.** The Provider catalog gains Gemini API. In the add-Account form the folder field becomes a project field for this Provider. Detection adds a Gemini API Account when `GEMINI_API_KEY` is set and gcloud can look up the key's project.
- **Settings.** The codexbar path setting is dropped; an old settings file that still has it loads normally because unknown fields are ignored.
- **Docs.** ADR 0001 is superseded by ADR 0003 (written). README, AGENTS.md build notes and release notes drop the Win-CodexBar requirement and mention gcloud for the Gemini API Row.

## Testing Decisions

- Tests check what a Row shows for a given Provider response, not how the reader gets there.
- Main seam: each Provider's pure parse function, fed with fixtures captured from the real endpoints on 2026-10-25 (personal data scrubbed). This follows the existing SnapshotBuilder fixture tests.
- Second seam, only for Claude token refresh: the Claude reader with a fake HTTP handler and a temp Login folder, checking that an expired token is refreshed, that the new tokens are written back with other fields kept, and that a token refreshed by someone else in the meantime is used without calling the token endpoint.
- Account catalog and detection tests extend the existing AccountsTests for the Gemini API Provider.
- The existing stale-Row tests stay and keep passing.
- Live check before closing: run the widget on this PC against all real Accounts with Win-CodexBar not on the path.

## Out of Scope

- Claude Reset tickets (no field found in the usage response).
- Gemini API cost in money and token counts (Cloud Monitoring has no cost; token metrics were not observed).
- Refreshing Codex tokens.
- Uninstalling Win-CodexBar from this PC (the user's choice, after the live check).

## Further Notes

- Every endpoint except Cloud Monitoring is undocumented and may change without notice; that is the accepted cost in ADR 0003.
- Cloud Monitoring API was enabled on `jun-personal` (`gen-lang-client-0045347318`) on 2026-10-25 at the user's request.
