# Larger Logo Uploads

**Date:** 2026-10-05
**Status:** Implemented (2026-10-05) on `feature/322-larger-logo-uploads` (#322).
**Driver:** Vicki wants to upload bigger images, mainly **staff directory photos** and **sponsor
logos**, both capped at 10 MB. The same treatment covers the event logo and Dashboard banner
(`/admin/theme`, 2 MB cap) and group logos (`/admin/groups`, 500 KB cap). Those caps were in app
code, not the database (`bytea` holds up to 1 GB). They were kept low because these images load on
every Hub or Dashboard visit. Sponsor logos were also stored and served at full size.

## Decisions

| Topic | Decision |
|---|---|
| Approach | Accept big files, store small ones. Raster uploads are downscaled and re-encoded with ImageSharp (already a dependency, used for team photos) before they're saved. No migration. |
| Upload cap | 20 MB for PNG, JPEG, and WebP (`ImageShrinker.MaxUploadBytes`). Images over 60 megapixels are refused before they're decoded, because decoding uses about 4 bytes per pixel. |
| Event logo | Longest side capped at 1200px (nav at ~42px tall, plus the login and Dashboard hero). Stored as lossless WebP, so flat colours and transparency stay crisp. |
| Dashboard banner | Longest side capped at 1800px. Stored as lossy WebP (quality 85), which keeps transparency. |
| Staff photo | On `/admin/team`, the longest side is capped at 600px (Hub cards are about 250px wide). Stored as lossy WebP. Before, it was a 400px JPEG with no EXIF rotation. |
| Sponsor logo | On `/admin/sponsors`, the longest side is capped at 800px and the stored file is whichever is smaller (`WebpMode.Smallest`): lossless WebP, unless it's more than twice the size of the lossy version, which happens with photos. WebP files are now accepted too. Before, the file was stored as uploaded, up to 10 MB. |
| Group logo | Longest side capped at 400px (shown at 48px, larger on the board projector). Stored as lossless WebP in the existing data URL. **SVGs pass through unchanged** under the old 500 KB cap. |
| Already-small files | If the original fits the size limit, isn't EXIF-rotated, and is no bigger than the re-encode, the original bytes and type are kept. |
| Orientation | EXIF orientation is applied (`AutoOrient`), so phone photos don't display sideways. |
| Unreadable files | They show "Couldn't read that image" instead of crashing the circuit. |
| Threading | Decoding and resizing run in `Task.Run`, so the circuit stays responsive. `/admin/theme` shows "Uploading and resizing…" while that runs, and the theme and sponsor file inputs are disabled until it finishes. |
| Out of scope | Location images keep their 10 MB cap. Images already stored aren't reprocessed. |
