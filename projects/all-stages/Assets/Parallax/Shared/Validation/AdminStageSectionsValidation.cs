using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;
using ShadeLink.Beach;
using ShadeLink.Eclipse;

namespace ShadeLink
{
    // Called by the isolated administrator runner after actual AdminTravel and
    // its normal reveal/resume. No renderer readback, OS input or success setter.
    public static class AdminStageSectionsValidation
    {
        public static void VerifyCatalog(Action<bool, string, string> check)
        {
            string[] scenes = { "Tutorial", "Gallery", "Beach", "Eclipse" };
            int[] counts = { 3, 5, 4, 2 };
            for (int map = 0; map < scenes.Length; map++)
            {
                string scene = scenes[map]; var sections = AdminStageSections.ForStage(scene);
                check(sections.Count == counts[map] && !string.IsNullOrEmpty(AdminStageSections.StageLabel(scene)),
                    "The section catalog follows the actual authored " + scene + " puzzle count", sections.Count.ToString());
                for (int index = 0; index < sections.Count; index++)
                {
                    var section = sections[index]; var record = AdminStageSections.CreateRecord(scene, index);
                    bool valid = scene == "Eclipse" ? record == null : GameProgress.Validate(record, out _);
                    check(section.Index == index && section.Scene == scene && !string.IsNullOrEmpty(section.Label) && valid,
                        "Catalog entry owns its valid stage/section restoration contract", scene + ":" + index + " " + section.Label);
                }
                check(!AdminStageSections.IsValid(scene, -1) && !AdminStageSections.IsValid(scene, sections.Count),
                    "Out-of-range administrator sections are rejected before loading", scene);
            }
            check(AdminStageSections.ForStage("Unknown").Count == 0 && !AdminStageSections.IsValid("Unknown", 0),
                "Unknown stage names expose no selectable section", "");
            bool throws = false;
            try { AdminStageSections.CreateRecord("Gallery", 5); } catch (ArgumentOutOfRangeException) { throws = true; }
            check(throws, "Creating a nonexistent section cannot silently restore another puzzle", "Gallery:5");
        }

