# Larger Logo Uploads

**Date:** 2026-10-05
**Status:** Implemented (2026-10-05) on `feature/322-larger-logo-uploads` (#322).
**Driver:** Vicki wants to upload bigger logo files. The event logo and Dashboard banner on
`/admin/theme` were capped at 2 MB, and group logos on `/admin/groups` at 500 KB. Those caps were in
app code, not the database (`bytea` holds up to 1 GB). They were low because the event logo loads
on every page and group logos are inlined as data URLs.

## Decisions

| Topic | Decision |
|---|---|
| Approach | Accept big files, store small ones. Raster uploads are downscaled and re-encoded with ImageSharp (already a dependency, used for team photos) before they're saved. No migration. |
| Upload cap | 20 MB for PNG, JPEG, and WebP (`ImageShrinker.MaxUploadBytes`). Images over 60 megapixels are refused before they're decoded, because decoding uses about 4 bytes per pixel. |
| Event logo | Longest side capped at 1200px (nav at ~42px tall, plus the login and Dashboard hero). Stored as lossless WebP, so flat colours and transparency stay crisp. |
| Dashboard banner | Longest side capped at 1800px. Stored as lossy WebP (quality 85), which keeps transparency. |
| Group logo | Longest side capped at 400px (shown at 48px, larger on the board projector). Stored as lossless WebP in the existing data URL. **SVGs pass through unchanged** under the old 500 KB cap. |
| Already-small files | If the original fits the size limit, isn't EXIF-rotated, and is no bigger than the re-encode, the original bytes and type are kept. |
| Orientation | EXIF orientation is applied (`AutoOrient`), so phone photos don't display sideways. |
| Unreadable files | They show "Couldn't read that image" instead of crashing the circuit. |
| Threading | Decoding and resizing run in `Task.Run`, so the circuit stays responsive. `/admin/theme` shows "Uploading and resizing…" and disables the file inputs while that runs. |
| Out of scope | Sponsor logos, location images, and team photos keep their current 10 MB caps. Existing stored images aren't reprocessed. |
