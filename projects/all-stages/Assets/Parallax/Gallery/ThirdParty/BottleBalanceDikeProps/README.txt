Dike props — acquired CC0 source assets

Book: Poly Haven Book Encyclopedia Set 01 — John Malcolm
https://polyhaven.com/a/book_encyclopedia_set_01
Rock: Poly Haven Rock 09 — Jenelle van Heerden
https://polyhaven.com/a/rock_09
License for 3D source assets: CC0 1.0, https://polyhaven.com/license

SourceAssets preserves the officially downloaded FBX/glTF/binary/textures and
derived OBJ/MTL geometry. The source is an encyclopedia, not a historical law
book. The game may add its law-book title/ornament separately. The rock has no
sword slot; runtime insertion geometry is a separate project-authored part.

Run ShadeLink.Editor.GalleryDikePropAssets.Prepare() from the Unity editor.
It does not edit scenes or add colliders. It creates visual-only prefabs at:
  Resources/BottleBalance/Dike/LawBook.prefab
  Resources/BottleBalance/Dike/SwordStone.prefab

LawBook uses source book 14 only, normalized to largest dimension 1.
Its cover plane is X/Z, its thin direction is Y, and front cover faces +Y.
Bounds size approximately X=0.686815, Y=0.137454, Z=1, centered at origin.
SwordStone uses source Rock09 representative highest LOD, same uniform
normalization, with all vertices shifted so minimum Y is exactly zero.
Bounds size approximately X=0.511735, Y=0.227277, Z=1.
Prepare() additionally mirrors source positions and normals on Z to pair
the triangle reversal with the right-handed to left-handed conversion.

Standard materials retain separate book cover/paper slots and original UVs.
Source OpenGL normal maps use NormalMap import, no green-channel flip.
Metallic and roughness are packed into Standard R/Alpha, respectively:
  output.R = source metallic (paper/rock = 0)
  output.A = 1 - source roughness
Source AO (explicit rock map or ARM red) is copied to grayscale because the
Standard shader samples occlusion from G. No procedural AO is invented.

acquisition-manifest.json and extra-pbr-map-records.json record official
source URLs, MD5 matches, SHA256 hashes, extraction and normalization.
prepared-prop-report.json is written only after Prepare() is actually run.
Official preview pictures are comparison references, not ingame textures.

Shared helper: GalleryDikePropAssets.ReadObj(path[, meshName, baseOnFloor,
reverseWinding]) returns an unsaved mesh. Supplied OBJ vertex normals are
preserved. Missing normals are recalculated; missing UVs are accepted.
Meshes above 65,535 vertices use UInt32. Default reverseWinding=true changes
OBJ CCW triangles into Unity CW triangles, with an explicit false override.
The shared ReadObj helper intentionally leaves positions/normals in source
coordinates; callers importing right-handed source data must also mirror
vertex Z and normal Z (as this file's prop Prepare() does internally).
