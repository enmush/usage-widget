# Usage Widget

A small always-on-top Windows window that shows the remaining allowance of every AI subscription account at once.

## Language

**Provider**:
An AI service whose subscription limits are tracked, such as Claude, Codex or Antigravity.
_Avoid_: Service, vendor, model

**Account**:
One signed-in login at a provider; a provider can have several (for example Claude 1 and Claude 2).
_Avoid_: Profile, user, token account

**Window**:
A rolling quota period with a used percentage and a reset time, such as the 5-hour session or the weekly limit.
_Avoid_: Limit, bucket, quota period

**Reset**:
The moment a window's usage returns to zero, either on its regular schedule or by an announced global reset.
_Avoid_: Refresh, renewal

**Reset ticket**:
A saved reset an account can spend on demand, with an optional expiry.
_Avoid_: Reset credit, banked reset, saved reset

**Announced reset**:
A provider-wide reset that the provider has announced ahead of time for a specific moment.
_Avoid_: Scheduled reset, event reset

**Row**:
The single line in the widget that summarises one account.
_Avoid_: Card, tile, entry
