using System.Collections;
using ShadeLink.Gallery;
using UnityEngine;
using UnityEngine.UI;

namespace GlassTrial
{
    // Displays the same status and progress as the Gallery HUD in glass form.
    [DefaultExecutionOrder(200)]
    public sealed class GlassExposureGauge : MonoBehaviour
    {
        GalleryGame game;
        GlassPanelPreview preview;
        Canvas canvas;
        RawImage backdrop;
        Text caption;
        Image bar, sun, safeIndicator, safeHalo, safeRing;
        Material glass, gauge;
        Texture2D sunTexture, safeTexture, haloTexture, ringTexture;
        Sprite sunSprite, safeSprite, haloSprite, ringSprite;
        float morph, completionUntil, previousProgress;
        bool ready, fadingDanger;
        int previousContext = -1;
        bool previousStarted, completionArmed;

        public static float VisiblePanelHeight(GalleryGame game)
        {
            var instance = game != null ? game.GetComponent<GlassExposureGauge>() : null;
            return instance != null && instance.canvas != null && instance.canvas.enabled &&
                instance.backdrop != null && instance.backdrop.enabled
                ? ((RectTransform)instance.backdrop.transform).rect.height : 0f;
        }

        public static bool CanDisplay(GalleryGame game)
        {
            if (game == null || !game.active || game.won || game.Transitioning || game.conceptCapture || game.exhibitionPreview || GlassPanelPreview.ArrivalBlocksHud(game)) return false;
            var instance = game.GetComponent<GlassExposureGauge>();
            if (instance == null || !instance.ready) return false;
            return true;
        }

        IEnumerator Start()
        {
            game = GetComponent<GalleryGame>();
            preview = GetComponent<GlassPanelPreview>();
            yield return null;

            var glassShader = Resources.Load<Shader>("GlassPanel");
            var gaugeShader = Resources.Load<Shader>("GlassExposureGauge");
            if (preview == null || glassShader == null || gaugeShader == null || !glassShader.isSupported || !gaugeShader.isSupported) yield break;

            while (preview != null && preview.enabled && preview.Capture == null) yield return null;
            if (preview == null || preview.Capture == null) yield break;

            glass = new Material(glassShader) { hideFlags = HideFlags.HideAndDontSave };
            gauge = new Material(gaugeShader) { hideFlags = HideFlags.HideAndDontSave };

            var root = new GameObject("Light exposure glass UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 19;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;

            var panel = Rect("Glass panel", root.transform, Vector2.zero, new Vector2(268, 52));
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f / 6f);
            panel.pivot = new Vector2(.5f, .5f);
            backdrop = panel.gameObject.AddComponent<RawImage>();
            backdrop.material = glass;
            backdrop.color = new Color(1, 1, 1, .97f);
            backdrop.raycastTarget = false;

            var text = Rect("Exposure status", panel, new Vector2(20, -17), new Vector2(337, 70));
            caption = text.gameObject.AddComponent<Text>();
            caption.font = game.font != null ? game.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 20;
            caption.alignment = TextAnchor.MiddleLeft;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Overflow;
            caption.color = Color.white;
            caption.raycastTarget = false;

            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .65f);
            shadow.effectDistance = new Vector2(1, -1);

            var barRect = Rect("Status gauge", panel, new Vector2(68, -43.3f), new Vector2(278, 17.375f));
            bar = barRect.gameObject.AddComponent<Image>();
            bar.material = gauge;
            bar.raycastTarget = false;

            sunSprite = MakeIcon(false, out sunTexture);
            var sunRect = Rect("Exposure sun", panel, new Vector2(20, -32), new Vector2(40, 40));
            sun = sunRect.gameObject.AddComponent<Image>();
            sun.sprite = sunSprite;
            sun.raycastTarget = false;

            safeSprite = MakeSafeIndicator(0, out safeTexture);
            haloSprite = MakeSafeIndicator(1, out haloTexture);
            ringSprite = MakeSafeIndicator(2, out ringTexture);
            var haloRect = Rect("Safe indicator halo", panel, new Vector2(24, -36), new Vector2(32, 32));
            haloRect.pivot = new Vector2(.5f, .5f); haloRect.anchoredPosition = new Vector2(40, -52);
            safeHalo = haloRect.gameObject.AddComponent<Image>(); safeHalo.sprite = haloSprite; safeHalo.raycastTarget = false;
            var ringRect = Rect("Safe indicator ring", panel, new Vector2(32, -44), new Vector2(16, 16));
            ringRect.pivot = new Vector2(.5f, .5f); ringRect.anchoredPosition = new Vector2(40, -52);
            safeRing = ringRect.gameObject.AddComponent<Image>(); safeRing.sprite = ringSprite; safeRing.raycastTarget = false;
            var safeRect = Rect("Safe indicator", panel, new Vector2(36, -48), new Vector2(8, 8));
            safeRect.pivot = new Vector2(.5f, .5f); safeRect.anchoredPosition = new Vector2(40, -52);
            safeIndicator = safeRect.gameObject.AddComponent<Image>(); safeIndicator.sprite = safeSprite; safeIndicator.raycastTarget = false;

