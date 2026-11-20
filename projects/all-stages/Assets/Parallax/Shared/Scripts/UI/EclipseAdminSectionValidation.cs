using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Focused native observations of the public administrator entry. This
    // helper does not authorize an entry, inject flags, render or send OS input.
    public static class EclipseAdminSectionValidation
    {
        public static void RunDenied(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            check(Application.isBatchMode && game != null && game.automated,
                "Administrator denial checks run in the isolated native fixture", "");
            check(!global::ParallaxGameShell.Administrator || !GameProgressRuntime.TestSession,
                "A required administrator entry credential is absent",
                "Administrator=" + global::ParallaxGameShell.Administrator + " TestSession=" + GameProgressRuntime.TestSession);
            string before = State(game);
            check(!game.TryStartAdminSection(0) && !game.TryStartAdminSection(1) &&
                !game.Credits.TryFinishAdministratorSection(),
                "Both Eclipse sections and the credit-finish helper deny missing credentials", "");
            check(State(game) == before,
                "Denied section entry leaves puzzle, credits, player, EXIT and route state unchanged", "");
        }

        // Leaves the second section paused, ready for the caller's real CC,
        // EXIT traversal and gradual title-return checks.
        public static void RunReady(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            check(Application.isBatchMode && game != null && game.automated &&
                global::ParallaxGameShell.Administrator && GameProgressRuntime.TestSession,
                "Administrator section checks use both real session credentials", "");
            bool unlockedBefore = GameCompletionState.CreditsUnlocked;
            Dictionary<string, string> storageBefore = Storage();

            check(game.TryStartAdminSection(0), "The public first-section entry succeeds for the administrator test session", "");
            CheckOpening(game, check);
            var mesh = game.moon.GetComponent<MeshFilter>().sharedMesh;
            var material = game.moon.GetComponent<Renderer>().sharedMaterial;
            var texture = material.mainTexture;
            Quaternion moonRotation = game.moon.rotation;
            Vector3 sourceSignPosition = game.exitSign.position, sourceSignScale = game.exitSign.localScale;
            Quaternion sourceSignRotation = game.exitSign.rotation;
            BoxCollider[] signPanels = game.exitSign.GetComponentsInChildren<BoxCollider>()
                .Where(panel => panel.enabled && !panel.isTrigger).ToArray();
            Vector3[] panelCenters = signPanels.Select(panel => panel.center).ToArray();
            Vector3[] panelSizes = signPanels.Select(panel => panel.size).ToArray();
            float fov = game.view.fieldOfView, radius = game.body.radius, height = game.body.height;
            string opening = State(game);
            check(!game.TryStartAdminSection(-1) && !game.TryStartAdminSection(2) && State(game) == opening,
                "Out-of-range section indices are denied without mutation", "");

            check(game.TryStartAdminSection(1), "The public post-credits entry succeeds without ticking the roll", "");
            check(game.paused && !game.started && game.body.enabled && game.phase == FinalePhase.Chamber &&
                game.held == FinaleItem.None && !game.EndingSequenceActive && !game.ExitPresentation.Active &&
                !game.QuitRequested && !game.EndingReturnReady,
                "Post-credits entry preserves the paused SceneStageReady handoff", "");
            check(game.Credits.Section == EndingCreditSection.Finished && game.Credits.TeamComplete &&
                game.Credits.ThanksComplete && game.ExitUnlocked && game.Credits.PresentedTeamLines.Length == 0,
                "Explicit administrator entry unlocks EXIT without claiming that team credits were viewed", "");
            MoonAlignmentSample accepted = game.ReleasedMoonAlignment;
            check(game.MoonAnchored && !game.MoonFalling && game.MoonAlignmentConfirmed &&
                accepted.GeometryValid && accepted.CanAnchor && accepted.Complete &&
                accepted.Coverage >= MoonAlignmentRules.RequiredCoverage &&
                accepted.ObserverPosition == game.Credits.LockedCameraPosition &&
                accepted.SolarPosition == game.Credits.LockedSunPosition &&
                accepted.MoonPosition == game.Credits.LockedMoonPosition,
                "Post-credits Moon retains the measured accepted release snapshot",
                "Raw coverage=" + accepted.Coverage + " effective ratio=" + accepted.EffectiveRadiusRatio);
            MoonAlignmentSample measured = MoonAlignmentRules.Evaluate(accepted.ObserverPosition, Vector3.forward,
                accepted.MoonPosition, game.Credits.LockedMoonScale.x * .5f, accepted.SolarPosition,
                EclipseFinaleGame.SunRadius, Coverage(accepted.ObserverPosition, accepted.MoonPosition,
                    game.Credits.LockedMoonScale.x * .5f, accepted.SolarPosition));
            check(measured.Complete && Mathf.Abs(measured.Coverage - accepted.Coverage) < .00001f &&
                game.totality && game.coverage == 1 && game.darkness == 1 && game.TotalityStableTime >= 1.8f &&
                game.world.PresentationDarkness == 1 && Mathf.Abs(game.world.SunLightIntensity - .002f) < .0001f,
                "Accepted geometry is independently recomputed and the completed night presentation is applied", "");
            check(game.moon.GetComponent<MeshFilter>().sharedMesh == mesh &&
                game.moon.GetComponent<Renderer>().sharedMaterial == material && material.mainTexture == texture &&
                Quaternion.Angle(game.moon.rotation, moonRotation) < .001f,
                "Administrator entry preserves the original Moon mesh, texture, material and rotation", "");
            float outer = game.Credits.LockedMoonOuterRadius;
            Vector3 eye = game.view.transform.position;
            check(!EclipseEndingCredits.IntersectsGameplayRoute(game.moon.position, outer) &&
                Vector3.Distance(eye, game.moon.position) > outer + .15f && game.moon.position.y - outer > .02f &&
                !Physics.ComputePenetration(game.body, game.body.transform.position, game.body.transform.rotation,
                    game.moon.GetComponent<SphereCollider>(), game.moon.position, game.moon.rotation, out _, out _),
                "The real Moon envelope clears the camera, player capsule, floor and EXIT route",
                "outer=" + outer + " floorClearance=" + (game.moon.position.y - outer));
            check(game.body.transform.position == game.Spawn &&
                Vector3.Distance(eye, game.body.transform.position + Vector3.up * EclipseFinaleGame.EyeHeight) < .00001f &&
                eye == game.Credits.LockedCameraPosition && game.view.fieldOfView == fov &&
                game.view.fieldOfView == game.Credits.LockedFieldOfView && game.body.radius == radius &&
                game.body.height == height && game.Credits.CameraLiftProgress == 0,
                "Post-credits view restores the configured gameplay eye, FOV and actual shared capsule", "");
            check(game.world.ExitRouteCleared && game.world.ExitRouteClearProgress == 1 &&
                game.world.RetractableShadeWalls.All(wall => !wall.enabled && wall.transform.localPosition.y + .625f < -.15f) &&
                game.world.RetractableShadeRenderers.All(renderer => !renderer.enabled) &&
                game.world.ExitRouteGuideRenderers.All(renderer => renderer.enabled &&
                    renderer.GetComponents<Collider>().All(collider => !collider.enabled)),
                "Post-credits shade walls are lowered and disabled together with flush non-solid guides visible", "");
            check(game.exitSign.position == sourceSignPosition && game.exitSign.localScale == sourceSignScale &&
                game.exitSign.rotation == sourceSignRotation && !game.ExitFalling && !game.DoorFits &&
                signPanels.Length == 4 && Enumerable.Range(0, signPanels.Length).All(i => signPanels[i].enabled &&
                    !signPanels[i].isTrigger && signPanels[i].center == panelCenters[i] && signPanels[i].size == panelSizes[i]),
                "The original EXIT pose, four solid i-opening panels and required enlargement remain intact", "");

            game.Play();
            game.AimAt(game.exitSign.TransformPoint(new Vector3(0, .52f, 0)));
            game.UpdateAim();
            check(game.aimed == FinaleItem.Exit, "The actual post-credits ray aim selects the original EXIT sign", "");
            game.Interact();
            check(game.held == FinaleItem.Exit && signPanels.All(panel => !panel.enabled),
                "The public gameplay interaction picks up EXIT through its normal placement path", "");
            game.Cancel();
            check(game.held == FinaleItem.None && game.exitSign.position == sourceSignPosition &&
                game.exitSign.localScale == sourceSignScale && signPanels.All(panel => panel.enabled) &&
                SameRelease(accepted, game.ReleasedMoonAlignment),
                "Normal EXIT cancellation restores the sign and leaves the accepted Moon release unchanged", "");

            check(game.TryStartAdminSection(0), "Administrator can return from the second section to the first", "");
            CheckOpening(game, check);
            check(game.TryStartAdminSection(1), "Administrator can select the post-credits section again after reset", "");
            check(game.paused && !game.started && game.ExitUnlocked && game.totality &&
                game.world.ExitRouteCleared && SameRelease(accepted, game.ReleasedMoonAlignment),
                "Repeated section selection reconstructs the same safe paused post-credits state", "");
            check(GameCompletionState.CreditsUnlocked == unlockedBefore &&
                storageBefore.All(pair => Hash(pair.Key) == pair.Value),
                "Section selection alone earns no ending-seen unlock and writes no checkpoint or completion profile", "");
        }

        static void CheckOpening(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            check(game.paused && !game.started && game.phase == FinalePhase.Chamber && game.body.enabled &&
                game.body.transform.position == game.Spawn && game.held == FinaleItem.None &&
                !game.MoonAnchored && !game.MoonFalling && !game.HasMoonReleaseSnapshot &&
                !game.totality && game.coverage == 0 && game.darkness == 0 &&
                game.Credits.Section == EndingCreditSection.Inactive && !game.Credits.TeamComplete &&
                !game.Credits.ThanksComplete && !game.ExitUnlocked && !game.EndingSequenceActive &&
                game.moon.position == new Vector3(-2.6f, .292f, 3.4f) &&
                game.moon.localScale == Vector3.one * .55f,
                "First section reconstructs the original unsolved paused Eclipse opening", "");
            check(!game.world.ExitRouteCleared && game.world.ExitRouteClearProgress == 0 &&
                game.world.RetractableShadeWalls.All(wall => wall.enabled) &&
                game.world.RetractableShadeRenderers.All(renderer => renderer.enabled) &&
                game.world.ExitRouteGuideRenderers.All(renderer => !renderer.enabled),
                "Returning to the first section restores both physical shade walls and hides post-credit guides", "");
        }

        static bool SameRelease(MoonAlignmentSample a, MoonAlignmentSample b)
        {
            return a.Complete == b.Complete && a.CanAnchor == b.CanAnchor && a.Coverage == b.Coverage &&
                a.ObserverPosition == b.ObserverPosition && a.SolarPosition == b.SolarPosition &&
                a.MoonPosition == b.MoonPosition && a.EffectiveRadiusRatio == b.EffectiveRadiusRatio;
        }
        static float Coverage(Vector3 eye, Vector3 moon, float radius, Vector3 sun)
        {
            // The declared administrator Moon is exactly centred. Recompute
            // its disc area without reading or replacing the release snapshot.
            Vector3 lunar = moon - eye, solar = sun - eye;
            if (lunar.magnitude >= solar.magnitude || lunar.magnitude <= radius ||
                Vector3.Angle(lunar, solar) > .001f) return 0;
            float a = Mathf.Asin(radius * MoonAlignmentRules.EffectiveSilhouetteFactor / lunar.magnitude);
            float b = Mathf.Asin(EclipseFinaleGame.SunRadius / solar.magnitude);
            return a >= b ? 1 : a * a / (b * b);
        }
        static Dictionary<string, string> Storage()
        {
            return new[] { GameProgress.SavePath, GameProgress.BackupPath, GameProgress.AdminSavePath,
                GameProgress.AdminSavePath + ".bak", GameCompletionState.ProfilePath, GameCompletionState.BackupPath }
                .ToDictionary(path => path, Hash);
        }
        static string Hash(string path)
        {
            if (!File.Exists(path)) return "<absent>";
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }
        [Serializable]
        sealed class ObservedState
        {
            public Vector3 moonPosition, moonScale, exitPosition, exitScale, feet, eye;
            public Quaternion moonRotation, exitRotation, eyeRotation;
            public bool paused, started, totality, anchored, falling, team, thanks, bodyEnabled, snapshot, exitActive;
            public float darkness, coverage, fov, route;
            public int section, phase, held, aimed;
            public ObservedState(EclipseFinaleGame game)
            {
                moonPosition = game.moon.position; moonScale = game.moon.localScale; moonRotation = game.moon.rotation;
                exitPosition = game.exitSign.position; exitScale = game.exitSign.localScale; exitRotation = game.exitSign.rotation;
                feet = game.body.transform.position; eye = game.view.transform.position; eyeRotation = game.view.transform.rotation;
                paused = game.paused; started = game.started; totality = game.totality; anchored = game.MoonAnchored;
                falling = game.MoonFalling; team = game.Credits.TeamComplete; thanks = game.Credits.ThanksComplete;
                bodyEnabled = game.body.enabled; snapshot = game.HasMoonReleaseSnapshot; exitActive = game.ExitPresentation.Active;
                darkness = game.darkness; coverage = game.coverage; fov = game.view.fieldOfView;
                route = game.world.ExitRouteClearProgress; section = (int)game.Credits.Section; phase = (int)game.phase;
                held = (int)game.held; aimed = (int)game.aimed;
            }
        }
        static string State(EclipseFinaleGame game) => JsonUtility.ToJson(new ObservedState(game));
    }
}