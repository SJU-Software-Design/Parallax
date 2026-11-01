# Held contact and project layout implementation plan

Goal: held objects shrink and regrow against ordinary props without freezing; keep the repository easy to navigate.
Spec: user request and Superliminal_Physics_REA_Package/Held_Contact_REA_v2.md.

Constraints: preserve Unity 6000.3.24f1, existing scene/asset GUIDs, both Unity projects, authored release bounds, existing binaries and third-party notices. Work directly in the user-specified checkout; the only pre-existing untracked directory is .idea. No commits or publishing requested.

1. Reproduce minimum-scale contact and camera-inside-blocker failures with real Unity colliders and the actual PerspectivePlacement/ObjectInteraction code. Compile temporary copies against the included Mono player when no Editor is installed; record that this is not an Editor rebuild. Watch the new regression fail before editing production code.
2. Repair held projection independently of rigidbody contacts and release validation. Test side/front contact, approach/retreat, camera motion, rotation, nested colliders, repeated frames, non-overlap and release/cancel. Keep distance/scale coupled to the captured reference; avoid frame smoothing and stale accepted-pose feedback.
3. Move intact Unity roots to projects/all-stages and projects/gallery, old players to builds/archive, outputs to artifacts/reports and historical work to archive/work. Preserve contents and GUIDs. Update current tooling, build outputs, launchers, path references and README; historical records remain historical.
4. Run native-engine regression and existing meaningful physics/control suites, C# compilation for both projects, build-command checks and path/reference checks. Request a read-only independent final review and fix significant findings.

Review focus: minimum-scale constraints versus held preview; queries starting inside blockers; non-monotonic finite obstacle geometry; stage boundary/release rules; stale paths and spaces/non-ASCII paths after moves.


Completion record: held contact, player-overlap and moving-transform failures reproduced before fixes; final native control/physics suites 334 checks passed, command/packaging tests 9 passed. All 3,654 relocated tracked files remain present; 782 existing .meta files and 24 critical archived player files unchanged; current document links valid. Independent review findings for release feedback, close-wall expectations and Mac package paths addressed. Historical exporters and exception recovery auxiliary feedback documented as retained scope. No staging, commits or publishing. Editor absent: no formal scene/shader rebuild or distributable update produced.
