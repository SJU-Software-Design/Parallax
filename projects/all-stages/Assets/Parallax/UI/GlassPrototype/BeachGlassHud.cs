using ShadeLink.Beach;
using UnityEngine;
using UnityEngine.UI;

namespace GlassTrial
{
    // Beach-only presentation; gameplay and checkpoint state remain in BeachGame.
    [DefaultExecutionOrder(100)]
    public sealed class BeachGlassHud : MonoBehaviour
    {
        const float NoticeMinHeight = 44f, NoticeVerticalPadding = 9f;
        const float ShadowPadding = 13f, ShadowFadeDistance = 11f, ShadowOffsetY = -3f;
        BeachGame game;
        GlassCameraCapture capture;
        bool ownsCapture;
        bool subscribed;
        Canvas canvas;
        CanvasGroup group;
        RawImage backdrop;
        RectTransform shadowRect;
        RawImage panelShadow;
        Material shadowMaterial;
        RawImage timer, timerGlow;
        Image dot, halo, ring;
        Image exposureBar, exposureSun;
        Material exposureMaterial;
        Texture2D exposureSunTexture;
        Sprite exposureSunSprite;
        readonly Sprite[] indicatorSprites = new Sprite[3];
        readonly Texture2D[] indicatorTextures = new Texture2D[3];
        Text caption;
        Material material;
        readonly MuseumHudTextTransition transition = new MuseumHudTextTransition();
        readonly MuseumCommonNoticeState commonNotice = new MuseumCommonNoticeState();
        readonly System.Collections.Generic.Queue<MuseumStageGuide.Notice> queuedNotices = new System.Collections.Generic.Queue<MuseumStageGuide.Notice>();
        readonly BeachHoistReminder hoistReminder = new BeachHoistReminder();
        bool previousHoistReady, hasHoistState, sunGaugeVisual;
        float gameplayNoticeRemaining;
        string gameplayNotice;
        float visibility, width, noticeClock, exposureMorph, widthVelocity, resizeStartWidth, captionReveal;
        bool wasResizing;
        string revealCaption;
        bool wasDanger;
        bool initialized;
        // Keep the legacy notice until a captured background actually exists.
        public bool Ready => isActiveAndEnabled && initialized && capture != null && capture.blurred != null;
        public bool OwnsCapture => isActiveAndEnabled && initialized && capture != null;

