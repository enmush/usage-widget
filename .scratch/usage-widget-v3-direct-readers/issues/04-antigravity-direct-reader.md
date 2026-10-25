# 04: Antigravity Row read from agy's usage report

**What to build:** The Antigravity Row comes from agy's built-in `/usage` JSON report (no model tokens), run directly from an empty temp folder.

**Blocked by:** 01

**Status:** done

- [x] Gemini weekly bucket is the weekly Window labelled "Gemini Weekly"; other weekly buckets are hover windows titled by group
- [x] Used percent is one minus the remaining fraction
- [x] Missing agy, timeout or a non-success report gives a stale Row with a cause; the process tree is killed on timeout or exit
- [x] Parsing tested from a captured fixture
