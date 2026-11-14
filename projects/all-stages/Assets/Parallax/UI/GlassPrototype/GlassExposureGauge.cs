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
        Image bar, sun, sparkle, safeIndicator, safeHalo, safeRing;
        Material glass, gauge;
        Texture2D sunTexture, sparkleTexture, safeTexture, haloTexture, ringTexture;
        Sprite sunSprite, sparkleSprite, safeSprite, haloSprite, ringSprite;
        float gaugeAlpha, completionUntil;
        bool ready, fadingDanger;
        int previousContext = -1;
        bool previousComplete;

        public static bool CanDisplay(GalleryGame game)
        {
            if (game == null || !game.active || game.won || game.Transitioning || game.conceptCapture || game.exhibitionPreview) return false;
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

            var panel = Rect("Glass panel", root.transform, new Vector2(1195, -25), new Vector2(377, 104));
            backdrop = panel.gameObject.AddComponent<RawImage>();
            backdrop.material = glass;
            backdrop.color = new Color(1, 1, 1, .97f);
            backdrop.raycastTarget = false;

            var text = Rect("Exposure status", panel, new Vector2(20, -17), new Vector2(337, 70));
            caption = text.gameObject.AddComponent<Text>();
            caption.font = game.font != null ? game.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 20;
            caption.alignment = TextAnchor.MiddleLeft;
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

            sparkleSprite = MakeIcon(true, out sparkleTexture);
            var sparkleRect = Rect("Puzzle completion sparkle", panel, new Vector2(20, -32), new Vector2(40, 40));
            sparkle = sparkleRect.gameObject.AddComponent<Image>();
            sparkle.sprite = sparkleSprite;
            sparkle.raycastTarget = false;

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

            // GlassLessonNotice와 동일한 셰이더 및 프리뷰 설정 적용 (사이즈 비율은 377x104 유지)
            glass.SetFloat("_GlassMode", 1);
            glass.SetFloat("_PanelAspect", 377f / 104f);
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

        struct Presentation
        {
            public bool danger, redGauge, fadingDanger;
            public float gaugeAlpha;
        }

        static Presentation DecidePresentation(bool safe, float exposure, bool started, bool celebrating,
            bool fadingDanger, float gaugeAlpha, float deltaTime)
        {
            bool danger = !safe || exposure > 0;
            bool showGauge = danger || started || celebrating;
            if (danger) fadingDanger = true;
            else if (showGauge) fadingDanger = false;
            gaugeAlpha = danger ? 1 : Mathf.MoveTowards(gaugeAlpha, showGauge ? 1 : 0, deltaTime / .18f);
            return new Presentation { danger = danger, redGauge = danger || (fadingDanger && gaugeAlpha > 0),
                fadingDanger = fadingDanger, gaugeAlpha = gaugeAlpha };
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
                    return "";
                case "니케가 그림으로 변하고 있어요 · 잠깐 기다려 주세요.":
                    return "니케가 그림으로 변하고 있어요.";
                default:
                    return message;
            }
        }

        void LateUpdate()
        {
            if (!ready) return;
            bool visible = CanDisplay(game);
            canvas.enabled = visible;
            if (!visible) { previousContext = -1; completionUntil = 0; gaugeAlpha = 0; fadingDanger = false; return; }

            backdrop.texture = preview.Capture.blurred;
            ApplyGlassSettings();

            caption.text = game.bottleBalance != null && game.bottleBalance.InRoom ? game.bottleBalance.Status :
                game.conceptLayout && game.checkpoint >= 2 ?
                (game.checkpoint == 2 ? (game.portraitGate == null ? "눈·코·입을 얼굴처럼 놓아 보세요" : game.portraitGate.Status) :
                game.orcusExit != null && game.orcusExit.cathedralActivation != null ? game.orcusExit.cathedralActivation.Status : "두 손 사이로 해변이 보입니다") :
                game.conceptLayout && game.checkpoint == 1 && game.nikeLight != null ?
                NikePanelText(game.nikeLight.Status) : "그늘 · 안전";

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
            if (previousContext != context) { previousContext = context; previousComplete = complete; completionUntil = 0; }
            else
            {
                if (complete && !previousComplete && !danger) completionUntil = Time.unscaledTime + .8f;
                if (!complete) completionUntil = 0;
                previousComplete = complete;
            }
            bool celebrating = !danger && complete && Time.unscaledTime < completionUntil;
            // Danger must appear in the same frame as the game's safety decision.
            var presentation = DecidePresentation(game.safe, game.exposure, started, celebrating,
                fadingDanger, gaugeAlpha, Time.unscaledDeltaTime);
            gaugeAlpha = presentation.gaugeAlpha;
            fadingDanger = presentation.fadingDanger;
            bool redGauge = presentation.redGauge;
            backdrop.enabled = !string.IsNullOrEmpty(caption.text) || gaugeAlpha > 0.001f;
            caption.color = new Color(1, 1, 1, danger ? 0 : 1 - gaugeAlpha);
            bar.color = new Color(1, 1, 1, gaugeAlpha);
            sun.color = new Color(1, .12f, .10f, redGauge ? gaugeAlpha : 0);
            float flash = celebrating ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((completionUntil - Time.unscaledTime) / .8f)) : 0;
            sparkle.color = new Color(.58f, 1, .86f, flash);
            glass.SetFloat("_SuccessGlow", flash);
            bool showSafeIndicator = caption.text == "그늘 · 안전" && !danger && !celebrating;
            float breath = .5f - .5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / 2.4f));
            float indicatorAlpha = showSafeIndicator ? (1f - gaugeAlpha) : 0f;
            float dotAlpha = Mathf.Lerp(.55f, 1f, breath);
            float dotScale = Mathf.Lerp(.92f, 1.12f, breath);
            Color mint = new Color(77f / 255f, 238f / 255f, 234f / 255f);
            safeIndicator.color = new Color(mint.r, mint.g, mint.b, indicatorAlpha * dotAlpha);
            safeHalo.color = new Color(mint.r, mint.g, mint.b, indicatorAlpha * dotAlpha * .85f);
            safeRing.color = new Color(mint.r, mint.g, mint.b, indicatorAlpha * dotAlpha * .45f * Mathf.Lerp(.15f, .65f, breath));
            safeIndicator.transform.localScale = safeHalo.transform.localScale = Vector3.one * dotScale;
            safeRing.transform.localScale = Vector3.one * (dotScale * Mathf.Lerp(.85f, 1.25f, breath));
            ((RectTransform)caption.transform).anchoredPosition = showSafeIndicator ? new Vector2(56, -17) : new Vector2(20, -17);
            ((RectTransform)caption.transform).sizeDelta = new Vector2(showSafeIndicator ? 301 : 337, 70);
            gauge.SetFloat("_Fill", Mathf.Clamp01(redGauge ? game.exposure : celebrating ? 1 : progress));
            gauge.SetFloat("_PuzzleMode", redGauge ? 0 : 1);
        }

        void OnDestroy()
        {
            ready = false;
            if (canvas != null) Destroy(canvas.gameObject);
            if (glass != null) Destroy(glass);
            if (gauge != null) Destroy(gauge);
            if (sunSprite != null) Destroy(sunSprite);
            if (sparkleSprite != null) Destroy(sparkleSprite);
            if (sunTexture != null) Destroy(sunTexture);
            if (sparkleTexture != null) Destroy(sparkleTexture);
            if (safeSprite != null) Destroy(safeSprite);
            if (safeTexture != null) Destroy(safeTexture);
            if (haloSprite != null) Destroy(haloSprite);
            if (ringSprite != null) Destroy(ringSprite);
            if (haloTexture != null) Destroy(haloTexture);
            if (ringTexture != null) Destroy(ringTexture);
        }
    }
}
