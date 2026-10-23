# Reusable Sponsors

**Date:** 2026-10-23
**Issue:** #341
**Status:** Implemented on `claude/guest-attendance-extend-djwotf`.

## Problem

Each `Sponsor` row belonged to one event (`Sponsor.EventId`), and every sponsor page (`/admin/sponsors`,
`/hub/sponsors`, the Dashboard widget) showed only the active event's rows. When Camp Harvest became
the active event, all three were empty even though CCN 2026's sponsors were in the database. The only way
to reuse a sponsor was the "Also copy: Sponsors" box when *creating* an event, and that made duplicate
rows, so a logo fix had to be repeated on every copy.

## Design Decisions

| Topic | Decision |
|---|---|
| Model | Same as the team (#311). `Sponsor` is global: name, logo, logo URL, website, contact, phone. A new `EventSponsor` join (`EventId`, `SponsorId`, `SortOrder`; key on both ids) holds which sponsors an event shows and in what order. |
| Edits | One record per sponsor. Editing the logo or contact details changes it on every event it's on. The form says so and names how many events that is. |
| Existing data | The migration links each sponsor to the event it belonged to, keeping its order, so CCN looks the same. |
| Duplicates | Sponsors whose names match, ignoring case and surrounding spaces, are merged into one. The keeper is the row with an uploaded logo, preferring the most recent event. Contact, website and logo URL fields it's missing are filled from the others. If two merged rows were on the same event, that event keeps one link (the lower sort order). |
| `/admin/sponsors` | Lists this event's sponsors (drag or ↑/↓ to order, saved per event). **+ Add from past events** opens a picker: search, see which events each sponsor was on, **Add** one at a time. The form adds a **new** sponsor and puts it on this event, and warns if the name already exists. **Remove** takes a sponsor off this event only. |
| Deleting a sponsor | Only from the picker, and only for sponsors that aren't on any event, so removing it from an event can never delete history. |
| Event creation | "Also copy: Sponsors" now copies the links (same sponsors, same order), not the rows. |
| Logo endpoint | Unchanged: `/sponsors/logo/{sponsorId}`. |
| Tiers | Not now. They'd be a column on `EventSponsor` later. |

## Out of scope

- Per-event sponsor tiers or per-event contact overrides.
- A separate library page. The picker covers finding and cleaning up sponsors.

## Testing

- **Migration:** on a copy of CCN's data, plus one duplicate created through "Copy sponsors". CCN's list and
  order are unchanged, the duplicate is merged into one sponsor linked to both events, and no shadow columns appear.
- **Admin:** with Camp Harvest active the page is empty. Add two sponsors from past events, add a new one,
  reorder, and remove one. The Hub page and Dashboard show the same list and order. CCN is unaffected.
- **Shared edits:** edit a logo on Harvest and confirm CCN shows the new logo.
- **Delete:** only possible from the picker, for a sponsor on no event.
