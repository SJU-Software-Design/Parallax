# Superliminal 기반 조작감 개선 Implementation Plan

> **For agentic workers:** Use task-scoped workers for independent files; the main agent integrates and verifies both Unity projects. Preserve all pre-existing working changes. Do not commit or publish unrelated work.

**Goal:** Apply the supplied 2026-10-31 Superliminal analysis to movement, camera, pickup, perspective placement, release and feedback across Gallery standalone and the four current stages.

**Architecture:** Keep each map's puzzles, support, water, shadows and completion conditions. Share a tested first-person motor/input profile and improve the existing shared interaction and projection solver. Mirror common source files into the standalone Gallery project, which has no external package dependency.

**Tech Stack:** Unity 6000.3.24f1, C#, Built-in renderer, legacy Input, existing batch Editor test harnesses.

**Spec:** User attachment `/Users/seob/.codex/attachments/bed1f7e9-29fd-4690-97cc-fc56dbb1413a/붙여넣은 텍스트.txt`.

## Global Constraints

- Existing modifications belong to the user; initial patch/status saved under `/tmp/parallax-control-feel-baseline`.
- Implement the supplied report's basic campaign interaction. Portals, multiplayer, cloning and global player resizing are outside the existing game's mechanics.
- Preserve checkpoint, hazard, mirror, buoyancy and puzzle behavior; keep optional Q/E, Z/X and C adjustments.
- Right mouse holds object rotation and freezes camera look; Backspace cancels. Left click remains pickup/drop toggle. Walking remains available while holding.
- Mouse look has no smoothing or delta-time multiplier. Object mouse rotation and feedback use frame-independent timing. No new third-party runtime dependencies.
- Current map scale limits remain intentional puzzle constraints. Preserve nonuniform shapes and usable platforms.

## Review Focus

- Pickup assistance must never select through a wall, including dense collider scenes.
- Pause/reset/respawn must clear buffered movement and jumping; no unintended jump or camera motion on resume.
- A held object's centre distance and scale preserve angular size while aligning over 0.8 seconds; collision and minimum size overrides must remain safe.
- Quick turns, offscreen silhouettes, narrow frames, walls and player overlap must not tunnel or permanently trap the held object.
- Short/long jumps and mantle must work at several frame rates, preserve overhead clearance and keep upper-floor puzzle routes reachable.

## Task 1: Shared movement and input

**Files:** New `Shared/Scripts/FirstPersonMotor.cs`, `Shared/Scripts/PlayerControls.cs`, `Shared/Editor/ControlFeelTests.cs`, mirrored into Gallery standalone. Main agent integrates `*Game.cs` and `*World.cs`.

**Interfaces:** `FirstPersonMotor.Step(CharacterController, Vector2 localInput, float yawRadians, bool jumpPressed, bool jumpHeld, float dt, int mask, Vector3 supportVelocity)`; `Reset()`; velocity/grounded/mantling properties. `PlayerControls` supplies keyboard/gamepad input, immediate look and saved options.

- [ ] Add and run focused behavior checks for keyboard ramp/reversal, squared analog input, diagonal cap, ground/air acceleration, variable jump, 50ms buffer, 0.75s jump cooldown and 0.3s mantle clearance.
- [ ] Implement input filtering (3/s), speed 4, acceleration 56.6/15, gravity 20, jump speed 6, hold gravity cancellation up to 0.5/6 seconds and terminal speed 20. Avoid reproducing known state-order bugs.
- [ ] Set capsule height/radius 1.6/.3, feet-relative centre .8, step .2, skin .05, slope 60; eye height 1.55 preserves feet-based map coordinates. FOV 70 (70–90 options), near .05.
- [ ] Integrate all five current controllers without changing public world-space Tick semantics or map callbacks.

## Task 2: Pickup and camera-space projection

**Files:** `Shared/Scripts/ObjectInteraction.cs`, `PerspectivePlacement.cs`, `InteractionRaycast.cs`, `Shared/Editor/ObjectInteractionTests.cs`; corresponding standalone interaction adapter; map-held pose integration.

- [ ] Extend current real-physics fixtures before implementation: assisted pickup and wall rejection, centre-distance reference, immediate first projection, 0.8s direction alignment, current-view drop solve, player-overlap shrinking, offscreen samples.
- [ ] Centre-based reference and camera-space direction/rotation, initial ease-out alignment, late-frame direct kinematic placement. Keep shape proportions and existing collision recovery.
- [ ] Rasterize full projected compound silhouette (including offscreen parts), select restrictive positive distance, apply common 0.99 clearance, fallback 400 limited by map bounds. Preserve holes and thin parts.
- [ ] Shrink around the placed centre to avoid player capsule overlap (10 binary steps, respecting map minimum size); use blocked feedback when no valid result exists.
- [ ] Re-solve at release, validate, then restore dynamic physics; cancellation restores complete captured state.

## Task 3: Release physics and feedback

**Files:** All five `*Prop.cs`, new `Shared/Scripts/PropReleasePhysics.cs`, all five `*Hud.cs`, new shared options/feedback scripts and focused Editor checks.

- [ ] Characterize cubic mass and zero release velocity; add checks for temporary 2s CCD restoration and cancel/regrab cleanup.
- [ ] Preserve existing mass safety limits and water/ball/upright behavior; restore pre-release collision mode after two seconds. No inherited player throw velocity.
- [ ] Update all control hints and pause options for rotation, cancel, mouse sensitivity, invert axes and FOV. Show distinct aim/held/blocked/rotation cursor states with frame-independent emphasis.
- [ ] Provide simple movement/pickup/release/impact audio using existing assets or small generated signals where no assets exist, with no artificial head bob.

## Task 4: Integration and delivery

- [ ] Run existing shared interaction, Gallery hold, map rules and build-command checks plus new behavior tests in Unity. Record baseline failures separately.
- [ ] Build runnable macOS Gallery and Starship previews and inspect actual rendered UI/gameplay where tooling permits.
- [ ] Review the final diff for regressions and preserve user edits. Report actual verification and unmeasured physical gamepad/subjective feel limits.
