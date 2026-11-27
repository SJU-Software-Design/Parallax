using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GlassTrial
{
    // Eclipse-only prototype: two independent SDF text slots over the existing glass.
    public sealed class EclipseHintOverlay : MonoBehaviour
    {
        sealed class Slot { public RectTransform root, titleRect, bodyRect; public CanvasGroup group; public TextMeshProUGUI title, body; public Material titleMat, bodyMat; }
        Canvas canvas; RectTransform root; TMP_FontAsset tmpFont; readonly Slot[] slots = new Slot[2];
        RawImage glassBackground, glassShadow;
        CanvasGroup panelGroup;
        readonly Image[] dots = new Image[3]; Texture2D dotTexture; Sprite dotSprite;
        readonly float[] dotProgress = new float[3];
        int active, shownFrame = -100, page; float elapsed = 1; bool transitioning;
        float appearance, lastScale = 1;
        Rect lastRect;
        string currentTitle, currentBody;
        public bool Transitioning => transitioning;

        void Awake()
        {
            var source = Resources.Load<Font>("HelpHudFonts/NotoSansKR-Regular");
            if (source == null || Shader.Find("TextMeshPro/Mobile/Distance Field") == null) { enabled = false; return; }
            tmpFont = TMP_FontAsset.CreateFontAsset(source, 48, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (tmpFont == null) { enabled = false; return; }
            var go = new GameObject("Eclipse hint text overlay", typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
            root = MakeRect("Hint content", go.transform);
            panelGroup = root.gameObject.AddComponent<CanvasGroup>(); panelGroup.blocksRaycasts = false; panelGroup.interactable = false;
            glassShadow = MakeRect("Glass shadow", root).gameObject.AddComponent<RawImage>(); glassShadow.raycastTarget = false;
            glassBackground = MakeRect("Glass background", root).gameObject.AddComponent<RawImage>(); glassBackground.raycastTarget = false;
            for (int i = 0; i < 2; i++) slots[i] = MakeSlot(i);
            dotTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(16, 16)); pixels[y * 32 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(16.5f - d))); }
            dotTexture.SetPixels32(pixels); dotTexture.Apply(); dotSprite = Sprite.Create(dotTexture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(15, 15, 15, 15));
            for (int i = 0; i < 3; i++) { var r = MakeRect("Hint dot " + i, root); dots[i] = r.gameObject.AddComponent<Image>(); dots[i].sprite = dotSprite; dots[i].type = Image.Type.Sliced; dots[i].raycastTarget = false; }
            canvas.enabled = false;
        }
        static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); return rect;
        }
        Slot MakeSlot(int index)
        {
            var s = new Slot(); s.root = MakeRect("Hint text slot " + index, root); s.group = s.root.gameObject.AddComponent<CanvasGroup>(); s.group.blocksRaycasts = false;
            s.titleRect = MakeRect("Title", s.root); s.title = s.titleRect.gameObject.AddComponent<TextMeshProUGUI>();
            s.bodyRect = MakeRect("Body", s.root); s.body = s.bodyRect.gameObject.AddComponent<TextMeshProUGUI>();
            foreach (var t in new[] { s.title, s.body }) { t.font = tmpFont; t.raycastTarget = false; t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Overflow; }
            s.title.color = new Color(.40f, 1f, .91f, 1); s.body.color = Color.white;
            s.titleMat = s.title.fontMaterial; s.bodyMat = s.body.fontMaterial; s.group.alpha = 0; return s;
        }
        public void Present(string title, string body, int hintPage, Rect screenRect, float scale, Texture blurred, Material glass, Material shadow)
        {
            if (!enabled || canvas == null) return;
            shownFrame = Time.frameCount; canvas.enabled = true;
            lastRect = screenRect; lastScale = scale;
            root.anchorMin = root.anchorMax = new Vector2(0, 1); root.pivot = new Vector2(.5f, .5f); root.sizeDelta = screenRect.size;
            bool hasGlass = blurred != null && glass != null;
            glassBackground.texture = hasGlass ? blurred : Texture2D.whiteTexture;
            glassBackground.material = hasGlass ? glass : null;
            glassBackground.color = hasGlass ? Color.white : new Color(.035f, .065f, .08f, .93f);
            glassBackground.rectTransform.anchoredPosition = Vector2.zero;
            glassBackground.rectTransform.sizeDelta = screenRect.size;
            glassShadow.enabled = hasGlass && shadow != null;
            if (glassShadow.enabled)
            {
                glassShadow.texture = Texture2D.whiteTexture; glassShadow.material = shadow;
                glassShadow.color = new Color(0, 0, 0, .38f);
                glassShadow.rectTransform.anchoredPosition = new Vector2(-13 * scale, 10 * scale);
                glassShadow.rectTransform.sizeDelta = screenRect.size + new Vector2(26, 26) * scale;
            }
            if (currentBody == null) { currentTitle = title; currentBody = body; active = 0; slots[0].title.text = title; slots[0].body.text = body; slots[0].group.alpha = 1; slots[1].group.alpha = 0; transitioning = false; }
            else if ((currentTitle != title || currentBody != body) && !transitioning)
            { int next = 1 - active; slots[next].title.text = title; slots[next].body.text = body; currentTitle = title; currentBody = body; active = next; elapsed = 0; transitioning = true; }
            page = Mathf.Clamp(hintPage - 1, 0, 2);
            for (int i = 0; i < 2; i++)
            {
                var s = slots[i]; s.root.sizeDelta = screenRect.size;
                s.title.fontSize = 17 * GameShellSettingsRuntime.TextScale * scale; s.body.fontSize = 19 * GameShellSettingsRuntime.TextScale * scale;
                s.titleRect.sizeDelta = new Vector2(screenRect.width - 48 * scale, 26 * scale);
                s.bodyRect.sizeDelta = new Vector2(screenRect.width - 48 * scale, screenRect.height - 55 * scale);
            }
            Layout(scale);
        }
        void Update()
        {
            if (canvas == null) return;
            bool requested = Time.frameCount - shownFrame <= 1;
            if (!canvas.enabled && !requested) return;
            appearance = Mathf.MoveTowards(appearance, requested ? 1 : 0, Time.unscaledDeltaTime / (requested ? .28f : .18f));
            if (!requested && appearance <= 0) { canvas.enabled = false; currentBody = null; transitioning = false; return; }
            for (int i = 0; i < dotProgress.Length; i++) dotProgress[i] = Mathf.MoveTowards(dotProgress[i], i == page ? 1 : 0, Time.unscaledDeltaTime / .32f);
            if (transitioning) { elapsed += Time.unscaledDeltaTime; if (elapsed >= .36f) { elapsed = .36f; transitioning = false; } }
            Layout(lastScale);
        }
        void Layout(float scale)
        {
            float eased = 1 - Mathf.Pow(1 - appearance, 3);
            panelGroup.alpha = eased;
            root.localScale = Vector3.one * Mathf.Lerp(.95f, 1f, eased);
            root.anchoredPosition = new Vector2(lastRect.x + lastRect.width * .5f, -(lastRect.y + lastRect.height * .5f) - 16f * scale * (1 - eased));
            float contentAlpha = Mathf.Clamp01((appearance * .28f - .04f) / .24f);
            contentAlpha = Mathf.SmoothStep(0, 1, contentAlpha);
            float incoming = transitioning ? Mathf.Clamp01(elapsed / .36f) : 1;
            float outgoing = transitioning ? Mathf.Clamp01(elapsed / .32f) : 1;
            float enter = 1 - Mathf.Pow(1 - incoming, 3); float exit = outgoing * outgoing;
            for (int i = 0; i < 2; i++)
            {
                var s = slots[i]; bool isActive = i == active; float a = isActive ? enter : 1 - exit;
                float tx = isActive ? 14 * (1 - enter) : -14 * exit;
                float bx = isActive ? 28 * (1 - enter) : -28 * exit;
                s.group.alpha = a * contentAlpha; s.titleRect.anchoredPosition = new Vector2((24 + tx) * scale, -13 * scale);
                s.bodyRect.anchoredPosition = new Vector2((24 + bx) * scale, -42 * scale);
                float blur = 1 - a;
                s.titleMat.SetFloat(ShaderUtilities.ID_OutlineSoftness, blur * .35f); s.bodyMat.SetFloat(ShaderUtilities.ID_OutlineSoftness, blur * .50f);
                s.titleMat.SetFloat(ShaderUtilities.ID_FaceDilate, -blur * .12f); s.bodyMat.SetFloat(ShaderUtilities.ID_FaceDilate, -blur * .16f);
            }
            float x = 24;
            for (int i = 0; i < 3; i++) { float t = Mathf.SmoothStep(0, 1, dotProgress[i]); float width = Mathf.Lerp(8, 26, t); var color = Color.Lerp(new Color(.72f, .76f, .78f, .75f), new Color(0, .66f, .78f, 1), t); color.a *= contentAlpha; dots[i].color = color; var r = dots[i].rectTransform; r.anchoredPosition = new Vector2(x * scale, -(root.rect.height - 18 * scale)); r.sizeDelta = new Vector2(width * scale, 8 * scale); x += width + 8; }
        }
        void OnDestroy()
        {
            foreach (var s in slots) if (s != null) { if (s.titleMat != null) Destroy(s.titleMat); if (s.bodyMat != null) Destroy(s.bodyMat); }
            if (tmpFont != null) Destroy(tmpFont); if (dotSprite != null) Destroy(dotSprite); if (dotTexture != null) Destroy(dotTexture);
        }
    }
}
