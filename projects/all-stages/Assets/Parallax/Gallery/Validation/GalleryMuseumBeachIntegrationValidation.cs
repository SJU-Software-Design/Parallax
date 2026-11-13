using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Beach;

namespace ShadeLink.Gallery
{
    // Focused actual-physics/scene-handoff QA, with explicitly disclosed poses.
    // Does not claim continuous puzzle solving or subjective sound approval.
    public sealed class GalleryMuseumBeachIntegrationValidation : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed, automaticPhysics, benchBlocksController, pedestalBlocksController, newStatueSurfaceBlocksController;
            public bool firstBottleFallAudible, rapidBottleBouncesSuppressed, separatedStrikeAudible, freshHoldDropAudible;
            public bool beachPreviewAsset, previewHasCoastalGeometry, noLibraryPreview, livePortalTexture, portalParallax;
            public bool handsOpenedNaturally, actualControllerCrossing, beachLoaded, beachFirstSpawn, beachControlsReady, beachCanMove, beachCameraRenderVisible;
            public float benchTravel, pedestalTravel, bottleMinimumRepeatInterval, previewLuminanceDeviation, previewParallaxDifference;
            public float beachCameraMeanLuminance, beachCameraLuminanceDeviation;
            public int bottleFirstCount, bottleRapidCount, bottleSeparatedCount, bottleFreshCount, previewRenderers, previewRenderCount, fixedSteps;
            public string phase, failure, physicsMode, unity, gpu, finalScene;
            public string arrivalCaptureMethod = "Actual BeachGame.view.Camera.Render into a temporary 1600x900 sRGB RenderTexture, preserving the camera and its existing effects. Captured only after MUSEUM_BEACH_HANDOFF_OK, transition-runner destruction, production movement and three warm-up frames. Production fade, lighting, exposure and materials are unchanged. The hidden-window ScreenCapture backbuffer method used by v3 produced a black PNG and is not treated as verified arrival imagery.";
            public Vector3 beachSpawn, beachAfterMove;
            public GalleryStaticExhibitCollisionValidation.Result staticCollisions;
            public string[] errors, checks, previewNames, bottleEvents;
            public string scope = "Actual saved museum and automatic FixedUpdate PhysX. Explicit poses test the visitor controller against a bench and P02, release the actual bottle above the actual floor, and add five small physical bounce impulses. Public BeginHold/Release isolate a fresh impact episode. A disclosed three-times hands placement uses production placement/opening; a real CharacterController crossing invokes production TryEnter and actual asynchronous Beach scene load. Portal render-texture pixels and view-dependent camera motion are inspected. This is focused integration QA, not continuous human play, complete Dike puzzle solving, or subjective sound/art approval.";
        }
        [Serializable] sealed class PreviewRecord
        { public string sourceScene; public int sourceRenderers, meshGroups; public string[] originalMaterialPaths; }
        public GalleryGame game;
        readonly Report report = new Report();
        readonly List<string> errors = new List<string>(), checks = new List<string>();
        string output; double deadline; int ticks; bool drive, handoffLogged;
        Vector2 move;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-museum-beach-integration-test") < 0 ||
                UnityEngine.Object.FindFirstObjectByType<GalleryMuseumBeachIntegrationValidation>() != null) return;
            var root = new GameObject("Museum and beach integration validation");
            DontDestroyOnLoad(root); root.AddComponent<GalleryMuseumBeachIntegrationValidation>();
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject); Application.logMessageReceived += Log;
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-validationDir");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) :
                Path.Combine(Application.persistentDataPath, "MuseumBeachIntegrationValidation");
            Directory.CreateDirectory(output); deadline = Time.realtimeSinceStartupAsDouble + 75;
            report.unity = Application.unityVersion; report.gpu = SystemInfo.graphicsDeviceName;
            report.physicsMode = Physics.simulationMode.ToString();
            IEnumerator run = Sequence();
            while (true)
            {
                bool more; object next = null;
                try { Require(Time.realtimeSinceStartupAsDouble < deadline, "Validation exceeded 75 seconds."); more = run.MoveNext(); if (more) next = run.Current; }
                catch (Exception exception) { report.failure = exception.ToString(); break; }
                if (!more) break; yield return next;
            }
            drive = false; report.fixedSteps = ticks; report.finalScene = SceneManager.GetActiveScene().name;
            report.errors = errors.ToArray(); report.checks = checks.ToArray();
            report.passed = report.failure == null && errors.Count == 0 && report.automaticPhysics &&
                report.staticCollisions != null && report.staticCollisions.passed && report.benchBlocksController && report.pedestalBlocksController && report.newStatueSurfaceBlocksController &&
                report.firstBottleFallAudible && report.rapidBottleBouncesSuppressed && report.separatedStrikeAudible && report.freshHoldDropAudible &&
                report.beachPreviewAsset && report.previewHasCoastalGeometry && report.noLibraryPreview && report.livePortalTexture && report.portalParallax &&
                report.handsOpenedNaturally && report.actualControllerCrossing && report.beachLoaded && report.beachFirstSpawn && report.beachControlsReady && report.beachCanMove && report.beachCameraRenderVisible;
            File.WriteAllText(Path.Combine(output, "museum-beach-integration-results.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= Log;
            if (report.passed) Debug.Log("MUSEUM_BEACH_INTEGRATION_VALIDATION_OK checks=" + checks.Count);
            else Debug.LogError("MUSEUM_BEACH_INTEGRATION_VALIDATION_FAIL " + report.phase + " " + report.failure);
            Application.Quit(report.passed ? 0 : 1);
        }

        void FixedUpdate()
        {
            if (!drive || game == null || !game.active || game.won || game.Transitioning) return;
            game.Tick(Time.fixedDeltaTime, move, false); ticks++;
        }
        void Log(string message, string stack, LogType type)
        {
            if (message.Contains("MUSEUM_BEACH_HANDOFF_OK")) handoffLogged = true;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
        }

        IEnumerator Sequence()
        {
            yield return null;
            game = UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            Require(game != null && game.conceptLayout, "Actual concept museum is required.");
            game.automated = true; game.conceptCapture = true; game.exhibitionPreview = true;
            Application.runInBackground = true; Application.targetFrameRate = 90; QualitySettings.vSyncCount = 0;
            game.ResetAll(); game.Resume(); drive = true;
            report.automaticPhysics = Physics.simulationMode == SimulationMode.FixedUpdate;
            Require(report.automaticPhysics, "This check requires automatic FixedUpdate physics.");
            var course = game.GetComponent<GalleryBottleBalanceCourse>();
            Require(course != null && course.Sounds != null, "Actual bottle/balance room and selected sound routing are required.");
            yield return null; yield return new WaitForFixedUpdate();
            report.phase = "static surfaces and actual visitor obstruction";
            report.staticCollisions = GalleryStaticExhibitCollisionValidation.Inspect(game);
            Require(report.staticCollisions.passed, "Static collision inspection failed: " + string.Join("; ", report.staticCollisions.failures));
            game.yaw = 0; game.PlacePlayer(new Vector3(-6.5f, .01f, 7));
            Vector3 before = game.player.feet; move = Vector2.right;
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            move = Vector2.zero; report.benchTravel = game.player.feet.x - before.x;
            report.benchBlocksController = report.benchTravel > .25f && game.player.feet.x < -5.30f;
            Require(report.benchBlocksController, "The actual visitor walked through the existing walnut bench.");
            Vector3 receiver = GalleryDikeGalleryLayout.ReceiverStation;
            game.PlacePlayer(receiver + new Vector3(-1.7f, .01f, 0)); before = game.player.feet; move = Vector2.right;
            for (int i = 0; i < 38; i++) yield return new WaitForFixedUpdate();
            move = Vector2.zero; report.pedestalTravel = game.player.feet.x - before.x;
            report.pedestalBlocksController = report.pedestalTravel > .20f && game.player.feet.x < receiver.x - .35f;
            Require(report.pedestalBlocksController, "The actual visitor walked through the P02 receiver.");
            var installer = game.GetComponent<GalleryStaticExhibitCollision>();
            MeshCollider newBody = installer.Added.Find(shape => shape.name == "Static collision · Dike body excluding sword clearance");
            Require(newBody != null, "The new exact statue clothing collider is missing.");
            var witness = game.controller.gameObject.AddComponent<GalleryMuseumIntegrationCollisionWitness>(); witness.expected = newBody;
            foreach (Vector3 outward in new[] { Vector3.back, Vector3.right, Vector3.left, Vector3.forward })
            {
                Vector3 center = newBody.bounds.center; center.y = game.transform.position.y + 1.0f;
                if (!newBody.Raycast(new Ray(center + outward * 4, -outward), out RaycastHit surface, 8)) continue;
                Vector3 feet = surface.point + outward * 1.0f; feet.y = game.transform.position.y + .03f;
                game.PlacePlayer(feet); move = new Vector2(-outward.x, -outward.z);
                for (int i = 0; i < 42 && !witness.seen; i++) yield return new WaitForFixedUpdate();
                move = Vector2.zero; if (witness.seen) break;
            }
            report.newStatueSurfaceBlocksController = witness.seen; Destroy(witness);
            Require(report.newStatueSurfaceBlocksController, "Actual visitor movement did not contact the new exact Dike clothing surface.");
            checks.Add("Actual CharacterController movement is blocked by bench and P02; exact added surfaces pass PhysX ray checks.");
            checks.Add("The actual visitor's ControllerColliderHit identifies the newly added exact Dike body MeshCollider itself.");

            report.phase = "actual bottle floor impacts and rolling throttle";
            game.PlacePlayer(new Vector3(0, .01f, 31));
            GalleryProp bottle = course.Bottle; string cue = GalleryDikeAudio.ImpactCue(true, 1);
            var impact = bottle.GetComponent<GalleryDikePropImpact>();
            Require(impact != null, "The actual bottle impact component is missing.");
            int initial = course.Sounds.Count(cue); impact.ResetContact();
            DropFixture(bottle, new Vector3(-2, 1.8f, 33));
            int until = ticks + 130;
            while (course.Sounds.Count(cue) == initial && ticks < until) yield return new WaitForFixedUpdate();
            report.bottleFirstCount = course.Sounds.Count(cue) - initial;
            report.firstBottleFallAudible = report.bottleFirstCount == 1;
            Require(report.firstBottleFallAudible, "The first actual bottle floor fall was not audible once.");
            float firstTime = LastTime(course.Sounds, cue);
            for (int pulse = 0; pulse < 5; pulse++)
            {
                Bounce(bottle);
                for (int i = 0; i < 7; i++) yield return new WaitForFixedUpdate();
            }
            report.bottleRapidCount = course.Sounds.Count(cue) - initial;
            report.rapidBottleBouncesSuppressed = report.bottleRapidCount == 1;
            Require(report.rapidBottleBouncesSuppressed, "Rapid actual floor bounces replayed the bottle sound within its cooldown.");
            while (Time.time - firstTime < GalleryDikePropImpact.BottleImpactInterval + .08f) yield return new WaitForFixedUpdate();
            int previous = course.Sounds.Count(cue); Bounce(bottle); until = ticks + 55;
            while (course.Sounds.Count(cue) == previous && ticks < until) yield return new WaitForFixedUpdate();
            report.bottleSeparatedCount = course.Sounds.Count(cue) - previous;
            report.separatedStrikeAudible = report.bottleSeparatedCount >= 1;
            Require(report.separatedStrikeAudible, "A later real normal strike was permanently silenced.");
            report.bottleMinimumRepeatInterval = MinimumInterval(course.Sounds, cue);
            Require(report.bottleMinimumRepeatInterval >= GalleryDikePropImpact.BottleImpactInterval - .025f,
                "Repeated rolling-episode sound events were closer than the approved interval.");
            previous = course.Sounds.Count(cue); bottle.BeginHold(); yield return null; yield return null;
            DropFixture(bottle, new Vector3(-2, 1.03f, 33)); until = ticks + 90;
            while (course.Sounds.Count(cue) == previous && ticks < until) yield return new WaitForFixedUpdate();
            report.bottleFreshCount = course.Sounds.Count(cue) - previous;
            report.freshHoldDropAudible = report.bottleFreshCount >= 1;
            Require(report.freshHoldDropAudible, "Public BeginHold/Release did not restore the next first landing sound.");
            var stamps = new List<string>();
            foreach (var stamp in course.Sounds.History) if (stamp.cue == cue) stamps.Add(stamp.cue + " @ " + stamp.time.ToString("F3"));
            report.bottleEvents = stamps.ToArray();
            bottle.body.isKinematic = true; bottle.body.useGravity = false;
            checks.Add("Actual bottle drops remain audible; five rapid physical bounce impulses are suppressed; a separated strike and fresh hold/drop remain audible.");

            report.phase = "coastal preview asset and live portal";
            var portal = game.GetComponentInChildren<GalleryCathedralPortal>(true);
            var entrance = game.orcusExit;
            Require(portal != null && entrance != null && entrance.cathedralActivation != null, "Final hand portal is not connected.");
            var source = Resources.Load<GameObject>(GalleryCathedralPortal.PreviewResource);
            report.beachPreviewAsset = GalleryCathedralPortal.PreviewResource == "MuseumPortal/BeachPreview" && source != null &&
                source.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 && source.GetComponentsInChildren<Collider>(true).Length == 0;
            Require(report.beachPreviewAsset, "A render-only actual BeachPreview resource is required.");
            var names = new List<string>(); bool coastal = false, landmark = false, library = false;
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                names.Add(renderer.name); string lower = renderer.name.ToLowerInvariant();
                coastal |= lower.Contains("sea") || lower.Contains("ocean") || lower.Contains("water") || lower.Contains("shore");
                landmark |= lower.Contains("castle") || lower.Contains("palm") || lower.Contains("cliff") || lower.Contains("beach");
                library |= lower.Contains("library") || lower.Contains("bookcase");
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.shader != null)
                        coastal |= material.shader.name == "Parallax/MuseumBeachPreviewWater";
            }
            // Preview surfaces are combined by material and may have numbered
            // render names. Its preserved extraction inventory identifies the
            // actual destination art instead of guessing from those names.
            var provenance = Resources.Load<TextAsset>("MuseumPortal/BeachPreview-source");
            var record = provenance == null ? null : JsonUtility.FromJson<PreviewRecord>(provenance.text);
            Require(record != null && record.sourceScene == "Assets/Parallax/Beach/Scenes/Beach.unity" &&
                record.sourceRenderers >= 100 && record.meshGroups == names.Count, "Beach extraction provenance does not match the preview geometry.");
            bool destinationLandmark = false;
            foreach (string path in record.originalMaterialPaths ?? Array.Empty<string>())
            {
                string lower = path.ToLowerInvariant();
                destinationLandmark |= lower.Contains("castle") || lower.Contains("palm") || lower.Contains("flower");
                library |= lower.Contains("library");
            }
            landmark &= destinationLandmark;
            report.previewNames = names.ToArray(); report.previewRenderers = names.Count;
            report.previewHasCoastalGeometry = coastal && landmark && names.Count >= 20;
            report.noLibraryPreview = !library;
            Require(report.previewHasCoastalGeometry && report.noLibraryPreview, "Preview must contain actual coastal scenery and no library furniture.");
            GalleryProp hands = entrance.prop; hands.InitializePhysics(); hands.body.isKinematic = true; hands.body.useGravity = false;
            hands.transform.SetPositionAndRotation(GalleryBottleBalanceCourse.ShiftLegacy(new Vector3(0, .006f, 53)), Quaternion.identity);
            hands.transform.localScale = Vector3.one * 3;
            hands.body.position = hands.transform.position; hands.body.rotation = hands.transform.rotation;
            Physics.SyncTransforms(); hands.placement.ResetPlaced();
            game.checkpoint = 3; game.PlacePlayer(GalleryBottleBalanceCourse.ShiftLegacy(new Vector3(0, .01f, 45.4f)));
            until = ticks + 260;
            while ((!portal.Ready || !entrance.IsReadyForEntry) && ticks < until) yield return new WaitForFixedUpdate();
            report.handsOpenedNaturally = entrance.IsReadyForEntry && entrance.cathedralActivation.Opened;
            Require(report.handsOpenedNaturally, "The production enlarged/settled hand fixture did not open.");
            game.yaw = 0; game.pitch = 0;
            Vector3 mouth = entrance.MouthPosition;
            game.PlacePlayer(new Vector3(mouth.x - .15f, mouth.y - 1.55f, mouth.z - 3));
            yield return null; yield return new WaitForEndOfFrame(); portal.RenderWindow();
            Color32[] first = Pixels(portal.PortalTexture, "01-live-beach-portal.png");
            report.previewLuminanceDeviation = Deviation(first);
            report.livePortalTexture = portal.Ready && portal.PreviewCamera != null && portal.PreviewCamera.GetComponent<AudioListener>() == null &&
                portal.PreviewRendererCount == report.previewRenderers && report.previewLuminanceDeviation > .012f;
            Require(report.livePortalTexture, "The portal render texture is missing, flat, or has extra audio.");
            Vector3 firstEye = portal.LastDestinationEye;
            game.PlacePlayer(game.player.feet + Vector3.right * .32f);
            yield return null; yield return new WaitForEndOfFrame(); portal.RenderWindow();
            Color32[] second = Pixels(portal.PortalTexture, "02-live-beach-portal-offset.png");
            report.previewParallaxDifference = Difference(first, second);
            report.portalParallax = Vector3.Distance(firstEye, portal.LastDestinationEye) > .02f && report.previewParallaxDifference > .0005f;
            report.previewRenderCount = portal.RenderCount;
            Require(report.portalParallax, "The coastal portal did not change with the actual viewer position.");
            checks.Add("Render-only actual coastal scene has landmarks and water; live portal pixels and camera-dependent parallax are present.");

            report.phase = "real controller crossing and asynchronous museum-to-beach handoff";
            Require(Application.CanStreamedLevelBeLoaded("Beach"), "Beach is absent from the actual build scene list.");
            entrance.enabled = false; move = Vector2.zero; drive = false;
            Vector3 approach = entrance.transform.TransformPoint(new Vector3(entrance.EntryCenterX, entrance.EntryBottom + .025f, entrance.EntryPlane - .12f));
            game.PlacePlayer(approach); Vector3 crossedFrom = game.controller.transform.position;
            game.controller.Move(entrance.transform.forward * .9f); Vector3 crossedTo = game.controller.transform.position;
            game.player.feet = crossedTo;
            report.actualControllerCrossing = entrance.CanEnterFrom(crossedFrom, crossedTo) && entrance.TryEnter(crossedFrom, crossedTo) && game.Transitioning;
            Require(report.actualControllerCrossing, "The real controller could not cross the opened hand plane and begin production entry.");
            while (SceneManager.GetActiveScene().name != "Beach") yield return null;
            report.beachLoaded = true;
            var beach = UnityEngine.Object.FindFirstObjectByType<BeachGame>();
            while (!handoffLogged || beach == null || !beach.enabled || !beach.active || !beach.started)
            { beach = UnityEngine.Object.FindFirstObjectByType<BeachGame>(); yield return null; }
            report.beachSpawn = beach.player.feet;
            report.beachFirstSpawn = beach.checkpoint == 0 && Vector2.Distance(new Vector2(beach.player.feet.x, beach.player.feet.z),
                new Vector2(BeachWorld.Checkpoints[0].x, BeachWorld.Checkpoints[0].z)) < .35f;
            report.beachControlsReady = beach.enabled && beach.active && beach.started && !beach.won && beach.controller.enabled &&
                beach.view.enabled && beach.held == null && beach.automated && Time.timeScale > .99f;
            Require(report.beachFirstSpawn && report.beachControlsReady, "Beach handoff did not restore first-spawn/controller state.");
            Vector3 old = beach.player.feet;
            for (int i = 0; i < 12; i++) { beach.Tick(.02f, Vector2.up, false); yield return new WaitForFixedUpdate(); }
            report.beachAfterMove = beach.player.feet;
            report.beachCanMove = new Vector2(beach.player.feet.x - old.x, beach.player.feet.z - old.z).magnitude > .10f;
            Require(report.beachCanMove, "The Beach controller did not respond to production movement after handoff.");
            report.phase = "actual destination camera render after completed fade";
            while (UnityEngine.Object.FindFirstObjectByType<GalleryStageTransition>() != null) yield return null;
            // Hidden-window backbuffers may never present a frame. Render the
            // actual destination camera explicitly after its normal transition
            // has finished; do not remove, disable or bypass the production fade.
            yield return null; yield return null; yield return null;
            Color32[] arrival = RenderCamera(beach.view, "03-actual-beach-arrival.png");
            report.beachCameraMeanLuminance = Mean(arrival);
            report.beachCameraLuminanceDeviation = Deviation(arrival);
            report.beachCameraRenderVisible = report.beachCameraMeanLuminance > .025f && report.beachCameraLuminanceDeviation > .012f;
            Require(report.beachCameraRenderVisible, "The actual destination camera rendered a black or flat arrival image.");
            checks.Add("Actual CharacterController crossing started the production async Beach load; initial spawn and production movement are restored.");
            checks.Add("After the production fade runner finished, the actual Beach view camera rendered non-black varied destination pixels without lighting/fade changes.");
        }

        static void DropFixture(GalleryProp piece, Vector3 at)
        {
            piece.body.isKinematic = true; piece.body.useGravity = false;
            piece.transform.localScale = Vector3.one; piece.transform.SetPositionAndRotation(at, Quaternion.identity);
            piece.body.position = at; piece.body.rotation = Quaternion.identity; Physics.SyncTransforms();
            piece.Release(); piece.body.linearVelocity = Vector3.down;
        }
        static void Bounce(GalleryProp bottle)
        {
            bottle.body.position += Vector3.up * .10f;
            bottle.body.linearVelocity = new Vector3(.55f, -.9f, .12f); bottle.body.angularVelocity = new Vector3(0, 0, 1.2f);
            bottle.body.WakeUp(); Physics.SyncTransforms();
        }
        static float LastTime(GalleryDikeAudio audio, string cue)
        { for (int i = audio.History.Count - 1; i >= 0; i--) if (audio.History[i].cue == cue) return audio.History[i].time; return -1000; }
        static float MinimumInterval(GalleryDikeAudio audio, string cue)
        {
            float previous = -1000, smallest = float.MaxValue;
            foreach (var stamp in audio.History) if (stamp.cue == cue) { if (previous > -999) smallest = Mathf.Min(smallest, stamp.time - previous); previous = stamp.time; }
            return smallest == float.MaxValue ? 0 : smallest;
        }
        Color32[] Pixels(RenderTexture target, string name)
        {
            Require(target != null && target.IsCreated(), "Portal texture is not created.");
            RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply(); RenderTexture.active = previous;
            Color32[] result = texture.GetPixels32(); File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG()); Destroy(texture); return result;
        }
        Color32[] RenderCamera(Camera camera, string name)
        {
            Require(camera != null && camera.enabled, "Actual destination view camera is unavailable.");
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D texture = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG()); return texture.GetPixels32();
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                target.Release(); Destroy(target); if (texture != null) Destroy(texture);
            }
        }
        static float Mean(Color32[] colors)
        {
            double sum = 0; int count = 0;
            for (int i = 0; i < colors.Length; i += 17)
            { var c = colors[i]; sum += (.2126 * c.r + .7152 * c.g + .0722 * c.b) / 255; count++; }
            return count == 0 ? 0 : (float)(sum / count);
        }
        static float Deviation(Color32[] colors)
        {
            double sum = 0, square = 0; int count = 0;
            for (int i = 0; i < colors.Length; i += 17) { var c = colors[i]; double l = (.2126 * c.r + .7152 * c.g + .0722 * c.b) / 255; sum += l; square += l * l; count++; }
            return (float)Math.Sqrt(Math.Max(0, square / count - sum * sum / count / count));
        }
        static float Difference(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return 0; double sum = 0; int count = 0;
            for (int i = 0; i < a.Length; i += 17) { sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b); count++; }
            return (float)(sum / count / 765);
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }

    public sealed class GalleryMuseumIntegrationCollisionWitness : MonoBehaviour
    {
        public Collider expected;
        public bool seen;
        void OnControllerColliderHit(ControllerColliderHit hit) { if (hit.collider == expected) seen = true; }
    }
}
