using ShadeLink;
using ShadeLink.Eclipse;
using UnityEngine;
using UnityEngine.UI;

namespace GlassTrial
{
    // Presentation only: the eclipse puzzle and input remain owned by the game.
    [DefaultExecutionOrder(200)]
    public sealed class EclipseGlassHud : MonoBehaviour
    {
        EclipseFinaleGame game;
        GlassCameraCapture capture;
        bool ownsCapture, ready, previousTotality, suppressFloorAfterBlocked, sunGaugeVisual;
        Canvas canvas;
        CanvasGroup group;
        RectTransform panel, shadowRect;
        RawImage backdrop, panelShadow;
        Image indicator, halo, ring, gaugeBar, exposureSun;
        Texture2D exposureSunTexture;
        Sprite exposureSunSprite;
        Text caption;
        Material glass, shadowMaterial, gauge;
        readonly Texture2D[] indicatorTextures = new Texture2D[3];
        readonly Sprite[] indicatorSprites = new Sprite[3];
        readonly MuseumHudTextTransition transition = new MuseumHudTextTransition();
        float successUntil, warningStart, moonBlockedUntil, width, gaugeMorph, visibility, captionReveal;
        string revealCaption;
        float widthVelocity, resizeStartWidth;
        bool wasResizing;
        string lastWarning;
        static readonly Color Mint = new Color(.34f, 1f, .82f);
        static readonly Color Amber = new Color(1f, .65f, .25f);
        public static bool ReplacesHud(EclipseFinaleGame game)
        {
            var hud = game != null ? game.GetComponent<EclipseGlassHud>() : null;
            return hud != null && hud.isActiveAndEnabled && hud.ready && hud.capture != null && hud.capture.blurred != null;
        }
        void Start()
        {
            game = GetComponent<EclipseFinaleGame>();
            var blur = Resources.Load<Shader>("GlassBlur");
            var glassShader = Resources.Load<Shader>("TutorialIslandGlass");
            var gaugeShader = Resources.Load<Shader>("GlassExposureGauge");
            if (game == null || game.view == null || blur == null || glassShader == null || gaugeShader == null ||
                !blur.isSupported || !glassShader.isSupported || !gaugeShader.isSupported)
            { Debug.LogWarning("Eclipse glass HUD unavailable; using original HUD."); enabled = false; return; }
            capture = game.view.GetComponent<GlassCameraCapture>();
            if (capture == null) { capture = game.view.gameObject.AddComponent<GlassCameraCapture>(); ownsCapture = true; }
            capture.blurShader = blur;
            glass = new Material(glassShader) { hideFlags = HideFlags.HideAndDontSave };
            gauge = new Material(gaugeShader) { hideFlags = HideFlags.HideAndDontSave };
            Configure(glass);
            var shadowShader = Resources.Load<Shader>("TutorialIslandShadow");
            if (shadowShader != null && shadowShader.isSupported)
            {
                shadowMaterial = new Material(shadowShader) { hideFlags = HideFlags.HideAndDontSave };
                shadowMaterial.SetFloat("_Padding", 13f); shadowMaterial.SetFloat("_FadeDistance", 11f);
            }
            var root = new GameObject("Eclipse glass HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 19;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            panel = Rect("Notice panel", root.transform); panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f / 6f);
            panel.pivot = new Vector2(.5f, .5f); panel.sizeDelta = new Vector2(500, 52);
            group = panel.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            backdrop = panel.gameObject.AddComponent<RawImage>(); backdrop.material = glass; backdrop.raycastTarget = false;
            shadowRect = Rect("Soft glass shadow", root.transform); shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(.5f, 1f / 6f);
            shadowRect.pivot = new Vector2(.5f, .5f); shadowRect.SetAsFirstSibling();
            panelShadow = shadowRect.gameObject.AddComponent<RawImage>(); panelShadow.texture = Texture2D.whiteTexture;
            panelShadow.material = shadowMaterial; panelShadow.enabled = shadowMaterial != null;
            panelShadow.color = new Color(0, 0, 0, .38f); panelShadow.raycastTarget = false;
            for (int i = 0; i < 3; i++) indicatorSprites[i] = MakeIndicator(i, out indicatorTextures[i]);
            halo = Dot("Indicator glow", panel, indicatorSprites[1], 34);
            ring = Dot("Indicator ring", panel, indicatorSprites[2], 18);
            indicator = Dot("Indicator", panel, indicatorSprites[0], 8);
            caption = Label("Notice text", panel, 20); var cr = (RectTransform)caption.transform;
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = new Vector2(56, 9); cr.offsetMax = new Vector2(-20, -9); caption.alignment = TextAnchor.MiddleCenter;
            var sunRect = Rect("Exposure sun", panel);
            sunRect.anchorMin = sunRect.anchorMax = new Vector2(0, .5f); sunRect.pivot = new Vector2(0, .5f);
            sunRect.anchoredPosition = new Vector2(16, 0); sunRect.sizeDelta = new Vector2(26, 26);
            exposureSun = sunRect.gameObject.AddComponent<Image>(); exposureSun.sprite = MakeExposureSun(); exposureSun.raycastTarget = false;
            var br = Rect("Progress", panel); br.anchorMin = br.anchorMax = new Vector2(0, .5f); br.pivot = new Vector2(0, .5f);
            br.anchoredPosition = new Vector2(50, 0); br.sizeDelta = new Vector2(204, 14);
            gaugeBar = br.gameObject.AddComponent<Image>(); gaugeBar.material = gauge; gaugeBar.raycastTarget = false;
            ready = true;
        }
        static void Configure(Material m)
        {
            m.SetFloat("_GlassMode", 1); m.SetFloat("_CornerRadius", 1); m.SetFloat("_Refraction", 1);
            m.SetFloat("_RefractionStrength", 9); m.SetFloat("_RefractionWidth", .18f); m.SetFloat("_EdgeContrast", 1);
            m.SetFloat("_EdgeContrastStrength", .35f); m.SetFloat("_SpectrumPixels", 3); m.SetFloat("_WhiteTintStrength", .14f);
            m.SetFloat("_DarkTintStrength", .26f); m.SetFloat("_Saturation", 1.45f); m.SetFloat("_ExtraBlurPixels", 9.5f);
        }
        static RectTransform Rect(string name, Transform parent)
        { var o = new GameObject(name, typeof(RectTransform)); o.transform.SetParent(parent, false); return o.GetComponent<RectTransform>(); }
        Image Dot(string name, Transform parent, Sprite sprite, float size)
        { var r = Rect(name, parent); r.anchorMin = r.anchorMax = new Vector2(0, .5f); r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(25, 0); r.sizeDelta = Vector2.one * size; var i = r.gameObject.AddComponent<Image>(); i.sprite = sprite; i.raycastTarget = false; return i; }
        static Sprite MakeIndicator(int shape, out Texture2D texture)
        {
            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f - size * .5f) / (size * .5f), dy = (y + .5f - size * .5f) / (size * .5f);
                float radius = Mathf.Sqrt(dx * dx + dy * dy), alpha;
                if (shape == 1)
                {
                    float falloff = Mathf.Clamp01((radius - .25f) / .75f);
                    alpha = Mathf.Exp(-4f * falloff * falloff) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.90f, 1f, radius)));
                }
                else if (shape == 2)
                    alpha = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.92f, 1f, radius))) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.79f, .87f, radius));
                else alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.88f, 1f, radius));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * alpha));
            }
            texture.SetPixels32(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        Sprite MakeExposureSun()
        {
            const int size = 64, samples = 4;
            exposureSunTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int covered = 0;
                for (int sy = 0; sy < samples; sy++) for (int sx = 0; sx < samples; sx++)
                {
                    float px = (x + (sx + .5f) / samples) / size * 2 - 1;
                    float py = (y + (sy + .5f) / samples) / size * 2 - 1;
                    float radius = Mathf.Sqrt(px * px + py * py), angle = Mathf.Atan2(py, px);
                    float nearestRay = Mathf.Round(angle / (Mathf.PI / 4)) * (Mathf.PI / 4);
                    float along = Mathf.Cos(angle - nearestRay) * radius, across = Mathf.Sin(angle - nearestRay) * radius;
                    float rayDistance = Mathf.Sqrt(Mathf.Pow(along - Mathf.Clamp(along, .64f, .84f), 2) + across * across);
                    if (radius <= .46f || rayDistance <= .075f) covered++;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(covered * 255 / (samples * samples)));
            }
            exposureSunTexture.SetPixels32(pixels); exposureSunTexture.Apply();
            return exposureSunSprite = Sprite.Create(exposureSunTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        static Text Label(string name, Transform parent, int size)
        { var r = Rect(name, parent); var t = r.gameObject.AddComponent<Text>(); t.font = Resources.Load<Font>("HelpHudFonts/NotoSansKR-Regular") ?? Resources.Load<Font>("Preview/BodyFont"); if (t.font == null) t.font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, size); t.fontSize = size; t.color = Color.white; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; var s = r.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, .7f); s.effectDistance = Vector2.one; return t; }
        void Update()
        {
            if (!ready || game == null) return;
            bool active = game.started && !game.paused && !game.EndingSequenceActive && game.phase == FinalePhase.Chamber && !ParallaxGameShell.BlocksGameplay;
            capture.captureEnabled = active; capture.blurStrength = 1;
            if (!active) { canvas.enabled = false; previousTotality = game.totality; return; }
            if (game.totality && !previousTotality) successUntil = Time.unscaledTime + 4f;
            if (!game.totality) successUntil = 0;
            previousTotality = game.totality;
            bool moonHeld = game.held == FinaleItem.Moon;
            // Keep the blocked message readable when placement feasibility briefly
            // changes as floor contact is resolved. Releasing the moon clears it.
            if (!moonHeld)
            {
                moonBlockedUntil = 0f;
                suppressFloorAfterBlocked = false;
            }
            else if (game.MoonPlacementBlocked)
            {
                moonBlockedUntil = Time.unscaledTime + 1.5f;
                suppressFloorAfterBlocked = true;
            }
            else if (!game.MoonFloorConstrained && Time.unscaledTime >= moonBlockedUntil)
                suppressFloorAfterBlocked = false;
            bool showMoonBlocked = moonHeld && (game.MoonPlacementBlocked || Time.unscaledTime < moonBlockedUntil ||
                (suppressFloorAfterBlocked && game.MoonFloorConstrained));
            string warning = showMoonBlocked ? "\ub2ec\uc744 \ub193\uc744 \uacf5\uac04\uc774 \uc5c6\uc2b5\ub2c8\ub2e4" :
                game.MoonFloorConstrained && moonHeld ? "\ub2ec\uc744 \ubc14\ub2e5\uc5d0\uc11c \uc62c\ub9ac\uc138\uc694" :
                game.ExitPlacementBlocked ? "\ub193\uc744 \uacf5\uac04\uc774 \uc5c6\uc2b5\ub2c8\ub2e4" : null;
            if (warning != lastWarning) { lastWarning = warning; warningStart = Time.unscaledTime; }
            bool success = warning == null && Time.unscaledTime < successUntil;
            bool staleFloorNotice = game.held != FinaleItem.Moon && game.notice == "\ubc14\ub2e5\uc744 \ub530\ub77c \ub2ec\uc744 \uc6c0\uc9c1\uc774\uace0 \uc788\uc2b5\ub2c8\ub2e4. \uc704\ub97c \ubc14\ub77c\ubcf4\uba74 \ub2e4\uc2dc \ub4e4\uc5b4 \uc62c\ub9b4 \uc218 \uc788\uc2b5\ub2c8\ub2e4.";
            bool timed = warning == null && !success && !staleFloorNotice && game.noticeUntil > Time.unscaledTime && !string.IsNullOrEmpty(game.notice);
            bool sun = warning == null && !success && !timed && game.exposure > .035f;
            bool puzzle = warning == null && !success && !timed && !sun && moonHeld;
            if (sun) sunGaugeVisual = true;
            else if (puzzle || gaugeMorph <= 0f) sunGaugeVisual = false;
            string text = warning ?? (success ? "\ub2ec\uc774 \ud0dc\uc591\uc744 \uac00\ub838\uc2b5\ub2c8\ub2e4." : timed ? game.notice : game.ExitUnlocked ? "\ub9c8\uc9c0\ub9c9 \ucd9c\uad6c" : game.Safe ? "그늘 · 안전" : "");
            bool show = warning != null || success || timed || sun || puzzle || game.ExitUnlocked || game.Safe;
            visibility = Mathf.MoveTowards(visibility, show ? 1f : 0f, Time.unscaledDeltaTime / .25f);
            if (show) transition.Update(new MuseumHudTextTransition.View { Mode = sun || puzzle ? 1 : 0, Caption = text }, Time.unscaledDeltaTime, warning != null || sun);
            else if (visibility <= 0) transition.Reset();
            bool wantGauge = sun || puzzle;
            gaugeMorph = sun ? 1f : Mathf.MoveTowards(gaugeMorph, wantGauge ? 1f : 0f, Time.unscaledDeltaTime / .36f);
            canvas.enabled = visibility > 0 && capture.blurred != null;
            if (!canvas.enabled) return;
            group.alpha = visibility;
            backdrop.texture = capture.blurred;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2.3f);
            Color color = warning != null ? Amber : Mint;
            float breath = .5f - .5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / 2.4f));
            caption.text = transition.Current.Caption ?? "";
            float gaugeAlpha = Mathf.Clamp01(gaugeMorph);
            // Fade the outgoing success caption, indicator and glow together.
            // Check the displayed caption so it cannot flash back during the text transition.
            bool displayingSuccess = caption.text == "\ub2ec\uc774 \ud0dc\uc591\uc744 \uac00\ub838\uc2b5\ub2c8\ub2e4.";
            float successFade = displayingSuccess ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((successUntil - Time.unscaledTime) / .4f)) : 1f;

            float indicatorAlpha = !string.IsNullOrEmpty(caption.text) ? transition.Alpha * Mathf.Lerp(.55f, 1f, breath) * (1 - gaugeAlpha) * successFade : 0f;
            if (warning != null && Time.unscaledTime - warningStart < .55f)
                indicatorAlpha *= Mathf.Lerp(.3f, 1f, Mathf.Abs(Mathf.Sin((Time.unscaledTime - warningStart) / .55f * Mathf.PI * 2f)));
            indicator.color = new Color(color.r, color.g, color.b, indicatorAlpha);
            halo.color = new Color(color.r, color.g, color.b, indicatorAlpha * .85f);
            ring.color = new Color(color.r, color.g, color.b, indicatorAlpha * .45f * Mathf.Lerp(.15f, .65f, breath));
            indicator.transform.localScale = halo.transform.localScale = Vector3.one * Mathf.Lerp(.92f, 1.12f, breath);
            ring.transform.localScale = Vector3.one * Mathf.Lerp(.92f, 1.12f, breath) * Mathf.Lerp(.85f, 1.25f, breath);
            float shake = warning != null && Time.unscaledTime - warningStart < .42f ? Mathf.Sin((Time.unscaledTime - warningStart) * 33f) * 7f * (1f - (Time.unscaledTime - warningStart) / .42f) : 0;
            panel.anchoredPosition = new Vector2(shake, 0);
            // Measure the caption at its final width, as in the museum HUD.
            float available = Mathf.Max(1f, ((RectTransform)canvas.transform).rect.width - 56f);
            float textWidth = Mathf.Min(Mathf.Max(1f, available - 76f), Mathf.Max(1f, caption.preferredWidth));
            var settings = caption.GetGenerationSettings(new Vector2(textWidth, 0));
            float textHeight = caption.cachedTextGeneratorForLayout.GetPreferredHeight(caption.text, settings) / caption.pixelsPerUnit;
            float descriptionWidth = Mathf.Min(available, Mathf.Max(120f, textWidth + 76f));
            float shape = Mathf.SmoothStep(0, 1, gaugeAlpha);
            float wanted = Mathf.Lerp(descriptionWidth, Mathf.Min(268f, available), shape);
            if (width <= 0) width = wanted;
            if (transition.Resizing)
            {
                if (!wasResizing) resizeStartWidth = width;
                width = Mathf.Lerp(resizeStartWidth, wanted, Mathf.SmoothStep(0, 1, transition.ResizeAmount));
                widthVelocity = 0;
            }
            else width = Mathf.SmoothDamp(width, wanted, ref widthVelocity, .08f, Mathf.Infinity, Time.unscaledDeltaTime);
            wasResizing = transition.Resizing;
            width = Mathf.Min(available, width);
            float height = Mathf.Lerp(Mathf.Max(52f, textHeight + 24f), 42f, shape);
            var captionRect = (RectTransform)caption.transform;
            captionRect.anchorMin = captionRect.anchorMax = captionRect.pivot = new Vector2(.5f, .5f);
            captionRect.anchoredPosition = new Vector2(18f, 0);
            captionRect.sizeDelta = new Vector2(textWidth, textHeight);
            if (revealCaption != caption.text) { revealCaption = caption.text; captionReveal = 0f; }
            bool captionFits = !string.IsNullOrEmpty(caption.text) && width >= descriptionWidth - .5f && gaugeAlpha <= .001f;
            if (captionFits) captionReveal = Mathf.MoveTowards(captionReveal, 1f, Time.unscaledDeltaTime / .16f);
            caption.canvasRenderer.SetAlpha(transition.Alpha * (1f - gaugeAlpha) * successFade * captionReveal);
            panel.sizeDelta = new Vector2(width, height);
            shadowRect.sizeDelta = new Vector2(width + 26, height + 26);
            shadowRect.anchoredPosition = panel.anchoredPosition + new Vector2(0, -3);
            if (shadowMaterial != null) shadowMaterial.SetVector("_PanelSize", new Vector4(width, height, 0, 0));
            exposureSun.color = new Color(1, .12f, .10f, sunGaugeVisual ? gaugeAlpha : 0f);
            gaugeBar.color = new Color(1, 1, 1, gaugeAlpha);
            var gaugeRect = (RectTransform)gaugeBar.transform;
            float gaugeLeft = sunGaugeVisual ? 50f : 14f;
            gaugeRect.anchoredPosition = new Vector2(gaugeLeft, 0);
            gaugeRect.sizeDelta = new Vector2(Mathf.Max(1, width - gaugeLeft - 14f), 14);
            if (sun || puzzle)
            {
                var sample = moonHeld ? game.CurrentMoonAlignment : default;
                float fill = sun ? game.exposure : sample.Complete && !game.MoonPlacementBlocked ? 1f : Mathf.Min(.95f, Mathf.Clamp01(sample.Coverage / MoonAlignmentRules.RequiredCoverage) * .95f);
                gauge.SetFloat("_Fill", Mathf.Clamp01(fill)); gauge.SetFloat("_PuzzleMode", puzzle ? 1 : 0);
            }
            glass.SetFloat("_PanelAspect", panel.sizeDelta.x / panel.sizeDelta.y);
            glass.SetColor("_NoticeTint", color); glass.SetFloat("_NoticeStrength", success ? (.85f + .15f * pulse) * successFade : warning != null ? .35f : 0f);
            glass.SetFloat("_SuccessGlow", success ? (.55f + .45f * pulse) * successFade : 0f);

        }
        void OnDestroy()
        {
            ready = false;
            if (ownsCapture && capture != null) Destroy(capture);
            if (canvas != null) Destroy(canvas.gameObject);
            if (glass != null) Destroy(glass); if (shadowMaterial != null) Destroy(shadowMaterial); if (gauge != null) Destroy(gauge);
            if (exposureSunSprite != null) Destroy(exposureSunSprite);
            if (exposureSunTexture != null) Destroy(exposureSunTexture);
            foreach (var sprite in indicatorSprites) if (sprite != null) Destroy(sprite);
            foreach (var texture in indicatorTextures) if (texture != null) Destroy(texture);
        }
    }
}