            ApplyGlassSettings();
            ready = true;
        }

        // Three separate alpha masks: solid dot, soft radial glow and a one-unit ring.
        static Sprite MakeSafeIndicator(int shape, out Texture2D texture)
        {
            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f - size * .5f) / (size * .5f), dy = (y + .5f - size * .5f) / (size * .5f);
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float a;
                if (shape == 1)
                {
                    float falloff = Mathf.Clamp01((radius - .25f) / .75f);
                    a = Mathf.Exp(-4f * falloff * falloff) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.90f, 1f, radius)));
                }
                else if (shape == 2)
                {
                    float outer = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.92f, 1f, radius));
                    float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.79f, .87f, radius));
                    a = outer * inner;
                }
                else a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.88f, 1f, radius));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * a));
            }
            texture.SetPixels32(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        // Reference: a red disc with eight separate rounded rays, without the square background.
        static Sprite MakeIcon(bool star, out Texture2D texture)
        {
            const int size = 64, samples = 4;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int covered = 0;
                for (int sy = 0; sy < samples; sy++) for (int sx = 0; sx < samples; sx++)
                {
                    float px = (x + (sx + .5f) / samples) / size * 2 - 1;
                    float py = (y + (sy + .5f) / samples) / size * 2 - 1;
                    bool inside;
                    if (star) inside = Mathf.Abs(px) + Mathf.Abs(py) < .72f && (Mathf.Abs(px * py) < .035f);
                    else
                    {
                        float r = Mathf.Sqrt(px * px + py * py);
                        float angle = Mathf.Atan2(py, px);
                        float nearestRay = Mathf.Round(angle / (Mathf.PI / 4)) * (Mathf.PI / 4);
                        float along = Mathf.Cos(angle - nearestRay) * r;
                        float across = Mathf.Sin(angle - nearestRay) * r;
                        float rayDistance = Mathf.Sqrt(Mathf.Pow(along - Mathf.Clamp(along, .64f, .84f), 2) + across * across);
                        inside = r <= .46f || rayDistance <= .075f;
                    }
                    if (inside) covered++;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(covered * 255 / (samples * samples)));
            }
            texture.SetPixels32(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        void ApplyGlassSettings()
        {
            if (glass == null || preview == null) return;

            // Only this material uses pill corners; other glass panels retain their settings.
            glass.SetFloat("_GlassMode", 1);
            glass.SetFloat("_CornerRadius", 1);
            glass.SetFloat("_Refraction", preview.refraction ? 1 : 0);
            glass.SetFloat("_RefractionStrength", preview.refractionStrength);
            glass.SetFloat("_RefractionWidth", preview.refractionWidth);
            glass.SetFloat("_LensDistortion", preview.lensDistortion);
            glass.SetFloat("_EdgeContrast", preview.edgeContrast ? 1 : 0);
            glass.SetFloat("_EdgeContrastStrength", preview.edgeContrastStrength);
            glass.SetFloat("_SpectrumPixels", preview.spectrumPixels);
            glass.SetFloat("_WhiteTintStrength", preview.whiteTint);
            glass.SetFloat("_MilkyTint", preview.milkyTint);
        }

        // This value is purely visual: neither puzzle progress nor game timing depends on it.
        static float AdvanceMorph(float value, bool showGauge, float deltaTime)
        {
            return Mathf.MoveTowards(value, showGauge ? 1 : 0, deltaTime / .36f);
        }

        static bool CompletionEdge(bool armed, bool started, bool wasStarted, float previous, float current)
        {
            return armed && (started || wasStarted) && previous < 1f && current >= 1f;
        }

        static string NikePanelText(string message)
        {
            switch (message)
            {
                case "조명 앞의 전시물을 옮겨 빛길을 열어 보세요.":
                case "그림자 전체를 벽의 금색 윤곽 안에 넣어 보세요.":
                case "그림자가 들어왔어요 · 조각상을 내려놓으세요.":
                case "문 중앙의 홈에 액자 크기를 맞춰 끼우세요.":
                case "액자가 맞물렸어요 · 열린 문으로 이동하세요.":
                case "발자국을 따라 그늘로 이동하세요.":
                case "그림을 세워 문 앞의 빛을 가리세요.":
                case "날개 그림자를 보면서 니케의 위치를 맞춰 보세요.":
                case "조각상이 안착하면 출구 앞의 그림자를 확인해요.":
                case "날개 그림자가 출구까지 이어졌어요.":
                case "니케를 옮겨 문 앞 표시를 그림자로 덮어 보세요.":
                case "그림자가 출구와 이어져요 · 조명을 내려놓아 고정하세요.":
                case "조명을 옮겨 니케의 그림자를 출구 발판으로 보내 보세요.":
                case "조명을 내려놓고 안착할 때까지 잠깐 기다려 주세요.":
                case "니케의 그림자가 출구와 이어졌어요 · 그늘을 따라 이동하세요.":
                case "조명을 왼쪽 앞쪽 바닥으로 옮겨 니케의 그림자를 출구 발판과 이어 보세요.":
                case "전원선이 닿는 앞쪽 바닥에 조명을 놓아 주세요.":
                case "니케가 그림으로 변하고 있어요 · 잠깐 기다려 주세요.":
                    return "";
                default:
                    return message;
            }
        }

        static string PanelText(string message)
        {
            switch (message)
            {
                case "병을 옮겨 키우고 좁은 입구를 넓혀 보세요":
                case "입구가 넓어졌어요 · 병을 들어 아래로 기울여 보세요":
                case "입구를 향해 움직이고 있어요 · 조금 더 기울여 보세요":
                case "조각이 나올 수 있도록 병 입구 아래 공간을 비워 주세요":
                case "오른쪽이 가벼워요 · 조각을 조금 키워 보세요":
                case "오른쪽이 무거워요 · 조각을 조금 줄여 보세요":
                case "수평이에요 · 조각을 내려놓고 잠깐 기다리세요":
                case "디케가 놓아준 검을 클릭해 집으세요":
                case "검이 떨어지고 있어요 · 아래 받침에서 집을 수 있어요":
                case "검으로 봉인을 풀었어요 · 열린 문으로 이동하세요":
                case "검을 받침대의 홈에 맞춰 넣고 있어요":
                case "작품을 내려놓으면 얼굴을 확인할 수 있어요.":
                case "얼굴이 보이는 전시실 앞쪽에서 바라봐 주세요.":
                case "조각을 조금 더 키워 보세요":
                case "충분히 커졌어요 · 이제 내려놓으세요":
                case "내려놓은 조각이 안착하면 문이 열려요":
                case "통로에 맞게 조각을 맞추고 있어요":
                case "손이 열리고 있습니다":
                case "계단을 올라 두 손 사이로 들어가세요":
                    return "";
                default:
                    return message;
            }
        }

        void LateUpdate()
        {
            if (!ready) return;
            bool visible = CanDisplay(game);
            canvas.enabled = visible;
            if (!visible) { previousContext = -1; completionUntil = 0; morph = 0; fadingDanger = false; return; }

            backdrop.texture = preview.Capture.blurred;
            ApplyGlassSettings();

            caption.text = game.bottleBalance != null && game.bottleBalance.InRoom ? game.bottleBalance.Status :
                game.conceptLayout && game.checkpoint >= 2 ?
                (game.checkpoint == 2 ? (game.portraitGate == null ? "눈·코·입을 얼굴처럼 놓아 보세요" : game.portraitGate.Status) :
                game.orcusExit != null && game.orcusExit.cathedralActivation != null ? game.orcusExit.cathedralActivation.Status : "두 손 사이로 해변이 보입니다") :
                game.conceptLayout && game.checkpoint == 1 && game.nikeLight != null ?
                NikePanelText(game.nikeLight.Status) : "그늘 · 안전";
            caption.text = PanelText(caption.text);
            // Keep the socket instruction until the blade is actually seated.
            // Cancelling insertion returns the sword to its recovery pad and ends the guide.
            var reward = game.bottleBalance != null ? game.bottleBalance.Reward : null;
            bool swordGuide = reward != null && (reward.Carrying || reward.Busy);
            bool doorGuide = reward != null && reward.Unlocked && game.bottleBalance.Door != null &&
                game.bottleBalance.Door.Opening < 1f;
            bool balanceGuide = reward != null && game.bottleBalance.InRoom &&
                reward.Stage == GalleryBottleBalanceKeyReward.RewardStage.Flashing;
            if (swordGuide)
                caption.text = "검을 들었어요 · 방 가운데 대리석 받침대의 홈을 클릭하세요";
            else if (doorGuide)
                caption.text = "검이 고정됐어요 · 봉인이 풀리고 있어요";

            int context = 0;
            bool started = false, complete = false;
            float progress = 0;
            if (game.bottleBalance != null && game.bottleBalance.InRoom)
            {
                context = 1;
                progress = game.bottleBalance.BalanceProgress;
                complete = game.bottleBalance.Solved;
                started = progress > 0 && !complete;
            }
            else if (game.conceptLayout && game.checkpoint == 2 && game.portraitGate != null)
            {
                context = 2;
                var gate = game.portraitGate;
                float alignment = gate.portrait != null ? gate.portrait.Alignment01 : 0;
                progress = gate.Opened ? 1 : gate.Performing ? gate.RevealProgress : alignment * .65f + gate.Progress * .35f;
                complete = gate.Opened;
                started = !complete && (alignment > 0 || gate.Progress > 0 || gate.Performing);
            }
            else if (game.conceptLayout && game.checkpoint == 3 && game.orcusExit != null && game.orcusExit.cathedralActivation != null)
            {
                context = 3;
                var activation = game.orcusExit.cathedralActivation;
                progress = activation.Progress;
                complete = activation.Opened;
                started = progress > 0 && !complete;
            }
            else if (game.conceptLayout && game.checkpoint == 1 && game.nikeLight != null)
            {
                context = 4;
                var light = game.nikeLight;
                var nikeCanvas = light.canvas;
                bool fusing = game.NikeCanvasPuzzle && nikeCanvas != null && nikeCanvas.Fusing;
                complete = game.NikeCanvasPuzzle && nikeCanvas != null && nikeCanvas.Complete;
                progress = fusing ? nikeCanvas.FusionProgress : light.ShadeReadiness;
                started = !complete && (fusing || progress > 0);
                // Keep the original canvas guide available whenever alignment stops.
                // Fusion and completed artwork retain their own presentation.
                if (nikeCanvas != null && !fusing && !complete)
                    caption.text = "빈 캔버스와 주변 전시물을 살펴보세요.";
                if (nikeCanvas != null && (nikeCanvas.Drawing || (fusing && nikeCanvas.BrushProgress >= 1f)))
                {
                    // Keep the real brush animation unchanged; only remap the UI.
                    float duration = Mathf.Max(.1f, nikeCanvas.BrushSeconds);
                    float intro = Mathf.Min(2f, duration);
                    float elapsed = nikeCanvas.BrushProgress * duration;
                    bool showingIntro = nikeCanvas.Drawing && elapsed < intro;
                    if (showingIntro)
                    {
                        caption.text = "붓결을 따라 그림이 채워지고 있어요";
                        started = false;
                    }
                    else
                    {
                        progress = duration > intro ? Mathf.Clamp01((elapsed - intro) / (duration - intro)) : 1f;
                        started = !complete;
                    }
                }
            }

            bool danger = !game.safe || game.exposure > 0;
            if (previousContext != context)
            {
                previousContext = context;
                completionUntil = 0;
                completionArmed = progress < 1f;
            }
            else
            {
                if (progress <= 0f) completionArmed = true;
                // The displayed progress, rather than a separate solved flag, owns the 100% edge.
                if (CompletionEdge(completionArmed, started, previousStarted, previousProgress, progress))
                {
                    completionArmed = false;
                    if (!danger) completionUntil = Time.unscaledTime + .65f;
                }
                if (started && progress < 1f) completionUntil = 0;
            }
            previousProgress = progress;
            previousStarted = started;
            if (danger) completionUntil = 0;
            bool celebrating = !danger && Time.unscaledTime < completionUntil;
            bool showGauge = danger || (!swordGuide && !doorGuide && !balanceGuide &&
                (started || (celebrating && context != 1)));
            if (danger) fadingDanger = true;
            else if (started || celebrating) fadingDanger = false;
            // Safety feedback is immediate; normal morphs are reversible at any point.
            // The brief balance-complete instruction must not be obscured by a fading gauge.
            morph = danger ? 1 : balanceGuide ? 0 : AdvanceMorph(morph, showGauge, Time.unscaledDeltaTime);
            bool redGauge = danger || (fadingDanger && morph > 0);
            float gaugeAlpha = danger ? 1 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.28f, 1f, morph));
            float captionAlpha = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .5f, morph));
            float captionLift = 12f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .5f, morph));

            var panel = (RectTransform)backdrop.transform;
            float available = Mathf.Max(1, ((RectTransform)canvas.transform).rect.width - 56f);
            float maxTextWidth = Mathf.Max(1, available - 76f);
            float textWidth = Mathf.Min(maxTextWidth, Mathf.Max(1, caption.preferredWidth));
            var settings = caption.GetGenerationSettings(new Vector2(textWidth, 0));
            float textHeight = caption.cachedTextGeneratorForLayout.GetPreferredHeight(caption.text, settings) / caption.pixelsPerUnit;
            float descriptionWidth = Mathf.Min(available, Mathf.Max(120, textWidth + 76f));
            float descriptionHeight = Mathf.Max(52, textHeight + 24f);
            float gaugeWidth = Mathf.Min(268, available);
            float shape = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.4f, 1f, morph));
            float width = Mathf.Lerp(descriptionWidth, gaugeWidth, shape);
            float height = Mathf.Lerp(descriptionHeight, 42, shape);
            panel.sizeDelta = new Vector2(width, height);
            glass.SetFloat("_PanelAspect", width / height);

            var captionRect = (RectTransform)caption.transform;
            captionRect.anchoredPosition = new Vector2(56, -(height - textHeight) * .5f + captionLift);
            captionRect.sizeDelta = new Vector2(textWidth, textHeight);
            float dotY = -height * .5f + captionLift;
            ((RectTransform)safeIndicator.transform).anchoredPosition = new Vector2(32, dotY);
            ((RectTransform)safeHalo.transform).anchoredPosition = new Vector2(32, dotY);
            ((RectTransform)safeRing.transform).anchoredPosition = new Vector2(32, dotY);
            float gaugeY = -height * .5f + (1f - gaugeAlpha) * -10f;
            float sunSize = 26f;
            ((RectTransform)sun.transform).anchoredPosition = new Vector2(16, gaugeY + sunSize * .5f);
            ((RectTransform)sun.transform).sizeDelta = new Vector2(sunSize, sunSize);
            float barLeft = redGauge ? 50 : 14;
            ((RectTransform)bar.transform).anchoredPosition = new Vector2(barLeft, gaugeY + 7);
            ((RectTransform)bar.transform).sizeDelta = new Vector2(Mathf.Max(1, width - barLeft - 14), 14);
            backdrop.enabled = !string.IsNullOrEmpty(caption.text) || morph > .001f;
            caption.color = new Color(1, 1, 1, captionAlpha);
            bar.color = new Color(1, 1, 1, gaugeAlpha);
            sun.color = new Color(1, .12f, .10f, redGauge ? gaugeAlpha : 0);
            float flash = celebrating ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((completionUntil - Time.unscaledTime) / .65f)) : 0;
            glass.SetFloat("_SuccessGlow", flash);
            float breath = .5f - .5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / 2.4f));
            float indicatorAlpha = !string.IsNullOrEmpty(caption.text) ? captionAlpha : 0f;
            float dotAlpha = Mathf.Lerp(.55f, 1f, breath);
            float dotScale = Mathf.Lerp(.92f, 1.12f, breath);
            Color mint = new Color(77f / 255f, 238f / 255f, 234f / 255f);
            safeIndicator.color = new Color(mint.r, mint.g, mint.b, indicatorAlpha * dotAlpha);
            safeHalo.color = new Color(mint.r, mint.g, mint.b, indicatorAlpha * dotAlpha * .85f);
            safeRing.color = new Color(mint.r, mint.g, mint.b, indicatorAlpha * dotAlpha * .45f * Mathf.Lerp(.15f, .65f, breath));
            safeIndicator.transform.localScale = safeHalo.transform.localScale = Vector3.one * dotScale;
            safeRing.transform.localScale = Vector3.one * (dotScale * Mathf.Lerp(.85f, 1.25f, breath));
            gauge.SetFloat("_Fill", Mathf.Clamp01(redGauge ? game.exposure : progress));
            gauge.SetFloat("_PuzzleMode", redGauge ? 0 : 1);
        }

        void OnDestroy()
        {
            ready = false;
            if (canvas != null) Destroy(canvas.gameObject);
            if (glass != null) Destroy(glass);
            if (gauge != null) Destroy(gauge);
            if (sunSprite != null) Destroy(sunSprite);
            if (sunTexture != null) Destroy(sunTexture);
            if (safeSprite != null) Destroy(safeSprite);
            if (safeTexture != null) Destroy(safeTexture);
            if (haloSprite != null) Destroy(haloSprite);
            if (ringSprite != null) Destroy(ringSprite);
            if (haloTexture != null) Destroy(haloTexture);
            if (ringTexture != null) Destroy(ringTexture);
        }
    }
}
