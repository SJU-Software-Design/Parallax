using UnityEngine;

namespace ShadeLink.Eclipse
{
    public sealed partial class EclipseFinaleGame
    {
        // Explicit administrator entry is kept separate from saved progression.
        // The caller still uses the ordinary SceneStageReady / Play handoff.
        public bool TryStartAdminSection(int section)
        {
            if (!global::ParallaxGameShell.Administrator || !GameProgressRuntime.TestSession ||
                section < 0 || section > 1 || !ProgressReady || Credits == null || ExitPresentation == null)
                return false;

            ResetGame(false);
            // A same-scene administrator selection must also discard any prior
            // credits audio latch. These are the existing reset observations.
            GetComponent<EclipseEndingAmbience>()?.TickAmbience(0);
            GetComponent<EclipseEndingMusic>()?.TickMusic(0);
            if (section == 0) return true;

            if (!PrepareAdministratorEclipse() || !Credits.TryFinishAdministratorSection())
            {
                ResetGame(false);
                return false;
            }
            Pause();
            Physics.SyncTransforms();
            return true;
        }

        bool PrepareAdministratorEclipse()
        {
            // This declared sky pose uses the unchanged Moon mesh, rotation and
            // material. Its measured release goes through the same alignment
            // evaluator as a real Drop; the entry does not forge a snapshot.
            moon.localScale = Vector3.one * 12f;
            Vector3 eye = view.transform.position, solarRay = SunPosition - eye;
            float solarAngle = MoonAlignmentRules.SolarAngularRadius(eye, SunPosition, SunRadius);
            if (solarAngle <= 0) return false;
            float depth = MoonRadius * MoonAlignmentRules.EffectiveSilhouetteFactor / Mathf.Sin(solarAngle * 1.08f);
            moon.position = eye + solarRay.normalized * depth;
            AimAt(SunPosition);
            Physics.SyncTransforms();
            MoonAnchored = CanAnchorMoon();
            if (!MoonAnchored || !MoonAlignmentConfirmed) return false;

            // This is the administrator's completed presentation state, not an
            // accelerated tick of the protected normal credits sequence.
            anchoredCoverage = 1; settle = .35f; darkness = 1;
            TotalityStableTime = 1.8f;
            UpdateEclipse(0);
            return totality && coverage == 1 && darkness == 1;
        }
    }

    public sealed partial class EclipseEndingCredits
    {
        // Only the explicit administrator entry may bypass the roll. Begin()
        // measures the real Moon envelope and reuses the normal route-safe
        // return pose; ordinary Team / Thanks input gates remain unchanged.
        public bool TryFinishAdministratorSection()
        {
            if (!global::ParallaxGameShell.Administrator || !GameProgressRuntime.TestSession ||
                game == null || !game.ProgressReady || game.held != FinaleItem.None ||
                !game.MoonAlignmentConfirmed || !game.totality || game.darkness < .985f ||
                Section != EndingCreditSection.Inactive || !Begin())
                return false;

            TeamComplete = ThanksComplete = true;
            returnRotation = Quaternion.LookRotation(game.exitSign.TransformPoint(new Vector3(0, .55f, 0)) - LockedCameraPosition);
            game.world.SetExitRouteReturnProgress(1);
            Section = EndingCreditSection.Finished; HideRows();
            game.world.SetSunPresentationPosition(LockedSunPosition);
            game.view.transform.position = LockedCameraPosition;
            game.view.fieldOfView = LockedFieldOfView;
            game.moon.SetPositionAndRotation(GameplayMoonPosition, GameplayMoonRotation);
            game.moon.localScale = GameplayMoonScale;
            CameraLiftProgress = 0;
            game.SetEndingRotation(returnRotation);
            game.UpdateEclipse(0);
            game.CompleteCredits();
            return true;
        }
    }
}