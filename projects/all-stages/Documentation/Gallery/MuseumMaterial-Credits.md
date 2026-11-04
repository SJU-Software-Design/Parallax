# Museum material texture credits

These three real PBR texture sets are downloaded source assets for the new museum's stone, plaster and timber finishes. They are not AI-generated concept images. The original image files have not been cropped, recolored, resized or repainted.

Poly Haven's [asset license](https://polyhaven.com/license) identifies its texture assets as **CC0 1.0** and permits their use and redistribution. Source pages, authors and original download URLs are retained here and in `Assets/Parallax/Gallery/Resources/MuseumMaterials/sources.json` for traceability. Source metadata and the license were checked on **2026-11-04**.

## Stone: Marble 01

- Source: [Marble 01 — Poly Haven](https://polyhaven.com/a/marble_01)
- Author: **Rob Tuytel**
- Source appearance: clean cream-beige indoor marble slabs with subtle veining.
- Recorded source width: **1.5 metres**.
- Unity Resources keys: `MuseumMaterials/StoneAlbedo`, `MuseumMaterials/StoneNormal`, `MuseumMaterials/StoneRoughness`.
- Downloaded maps: 1K JPG diffuse, 1K PNG OpenGL normal, 1K JPG roughness.
- The diffuse texture already contains slab joints; combining it with an additional procedural tile grid may create two conflicting joint patterns.

## Plaster: Beige Wall 001

- Source: [Beige Wall 001 — Poly Haven](https://polyhaven.com/a/beige_wall_001)
- Authors: **Dimitrios Savva** (photography), **Rico Cilliers** (processing).
- Source appearance: smooth warm beige painted plaster, with fine grain and small surface variations.
- Recorded source height: **3 metres**.
- Unity Resources keys: `MuseumMaterials/PlasterAlbedo`, `MuseumMaterials/PlasterNormal`, `MuseumMaterials/PlasterRoughness`.
- Downloaded maps: 1K JPG diffuse, 1K PNG OpenGL normal, 1K JPG roughness.

## Timber: Mocha Oak Veneer

- Source: [Mocha Oak Veneer — Poly Haven](https://polyhaven.com/a/mocha_oak_veneer)
- Author: **Jenelle van Heerden**.
- Source appearance: subtle horizontal oak grain and a smooth satin finish for furniture and interior trim.
- Recorded source width: **1 metre**.
- Unity Resources keys: `MuseumMaterials/WoodAlbedo`, `MuseumMaterials/WoodNormal`, `MuseumMaterials/WoodRoughness`.
- Downloaded maps: 1K JPG diffuse, 1K PNG OpenGL normal, 1K JPG roughness.

## Import and validation

All nine images were opened successfully and measured at **1024 × 1024 pixels**. Their downloaded MD5 values match the file metadata returned by the official [Poly Haven files API](https://api.polyhaven.com/). Local SHA-256 values, map roles, source URLs and file sizes are recorded in `sources.json`. Total source texture payload is **12,623,709 bytes (12.04 MiB)**.

- Albedo textures use **sRGB** color sampling.
- Roughness textures use **linear** sampling; higher values mean a rougher surface.
- Normals use **linear**, uncompressed, ordinary RGB `Texture2D` imports (`textureType: 0`), retaining the source **OpenGL** normal convention.
- The custom shader should decode normal RGB as `sample.xyz * 2 - 1`, then transform from the chosen surface/triplanar basis. These files are deliberately not Unity-repacked normal maps; do not assume DXT5nm or call `UnpackNormal` for that format.
- Texture wrapping repeats, mipmaps are enabled and anisotropic filtering is set to 8.

These material images describe surface appearance. The museum's gameplay light and safe-shadow rules are defined separately by its gameplay implementation.
