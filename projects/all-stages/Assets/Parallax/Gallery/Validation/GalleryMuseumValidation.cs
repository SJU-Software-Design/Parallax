using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Library;

namespace ShadeLink.Gallery
{
    // Focused museum fixtures, not an automated solution of all four puzzle rooms.
    public sealed class GalleryMuseumValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly string[] artworks = { "StarryNight", "WaterLilyBridge", "Scream", "Ambassadors", "GirlWithPearlEarring" };
        string output;
        SimulationMode previousSimulation;
        bool ownsSimulation, logging;
        int libraryLoads;
        readonly Report report = new Report();

        [Serializable] sealed class Report
        {
            public bool passed, sharedInteraction, smallEntryRejected, heldEntryRejected;
            public bool unsupportedEntryRejected, reverseEntryRejected, sideEntryRejected;
            public bool physicalMouthCrossing, inputFrozenDuringPassage, libraryActive;
            public bool windDistanceCurve, libraryLoaded, simulationRestored;
            public bool inputOnlyEnlargement, inputOnlyRelease, inputOnlySettled;
            public bool perspectiveAlignmentFixture, perspectiveHeldRejected, perspectiveResetRestored;
            public int artworkCount, artworkDisplays, walkFrames, librarySceneLoads, enlargementLookUpdates, perspectivePickups;
            public float carryWalkDistance, mouthWalkDistance, enlargedScale, requiredScale, fallDistance;
            public float inputOnlyMaximumScale, inputOnlyPlacedScale, enlargementYawDegrees, enlargementPitchDegrees;
            public string scope, fixture, failure, unity, gpu, version, enlargementLastRestriction;
            public string[] checks, errors;
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-shade-output");
            output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Application.persistentDataPath, "GalleryMuseumValidation");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += Log; logging = true;
            SceneManager.sceneLoaded += SceneLoaded;
            previousSimulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script; ownsSimulation = true;
            report.unity = Application.unityVersion; report.gpu = SystemInfo.graphicsDeviceName; report.version = Application.version;
            report.scope = "Real museum artwork resources and scene displays, public camera pickup/carry/cancel, input-only Orcus enlargement/release/settling, perspective exhibit pickup and explicit alignment fixtures, entry eligibility fixtures, physical CharacterController approach through the enlarged mouth, and an actual Library scene handoff. This does not claim a full museum puzzle playthrough or human evaluation of audio loudness.";
            report.fixture = "The input-only enlargement fixture spawns the player once near the original small Orcus, then uses public pickup, camera yaw/pitch, held updates and release; it never writes that object's pose or scale. Perspective alignment uses explicitly assigned mathematical goal poses, not a claimed player solution. Negative entry tests use explicit world-point pairs. A later independent crossing fixture assigns an explicit upright Orcus scale at home and settles it under PhysX gravity; the player starts on its first approach step and walks through the real hole with no subsequent pose writes.";
            var sequence = Sequence();
            while (true)
            {
                object step = null; bool more;
                try { more = sequence.MoveNext(); if (more) step = sequence.Current; }
                catch (Exception failure) { report.failure = failure.ToString(); more = false; }
                if (!more) break;
                yield return step;
            }
            RestoreSimulation();
            report.simulationRestored = Physics.simulationMode == previousSimulation;
            report.librarySceneLoads = libraryLoads;
            report.passed = report.failure == null && errors.Count == 0 && report.simulationRestored;
            report.checks = checks.ToArray(); report.errors = errors.ToArray();
            File.WriteAllText(Path.Combine(output, "museum-results.json"), JsonUtility.ToJson(report, true));
            if (!report.passed)
            {
                Debug.Log("GALLERY_MUSEUM_FAIL " + report.failure);
                var camera = game != null ? game.view : FindFirstObjectByType<LibraryGame>()?.view;
                if (camera != null) Capture(camera, "museum-failure");
            }
            else Debug.Log("GALLERY_MUSEUM_OK " + checks.Count + " checks; Library handoff verified");
            Unsubscribe();
            Application.Quit(report.passed ? 0 : 1);
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode) { if (scene.name == "Library") libraryLoads++; }
        void Log(string message, string stack, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + stack); }
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("GALLERY_MUSEUM: " + message); }
        void Check(string message) { checks.Add(message); Debug.Log("GALLERY_MUSEUM_PASS " + message); }

        void RestoreSimulation()
        {
            if (!ownsSimulation) return;
            Physics.simulationMode = previousSimulation; ownsSimulation = false;
        }
        void Unsubscribe()
        {
            if (logging) { Application.logMessageReceived -= Log; logging = false; }
            SceneManager.sceneLoaded -= SceneLoaded;
        }
        void OnDestroy() { RestoreSimulation(); Unsubscribe(); }

        void Look(Vector3 target)
        {
            Vector3 direction = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(direction.x, direction.z);
            game.pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude);
            game.SyncView();
        }
        void Grab(GalleryProp prop)
        {
            foreach (var part in prop.parts)
            {
                if (part == null || !part.enabled) continue;
                Look(part.bounds.center); game.UpdateAim();
                if (game.aimed != prop) continue;
                game.Interact();
                if (game.held == prop) break;
            }
            Require(game.held == prop, "Visible collider cannot be picked up through the public shared interaction: " + prop.title);
        }
        void Simulate()
        {
            Physics.Simulate(Dt); Physics.SyncTransforms();
            if (game == null) return;
            foreach (var prop in game.props) prop.placement.Tick(Dt);
            game.lighting.Synchronize();
        }
        Vector3 MouthFeet(GalleryOrcusExit exit, float x, float z)
        { return exit.transform.TransformPoint(new Vector3(x, GalleryOrcusExit.MouthBottom + .025f, GalleryOrcusExit.MouthPlane + z)); }

        IEnumerator InputOnlyEnlargement(GalleryOrcusExit exit)
        {
            var sculpture = exit.prop;
            game.PlacePlayer(sculpture.homePosition + Vector3.back * 1.25f);
            Require(game.CanStand(game.player.feet), "Close Orcus pickup fixture is not in a free standing position");
            Grab(sculpture);
            Vector3 playerPosition = game.controller.transform.position;
            // Smooth camera motion through a bounded set of targets, exactly as
            // held mouse-look moves the common placement resolver. No prop pose
            // or scale writes, resize shortcut, forced release, or re-grab.
            Vector2[] targets = { new Vector2(-75, 12), new Vector2(75, 12), new Vector2(-90, 0), new Vector2(90, 0) };
            bool released = false;
            for (int target = 0; target < targets.Length && !released && report.enlargementLookUpdates < 180; target++)
            {
                for (int update = 0; update < 90 && !released && report.enlargementLookUpdates < 180; update++)
                {
                    float yaw = Mathf.MoveTowardsAngle(game.yaw * Mathf.Rad2Deg, targets[target].x, 2.5f);
                    float pitch = Mathf.MoveTowards(game.pitch * Mathf.Rad2Deg, targets[target].y, 2.5f);
                    game.yaw = yaw * Mathf.Deg2Rad; game.pitch = pitch * Mathf.Deg2Rad; game.SyncView();
                    game.Tick(Dt, Vector2.zero); Simulate(); report.enlargementLookUpdates++;
                    Require(game.held == sculpture && sculpture.body.isKinematic, "Input-only enlargement lost the carried sculpture");
                    float scale = Mathf.Min(sculpture.transform.lossyScale.x, sculpture.transform.lossyScale.y, sculpture.transform.lossyScale.z);
                    if (scale > report.inputOnlyMaximumScale)
                    {
                        report.inputOnlyMaximumScale = scale;
                        report.enlargementYawDegrees = yaw; report.enlargementPitchDegrees = pitch;
                    }
                    report.enlargementLastRestriction = game.PlacementDiagnostic.Limiter + ": " + game.invalidReason;
                    if (scale >= exit.RequiredScale && game.ValidPlacement(sculpture, out var reason))
                    {
                        report.inputOnlyEnlargement = true;
                        Capture(game.view, "museum-orcus-input-enlarged");
                        released = game.Drop();
                        if (released)
                        {
                            report.inputOnlyRelease = true;
                            report.inputOnlyPlacedScale = sculpture.transform.lossyScale.x;
                            report.enlargementYawDegrees = yaw; report.enlargementPitchDegrees = pitch;
                        }
                    }
                    if (report.enlargementLookUpdates % 8 == 0) yield return null;
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, targets[target].x)) < .1f && Mathf.Abs(pitch - targets[target].y) < .1f && update > 74) break;
                }
            }
            if (!released) Capture(game.view, "museum-orcus-input-enlargement-failure");
            Require(report.inputOnlyEnlargement && released,
                "Original small Orcus cannot be enlarged and released through the actual common controls; maximum=" + report.inputOnlyMaximumScale +
                ", required=" + exit.RequiredScale + ", updates=" + report.enlargementLookUpdates + ", last=" + report.enlargementLastRestriction);
            Require(Vector3.Distance(playerPosition, game.controller.transform.position) < .15f,
                "Input-only enlargement fixture unexpectedly moved the player");
            for (int i = 0; i < 270 && !exit.IsReadyForEntry; i++)
            { Simulate(); if (i % 12 == 0) yield return null; }
            report.inputOnlySettled = exit.IsReadyForEntry;
            Require(report.inputOnlySettled, "Input-enlarged sculpture could be dropped but never became a settled traversable exit: " + sculpture.placement.Status);
            Check("Original small Orcus enlarged through public pickup and " + report.enlargementLookUpdates + " camera updates, released and settled; scale=" +
                report.inputOnlyPlacedScale + ", yaw=" + report.enlargementYawDegrees + ", pitch=" + report.enlargementPitchDegrees);
        }

        IEnumerator PerspectiveFixtures()
        {
            var exhibit = game.GetComponentInChildren<GalleryMuseumPerspective>(true);
            Require(exhibit != null && exhibit.pieces != null && exhibit.pieces.Length == 3, "Perspective exhibit or its three real props missing");
            Vector3[] pickupPositions = { new Vector3(-8.2f, 0, 37.3f), new Vector3(-5, 0, 35.6f), new Vector3(-12.5f, 0, 33.3f) };
            for (int i = 0; i < exhibit.pieces.Length; i++)
            {
                game.PlacePlayer(pickupPositions[i]);
                Require(game.CanStand(game.player.feet), "Perspective pickup fixture is obstructed for piece " + i);
                Grab(exhibit.pieces[i]);
                Require(exhibit.pieces[i].placement.Held, "Perspective piece did not enter normal held placement state");
                game.Cancel(true); report.perspectivePickups++;
                yield return null;
            }
            Check("All three perspective exhibit pieces support the same public pickup/cancel interaction");
            // Explicit projection/placement fixture: assign published goal poses
            // only here, then exercise the real evaluator and held-state filter.
            for (int i = 0; i < exhibit.pieces.Length; i++)
            {
                var prop = exhibit.pieces[i]; prop.Restore();
                prop.transform.SetPositionAndRotation(exhibit.solutionPositions[i], Quaternion.identity);
                prop.transform.localScale = Vector3.one * exhibit.solutionScales[i];
                prop.body.position = prop.transform.position; prop.body.rotation = prop.transform.rotation;
                Physics.SyncTransforms(); prop.placement.ResetPlaced();
                Require(prop.placement.Ready, "Perspective mathematical goal pose is not physically supported: " + i);
            }
            game.PlacePlayer(exhibit.viewingPoint.position); game.yaw = 0; game.pitch = 0; game.SyncView();
            exhibit.Evaluate();
            Require(exhibit.Complete && exhibit.AtViewingPoint && exhibit.AlignedPieces == 3, "Real perspective evaluator does not recognize its published solution");
            report.perspectiveAlignmentFixture = true;
            CaptureView("museum-perspective-solved", exhibit.viewingPoint.position + Vector3.up * 1.55f,
                exhibit.viewingPoint.position + Vector3.up * 1.55f + Vector3.forward * 10);
            Grab(exhibit.pieces[2]); exhibit.Evaluate();
            Require(!exhibit.Complete && exhibit.AlignedPieces < 3, "Held perspective artwork still counts toward the solution");
            report.perspectiveHeldRejected = true;
            game.Cancel(true); exhibit.Evaluate();
            Require(exhibit.Complete, "Cancel failed to restore perspective fixture eligibility");
            game.ResetAll(); game.Resume(); exhibit.Evaluate();
            Require(!exhibit.Complete, "ResetAll left the solved perspective state active");
            report.perspectiveResetRestored = true;
            Check("Explicit supported goal poses complete the perspective evaluator; pickup invalidates it, cancel restores it, and reset clears it");
        }

        IEnumerator Sequence()
        {
            yield return null; // Runtime labels and the spatial audio source initialize through their real Start methods.
            Require(game != null && game.controller != null && game.view != null, "Museum game/controller/camera missing");
            Require(game.orcusExit != null && game.orcusExit.prop != null, "Orcus exhibit missing from the actual scene");
            Require(Application.CanStreamedLevelBeLoaded("Library"), "Latest Gallery build does not include the Library scene");
            game.ResetAll(); game.Resume();
            var renderers = game.GetComponentsInChildren<Renderer>(true);
            foreach (string key in artworks)
            {
                var texture = Resources.Load<Texture2D>("MuseumArt/" + key);
                Require(texture != null && texture.width >= 512 && texture.height >= 512, "Actual artwork resource missing or too small: " + key);
                bool displayed = false;
                foreach (var renderer in renderers) foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.HasProperty("_MainTex") && material.mainTexture == texture)
                    { displayed = true; report.artworkDisplays++; }
                Require(displayed, "Downloaded artwork is not displayed by a scene renderer: " + key);
                report.artworkCount++;
            }
            Check("All five real artwork textures load and are assigned to actual museum displays");
            CaptureView("museum-entrance", new Vector3(0, 1.58f, 3.8f), new Vector3(0, 2.6f, 8));
            CaptureView("museum-sculpture-gallery", new Vector3(8.5f, 3.0f, 24), new Vector3(-5, 2.8f, 17));

            var painting = game.props[0];
            Vector3 paintingPosition = painting.transform.position, paintingScale = painting.transform.localScale;
            Quaternion paintingRotation = painting.transform.rotation;
            game.PlacePlayer(GalleryWorld.Checkpoints[0]);
            Grab(painting);
            Require(painting.body.isKinematic, "Carried painting does not use kinematic ownership");
            Vector3 carryStart = game.player.feet;
            for (int i = 0; i < 35; i++)
            {
                game.Tick(Dt, new Vector2(.5f, 0)); Simulate();
                Require(game.held == painting, "Carried artwork was lost during walking");
                foreach (var part in painting.parts) Require(part.enabled, "Carried collider disabled");
                if (i % 10 == 0) yield return null;
            }
            report.carryWalkDistance = Vector3.Distance(carryStart, game.player.feet);
            Require(report.carryWalkDistance > .15f, "Holding an artwork prevents ordinary walking");
            game.Cancel();
            Require(game.held == null && Vector3.Distance(painting.transform.position, paintingPosition) < .002f &&
                Vector3.Distance(painting.transform.localScale, paintingScale) < .002f && Quaternion.Angle(painting.transform.rotation, paintingRotation) < .02f,
                "Cancel did not restore the original painting pose");
            report.sharedInteraction = true;
            Check("Public camera pickup, shared carried walking and cancel preserve the artwork pose and collider ownership");

            CaptureView("museum-perspective-initial", new Vector3(-9, 1.55f, 29.2f), new Vector3(-9, 1.55f, 39.2f));

            game.StartOrcusDemo();
            var exit = game.orcusExit; var sculpture = exit.prop;
            exit.ResetEntrance(); report.requiredScale = exit.RequiredScale;
            Require(!exit.IsReadyForEntry, "Small original exhibit is already traversable");
            Require(!exit.TryEnter(MouthFeet(exit, 0, -.1f), MouthFeet(exit, 0, .1f)), "Small exhibit accepted a crossing");
            report.smallEntryRejected = true;
            Check("Small original Orcus rejects entrance");
            CaptureView("museum-orcus-small", GalleryWorld.OrcusDemoStart + Vector3.up * 1.55f, exit.MouthPosition);

            float near = GalleryOrcusExit.WindVolumeAtDistance(.5f), middle = GalleryOrcusExit.WindVolumeAtDistance(5), far = GalleryOrcusExit.WindVolumeAtDistance(12);
            Require(near > middle && middle > far && far > 0 && GalleryOrcusExit.WindVolumeAtDistance(18) == 0,
                "Wind attenuation does not grow toward the mouth or fade at distance");
            bool spatialWind = false;
            foreach (var audio in sculpture.GetComponentsInChildren<AudioSource>(true))
                if (audio.clip != null && audio.clip.name.Contains("soft air") && audio.spatialBlend > .99f && audio.loop) spatialWind = true;
            Require(spatialWind, "Actual spatial looping wind audio source missing");
            report.windDistanceCurve = true;
            Check("Wind target volume increases with proximity and the real exhibit owns a spatial looping audio source");

            var enlargement = InputOnlyEnlargement(exit);
            while (enlargement.MoveNext()) yield return enlargement.Current;
            var perspective = PerspectiveFixtures();
            while (perspective.MoveNext()) yield return perspective.Current;
            game.StartOrcusDemo(); exit = game.orcusExit; sculpture = exit.prop;

            // Explicit fixture setup is confined to this block, before the final physical approach.
            game.PlacePlayer(new Vector3(5.7f, sculpture.homePosition.y, 49.8f));
            sculpture.Restore();
            sculpture.transform.SetPositionAndRotation(sculpture.homePosition + Vector3.up * .035f, Quaternion.identity);
            report.enlargedScale = Mathf.Max(2.5f, exit.RequiredScale * 1.2f);
            sculpture.transform.localScale = Vector3.one * report.enlargedScale;
            sculpture.body.position = sculpture.transform.position; sculpture.body.rotation = sculpture.transform.rotation;
            Physics.SyncTransforms(); sculpture.Release();
            Require(!exit.IsReadyForEntry && !exit.TryEnter(MouthFeet(exit, 0, -.1f), MouthFeet(exit, 0, .1f)),
                "An unplaced enlarged exhibit accepted a crossing");
            report.unsupportedEntryRejected = true;
            for (int i = 0; i < 160 && !exit.IsReadyForEntry; i++)
            { Simulate(); if (i % 12 == 0) yield return null; }
            Require(exit.IsReadyForEntry, "Explicitly enlarged exhibit did not settle on the real balcony: " + sculpture.placement.Status);
            Check("Enlarged fixture remains unavailable until actual placement support and settling complete");

            Vector3 before = MouthFeet(exit, 0, -.12f), after = MouthFeet(exit, 0, .12f);
            Require(!exit.CanEnterFrom(after, before), "Reverse passage accepted");
            report.reverseEntryRejected = true;
            Require(!exit.CanEnterFrom(MouthFeet(exit, GalleryOrcusExit.MouthHalfWidth + .5f, -.12f),
                MouthFeet(exit, GalleryOrcusExit.MouthHalfWidth + .3f, .12f)), "Side crossing outside the aperture accepted");
            report.sideEntryRejected = true;
            Require(!exit.CanEnterFrom(before - exit.transform.forward * 3, after), "Teleport crossing accepted");
            Check("Back, side and discontinuous teleport point-pair fixtures cannot trigger the mouth");

            // Pick up the real hollow compound object through the same path used by ordinary Gallery props.
            Grab(sculpture);
            Require(sculpture.placement.Held && !exit.IsReadyForEntry && !exit.TryEnter(before, after), "Held sculpture accepted entrance");
            report.heldEntryRejected = true;
            game.Cancel(true);
            for (int i = 0; i < 90 && !exit.IsReadyForEntry; i++)
            { Simulate(); if (i % 12 == 0) yield return null; }
            Require(exit.IsReadyForEntry, "Cancel did not restore settled enlarged exhibit eligibility");
            Check("Actual shared pickup disables the Orcus exit; cancel restores its prior settled state");

            CaptureView("museum-orcus-enlarged", GalleryWorld.OrcusDemoStart + Vector3.up * 1.55f,
                exit.MouthPosition + Vector3.up * .15f);
            // Spawn onto the first real approach tread; walk over the remaining physical steps and through the hollow mouth.
            Vector3 approach = exit.transform.TransformPoint(new Vector3(0, .06f, -.92f));
            game.PlacePlayer(approach + Vector3.up * .005f); game.yaw = 0; game.pitch = -.02f; game.SyncView();
            Require(game.CanStand(game.player.feet), "Physical mouth approach spawn overlaps scene geometry");
            exit.ResetEntrance();
            yield return null;
            Vector3 walkStart = game.player.feet;
            for (int i = 0; i < 150 && !game.Transitioning; i++)
            {
                game.Tick(Dt, new Vector2(0, .65f)); Simulate(); report.walkFrames++;
                Require(game.respawns == 0, "Final physical approach respawned before reaching the mouth");
                yield return null; // The actual GalleryOrcusExit.Update samples each accepted controller position.
            }
            Require(game.Transitioning && exit.Entering, "CharacterController could not walk through the ready mouth");
            report.mouthWalkDistance = Vector3.Distance(walkStart, game.player.feet);
            Require(report.mouthWalkDistance > .35f && report.walkFrames > 3, "Entry did not exercise a physical walking approach");
            report.physicalMouthCrossing = true;
            var passage = game.stageTransition;
            Require(passage != null && !game.active && game.won && !((IObjectInteractionHost<GalleryProp>)game).CanInteract,
                "Normal gameplay or pickup remained active during passage");
            Vector3 frozen = game.controller.transform.position;
            game.Tick(.05f, Vector2.one, true); game.Interact(); game.Resume(); game.Pause();
            Require(Vector3.Distance(game.controller.transform.position, frozen) < .0001f && game.held == null && !game.active && Time.timeScale == 1,
                "Movement, grab or pause/resume disturbed the active transition");
            report.inputFrozenDuringPassage = true;
            Check("Real CharacterController movement crosses the open compound aperture and freezes gameplay exactly once");
            RestoreSimulation(); // Restore normal physics before the destination scene's Awake and resumed gameplay.

            float deadline = Time.realtimeSinceStartup + 15;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (passage != null) report.fallDistance = Mathf.Max(report.fallDistance, passage.FallDistance);
                var library = FindFirstObjectByType<LibraryGame>();
                // The transition runner is destroyed after its final fade. A
                // playable, enabled destination is the durable completion state.
                if (library != null && library.enabled && library.active)
                {
                    Require(SceneManager.GetActiveScene().name == "Library" && libraryLoads == 1, "Destination scene was not loaded exactly once");
                    Require(library.active && library.started && !library.won && Time.timeScale == 1, "Library did not resume into active gameplay");
                    report.libraryLoaded = true; report.libraryActive = true;
                    Capture(library.view, "museum-library-arrival");
                    break;
                }
                yield return null;
            }
            Require(report.libraryLoaded && report.libraryActive, "Timed out waiting for the actual Library scene handoff");
            Require(report.fallDistance > 10, "Passage did not show a descent longer than ten metres");
            Check("A fall longer than ten metres hands off to the actual Library scene with active gameplay and normal time scale");
        }

        void CaptureView(string name, Vector3 position, Vector3 target)
        {
            var camera = game.view;
            Vector3 oldPosition = camera.transform.position; Quaternion oldRotation = camera.transform.rotation;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            Capture(camera, name);
            camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
        }
        void Capture(Camera camera, string name)
        {
            if (camera == null) return;
            var old = camera.targetTexture; var active = RenderTexture.active;
            var rt = new RenderTexture(1600, 900, 24);
            Texture2D texture = null;
            try
            {
                camera.targetTexture = rt;
                if (game != null) game.lighting.Synchronize();
                camera.Render(); RenderTexture.active = rt;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = old; RenderTexture.active = active;
                rt.Release(); Destroy(rt); if (texture != null) Destroy(texture);
            }
        }
    }
}
