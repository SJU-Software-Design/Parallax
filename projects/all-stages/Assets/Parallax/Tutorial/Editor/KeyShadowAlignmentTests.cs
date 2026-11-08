using System;
using System.IO;
using System.Reflection;
using ShadeLink.Gallery;
using ShadeLink.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShadeLink.Editor
{
    public static class KeyShadowAlignmentTests
    {
        const string Request = "Temp/KeyShadowAlignment.verify";
        const string Result = "Temp/KeyShadowAlignment.verify.txt";
        const string PlayRequest = "Parallax.KeyShadowAlignment.PlayCheck";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly Vector3 PassageCenter = new Vector3(.15f, 3.71f, 0f);
        static double deadline;
        static bool started, sawExactPose;
        static int playCase;
        static KeyShadowTutorialController controller;
        static GalleryProp sculpture;

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (File.Exists(Request)) EditorApplication.delayCall += RunRequested;
            if (SessionState.GetBool(PlayRequest, false))
            {
                deadline = EditorApplication.timeSinceStartup + 30;
                EditorApplication.update -= CheckPlay;
                EditorApplication.update += CheckPlay;
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [MenuItem("Parallax/Verify key alignment geometry and motion")]
        public static void Run()
        {
            var root = new GameObject("Key alignment test root");
            var mesh = new GameObject("Key alignment test passage");
            var lamp = new GameObject("Key alignment test lamp");
            try
            {
                mesh.transform.SetParent(root.transform, false);
                mesh.transform.localPosition = -PassageCenter;
                lamp.transform.position = new Vector3(0, 0, -20);
                lamp.transform.rotation = Quaternion.Euler(30, 65, 42);
                var align = new KeyShadowAutoAlign(root.transform, lamp.transform, mesh.transform);
                Require(KeyShadowAutoAlign.AllowedAngle == 2f, "The tolerance is not 2 degrees.");
                foreach (float side in new[] { 0f, 180f })
                foreach (float degrees in new[] { 0f, 1.99f, 2f, 2.01f, 7f })
                {
                    align.Reset();
                    Quaternion pose = Quaternion.Euler(0, side + degrees, 0);
                    Quaternion output = align.Tick(pose, 1f / 60f);
                    bool inside = degrees <= 2f;
                    Require(align.IsAligning == inside, $"Boundary failed: side={side}, angle={degrees}.");
                    Require(Quaternion.Angle(output, pose) < .01f, "Capture jumped instead of animating.");
                    if (!inside) continue;
                    Quaternion halfway = align.Tick(output, .275f);
                    Require(align.IsAligning && !align.HasPendingCompletion, "Alignment completed too soon.");
                    if (degrees > 0)
                        Require(Quaternion.Angle(halfway, pose) > .1f, "No visible alignment motion.");
                    Quaternion final = align.Tick(halfway, .275f);
                    Require(align.HasPendingCompletion && align.NearestTargetAngle(final, out _) < .01f,
                        "The motion did not reach the passage axis.");
                    Require(align.TryConsumeCompletion(out _, out _) && !align.TryConsumeCompletion(out _, out _),
                        "Completion must be consumed exactly once.");
                }

                foreach (float roll in new[] { -179f, -45f, 0f, 90f, 359.9f })
                {
                    align.Reset();
                    Quaternion rolled = Quaternion.AngleAxis(roll, Vector3.forward);
                    align.Tick(Quaternion.AngleAxis(1.5f, Vector3.up) * rolled, 0f);
                    Quaternion final = align.Tick(Quaternion.AngleAxis(1.5f, Vector3.up) * rolled, .6f);
                    Require(Quaternion.Angle(final, rolled) < .02f, "Alignment changed the free roll.");
                }

                align.Reset();
                Quaternion near = Quaternion.Euler(0, 1.5f, 0);
                align.Tick(near, 0f);
                align.Tick(near, .55f);
                align.RejectAlignmentStep(near);
                Require(!align.TryConsumeCompletion(out _, out _) && align.IsAligning,
                    "A collision-rejected final pose completed the puzzle.");
                Quaternion accepted = align.Tick(near, .55f);
                Require(align.TryConsumeCompletion(out _, out _) && align.NearestTargetAngle(accepted, out _) < .01f,
                    "Alignment could not resume after a blocked step.");
                align.Reset();
                Require(!align.IsAligning && !align.HasPendingCompletion, "Reset left a pending success.");

                CheckScenePassage();
                CheckSolidBlocker();
                KeyShadowPassageVariants.VerifyGeometry();
                File.WriteAllText(Result, "EDIT_OK: 2-degree assist, both faces, smooth capture, roll, blocked final step, reset, real oblique passage, solid blocker rejected.\n");
                Debug.Log("KEY_ALIGNMENT_EDIT_OK");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(lamp);
            }
        }

        static void CheckScenePassage()
        {
            var source = GameObject.Find("ShadowKey - Branch Sculpture Preview");
            var lamp = GameObject.Find("Only light source · modern wall lamp on right");
            Require(source != null && lamp != null, "Open KeyShadowPreview to check its imported mesh.");
            var sourceMesh = source.GetComponentInChildren<MeshFilter>();
            var root = new GameObject("Imported passage test");
            var visual = new GameObject("Imported passage test mesh");
            try
            {
                root.transform.SetPositionAndRotation(source.transform.position, Quaternion.Euler(-16.924f, 101.771f, 16.816f));
                root.transform.localScale = source.transform.lossyScale;
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = sourceMesh.transform.localPosition;
                visual.transform.localRotation = sourceMesh.transform.localRotation;
                visual.transform.localScale = sourceMesh.transform.localScale;
                visual.AddComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
                var collider = visual.AddComponent<MeshCollider>();
                collider.sharedMesh = sourceMesh.sharedMesh;
                using var align = new KeyShadowAutoAlign(root.transform, lamp.transform, visual.transform);
                float before = align.NearestTargetAngle(root.transform.rotation, out _);
                Require(before < 2f, $"The reported near-key pose is still rejected ({before} degrees).");
                align.Tick(root.transform.rotation, 0f);
                Quaternion final = align.Tick(root.transform.rotation, .6f);
                root.transform.rotation = final;
                Vector3 hole = visual.transform.TransformPoint(PassageCenter);
                Vector3 ray = hole - lamp.transform.position;
                float angle = Vector3.Angle(visual.transform.forward, ray);
                Require(Mathf.Min(angle, 180f - angle) < .02f, "The solved passage is not parallel to the actual light ray.");
                Physics.SyncTransforms();
                Require(!collider.Raycast(new Ray(lamp.transform.position, ray.normalized), out _, ray.magnitude + 4f),
                    "The success ray hits the real sculpture mesh instead of passing through the hole.");
                File.WriteAllText("Temp/KeyShadowAlignment.geometry.txt", $"Near pose={before:F4} deg\nAligned root={final.eulerAngles:F5}\nPhysical passage error={Mathf.Min(angle, 180f - angle):F5} deg\nMesh ray: clear\n");
                root.transform.rotation = Quaternion.Euler(22.598f, -79.588f, -17.445f);
                align.Reset();
                float oblique = align.NearestTargetAngle(root.transform.rotation, out _);
                bool canCapture = align.CanCapture(root.transform.rotation);
                File.AppendAllText("Temp/KeyShadowAlignment.geometry.txt", $"Oblique diagnostic: axis={oblique:F5}, accepted={canCapture}, passage={align.PassageIsClear}, localSource={visual.transform.InverseTransformPoint(lamp.transform.position):F6}\n");
                Require(oblique > 5f && canCapture && align.PassageIsClear,
                    $"The photographed 5.8-degree pose is rejected (axis={oblique}, capture={canCapture}, clear={align.PassageIsClear}).");
                File.AppendAllText("Temp/KeyShadowAlignment.geometry.txt", $"Photographed pose: axis {oblique:F4} deg, real mesh passage clear and accepted.\n");
                root.transform.rotation = Quaternion.Euler(-11.873f, 50.006f, -16.429f);
                align.Reset();
                Require(!align.CanCapture(root.transform.rotation), "The original blocked sculpture pose was accepted.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void CheckSolidBlocker()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var lamp = new GameObject("Solid passage test lamp");
            Mesh solid = Object.Instantiate(root.GetComponent<MeshFilter>().sharedMesh);
            try
            {
                var vertices = solid.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] += PassageCenter;
                solid.vertices = vertices;
                solid.RecalculateBounds();
                root.GetComponent<MeshFilter>().sharedMesh = solid;
                root.transform.position = -PassageCenter;
                lamp.transform.position = new Vector3(0, 0, -20);
                using var align = new KeyShadowAutoAlign(root.transform, lamp.transform, root.transform);
                Require(align.NearestTargetAngle(root.transform.rotation, out _) < .01f,
                    "Solid-blocker control is not axis-aligned.");
                Require(!align.CanCapture(root.transform.rotation),
                    "A solid mesh was accepted just because its axis was aligned.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(lamp);
                Object.DestroyImmediate(solid);
            }
        }

        static void RunRequested()
        {
            File.Delete(Request);
            try
            {
                Run();
                SessionState.SetBool(PlayRequest, true);
                EditorApplication.update -= CheckPlay;
                EditorApplication.update += CheckPlay;
                deadline = EditorApplication.timeSinceStartup + 40;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error)
            {
                File.AppendAllText(Result, "FAIL: " + error + "\n");
                Debug.LogException(error);
            }
        }

        public static void RunAll() => RunRequested();

        static object Field(object instance, string name) => instance.GetType().GetField(name, Private).GetValue(instance);
        static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Private).SetValue(instance, value);
        static void Call(object instance, string name, params object[] values) => instance.GetType().GetMethod(name, Private).Invoke(instance, values);

        static void CheckPlay()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Play-mode alignment did not finish.");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (controller == null) controller = Object.FindFirstObjectByType<KeyShadowTutorialController>();
                if (controller == null || Field(controller, "interaction") == null) return;
                if (!started)
                {
                    started = true;
                    var gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
                    if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
                    sculpture = (GalleryProp)Field(controller, "sculpture");
                    Require(sculpture != null, "The playable sculpture is missing.");
                    var passageSettings = Resources.Load<KeyShadowPassageSettings>(KeyShadowPassageSettings.ResourcePath);
                    if (passageSettings != null)
                    {
                        Require(sculpture.GetComponentInChildren<MeshFilter>().sharedMesh == passageSettings.SelectedMesh,
                            "The active passage test was not applied to the rendered sculpture.");
                        File.AppendAllText(Result, $"ACTIVE_PASSAGE: {controller.PassageTestLabel}\n");
                    }
                    controller.Controller.enabled = false;
                    controller.Controller.transform.position = sculpture.transform.position + new Vector3(4f, -sculpture.transform.position.y + .03f, -1f);
                    controller.Controller.enabled = true;
                    Vector3 look = (sculpture.transform.position - controller.Camera.transform.position).normalized;
                    Set(controller, "yaw", Mathf.Atan2(look.x, look.z));
                    Set(controller, "pitch", Mathf.Asin(look.y));
                    Call(controller, "SyncView");
                    Call(controller, "SetPaused", false);
                    Physics.SyncTransforms();
                    var interaction = (ObjectInteraction<GalleryProp>)Field(controller, "interaction");
                    if (playCase == 1 || playCase == 3)
                    {
                        interaction.Interact();
                        Require(controller.Held == sculpture, "The real interaction could not pick up the sculpture.");
                    }
                    Quaternion photographed = Quaternion.Euler(22.598f, -79.588f, -17.445f);
                    bool narrow = passageSettings != null && passageSettings.useNarrowPassage;
                    if (narrow)
                    {
                        Quaternion sideGap = Quaternion.Euler(-11.873f, 69.006f, -16.429f);
                        if (playCase == 3) interaction.Rotation = sideGap;
                        else sculpture.transform.rotation = sideGap;
                        Physics.SyncTransforms();
                        Call(controller, "Update");
                        Require(!((KeyShadowAutoAlign)Field(controller, "autoAlign")).IsAligning &&
                            !(bool)Field(controller, "swapping"), "A slight turn into the remote side gap started success.");
                        File.AppendAllText(Result, $"PLAY_BAD_POSE_REJECTED {playCase + 1}: manual-play side gap does not capture.\n");
                    }
                    Quaternion valid = narrow ? Quaternion.Euler(-16.924f, 101.771f, 16.816f) : photographed;
                    if (playCase == 3) interaction.Rotation = valid;
                    else sculpture.transform.rotation = playCase == 2
                        ? Quaternion.Euler(343.33970f, 101.07150f, 17.04333f) : valid;
                    Physics.SyncTransforms();
                    string label = playCase == 0 ? "unheld near-solution pose" : playCase == 1
                        ? "numeric Transform edit while held" : playCase == 2 ? "unheld numeric exact angle" : "normal held rotation";
                    File.AppendAllText(Result, $"PLAY_STARTED {playCase + 1}: {label}.\n");
                    if (playCase == 0)
                    {
                        Call(controller, "SetPaused", true);
                        Call(controller, "Update");
                        Require(!((KeyShadowAutoAlign)Field(controller, "autoAlign")).IsAligning,
                            "Changing a numeric pose bypassed pause.");
                        Call(controller, "SetPaused", false);
                    }
                    Call(controller, "Update");
                    Require(((KeyShadowAutoAlign)Field(controller, "autoAlign")).IsAligning,
                        "The numeric/manual pose did not start alignment on the first active frame.");
                }
                Call(controller, "SetPaused", false);
                Require(Cursor.lockState == CursorLockMode.Locked,
                    "The Game view must have focus for the real player controller to run.");
                // Drive the real controller even if the Game view loses focus to
                // the editor. All motion, collision and reward code stays intact.
                Call(controller, "Update");
                if ((bool)Field(controller, "swapping") && sculpture.gameObject.activeInHierarchy)
                {
                    var align = (KeyShadowAutoAlign)Field(controller, "autoAlign");
                    sawExactPose |= align.NearestTargetAngle(sculpture.transform.rotation, out _) < .02f;
                }
                var key = GameObject.Find("Matched silver key · reward");
                if (key == null || sculpture.gameObject.activeInHierarchy || (bool)Field(controller, "swapping")) return;
                Require(sawExactPose, "The reward appeared without first reaching the exact passage angle.");
                Require(key.GetComponent<GalleryProp>() != null && key.GetComponent<Collider>() != null,
                    "The reward is missing interaction or collision.");
                var clip = (AudioClip)Field(controller, "alignmentDing");
                var audio = (AudioSource)Field(controller, "alignmentAudio");
                Require(clip != null && clip.samples > 0 && audio != null && audio.spatialBlend == 0,
                    "The alignment bell was not created or cannot be heard consistently.");
                var samples = new float[clip.samples];
                clip.GetData(samples, 0);
                float peak = 0f;
                foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                Require(peak > .1f && peak <= 1f, "The bell is silent or clips.");
                File.AppendAllText(Result, $"PLAY_OK {playCase + 1}: exact alignment, bell (peak {peak:F3}), flash, interactive reward, controls unlocked.\n");
                if (++playCase < 4)
                {
                    started = sawExactPose = false;
                    controller = null;
                    sculpture = null;
                    deadline = EditorApplication.timeSinceStartup + 25;
                    EditorSceneManager.LoadSceneInPlayMode("Assets/Parallax/Tutorial/Scenes/KeyShadowPreview.unity",
                        new LoadSceneParameters(LoadSceneMode.Single));
                    return;
                }
                File.AppendAllText(Result, "ALL_PLAY_OK: unheld screenshot, held Inspector edit, unheld exact numeric pose, normal rotation, pause/resume.\n");
                FinishPlay();
                Debug.Log("KEY_ALIGNMENT_PLAY_OK");
            }
            catch (Exception error)
            {
                File.AppendAllText(Result, "PLAY_FAIL: " + error + "\n");
                Debug.LogException(error);
                FinishPlay();
            }
        }

        static void FinishPlay()
        {
            EditorApplication.update -= CheckPlay;
            SessionState.EraseBool(PlayRequest);
            started = sawExactPose = false;
            playCase = 0;
            controller = null;
            sculpture = null;
            EditorApplication.ExitPlaymode();
        }
    }
}
