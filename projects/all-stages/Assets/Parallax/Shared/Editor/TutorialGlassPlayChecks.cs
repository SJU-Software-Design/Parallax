using System;
using System.Reflection;
using GlassTrial;
using ShadeLink.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ShadeLink.Editor
{
    [InitializeOnLoad]
    public static class TutorialGlassPlayChecks
    {
        static double started;
        static int state, frames;
        static TutorialGame game;
        static int noticeCount;
        static bool lastReset;
        static string lastNotice;
        const string SessionKey = "Parallax.TutorialGlassPlayChecks";
        static TutorialGlassPlayChecks()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }
        }
        static object Field(string name) => typeof(TutorialGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        static T GlassField<T>(TutorialGlassNotice glass, string name) => (T)typeof(TutorialGlassNotice).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(glass);
        static T HoverField<T>(TutorialGlassHover hover, string name) => (T)typeof(TutorialGlassHover).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hover);
        static void Set(string name, object value) => typeof(TutorialGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, value);
        static void Call(string name, params object[] args) => typeof(TutorialGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Parallax/Tutorial/Scenes/Tutorial.unity");
            SessionState.SetBool(SessionKey, true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("play check timeout");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if (state == 0)
                {
                    game = UnityEngine.Object.FindFirstObjectByType<TutorialGame>();
                    if (game == null || game.Camera == null) return;
                    var glass = game.GetComponent<TutorialGlassNotice>();
                    if (glass == null) return;
                    game.CheckpointNotice += (reset, text) => { noticeCount++; lastReset = reset; lastNotice = text; };
                    Require(game.CurrentBottomNotice == "주사위를 클릭해서 잡아보세요", "stage 1 starts with pickup guidance");
                    var firstDie = (ShadeLink.Gallery.GalleryProp)Field("die");
                    var initialFeet = game.Controller.transform.position;
                    var initialCameraRotation = game.Camera.transform.rotation;
                    var initialBounds = firstDie.WorldBounds;
                    game.Controller.enabled = false;
                    game.Controller.transform.position = new Vector3(initialBounds.center.x, initialBounds.max.y + .025f, initialBounds.center.z);
                    game.Controller.enabled = true;
                    game.Camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    Physics.SyncTransforms();
                    var openingInteraction = (ShadeLink.ObjectInteraction<ShadeLink.Gallery.GalleryProp>)Field("interaction");
                    openingInteraction.Interact();
                    Require(game.Held == null && game.CurrentBottomNotice == "주사위를 클릭해서 잡아보세요", "failed pickup keeps opening guidance");
                    game.Controller.enabled = false;
                    game.Controller.transform.position = initialFeet;
                    game.Controller.enabled = true;
                    game.Camera.transform.rotation = Quaternion.LookRotation(initialBounds.center - game.Camera.transform.position);
                    Physics.SyncTransforms();
                    openingInteraction.Interact();
                    Require(game.Held == firstDie && game.CurrentBottomNotice == null, "successful pickup removes opening guidance");
                    openingInteraction.Cancel(true);
                    Require(game.CurrentBottomNotice == null, "opening guidance does not reappear after cancel");
                    game.Camera.transform.rotation = initialCameraRotation;
                    GlassField<MuseumCommonNoticeState>(glass, "commonNotice").Update(Time.unscaledTime, true);
                    GlassField<MuseumHudTextTransition>(glass, "transition").Reset();
                    game.Tell("test notice", 10);
                    state = 1; frames = 0;
                }
                else if (state == 1)
                {
                    if (++frames < 4) return;
                    var capture = game.Camera.GetComponent<GlassCameraCapture>();
                    Require(capture != null && capture.captureEnabled, "notice enables blur capture");
                    Require(game.CurrentBottomNotice == "test notice", "notice unchanged");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        var glass = game.GetComponent<TutorialGlassNotice>();
                        var tutorialMaterial = GlassField<Material>(glass, "material");
                        Require(tutorialMaterial.shader.name == "Hidden/GlassTrial/TutorialIsland" && Mathf.Abs(tutorialMaterial.GetFloat("_DarkTintStrength") - .26f) < .001f && Mathf.Abs(tutorialMaterial.GetFloat("_WhiteTintStrength") - .14f) < .001f && Mathf.Abs(tutorialMaterial.GetFloat("_EdgeContrastStrength") - .35f) < .001f, "tutorial glass material settings");
                        Require(Mathf.Abs(GlassField<RawImage>(glass, "panelShadow").color.a - .38f) < .001f && !GlassField<RawImage>(glass, "panelShadow").raycastTarget, "soft panel shadow settings");
                        Require(GlassField<Material>(glass, "shadowMaterial").shader.name == "Hidden/GlassTrial/TutorialIslandShadow", "pill shadow shader installed");
                        var previousTarget = game.Camera.targetTexture;
                        var frame = new RenderTexture(640, 360, 24);
                        try
                        {
                            game.Camera.targetTexture = frame;
                            game.Camera.Render();
                            typeof(TutorialGlassNotice).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(glass, null);
                            var glassPanel = GlassField<RawImage>(glass, "backdrop").rectTransform;
                            var softShadow = GlassField<RectTransform>(glass, "shadowRect");
                            var shadowSize = GlassField<Material>(glass, "shadowMaterial").GetVector("_PanelSize");
                            Require(Vector2.Distance(softShadow.sizeDelta, glassPanel.sizeDelta + new Vector2(26, 26)) < .01f && Vector2.Distance(softShadow.anchoredPosition, glassPanel.anchoredPosition + new Vector2(0, -3)) < .01f && Vector3.Distance(softShadow.localScale, glassPanel.localScale) < .001f, "notice shadow follows panel");
                            Require(Vector2.Distance(new Vector2(shadowSize.x, shadowSize.y), glassPanel.sizeDelta) < .01f, "shadow shape follows notice dimensions");
                        }
                        finally
                        {
                            game.Camera.targetTexture = previousTarget;
                            frame.Release();
                            UnityEngine.Object.DestroyImmediate(frame);
                        }
                        Require(glass.Ready, "glass background captured");
                        var dot = GlassField<Image>(glass, "dot");
                        var caption = GlassField<Text>(glass, "caption");
                        Require(dot != null && dot.color.a > 0 && Mathf.Abs(dot.color.r - 77f / 255f) < .01f &&
                            Mathf.Abs(dot.color.g - 238f / 255f) < .01f && Mathf.Abs(dot.color.b - 234f / 255f) < .01f, "ordinary notice has mint indicator");
                        Require(((RectTransform)caption.transform).offsetMin.x >= 56, "caption does not overlap indicator");
                        var hover = game.GetComponent<TutorialGlassHover>();
                        Require(hover != null, "tutorial hover installed");
                        var hoverMaterial = HoverField<Material>(hover, "material");
                        Require(hoverMaterial.shader.name == "Hidden/GlassTrial/TutorialIsland" && Mathf.Abs(hoverMaterial.GetFloat("_DarkTintStrength") - .26f) < .001f && Mathf.Abs(hoverMaterial.GetFloat("_WhiteTintStrength") - .14f) < .001f && Mathf.Abs(hoverMaterial.GetFloat("_EdgeContrastStrength") - .35f) < .001f, "hover matches island glass style");
                        Require(HoverField<Material>(hover, "shadowMaterial").shader.name == "Hidden/GlassTrial/TutorialIslandShadow" && Mathf.Abs(HoverField<RawImage>(hover, "panelShadow").color.a - .38f) < .001f, "hover matches island shadow style");
                        var hoverDie = (ShadeLink.Gallery.GalleryProp)Field("die");
                        game.Aimed = hoverDie;
                        typeof(TutorialGlassHover).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hover, null);
                        Require(hover.NeedsCapture && HoverField<Text>(hover, "label").text == "주사위" && HoverField<Canvas>(hover, "canvas").enabled, "aimed die shows glass hover and requests capture");
                        var hoverPanel = HoverField<RectTransform>(hover, "panel");
                        var hoverShadow = HoverField<RectTransform>(hover, "shadowRect");
                        var hoverShadowSize = HoverField<Material>(hover, "shadowMaterial").GetVector("_PanelSize");
                        Require(Vector2.Distance(hoverShadow.sizeDelta, hoverPanel.sizeDelta + new Vector2(26, 26)) < .01f && Vector2.Distance(hoverShadow.anchoredPosition, hoverPanel.anchoredPosition + new Vector2(0, -3)) < .01f && Vector3.Distance(hoverShadow.localScale, hoverPanel.localScale) < .001f && Vector2.Distance(new Vector2(hoverShadowSize.x, hoverShadowSize.y), hoverPanel.sizeDelta) < .01f, "hover shadow follows panel");
                        game.Aimed = null; game.Held = hoverDie;
                        typeof(TutorialGlassHover).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hover, null);
                        Require(HoverField<bool>(hover, "clickTriggered") && HoverField<Image>(hover, "mouseIcon").color == Color.white && HoverField<RectTransform>(hover, "panel").localScale.x < 1f, "actual held target plays click pulse");
                        Require(Vector3.Distance(hoverShadow.localScale, hoverPanel.localScale) < .001f, "hover shadow follows grab pulse");
                        Require(hover.NeedsCapture, "grab pulse keeps background capture");
                        game.Held = null;
                        var previousRotation = game.Camera.transform.rotation;
                        game.Camera.transform.rotation = Quaternion.LookRotation(hoverDie.WorldBounds.center - game.Camera.transform.position);
                        Set("step", 2);
                        Set("rotationHintCompleted", false);
                        Require(game.RotationGlassCaptureNeeded, "rotation pickup card requests blur capture");
                        var ensureRotation = typeof(TutorialGame).GetMethod("EnsureRotationGlassMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
                        Require(ensureRotation != null && (bool)ensureRotation.Invoke(game, null), "rotation material available");
                        var rotationMaterial = (Material)Field("rotationGlassMaterial");
                        Require(rotationMaterial.shader.name == "Hidden/GlassTrial/TutorialRotation" && rotationMaterial.GetFloat("_SpectrumPixels") == 4f && rotationMaterial.GetFloat("_RefractionStrength") == 14f, "rotation uses refractive spectrum shader");
                        Set("rotationHintCompleted", true);
                        Require(!game.RotationGlassCaptureNeeded, "completed pickup card releases blur capture");
                        Set("rotationHintCompleted", false);
                        game.Held = hoverDie;
                        Require(game.RotationGlassCaptureNeeded, "held rotation instruction requests blur capture");
                        Set("rotationHintCompleted", true);
                        Set("rotationConfirmationRemaining", 3f);
                        Require(game.RotationGlassCaptureNeeded, "rotation confirmation requests blur capture");
                        game.Held = null;
                        Set("rotationConfirmationRemaining", 0f);
                        Set("rotationHintCompleted", false);
                        Set("step", 0);
                        game.Camera.transform.rotation = previousRotation;
                        Call("SetPaused", true);
                        typeof(TutorialGlassHover).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hover, null);
                        Require(!hover.NeedsCapture && !HoverField<Canvas>(hover, "canvas").enabled, "pause hides hover");
                        Call("SetPaused", false);
                        var common = GlassField<MuseumCommonNoticeState>(glass, "commonNotice");
                        const string standing = "서 있는 사물은 잡을 수 없습니다. 먼저 바닥이나 다른 발판으로 이동하세요.";
                        // Exercise the actual shared pickup path while standing on the tutorial die.
                        var die = (ShadeLink.Gallery.GalleryProp)Field("die");
                        var originalFeet = game.Controller.transform.position;
                        var originalCameraRotation = game.Camera.transform.rotation;
                        var bounds = die.WorldBounds;
                        game.Controller.enabled = false;
                        game.Controller.transform.position = new Vector3(bounds.center.x, bounds.max.y + .025f, bounds.center.z);
                        game.Controller.enabled = true;
                        game.Camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                        Physics.SyncTransforms();
                        var actualInteraction = (ShadeLink.ObjectInteraction<ShadeLink.Gallery.GalleryProp>)Field("interaction");
                        actualInteraction.Interact();
                        Require(game.Held == null && common.Active && common.Current.Kind == MuseumStageGuide.NoticeKind.Warning && common.Current.Seconds == 2f && common.Current.Text == "서 있는 사물은 잡을 수 없습니다.", "standing on die triggers actual pickup warning");
                        game.Controller.enabled = false;
                        game.Controller.transform.position = originalFeet;
                        game.Controller.enabled = true;
                        game.Camera.transform.rotation = originalCameraRotation;
                        Physics.SyncTransforms();
                        game.Tell(standing);
                        Require(common.Active && common.Current.Kind == MuseumStageGuide.NoticeKind.Warning && common.Current.Seconds == 2f && common.Current.Text == "서 있는 사물은 잡을 수 없습니다.", "standing warning uses museum text and duration");
                        string[] warnings = { "위에 놓인 사물을 먼저 옮겨 주세요.", "현재 크기로 조준한 위치에 놓을 공간이 없습니다.", "잡기 전 위치에 서 있습니다. 옆으로 이동한 뒤 취소하세요." };
                        foreach (var warning in warnings)
                        {
                            game.Tell(warning);
                            Require(common.Active && common.Current.Kind == MuseumStageGuide.NoticeKind.Warning && common.Current.Seconds == 3 && common.Current.Text == warning, "interaction warning routes to common panel");
                            Require(game.CurrentBottomNotice == "test notice", "underlying puzzle notice preserved");
                        }
                        var oldNow = GlassField<float>(glass, "noticeClock");
                        typeof(TutorialGlassNotice).GetField("noticeClock", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(glass, oldNow + .1f);
                        typeof(TutorialGlassNotice).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(glass, null);
                        Require(caption.text == warnings[2] && Mathf.Abs(dot.color.r - 251f / 255f) < .01f && Mathf.Abs(dot.color.g - 191f / 255f) < .01f && Mathf.Abs(dot.color.b - 36f / 255f) < .01f, "warning uses amber indicator and unchanged text");
                        var panel = GlassField<RawImage>(glass, "backdrop").rectTransform;
                        Require(Mathf.Abs(panel.anchoredPosition.x) > .01f, "warning panel shakes");
                        common.Update(oldNow + 3.1f, false);
                        Require(!common.Active && game.CurrentBottomNotice == "test notice", "warning expires and puzzle notice remains");
                        game.Tell(standing);
                        typeof(TutorialGlassNotice).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(glass, null);
                        Require(caption.text == "서 있는 사물은 잡을 수 없습니다." && Mathf.Abs(dot.color.r - 251f / 255f) < .01f && Mathf.Abs(dot.color.g - 191f / 255f) < .01f && Mathf.Abs(dot.color.b - 36f / 255f) < .01f, "standing warning is amber");
                        const string bodyWarning = "몸에서 조금 떨어진 곳에 놓으세요.";
                        game.Tell(bodyWarning);
                        Require(common.Active && common.Current.Text == bodyWarning && common.Current.Kind == MuseumStageGuide.NoticeKind.Warning && common.Current.Seconds == 2f, "body overlap warning uses two seconds");
                        typeof(TutorialGlassNotice).GetField("noticeClock", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(glass, oldNow + .2f);
                        typeof(TutorialGlassNotice).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(glass, null);
                        Require(caption.text == bodyWarning && Mathf.Abs(dot.color.r - 251f / 255f) < .01f && Mathf.Abs(dot.color.g - 191f / 255f) < .01f && Mathf.Abs(dot.color.b - 36f / 255f) < .01f && Mathf.Abs(panel.anchoredPosition.x) > .01f, "body overlap warning is amber and shakes");
                        common.Update(common.StartedAt + 2.01f, false);
                        Require(!common.Active && game.CurrentBottomNotice == "test notice", "body warning expires and puzzle notice remains");
                        game.Tell(warnings[0]);
                        Require(common.Active && common.Current.Kind == MuseumStageGuide.NoticeKind.Warning, "warning can be retriggered");
                        Call("RestartLesson");
                        Require(common.Active && common.Current.Kind == MuseumStageGuide.NoticeKind.Reset, "reset replaces warning");
                        glass.enabled = false;
                        game.Tell(bodyWarning);
                        Require(game.CurrentBottomNotice == bodyWarning && (float)Field("messageUntil") - Time.time > 1.9f && (float)Field("messageUntil") - Time.time <= 2.01f, "disabled glass preserves body warning for two seconds");
                        game.Tell(standing);
                        Require(game.CurrentBottomNotice == standing && (float)Field("messageUntil") - Time.time > 1.9f && (float)Field("messageUntil") - Time.time <= 2.01f, "disabled glass preserves full standing warning for two seconds");
                        game.Tell(warnings[1]);
                        Require(game.CurrentBottomNotice == warnings[1], "disabled glass keeps warning fallback");
                        Require((float)Field("messageUntil") - Time.time > 2.9f && (float)Field("messageUntil") - Time.time <= 3.01f, "fallback warning lasts three seconds");
                        glass.enabled = true;
                    }
                    Call("SetPaused", true);
                    Require(!game.BottomNoticeVisible, "pause hides glass");
                    Call("SetPaused", false);
                    int beforeStageOne = noticeCount;
                    Call("AdvanceLesson", Vector3.zero, 1f, "기존 안내");
                    Require(noticeCount == beforeStageOne + 1 && !lastReset && lastNotice == "상태 저장", "first stage 1 checkpoint notification");
                    Call("AdvanceLesson", Vector3.zero, 1f, "기존 안내");
                    Require(noticeCount == beforeStageOne + 2 && !lastReset && lastNotice == "상태 저장", "second stage 1 checkpoint notification");
                    float until = (float)Field("messageUntil");
                    game.PreserveBottomNoticeTime(2f);
                    Require((float)Field("messageUntil") >= until + 1.99f, "underlying guidance preserved");
                    Call("RestartLesson");
                    Require(lastReset && lastNotice == "현재 구역을 초기화했습니다.", "stage 1 reset notification");
                    Call("StartTutorialStage", 2);
                    Require(!lastReset && lastNotice == "02 구역 저장", "stage 2 checkpoint notification");
                    Require((bool)Field("inClimbRoom"), "stage 2 begins");
                    Require(game.CurrentBottomNotice.Contains("주사위가 차례로"), "stage 2 arrival notice");
                    Call("RestartLesson");
                    Require(lastReset && lastNotice == "현재 구역을 초기화했습니다.", "stage 2 reset notification");
                    Require((bool)Field("inClimbRoom") && game.CurrentBottomNotice.Contains("주사위가 차례로"), "stage 2 reset retained");
                    int beforeStageThree = noticeCount;
                    Call("StartTutorialStage", 3);
                    Require(noticeCount == beforeStageThree + 1 && !lastReset && lastNotice == "03 구역 저장", "stage 3 entry checkpoint notification");
                    Require((bool)Field("inSunlightRoom"), "stage 3 begins");
                    Require(game.CurrentBottomNotice.StartsWith("햇빛이 닿으면"), "stage 3 entry notice");
                    Set("exitUnlocked", true);
                    Set("messageUntil", 0f);
                    Require(game.CurrentBottomNotice == null, "stage 1 completion notice cannot leak into stage 3");
                    Set("inClimbRoom", true);
                    Set("inSunlightRoom", false);
                    Require(game.CurrentBottomNotice == null, "stage 1 completion notice cannot leak into stage 2");
                    Set("inClimbRoom", false);
                    Require(game.CurrentBottomNotice != null && game.CurrentBottomNotice.StartsWith("주사위 한 변"), "stage 1 exit guidance remains");
                    Set("inSunlightRoom", true);
                    Set("exitUnlocked", false);
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        var sunGlass = game.GetComponent<TutorialGlassNotice>();
                        var common = GlassField<MuseumCommonNoticeState>(sunGlass, "commonNotice");
                        common.Update(1000000, true);
                        GlassField<System.Collections.Generic.Queue<MuseumStageGuide.Notice>>(sunGlass, "queuedNotices").Clear();
                        Set("sunlightSafe", true); Set("sunlightExposure", 0f); Set("messageUntil", 0f);
                        var updateGlass = typeof(TutorialGlassNotice).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                        GlassField<MuseumHudTextTransition>(sunGlass, "transition").Reset();
                        updateGlass.Invoke(sunGlass, null);
                        var stateText = GlassField<Text>(sunGlass, "caption");
                        Require(stateText.text == "그늘 · 안전", "safe indicator appears without other notice");
                        game.Tell("퍼즐 안내", 10);
                        float originalUntil = (float)Field("messageUntil");
                        common.Trigger(new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.Warning, "경고", 3), GlassField<float>(sunGlass, "noticeClock"), false);
                        float clockBeforeDanger = GlassField<float>(sunGlass, "noticeClock");
                        Set("sunlightSafe", false); Set("sunlightExposure", .45f);
                        updateGlass.Invoke(sunGlass, null);
                        Require(GlassField<Image>(sunGlass, "exposureBar").color.a > 0 && GlassField<Image>(sunGlass, "exposureSun").color.a > 0, "sun exposure gauge appears immediately");
                        var gaugePanel = GlassField<RawImage>(sunGlass, "backdrop").rectTransform;
                        var gaugeShadow = GlassField<RectTransform>(sunGlass, "shadowRect");
                        var gaugeShadowSize = GlassField<Material>(sunGlass, "shadowMaterial").GetVector("_PanelSize");
                        Require(Vector2.Distance(gaugeShadow.sizeDelta, gaugePanel.sizeDelta + new Vector2(26, 26)) < .01f && Vector2.Distance(gaugeShadow.anchoredPosition, gaugePanel.anchoredPosition + new Vector2(0, -3)) < .01f && Vector3.Distance(gaugeShadow.localScale, gaugePanel.localScale) < .001f, "shadow follows gauge morph");
                        Require(Vector2.Distance(new Vector2(gaugeShadowSize.x, gaugeShadowSize.y), gaugePanel.sizeDelta) < .01f, "shadow shape follows gauge dimensions");
                        Require(Mathf.Abs(GlassField<float>(sunGlass, "noticeClock") - clockBeforeDanger) < .0001f && common.Active, "warning is preserved while danger has priority");
                        Require(Mathf.Abs(GlassField<Material>(sunGlass, "exposureMaterial").GetFloat("_Fill") - .45f) < .001f, "gauge uses real exposure");
                        Require((float)Field("messageUntil") >= originalUntil, "danger does not consume puzzle notice");
                        Set("sunlightSafe", true); Set("sunlightExposure", .2f);
                        updateGlass.Invoke(sunGlass, null);
                        Require(GlassField<Image>(sunGlass, "exposureBar").color.a > 0, "gauge remains during exposure recovery");
                        Set("sunlightExposure", 0f);
                        common.Update(1000000, true);
                        updateGlass.Invoke(sunGlass, null);
                        Require(stateText.text == "퍼즐 안내", "puzzle notice resumes after recovery");
                        Set("messageUntil", 0f);
                    }
                    Set("sunlightCheckpoint", 1);
                    var sunDice = (ShadeLink.Gallery.GalleryProp[])Field("sunlightDice");
                    var savedPosition = sunDice[0].transform.position;
                    sunDice[0].transform.position = savedPosition + new Vector3(.3f, 0, 0);
                    var placedPosition = sunDice[0].transform.position;
                    int beforeReset = noticeCount;
                    Call("RestartLesson");
                    Require(noticeCount == beforeReset + 1 && lastReset && lastNotice == "마지막 쉼터로 돌아왔어요.", "stage 3 return notification");
                    Require((int)Field("sunlightCheckpoint") == 1 && game.CurrentBottomNotice == null, "stage 3 reset suppresses legacy notice");
                    Require(Vector3.Distance(sunDice[0].transform.position, placedPosition) < .001f, "stage 3 placed object stays put");
                    Set("sunlightExposure", .99f);
                    game.Controller.enabled = false;
                    var sunOrigin = (Vector3)typeof(TutorialGame).GetField("SunlightOrigin", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    game.Controller.transform.position = sunOrigin + new Vector3(20f, .03f, 3f);
                    game.Controller.enabled = true;
                    beforeReset = noticeCount;
                    Call("TickSunlight", .05f);
                    Require(noticeCount == beforeReset + 1 && lastReset && lastNotice == "마지막 쉼터로 돌아왔어요.", "sunlight exposure reset notification");
                    Require((int)Field("sunlightCheckpoint") == 1 && game.CurrentBottomNotice == null, "exposure reset uses saved shelter without legacy notice");
                    game.Controller.enabled = false;
                    game.Controller.transform.position = sunOrigin + new Vector3(10f, -3f, 9.5f);
                    game.Controller.enabled = true;
                    beforeReset = noticeCount;
                    Call("TickSunlight", .05f);
                    Require(noticeCount == beforeReset + 1 && lastReset && lastNotice == "마지막 쉼터로 돌아왔어요.", "player fall reset notification");
                    Require((int)Field("sunlightCheckpoint") == 1 && game.CurrentBottomNotice == null, "fall uses saved shelter without legacy notice");
                    Require(Vector3.Distance(sunDice[0].transform.position, placedPosition) < .001f, "automatic returns preserve placed object");
                    var glassNotice = game.GetComponent<TutorialGlassNotice>();
                    glassNotice.enabled = false;
                    beforeReset = noticeCount;
                    Call("RespawnSunlight");
                    Require(noticeCount == beforeReset + 1 && game.CurrentBottomNotice == "마지막 쉼터로 돌아왔어요.", "fallback notice when glass is disabled");
                    glassNotice.enabled = true;
                    Call("StartTutorialStage", 1);
                    Require(!(bool)Field("inClimbRoom") && !(bool)Field("inSunlightRoom"), "stage 1 restart");
                    Require(game.CurrentBottomNotice == "주사위를 클릭해서 잡아보세요", "stage 1 restart restores pickup guidance");
                    Finish();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SessionState.EraseBool(SessionKey);
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
                EditorApplication.Exit(1);
            }
        }
        static void Finish()
        {
            Debug.Log("TUTORIAL_GLASS_PLAY_CHECKS_PASSED");
            SessionState.EraseBool(SessionKey);
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }

}