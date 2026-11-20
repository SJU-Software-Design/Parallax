using System;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Every positive case uses the original Moon and production grab/hold/drop.
    // Only declared feet/view fixtures change; no shape or success is injected.
    public static class EclipseAlignmentValidation
    {
        const float Dt = 1f / 60;

        public static void Run(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            bool allowed = game != null && Application.isBatchMode &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-test") >= 0;
            check(allowed, "Comfort eclipse fixtures run only in the explicit isolated batch player", "No captures or OS input");
            if (!allowed) return;
            bool automated = game.automated, started = game.started, paused = game.paused;
            float configuredFov = game.view.fieldOfView;
            try
            {
                MoonAlignmentSample giant = Accept(game, check, "large ordinary pickup", 1.75f, 0);
                check(giant.EffectiveRadiusRatio > 1.5f && giant.Complete,
                    "A large Moon covering the Sun is accepted without an angular-size ceiling", Describe(giant));

                MoonAlignmentSample annular = Accept(game, check, "visible thin solar ring", .93f, 0);
                check(annular.Coverage >= MoonAlignmentRules.RequiredCoverage && annular.Coverage < .975f &&
                    annular.EffectiveRadiusRatio < 1 && annular.Complete,
                    "A real annular release retains its bright rim and qualifies without near-total coverage", Describe(annular));
                check(game.darkness > annular.Coverage + .02f && game.darkness > .985f &&
                    Mathf.Abs(game.ReleasedMoonAlignment.Coverage - annular.Coverage) < .00001f,
                    "Accepted annular coverage eases presentation to full darkness while its measured snapshot stays unchanged",
                    "measured=" + annular.Coverage + " presentation=" + game.darkness);

                MoonAlignmentSample offset = Accept(game, check, "ordinary slightly offset release", 1.10f, .4f);
                check(offset.CentreErrorDegrees > offset.SolarRadiusDegrees * .20f && offset.Complete,
                    "Actual sufficient overlap succeeds without the old precise centre gate", Describe(offset));

                MoonAlignmentSample turned = Accept(game, check, "look-away release of the already covering Moon", 1.10f, 0, true);
                check(turned.ViewErrorDegrees > turned.SolarRadiusDegrees * .60f && turned.Complete,
                    "Looking aside before release does not reject a Moon that still genuinely covers the Sun", Describe(turned));

                Reject(game, check, "visibly too small", .55f, 0);
                Reject(game, check, "no solar overlap", 1.10f, 5);

                MoonAlignmentSample accepted = Accept(game, check, "normal completed eclipse", 1.10f, 0);
                Advance(game, 30);
                game.AimAt(EclipseFinaleGame.SunPosition + Vector3.right * 300);
                for (int i = 0; i < 20; i++) game.Tick(Dt, Vector2.right * .5f, false);
                check(game.MoonAlignmentConfirmed && SameSnapshot(accepted, game.ReleasedMoonAlignment),
                    "Walking and looking away after release preserve the actual accepted observation snapshot", "");
                game.AimAt(game.moon.position); game.Interact();
                check(game.held == FinaleItem.Moon && !game.MoonAlignmentConfirmed,
                    "Regrabbing the original Moon suspends its previous release confirmation", "");
                game.Cancel();
                check(game.MoonAnchored && game.MoonAlignmentConfirmed && SameSnapshot(accepted, game.ReleasedMoonAlignment),
                    "Cancel restores the accepted Moon pose and original measured snapshot together", "");
                for (int i = 0; i < 600 && !game.EndingSequenceActive; i++) game.Tick(Dt, Vector2.zero, false);
                check(game.totality && game.Credits.Active && game.MoonAlignmentConfirmed,
                    "A legitimate relaxed eclipse still reaches the ordinary protected credit sequence", "");
                for (int i = 0; i < Mathf.CeilToInt((EclipseEndingCredits.IntroDuration + .1f) / .05f); i++)
                    game.TickEndingSequence(.05f, false, false);
                check(game.MoonAlignmentConfirmed && SameSnapshot(accepted, game.ReleasedMoonAlignment),
                    "The cinematic camera and Sun presentation never rewrite measured release coverage", "");
                game.ResetGame(true);
                check(!game.MoonAlignmentConfirmed && !game.HasMoonReleaseSnapshot,
                    "Restart clears the relaxed release snapshot and restores the original small Moon", "");
                check(Mathf.Abs(game.view.fieldOfView - configuredFov) < .001f,
                    "Comfort alignment, cancellation and cinematic fixtures preserve configured FOV", "FOV=" + configuredFov);
            }
            finally
            {
                game.ResetGame(started);
                if (paused) game.Pause(); else if (started) game.Play();
                game.view.fieldOfView = configuredFov;
                game.automated = automated;
            }
        }

        static MoonAlignmentSample Accept(EclipseFinaleGame game, Action<bool, string, string> check,
            string purpose, float radiusRatio, float offsetFraction, bool lookAway = false)
        {
            PrepareHeld(game, check, purpose, radiusRatio);
            float pickupRatio = game.moon.localScale.x / Vector3.Distance(game.view.transform.position, game.moon.position);
            AimOnSun(game, offsetFraction); Advance(game, 180);
            float actualRatio = game.moon.localScale.x / Vector3.Distance(game.view.transform.position, game.moon.position);
            if (lookAway) game.AimAt(EclipseFinaleGame.SunPosition + Vector3.right * 200);
            MoonAlignmentSample observed = game.CurrentMoonAlignment;
            check(Mathf.Abs(actualRatio / pickupRatio - 1) <= .005f && observed.Complete &&
                observed.Coverage >= MoonAlignmentRules.RequiredCoverage && !game.totality && !game.MoonAlignmentConfirmed,
                "Actual held perspective size and sufficient overlap reach a release candidate: " + purpose,
                Describe(observed) + " pickup/held=" + pickupRatio + "/" + actualRatio);
            check(game.Drop() && game.MoonAnchored && game.MoonAlignmentConfirmed &&
                game.ReleasedMoonAlignment.Complete && SameSnapshot(observed, game.ReleasedMoonAlignment),
                "Production grab, aim and release accept the covering Moon without success injection: " + purpose,
                Describe(game.ReleasedMoonAlignment));
            MoonAlignmentSample accepted = game.ReleasedMoonAlignment;
            Advance(game, 45);
            check(game.totality && !game.EndingSequenceActive && game.darkness > .985f && SameSnapshot(accepted, game.ReleasedMoonAlignment),
                "Accepted coverage settles into full-darkness totality before the protected credits: " + purpose,
                "actualCoverage=" + accepted.Coverage + " darkness=" + game.darkness);
            return accepted;
        }

        static void Reject(EclipseFinaleGame game, Action<bool, string, string> check,
            string purpose, float radiusRatio, float offsetFraction)
        {
            PrepareHeld(game, check, purpose, radiusRatio);
            AimOnSun(game, offsetFraction); Advance(game, 180);
            MoonAlignmentSample observed = game.CurrentMoonAlignment;
            check(observed.Coverage < MoonAlignmentRules.RequiredCoverage && !observed.Complete &&
                game.MoonAlignmentHint == "태양을 조금 더 가려 보세요.",
                "An insufficient actual overlap keeps only a neutral guidance message: " + purpose, Describe(observed));
            check(game.Drop() && !game.MoonAnchored && !game.MoonAlignmentConfirmed && !game.ReleasedMoonAlignment.Complete,
                "Too little actual solar coverage cannot confirm the eclipse: " + purpose, Describe(game.ReleasedMoonAlignment));
            Advance(game, 45);
            check(!game.totality && !game.EndingSequenceActive,
                "An insufficient release cannot begin the ending after settle time: " + purpose, "");
        }

        static void PrepareHeld(EclipseFinaleGame game, Action<bool, string, string> check, string purpose, float radiusRatio)
        {
            game.ResetGame(true); game.automated = true;
            game.Teleport(EclipsePickupContinuityValidation.SolvableMoonPickupFeet(game, radiusRatio));
            for (int i = 0; i < 15; i++) game.Tick(Dt, Vector2.zero, false);
            check(game.InShelter, "Declared pickup feet remain in the real broad starting shadow: " + purpose, game.body.transform.position.ToString("F4"));
            game.AimAt(game.moon.position); game.Interact();
            check(game.held == FinaleItem.Moon, "Alignment uses the real original-Moon pickup: " + purpose,
                "Declared left-side feet/view fixture only; no prop pose, size or completion injection");
        }
        static void AimOnSun(EclipseFinaleGame game, float offsetFraction)
        {
            Vector3 eye = game.view.transform.position, sun = EclipseFinaleGame.SunPosition;
            float degrees = MoonAlignmentRules.SolarAngularRadius(eye, sun, EclipseFinaleGame.SunRadius) * Mathf.Rad2Deg;
            game.AimAt(eye + Quaternion.AngleAxis(degrees * offsetFraction, Vector3.up) * (sun - eye));
        }
        static void Advance(EclipseFinaleGame game, int frames)
        { for (int i = 0; i < frames; i++) { game.Tick(Dt, Vector2.zero, false); Physics.SyncTransforms(); } }
        static bool SameSnapshot(MoonAlignmentSample a, MoonAlignmentSample b)
        {
            return a.Complete == b.Complete && a.CanAnchor == b.CanAnchor && a.EffectiveRadiusRatio == b.EffectiveRadiusRatio &&
                a.Coverage == b.Coverage && a.ObserverPosition == b.ObserverPosition && a.SolarPosition == b.SolarPosition && a.MoonPosition == b.MoonPosition;
        }
        static string Describe(MoonAlignmentSample sample)
        {
            return "coverage=" + sample.Coverage.ToString("F5") + " real/effectiveRatio=" + sample.RealRadiusRatio.ToString("F4") + "/" +
                sample.EffectiveRadiusRatio.ToString("F4") + " centre/solar=" + sample.CentreErrorDegrees.ToString("F4") + "/" +
                sample.SolarRadiusDegrees.ToString("F4") + " view=" + sample.ViewErrorDegrees.ToString("F4") + " result=" + sample.Failure;
        }
    }
}