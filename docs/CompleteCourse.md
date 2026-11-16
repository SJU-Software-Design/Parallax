# Complete course: Tutorial → Museum → Beach → pirate-ship ending

Version **0.29.2-complete-harbor**, Windows x64 / Unity **6000.3.24f1**, Built-in render pipeline / Mono.

## Play without Unity

Download **Parallax-CompleteHarbor-Windows-x64.zip** from the [PR preview release](https://github.com/SJU-Software-Design/Parallax/releases/tag/complete-harbor-preview-20261117).
Extract the whole ZIP into a new directory and start **Parallax.exe**. Keep all data/runtime and attribution files beside it. No personal build path or Unity installation is required. Windows x64 is the verified platform; a macOS/Linux executable is not supplied.

The normal entry point is Tutorial lesson 1, without review flags or puzzle-state injection. Tutorial ends with the 1F→B1 elevator. The museum route is Nike, bottle/Justice scale/book/sword/P02 pedestal, portrait/curtain and the hand portal. The beach route is sunlight receivers, water/shade traversal, shadow-assembled boat, then the final harbor. Enlarge the natural rock using perspective and drop it on the counterweight platform. Actual settled weight releases the latch and lifts the fully submerged covered bridge. Cross its real canopy shade, board the pirate ship, press E, and watch departure/flight/fade/END. HB02 Wanderlust replaces puzzle BGM on departure; selected rock/pulley/water/bridge/drips/ship sounds are included.

WASD move, mouse look, Space jump, left click grab/drop, right click + mouse rotate, Backspace cancel. Esc opens pause and three-map travel. R resets the current Beach section; Shift+R restarts Beach. Pausing/resetting also controls the finale, sound and drips.

## Build a downloaded source checkout

Install **Unity 6000.3.24f1 + Windows Build Support (Mono)**. From the repository root, double-click `run-complete.cmd`, or run:

```powershell
.\scripts\run-complete.ps1
.\scripts\run-complete.ps1 -BuildOnly
.\scripts\run-complete.ps1 -Rebuild -UnityEditor 'X:\Unity\6000.3.24f1\Editor\Unity.exe' -WorkspaceRoot 'X:\ParallaxBuilds'
```

The default is build-if-needed then launch. `-BuildOnly` leaves the successful player ready without opening it. The `.cmd` wrapper retains errors on screen. Editor lookup uses `-UnityEditor`, `UNITY_EDITOR_PATH`, standard Unity Hub paths and the user's Codex App Unity cache. It does not install Unity automatically.

Workspace priority: `-WorkspaceRoot`, `PARALLAX_BUILD_ROOT`, ignored `Builds/build-workspace.json` (`{"workspaceRoot":"X:\\ParallaxBuilds"}`), then ignored repository `Builds`. No machine-specific workspace is committed. Output: `Latest/Tutorial/Parallax.exe`; logs: `Logs/Complete`. The launcher prepares the **copied** project in `StageProject`, preserving source scenes/settings. It regenerates Beach, Tutorial and Gallery, the actual beach portal preview and readable museum insertion meshes. Do not use a legacy stage-only launcher for the integrated build.

The build verifies all 15 ending clips against recorded SHA256, Resources keys, length/channels and streaming settings. Required audio/model/art/font notices are copied beside the player. Source fingerprinting includes Assets, Packages, ProjectSettings, Documentation and the launcher before/after building. A complete successful Candidate is promoted to Latest, keeping the prior successful player in Previous. Failed candidates do not launch or replace Latest. Source-overlap and junction/symlink output paths are rejected. Only another editor locking this generated project blocks preparation; unrelated projects may remain open.

## Reproducible native validation

The committed validation summary describes the newly built PR player and its exact scope. The previous local 0.26.10/0.29.1 reports are historical, not evidence for this new binary. Gameplay suites use declared fixtures and do not claim an unassisted, continuous human solve or subjective sound-mix approval.

Use one validation mode per process, with a fresh absolute output folder and a separate `-logFile`:

```text
-stage-travel-validate -validation-output <folder>
-gallery-natural-progression-test -gallery-bottle-balance -validation-output <folder>
-beach-audio-validate -validation-output <folder>
-beach-boat-pickup-validate -validation-no-capture -validation-output <folder>
-validate-beach-counterweight -counterweight-audio-only -counterweight-validation-output <folder>
-tutorial-elevator-review -tutorial-elevator-validate -tutorial-route-beach-validate -validation-no-capture -validation-output <results.json>
```

`-validation-no-capture` skips only capture/visual checks and explicitly records skips. Counterweight audio-only preserves real physics, bridge traversal, ship END, audio, pause/reset and drip logic, while reporting the six visual checks as skipped. Neither mode captures the desktop or writes screenshots. Default visual validation modes remain available when explicitly requested. Batch-mode cursor behavior and each fixture are documented in individual reports.

## Attribution

The release includes existing Tutorial/Museum/Beach credits plus pirate-ship, source-record, ending-audio selection/credits and controls documents. HB02 is the full **Wanderlust / Scott Buckley / CC BY 4.0** source, streamed with a 24-second playback offset; the build does not loop a short audition extract. New selected ending effects retain CC0 records and processing lineage.

TEAM11/TEAM12 are team-supplied sounds whose original creators/source licenses remain unconfirmed; they are not labeled CC0. Existing Asset Store EULA, CC BY, OFL and artwork rights notices are retained. This PR does not change those rights claims. Only selected game assets are included, with no audio-review catalog/download archives, Unity caches or private machine paths. The separate UI-prototype PR is outside this integration.