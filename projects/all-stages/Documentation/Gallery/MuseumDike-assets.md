# Dike exhibition: source and modifications

The Dike exhibition is a game-authored interpretation of Justice imagery. It is
not a claim that the imported model is a documented ancient statue of Dike.

Lady Justice by Kenny Maguire Studio, CC BY 4.0:
https://sketchfab.com/3d-models/lady-justice-67b91f4d07ee423b9c5c53a08d8aa454
https://creativecommons.org/licenses/by/4.0/

The project obtained a publicly redistributed, optimised GLB from maybelucas.com,
whose file embeds the original author, source URL and CC BY 4.0 license. The
original creator ZIP was not obtained. Full redistribution URL, SHA256, embedded
credits and part-separation evidence are preserved in ThirdParty/BottleBalanceDikeStatue.
Changes: meshopt decoding with its official decoder; original-coordinate static
body separated from sword and scale; mirrored Z and winding for Unity; sword
handle/blade normalised into one Y-axis reward with two materials; scale parts
re-rigged for level pans; project-authored stone/brass/steel materials. Source
has no skin or animation. No hand-opening animation is claimed to be supplied.
The original whole model has 400,351 triangles; the displayed game sculpture
uses its corresponding separated portions, without the original duplicate sword.

The original statue sword was independently released in both Unity Play Mode
and a native Windows player. The user rejected its close-up appearance. The
current visual replacement is prepared separately as ConceptSword so rerunning
the statue importer cannot restore that rejected visual.

Realistic Medieval Long Sword / Brescia Spadona by Izkeas, CC0 1.0:
https://izkeas.itch.io/realistic-medieval-long-sword
https://creativecommons.org/publicdomain/zero/1.0/
The creator's official free FBX and Blender sources were acquired without an
account or purchase. Source bytes and hashes are preserved in
ThirdParty/BottleBalanceDikeSword. The project's ConceptSword is a derivative
that matches the approved concept's silver straight blade, gold ceremonial
crossguard and pommel, and dark grip. Added ornaments and material changes are
project-authored; they are not claimed to be included in the original download.
The replacement stays a separate rigid object and uses the verified drop,
pickup, insertion, and door-unlock logic. Final replacement physics validation
is recorded separately from the earlier statue-sword separation proof.

Book Encyclopedia Set 01 by John Malcolm / Poly Haven, CC0:
https://polyhaven.com/a/book_encyclopedia_set_01
The source is an encyclopedia, not a historical law code. One closed book was
isolated with its original UVs and PBR textures, laid flat, and adapted with
project-authored LEX / law-book cover lettering.

Rock 09 by Jenelle van Heerden / Poly Haven, CC0:
https://polyhaven.com/a/rock_09
This scanned natural stone is the sword-receiver base. The brass opening,
internal receiver, interaction and door linkage are project-authored additions;
the source rock contains no prebuilt sword socket or animation.

Inscription chosen by the user:
저울 없는 검은 폭력이며, 검 없는 저울은 법의 무력함이다
It conveys a passage in Rudolf von Jhering's The Struggle for Law, chapter I;
it is not attributed as speech by Dike.
https://en.wikisource.org/wiki/The_Struggle_for_Law/Chapter_I

The existing Nike -> inserted balance room -> portrait-room route, original
saved scene/lighting and selected museum audio are preserved. No new sword,
glow, stone or balance sound clips are applied in this change.

Interaction refinement after user play, 2026-11-14:
The existing sword and stone visuals remain pending the user's next asset
selection. The fixed left-pan sword is enlarged from 0.52m to 0.78m with uniform
proportions and a support height calculated from its actual rotated bounds.
Insertion now takes 1.50 gameplay seconds: 0.33s to align the tip 0.10m above the
socket, then 1.17s of vertical travel with smooth seating. At complete seating,
one 1.10s camera pulse uses 75% of the existing final-hand rumble signal. The
controller, input angles and colliders remain fixed by the camera pulse. The
door waits 0.40s after seating and opens over 3.0s. Active gameplay time drives
insertion, pulse and door; pause freezes them and reset clears the camera pulse.
No new audio is applied. Candidate previews and licenses are research material,
not a claim that the unselected models are included in this player.


P02 Versailles pedestal and exit-wall arrangement (2026-11-14)

The user-selected Treleau Pedestal is now acquired and applied under CC BY 4.0. See DikePedestalP02-CREDITS.txt beside the player and Assets/Parallax/Gallery/ThirdParty/DikePedestalP02/source-manifest.json. The separate ground scan is excluded. Original body topology/UVs/normals are retained except the custom open sword socket. Body is uniformly normalized to 1.20m, original 8K textures are prepared at 4K, with original specular RGB plus inverse roughness in alpha.
Layout in the 22x17m inserted room: bottle (-6,0,42.5), Dike/scales (6,0,42.2), central receiver (0,0,37.5). The chosen inscription is above the exit, clear of the relocated statue. Actual native physics verifies the cap opening and adjacent solid scan, sword insertion, and a real controller route around the centre and through the opened exit. G01 and all prior selected sounds are retained.
