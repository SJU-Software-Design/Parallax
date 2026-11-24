using ShadeLink.Gallery;
using ShadeLink.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace GlassTrial
{
    // Tutorial variant of the museum hover cue. It never changes targeting or input.
    [DefaultExecutionOrder(110)]
    public sealed class TutorialGlassHover : MonoBehaviour
    {
        const float EnterSeconds = .24f, ExitSeconds = .14f, ClickSeconds = .14f, VerticalTravel = 12f;
        TutorialGame game;
        GlassCameraCapture capture;
        bool ownsCapture;
        Canvas canvas;
        CanvasGroup group;
        RectTransform panel;
        RectTransform shadowRect;
        RawImage backdrop;
        RawImage panelShadow;
        Image mouseIcon;
        Text label;
        Material material;
        Material shadowMaterial;
        Texture2D mouseTexture;
        Sprite mouseSprite;
        GalleryProp lastTarget;
        float visibleAmount, pulseUntil;
        bool ready, clickTriggered;
        public bool NeedsCapture => isActiveAndEnabled && ready && (visibleAmount > 0 || Target() != null || Time.unscaledTime < pulseUntil);
        public bool TakeCaptureOwnership(GlassCameraCapture value)
        {
            if (!isActiveAndEnabled || !ready || capture != value) return false;
            ownsCapture = true;
            return true;
        }

        GalleryProp Target()
        {
            return game != null && game.BottomNoticeVisible && game.CanInteract && game.Held == null ? game.Aimed : null;
        }

        void Start()
        {
            game = GetComponent<TutorialGame>();
            var glass = Resources.Load<Shader>("TutorialIslandGlass");
            var shadowShader = Resources.Load<Shader>("TutorialIslandShadow");
            var blur = Resources.Load<Shader>("GlassBlur");
            if (game == null || game.Camera == null || glass == null || blur == null || !glass.isSupported || !blur.isSupported)
            { Debug.LogWarning("Tutorial object glass prompt unavailable."); enabled = false; return; }
            capture = game.Camera.GetComponent<GlassCameraCapture>();
            if (capture == null) { capture = game.Camera.gameObject.AddComponent<GlassCameraCapture>(); ownsCapture = true; }
            capture.blurShader = blur;
            material = new Material(glass) { hideFlags = HideFlags.HideAndDontSave };
            if (shadowShader != null && shadowShader.isSupported)
            {
                shadowMaterial = new Material(shadowShader) { hideFlags = HideFlags.HideAndDontSave };
                shadowMaterial.SetFloat("_Padding", 13f);
                shadowMaterial.SetFloat("_FadeDistance", 11f);
            }
            material.SetFloat("_GlassMode", 1); material.SetFloat("_CornerRadius", 1);
            material.SetFloat("_Refraction", 1); material.SetFloat("_RefractionStrength", 9);
            material.SetFloat("_RefractionWidth", .18f); material.SetFloat("_LensDistortion", 0);
            material.SetFloat("_EdgeContrast", 1); material.SetFloat("_EdgeContrastStrength", .35f);
            material.SetFloat("_SpectrumPixels", 3); material.SetFloat("_WhiteTintStrength", .14f); material.SetFloat("_MilkyTint", 0);
            material.SetFloat("_DarkTintStrength", .26f); material.SetFloat("_Saturation", 1.45f); material.SetFloat("_ExtraBlurPixels", 9.5f);
            var root = new GameObject("Tutorial glass hover prompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 21;
            group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            panel = Rect("Hover panel", root.transform); panel.anchorMin = panel.anchorMax = new Vector2(.5f, .25f);
            panel.pivot = new Vector2(.5f, .5f); panel.sizeDelta = new Vector2(150, 54);
            shadowRect = Rect("Hover soft glass shadow", root.transform); shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(.5f, .25f);
            shadowRect.pivot = new Vector2(.5f, .5f); shadowRect.SetAsFirstSibling();
            panelShadow = shadowRect.gameObject.AddComponent<RawImage>(); panelShadow.texture = Texture2D.whiteTexture;
            panelShadow.material = shadowMaterial; panelShadow.enabled = shadowMaterial != null;
            panelShadow.color = new Color(0, 0, 0, .38f); panelShadow.raycastTarget = false;
            backdrop = panel.gameObject.AddComponent<RawImage>(); backdrop.material = material;
            backdrop.color = new Color(1, 1, 1, .97f); backdrop.raycastTarget = false;
            var icon = Rect("Left mouse button", panel); icon.anchoredPosition = new Vector2(15, -9); icon.sizeDelta = new Vector2(27, 36);
            mouseIcon = icon.gameObject.AddComponent<Image>(); mouseIcon.sprite = MakeMouseSprite(); mouseIcon.preserveAspect = true; mouseIcon.raycastTarget = false;
            var text = Rect("Object name", panel); text.anchoredPosition = new Vector2(54, -10); text.sizeDelta = new Vector2(90, 34);
            label = text.gameObject.AddComponent<Text>();
            label.font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 20);
            if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "주사위"; label.fontSize = 20; label.fontStyle = FontStyle.Bold; label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow; label.raycastTarget = false;
            var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .7f); shadow.effectDistance = new Vector2(1, -1);
            canvas.enabled = false; ready = true;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); return rect;
        }

        Sprite MakeMouseSprite()
        {
            const int w = 48, h = 64;
            mouseTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tutorial left click icon", filterMode = FilterMode.Bilinear };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float px = (x + .5f - w * .5f) / w, py = (y + .5f - h * .5f) / h;
                float qx = Mathf.Abs(px) - .075f, qy = Mathf.Abs(py) - .12f;
                float d = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - .34f;
                bool inside = d < 0, edge = inside && d > -.04f;
                bool leftButton = inside && d < -.05f && py > .015f && px < -.035f;
                bool separator = inside && (Mathf.Abs(py) < .018f || (Mathf.Abs(px) < .022f && py > 0));
                pixels[y * w + x] = new Color32(255, 255, 255, edge || separator ? (byte)255 : leftButton ? (byte)200 : (byte)0);
            }
            mouseTexture.SetPixels32(pixels); mouseTexture.Apply();
            return mouseSprite = Sprite.Create(mouseTexture, new Rect(0, 0, w, h), new Vector2(.5f, .5f));
        }

        void LateUpdate()
        {
            if (!ready || game == null || capture == null) return;
            var target = Target();
            if (!clickTriggered && lastTarget != null && target == null && game.BottomNoticeVisible && game.CanInteract && game.Held == lastTarget)
            { pulseUntil = Time.unscaledTime + ClickSeconds; clickTriggered = true; }
            if (target != null) { lastTarget = target; pulseUntil = 0; clickTriggered = false; }
            bool pulsing = target == null && Time.unscaledTime < pulseUntil;
            bool show = target != null || pulsing;
            if (!game.BottomNoticeVisible || !game.CanInteract) { show = false; pulsing = false; pulseUntil = 0; visibleAmount = 0; }
            visibleAmount = Mathf.MoveTowards(visibleAmount, show ? 1 : 0, Time.unscaledDeltaTime / (show ? EnterSeconds : ExitSeconds));
            if (!show && visibleAmount <= 0) lastTarget = null;
            var notice = GetComponent<TutorialGlassNotice>();
            if (notice == null || !notice.OwnsCapture) capture.captureEnabled = show || visibleAmount > 0 || game.RotationGlassCaptureNeeded;
            canvas.enabled = visibleAmount > 0 && capture.blurred != null;
            if (!canvas.enabled) return;
            float eased = 1 - Mathf.Pow(1 - visibleAmount, 3);
            group.alpha = eased;
            panel.anchoredPosition = new Vector2(0, -(1 - eased) * VerticalTravel);
            panel.localScale = Vector3.one * (pulsing ? .95f : 1f);
            mouseIcon.color = pulsing ? Color.white : new Color(.30f, 1f, .96f, 1f);
            backdrop.texture = capture.blurred;
            float available = Mathf.Max(1, ((RectTransform)canvas.transform).rect.width - 56);
            float textWidth = Mathf.Min(Mathf.Max(1, available - 74), Mathf.Max(1, label.preferredWidth));
            var settings = label.GetGenerationSettings(new Vector2(textWidth, 0));
            float textHeight = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
            float width = Mathf.Min(available, Mathf.Max(100, textWidth + 74));
            float height = Mathf.Max(54, textHeight + 20);
            panel.sizeDelta = new Vector2(width, height);
            shadowRect.sizeDelta = new Vector2(width + 26, height + 26);
            shadowRect.anchoredPosition = panel.anchoredPosition + new Vector2(0, -3);
            shadowRect.localScale = panel.localScale;
            if (shadowMaterial != null) shadowMaterial.SetVector("_PanelSize", new Vector4(width, height, 0, 0));
            var labelRect = (RectTransform)label.transform;
            labelRect.anchoredPosition = new Vector2(54, -(height - textHeight) * .5f); labelRect.sizeDelta = new Vector2(textWidth, textHeight);
            ((RectTransform)mouseIcon.transform).anchoredPosition = new Vector2(15, -(height - 36) * .5f);
            material.SetFloat("_PanelAspect", width / height);
        }

        void OnDisable()
        {
            if (canvas != null) canvas.enabled = false;
            var notice = GetComponent<TutorialGlassNotice>();
            if ((notice == null || !notice.OwnsCapture) && capture != null) capture.captureEnabled = false;
        }
        void OnDestroy()
        {
            ready = false;
            if (canvas != null) Destroy(canvas.gameObject);
            if (material != null) Destroy(material);
            if (shadowMaterial != null) Destroy(shadowMaterial);
            if (ownsCapture && capture != null) Destroy(capture);
            if (mouseSprite != null) Destroy(mouseSprite);
            if (mouseTexture != null) Destroy(mouseTexture);
        }
    }
}