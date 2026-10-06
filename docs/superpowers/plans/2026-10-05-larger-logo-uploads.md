# Larger Logo Uploads: Plan

Spec: `docs/superpowers/specs/2026-10-05-larger-logo-uploads-design.md` (#322)

1. `Services/ImageShrinker.cs`: a static `Shrink(data, contentType, maxSide, encoder)` returning `ShrunkImage?`, plus `Lossless` and `Lossy` WebP encoders and the `MaxUploadBytes` and megapixel guards.
2. `ThemeAdminService`: raise `MaxImageBytes` to 20 MB, add `LogoMaxSide` and `BannerMaxSide`, shrink in `SetLogoAsync`/`SetBannerAsync`, and add `ImageUploadResult.Unreadable`.
3. `ThemeEditor.razor`: run the upload through `Task.Run`, add the `_uploading` state and hint, set `SetUploadError` messages, and update the hint text.
4. `Groups.razor`: shrink rasters to 400px lossless WebP, keep the SVG pass-through at 500 KB, and update the hint text.
5. Verify: `dotnet build` (3 baseline warnings), then upload a >2 MB PNG on `/admin/theme` and a >500 KB PNG on `/admin/groups` in the running app.
