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
            font = Resources.Load<Font>("Preview/BodyFont");
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
                if (GameShellSettingsRuntime.ShowControlHints)
                {
                    Label(new Rect(38, 28, 440, 48), "PARALLAX  /  ECLIPSE", 22, ivory);
                    string status = game.ExitUnlocked ? "마지막 출구" : game.InShelter ? "안전한 출발 지점" : game.Safe ? "그늘 안" : "직사광선";
                    Label(new Rect(1400, 28, 480, 48), status, 21, game.Safe ? green : new Color(1, .69f, .35f), TextAnchor.MiddleRight);
                }
                PlayerControls.DrawCursor(new Vector2(960, 540), game.aimed != FinaleItem.None,
                    game.held != FinaleItem.None, false, ivory, new Color(.95f, .95f, .9f, .65f));
                if (GameShellSettingsRuntime.ShowControlHints)
                {
                    if (game.aimed != FinaleItem.None)
                        Label(new Rect(650, 566, 620, 48),
                            (game.aimed == FinaleItem.Moon ? "달" : "Exit") + " · " + Key(PlayerControls.Action.Grab) + " 잡기", 22, ivory, TextAnchor.MiddleCenter);
                    if (game.held != FinaleItem.None)
                    {
                        string manipulation = game.held == FinaleItem.Moon ? game.MoonAlignmentHint : game.ExitPlacementBlocked ? "놓을 공간이 없습니다. 조준 방향을 조금 바꿔 보세요." : "가까이서 잡아 먼 곳에 놓으면 커집니다";
                        Color manipulationColor = game.held == FinaleItem.Moon && !game.MoonPlacementBlocked && game.CurrentMoonAlignment.Complete ? green : ivory;
                        Label(new Rect(380, 897, 1160, 58), manipulation, 24, manipulationColor, TextAnchor.MiddleCenter);
                        Label(new Rect(380, 951, 1160, 48),
                            Key(PlayerControls.Action.Grab) + " 놓기  ·  " + Key(PlayerControls.Action.Cancel) + " 취소  ·  " +
                            Key(PlayerControls.Action.Rotate) + " + 마우스 회전", 20, ivory, TextAnchor.MiddleCenter);
                    }
                    if (game.hint > 0) Hint();
                    Label(new Rect(36, 1022, 1848, 42),
                        Key(PlayerControls.Action.Forward) + Key(PlayerControls.Action.Backward) +
                        Key(PlayerControls.Action.Left) + Key(PlayerControls.Action.Right) + " 이동    " +
                        Key(PlayerControls.Action.Jump) + " 점프    " + Key(PlayerControls.Action.Hints) + " 힌트    ESC 일시정지",
                        19, new Color(.80f, .81f, .79f, .8f));
                }
                if (game.noticeUntil > Time.unscaledTime)
                    Label(new Rect(370, 132, 1180, 78), game.notice, 25, ivory, TextAnchor.MiddleCenter);
                if (game.exposure > .035f)
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
            string[] moon = {
                "달을 잡고 태양을 향해 들어 보세요.",
                "달에 가까이 다가가 잡으면 더 크게 만들 수 있습니다. 태양 가장자리에 빛이 조금 남아도 괜찮습니다.",
                "태양을 충분히 가렸다면 달을 놓아 보세요."
            };
            string[] exit = {
                "표지판의 소문자 i를 살펴보세요.",
                "i의 점 아래, 검은 세로획은 실제로 비어 있습니다.",
                "Exit 표지판을 크게 만들어 놓고 i의 빈 통로로 나가세요."
            };
            Fill(new Rect(36, 750, 510, 220), new Color(.018f, .022f, .026f, .84f));
            Label(new Rect(58, 768, 460, 43), "HINT " + game.hint + " / 3", 19, ivory);
            Label(new Rect(58, 816, 460, 140), (game.ExitUnlocked ? exit : moon)[Mathf.Clamp(game.hint - 1, 0, 2)], 24, ivory);
        }

        void StandaloneMenu()
        {
            Fill(new Rect(0, 0, 1920, 1080), new Color(0, 0, 0, .52f));
            Label(new Rect(600, 340, 720, 90), "PARALLAX · ECLIPSE", 40, ivory, TextAnchor.MiddleCenter);
            if (GUI.Button(new Rect(750, 485, 420, 80), game.started ? "게임 재개하기" : "시작하기")) game.Play();
        }
    }
}
