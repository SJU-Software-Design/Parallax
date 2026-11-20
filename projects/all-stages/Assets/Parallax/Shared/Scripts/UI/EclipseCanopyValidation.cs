using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Eclipse
{
    // Explicit isolated native fixtures only. These inspect actual scene colliders,
    // supported materials and camera projection data, never rendered pixels. Grid
    // eyes and the visibility-only teleports below are declared fixtures; the
    // approach and jump also consume the production movement Tick.
    public static class EclipseCanopyValidation
    {
        const float Dt = 1f / 60;
        const int RimSamples = 64;

        public static void Run(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            check(Application.isBatchMode && Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-test") >= 0,
                "Canopy fixtures run only in the explicit isolated native batch player", "No render/readback, captures or OS input");
            check(game != null && game.world != null && game.view != null && game.body != null &&
                game.Credits.Section == EndingCreditSection.Inactive && game.held == FinaleItem.None,
                "Canopy validation begins in the real unsolved Eclipse opening", "");
            bool started = game.started, paused = game.paused, automated = game.automated;
            float configuredFov = game.view.fieldOfView, configuredAspect = game.view.aspect;
            var world = game.world;
            var roofs = world.ShelterRoofColliders;
            var renderers = world.ShelterRoofRenderers;
            try
            {
                game.automated = true;
                game.ResetGame(true);
                RecoverOpaque(world);
                Physics.SyncTransforms();
                check(world.ShelterRoot != null && roofs.Count == 3 && renderers.Count == 3 &&
                    roofs.All(roof => roof != null && roof.enabled && !roof.isTrigger && roof.gameObject.activeInHierarchy &&
                        (roof.gameObject.layer == 0 || roof.gameObject.layer == 8)) &&
                    renderers.All(renderer => renderer != null && renderer.enabled && renderer.shadowCastingMode == ShadowCastingMode.On &&
                        renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null && renderer.sharedMaterial.shader.isSupported &&
                        renderer.sharedMaterial.HasProperty("_CameraOpacity") && renderer.sharedMaterial.FindPass("ShadowCaster") >= 0),
                    "The high rear roof and two low shade walls retain actual solid colliders and supported shadow-caster materials",
                    "Scene/material data only; rendered shadow pixels are not inspected.");
                check(roofs.Count(roof => roof.bounds.min.y > 4.8f && roof.bounds.max.z <= 5.01f) == 1 &&
                    roofs.Count(roof => roof.bounds.min.y < .01f && roof.bounds.max.y < 1.26f && roof.bounds.min.z > 5.07f &&
                        roof.bounds.max.z < 5.33f && roof.bounds.size.x > 11) == 2 &&
                    !roofs.Any(roof => roof.bounds.min.z > 12),
                    "The narrow forward beam is replaced by low walls with a three-metre central passage",
                    string.Join("; ", roofs.Select(roof => roof.name + " bounds=" + roof.bounds)));
                check(Vector3.Distance(EclipseFinaleGame.SunPosition, EclipseFinaleGame.PuzzleSunPosition) < .0001f &&
                    Mathf.Abs(EclipseFinaleGame.SunPosition.y - 30) < .001f && Vector3.Distance(game.Spawn, new Vector3(-3.2f,.06f,1)) < .001f &&
                    Vector3.Distance(game.moon.position, new Vector3(-2.6f, .292f, 3.4f)) < .0001f,
                    "The lower puzzle sun and relocated spawn preserve the original moon position", "sun=" + EclipseFinaleGame.SunPosition + " spawn=" + game.Spawn);
                check(roofs.All(roof =>
                    ((roof.bounds.min.y > 4.8f && roof.gameObject.layer == 0 &&
                        (EclipseFinaleWorld.MoonSupportMask & (1 << roof.gameObject.layer)) == 0) ||
                     (roof.bounds.max.y < 1.3f && roof.gameObject.layer == 8 &&
                        (EclipseFinaleWorld.MoonSupportMask & (1 << roof.gameObject.layer)) != 0)) &&
                    !Physics.GetIgnoreLayerCollision(game.body.gameObject.layer, roof.gameObject.layer) && !Physics.GetIgnoreCollision(game.body, roof)),
                    "High roofs leave moon support while low walls retain real placement support and all player collision", "MoonSupportMask=" + EclipseFinaleWorld.MoonSupportMask);
                CheckStation(game, check, "initial configured opening");
                CheckInitialFrustum(game, check, "actual configured FOV/aspect", false);
                foreach (float fov in new[] { 50f, 70f, 100f })
                {
                    // 50/100 are deliberately camera-only projection stresses,
                    // outside the shared menu's 70..90 allowed settings range.
                    game.view.fieldOfView = fov;
                    game.view.aspect = 16f / 9;
                    game.FrameOpeningSun();
                    CheckInitialFrustum(game, check, "camera-only FOV " + fov + " at 16:9", true);
                    if (Mathf.Abs(fov-70)<.01f) CheckOpeningMoon(game,check);
                }
                game.view.fieldOfView = configuredFov;
                game.view.aspect = configuredAspect;
                game.FrameOpeningSun();
                CheckConnectedShadowGrid(game, check);
                CheckActualApproach(game, check);
                CheckActualJump(game, check);
                CheckExposureRecovery(game, check);
                CheckCentralPassage(game, check);
                CheckVisibilityFallback(game, check);
            }
            finally
            {
                game.view.fieldOfView = configuredFov;
                game.view.aspect = configuredAspect;
                game.ResetGame(started);
                if (paused) game.Pause(); else if (started) game.Play();
                game.automated = automated;
                RecoverOpaque(world);
                Physics.SyncTransforms();
            }
            check(world.ShelterRoofOpacities.All(value => value > .999f) && game.held == FinaleItem.None &&
                Mathf.Abs(game.view.fieldOfView - configuredFov) < .001f && Mathf.Abs(game.view.aspect - configuredAspect) < .001f,
                "Canopy fixtures restore the unsolved moon, opaque panels and original camera settings", "");
        }

        static void RecoverOpaque(EclipseFinaleWorld world)
        {
            for (int i = 0; i < 25; i++) world.TickShelterVisibility(.1f);
        }
        static bool DirectShade(EclipseFinaleWorld world, Vector3 floor)
        {
            Vector3 direction = world.CurrentSunPosition.normalized;
            return world.ShelterRoofColliders.Any(roof => roof.enabled && roof.Raycast(new Ray(floor, direction), out _, 250));
        }
        static Vector3 SolarRay(Vector3 eye, Vector3 sun, int sample)
        {
            Vector3 delta = sun - eye;
            Vector3 axis = delta.normalized;
            if (sample == 0) return axis;
            Vector3 right = Vector3.Cross(Vector3.up, axis).normalized;
            if (right.sqrMagnitude < .0001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(axis, right).normalized;
            float angle = Mathf.Asin(Mathf.Clamp01(EclipseFinaleGame.SunRadius / delta.magnitude));
            float around = (sample - 1) * 2 * Mathf.PI / RimSamples;
            return (axis * Mathf.Cos(angle) + (right * Mathf.Cos(around) + up * Mathf.Sin(around)) * Mathf.Sin(angle)).normalized;
        }
        static int SolarBlockedRays(EclipseFinaleWorld world, Vector3 eye, out string first)
        {
            int count = 0;
            first = "";
            float distance = Vector3.Distance(eye, world.CurrentSunPosition);
            for (int sample = 0; sample <= RimSamples; sample++)
            {
                Vector3 direction = SolarRay(eye, world.CurrentSunPosition, sample);
                foreach (var roof in world.ShelterRoofColliders)
                    if (roof.enabled && roof.Raycast(new Ray(eye, direction), out _, distance))
                    {
                        count++;
                        if (first.Length == 0) first = "sample=" + sample + " roof=" + roof.name + " eye=" + eye;
                        break;
                    }
            }
            return count;
        }
        static void CheckStation(EclipseFinaleGame game, Action<bool, string, string> check, string label)
        {
            Vector3 floor = game.body.transform.position; floor.y = .03f;
            bool actualShade = DirectShade(game.world, floor);
            int blocked = SolarBlockedRays(game.world, game.view.transform.position, out string first);
            check(actualShade && game.world.IsShelterShadowAt(floor) && game.InShelter && game.Safe && blocked == 0 &&
                game.held == FinaleItem.None && game.world.ShelterRoofOpacities.All(value => value > .999f),
                "Opaque whole-sun visibility and real floor shadow coexist: " + label,
                "floor=" + floor + " solar rays=" + (RimSamples + 1) + " blocked=" + blocked + " " + first);
        }
        static void CheckInitialFrustum(EclipseFinaleGame game, Action<bool, string, string> check, string label, bool stress)
        {
            Vector3 eye = game.view.transform.position;
            float distance = Vector3.Distance(eye, game.world.CurrentSunPosition);
            float minX = 1, maxX = 0, minY = 1, maxY = 0;
            bool inside = true;
            for (int sample = 0; sample <= RimSamples; sample++)
            {
                Vector3 at = game.view.WorldToViewportPoint(eye + SolarRay(eye, game.world.CurrentSunPosition, sample) * distance);
                inside &= at.z > 0 && at.x > .001f && at.x < .999f && at.y > .001f && at.y < .999f;
                minX = Mathf.Min(minX, at.x); maxX = Mathf.Max(maxX, at.x);
                minY = Mathf.Min(minY, at.y); maxY = Mathf.Max(maxY, at.y);
            }
            int blocked = SolarBlockedRays(game.world, eye, out string first);
            check(inside && blocked == 0 && game.held == FinaleItem.None && game.world.ShelterRoofOpacities.All(value => value > .999f),
                "The actual opening camera contains the full unobstructed solar disc: " + label,
                "FOV=" + game.view.fieldOfView + " aspect=" + game.view.aspect + " viewport=" +
                minX.ToString("F4") + ".." + maxX.ToString("F4") + "/" + minY.ToString("F4") + ".." + maxY.ToString("F4") +
                " cameraOnlyStress=" + stress + " " + first);
        }
        static void CheckOpeningMoon(EclipseFinaleGame game,Action<bool,string,string> check)
        {
            Vector3 eye=game.view.transform.position,delta=game.moon.position-eye,axis=delta.normalized;
            Vector3 right=Vector3.Cross(Vector3.up,axis).normalized,up=Vector3.Cross(axis,right).normalized;
            float angle=Mathf.Asin(Mathf.Clamp01(game.MoonRadius*1.04f/delta.magnitude));bool inside=true;
            for(int i=0;i<=64;i++)
            {
                float around=(i-1)*Mathf.PI/32;
                Vector3 direction=i==0?axis:axis*Mathf.Cos(angle)+(right*Mathf.Cos(around)+up*Mathf.Sin(around))*Mathf.Sin(angle);
                Vector3 at=game.view.WorldToViewportPoint(eye+direction*delta.magnitude);
                inside&=at.z>0 && at.x>0 && at.x<1 && at.y>0 && at.y<1;
            }
            check(inside,"The default 70-degree opening also frames the unchanged floor moon","Moon mesh envelope stress 1.04 times nominal radius, actual camera projection");
        }
        static void CheckConnectedShadowGrid(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            int points = 0, blockedRays = 0, mismatchedShade = 0;
            string first = "";
            // Declared observation grid spans both sides of the lateral route and
            // the broad connected low-wall shadow, including the common jump eye.
            foreach (float eyeY in new[] { 1.55f, 1.61f, 2.4f, 3.01f })
                for (int ix = 0; ix < 13; ix++) for (int iz = 0; iz < 12; iz++)
                {
                    Vector3 floor = new Vector3(-8f + ix * .5f, .03f, -1f + iz * .5f);
                    Vector3 eye = new Vector3(floor.x, eyeY, floor.z);
                    if (!DirectShade(game.world, floor) || !game.world.IsShelterShadowAt(floor)) mismatchedShade++;
                    int blocked = SolarBlockedRays(game.world, eye, out string failure);
                    blockedRays += blocked;
                    if (blocked > 0 && first.Length == 0) first = failure;
                    points++;
                }
            check(mismatchedShade == 0 && blockedRays == 0 && game.world.ShelterRoofOpacities.All(value => value > .999f),
                "A 6m by 5.5m free-walking area has real shade and a fully clear sun at standing and jump heights",
                "area=33 square metres; x[-8,-2], z[-1,4.5]; declaredGridEyes=" + points + " actualColliderRays=" + points * (RimSamples + 1) +
                " missingRealShade=" + mismatchedShade + " blocked=" + blockedRays + " " + first);
        }
        static void CheckActualApproach(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            game.Teleport(game.Spawn);
            game.FrameOpeningSun();
            Vector3 start = game.body.transform.position;
            Vector3 target = new Vector3(game.moon.position.x - 1f, start.y, game.moon.position.z);
            Vector3 direction = Vector3.ProjectOnPlane(target - start, Vector3.up).normalized;
            Vector2 input = new Vector2(direction.x, direction.z);
            int observed = 0;
            bool allSafe = true, allClear = true;
            for (int frame = 0; frame < 120; frame++)
            {
                game.ApplySharedControlFrame(new PlayerControls.Frame { Move = input });
                game.Tick(Dt, input, false);
                Physics.SyncTransforms();
                Vector3 floor = game.body.transform.position; floor.y = .03f;
                allSafe &= DirectShade(game.world, floor) && game.world.IsShelterShadowAt(floor) && game.InShelter && game.Safe && game.exposure < .0001f;
                allClear &= SolarBlockedRays(game.world, game.view.transform.position, out _) == 0 &&
                    game.world.ShelterRoofOpacities.All(value => value > .999f) && game.held == FinaleItem.None;
                float progress = Vector3.Dot(game.body.transform.position - start, direction);
                if (observed < 5 && progress >= (target - start).magnitude * (observed + 1) / 5)
                {
                    CheckStation(game, check, "production approach station " + (++observed));
                }
                if (progress >= (target - start).magnitude) break;
            }
            check(observed == 5 && allSafe && allClear && game.respawns == 0,
                "The production movement path toward the original moon preserves continuous shade and a fully open sun",
                "stations=" + observed + " start=" + start + " end=" + game.body.transform.position + " actualTickFrames, no teleport along this route");
        }
        static void CheckActualJump(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            game.Teleport(game.Spawn);
            game.FrameOpeningSun();
            for (int i = 0; i < 12; i++) game.Tick(Dt, Vector2.zero, false);
            float baseY = game.body.transform.position.y, peak = baseY;
            bool safe = true, clear = true;
            for (int i = 0; i < 90; i++)
            {
                game.ApplySharedControlFrame(new PlayerControls.Frame { JumpPressed = i == 0, JumpHeld = true });
                game.Tick(Dt, Vector2.zero, i == 0);
                Physics.SyncTransforms();
                peak = Mathf.Max(peak, game.body.transform.position.y);
                safe &= game.InShelter && game.Safe && game.exposure < .0001f;
                clear &= SolarBlockedRays(game.world, game.view.transform.position, out _) == 0;
            }
            check(peak - baseY > 1.1f && safe && clear && peak + game.body.height < game.world.ShelterRoofColliders.Where(roof => roof.bounds.min.y > 4.8f).Min(roof => roof.bounds.min.y),
                "The actual common held jump stays safe below the high roof with the whole sun visible",
                "base=" + baseY + " peak=" + peak + " bodyHeight=" + game.body.height);
        }
        static void CheckExposureRecovery(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            Vector3 outside = new Vector3(0, .03f, 30);
            check(!DirectShade(game.world, outside) && !game.world.IsShelterShadowAt(outside),
                "The real roof-ray and shelter queries agree outside the arrival shadow", "outside=" + outside);
            game.Teleport(new Vector3(0, .06f, 30));
            for (int i = 0; i < 10; i++) game.Tick(Dt, Vector2.zero, false);
            float exposed = game.exposure;
            check(!game.InShelter && !game.Safe && exposed > .005f,
                "Leaving the physical canopy shadow drives live sunlight exposure", "exposure=" + exposed);
            game.Teleport(game.Spawn);
            for (int i = 0; i < 10; i++) game.Tick(Dt, Vector2.zero, false);
            check(game.InShelter && game.Safe && game.exposure < exposed,
                "Returning to the broad low-wall shadow restores safety and live recovery", "exposure=" + game.exposure);
        }
        static void CheckCentralPassage(EclipseFinaleGame game, Action<bool,string,string> check)
        {
            game.ResetGame(true);
            // Stated collision-route fixture: this post-eclipse route is tested
            // with the actual CharacterController, without injecting a completed
            // eclipse or advancing the unrelated pre-eclipse exposure clock.
            Vector3 start=game.body.transform.position;
            int moves=0;bool clear=true;
            foreach(Vector3 waypoint in new[]{new Vector3(0,start.y,4.4f),new Vector3(0,start.y,6.2f)})
            {
                for(int i=0;i<200 && Vector3.Distance(game.body.transform.position,waypoint)>.002f;i++)
                {
                    Vector3 before=game.body.transform.position;
                    Vector3 step=Vector3.ClampMagnitude(waypoint-before,.10f);
                    CollisionFlags flags=game.body.Move(step);Physics.SyncTransforms();moves++;
                    clear&=(flags&(CollisionFlags.Sides|CollisionFlags.Above))==0 &&
                        Vector3.Distance(game.body.transform.position,before+step)<.005f;
                }
                clear&=Vector3.Distance(game.body.transform.position,waypoint)<.005f;
            }
            check(clear && moves>30 && game.body.transform.position.z>=6.19f && Mathf.Abs(game.body.transform.position.x)<.01f,
                "The real player capsule reaches and crosses the central passage from the source spawn",
                "Declared CC-only route without success flags: "+start+" -> (0,4.4) -> (0,6.2); moves="+moves+" end="+game.body.transform.position);
            game.ResetGame(true);
        }
        static void CheckVisibilityFallback(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            RecoverOpaque(game.world);
            game.Teleport(new Vector3(game.moon.position.x - 1.6f, .04f, game.moon.position.z));
            game.AimAt(game.moon.position); game.Interact();
            check(game.held == FinaleItem.Moon,
                "The fallback obstruction fixture first grabs the actual original moon", "Declared pickup-position fixture");
            // A separate beyond-wall fixture deliberately goes outside the clear
            // arrival route. Fade remains a fallback for later free exploration.
            game.Teleport(new Vector3(-3.2f, .06f, 8));
            game.AimAt(game.moon.position);
            Vector3 towardMoon = game.moon.position - game.view.transform.position;
            check(game.world.ShelterRoofColliders.Any(roof => roof.Raycast(new Ray(game.view.transform.position,
                    towardMoon.normalized), out _, towardMoon.magnitude)) &&
                game.world.IsShelterBlockingDisc(game.view.transform.position, game.moon.position, game.MoonRadius),
                "A declared beyond-wall held-moon fixture really intersects a low shade wall", "Visibility fixture only, no completion flag injection");
            float before = game.world.ShelterRoofOpacities.Min();
            game.world.TickShelterVisibility(Dt);
            float initial = game.world.ShelterRoofOpacities.Min();
            for (int i = 0; i < 14; i++) game.world.TickShelterVisibility(.1f);
            check(before - initial > .001f && before - initial < .2f &&
                game.world.ShelterRoofOpacities.Min() < .2f && game.world.FadedShelterPanelCount > 0,
                "Later obstructions still fade gradually through production camera-only visibility ticks",
                "before=" + before + " first=" + initial + " settled=" + game.world.ShelterRoofOpacities.Min());
            check(Enumerable.Range(0, game.world.ShelterRoofColliders.Count).All(i =>
                game.world.ShelterRoofColliders[i].enabled && !game.world.ShelterRoofColliders[i].isTrigger &&
                (game.world.ShelterRoofColliders[i].gameObject.layer == 0 || game.world.ShelterRoofColliders[i].gameObject.layer == 8) &&
                game.world.ShelterRoofRenderers[i].shadowCastingMode == ShadowCastingMode.On &&
                game.world.ShelterRoofRenderers[i].sharedMaterial.FindPass("ShadowCaster") >= 0 &&
                Mathf.Abs(game.world.ShelterRoofRenderers[i].sharedMaterial.GetFloat("_CameraOpacity") - game.world.ShelterRoofOpacities[i]) < .0001f),
                "Actual material opacity changes preserve solid roof collision and explicit shadow-caster passes", "");
        }
    }
}