        void Start()
        {
            game = GetComponent<BeachGame>();
            var blur = Resources.Load<Shader>("GlassBlur");
            var glass = Resources.Load<Shader>("TutorialIslandGlass");
            var gauge = Resources.Load<Shader>("GlassExposureGauge");
            var shadowShader = Resources.Load<Shader>("TutorialIslandShadow");
            if (game == null || game.view == null || blur == null || glass == null || gauge == null || !blur.isSupported || !glass.isSupported || !gauge.isSupported)
            {
                Debug.LogWarning("Beach glass HUD unavailable; using original notice.");
                enabled = false;
                return;
            }
            capture = game.view.GetComponent<GlassCameraCapture>();
            if (capture == null) { capture = game.view.gameObject.AddComponent<GlassCameraCapture>(); ownsCapture = true; }
            capture.blurShader = blur;
            material = new Material(glass) { hideFlags = HideFlags.HideAndDontSave };
            exposureMaterial = new Material(gauge) { hideFlags = HideFlags.HideAndDontSave };
            if (shadowShader != null && shadowShader.isSupported)
                shadowMaterial = new Material(shadowShader) { hideFlags = HideFlags.HideAndDontSave };
            exposureMaterial.SetFloat("_PuzzleMode", 0);
            material.SetFloat("_GlassMode", 1);
            material.SetFloat("_CornerRadius", 1);
            material.SetFloat("_Refraction", 1);
            material.SetFloat("_RefractionStrength", 9);
            material.SetFloat("_RefractionWidth", .18f);
            material.SetFloat("_LensDistortion", 0);
            material.SetFloat("_EdgeContrast", 1);
            material.SetFloat("_EdgeContrastStrength", .35f);
            material.SetFloat("_SpectrumPixels", 3);
            material.SetFloat("_WhiteTintStrength", .14f);
            material.SetFloat("_DarkTintStrength", .26f);
            material.SetFloat("_Saturation", 1.45f);
            material.SetFloat("_ExtraBlurPixels", 9.5f);
            material.SetFloat("_MilkyTint", 0);

            var root = new GameObject("Beach glass HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 19;
            group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var panel = NewRect("Glass panel", root.transform);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f / 6f);
            panel.pivot = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(500, NoticeMinHeight);
            shadowRect = NewRect("Soft glass shadow", root.transform);
            shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(.5f, 1f / 6f);
            shadowRect.pivot = new Vector2(.5f, .5f);
            panelShadow = shadowRect.gameObject.AddComponent<RawImage>(); panelShadow.texture = Texture2D.whiteTexture;
            panelShadow.material = shadowMaterial; panelShadow.enabled = shadowMaterial != null;
            panelShadow.color = new Color(0, 0, 0, .38f); panelShadow.raycastTarget = false;
            if (shadowMaterial != null) { shadowMaterial.SetFloat("_Padding", ShadowPadding); shadowMaterial.SetFloat("_FadeDistance", ShadowFadeDistance); }
            shadowRect.SetAsFirstSibling();
            backdrop = panel.gameObject.AddComponent<RawImage>();
            backdrop.material = material; backdrop.color = new Color(1, 1, 1, .97f); backdrop.raycastTarget = false;
            var textRect = NewRect("Notice", panel);
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            // Reserve space for the indicator while keeping the caption centered
            // in the remaining area, including when the panel wraps on narrow screens.
            textRect.offsetMin = new Vector2(56, NoticeVerticalPadding); textRect.offsetMax = new Vector2(-20, -NoticeVerticalPadding);
            caption = textRect.gameObject.AddComponent<Text>();
            caption.font = Resources.Load<Font>("HelpHudFonts/NotoSansKR-Regular");
            if (caption.font == null) caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 20; caption.color = Color.white; caption.alignment = TextAnchor.MiddleCenter;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap; caption.verticalOverflow = VerticalWrapMode.Overflow;
            caption.raycastTarget = false;
            var shadow = textRect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .75f); shadow.effectDistance = new Vector2(1, -1);
            for (int i = 0; i < 3; i++) indicatorSprites[i] = MakeIndicator(i, out indicatorTextures[i]);
            halo = Indicator("Notice halo", panel, indicatorSprites[1], 34);
            ring = Indicator("Notice ring", panel, indicatorSprites[2], 18);
            dot = Indicator("Notice dot", panel, indicatorSprites[0], 8);
            timerGlow = Timer("Notice timer glow", panel);
            timer = Timer("Notice timer", panel);
            var sunRect = NewRect("Exposure sun", panel);
            sunRect.anchorMin = sunRect.anchorMax = new Vector2(0, .5f); sunRect.pivot = new Vector2(0, .5f);
            sunRect.anchoredPosition = new Vector2(16, 0); sunRect.sizeDelta = new Vector2(26, 26);
            exposureSun = sunRect.gameObject.AddComponent<Image>(); exposureSun.sprite = MakeExposureSun(); exposureSun.raycastTarget = false;
            var barRect = NewRect("Exposure gauge", panel);
            barRect.anchorMin = barRect.anchorMax = new Vector2(0, .5f); barRect.pivot = new Vector2(0, .5f);
            barRect.anchoredPosition = new Vector2(50, 0); barRect.sizeDelta = new Vector2(204, 14);
            exposureBar = barRect.gameObject.AddComponent<Image>(); exposureBar.material = exposureMaterial; exposureBar.raycastTarget = false;
            canvas.enabled = false;
            initialized = true;
            Subscribe();
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        public static float NoticeHeight(float textHeight)
        {
            return Mathf.Max(NoticeMinHeight, Mathf.Max(0, textHeight) + NoticeVerticalPadding * 2);
        }

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

        static Image Indicator(string name, Transform parent, Sprite sprite, float size)
        {
            var rect = NewRect(name, parent); rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(32, 0); rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        static RawImage Timer(string name, Transform parent)
        {
            var rect = NewRect(name, parent); rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
            var image = rect.gameObject.AddComponent<RawImage>(); image.texture = Texture2D.whiteTexture; image.raycastTarget = false;
            return image;
        }

        void OnCheckpointNotice(bool reset, string text)
        {
            var kind = reset ? MuseumStageGuide.NoticeKind.Reset : MuseumStageGuide.NoticeKind.Save;
            var notice = new MuseumStageGuide.Notice(kind, text, CheckpointNoticeSeconds(reset, text));
            bool blocked = game != null && (!game.active || !game.safe || game.exposure > 0);
            if (reset)
            {
                queuedNotices.Clear();
                commonNotice.Trigger(notice, noticeClock, false);
            }
            else if (blocked || commonNotice.Active) queuedNotices.Enqueue(notice);
            else commonNotice.Trigger(notice, noticeClock, false);
        }

        public static MuseumStageGuide.Notice ClassifyInteractionWarning(string text)
        {
            return text == "몸에서 조금 떨어진 곳에 놓으세요."
                ? new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.Warning, text, 2f)
                : MuseumStageGuide.ClassifyNotice(text);
        }

