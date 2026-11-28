using System;
using GlassTrial;
using TMPro;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Uses the product geometry path and real font metrics. No scene rendering,
    // screenshots, window resize, input injection, or personal settings changes.
    public static class HintPanelGeometryTests
    {
        [Serializable] sealed class HintFile { public HintEntry[] entries; }
        [Serializable] sealed class HintEntry { public string id; public string[] pages; }
        static int checks;
        static void Check(bool condition, string message)
        { checks++; if (!condition) throw new Exception("HINT_PANEL_GEOMETRY_FAILED: " + message); }
        static void Near(float actual, float expected, string message)
        { Check(Mathf.Abs(actual - expected) < .05f, message + " actual=" + actual + " expected=" + expected); }
        static readonly Vector2[] Displays = { new Vector2(1920, 1080), new Vector2(3440, 1440),
            new Vector2(3840, 1080), new Vector2(1280, 960) };

        public static void Run()
        {
            checks = 0;
            var source = Resources.Load<Font>("HelpHudFonts/NotoSansKR-Regular");
            Check(source != null, "bundled Korean source font exists");
            var font = TMP_FontAsset.CreateFontAsset(source, 48, 8,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            Check(font != null, "dynamic TMP font can be created");
            var node = new GameObject("Hint font geometry check", typeof(RectTransform), typeof(TextMeshProUGUI));
            var text = node.GetComponent<TextMeshProUGUI>();
            text.enabled = false; text.font = font; text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            try
            {
                var asset = Resources.Load<TextAsset>("HintText");
                Check(asset != null, "hint text resource exists");
                var data = JsonUtility.FromJson<HintFile>(asset.text);
                Check(data?.entries != null && data.entries.Length > 0, "real hint catalogue is nonempty");
                foreach (var display in Displays)
                {
                    var legacy = Matrix4x4.Scale(new Vector3(display.x / 1600f, display.y / 900f, 1));
                    CheckAnchoredPanel(display, legacy, 1570, 855, 520, 111);
                    CheckAnchoredPanel(display, legacy, 1570, 855, 520, 260);
                    // A very long future hint must shrink as one object instead
                    // of stretching glyphs or extending outside the viewport.
                    CheckFittedPanel(display, legacy, 4000, 4000, 520, 2400);
                    float eclipseScale = Mathf.Min(display.x / 1920f, display.y / 1080f);
                    var eclipse = Matrix4x4.TRS(new Vector3((display.x - 1920 * eclipseScale) * .5f,
                        (display.y - 1080 * eclipseScale) * .5f, 0), Quaternion.identity, Vector3.one * eclipseScale);
                    CheckAnchoredPanel(display, eclipse, 1884, 1026, 624, 160);
                    CheckAnchoredPanel(display, Matrix4x4.identity, display.x - 30, display.y - 50, 520, 160);
                    foreach (float textScale in new[] { .9f, 1f, 1.2f })
                    {
                        var measure = new GUIStyle { font = source, fontSize = Mathf.RoundToInt(19 * textScale), wordWrap = true };
                        foreach (float panelWidth in new[] { 520f, 624f })
                        {
                            // Gallery/Beach use a stretched 1600x900 HUD, while
                            // Eclipse uses its own uniformly letterboxed 1920x1080
                            // HUD. Measure every real hint at both actual widths.
                            bool eclipsePanel = panelWidth == 624f;
                            Matrix4x4 mapMatrix = eclipsePanel ? eclipse : legacy;
                            float right = eclipsePanel ? 1884 : 1570;
                            float bottom = eclipsePanel ? 1026 : 855;
                            foreach (var entry in data.entries)
                                foreach (string body in entry.pages)
                                {
                                    float bodyHeight = measure.CalcHeight(new GUIContent(body), panelWidth - 48);
                                    float height = Mathf.Max(82, bodyHeight + 85);
                                    float scale;
                                    Rect rect = HelpHudController.HintPanelScreenRect(mapMatrix, display, right, bottom, panelWidth, height, out scale);
                                    CheckInside(rect, display, entry.id + " width=" + panelWidth);
                                    text.fontSize = 19 * textScale * scale;
                                    Vector2 preferred = text.GetPreferredValues(body, rect.width - 48 * scale, float.PositiveInfinity);
                                    Check(preferred.y <= rect.height - 55 * scale + .5f,
                                        display + " " + entry.id + " " + textScale + " width=" + panelWidth + " text fits its actual TMP body area: " + preferred.y + "/" + (rect.height - 55 * scale));
                                    Check(42 * scale + preferred.y <= rect.height - 30 * scale + .5f,
                                        display + " " + entry.id + " width=" + panelWidth + " text stays above pagination");
                                }
                        }
                    }
                }
                Debug.Log("HINT_PANEL_GEOMETRY_TESTS_PASSED checks=" + checks + " displays=" + Displays.Length + " hintGroups=" + data.entries.Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(node);
                if (font != null)
                {
                    if (font.material != null) UnityEngine.Object.DestroyImmediate(font.material);
                    foreach (var atlas in font.atlasTextures) if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
                    UnityEngine.Object.DestroyImmediate(font);
                }
            }
        }
        static void CheckAnchoredPanel(Vector2 display, Matrix4x4 matrix, float right, float bottom, float width, float height)
        {
            float scale;
            Rect rect = HelpHudController.HintPanelScreenRect(matrix, display, right, bottom, width, height, out scale);
            Vector3 corner = matrix.MultiplyPoint3x4(new Vector3(right, bottom, 0));
            Near(rect.xMax, corner.x, display + " right anchor");
            Near(rect.yMax, corner.y, display + " bottom anchor");
            Near(rect.width / width, rect.height / height, display + " uniform panel scale");
            Near(rect.width / width, scale, display + " shared TMP scale");
            // The body and pagination coordinates used by EclipseHintOverlay
            // must remain inside the same uniformly scaled screen rectangle.
            Check(rect.xMin + 24 * scale >= 0 && rect.xMax - 24 * scale <= display.x, "text horizontal inset");
            Check(rect.yMax - 18 * scale >= 0 && rect.yMax - 10 * scale <= display.y, "pagination stays on screen");
            CheckInside(rect, display, "anchored panel");
        }
        static void CheckFittedPanel(Vector2 display, Matrix4x4 matrix, float right, float bottom, float width, float height)
        {
            float scale;
            Rect rect = HelpHudController.HintPanelScreenRect(matrix, display, right, bottom, width, height, out scale);
            CheckInside(rect, display, "oversize panel");
            Near(rect.width / width, rect.height / height, display + " fitted glyph scale is uniform");
            Check(scale > 0, "fitted text remains positive");
        }
        static void CheckInside(Rect rect, Vector2 display, string label)
        {
            Check(rect.xMin >= -.05f && rect.yMin >= -.05f && rect.xMax <= display.x + .05f && rect.yMax <= display.y + .05f,
                display + " " + label + " bounds=" + rect);
        }
    }
}