using System;
using System.Collections;
using UnityEngine;

namespace ShadeLink.Tutorial
{
    // Invoked only by the integrated, isolated native validator. No capture,
    // input synthesis, direct exposure assignment or old tutorial test driver.
    public static class TutorialSunlightGaugeValidation
    {
        static string Detail(TutorialGame game)
        {
            var gauge = game.GetSunlightExposureGauge(1600, 900);
            return "visible=" + gauge.Visible + " safe=" + gauge.Safe + " exposure=" + gauge.Exposure.ToString("F4") +
                " feet=" + game.Controller.transform.position + " fill=" + gauge.Fill.width.ToString("F2");
        }
        static void Move(TutorialGame game, Vector3 feet)
        {
            game.Controller.enabled = false;
            game.Controller.transform.position = feet;
            game.Controller.enabled = true;
            Physics.SyncTransforms();
        }
        static bool Inside(Rect outer, Rect inner) => inner.xMin >= outer.xMin - .01f && inner.yMin >= outer.yMin - .01f &&
            inner.xMax <= outer.xMax + .01f && inner.yMax <= outer.yMax + .01f;

        public static IEnumerator Run(TutorialGame game, Action<bool, string, string> check)
        {
            bool ready = game != null && game.ProgressReady && game.Controller != null;
            check(ready, "Tutorial sunlight gauge has a live initialized stage", "game=" + (game != null));
            if (!ready) yield break;
            var original = game.CaptureProgress();
            bool wasPaused = game.ProgressPaused;
            try
            {
                game.RestoreProgress(new GameProgressRecord { stage = "Tutorial", tutorialStage = 1 });
                game.SetProgressPaused(false);
                yield return null;
                check(!game.GetSunlightExposureGauge(1600, 900).Visible,
                    "Tutorial sunlight gauge stays out of lesson 1", Detail(game));
                game.RestoreProgress(new GameProgressRecord { stage = "Tutorial", tutorialStage = 2 });
                game.SetProgressPaused(false);
                yield return null;
                check(!game.GetSunlightExposureGauge(1600, 900).Visible,
                    "Tutorial sunlight gauge stays out of lesson 2", Detail(game));

                game.RestoreProgress(new GameProgressRecord { stage = "Tutorial", tutorialStage = 3 });
                game.SetProgressPaused(false);
                yield return null;
                var courtyard = game.transform.Find("03 · Sunlight courtyard draft");
                check(courtyard != null, "Tutorial sunlight fixture uses the actual courtyard", "found=" + (courtyard != null));
                if (courtyard == null) yield break;
                Vector3 shelter = courtyard.position + new Vector3(2.2f, .03f, 9.5f);
                Vector3 exposed = courtyard.position + new Vector3(10f, .03f, 9.5f);
                var initial = game.GetSunlightExposureGauge(1600, 900);
                check(initial.Visible && initial.Safe && initial.Exposure <= .001f && initial.Fill.width <= .001f,
                    "Tutorial shelter shows a visible empty safe gauge", Detail(game));

                bool geometry = true, textScaling = true;
                foreach (Vector2 size in new[] { new Vector2(320, 180), new Vector2(640, 360), new Vector2(1600, 900), new Vector2(2560, 1440) })
                {
                    int previousSize = 0;
                    foreach (float textScale in new[] { .9f, 1f, 1.2f })
                    {
                        var layout = game.GetSunlightExposureGauge(size.x, size.y, textScale);
                        geometry &= Inside(new Rect(0, 0, size.x, size.y), layout.Panel) && Inside(layout.Panel, layout.Label) &&
                            Inside(layout.Panel, layout.Track) && Inside(layout.Track, layout.Fill) && layout.Track.width > 0;
                        textScaling &= layout.FontSize >= previousSize;
                        previousSize = layout.FontSize;
                    }
                    textScaling &= game.GetSunlightExposureGauge(size.x, size.y, 1.2f).FontSize >
                        game.GetSunlightExposureGauge(size.x, size.y, .9f).FontSize;
                }
                check(geometry, "Tutorial gauge layout and fill stay inside four screen sizes", "320x180/640x360/1600x900/2560x1440 x text .9/1/1.2");
                check(textScaling && game.GetSunlightExposureGauge(1600, 900).FontSize ==
                    game.GetSunlightExposureGauge(1600, 900, global::GameShellSettingsRuntime.TextScale).FontSize,
                    "Tutorial gauge follows the applied text-size setting", "textScale=" + global::GameShellSettingsRuntime.TextScale);

                Move(game, exposed);
                float deadline = Time.realtimeSinceStartup + 4;
                while (game.GetSunlightExposureGauge(1600, 900).Exposure < .25f && Time.realtimeSinceStartup < deadline) yield return null;
                var danger = game.GetSunlightExposureGauge(1600, 900);
                check(danger.Visible && !danger.Safe && danger.Exposure >= .2f && danger.Exposure < .8f,
                    "Actual tutorial sunlight ticks increase the displayed gauge", Detail(game));
                check(danger.Exposure > 0 && Mathf.Abs(danger.Fill.width / danger.Track.width - danger.Exposure) < .00001f &&
                    danger.Status == "빛 노출 · 그늘로 이동",
                    "Tutorial hazard fill and status use the live exposure", Detail(game));

                game.SetProgressPaused(true);
                float pausedExposure = game.GetSunlightExposureGauge(1600, 900).Exposure;
                yield return new WaitForSecondsRealtime(.18f);
                check(Mathf.Abs(game.GetSunlightExposureGauge(1600, 900).Exposure - pausedExposure) < .00001f,
                    "Pausing tutorial freezes exposure and the gauge", Detail(game));
                game.SetProgressPaused(false);

                Move(game, shelter);
                float beforeRecovery = game.GetSunlightExposureGauge(1600, 900).Exposure;
                yield return null;
                var recovering = game.GetSunlightExposureGauge(1600, 900);
                check(recovering.Safe && recovering.Exposure < beforeRecovery && recovering.Exposure > 0,
                    "Actual shelter ticks drain the same gauge", Detail(game));
                deadline = Time.realtimeSinceStartup + 3;
                while (game.GetSunlightExposureGauge(1600, 900).Exposure > .001f && Time.realtimeSinceStartup < deadline) yield return null;
                var recovered = game.GetSunlightExposureGauge(1600, 900);
                check(recovered.Visible && recovered.Safe && recovered.Exposure <= .001f && recovered.Fill.width <= .001f,
                    "Tutorial shelter recovery returns the bar to empty", Detail(game));

                Move(game, exposed);
                bool sawHighExposure = false, respawned = false;
                deadline = Time.realtimeSinceStartup + 6;
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    var sample = game.GetSunlightExposureGauge(1600, 900);
                    sawHighExposure |= sample.Exposure > .7f;
                    if (sawHighExposure && sample.Exposure <= .001f && Vector3.Distance(game.Controller.transform.position, shelter) < .25f)
                    { respawned = true; break; }
                }
                check(sawHighExposure && respawned && game.GetSunlightExposureGauge(1600, 900).Safe,
                    "Full actual exposure respawns at the original shelter and clears the gauge", Detail(game));
                game.RestartProgressCheckpoint();
                check(game.GetSunlightExposureGauge(1600, 900).Visible && game.GetSunlightExposureGauge(1600, 900).Exposure <= .001f,
                    "Existing tutorial checkpoint restart retains an empty sunlight gauge", Detail(game));
            }
            finally
            {
                if (game != null && game.ProgressReady)
                {
                    game.RestoreProgress(original);
                    game.SetProgressPaused(wasPaused);
                }
            }
        }
    }
}
