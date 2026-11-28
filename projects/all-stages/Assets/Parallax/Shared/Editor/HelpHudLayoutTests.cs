using System;
using GlassTrial;
using UnityEngine;
using UnityEngine.UI;

namespace ShadeLink.Editor
{
    public static class HelpHudLayoutTests
    {
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("HELP_HUD_LAYOUT_FAILED: " + message); }

        public static void Run()
        {
            foreach (var size in new[] { new Vector2(1920, 1080), new Vector2(3440, 1440),
                new Vector2(3840, 1080), new Vector2(1280, 960) })
                CheckDisplay(size);
            Debug.Log("HELP_HUD_LAYOUT_TESTS_PASSED");
        }

        static void CheckDisplay(Vector2 size)
        {
            var root = new GameObject("Help layout canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var cameraRoot = new GameObject("Help layout camera", typeof(Camera));
            RenderTexture virtualDisplay = null;
            try
            {
                // A real canvas and camera supply projected geometry for a virtual
                // display. No window resize, rendering, or pixel readback is needed.
                var camera = cameraRoot.GetComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = size.y * .5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
                // Native batch mode clamps an untargeted camera to its 640x480
                // display. A blank target supplies the virtual display dimensions;
                // Unity may allocate it on assignment, but nothing is rendered or read.
                virtualDisplay = new RenderTexture((int)size.x, (int)size.y, 0);
                camera.targetTexture = virtualDisplay; camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = size.x / size.y;
                Check(Vector2.Distance(camera.pixelRect.size, size) < .01f,
                    size + " camera viewport must match the virtual display: " + camera.pixelRect);
                camera.transform.position = new Vector3(0, 0, -10);
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                float scale = Mathf.Pow(2, Mathf.Lerp(Mathf.Log(size.x / 1600f, 2), Mathf.Log(size.y / 900f, 2), .5f));
                canvas.scaleFactor = scale;
                var canvasRect = root.GetComponent<RectTransform>();
                canvasRect.sizeDelta = size / scale; canvasRect.localScale = Vector3.one * scale;
                var panelRoot = new GameObject("Help layout panel", typeof(RectTransform));
                panelRoot.transform.SetParent(root.transform, false);
                var panel = panelRoot.GetComponent<RectTransform>();
                panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f / 6f);
                panel.pivot = new Vector2(.5f, .5f); panel.sizeDelta = new Vector2(760, 454);
                panel.anchoredPosition = new Vector2(0, 200);
                Canvas.ForceUpdateCanvases();
                // A WorldSpace CanvasScaler updates only pixel density. Restore
                // the display scale represented by this test canvas explicitly.
                canvas.scaleFactor = scale;
                var matrix = HelpHudController.PanelGuiMatrix(panel, canvas, size.y);
                var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                var design = new[] { new Vector3(-380, 227), new Vector3(-380, -227),
                    new Vector3(380, -227), new Vector3(380, 227) };
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 projected = camera.WorldToScreenPoint(corners[i]);
                    var actual = new Vector2(projected.x, size.y - projected.y);
                    Vector3 transformed = matrix.MultiplyPoint3x4(design[i]);
                    Check(Vector2.Distance(actual, new Vector2(transformed.x, transformed.y)) < .05f,
                        size + " panel corner " + i + " actual=" + actual.ToString("F5") +
                        " gui=" + transformed.ToString("F5") + " scale=" + scale + " canvasScale=" + canvas.scaleFactor +
                        " pixelRect=" + camera.pixelRect + " aspect=" + camera.aspect + " ortho=" + camera.orthographicSize +
                        " canvasRect=" + canvasRect.rect + " localScale=" + canvasRect.localScale +
                        " panelRect=" + panel.rect + " worldCorner=" + corners[i].ToString("F5") + " matrix=" + matrix);
                }
                Check(Mathf.Abs(matrix.m00 - scale) < .0001f && Mathf.Abs(matrix.m11 - scale) < .0001f,
                    size + " text and buttons must use the canvas display scale");
            }
            finally
            {
                cameraRoot.GetComponent<Camera>().targetTexture = null;
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraRoot);
                if (virtualDisplay != null) { virtualDisplay.Release(); UnityEngine.Object.DestroyImmediate(virtualDisplay); }
            }
        }
    }
}