        public static void InspectCurrent(string scene, int index, IGameProgressStage stage, Action<bool, string, string> check)
        {
            check(ParallaxGameShell.Administrator && GameProgressRuntime.TestSession && AdminStageSections.IsValid(scene, index),
                "Section inspection follows an authorized administrator test-session load", scene + ":" + index);
            check(SceneManager.GetActiveScene().name == scene && stage != null && stage.ProgressReady && !stage.ProgressPaused && !stage.ProgressTransitioning,
                "The actual selected scene has completed loading and resumed its controller", scene);
            CharacterController body; Camera view;
            if (stage is TutorialGame tutorial)
            {
                body = tutorial.Controller; view = tutorial.Camera;
                var record = tutorial.CaptureProgress();
                check(record.tutorialStage == index + 1 && record.tutorialStep == 0 && record.checkpoint == 0 &&
                    tutorial.Props != null && tutorial.Props.Length == index + 1 && tutorial.Held == null,
                    "Tutorial restoration selects its actual size, platform or sunlight room with fresh props",
                    "stage=" + record.tutorialStage + ", props=" + tutorial.Props?.Length);
            }
            else if (stage is GalleryGame gallery)
            {
                body = gallery.controller; view = gallery.view;
                var record = gallery.CaptureProgress(); var expected = AdminStageSections.CreateRecord(scene, index);
                check(gallery.bottleBalance != null && record.galleryRoom == index && record.checkpoint == expected.checkpoint && gallery.held == null,
                    "Gallery restoration places the actor inside the selected real exhibition", "room=" + record.galleryRoom + ", feet=" + gallery.player.feet);
                check(gallery.shadowCourse != null && gallery.shadowCourse.TutorialCleared == (index >= 1) &&
                    gallery.shadowCourse.NikeUnlocked == (index >= 2) && gallery.shadowCourse.NikeGateOpen == (index >= 2),
                    "Only preceding bronze and Nike gates are complete", "section=" + index);
                check(gallery.bottleBalance.Solved == (index >= 3) && gallery.bottleBalance.Reward.Unlocked == (index >= 3) &&
                    (gallery.bottleBalance.Door.Opening >= .995f) == (index >= 3),
                    "The balance/sword puzzle is fresh until the following portrait room", "section=" + index);
                check(gallery.portraitGate != null && gallery.portraitGate.Opened == (index >= 4) &&
                    gallery.orcusExit != null && !gallery.orcusExit.Entering && !gallery.orcusExit.IsReadyForEntry,
                    "The portrait gate reflects preceding progress while the enlarged-hand puzzle stays fresh", "section=" + index);
                Vector3 spawn = index == 2 ? gallery.bottleBalance.DemoPosition : index == 4 ?
                    GalleryBottleBalanceCourse.ShiftLegacy(new Vector3(0, 0, 49.1f)) : gallery.SpawnPoints[expected.checkpoint];
                check(Horizontal(body.transform.position, spawn) < .08f && gallery.CanStand(gallery.player.feet),
                    "The chosen Gallery section uses its actual clear authored approach", "expected=" + spawn + ", actual=" + body.transform.position);
                if (index == 2)
                    check(gallery.bottleBalance.InRoom && !gallery.bottleBalance.Solved,
                        "The inserted balance section starts in its own room rather than Nike's shared checkpoint", "");
            }
            else if (stage is BeachGame beach)
            {
                body = beach.controller; view = beach.view;
                check(beach.checkpoint == index && beach.held == null && !beach.won,
                    "Beach restoration selects the real current puzzle without winning it", "checkpoint=" + beach.checkpoint);
                check(beach.lightGate != null && beach.lightGate.unlocked == (index >= 1) && beach.waterGate != null && beach.waterGate.unlocked == (index >= 3),
                    "Beach gates open only for preceding sections", "section=" + index);
                check(beach.shadowHarbor != null && beach.shadowHarbor.Solved == (index >= 3) && beach.boat != null && beach.boat.Arrived == (index >= 3),
                    "The shadow harbor and boat restore prior travel only at the final section", "section=" + index);
                check(beach.counterweightFinale != null && !beach.counterweightFinale.Ready && !beach.counterweightFinale.LoadLatched,
                    "The actual counterweight finale remains a fresh playable puzzle", "");
                Vector3 spawn = index == 3 ? beach.counterweightFinale.ObservationPoint : BeachWorld.Checkpoints[index];
                check(Horizontal(body.transform.position, spawn) < .08f && beach.CanStand(beach.player.feet),
                    "The chosen Beach section uses its real clear checkpoint or observer", "expected=" + spawn + ", actual=" + body.transform.position);
            }
            else if (stage is EclipseFinaleGame eclipse)
            {
                body = eclipse.body; view = eclipse.view;
                check(eclipse.InputEnabled, "The Eclipse administrator section resumes normal player input", "section=" + index);
            }
            else throw new ArgumentException("Unsupported stage controller");
            check(body != null && body.enabled && view != null && view.enabled &&
                Vector3.Distance(view.transform.position, body.transform.position + Vector3.up * 1.55f) < .12f,
                "The actual section owns an enabled capsule and synchronized first-person camera", scene);
            bool supported = Physics.RaycastAll(body.transform.position + Vector3.up * .35f, Vector3.down, 1,
                ~0, QueryTriggerInteraction.Ignore).Any(hit => hit.collider != body && hit.normal.y > .5f &&
                    hit.point.y <= body.transform.position.y + .1f && !hit.collider.transform.IsChildOf(body.transform));
            check(supported, "The real section capsule starts above physical floor support", body.transform.position.ToString("F3"));
        }

        // Optional short collision probe. It exercises actual controller sweeps,
        // rather than pretending Tutorial exposes a deterministic motor Tick.
        // The disclosed actor pose is restored before returning to the caller.
        public static void ExerciseController(IGameProgressStage stage, Action<bool, string, string> check)
        {
            CharacterController body = stage is TutorialGame tutorial ? tutorial.Controller : stage is GalleryGame gallery ? gallery.controller :
                stage is BeachGame beach ? beach.controller : stage is EclipseFinaleGame eclipse ? eclipse.body : null;
            if (body == null || !body.enabled) throw new InvalidOperationException("Missing active section capsule");
            Vector3 start = body.transform.position; float moved = 0;
            try
            {
                foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    body.Move(direction * .18f); moved = Mathf.Max(moved, Horizontal(start, body.transform.position));
                    body.Move(start - body.transform.position);
                    if (moved > .08f) break;
                }
                check(moved > .08f, "The actual section capsule accepts a short physical sweep", moved.ToString("F3") + "m; explicit collision probe, not continuous gameplay");
            }
            finally
            {
                body.enabled = false; body.transform.position = start; body.enabled = true; Physics.SyncTransforms();
            }
        }
        static float Horizontal(Vector3 a, Vector3 b) => new Vector2(a.x-b.x, a.z-b.z).magnitude;
    }
}