        public static float CheckpointNoticeSeconds(bool reset, string text)
        {
            return reset ? 3f : 2.5f;
        }

        void Subscribe()
        {
            if (game == null || subscribed) return;
            game.UiNoticeIssued += ReceiveNotice;
            subscribed = true;
        }

        void ReceiveNotice(string source)
        {
            string text = BeachHudState.FilterNotice(source);
            if (string.IsNullOrEmpty(text)) return;
            var classified = BeachHudState.ClassifyCommonNotice(text);
            bool blocked = !game.active || !game.safe || game.exposure > 0;
            if (classified.Kind == MuseumStageGuide.NoticeKind.Warning)
            {
                if (commonNotice.Active && commonNotice.Current.Kind == MuseumStageGuide.NoticeKind.Save)
                    queuedNotices.Enqueue(commonNotice.Current);
                if (blocked || (commonNotice.Active && commonNotice.Current.Kind == MuseumStageGuide.NoticeKind.Reset)) queuedNotices.Enqueue(classified);
                else commonNotice.Trigger(classified, noticeClock, false);
            }
            else if (classified.Kind == MuseumStageGuide.NoticeKind.Save || classified.Kind == MuseumStageGuide.NoticeKind.Reset)
                OnCheckpointNotice(classified.Kind == MuseumStageGuide.NoticeKind.Reset, classified.Text);
            else
            {
                gameplayNotice = text;
                gameplayNoticeRemaining = text == "수신기가 활성화 되었어요" ? 3f : Mathf.Max(0, game.noticeUntil - Time.unscaledTime);
            }
        }

        void OnDisable()
        {
            if (game != null && subscribed) { game.UiNoticeIssued -= ReceiveNotice; subscribed = false; }
            if (canvas != null) canvas.enabled = false;
            if (ownsCapture && capture != null) capture.captureEnabled = false;
        }

        void OnEnable() { if (initialized) Subscribe(); }

