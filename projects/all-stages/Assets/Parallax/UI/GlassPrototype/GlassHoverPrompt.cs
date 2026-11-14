using System.Collections;
using ShadeLink.Gallery;
using UnityEngine;
using UnityEngine.UI;

namespace GlassTrial
{
    // Uses the actual interaction targets; never changes input or grabbing.
    public sealed class GlassHoverPrompt : MonoBehaviour
    {
        public float enterSeconds = .24f, exitSeconds = .14f, clickSeconds = .14f;
        public float verticalTravel = 12f;
        GalleryGame game;
        GlassPanelPreview preview;
        Canvas canvas;
        CanvasGroup group;
        RectTransform panel;
        RawImage backdrop;
        Image mouseIcon;
        Text label;
        Material material;
        Texture2D mouseTexture;
        Sprite mouseSprite;
        UnityEngine.Object lastTarget;
        string lastName;
        float visibleAmount, pulseUntil;
        bool ready, clickTriggered;

        static bool CanInteract(GalleryGame game)
        {
            return game != null && game.active && !game.won && !game.Transitioning && !game.conceptCapture &&
                game.held == null && !game.adjustingMirror && (game.bottleBalance == null || game.bottleBalance.Reward == null ||
                (!game.bottleBalance.Reward.Carrying && !game.bottleBalance.Reward.Busy));
        }
        static UnityEngine.Object Target(GalleryGame game)
        {
            if (!CanInteract(game)) return null;
            var reward = game.bottleBalance != null ? game.bottleBalance.Reward : null;
            if (reward != null && reward.Targeted) return reward;
            if (game.bottleBalance != null && game.bottleBalance.Targeted) return game.bottleBalance.Bottle;
            if (game.aimedMirror != null) return game.aimedMirror;
            return game.aimed;
        }
        public static bool ReplacesHud(GalleryGame game)
        {
            var prompt = game != null ? game.GetComponent<GlassHoverPrompt>() : null;
            return prompt != null && prompt.ready && Target(game) != null;
        }
        public static bool NeedsCapture(GalleryGame game)
        {
            var prompt = game != null ? game.GetComponent<GlassHoverPrompt>() : null;
            return prompt != null && prompt.ready && (prompt.visibleAmount > 0 || Target(game) != null);
        }
        static string NameFor(UnityEngine.Object target)
        {
            var prop = target as GalleryProp;
            if (prop != null)
            {
                string name = prop.title;
                if (string.IsNullOrWhiteSpace(name)) return "사물";
                var parts = name.Split('·');
                if (parts.Length > 1 && int.TryParse(parts[0].Trim(), out _)) return parts[1].Trim();
                return parts[0].Trim();
            }
            return target is GalleryMirror ? "거울 각도 조절" : "검";
        }
        IEnumerator Start()
        {
            game = GetComponent<GalleryGame>(); preview = GetComponent<GlassPanelPreview>();
            yield return null;
            var shader = Resources.Load<Shader>("GlassPanel");
            if (game == null || preview == null || shader == null || !shader.isSupported) yield break;
            while (preview != null && preview.enabled && preview.Capture == null) yield return null;
            if (preview == null || preview.Capture == null) yield break;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var root = new GameObject("Glass hover prompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false); canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 21;
            group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            panel = Rect("Hover panel", root.transform); panel.anchorMin = panel.anchorMax = new Vector2(.5f,.5f); panel.pivot = new Vector2(.5f,.5f); panel.sizeDelta = new Vector2(280, 54);
            backdrop = panel.gameObject.AddComponent<RawImage>(); backdrop.material = material; backdrop.color = new Color(1,1,1,.97f); backdrop.raycastTarget = false;
            var iconRect = Rect("Left mouse button", panel); iconRect.anchoredPosition = new Vector2(15,-9); iconRect.sizeDelta = new Vector2(27,36);
            mouseIcon = iconRect.gameObject.AddComponent<Image>(); mouseIcon.sprite = MakeMouseSprite(); mouseIcon.preserveAspect = true; mouseIcon.raycastTarget = false;
            var textRect = Rect("Object name", panel); textRect.anchoredPosition = new Vector2(54,-10); textRect.sizeDelta = new Vector2(210,34);
            label = textRect.gameObject.AddComponent<Text>(); label.font = game.font != null ? game.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20; label.fontStyle = FontStyle.Bold; label.resizeTextForBestFit = true; label.resizeTextMinSize = 13; label.resizeTextMaxSize = 20;
            label.color = Color.white; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            var shadow = textRect.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0,0,0,.7f); shadow.effectDistance = new Vector2(1,-1);
            ready = true;
        }
        static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent,false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1); return rect;
        }
        Sprite MakeMouseSprite()
        {
            const int w=48,h=64; mouseTexture = new Texture2D(w,h,TextureFormat.RGBA32,false) { name="Left click icon", filterMode=FilterMode.Bilinear };
            var pixels = new Color32[w*h];
            for (int y=0; y<h; y++) for (int x=0; x<w; x++)
            {
                float px=(x+.5f-w*.5f)/w, py=(y+.5f-h*.5f)/h;
                float qx=Mathf.Abs(px)-.075f, qy=Mathf.Abs(py)-.12f;
                float d=Mathf.Sqrt(Mathf.Max(qx,0)*Mathf.Max(qx,0)+Mathf.Max(qy,0)*Mathf.Max(qy,0))+Mathf.Min(Mathf.Max(qx,qy),0)-.34f;
                bool inside=d<0; bool edge=inside && d>-.04f;
                bool leftButton=inside && d<-.05f && py>.015f && px<-.035f;
                bool separator=inside && ((Mathf.Abs(py)<.018f) || (Mathf.Abs(px)<.022f && py>0));
                byte a = edge||separator ? (byte)255 : leftButton ? (byte)200 : (byte)0;
                pixels[y*w+x]=new Color32(255,255,255,a);
            }
            mouseTexture.SetPixels32(pixels); mouseTexture.Apply();
            mouseSprite=Sprite.Create(mouseTexture,new Rect(0,0,w,h),new Vector2(.5f,.5f)); return mouseSprite;
        }
        void Update()
        {
            if (!ready) return;
            var target = Target(game);
            if (!clickTriggered && lastTarget != null && target == null && game.active && !game.Transitioning)
            {
                var reward = game.bottleBalance != null ? game.bottleBalance.Reward : null;
                bool grabbed = game.held == lastTarget || (reward != null && reward == lastTarget && reward.Carrying) ||
                    (game.adjustingMirror && game.mirror == lastTarget);
                if (grabbed) { pulseUntil = Time.unscaledTime + Mathf.Max(0,clickSeconds); clickTriggered = true; }
            }
            if (target != null) { lastTarget = target; lastName = NameFor(target); pulseUntil = 0; clickTriggered = false; }
            bool pulsing = target == null && Time.unscaledTime < pulseUntil;
            bool show = target != null || pulsing;
            if (!game.active || game.Transitioning || game.conceptCapture) { show=false; pulsing=false; pulseUntil=0; }
            visibleAmount = Mathf.MoveTowards(visibleAmount, show ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(.01f, show ? enterSeconds : exitSeconds));
            if (!show && visibleAmount <= 0) lastTarget = null;
            canvas.enabled = visibleAmount > 0;
            if (!canvas.enabled) return;
            float eased = 1-Mathf.Pow(1-visibleAmount,3);
            group.alpha = eased;
            panel.anchoredPosition = new Vector2(0, -94f - (1-eased)*verticalTravel);
            panel.localScale = Vector3.one*(pulsing ? .95f : 1f);
            mouseIcon.color = pulsing ? Color.white : new Color(.30f, 1f, .96f, 1f);
            backdrop.texture = preview.Capture.blurred;
            label.text = lastName;
            material.SetFloat("_GlassMode",1); material.SetFloat("_PanelAspect",280f/54f); material.SetFloat("_CornerRadius", 1f);
            material.SetFloat("_Refraction",preview.refraction?1:0); material.SetFloat("_RefractionStrength",preview.refractionStrength);
            material.SetFloat("_RefractionWidth",preview.refractionWidth); material.SetFloat("_LensDistortion",preview.lensDistortion);
            material.SetFloat("_EdgeContrast",preview.edgeContrast?1:0); material.SetFloat("_EdgeContrastStrength",Mathf.Max(1.5f,preview.edgeContrastStrength));
            material.SetFloat("_SpectrumPixels",preview.spectrumPixels); material.SetFloat("_WhiteTintStrength",preview.whiteTint); material.SetFloat("_MilkyTint",preview.milkyTint);
        }
        void OnDestroy() { ready=false; if(canvas!=null)Destroy(canvas.gameObject); if(material!=null)Destroy(material); if(mouseSprite!=null)Destroy(mouseSprite); if(mouseTexture!=null)Destroy(mouseTexture); }
    }
}
