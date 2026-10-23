# Sponsor Ads

**Date:** 2026-10-23
**Issue:** #350
**Status:** Implemented on `claude/guest-attendance-extend-djwotf`.

## Problem

Sponsors give HBDA high-res JPEG ads. The app only has sponsor logos (`/hub/sponsors`), so there's
nowhere to show an ad that, when tapped, opens the sponsor's page. Sponsors mostly stay the same
from event to event, but their ads can change, and the same ad should be reusable at a later event
without being uploaded again.

## Design Decisions

| Topic | Decision |
|---|---|
| Model | Same pattern as reusable sponsors (#341). `SponsorAd` is global: title (alt text and admin label), image, **optional** `SponsorId`, optional `LinkUrl`. `EventAd` (`EventId`, `SponsorAdId`, `SortOrder`, `ClickCount`; key on both ids) is which ads an event shows, in what order. |
| Sponsor link | Optional. An ad linked to a sponsor is labeled with it, and its click-through defaults to the sponsor's website. A standalone ad (an HBDA promo, a "save the date") has no sponsor. Deleting a sponsor (only possible when it's on no event) leaves its ads standalone. |
| Click target | The ad's `LinkUrl`, else its sponsor's website, else not clickable. |
| Where ads show | `/hub/sponsors` only: a full-width section above the logo grid, in the event's order. |
| Image shape | Shown at its own proportions, full width (up to 720px), never cropped. |
| Image storage | Uploads up to 20 MB (the existing limit). Stored resized to 1600px on the long side with `ImageShrinker` (`Smallest`: lossless when that keeps text sharp at a sensible size, otherwise WebP quality 85). Served from `/sponsors/ad/{id}?v=…` with a long cache, and the version changes when the image is replaced. |
| Click counts | `/ads/{eventId}/{adId}` adds 1 to that event's `EventAd.ClickCount` and redirects to the ad's target. The target always comes from the database, so this can't be used as an open redirect. No personal data is stored. Counts show on the admin Ads tab. |
| Admin | `/admin/sponsors` gets **Sponsors** and **Ads** tabs. The Ads tab lists this event's ads (thumbnail, title, sponsor, where it links, clicks here, order, Edit, Remove). **+ Add from past events** opens a picker that shows which events each ad was on, with Delete for ads on no event. **+ New ad** opens a dialog: title, image, sponsor (or none), link. Editing says how many events the ad is on, since changes apply to all of them. |
| Event creation | "Also copy: Sponsors" becomes "Sponsors and ads". It copies the sponsor links and the ad links, in the same order, with click counts starting at 0. |

## Out of scope

- Ads on the Dashboard or in the schedule (the Dashboard carousel was offered and not chosen).
- Scheduling ads by date, per-ad expiry, or impression counts.
- Cropping or editing images in the app.

## Testing

- Upload a large JPEG: it's stored at no more than 1600px and shows on `/hub/sponsors` at its own proportions.
- An ad linked to a sponsor with no link URL opens the sponsor's website. An override URL wins over it. An ad with neither isn't clickable.
- Tapping an ad (as a guest) adds 1 to that event's count, and the admin tab shows it. Another event's count is unaffected.
- Add from past events: an ad on CCN can be added to Camp Harvest. Removing it from Harvest keeps it on CCN. Delete only shows for ads on no event.
- Creating an event with "Sponsors and ads" copies both lists in the same order.
- The migration adds the two tables only. No shadow columns.
