using System;
using System.Collections;
using System.IO;
using ShadeLink.Beach;
using ShadeLink.Gallery;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Tutorial
{
    // Opt-in route QA, not a puzzle solver. The caller has already walked from
    // Tutorial through the actual elevator into Gallery. This disclosed fixture
    // moves to checkpoint three and places the real hands at three-times scale.
    // Production placement, hand opening, entry tracking, fade and Beach loading
    // must then run; no completion/opening flags or scene-load setters are used.
    public sealed class TutorialRouteBeachValidation : MonoBehaviour
    {
        GalleryGame gallery;
        bool drive;
        int fixedSteps;

        void FixedUpdate()
        {
            if (!drive || gallery == null || !gallery.active || gallery.won || gallery.Transitioning) return;
            gallery.Tick(Time.fixedDeltaTime, Vector2.zero, false);
            fixedSteps++;
        }

        public static IEnumerator Run(GalleryGame game, Action<bool, string, string> check, string outputFolder = null)
        {
            Require(check, game != null && SceneManager.GetActiveScene().name == "Gallery", "Route begins in the actual arriving Gallery", "Tutorial elevator handoff must have completed first");
            Require(check, Application.CanStreamedLevelBeLoaded("Beach"), "Beach belongs to the actual build scene list", "No direct test scene load is used");
            Require(check, Physics.simulationMode == SimulationMode.FixedUpdate, "Route uses automatic PhysX", Physics.simulationMode.ToString());
            var node = new GameObject("Tutorial route · real Gallery to Beach verification");
            DontDestroyOnLoad(node);
            var driver = node.AddComponent<TutorialRouteBeachValidation>();
            driver.gallery = game;
            bool handoffLogged = false;
            Application.LogCallback onLog = (message, stack, type) =>
            {
                if (message.Contains("MUSEUM_BEACH_HANDOFF_OK")) handoffLogged = true;
            };
            Application.logMessageReceived += onLog;
            try
            {
                game.automated = true;
                // Inspection mode removes unrelated earlier sunlight exposure;
                // it does not set hands placement/opening/entry completion.
                game.exhibitionPreview = true;
                game.Resume();
                driver.drive = true;
                var entrance = game.orcusExit;
                var portal = game.GetComponentInChildren<GalleryCathedralPortal>(true);
                Require(check, entrance != null && entrance.prop != null && entrance.cathedralActivation != null && portal != null,
                    "Final hand exhibit and coastal portal are connected", "Production GalleryOrcusExit + activation + portal");
                var preview = Resources.Load<GameObject>(GalleryCathedralPortal.PreviewResource);
                Require(check, GalleryCathedralPortal.PreviewResource == "MuseumPortal/BeachPreview" && preview != null &&
                    preview.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 && preview.GetComponentsInChildren<Collider>(true).Length == 0,
                    "Portal uses the render-only Beach preview resource", GalleryCathedralPortal.PreviewResource);

                Debug.Log("TUTORIAL_ROUTE_BEACH_FIXTURE: checkpoint=3; hands scale=3; explicit exhibit/player poses; production placement/opening and entry remain authoritative. This is focused route QA, not continuous puzzle solving.");
                GalleryProp hands = entrance.prop;
                hands.InitializePhysics();
                hands.body.isKinematic = true;
                hands.body.useGravity = false;
                Vector3 handsPosition = Shift(game, new Vector3(0, .006f, 53));
                hands.transform.SetPositionAndRotation(handsPosition, Quaternion.identity);
                hands.transform.localScale = Vector3.one * 3;
                hands.body.position = handsPosition;
                hands.body.rotation = Quaternion.identity;
                Physics.SyncTransforms();
                hands.placement.ResetPlaced();
                game.checkpoint = 3;
                game.PlacePlayer(Shift(game, new Vector3(0, .01f, 45.4f)));
                game.yaw = 0; game.pitch = 0; game.SyncView();
                double until = Time.realtimeSinceStartupAsDouble + 20;
                while ((!portal.Ready || !entrance.IsReadyForEntry) && Time.realtimeSinceStartupAsDouble < until)
                    yield return null;
                Require(check, driver.fixedSteps > 0 && hands.placement.Ready && entrance.IsReadyForEntry && entrance.cathedralActivation.Opened,
                    "Actual enlarged hands settle and open through production logic", "fixedSteps=" + driver.fixedSteps + ", progress=" + entrance.cathedralActivation.Progress.ToString("F3"));

                Vector3 mouth = entrance.MouthPosition;
                game.PlacePlayer(new Vector3(mouth.x - .15f, mouth.y - 1.55f, mouth.z - 3));
                game.yaw = 0; game.pitch = 0; game.SyncView();
                yield return null; yield return null;
                portal.RenderWindow();
                Color32[] coastal = ReadTexture(portal.PortalTexture, FileAt(outputFolder, "04-route-live-beach-portal.png"));
                float portalDeviation = Deviation(coastal);
                Require(check, portal.Ready && portal.PreviewCamera != null && portal.PreviewCamera.GetComponent<AudioListener>() == null && portalDeviation > .012f,
                    "Production coastal portal renders actual varied pixels", "deviation=" + portalDeviation.ToString("F4"));

                driver.drive = false;
                entrance.ResetEntrance();
                Vector3 approach = entrance.transform.TransformPoint(new Vector3(entrance.EntryCenterX,
                    entrance.EntryBottom + .025f, entrance.EntryPlane - .12f));
                game.PlacePlayer(approach);
                game.yaw = 0; game.pitch = 0; game.SyncView();
                // Warm the production Update tracker at the disclosed approach.
                yield return null; yield return null;
                Vector3 crossedFrom = game.controller.transform.position;
                game.controller.Move(entrance.transform.forward * .9f);
                Vector3 crossedTo = game.controller.transform.position;
                game.player.feet = crossedTo;
                bool crossedGeometry = entrance.CanEnterFrom(crossedFrom, crossedTo);
                Require(check, crossedGeometry && Vector3.Distance(crossedFrom, crossedTo) > .1f,
                    "Actual CharacterController crosses the opened hand plane", "from=" + crossedFrom.ToString("F3") + ", to=" + crossedTo.ToString("F3"));
                until = Time.realtimeSinceStartupAsDouble + 3;
                while (game != null && !game.Transitioning && Time.realtimeSinceStartupAsDouble < until) yield return null;
                Require(check, game != null && game.Transitioning && entrance.Entering,
                    "Normal entrance Update begins the production Beach passage", "No test call to SceneManager.LoadScene or TryEnter");

                until = Time.realtimeSinceStartupAsDouble + 45;
                while (SceneManager.GetActiveScene().name != "Beach" && Time.realtimeSinceStartupAsDouble < until) yield return null;
                Require(check, SceneManager.GetActiveScene().name == "Beach", "Actual async route reaches Beach", SceneManager.GetActiveScene().name);
                BeachGame beach = UnityEngine.Object.FindFirstObjectByType<BeachGame>();
                until = Time.realtimeSinceStartupAsDouble + 15;
                while ((!handoffLogged || beach == null || !beach.enabled || !beach.active || !beach.started ||
                    UnityEngine.Object.FindFirstObjectByType<GalleryStageTransition>() != null) && Time.realtimeSinceStartupAsDouble < until)
                {
                    beach = UnityEngine.Object.FindFirstObjectByType<BeachGame>();
                    yield return null;
                }
                Require(check, handoffLogged && beach != null && beach.enabled && beach.active && beach.started && !beach.won &&
                    beach.controller != null && beach.controller.enabled && beach.view != null && beach.view.enabled && beach.held == null &&
                    beach.automated && Time.timeScale > .99f,
                    "Production Beach fade completes and controls resume", "MUSEUM_BEACH_HANDOFF_OK=" + handoffLogged);
                Vector3 start = beach.player.feet;
                Require(check, beach.checkpoint == 0 && Vector2.Distance(new Vector2(start.x, start.z),
                    new Vector2(BeachWorld.Checkpoints[0].x, BeachWorld.Checkpoints[0].z)) < .35f,
                    "Beach arrives at its first checkpoint", start.ToString("F3"));
                for (int i = 0; i < 12; i++)
                {
                    beach.Tick(.02f, Vector2.up, false);
                    yield return new WaitForFixedUpdate();
                }
                float moved = new Vector2(beach.player.feet.x - start.x, beach.player.feet.z - start.z).magnitude;
                Require(check, moved > .10f, "Actual Beach motor accepts movement after the route", moved.ToString("F3") + " m");
                yield return null; yield return null; yield return null;
                Require(check, !SceneManager.GetSceneByName("Tutorial").isLoaded && !SceneManager.GetSceneByName("Gallery").isLoaded &&
                    UnityEngine.Object.FindFirstObjectByType<TutorialGame>() == null && UnityEngine.Object.FindFirstObjectByType<GalleryGame>() == null,
                    "Tutorial and Gallery stages have actually unloaded", "Only Beach and intentional persistent infrastructure remain");
                Require(check, UnityEngine.Object.FindFirstObjectByType<TutorialMuseumElevatorFlow>() == null &&
                    UnityEngine.Object.FindFirstObjectByType<TutorialElevatorRide>() == null,
                    "Elevator cabin and route owner do not leak into Beach", "No stale elevator geometry, voices or controller route");
                int listeners = 0;
                foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (listener.enabled && listener.gameObject.activeInHierarchy) listeners++;
                Require(check, listeners == 1, "Beach has one active AudioListener", listeners.ToString());
                Color32[] arrival = RenderCamera(beach.view, FileAt(outputFolder, "05-route-actual-beach-arrival.png"));
                float mean = Mean(arrival), deviation = Deviation(arrival);
                Require(check, mean > .025f && deviation > .012f,
                    "Actual Beach view renders a visible arrival after the production fade", "mean=" + mean.ToString("F4") + ", deviation=" + deviation.ToString("F4"));
                Debug.Log("TUTORIAL_ROUTE_BEACH_VALIDATION_OK: real elevator arrival followed by production hand opening/crossing/load; first Beach checkpoint and actual movement/render verified.");
            }
            finally
            {
                driver.drive = false;
                Application.logMessageReceived -= onLog;
                if (node != null) Destroy(node);
            }
        }

        static Vector3 Shift(GalleryGame game, Vector3 legacy) => game.bottleBalance != null ? GalleryBottleBalanceCourse.ShiftLegacy(legacy) : legacy;
        static void Require(Action<bool, string, string> check, bool passed, string name, string detail)
        {
            check?.Invoke(passed, name, detail);
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
        static string FileAt(string directory, string name)
        {
            if (string.IsNullOrEmpty(directory)) return null;
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, name);
        }
        static Color32[] ReadTexture(RenderTexture target, string path)
        {
            if (target == null || !target.IsCreated()) throw new InvalidOperationException("Production portal texture is not created");
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                if (path != null) File.WriteAllBytes(path, pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }
            finally { RenderTexture.active = previous; if (pixels != null) Destroy(pixels); }
        }
        static Color32[] RenderCamera(Camera camera, string path)
        {
            if (camera == null || !camera.enabled) throw new InvalidOperationException("Actual Beach view is unavailable");
            var oldTarget = camera.targetTexture;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try { camera.targetTexture = target; camera.Render(); return ReadTexture(target, path); }
            finally { camera.targetTexture = oldTarget; target.Release(); Destroy(target); }
        }
        static float Mean(Color32[] pixels)
        {
            double total = 0;
            foreach (var p in pixels) total += (.2126 * p.r + .7152 * p.g + .0722 * p.b) / 255;
            return (float)(total / pixels.Length);
        }
        static float Deviation(Color32[] pixels)
        {
            double total = 0, square = 0;
            foreach (var p in pixels)
            {
                double value = (.2126 * p.r + .7152 * p.g + .0722 * p.b) / 255;
                total += value; square += value * value;
            }
            double mean = total / pixels.Length;
            return (float)Math.Sqrt(Math.Max(0, square / pixels.Length - mean * mean));
        }
    }
}
