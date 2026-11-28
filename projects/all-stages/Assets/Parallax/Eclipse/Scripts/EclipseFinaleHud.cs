using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Gameplay hints belong to this stage; pause/settings are owned by the shared shell.
    public sealed class EclipseFinaleHud : MonoBehaviour
    {
        public EclipseFinaleGame game;
        Font font;
        readonly Color ivory = new Color(.91f, .90f, .85f);
        readonly Color green = new Color(.55f, .92f, .70f);

        void Awake()
        {
            font = Resources.Load<Font>("HelpHudFonts/NotoSansKR-Regular") ?? Resources.Load<Font>("Preview/BodyFont");
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 32);
        }

        void Fill(Rect rect, Color color)
        { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }

        void Label(Rect rect, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var style = new GUIStyle(GUI.skin.label) { font = font,
                fontSize = Mathf.RoundToInt(size * GameShellSettingsRuntime.TextScale),
                alignment = anchor, wordWrap = true };
            style.normal.textColor = color; GUI.Label(rect, text, style);
        }
        static string Key(PlayerControls.Action action) => PlayerControls.BindingLabel(action);

        void OnGUI()
        {
            if (game == null || game.view == null) return;
            Matrix4x4 oldMatrix = GUI.matrix; Color oldColor = GUI.color; int oldDepth = GUI.depth;
            try
            {
                GUI.depth = -40;
                if (game.FinalQuitFadeActive)
                {
                    GUI.matrix = Matrix4x4.identity;
                    Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, game.QuitFadeAlpha));
                    float thanks = game.ExitPresentation != null ? game.ExitPresentation.ThanksAlpha : 0;
                    if (thanks > 0)
                    {
                        int size = Mathf.RoundToInt(42 * Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
                        Label(new Rect(0, Screen.height * .42f, Screen.width, Screen.height * .16f),
                            EclipseExitPresentation.ClosingThanksText, size, new Color(ivory.r, ivory.g, ivory.b, thanks), TextAnchor.MiddleCenter);
                    }
                    return;
                }
                float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
                GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1920 * scale) * .5f, (Screen.height - 1080 * scale) * .5f, 0),
                    Quaternion.identity, Vector3.one * scale);
                if (game.Credits != null && game.Credits.Active)
                {
                    if (game.Credits.CanControlCredits && GameShellSettingsRuntime.ShowControlHints)
                        Label(new Rect(520, 1005, 880, 45), "SPACE 누르고 있기 · 빠르게     ESC · 마지막 인사로",
                            21, new Color(.88f, .88f, .82f, .8f), TextAnchor.MiddleCenter);
                    return;
                }
                if (!game.started || game.paused)
                {
                    if (!ParallaxGameShell.OwnsPauseMenus) StandaloneMenu();
                    return;
                }
                if (GameShellSettingsRuntime.ShowControlHints && !GlassTrial.EclipseGlassHud.ReplacesHud(game))
                {
                    Label(new Rect(38, 28, 440, 48), "PARALLAX  /  ECLIPSE", 22, ivory);
                    string status = game.ExitUnlocked ? "마지막 출구" : game.InShelter ? "안전한 출발 지점" : game.Safe ? "그늘 안" : "직사광선";
                    Label(new Rect(1400, 28, 480, 48), status, 21, game.Safe ? green : new Color(1, .69f, .35f), TextAnchor.MiddleRight);
                }
                PlayerControls.DrawCursor(new Vector2(960, 540), game.aimed != FinaleItem.None,
                    game.held != FinaleItem.None, false, ivory, new Color(.95f, .95f, .9f, .65f));
                if (GameShellSettingsRuntime.ShowControlHints)
                {
                    if (!GlassTrial.EclipseGlassHud.ReplacesHud(game) && game.aimed != FinaleItem.None)
                        Label(new Rect(650, 566, 620, 48),
                            (game.aimed == FinaleItem.Moon ? "달" : "Exit") + " · " + Key(PlayerControls.Action.Grab) + " 잡기", 22, ivory, TextAnchor.MiddleCenter);
                    if (!GlassTrial.EclipseGlassHud.ReplacesHud(game) && game.held != FinaleItem.None)
                    {
                        string manipulation = game.held == FinaleItem.Moon ? game.MoonAlignmentHint : game.ExitPlacementBlocked ? "놓을 공간이 없습니다. 조준 방향을 조금 바꿔 보세요." : "가까이서 잡아 먼 곳에 놓으면 커집니다";
                        Color manipulationColor = game.held == FinaleItem.Moon && !game.MoonPlacementBlocked && game.CurrentMoonAlignment.Complete ? green : ivory;
                        Label(new Rect(380, 897, 1160, 58), manipulation, 24, manipulationColor, TextAnchor.MiddleCenter);
                        Label(new Rect(380, 951, 1160, 48),
                            Key(PlayerControls.Action.Grab) + " 놓기  ·  " + Key(PlayerControls.Action.Cancel) + " 취소  ·  " +
                            Key(PlayerControls.Action.Rotate) + " + 마우스 회전", 20, ivory, TextAnchor.MiddleCenter);
                    }
                    if (game.hint > 0) Hint();
                }
                if (!GlassTrial.EclipseGlassHud.ReplacesHud(game) && game.noticeUntil > Time.unscaledTime)
                    Label(new Rect(370, 132, 1180, 78), game.notice, 25, ivory, TextAnchor.MiddleCenter);
                if (!GlassTrial.EclipseGlassHud.ReplacesHud(game) && game.exposure > .035f)
                {
                    Fill(new Rect(1495, 960, 360, 5), new Color(.28f, .23f, .19f, .75f));
                    Fill(new Rect(1495, 960, 360 * Mathf.Clamp01(game.exposure), 5), new Color(1, .62f, .28f));
                    Label(new Rect(1490, 910, 370, 44), "햇빛 노출", 20, ivory, TextAnchor.MiddleRight);
                }
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.depth = oldDepth; }
        }

        void Hint()
        {
            string key = game.ExitUnlocked ? "eclipse.exit" : "eclipse.moon";
            GlassTrial.HelpHudController.DrawEclipseHintPanel("HINT " + game.hint + " / 3", GlassTrial.HintCatalogue.Text(key, game.hint), game.hint, 1884, 1026, 624);
        }

        void StandaloneMenu()
        {
            Fill(new Rect(0, 0, 1920, 1080), new Color(0, 0, 0, .52f));
            Label(new Rect(600, 340, 720, 90), "PARALLAX · ECLIPSE", 40, ivory, TextAnchor.MiddleCenter);
            if (GUI.Button(new Rect(750, 485, 420, 80), game.started ? "게임 재개하기" : "시작하기")) game.Play();
        }
    }
}
