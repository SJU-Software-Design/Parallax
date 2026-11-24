using System;
using System.IO;
using GlassTrial;
using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class HelpHudRenderChecks
    {
        static void Require(bool ok, string message) { if (!ok) throw new Exception("HELP_HUD_RENDER_FAILED: " + message); }
        public static void Run()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-help-hud-snapshot-dir");
            Require(index >= 0 && index + 1 < args.Length && Path.IsPathRooted(args[index + 1]), "absolute snapshot directory required");
            string path = args[index + 1]; Directory.CreateDirectory(path);
            var root = new GameObject("Help HUD render test");
            var panel = new GameObject("Display panel", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            try
            {
                var pages = root.AddComponent<HelpHudFaces>(); pages.Initialize(panel.transform, font);
                var state = new HelpHudState(); state.Open(); state.Tick(HelpHudState.ExpandSeconds);
                Capture(pages, state, Path.Combine(path, "help-hud-front.png"));
                Require(state.Turn(1), "begin page turn"); state.Tick(HelpHudState.TurnSeconds * .27f);
                Capture(pages, state, Path.Combine(path, "help-hud-turn.png"));
                state.Tick(HelpHudState.TurnSeconds); Require(state.Page == 1, "mouse page reached");
                Capture(pages, state, Path.Combine(path, "help-hud-mouse.png"));
                Require(state.Turn(1), "turn to reset page"); state.Tick(HelpHudState.TurnSeconds);
                Require(state.Page == 2, "reset page reached");
                Capture(pages, state, Path.Combine(path, "help-hud-reset.png"));
                Debug.Log("HELP_HUD_RENDER_TESTS_PASSED");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); if (font != null) UnityEngine.Object.DestroyImmediate(font); }
        }
        static void Capture(HelpHudFaces pages, HelpHudState state, string path)
        {
            pages.Render(state); Canvas.ForceUpdateCanvases();
            Camera camera = pages.GetComponentInChildren<Camera>(true); Require(camera != null && pages.Texture != null, "camera and target");
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = pages.Texture;
            var pixels = new Texture2D(pages.Texture.width, pages.Texture.height, TextureFormat.RGBA32, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0); pixels.Apply();
                int occupied = 0; foreach (var p in pixels.GetPixels32()) if (p.a > 16) occupied++;
                Require(occupied > 5000, "content camera produced too few visible pixels: " + occupied);
                File.WriteAllBytes(path, pixels.EncodeToPNG()); Debug.Log("HELP_HUD_CAPTURE " + path + " alphaPixels=" + occupied);
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
        }
    }
}
