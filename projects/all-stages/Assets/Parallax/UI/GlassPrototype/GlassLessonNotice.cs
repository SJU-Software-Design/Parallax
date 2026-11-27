using System.Collections;
using ShadeLink;
using ShadeLink.Gallery;
using UnityEngine;
using UnityEngine.UI;

namespace GlassTrial
{
    // Gallery-only presentation for the shared placement lesson.
    public sealed class GlassLessonNotice : MonoBehaviour
    {
        public float fadeSeconds = .25f;
        GalleryGame game;
        GlassPanelPreview preview;
        PlacementLesson lesson;
        Canvas canvas;
        CanvasGroup group;
        RawImage backdrop;
        Text title, detail, status;
        Material material;
        bool ready, shownExpanded;
        float alpha;

        public static bool NeedsCapture(GalleryGame game)
        {
            if (game == null) return false;
            var notice = game.GetComponent<GlassLessonNotice>();
            return notice != null && notice.ready && notice.alpha > 0;
        }
        IEnumerator Start()
        {
            game = GetComponent<GalleryGame>(); preview = GetComponent<GlassPanelPreview>();
            yield return null;
            var shader = Resources.Load<Shader>("GlassPanel");
            if (game == null || preview == null || shader == null || !shader.isSupported) yield break;
            while (preview != null && preview.enabled && preview.Capture == null) yield return null;
            if (preview == null || preview.Capture == null || game.lesson == null) yield break;
            lesson = game.lesson;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var root = new GameObject("Placement lesson glass UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 18;
            group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var panel = Rect("Lesson glass panel", root.transform, new Vector2(1195, -130), new Vector2(377, 260));
            backdrop = panel.gameObject.AddComponent<RawImage>(); backdrop.material = material; backdrop.color = new Color(1, 1, 1, .97f); backdrop.raycastTarget = false;
            Font font = Resources.Load<Font>("HelpHudFonts/NotoSansKR-Regular") ?? game.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title = Label(panel, font, 22, FontStyle.Bold, new Vector2(17, -14), new Vector2(341, 55));
            detail = Label(panel, font, 18, FontStyle.Normal, new Vector2(17, -67), new Vector2(341, 134));
            status = Label(panel, font, 18, FontStyle.Normal, new Vector2(17, -198), new Vector2(341, 52));
            shownExpanded = lesson.Visible;
            ready = true;
            lesson.SuppressDefaultHud = true;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = pos; rect.sizeDelta = size; return rect;
        }
        static Text Label(Transform parent, Font font, int size, FontStyle style, Vector2 pos, Vector2 dimensions)
        {
            var rect = Rect("Label", parent, pos, dimensions); var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.fontStyle = style; text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .65f); shadow.effectDistance = new Vector2(1, -1);
            return text;
        }
        void Update()
        {
            if (!ready || lesson == null) return;
            bool blocked = GlassPanelPreview.ArrivalBlocksHud(game);
            bool active = lesson.ShouldShow && !game.Transitioning && !game.conceptCapture && !blocked;
            bool changing = shownExpanded != lesson.Visible;
            float target = active && !changing ? 1f : 0f;
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime / Mathf.Max(.01f, fadeSeconds));
            if (blocked) alpha = 0;
            if (changing && alpha <= 0f) shownExpanded = lesson.Visible;
            canvas.enabled = alpha > 0f; group.alpha = alpha;
            if (!canvas.enabled) return;
            float height = shownExpanded ? 260 : 132;
            ((RectTransform)backdrop.transform).sizeDelta = new Vector2(377, height);
            backdrop.texture = preview.Capture.blurred;
            material.SetFloat("_GlassMode", 1); material.SetFloat("_PanelAspect", 377f / height);
            material.SetFloat("_Refraction", preview.refraction ? 1 : 0); material.SetFloat("_RefractionStrength", preview.refractionStrength);
            material.SetFloat("_RefractionWidth", preview.refractionWidth); material.SetFloat("_LensDistortion", preview.lensDistortion);
            material.SetFloat("_EdgeContrast", preview.edgeContrast ? 1 : 0); material.SetFloat("_EdgeContrastStrength", preview.edgeContrastStrength);
            material.SetFloat("_SpectrumPixels", preview.spectrumPixels); material.SetFloat("_WhiteTintStrength", preview.whiteTint); material.SetFloat("_MilkyTint", preview.milkyTint);
            title.text = lesson.CurrentTitle;
            detail.gameObject.SetActive(shownExpanded); detail.text = lesson.CurrentDetail;
            ((RectTransform)status.transform).anchoredPosition = new Vector2(17, shownExpanded ? -198 : -67);
            status.text = lesson.CurrentStatus;
        }
        void OnDestroy()
        {
            if (lesson != null) lesson.SuppressDefaultHud = false;
            ready = false; if (canvas != null) Destroy(canvas.gameObject); if (material != null) Destroy(material);
        }
    }
}