        void LateUpdate()
        {
            if (!initialized || game == null || capture == null) return;
            bool active = game.active && !game.won && (game.skyShip == null || !game.skyShip.Flying);
            bool danger = active && (!game.safe || game.exposure > 0);
            if (wasDanger && !danger) transition.Reset();
            wasDanger = danger;
            if (active && !danger) noticeClock += Time.unscaledDeltaTime;
            float now = noticeClock;
            if (active && !danger && gameplayNoticeRemaining > 0) gameplayNoticeRemaining = Mathf.Max(0, gameplayNoticeRemaining - Time.unscaledDeltaTime);
            var hoist = game.checkpoint == 3 ? game.counterweightFinale : null;
            bool hoistReady = hoist != null && hoist.Ready;
            if (active && hasHoistState && !previousHoistReady && hoistReady)
            { gameplayNotice = "인양을 마쳤어요"; gameplayNoticeRemaining = 3f; }
            hasHoistState = hoist != null; previousHoistReady = hoistReady;
            commonNotice.Update(now, false);
            if (!danger && !commonNotice.Active && queuedNotices.Count > 0)
                commonNotice.Trigger(queuedNotices.Dequeue(), now, false);
            bool showCommon = active && !danger && commonNotice.Active;
            bool hoistMoving = hoist != null && hoist.LoadLatched && !hoist.Ready;
            string reminder = hoistReminder.Update(hoistMoving, active && !danger && !showCommon && gameplayNoticeRemaining <= 0, Time.unscaledDeltaTime);
            bool shadowPuzzle = game.checkpoint == 2 && game.shadowHarbor != null && !game.shadowHarbor.Solved && game.shadowHarbor.target != null && game.view != null && Vector3.Distance(game.view.transform.position, game.shadowHarbor.target.position) < 18f;
            float progress = hoistMoving ? hoist.LiftProgress : shadowPuzzle ? BeachHudState.ShadowProgress(game.shadowHarbor.HullCoverage, game.shadowHarbor.CanopyCoverage, game.shadowHarbor.Coverage, game.shadowHarbor.Clearance, game.shadowHarbor.Charge) : 0;
            string boarded = game.checkpoint == 3 && game.skyShip != null && game.skyShip.IsAboard && hoistReady ? "E를 눌러 출항" : game.checkpoint == 2 && game.boat != null && game.boat.IsAboard && !game.boat.Departed ? "그늘 안에서 E로 출항" : null;
            string transient = showCommon ? commonNotice.Current.Text : gameplayNoticeRemaining > 0 ? gameplayNotice : null;
            var view = BeachHudState.Select(!danger, game.exposure, transient, hoistMoving || shadowPuzzle, progress, reminder, boarded);
            string notice = active && view.Mode != BeachHudState.Mode.Sunlight && view.Mode != BeachHudState.Mode.Puzzle ? view.Text : null;
            bool gaugeRequested = active && (view.Mode == BeachHudState.Mode.Sunlight || view.Mode == BeachHudState.Mode.Puzzle);
            if (danger) sunGaugeVisual = true;
            else if (view.Mode == BeachHudState.Mode.Puzzle || exposureMorph <= 0) sunGaugeVisual = false;
            bool requested = active && (gaugeRequested || !string.IsNullOrEmpty(notice));
            visibility = Mathf.MoveTowards(visibility, requested ? 1 : 0, Time.unscaledDeltaTime / .25f);
            if (!requested && visibility <= 0) transition.Reset();
            bool warning = showCommon && commonNotice.Current.Kind == MuseumStageGuide.NoticeKind.Warning;
            if (requested) transition.Update(new MuseumHudTextTransition.View { Mode = gaugeRequested ? 1 : 0, Caption = notice }, Time.unscaledDeltaTime, warning || danger);
            exposureMorph = Mathf.MoveTowards(exposureMorph, gaugeRequested ? 1 : 0, Time.unscaledDeltaTime / .36f);
            var hover = GetComponent<BeachGlassHover>();
            capture.captureEnabled = visibility > 0 || (hover != null && hover.NeedsCapture);
            canvas.enabled = active && visibility > 0 && capture.blurred != null;
            if (!canvas.enabled) return;
            backdrop.texture = capture.blurred;
            bool renderGauge = gaugeRequested || exposureMorph > 0;
            caption.text = transition.Current.Caption ?? "";
            float gaugeAlpha = renderGauge ? Mathf.Clamp01(exposureMorph) : 0;
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
            caption.canvasRenderer.SetAlpha(transition.Alpha * (1f - gaugeAlpha) * captionReveal);
            var panel = (RectTransform)backdrop.transform;
            panel.sizeDelta = new Vector2(width, height);
            shadowRect.sizeDelta = new Vector2(width + ShadowPadding * 2, height + ShadowPadding * 2);
            if (shadowMaterial != null) shadowMaterial.SetVector("_PanelSize", new Vector4(width, height, 0, 0));
            material.SetFloat("_PanelAspect", width / height);
            float age = now - commonNotice.StartedAt;
            bool reset = showCommon && commonNotice.Current.Kind == MuseumStageGuide.NoticeKind.Reset;
            Color color = warning ? new Color(251f / 255f, 191f / 255f, 36f / 255f) : reset ? new Color(56f / 255f, 189f / 255f, 248f / 255f) :
                showCommon ? new Color(52f / 255f, 211f / 255f, 153f / 255f) : new Color(77f / 255f, 238f / 255f, 234f / 255f);
            float duration = reset ? .48f : .52f;
            float motion = showCommon && !warning && age < duration ? (reset ? 6f : -4f) * (1f - Mathf.SmoothStep(0, 1, age / duration)) : 0;
            float motionX = warning && age < .42f ? Mathf.Sin(age * 33f) * 7f * (1f - age / .42f) : 0;
            float scale = showCommon && !warning && age < duration ? Mathf.Lerp(reset ? .97f : .96f, 1, Mathf.SmoothStep(0, 1, age / duration)) : 1;
            panel.anchoredPosition = new Vector2(motionX, motion); panel.localScale = Vector3.one * scale;
            shadowRect.anchoredPosition = panel.anchoredPosition + new Vector2(0, ShadowOffsetY);
            shadowRect.localScale = panel.localScale;
            material.SetColor("_NoticeTint", color);
            material.SetFloat("_NoticeStrength", showCommon ? transition.Alpha : 0);
            float ringTime = reset ? .85f : 1.1f;
            float ringAmount = Mathf.Clamp01(age / ringTime);
            float breath = .5f - .5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / 2.4f));
            float ringScale = showCommon ? Mathf.Lerp(reset ? .5f : .6f, reset ? 1.75f : 1.9f, ringAmount) : Mathf.Lerp(.92f, 1.12f, breath) * Mathf.Lerp(.85f, 1.25f, breath);
            float dotScale = showCommon ? (reset ? 1 : 1 + .35f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(age / .65f))) : Mathf.Lerp(.92f, 1.12f, breath);
            float dotAlpha = showCommon ? 1 : Mathf.Lerp(.55f, 1f, breath);
            if (warning)
            {
                float blink = Mathf.Clamp01(age / .55f);
                dotAlpha = blink < 1 ? Mathf.Lerp(.3f, 1f, Mathf.Abs(Mathf.Sin(blink * Mathf.PI * 2f))) : 1f;
                dotScale = blink < 1 ? Mathf.Lerp(.8f, 1.25f, dotAlpha) : 1f;
                ringScale = 1f;
                ringAmount = 0f;
            }
            float indicatorAlpha = !string.IsNullOrEmpty(caption.text) ? transition.Alpha * dotAlpha * (1 - gaugeAlpha) : 0;
            dot.color = new Color(color.r, color.g, color.b, indicatorAlpha);
            halo.color = new Color(color.r, color.g, color.b, indicatorAlpha * .85f);
            ring.color = new Color(color.r, color.g, color.b, indicatorAlpha * .45f * (showCommon ? 1 - ringAmount : Mathf.Lerp(.15f, .65f, breath)));
            dot.transform.localScale = halo.transform.localScale = Vector3.one * dotScale;
            ring.transform.localScale = Vector3.one * ringScale;
            float timerWidth = Mathf.Max(0, width - 36) * commonNotice.Remaining(now);
            var timerRect = (RectTransform)timer.transform; timerRect.anchoredPosition = new Vector2(18, 4); timerRect.sizeDelta = new Vector2(timerWidth, 2);
            var glowRect = (RectTransform)timerGlow.transform; glowRect.anchoredPosition = new Vector2(18, 6); glowRect.sizeDelta = new Vector2(timerWidth, 6);
            timer.color = new Color(color.r, color.g, color.b, showCommon ? transition.Alpha : 0);
            timerGlow.color = new Color(color.r, color.g, color.b, showCommon ? transition.Alpha * .22f : 0);
            exposureMaterial.SetFloat("_Fill", sunGaugeVisual ? Mathf.Clamp01(game.exposure) : progress);
            exposureMaterial.SetFloat("_PuzzleMode", sunGaugeVisual ? 0f : 1f);
            exposureSun.color = new Color(1, .12f, .10f, sunGaugeVisual ? gaugeAlpha : 0);
            exposureBar.color = new Color(1, 1, 1, gaugeAlpha);
            var gaugeRect = (RectTransform)exposureBar.transform;
            float gaugeLeft = sunGaugeVisual ? 50 : 14;
            gaugeRect.anchoredPosition = new Vector2(gaugeLeft, 0);
            gaugeRect.sizeDelta = new Vector2(Mathf.Max(1, width - gaugeLeft - 14), 14);
            group.alpha = visibility;
        }

        void OnDestroy()
        {
            initialized = false;
            if (game != null && subscribed) game.UiNoticeIssued -= ReceiveNotice;
            if (canvas != null) Destroy(canvas.gameObject);
            if (material != null) Destroy(material);
            if (exposureMaterial != null) Destroy(exposureMaterial);
            if (exposureSunSprite != null) Destroy(exposureSunSprite);
            if (exposureSunTexture != null) Destroy(exposureSunTexture);
            if (shadowMaterial != null) Destroy(shadowMaterial);
            if (ownsCapture && capture != null)
            {
                var hover = GetComponent<BeachGlassHover>();
                if (hover == null || !hover.TakeCaptureOwnership(capture)) Destroy(capture);
            }
            foreach (var sprite in indicatorSprites) if (sprite != null) Destroy(sprite);
            foreach (var texture in indicatorTextures) if (texture != null) Destroy(texture);
        }
    }
}