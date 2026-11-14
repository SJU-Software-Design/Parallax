Lady Justice - acquired public CC BY 4.0 redistribution

Original creator: Kenny Maguire Studio
Original model: https://sketchfab.com/3d-models/lady-justice-67b91f4d07ee423b9c5c53a08d8aa454
License: Creative Commons Attribution 4.0
License URL: https://creativecommons.org/licenses/by/4.0/
Public redistribution served by: https://maybelucas.com/models/lady-justice/scene.glb
Public credit page: https://maybelucas.com/

This is not the original creator's downloadable ZIP. It is the publicly served,
web-optimized GLB credited to Kenny Maguire Studio. The GLB asset.extras itself
contains the original creator, title, CC-BY-4.0 license and original Sketchfab URL.
The observed public page script references this exact GLB URL; no protected
Sketchfab viewer file was extracted or decrypted and no login was bypassed.

The public GLB has 400,351 triangles, 58 nodes, 54 independent mesh primitives,
no skin and no animation. The official original model listing says 3,960,095
triangles; do not confuse the original listing with this public optimized copy.

Source nodes 40 and 41 are the sword hilt/guard and blade. Actual geometry was
highlighted and removed in inspection renders; the whole sword disappeared while
the hand and statue body remained. The blade is intact. This verifies independent
rigid geometry, not an included hand-opening rig or ready-made drop animation.
Game physics/drop and insertion are separately implemented and validated.

SourceGeometry/*.obj.txt stores credited OBJ geometry as text to avoid native OBJ
auto-import duplicating the mesh. GalleryDikeStatueAssets.Prepare() parses these
files with the shared GalleryDikePropAssets.ReadObj helper and writes Mesh assets
and visual prefabs. Source coordinates are retained except RewardSword, which is
centered, Y-long-axis and unit length. Rig.json contains the inverse normalization
and handedness conversion needed to place it precisely in the original hand.

Coordinate conversion: glTF right-handed Y-up to Unity left-handed Y-up by
mirroring vertex and normal Z. Source triangle winding is reversed by the shared
OBJ reader. Supplied vertex normals are preserved; no new UV or texture is claimed.

StaticBody omits the sword and all scale geometry, preserving the hands.
Scale geometry is author-modified into fixed suspension, rotating beam, and left/
right chain and pan groups. The pan bowls and chains are split so the game can
extend chains to a comfortable placement height without stretching bowl geometry.
This is a project-authored geometric partition, not the original model's rig.

RewardSword has two submeshes named SwordHilt and SwordBlade. Game materials are
new cream stone, antique brass and steel using the existing MuseumSurface shader.
No original PBR texture or release animation is implied.

Required credits and modifications are in CREDITS.txt. Exact source hashes,
normalization and per-part bounds are in game-parts-manifest.json. The full public
GLB and original official metadata are preserved here for provenance.
