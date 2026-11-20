using System;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Explicit feet/view fixtures call production grab/hold/cancel. Positive
    // observations never write prop transforms, sizes, or puzzle completion.
    public static class EclipsePickupContinuityValidation
    {
        const float Dt = 1f / 60, Tolerance = .005f;
        const string Fixture = "Declared feet/view fixture; real game.Tick and interaction; no OS input or captures";

        public static Vector3 SolvableMoonPickupFeet(EclipseFinaleGame game, float realSolarRadiusRatio = 1.10f)
        {
            Vector3 centre = game.moon.position;
            Vector3 eye = centre + new Vector3(-2, .04f + EclipseFinaleGame.EyeHeight - centre.y, 0);
            // Select an ordinary pickup distance giving the requested real solar radius ratio.
            // Only the declared body-position fixture changes, never the Moon.
            for (int i = 0; i < 8; i++)
            {
                float solarAngle = MoonAlignmentRules.SolarAngularRadius(eye, EclipseFinaleGame.PuzzleSunPosition,
                    EclipseFinaleGame.SunRadius);
                float distance = game.moon.localScale.x / (2 * Mathf.Sin(solarAngle * realSolarRadiusRatio));
                float vertical = eye.y - centre.y;
                float minimumHorizontal = game.body.radius + game.MoonRadius + .04f;
                eye.x = centre.x - Mathf.Sqrt(Mathf.Max(minimumHorizontal * minimumHorizontal, distance * distance - vertical * vertical));
            }
            return new Vector3(eye.x, .04f, centre.z);
        }

        public static void RunMoon(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            if (!Allowed(game, check, "Moon pickup continuity")) return;
            bool automated = game.automated, started = game.started, paused = game.paused;
            float fov = game.view.fieldOfView;
            try
            {
                foreach (string sample in new[] { "near 1.5m", "solvable solar ratio", "far 4m" })
                {
                    game.ResetGame(true); game.automated = true;
                    Vector3 sourcePosition = game.moon.position, sourceScale = game.moon.localScale;
                    Quaternion sourceRotation = game.moon.rotation;
                    Vector3 feet = sample == "solvable solar ratio" ? SolvableMoonPickupFeet(game) :
                        MoonFeetAtDistance(game, sample == "near 1.5m" ? 1.5f : 4);
                    game.Teleport(feet); Settle(game);
                    game.AimAt(game.moon.position);
                    float ratio = game.moon.localScale.x / Vector3.Distance(game.view.transform.position, game.moon.position);
                    game.Interact();
                    check(game.held == FinaleItem.Moon, "Moon continuity starts with actual source-moon pickup: " + sample, Fixture);
                    Observe(game, FinaleItem.Moon, ratio, 1, false, "first pickup frame / " + sample, null, check);
                    game.AimAt(EclipseFinaleGame.SunPosition);
                    Observe(game, FinaleItem.Moon, ratio, 180, true, "Sun aim / " + sample, null, check);
                    check(!game.totality && !game.MoonAlignmentConfirmed,
                        "Holding the real source Moon never completes before release: " + sample, "");
                    if (sample == "near 1.5m")
                        check(game.CurrentMoonAlignment.Complete && game.CurrentMoonAlignment.EffectiveRadiusRatio > 1.2f,
                            "A near pickup stays visibly large and sufficiently covered Sun is now accepted", "ratio=" + ratio);
                    if (sample == "solvable solar ratio")
                        check(game.CurrentMoonAlignment.Complete,
                            "A mathematically chosen ordinary body position reaches a valid Moon release without size injection",
                            "real/effective=" + game.CurrentMoonAlignment.RealRadiusRatio + "/" + game.CurrentMoonAlignment.EffectiveRadiusRatio);
                    game.AimAt(game.view.transform.position + new Vector3(0, 100, 30));
                    Observe(game, FinaleItem.Moon, ratio, 180, true, "upper sky and global size boundary / " + sample, null, check);
                    if (sample == "near 1.5m")
                        check(game.moon.localScale.x > 47.95f && game.moon.localScale.x <= 48.001f,
                            "The old physical 48m Moon limit now restricts depth while preserving pickup angular size",
                            "scale=" + game.moon.localScale.x + " depth=" + Vector3.Distance(game.view.transform.position, game.moon.position));
                    AimSide(game, -40);
                    Observe(game, FinaleItem.Moon, ratio, 180, true, "left aim / " + sample, null, check);
                    AimSide(game, 40);
                    Observe(game, FinaleItem.Moon, ratio, 180, true, "right aim / " + sample, null, check);
                    if (sample == "far 4m")
                    {
                        Vector3 acceptedPosition = game.moon.position;
                        game.AimAt(game.view.transform.position + new Vector3(0, -10, 3));
                        for (int i = 0; i < 30; i++) game.Tick(Dt, Vector2.zero, false);
                        check(!game.MoonPlacementBlocked && game.MoonFloorConstrained &&
                            Vector3.Distance(game.moon.position, acceptedPosition) > .1f &&
                            game.moon.localScale.x >= .049f,
                            "Downward aim follows the closest floor-safe pose instead of freezing the prior sky pose", Fixture);
                        float floorRatio = game.moon.localScale.x / Vector3.Distance(game.view.transform.position, game.moon.position);
                        check(Mathf.Abs(floorRatio / ratio - 1) < Tolerance,
                            "Floor contact preserves pickup angular size while using the bounded .05m ground exception", "ratio=" + floorRatio);
                        game.AimAt(EclipseFinaleGame.SunPosition);
                        Observe(game, FinaleItem.Moon, ratio, 180, true, "recovery from floor-constrained downward aim", null, check);
                        check(!game.MoonPlacementBlocked, "A normal upward aim recovers the same held Moon after floor contact", "");
                    }
                    game.Cancel();
                    check(game.held == FinaleItem.None && Vector3.Distance(game.moon.position, sourcePosition) < .0001f &&
                        Vector3.Distance(game.moon.localScale, sourceScale) < .0001f && Quaternion.Angle(game.moon.rotation, sourceRotation) < .01f,
                        "Cancel restores the exact source Moon after continuity fixtures: " + sample, "");
                }
                check(Mathf.Abs(game.view.fieldOfView - fov) < .001f, "Moon continuity fixtures preserve the applied FOV", "FOV=" + fov);
            }
            finally
            {
                game.ResetGame(started);
                if (paused) game.Pause(); else if (started) game.Play();
                game.view.fieldOfView = fov;
                game.automated = automated;
            }
        }

        public static void RunExit(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            if (!Allowed(game, check, "EXIT pickup continuity")) return;
            check(game.ExitUnlocked && game.totality && game.Credits.Section == EndingCreditSection.Finished && game.held == FinaleItem.None,
                "EXIT continuity runs only after the actual protected credits have finished", "");
            if (!game.ExitUnlocked || game.held != FinaleItem.None) return;
            Vector3 sourcePosition = game.exitSign.position, sourceScale = game.exitSign.localScale;
            Quaternion sourceRotation = game.exitSign.rotation;
            Vector3 originalFeet = game.body.transform.position;
            Quaternion originalView = game.view.transform.rotation;
            MoonAlignmentSample accepted = game.ReleasedMoonAlignment;
            bool automated = game.automated, started = game.started, paused = game.paused;
            float fov = game.view.fieldOfView;
            var parts = Array.FindAll(game.exitSign.GetComponentsInChildren<BoxCollider>(), part => part.enabled && !part.isTrigger);
            var sizes = Array.ConvertAll(parts, part => part.size);
            var centres = Array.ConvertAll(parts, part => part.center);
            try
            {
                game.automated = true;
                check(Mathf.Abs(sourceScale.x - .72f) < .0001f && parts.Length == 4,
                    "EXIT continuity starts from the normal .72 source sign and original four-panel i passage", "parts=" + parts.Length);
                Vector3 anchor = game.exitSign.TransformPoint(new Vector3(0, .52f, 0));
                float vertical = .04f + EclipseFinaleGame.EyeHeight - anchor.y;
                float horizontal = Mathf.Sqrt(Mathf.Max(.01f, 2.2f * 2.2f - vertical * vertical));
                game.Teleport(new Vector3(anchor.x, .04f, anchor.z - horizontal)); Settle(game);
                game.AimAt(anchor);
                float ratio = game.exitSign.localScale.x / Vector3.Distance(game.view.transform.position, anchor);
                game.Interact();
                check(game.held == FinaleItem.Exit, "EXIT continuity uses the actual close sign pickup after credits", Fixture);
                Observe(game, FinaleItem.Exit, ratio, 1, false, "first EXIT pickup frame", parts, check);
                game.AimAt(game.view.transform.position + Vector3.forward * 50);
                Observe(game, FinaleItem.Exit, ratio, 180, true, "EXIT horizontal/floor support", parts, check);
                game.AimAt(game.view.transform.position + new Vector3(0, 20, 60));
                Observe(game, FinaleItem.Exit, ratio, 180, true, "EXIT slightly upward depth/size boundary", parts, check);
                AimSide(game, -40);
                Observe(game, FinaleItem.Exit, ratio, 180, true, "EXIT left aim", parts, check);
                AimSide(game, 40);
                Observe(game, FinaleItem.Exit, ratio, 180, true, "EXIT right aim", parts, check);
                game.Cancel();
                bool unchanged = game.held == FinaleItem.None && Vector3.Distance(game.exitSign.position, sourcePosition) < .0001f &&
                    Vector3.Distance(game.exitSign.localScale, sourceScale) < .0001f && Quaternion.Angle(game.exitSign.rotation, sourceRotation) < .01f;
                for (int i = 0; i < parts.Length; i++) unchanged &= parts[i].size == sizes[i] && parts[i].center == centres[i] && parts[i].enabled;
                check(unchanged, "Cancel restores the source EXIT pose and unchanged real i doorway geometry", "");
                check(game.ExitUnlocked && game.Credits.Section == EndingCreditSection.Finished && game.MoonAlignmentConfirmed &&
                    game.ReleasedMoonAlignment.ObserverPosition == accepted.ObserverPosition && game.ReleasedMoonAlignment.MoonPosition == accepted.MoonPosition,
                    "EXIT pickup fixtures preserve the actual completed credits and released eclipse snapshot", "");
                check(Mathf.Abs(game.view.fieldOfView - fov) < .001f, "EXIT continuity fixtures preserve the applied FOV", "FOV=" + fov);
            }
            finally
            {
                if (game.held != FinaleItem.None) game.Cancel();
                game.Teleport(originalFeet); game.SetEndingRotation(originalView);
                if (paused) game.Pause(); else if (started) game.Play();
                game.view.fieldOfView = fov;
                game.automated = automated;
            }
        }

        static bool Allowed(EclipseFinaleGame game, Action<bool, string, string> check, string purpose)
        {
            bool allowed = game != null && Application.isBatchMode &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-test") >= 0;
            check(allowed, purpose + " fixtures run only in the explicit isolated batch player", "No captures, OS input or user data changes");
            return allowed;
        }

        static Vector3 MoonFeetAtDistance(EclipseFinaleGame game, float distance)
        {
            float vertical = .04f + EclipseFinaleGame.EyeHeight - game.moon.position.y;
            return new Vector3(game.moon.position.x - Mathf.Sqrt(Mathf.Max(.01f, distance * distance - vertical * vertical)), .04f, game.moon.position.z);
        }
        static void Settle(EclipseFinaleGame game)
        { for (int i = 0; i < 15; i++) game.Tick(Dt, Vector2.zero, false); }
        static void AimSide(EclipseFinaleGame game, float degrees)
        {
            Vector3 forward = (EclipseFinaleGame.SunPosition - game.view.transform.position).normalized;
            game.AimAt(game.view.transform.position + Quaternion.AngleAxis(degrees, Vector3.up) * forward * 100);
        }

        static void Observe(EclipseFinaleGame game, FinaleItem item, float expectedRatio, int frames, bool centred,
            string label, BoxCollider[] parts, Action<bool, string, string> check)
        {
            float maximumError = 0, maximumScale = 0;
            int blocked = 0; bool clear = true;
            for (int i = 0; i < frames; i++)
            {
                game.Tick(Dt, Vector2.zero, false);
                bool isMoon = item == FinaleItem.Moon;
                Transform prop = isMoon ? game.moon : game.exitSign;
                Vector3 anchor = isMoon ? prop.position : prop.TransformPoint(new Vector3(0, .52f, 0));
                float ratio = prop.localScale.x / Vector3.Distance(game.view.transform.position, anchor);
                maximumError = Mathf.Max(maximumError, Mathf.Abs(ratio / expectedRatio - 1));
                maximumScale = Mathf.Max(maximumScale, prop.localScale.x);
                if (isMoon ? game.MoonPlacementBlocked : game.ExitPlacementBlocked) blocked++;
                if (parts != null) clear &= ExitClear(parts);
            }
            check(maximumError <= Tolerance, "Every production hold frame preserves pickup scale/anchor-distance: " + label,
                "frames=" + frames + " maxRelativeError=" + maximumError.ToString("F7") + " maximumScale=" + maximumScale.ToString("F5") + " blocked=" + blocked);
            if (centred)
            {
                Vector3 anchor = item == FinaleItem.Moon ? game.moon.position : game.exitSign.TransformPoint(new Vector3(0, .52f, 0));
                float error = Vector3.Angle(anchor - game.view.transform.position, game.view.transform.forward);
                check(blocked == 0 && error < .15f,
                    "Normal held anchor reaches the view-ray centre after the shared .8s alignment: " + label,
                    "angleDegrees=" + error.ToString("F5") + " blockedFrames=" + blocked);
            }
            if (parts != null) check(clear, "Actual EXIT compound colliders remain clear of support geometry: " + label, "Original i passage; mask8 world support");
        }

        static bool ExitClear(BoxCollider[] parts)
        {
            foreach (var part in parts)
            {
                Vector3 half = Vector3.Scale(part.size, part.transform.lossyScale) * .5f;
                half = Vector3.Max(Vector3.one * .0001f, half - Vector3.one * .001f);
                // Disabled colliders report empty bounds. Measure their real
                // transformed corners while the production carry disables them.
                var bounds = new Bounds(part.transform.TransformPoint(part.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                    bounds.Encapsulate(part.transform.TransformPoint(part.center + Vector3.Scale(part.size * .5f,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                if (bounds.min.y < -.005f || bounds.min.x < -12.505f || bounds.max.x > 12.505f ||
                    bounds.min.z < 1.995f || bounds.max.z > 65.005f ||
                    Physics.CheckBox(part.transform.TransformPoint(part.center), half, part.transform.rotation,
                        EclipseFinaleWorld.MoonSupportMask, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }
    }
}