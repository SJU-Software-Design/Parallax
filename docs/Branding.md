# Parallax puzzle-P branding

The shared mark is the approved angled ivory puzzle piece with a recessed P. It has no Parallax wordmark and uses a square canvas with actual transparency. The upright front-facing alternative is not used.

- Source: project-created artwork using the built-in imagegen tool.
- File: projects/all-stages/Assets/Parallax/Shared/Branding/ParallaxIcon.png
- Original PNG: 1254 × 1254 RGBA.
- SHA-256: 048954564958005245ed6589705e7b7199370c8f8d6c085aad25aefb45329155.
- Usage: repository README cover and Unity Default/Standalone application icons.
- Import: no NPOT rescale, no compression, no mipmaps, preserve input alpha and sRGB.
- Artwork bytes are preserved. Platform icon sizes are produced by Unity during the Windows build.

The icon settings are stored in ProjectSettings.asset. Existing build scripts copy this file and do not override application icons, so a new Windows build picks up the same mark. Already-built executables retain their previous embedded icon until rebuilt.

This change is based on main and is independent of the unmerged gameplay integration PR51. It does not change gameplay, saves, menus or audio.

GitHub Social preview is a separate repository setting; merging this PR changes the README cover but does not upload a Social preview image. GitHub accepts transparent PNG; the image must be under 1 MB and 1280 × 640 is the recommended display size. The source icon remains an unmodified square PNG. See [GitHub's official instructions](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/customizing-your-repositorys-social-media-preview).