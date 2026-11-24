using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace GlassTrial
{
    // Only enabled by an explicit validation flag in an isolated player build.
    public sealed class HelpHudVideoValidation : MonoBehaviour
    {
        static readonly string[] Names = { "move.mp4", "mouse.mp4", "reset.mp4" };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-help-hud-video-check") < 0) return;
            var root = new GameObject("Help HUD video validation"); DontDestroyOnLoad(root);
            root.AddComponent<HelpHudVideoValidation>();
        }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-help-hud-check-output");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
            { Debug.LogError("HELP_HUD_VIDEO_FAILED: absolute output directory required"); Application.Quit(1); yield break; }
            string directory = args[index + 1]; Directory.CreateDirectory(directory);
            var player = gameObject.AddComponent<VideoPlayer>(); player.playOnAwake = false;
            player.source = VideoSource.Url; player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.None; player.isLooping = true; player.waitForFirstFrame = true;
            var target = new RenderTexture(608, 210, 0, RenderTextureFormat.ARGB32); target.Create(); player.targetTexture = target;
            bool failed = false; string error = null; bool looped = false;
            player.errorReceived += (p, message) => { failed = true; error = message; };
            player.loopPointReached += p => looped = true;
            foreach (string name in Names)
            {
                player.Stop(); player.url = Path.Combine(Application.streamingAssetsPath, "HelpHudVideos", name);
                failed = false; error = null; looped = false; player.Prepare();
                float deadline = Time.realtimeSinceStartup + 20;
                while (!player.isPrepared && !failed && Time.realtimeSinceStartup < deadline) yield return null;
                if (!player.isPrepared || failed) { Debug.LogError("HELP_HUD_VIDEO_FAILED prepare " + name + ": " + error); failed = true; break; }
                player.Play(); deadline = Time.realtimeSinceStartup + 12;
                while (player.frame < 10 && !failed && Time.realtimeSinceStartup < deadline) yield return null;
                if (player.frame < 10 || failed) { Debug.LogError("HELP_HUD_VIDEO_FAILED decode " + name + ": " + error); failed = true; break; }
                var old = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(608, 210, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 608, 210), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(directory, Path.GetFileNameWithoutExtension(name) + "-unity.png"), image.EncodeToPNG());
                Destroy(image); RenderTexture.active = old;
                while (!looped && !failed && Time.realtimeSinceStartup < deadline) yield return null;
                if (!looped || failed) { Debug.LogError("HELP_HUD_VIDEO_FAILED loop " + name + ": " + error); failed = true; break; }
                Debug.Log("HELP_HUD_VIDEO_PASSED " + name + " duration=" + player.length);
            }
            player.Stop(); player.targetTexture = null; target.Release(); Destroy(target);
            if (!failed) yield return CheckIntegratedPages(directory, message => { failed = true; error = message; });
            if (failed && error != null) Debug.LogError("HELP_HUD_VIDEO_FAILED " + error);
            if (!failed) Debug.Log("HELP_HUD_VIDEOS_ALL_PASSED");
            Application.Quit(failed ? 1 : 0);
        }
        static IEnumerator CheckIntegratedPages(string directory, Action<string> fail)
        {
            var root = new GameObject("Help HUD integrated video probe");
            var display = new GameObject("Display", typeof(RectTransform)); display.transform.SetParent(root.transform, false);
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            var faces = root.AddComponent<HelpHudFaces>(); faces.Initialize(display.transform, font);
            var state = new HelpHudState(); state.Open(); state.Tick(HelpHudState.ExpandSeconds);
            var videos = root.GetComponents<VideoPlayer>();
            for (int i = 0; i < Names.Length; i++)
            {
                if (i > 0) { if (!state.Turn(1)) { fail("integrated page turn"); break; } state.Tick(HelpHudState.TurnSeconds); }
                float deadline = Time.realtimeSinceStartup + 15;
                do { faces.Render(state); yield return null; }
                while ((!videos[i].isPrepared || !videos[i].isPlaying || videos[i].frame < 10) && Time.realtimeSinceStartup < deadline);
                if (!videos[i].isPrepared || !videos[i].isPlaying || videos[i].frame < 10)
                { fail("integrated playback " + Names[i]); break; }
                Canvas.ForceUpdateCanvases(); var camera = root.GetComponentInChildren<Camera>(true); camera.Render();
                var old = RenderTexture.active; RenderTexture.active = faces.Texture;
                var image = new Texture2D(faces.Texture.width, faces.Texture.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(directory, "page-" + (i + 1) + "-integrated.png"), image.EncodeToPNG());
                Destroy(image); RenderTexture.active = old;
                Debug.Log("HELP_HUD_INTEGRATED_PAGE_PASSED " + Names[i]);
            }
            state.Close(); state.Tick(HelpHudState.CollapseSeconds); faces.Render(state);
            state.Open(); state.Tick(HelpHudState.ExpandSeconds);
            for (int i = 0; i < Names.Length; i++)
            {
                if (i > 0) { state.Turn(1); state.Tick(HelpHudState.TurnSeconds); }
                float deadline = Time.realtimeSinceStartup + 15;
                do { faces.Render(state); yield return null; }
                while ((!videos[i].isPrepared || !videos[i].isPlaying || videos[i].frame < 10) && Time.realtimeSinceStartup < deadline);
                if (!videos[i].isPrepared || !videos[i].isPlaying || videos[i].frame < 10)
                { fail("reopened playback " + Names[i]); break; }
                long firstFrame = videos[i].frame;
                do { faces.Render(state); yield return null; }
                while (videos[i].frame == firstFrame && Time.realtimeSinceStartup < deadline);
                if (videos[i].frame == firstFrame) { fail("reopened video stuck " + Names[i]); break; }
                Debug.Log("HELP_HUD_REOPEN_PAGE_PASSED " + Names[i]);
            }
            Destroy(root); if (font != null) Destroy(font);
        }
    }
}
