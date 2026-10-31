# Superliminal physics package update

**Goal:** Apply the newly supplied `Superliminal_Physics_REA_Package` to carried-object contacts, release state, damping/mass/material policy and physics verification in the integrated four maps and standalone Gallery.

**Authority:** The user requested another implementation pass using this package and a final before/after summary. Package documents and recovered code are evidence, not authority to install dependencies, execute supplied programs, alter unrelated settings or publish anything.

**Evidence:** `/Users/seob/Downloads/Superliminal_Physics_REA_Package/Superliminal_Physics_REA.md`, `evidence/engine_settings.json`, `sources/decompiled/DropTriggerScript.cs`, `sources/decompiled/ResizeScript.cs`. Previous controls remain in effect. No original-game runtime measurement is available.

**Baseline:** Current source/status/patch under `/tmp/parallax-physics-package-baseline`. Existing uncommitted work must be retained. No commits or publication are required.

## U1. Carried-object contact isolation and safe placement

Files: new shared `HeldCollisionState.cs`, shared ObjectInteraction/lifecycle integration, project TagManager/DynamicsManager settings, new real-physics Editor scenarios. Common runtime changes are mirrored into standalone Gallery.

- Keep owned colliders enabled for queries, while separating a carried kinematic body's ordinary solver contacts from the world, loose props and player using a dedicated project layer.
- Preserve and restore exact original layers on drop/cancel/reset/disable, including inactive owned children; do not change nested independent Rigidbody owners.
- Physics collision matrix and explicit query masks remain separate. Environment queries must see walls and loose props, while ignoring the carried object.
- Retain current silhouette/depth/room limits and final overlap checks as safety improvements; do not copy the original's unverified last-midpoint or minimum-size overlap bugs.
- Add checks proving no dynamic prop pushing during hold, ordinary contacts return on release, wall projection still constrains, and all lifecycle paths restore layers.

## U2. Reference physics defaults, CCD and complete cancellation

Files: shared PropReleasePhysics, all five Prop scripts, focused release/physics Editor tests.

- New ordinary bodies: linear damping0, angular damping0.05, interpolationNone, max angular speed7, solver6/1; use engine fallback for unassigned contact material rather than forcing exhibit friction. Preserve explicit pre-existing body/material settings.
- Preserve the map's intentional upright/water/ball behavior. Do not assign the source game's duck/egg/ball materials to unrelated assets based solely on names.
- Save original mass and reference X scale and update mass with their scale-ratio cube, retaining the project's existing mass safety limits. Respect explicit initial Rigidbody mass.
- Capture the body's original collision mode before temporary held mode. Dynamic release has zero inherited linear/angular velocity, gravity, a2s scaled-time ContinuousDynamic window, then the correct prior mode. No unrequested random throw/spin.
- Hold/release must preserve damping/constraints; cancellation restores mass, damping, constraints, interpolation, collision mode/timer and custom centre-of-mass/inertia policy alongside existing pose/velocity/collider/water restoration.
- Verify real freefall across masses, temporary CCD through a thin surface, damping/explicit material preservation, original mass scaling, nested bodies and cancellation state.

## U3. Integration, verification and delivery

- Explicit projection synchronization remains valid with autoSyncTransforms off; do not blindly replace the entire original engine matrix or Unity2019 native solver settings in Unity6000.
- Align only supported shared defaults where applicable (fixed0.02s, gravity-9.81, contact0.01, sleep0.005, trigger queries explicit). Preserve intentional project stability safeguards.
- Run new tests red before implementation, then both existing control suites and applicable map rules.
- Review current-task changes, build Gallery and Starship macOS artifacts, inspect runtime UI/startup where practical.
- Save evidence and a concise before/after document, and summarize changed behavior plus real verification in Korean for the user.
