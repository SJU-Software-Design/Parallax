using System.Collections;
using ShadeLink.Gallery;
using UnityEngine;
using UnityEngine.UI;

namespace GlassTrial
{
    // Stage announcements are separate from puzzle and exposure feedback.
    public sealed class GlassStageNotice : MonoBehaviour
    {
        public float displaySeconds = 3f;
        public float fadeSeconds = .25f;
        GalleryGame game;
        GlassPanelPreview preview;
        Canvas canvas;
        CanvasGroup group;
        RawImage backdrop;
        Text heading, room, objective, sensor;
        Material material;
        string lastRoom;
        float shownAt, alpha;
        bool ready;

        public static bool ReplacesHud(GalleryGame game)
        {
            if (game == null || !game.active || game.won || game.Transitioning || game.conceptCapture || GlassPanelPreview.ArrivalBlocksHud(game)) return false;
            var notice = game.GetComponent<GlassStageNotice>();
            return notice != null && notice.ready;
        }
        public static bool NeedsCapture(GalleryGame game)
        {
            if (!ReplacesHud(game)) return false;
            var notice = game.GetComponent<GlassStageNotice>();
            return notice.alpha > 0f || notice.lastRoom == null || notice.lastRoom != game.RoomTitle;
        }
        IEnumerator Start()
        {
            game = GetComponent<GalleryGame>(); preview = GetComponent<GlassPanelPreview>();
            yield return null;
            var shader = Resources.Load<Shader>("GlassPanel");
            if (preview == null || shader == null || !shader.isSupported) yield break;
            while (preview != null && preview.enabled && preview.Capture == null) yield return null;
            if (preview == null || preview.Capture == null) yield break;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var root = new GameObject("Stage announcement glass UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 18;
            group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var panel = Rect("Stage glass panel", root.transform, Vector2.zero, new Vector2(670, 150));
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f / 6f);
            panel.pivot = new Vector2(.5f, 0);
            backdrop = panel.gameObject.AddComponent<RawImage>(); backdrop.material = material; backdrop.color = new Color(1, 1, 1, .97f); backdrop.raycastTarget = false;
            Font font = game.font != null ? game.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            heading = Label(panel, font, 21, new Vector2(20, -11), new Vector2(630, 31));
            room = Label(panel, font, 17, new Vector2(20, -51), new Vector2(620, 30));
            objective = Label(panel, font, 17, new Vector2(20, -84), new Vector2(620, 49));
            sensor = Label(panel, font, 17, new Vector2(20, -88), new Vector2(510, 34));
            ready = true;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static Text Label(Transform parent, Font font, int size, Vector2 pos, Vector2 dimensions)
        {
            var rect = Rect("Label", parent, pos, dimensions); var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.color = Color.white; text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .65f); shadow.effectDistance = new Vector2(1, -1);
            return text;
        }
        void Update()
        {
            if (!ready) return;
            bool active = ReplacesHud(game);
            if (active && (lastRoom == null || lastRoom != game.RoomTitle)) { lastRoom = game.RoomTitle; shownAt = Time.unscaledTime; }
            bool requested = active && Time.unscaledTime - shownAt < Mathf.Max(0, displaySeconds);
            alpha = Mathf.MoveTowards(alpha, requested ? 1 : 0, Time.unscaledDeltaTime / Mathf.Max(.01f, fadeSeconds));
            // The hover prompt owns the same central area; do not draw over it.
            canvas.enabled = active && alpha > 0 && !GlassHoverPrompt.OccupiesCenter(game); group.alpha = alpha;
            if (!canvas.enabled) return;
            backdrop.texture = preview.Capture.blurred;
            float height = game.conceptLayout ? 150 : game.checkpoint == 1 ? 139 : 102;
            float width = game.conceptLayout ? 670 : 555;
            var panel = (RectTransform)backdrop.transform;
            panel.sizeDelta = new Vector2(width, height);
            float guideHeight = GlassExposureGauge.VisiblePanelHeight(game);
            panel.anchoredPosition = new Vector2(0, guideHeight * .5f + 12f);
            var canvasSize = ((RectTransform)canvas.transform).rect.size;
            // Keep the announcement above the guide and below the hover prompt.
            float maxHeight = Mathf.Max(1f, canvasSize.y / 3f - guideHeight * .5f - 145f);
            float scale = Mathf.Min(1f, Mathf.Max(1f, canvasSize.x - 56f) / width, maxHeight / height);
            panel.localScale = Vector3.one * scale;
            material.SetFloat("_GlassMode", 1); material.SetFloat("_PanelAspect", width / height);
            material.SetFloat("_Refraction", preview.refraction ? 1 : 0); material.SetFloat("_RefractionStrength", preview.refractionStrength);
            material.SetFloat("_RefractionWidth", preview.refractionWidth); material.SetFloat("_LensDistortion", preview.lensDistortion);
            material.SetFloat("_EdgeContrast", preview.edgeContrast ? 1 : 0); material.SetFloat("_EdgeContrastStrength", preview.edgeContrastStrength);
            material.SetFloat("_SpectrumPixels", preview.spectrumPixels); material.SetFloat("_WhiteTintStrength", preview.whiteTint); material.SetFloat("_MilkyTint", preview.milkyTint);
            heading.text = "PARALLAX v" + Application.version.Split('-')[0] + "  /  빛과 시선의 미술관";
            room.text = game.RoomTitle;
            objective.gameObject.SetActive(game.conceptLayout); objective.text = game.conceptLayout ? game.Objective : "";
            bool showSensor = game.checkpoint == 1 && game.lightGate != null;
            sensor.gameObject.SetActive(showSensor);
            if (showSensor) sensor.text = game.lightGate.illuminated ? "센서: 빛 감지 · 문 열림 · 그림자로 이동하세요" : "센서: 빛 없음 · 거울의 각도를 바꾸세요";
        }
        void OnDestroy() { ready = false; if (canvas != null) Destroy(canvas.gameObject); if (material != null) Destroy(material); }
    }
}
