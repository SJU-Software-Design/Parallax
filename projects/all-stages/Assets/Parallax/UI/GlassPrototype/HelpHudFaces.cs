using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GlassTrial
{
    // Original reference animation videos on real perspective UI faces.
    public sealed class HelpHudFaces : MonoBehaviour
    {
        const float Width = 760f, Height = 340f, HalfWidth = Width * .5f, Distance = 1300f;
        const int UiLayer = 27;
        static readonly string[] Files = { "move.mp4", "mouse.mp4", "reset.mp4" };
        readonly RectTransform[] faces = new RectTransform[3];
        readonly CanvasGroup[] groups = new CanvasGroup[3];
        readonly VideoPlayer[] players = new VideoPlayer[3];
        readonly RenderTexture[] clips = new RenderTexture[3];
        readonly RawImage[] videoImages = new RawImage[3];
        readonly bool[] requested = new bool[3];
        readonly List<Texture2D> generated = new List<Texture2D>();
        readonly List<Sprite> generatedSprites = new List<Sprite>();
        Camera uiCamera;
        RectTransform pivot;
        RawImage output;
        RenderTexture target;
        Font font;
        Sprite roundedStage;

        public static Vector3 FacePosition(int direction) => direction == 0 ? new Vector3(0, 0, -HalfWidth) : new Vector3(direction * HalfWidth, 0, 0);
        public static float FaceRotation(int direction) => -direction * 90f;
        public static float PivotRotation(int direction, float progress) => direction * 90f * Mathf.Clamp01(progress);
        public RenderTexture Texture => target;

        public void Initialize(Transform displayParent, Font bodyFont)
        {
            font = bodyFont;
            target = NewTarget(2280, 1020, 24, "Help HUD perspective");
            var cameraObject = new GameObject("Help HUD isolated camera", typeof(Camera)); cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = new Vector3(10000, 10000, 10000 - Distance);
            uiCamera = cameraObject.GetComponent<Camera>(); uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = Color.clear; uiCamera.cullingMask = 1 << UiLayer;
            uiCamera.nearClipPlane = .1f; uiCamera.farClipPlane = Distance + 1000f;
            uiCamera.fieldOfView = 2f * Mathf.Atan(Height / (2f * Distance)) * Mathf.Rad2Deg;
            uiCamera.targetTexture = target; uiCamera.allowHDR = false; uiCamera.enabled = false;
            var root = new GameObject("Help HUD 3D content", typeof(RectTransform), typeof(Canvas));
            root.layer = UiLayer; root.transform.SetParent(transform, false); root.transform.position = new Vector3(10000, 10000, 10000);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(Width, Height);
            var uiCanvas = root.GetComponent<Canvas>(); uiCanvas.renderMode = RenderMode.WorldSpace; uiCanvas.worldCamera = uiCamera;
            var faceScaler = root.AddComponent<CanvasScaler>(); faceScaler.dynamicPixelsPerUnit = 3;
            pivot = Rect("Cube pivot", root.transform, new Vector2(Width, Height), Vector2.zero);
            pivot.localPosition = new Vector3(0, 0, HalfWidth);
            roundedStage = MakeRoundedStage();
            string[] titles = { "<color=#00CDE6>WASD</color> 이동  →  <color=#00CDE6>스페이스바</color> 점프", "마우스 <color=#00CDE6>좌클릭</color> 잡기  →  <color=#00CDE6>우클릭</color> 회전", "<color=#00CDE6>R</color> 초기화  →  <color=#00CDE6>SHIFT + R</color> 전체 초기화" };
            string[] descriptions = { "공간을 이동하고 점프해 퍼즐의 단서를 탐색하세요.", "사물을 잡고 회전해 원하는 방향으로 놓아보세요.", "사물의 배치를 다시 시작할 수 있어요." };
            for (int i = 0; i < 3; i++)
            {
                faces[i] = Rect("Page " + (i + 1), pivot, new Vector2(Width, Height), Vector2.zero);
                groups[i] = faces[i].gameObject.AddComponent<CanvasGroup>(); groups[i].blocksRaycasts = false;
                MakeVideo(i, faces[i]); Caption(faces[i], titles[i], 23, new Vector2(0, -104), true);
                Caption(faces[i], descriptions[i], 16, new Vector2(0, -132), false);
                faces[i].gameObject.SetActive(false);
            }
            var screenRect = Rect("Perspective page output", displayParent, new Vector2(Width, Height), new Vector2(0, -2));
            output = screenRect.gameObject.AddComponent<RawImage>(); output.texture = target; output.raycastTarget = false; output.enabled = false;
        }
        static RenderTexture NewTarget(int width, int height, int depth, string name)
        {
            var rt = new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32) { name = name, antiAliasing = 1, filterMode = FilterMode.Bilinear };
            rt.Create(); return rt;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.layer = UiLayer; obj.transform.SetParent(parent, false);
            var r = obj.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = size; r.anchoredPosition = pos; r.localScale = Vector3.one; return r;
        }
        void Caption(Transform face, string text, int size, Vector2 pos, bool bold)
        {
            var r = Rect("Caption", face, new Vector2(610, 34), pos); var label = r.gameObject.AddComponent<Text>();
            var boldFont = bold ? Resources.Load<Font>("HelpHudFonts/NotoSansKR-Bold") : null;
            label.font = boldFont != null ? boldFont : font; label.text = text; label.fontSize = size;
            label.supportRichText = true;
            label.fontStyle = bold && boldFont == null ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = r.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .35f); shadow.effectDistance = new Vector2(.6f, -.6f);
        }
        void MakeVideo(int index, Transform face)
        {
            var stage = Rect("Original animation mask", face, new Vector2(608, 210), new Vector2(0, 32));
            var back = stage.gameObject.AddComponent<Image>(); back.sprite = roundedStage; back.type = Image.Type.Sliced;
            back.color = Color.white; back.raycastTarget = false; stage.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var picture = Rect("Original reference video", stage, stage.sizeDelta, Vector2.zero);
            videoImages[index] = picture.gameObject.AddComponent<RawImage>(); videoImages[index].raycastTarget = false;
            clips[index] = NewTarget(1824, 630, 0, "Help HUD video " + (index + 1));
            videoImages[index].texture = clips[index]; videoImages[index].enabled = false;
            // Keep decoders on the persistent owner so hidden faces can prepare.
            var player = gameObject.AddComponent<VideoPlayer>(); players[index] = player;
            player.playOnAwake = false; player.isLooping = true; player.audioOutputMode = VideoAudioOutputMode.None;
            player.source = VideoSource.Url; player.url = Path.Combine(Application.streamingAssetsPath, "HelpHudVideos", Files[index]);
            player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = clips[index];
            player.waitForFirstFrame = true; player.skipOnDrop = true; player.sendFrameReadyEvents = true;
            int captured = index;
            player.frameReady += (p, frame) => { if (this != null && videoImages[captured] != null) { videoImages[captured].enabled = true; p.sendFrameReadyEvents = false; } };
            player.errorReceived += (p, message) => Debug.LogError("Help HUD video error: " + Files[captured] + ": " + message);
        }
        Sprite MakeRoundedStage()
        {
            const int n = 96; const float radius = 18f;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var data = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + .5f - n * .5f) - (n * .5f - radius), 0);
                float dy = Mathf.Max(Mathf.Abs(y + .5f - n * .5f) - (n * .5f - radius), 0);
                float a = Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
                data[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(data); t.Apply(); generated.Add(t);
            var sprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
            generatedSprites.Add(sprite); return sprite;
        }
        public void Render(HelpHudState state)
        {
            bool visible = state.IsActive && state.Expansion > .95f;
            output.enabled = visible; uiCamera.enabled = visible;
            for (int i = 0; i < 3; i++)
            {
                bool active = visible && (i == state.Page || (state.Current == HelpHudState.Phase.Turning && i == state.NextPage));
                faces[i].gameObject.SetActive(active);
                if (!Application.isPlaying) continue;
                if (!active && requested[i])
                {
                    players[i].Stop(); requested[i] = false;
                    videoImages[i].enabled = false; players[i].sendFrameReadyEvents = true;
                }
                if (active && !requested[i]) { requested[i] = true; players[i].Prepare(); }
                if (active && players[i].isPrepared && !players[i].isPlaying) players[i].Play();
            }
            if (!visible) return;
            faces[state.Page].localPosition = FacePosition(0); faces[state.Page].localRotation = Quaternion.identity;
            groups[state.Page].alpha = state.Current == HelpHudState.Phase.Turning ? 1f - state.TurnProgress : 1;
            if (state.Current == HelpHudState.Phase.Turning)
            {
                int d = state.Direction; faces[state.NextPage].localPosition = FacePosition(d);
                faces[state.NextPage].localRotation = Quaternion.Euler(0, FaceRotation(d), 0);
                groups[state.NextPage].alpha = Mathf.Lerp(.35f, 1, state.TurnProgress);
                pivot.localRotation = Quaternion.Euler(0, PivotRotation(d, state.TurnProgress), 0);
            }
            else pivot.localRotation = Quaternion.identity;
        }
        void OnDestroy()
        {
            if (uiCamera != null) uiCamera.targetTexture = null;
            foreach (var player in players) if (player != null) { player.Stop(); player.targetTexture = null; }
            foreach (var clip in clips) if (clip != null) { clip.Release(); Release(clip); }
            if (target != null) { target.Release(); Release(target); }
            foreach (var s in generatedSprites) if (s != null) Release(s);
            foreach (var t in generated) if (t != null) Release(t);
        }
        static void Release(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
