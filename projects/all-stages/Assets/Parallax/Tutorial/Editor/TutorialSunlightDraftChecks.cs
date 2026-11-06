using System;
using System.IO;
using System.Reflection;
using ShadeLink.Tutorial;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class TutorialSunlightDraftChecks
    {
        const string Key = "Parallax.SunlightDraft.Build";
        const string Passed = Key + ".Passed";
        const string Scene = "Assets/Parallax/Tutorial/Scenes/Tutorial.unity";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Call(TutorialGame game, string method, params object[] args) => typeof(TutorialGame).GetMethod(method, Flags).Invoke(game, args);
        static T Get<T>(TutorialGame game, string field) => (T)typeof(TutorialGame).GetField(field, Flags).GetValue(game);
        static void Set(TutorialGame game, string field, object value) => typeof(TutorialGame).GetField(field, Flags).SetValue(game, value);
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        [MenuItem("Parallax/튜토리얼 햇빛 초안 검사 및 macOS 빌드")]
        public static void Build()
        {
            TutorialLandingFeedbackTests.Run();
            TutorialBuild.Prepare();
            EditorSceneManager.OpenScene(Scene);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Passed, false);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= CheckWhenPlaying;
            EditorApplication.update += CheckWhenPlaying;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void CheckWhenPlaying()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            var game = UnityEngine.Object.FindFirstObjectByType<TutorialGame>();
            if (game == null || game.Camera == null) return;
            EditorApplication.update -= CheckWhenPlaying;
            try
            {
                Verify(game);
                SessionState.SetBool(Passed, true);
                Debug.Log("TUTORIAL_SUNLIGHT_SMOKE_OK shelters, exposed gaps, three-die route, checkpoints, preserved poses and natural handoff");
            }
            catch (Exception exception) { Debug.LogException(exception); }
            EditorApplication.ExitPlaymode();
        }

        static void Verify(TutorialGame game)
        {
            Call(game, "StartTutorialStage", 3);
            Require(Get<bool>(game, "inSunlightRoom") && game.Props.Length == 3, "Stage 3 did not expose three dice");
            var type = typeof(TutorialGame);
            var origin = (Vector3)type.GetField("SunlightOrigin", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var stops = (Vector3[])type.GetField("SunlightStops", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            bool Safe(Vector3 local) => (bool)Call(game, "SunlightSafe", origin + local);
            Physics.SyncTransforms();
            foreach (var stop in stops) Require(Safe(stop), "A fixed shelter is not shaded: " + stop);
            Require(!Safe(new Vector3(10.5f, .03f, 9.5f)), "First sunlight gap can be crossed in permanent shade");
            Require(!Safe(new Vector3(21f, .03f, 9.5f)), "Second sunlight gap is permanently covered");
            Capture(game, "01-stage3-entry", origin + stops[0] + Vector3.up * 1.65f,
                origin + new Vector3(17, 1.2f, 9.5f));

            var poses = new[] { new Vector3(12.5f, 2.508f, 4.8f), new Vector3(23.5f, 2.808f, 4.4f), new Vector3(33, 4.008f, 14) };
            var sizes = new[] { 5f, 5.6f, 8f };
            for (int i = 0; i < game.Props.Length; i++)
            {
                var prop = game.Props[i];
                prop.transform.SetPositionAndRotation(origin + poses[i], Quaternion.identity);
                prop.transform.localScale = Vector3.one * sizes[i];
                prop.body.position = prop.transform.position;
                prop.body.rotation = Quaternion.identity;
            }
            Physics.SyncTransforms();
            foreach (var prop in game.Props) prop.placement.ResetPlaced();
            Capture(game, "02-grown-dice-shadow-route", origin + new Vector3(5.8f, 1.68f, 11.8f),
                origin + new Vector3(13.5f, .7f, 7));
            Capture(game, "03-corner-to-exit", origin + new Vector3(26.5f, 1.68f, 10),
                origin + new Vector3(27, 1.2f, 28));
            void RoutePoint(Vector3 local)
            {
                Require(Safe(local), "Example route has a sunlit gap at " + local);
                Vector3 feet = origin + local;
                Require(!Physics.CheckCapsule(feet + Vector3.up * .32f, feet + Vector3.up * 1.28f, .28f,
                    1 << 0, QueryTriggerInteraction.Ignore), "Example route is blocked at " + local);
            }
            for (float x = 2.2f; x <= 27f; x += .2f) RoutePoint(new Vector3(x, .03f, 9.5f));
            for (float z = 9.5f; z <= 31.5f; z += .2f) RoutePoint(new Vector3(27, .03f, z));

            void Move(Vector3 local)
            {
                game.Controller.enabled = false;
                game.Controller.transform.position = origin + local;
                game.Controller.enabled = true;
                Physics.SyncTransforms();
            }
            Move(stops[1]); Call(game, "TickSunlight", .02f);
            Require(Get<int>(game, "sunlightCheckpoint") == 1, "First rest bay did not save a checkpoint");
            Vector3 placed = game.Props[0].transform.position;
            Set(game, "sunlightExposure", 1f);
            Move(new Vector3(7.7f, .03f, 12.5f));
            Call(game, "RespawnSunlight");
            Require(Vector3.Distance(game.Controller.transform.position, origin + stops[1]) < .05f &&
                Vector3.Distance(game.Props[0].transform.position, placed) < .001f, "Respawn lost the checkpoint or placed die");
            Move(new Vector3(27, .03f, 31.1f)); Call(game, "TickSunlight", .02f);
            Require(Get<bool>(game, "sunlightComplete"), "Walking through the exit did not complete stage 3");

            Call(game, "StartTutorialStage", 2);
            float doorX = (float)type.GetField("ClimbWallX", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            game.Controller.enabled = false;
            game.Controller.transform.position = new Vector3(doorX + .9f, origin.y + .03f, 4.5f);
            game.Controller.enabled = true;
            Set(game, "yaw", .68f); Set(game, "pitch", -.12f); Call(game, "SyncView");
            Quaternion view = game.Camera.transform.rotation;
            Call(game, "UpdateExit", .02f);
            Require(Get<bool>(game, "inSunlightRoom") && Quaternion.Angle(view, game.Camera.transform.rotation) < .001f,
                "Natural stage 2 exit changed the view or did not enter stage 3");
            Call(game, "StartTutorialStage", 1);
            Require(!Get<bool>(game, "inSunlightRoom") && game.Props.Length == 1 && !Get<Light>(game, "sunlight").enabled,
                "Stage 1 restart retained stage 3 state");
        }

        static void Capture(TutorialGame game, string name, Vector3 position, Vector3 target)
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-tutorialCaptureDir");
            if (at < 0) return;
            if (at + 1 >= args.Length || !Path.IsPathRooted(args[at + 1]))
                throw new ArgumentException("-tutorialCaptureDir requires an absolute output directory");
            string output = args[at + 1]; Directory.CreateDirectory(output);
            var camera = game.Camera; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            Vector3 oldPosition = camera.transform.position; Quaternion oldRotation = camera.transform.rotation;
            float oldAspect = camera.aspect;
            var render = new RenderTexture(1600, 900, 24);
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
                camera.aspect = 1600f / 900f; camera.targetTexture = render;
                camera.Render(); RenderTexture.active = render;
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                string path = Path.Combine(output, name + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("TUTORIAL_SUNLIGHT_CAPTURE_OK " + path);
            }
            finally
            {
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation); camera.aspect = oldAspect;
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                render.Release(); UnityEngine.Object.DestroyImmediate(render); UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            SessionState.EraseBool(Key);
            bool passed = SessionState.GetBool(Passed, false);
            SessionState.EraseBool(Passed);
            EditorApplication.delayCall += () =>
            {
                if (!passed) { if (Application.isBatchMode) EditorApplication.Exit(1); return; }
                string oldName = PlayerSettings.productName;
                string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
                try
                {
                    PlayerSettings.productName = "Parallax - Tutorial Sunlight Draft";
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.parallax.tutorial.sunlightdraft");
                    string output = Parallax.Editor.BuildPaths.Output("Parallax-Tutorial-Sunlight-Draft.app");
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    var report = BuildPipeline.BuildPlayer(new[] { Scene }, output, BuildTarget.StandaloneOSX, BuildOptions.Development);
                    Debug.Log("TUTORIAL_SUNLIGHT_BUILD " + report.summary.result + " errors=" + report.summary.totalErrors + " output=" + output);
                    if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
                }
                catch (Exception exception) { Debug.LogException(exception); if (Application.isBatchMode) EditorApplication.Exit(1); }
                finally
                {
                    PlayerSettings.productName = oldName;
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, oldId);
                }
            };
        }
    }
}
