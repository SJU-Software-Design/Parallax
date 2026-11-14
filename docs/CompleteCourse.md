# Tutorial → Museum → Beach complete course

The integrated player starts at Tutorial lesson 1. Finish the tutorial, board the elevator, descend from 1F to B1, and enter the museum. The museum route includes Nike, the inserted bottle/Justice-scale exhibit, the portrait curtain, and the final hand portal to the beach.

Press **Esc** in Tutorial, Museum, or Beach for **일시정지 / 맵 이동**. The travel tab offers exactly Tutorial, Museum, and Beach. Choosing a map starts a fresh first room/checkpoint of that map, including a fresh restart of the current map. Normal room demos and pause controls remain in the pause tab.

## Windows source build and launch

Install **Unity 6000.3.24f1** with Windows Build Support. From the repository root:

    .\scripts\run-complete.ps1
    .\scripts\run-complete.ps1 -BuildOnly
    .\scripts\run-complete.ps1 -Run -Rebuild
    .\scripts\run-complete.ps1 -UnityEditor 'X:\Unity\6000.3.24f1\Editor\Unity.exe' -WorkspaceRoot 'X:\ParallaxBuilds'

The root run-complete.cmd is the double-click entry and accepts the same arguments. Default behavior is build-if-needed then run; -BuildOnly never starts the game. -Rebuild bypasses the content-fingerprint cache.

Unity lookup uses -UnityEditor, then UNITY_EDITOR_PATH, then the usual Unity Hub locations. Generated workspace lookup uses this priority:

1. -WorkspaceRoot.
2. PARALLAX_BUILD_ROOT.
3. Ignored Builds/build-workspace.json, with a JSON workspaceRoot string.
4. Ignored repository Builds.

A private example configuration is {"workspaceRoot":"X:\\ParallaxBuilds"}. Do not commit a personal machine path. Relative configured paths are resolved against the repository root.

The launcher copies **Assets, Packages, ProjectSettings, and Documentation** from projects/all-stages to the disposable workspace StageProject. Unity's integrated build method is ShadeLink.Editor.TutorialElevatorBuild.BuildWindows, with exactly Tutorial/Gallery/Beach in the player. It prepares generated copies, leaving canonical source scenes and settings unchanged. Documentation is copied so typography/font license and material/art credits can be packaged beside the player.

The script checks a source SHA256 fingerprint, including itself and Documentation, before and after copying/building. A matching successful Latest/Tutorial player is reused. Source edits require a fresh build; checking the fingerprint can take time for large assets.

Build output first goes to Candidate/Tutorial. Only a successful Unity exit, Succeeded/zero-error report, complete player files, and unchanged source fingerprint allow promotion to Latest/Tutorial. The former Latest becomes Previous/Tutorial; only one previous successful build is retained. Failure does not launch the candidate or replace Latest. Logs are in Logs/Complete, and child Unity temporary files use workspace Temp/Complete. Close running players that would be replaced, and finish/close Unity editors before building.

Mirrors and cleanup are constrained to verified generated subdirectories; junction/symlink paths are rejected. The original source is never a mirror deletion target. The existing scripts/run-stage.ps1 keeps its interface and stage behavior, with the same compatible Documentation mirror/fingerprint correction.

## Verification scope

The launcher performs import/build/result/file/source-integrity checks. It does **not** automatically run the focused native gameplay validation suites or claim a continuous manual playthrough. Those checks are separate from building.

The portable launcher was separately exercised in Windows PowerShell 5.1 using a minimal isolated source fixture and a small .NET fake builder: eighteen orchestration/safety checks passed. They cover successful promotion, cache reuse, forced rebuild, prior-build retention, failing exits/invalid or missing reports, Documentation mirroring, unchanged fixture source, and source-overlap/junction rejection. This mock test did not run Unity, render the game, start a player, or replace the real Latest output. It is separate from the native gameplay evidence below.

The completed local 0.26.10 integration was verified with the **same Windows binary** for Museum (59), travel (131), Tutorial route (161), Beach audio (63), and boat pickup (20): **434 focused native checks passed**, plus nineteen actual museum camera renders, twelve bronze catalogue plaques/thirty-six text labels, and all 317 scenery collision surfaces. The tested Assembly-CSharp SHA256 is 7367FEBC23B9A752305226DE99DD69C5406D0695DAF1393511A2CFD9B527C539.

This PR checkout additionally preserves the latest main-branch null/inactive-prop guard and includes whitespace, build-only credits packaging, and portable launcher/configuration changes. Those changes are not represented as a fresh Unity-built player: a new PR-source build remains unverified because the Unity Editor was unavailable. The eighteen mock launcher checks above do not replace that build. Native checks use declared fixtures; they are not an unassisted solve or subjective sound-mix approval. Build and check a new checkout/platform again.

Useful native flags for the integrated Tutorial-entry player:

    -stage-travel-validate -validation-output <absolute output folder>
    -gallery-natural-progression-test -validation-output <absolute output folder>
    -gallery-concept-capture -captureDir <absolute output folder>
    -beach-audio-validate -validation-output <absolute output folder>
    -beach-boat-pickup-validate -validation-output <absolute output folder>

Use one validation mode per process. Camera capture enters Gallery through the public travel coordinator and adapts rear-room coordinates for the inserted Dike gallery.

## Sound and attribution records

The tutorial uses TB01's authorized 0:00–2:59 edit; Beach BB01 uses the full track. Museum C01, the chosen elevator clips, Dike effects, and selected Beach effects retain their own recorded credits and edits. See projects/all-stages/Documentation/Gallery, the Tutorial/Beach resource manifests and third-party credits packaged beside the player.

**TEAM11** (team-supplied medium water impact, enter1) and **TEAM12** (team-supplied large sand impact, break3) have unconfirmed original creators/source licenses. The selected recordings came from the team; they are not labeled CC0 or commercially licensed by this PR. The break3 filename suggests a small impact while the team proposed it for the large-sand event; the original is not presented as a literal giant-die or large-object recording.

The latest upstream key-shadow preview classes and shaders remain in the repository. Their preview scene is outside the three integrated build scenes; a fresh PR build must still compile these preserved sources.

The common museum credit exporter was separately compiled and executed: all 17 required source documents produced 18 output files with matching SHA256 content, including audio/Dike/P02/book/sword attribution. Missing required input fails before output is written. This packaging check is separate from a Unity build.
