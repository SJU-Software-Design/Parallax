using System.Collections;
using ShadeLink.Gallery;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GlassTrial
{
    // Standalone preview: no changes to the gameplay HUD or scene assets.
    public sealed class GlassPanelPreview : MonoBehaviour
    {
        public string eyebrow = "LIVE GLASS  /  NIKE 50K";
        public string objectName = "빛과 그림자의 미술관";
        [TextArea] public string description = "니케의 그림자를 캔버스의 윤곽에 맞춰보세요.";
        [TextArea] public string status = "WASD · 시점 이동  /  클릭 · 잡기";
        public Vector2 position = new Vector2(34f, 325f);
        public Vector2 panelSize = new Vector2(500f, 340f);
        [Range(0f, 1f)] public float whiteTint = 1f;
        [Range(0f, 0.5f)] public float milkyTint = 0f;
        [Range(0f, 8f)] public float spectrumPixels = 3f;
        public bool refraction = true;
        public bool edgeContrast = true;
        [Range(0f, 30f)] public float refractionStrength = 9f;
        [Range(0.01f, 0.6f)] public float refractionWidth = 0.18f;
        [Range(0f, 80f)] public float lensDistortion = 0f;
        [Range(0f, 1f)] public float blurStrength = 1f;
        [Range(0f, 2f)] public float edgeContrastStrength = 1f;
        public bool showPreview;
        public float nameDisplaySeconds = 2f;
        GalleryGame game;
        GlassCameraCapture capture;
        Material panelMaterial;
        Canvas canvas;
        CanvasGroup canvasGroup;
        RectTransform panelRect, shadowRect;
        RawImage raw;
        Text eyebrowText, headingText, descriptionText, statusText;
        Texture2D shadowTexture;
        Sprite shadowSprite;
        bool ownsCapture;
        public static bool ArrivalBlocksHud(GalleryGame game)
        {
            return ShadeLink.Tutorial.TutorialMuseumElevatorFlow.ArrivalPending ||
                (game != null && game.ElevatorArrival && game.active);
        }
        UnityEngine.Object lastHeldObject;
        string heldDisplayName;
        float nameShownAt;
        float displayAlpha;
        public static bool IsFoldedBronze(GalleryProp prop)
        {
            return prop != null && prop.title == "01 · 접힌 청동 · 그림자를 만드는 조각";
        }
        public static bool CanShowObjectName(GalleryGame game)
        {
            var preview = game != null ? game.GetComponent<GlassPanelPreview>() : null;
            return preview != null && preview.isActiveAndEnabled && preview.canvas != null && preview.capture != null && preview.panelMaterial != null;
        }
        static string ShortName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "사물";
            string[] parts = value.Split('·');
            if (parts.Length > 1 && int.TryParse(parts[0].Trim(), out _)) return parts[1].Trim();
            return parts[0].Trim();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install();
        }
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Install(); }
        static void Install()
        {
            var game = FindFirstObjectByType<GalleryGame>();
            if (game != null)
            {
                var lessonNotice = game.GetComponent<GlassLessonNotice>();
                if (lessonNotice != null)
                {
                    lessonNotice.enabled = false;
                    if (game.lesson != null) game.lesson.SuppressDefaultHud = false;
                    UnityEngine.Object.Destroy(lessonNotice);
                }
            }
            if (game != null && game.GetComponent<GlassPanelPreview>() == null)
                game.gameObject.AddComponent<GlassPanelPreview>();
            if (game != null && game.GetComponent<GlassExposureGauge>() == null)
                game.gameObject.AddComponent<GlassExposureGauge>();
            if (game != null && game.GetComponent<GlassStageNotice>() == null)
                game.gameObject.AddComponent<GlassStageNotice>();
            if (game != null && game.GetComponent<GlassHoverPrompt>() == null)
                game.gameObject.AddComponent<GlassHoverPrompt>();
        }
        IEnumerator Start()
        {
            game = GetComponent<GalleryGame>();
            yield return null;
            if (game == null || game.view == null) { enabled = false; yield break; }
            Shader blur = Resources.Load<Shader>("GlassBlur");
            Shader panel = Resources.Load<Shader>("GlassPanel");
            if (blur == null || panel == null || !blur.isSupported || !panel.isSupported)
            { Debug.LogWarning("Glass panel preview shaders unavailable."); enabled = false; yield break; }
            capture = game.view.GetComponent<GlassCameraCapture>();
            if (capture == null) { capture = game.view.gameObject.AddComponent<GlassCameraCapture>(); ownsCapture = true; }
            capture.blurShader = blur;
            capture.captureEnabled = false;
            panelMaterial = new Material(panel) { hideFlags = HideFlags.HideAndDontSave };
            BuildUI();
            Refresh();
        }
        void BuildUI()
        {
            var root = new GameObject("Glass trial Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            canvasGroup = root.AddComponent<CanvasGroup>(); canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            shadowRect = Rect("Soft panel shadow", root.transform);
            var shadow = shadowRect.gameObject.AddComponent<Image>(); shadow.sprite = MakeShadowSprite(); shadow.type = Image.Type.Sliced;
            shadow.color = new Color(.008f, .016f, .018f, .36f); shadow.raycastTarget = false;
            panelRect = Rect("Real-time glass panel", root.transform);
            raw = panelRect.gameObject.AddComponent<RawImage>(); raw.color = new Color(1, 1, 1, .97f); raw.raycastTarget = false; raw.material = panelMaterial;
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            eyebrowText = Label(panelRect, font, 19, new Vector2(25, -23), new Vector2(450, 30), new Color(.74f, 1, .94f, 1), FontStyle.Bold);
            headingText = Label(panelRect, font, 30, new Vector2(25, -68), new Vector2(450, 52), new Color(.96f, .99f, .98f, 1), FontStyle.Bold);
            descriptionText = Label(panelRect, font, 20, new Vector2(25, -129), new Vector2(450, 55), new Color(.96f, .98f, .97f, 1), FontStyle.Normal);
            statusText = Label(panelRect, font, 18, new Vector2(25, -205), new Vector2(450, 100), new Color(.92f, .97f, .95f, 1), FontStyle.Normal);
        }
        static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); return rect;
        }
        static Text Label(Transform parent, Font font, int size, Vector2 offset, Vector2 dimensions, Color color, FontStyle style)
        {
            var rect = Rect("Label", parent); rect.anchoredPosition = offset; rect.sizeDelta = dimensions;
            var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.fontSize = size; label.fontStyle = style; label.color = color;
            label.alignment = TextAnchor.UpperLeft; label.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, .02f, .025f, .6f); shadow.effectDistance = new Vector2(1, -1);
            return label;
        }
        Sprite MakeShadowSprite()
        {
            const int size = 128, radius = 24;
            shadowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Glass shadow" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x, 0, x - (size - radius - 1));
                float dy = Mathf.Max(radius - y, 0, y - (size - radius - 1));
                float alpha = 1 - Mathf.SmoothStep(radius - 12, radius, Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
            shadowTexture.SetPixels32(pixels); shadowTexture.Apply();
            shadowSprite = Sprite.Create(shadowTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return shadowSprite;
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6)) showPreview = !showPreview;
            displayAlpha = Mathf.MoveTowards(displayAlpha, showPreview ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
            Refresh();
        }
        void Refresh()
        {
            if (capture == null || canvas == null || panelMaterial == null) return;
            bool visible = displayAlpha > 0f && !ArrivalBlocksHud(game);
            capture.captureEnabled = visible || GlassExposureGauge.CanDisplay(game) || GlassStageNotice.NeedsCapture(game) || GlassHoverPrompt.NeedsCapture(game); capture.blurStrength = blurStrength;
            canvas.enabled = visible; canvasGroup.alpha = displayAlpha;
            float w = showPreview ? Mathf.Max(120f, panelSize.x) : 230f;
            float h = showPreview ? Mathf.Max(100f, panelSize.y) : 56f;
            Vector2 pos = showPreview ? position : new Vector2(900f, 505f);
            panelRect.anchoredPosition = new Vector2(pos.x, -pos.y); panelRect.sizeDelta = new Vector2(w, h);
            shadowRect.anchoredPosition = new Vector2(pos.x - 6f, -pos.y - 8f); shadowRect.sizeDelta = new Vector2(w + 24f, h + 24f);
            raw.texture = capture.blurred;
            eyebrowText.gameObject.SetActive(showPreview); descriptionText.gameObject.SetActive(showPreview); statusText.gameObject.SetActive(showPreview);
            headingText.text = showPreview ? objectName : heldDisplayName;
            headingText.fontSize = showPreview ? 30 : 22;
            headingText.resizeTextForBestFit = !showPreview;
            headingText.resizeTextMinSize = 14;
            headingText.resizeTextMaxSize = 22;
            var headingRect = (RectTransform)headingText.transform;
            headingRect.anchoredPosition = showPreview ? new Vector2(25, -68) : new Vector2(18, -12);
            headingRect.sizeDelta = showPreview ? new Vector2(450, 52) : new Vector2(w - 36, h - 16);
            eyebrowText.text = eyebrow; descriptionText.text = description; statusText.text = status;
            panelMaterial.SetFloat("_GlassMode", 1f); panelMaterial.SetFloat("_PanelAspect", w / h);
            panelMaterial.SetFloat("_Refraction", refraction ? 1f : 0f); panelMaterial.SetFloat("_EdgeContrast", edgeContrast ? 1f : 0f);
            panelMaterial.SetFloat("_RefractionStrength", refractionStrength); panelMaterial.SetFloat("_RefractionWidth", refractionWidth);
            panelMaterial.SetFloat("_LensDistortion", lensDistortion); panelMaterial.SetFloat("_EdgeContrastStrength", edgeContrastStrength);
            panelMaterial.SetFloat("_SpectrumPixels", spectrumPixels); panelMaterial.SetFloat("_WhiteTintStrength", whiteTint); panelMaterial.SetFloat("_MilkyTint", milkyTint);
        }
        public GlassCameraCapture Capture => capture;
        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            if (ownsCapture && capture != null) Destroy(capture);
            if (panelMaterial != null) Destroy(panelMaterial);
            if (shadowSprite != null) Destroy(shadowSprite);
            if (shadowTexture != null) Destroy(shadowTexture);
        }
    }
}
