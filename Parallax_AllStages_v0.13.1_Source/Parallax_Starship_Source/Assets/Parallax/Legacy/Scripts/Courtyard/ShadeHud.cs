using UnityEngine;

namespace ShadeLink
{
    public sealed partial class ShadeHud : MonoBehaviour
    {
        ShadeGame game;
        Font font;
        GUIStyle label, button;
        Texture2D map;
        readonly Color ink = new Color(.91f, .92f, .85f), muted = new Color(.62f, .72f, .71f);
        readonly Color panel = new Color(.035f, .07f, .08f, .92f);
        float width = 1600, height = 900;
        bool showMap = true;
        public void Initialize(ShadeGame owner, Font koreanFont)
        {
            game = owner; font = koreanFont; map = new Texture2D(140, 320, TextureFormat.RGB24, false);
            map.filterMode = FilterMode.Point;
        }
        public void RefreshMap()
        {
            if (map == null || game.shadows == null) return;
            var pixels = new Color32[map.width * map.height];
            Bounds[] solids = Rules.Solids(game.state.blocks, game.puzzle?.solids, game.state.groundExit);
            var safety = game.SafetyShadows;
            for (int y = 0; y < map.height; y++) for (int x = 0; x < map.width; x++)
            {
                var point = new Vector2(-7 + (x + .5f) * .1f, 16 - (y + .5f) * .1f);
                Color color = Rules.Safe(point, safety) ? new Color(.19f, .37f, .37f) : new Color(.77f, .69f, .50f);
                foreach (Bounds box in solids)
                    if (point.x > box.min.x && point.x < box.max.x && point.y > box.min.z && point.y < box.max.z)
                        color = new Color(.08f, .12f, .13f);
                pixels[y * map.width + x] = color;
            }
            map.SetPixels32(pixels); map.Apply(false);
        }
        void Rect(Rect rect, Color color)
        { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }
        void Text(float x, float y, float w, float h, string text, int size = 20, Color? color = null, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            label.fontSize = size; label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.normal.textColor = color ?? ink; label.alignment = align;
            GUI.Label(new Rect(x, y, w, h), text, label);
        }
        bool Button(float x, float y, float w, float h, string text, bool primary = false)
        {
            var rect = new Rect(x, y, w, h);
            bool hover = rect.Contains(Event.current.mousePosition);
            Rect(rect, primary ? (hover ? new Color(.55f, .92f, .82f) : ShadeVisuals.Teal) : (hover ? new Color(.18f, .29f, .30f) : new Color(.12f, .21f, .22f)));
            button.normal.textColor = primary ? panel : ink;
            return GUI.Button(rect, text, button);
        }
        void OnGUI()
        {
            if (game == null) return;
            if (label == null)
            {
                label = new GUIStyle { font = font, wordWrap = true, richText = false };
                button = new GUIStyle { font = font, fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            width = Screen.width / scale; height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            if (game.state.started) DrawHud();
            if (!game.state.active) DrawMenu();
            GUI.matrix = Matrix4x4.identity;
        }
        void DrawHud()
        {
            if (game.keyPuzzle != null) { DrawKeyHud(); return; }
            var s = game.state;
            Rect(new Rect(28, 26, 500, 169), panel);
            Rect(new Rect(28, 26, 3, 169), ShadeVisuals.Teal);
            Text(50, 42, 430, 28, game.puzzle != null ? "PARALLAX   /   그늘 속 방탈출  0.5" : "그늘 잇기   /   원본 규칙", 18, muted);
            Text(50, 80, 452, 36, game.puzzle != null ? (s.pictureRestored ? "액자 복원 완료 · 발판을 밟고 탈출" : "그늘을 잇고, 그림자를 액자로") : s.shadeSolved ? "02   사물을 발판으로, 출구까지" : "01   두 그림자를 이어 길을 만드세요", 24, ink, true);
            Text(50, 123, 450, 55, game.puzzle != null ? (s.shadeSolved ? "도착 그늘 저장됨 · 액자 복원 후 출구로 올라가세요.\n햇빛을 피하면서 두 사물을 발판으로 재배치합니다." : "시작 → 쉼터 → 도착 구역을 그늘로 연결하세요.\n완성한 강아지 그림자를 도착 구역 액자로 운반합니다.") : s.shadeSolved ? "저장 완료 · 사물을 작게 다시 배치할 수 있습니다.\n최대 점프 높이 1.1 m  /  출구 높이 2.7 m"
                : "시작 그늘 → 쉼터 → 도착 그늘의 저장 지점\n그늘을 연결한 뒤 직접 건너가세요.", 18, muted);
            float gx = width - 354;
            Rect(new Rect(gx, 26, 326, 143), panel);
            Color status = game.safe ? ShadeVisuals.Teal : ShadeVisuals.Amber;
            Text(gx + 20, 42, 220, 28, game.safe ? "그늘 안 · 회복 중" : "햇빛 노출", 20, status, true);
            Text(gx + 236, 43, 70, 26, Mathf.CeilToInt(s.exposure * 100) + "%", 20, ink, false, TextAnchor.UpperRight);
            Rect(new Rect(gx + 20, 82, 286, 7), new Color(.20f, .27f, .28f));
            Rect(new Rect(gx + 20, 82, Mathf.Max(1, s.exposure * 286), 7), status);
            Text(gx + 20, 106, 287, 32, "현재 높이 " + s.player.feet.y.ToString("0.00") + " m   ·   " + (s.player.grounded ? "착지" : "공중"), 17, muted);

            Rect(new Rect(28, 208, 500, 56), panel);
            Text(49, 223, 456, 34, (s.shadeSolved ? "● 그늘 연결 저장됨" : (game.firstLinked ? "● 시작 → 쉼터" : "○ 시작 → 쉼터")
                + "     " + (game.secondLinked ? "● 쉼터 → 도착" : "○ 쉼터 → 도착")), 19, game.connected || s.shadeSolved ? ShadeVisuals.Teal : muted);
            if (game.puzzle != null)
            {
                var p = game.puzzle; var picture = p.state;
                Rect(new Rect(28, 278, 500, 105), panel);
                string stage = p.Restored ? "● 액자 복원 · 출구 잠금 해제" : p.Carrying ? "● 그림자 운반 중 · 햇빛 주의" : picture.stage == ShadowStage.Ready ? "● 강아지 그림자 완성 · 클릭해서 집기" : "강아지 그림자 일치도  " + Mathf.RoundToInt(picture.match.iou * 100) + "%";
                Text(49, 293, 456, 32, stage, 21, ShadeVisuals.Teal, true);
                Text(49, 337, 456, 35, p.hints ? "H 힌트 켜짐 · 시작 그늘의 도형별 윤곽 확인" : "시작 그늘에서 7개 도형 조합 · H 힌트", 18, muted);
            }

            float cx = width / 2, cy = height / 2;
            Color cross = game.aimed >= 0 || s.held != null ? ShadeVisuals.Teal : new Color(1, 1, .91f, .8f);
            Rect(new Rect(cx - 6, cy - 1, 12, 2), cross); Rect(new Rect(cx - 1, cy - 6, 2, 12), cross);
            if (s.held != null)
            {
                Block block = s.blocks[s.held.index];
                Rect(new Rect(cx - 255, cy + 40, 510, 126), panel);
                Text(cx - 235, cy + 53, 470, 28, Rules.Names[block.kind] + "  ·  " + block.scale.ToString("0.00") + " 배  ·  높이 " + block.Bounds.max.y.ToString("0.00") + " m", 22, ShadeVisuals.Teal, true, TextAnchor.UpperCenter);
                Text(cx - 235, cy + 94, 470, 59, game.blocked ? "겹치는 위치입니다 · 마지막 가능한 위치에 놓습니다"
                    : game.limited ? "최대 배치 범위입니다 · 클릭하면 놓습니다"
                    : "먼 곳을 보면 커지고, 가까운 곳을 보면 작아집니다\n테두리 안이 실제 그늘입니다 · 왼쪽 클릭으로 놓기", 17, game.blocked ? ShadeVisuals.Amber : ink, false, TextAnchor.UpperCenter);
            }
            else if (game.aimed >= 0 && (game.puzzle == null || (!game.puzzle.Carrying && !game.puzzle.HoldingPiece)))
                Text(cx - 250, cy + 28, 500, 37, "왼쪽 클릭 · " + Rules.Names[s.blocks[game.aimed].kind] + " 잡기", 20, ink, true, TextAnchor.UpperCenter);
            if (game.puzzle != null && s.held == null)
            {
                var p = game.puzzle; string prompt = p.Prompt();
                if (prompt != null)
                {
                    Rect(new Rect(cx - 350, cy + 38, 700, p.HoldingPiece ? 104 : 53), panel);
                    Text(cx - 333, cy + 51, 666, 32, prompt, 19, ShadeVisuals.Teal, true, TextAnchor.UpperCenter);
                    if (p.HoldingPiece) Text(cx - 333, cy + 91, 666, 40, p.blocked ? "겹치는 위치입니다 · 마지막 유효 배치를 유지합니다" : p.state.fixedSize ? "크기를 고정한 채 위치를 조절하고 있습니다" : "가까이 보면 작게 · 멀리 보면 크게 · 노출 게이지에 주의하세요", 17, p.blocked ? ShadeVisuals.Amber : ink, false, TextAnchor.UpperCenter);
                }
            }
            if (showMap)
            {
                float mx = width - 218, my = 209;
                Rect(new Rect(mx - 15, my - 18, 205, 415), panel);
                Text(mx, my - 5, 175, 30, "방 전체 보기", 17, muted);
                GUI.DrawTexture(new Rect(mx, my + 34, 175, 350), map);
                Vector2 p = s.player.Point;
                float px = mx + (p.x + 7) / 14 * 175, py = my + 34 + (p.y + 16) / 32 * 350;
                Rect(new Rect(px - 4, py - 4, 8, 8), Color.white);
                Rect(new Rect(mx + (6.475f + 7) / 14 * 175 - 3, my + 34 + (-14.33f + 16) / 32 * 350 - 3, 6, 6), ShadeVisuals.Teal);
            }
            if (!string.IsNullOrEmpty(game.notice) && Time.unscaledTime < game.noticeUntil)
            {
                Rect(new Rect(cx - 410, height - 156, 820, 53), panel);
                Text(cx - 390, height - 144, 780, 33, game.notice, 19, ShadeVisuals.Teal, false, TextAnchor.UpperCenter);
            }
            Rect(new Rect(28, height - 78, width - 56, 50), panel);
            Text(48, height - 64, width - 96, 35, game.puzzle != null ? "WASD 이동   ·   마우스 시점   ·   클릭 잡기 / 놓기 / 넣기   ·   Shift 도형 크기 고정   ·   H 힌트   ·   Space 점프   ·   R 초기화   ·   Esc 메뉴" : "W A S D  이동       마우스  시점       왼쪽 클릭  잡기 / 놓기       Space  점프       R  초기화       Esc  메뉴", 18, ink, false, TextAnchor.UpperCenter);
        }
        void DrawMenu()
        {
            if (game.keyPuzzle != null) { DrawKeyMenu(); return; }
            if (game.puzzle != null) { DrawIntegratedMenu(); return; }
            bool won = game.state.won, started = game.state.started;
            Rect(new Rect(0, 0, width, height), new Color(.015f, .04f, .045f, started ? .72f : .20f));
            float x = 64, y = Mathf.Max(36, (height - 776) / 2);
            Rect(new Rect(x, y, 628, 776), new Color(.032f, .067f, .077f, .97f));
            Rect(new Rect(x, y, 628, 4), ShadeVisuals.Teal);
            Text(x + 38, y + 33, 555, 30, "UNITY PROTOTYPE     /     0.3", 17, ShadeVisuals.Teal);
            Text(x + 36, y + 78, 555, 86, won ? "빛 너머에 도착했습니다" : "그늘 잇기", won ? 39 : 60, ink, true);
            Text(x + 38, y + 176, 548, 58, won ? "그늘을 잇고, 사물을 딛고, 높은 출구까지." : started ? "잠시 쉬어가세요." : "시선을 바꾸면 크기가 달라집니다.\n크기를 바꾸면 길이 생깁니다.", 24, muted);
            if (won)
            {
                int seconds = Mathf.FloorToInt(game.state.elapsed);
                Text(x + 38, y + 266, 547, 70, "플레이 시간  " + seconds / 60 + "분 " + seconds % 60 + "초\n다시 돌아온 횟수  " + game.state.respawns + "회", 24, ShadeVisuals.Teal);
                Text(x + 38, y + 367, 547, 110, "두 사물로 그림자의 길과 발판을 만들었습니다.\n다른 크기와 배치로 다시 도전해 보세요.", 21, ink);
            }
            else
            {
                Text(x + 38, y + 267, 547, 32, "01   그림자를 이어 건너기", 23, ShadeVisuals.Teal, true);
                Text(x + 38, y + 307, 547, 71, "사물을 클릭하고 먼 바닥을 바라보세요.\n커진 사물의 그림자로 두 그늘을 연결합니다.", 21, ink);
                Text(x + 38, y + 397, 547, 32, "02   사물을 딛고 올라가기", 23, ShadeVisuals.Teal, true);
                Text(x + 38, y + 437, 547, 82, "도착 그늘 오른쪽의 저장 지점까지 걸어가세요.\n그 뒤 사물을 작게 재배치해 높은 출구로 점프합니다.", 21, ink);
                Text(x + 38, y + 533, 550, 49, "햇빛에 오래 노출되면 마지막 그늘로 돌아갑니다.", 18, muted);
            }
            if (Button(x + 38, y + 610, 550, 58, won ? "처음부터 다시 플레이" : started ? "계속 플레이" : "그늘로 들어가기", true)) game.Resume();
            if (started && !won)
            {
                if (Button(x + 38, y + 684, 267, 50, "방 초기화")) { game.ResetGame(); game.Resume(); }
                if (Button(x + 321, y + 684, 267, 50, "게임 종료")) Application.Quit();
            }
            else if (Button(x + 38, y + 684, 550, 50, "게임 종료")) Application.Quit();
            Text(width - 493, height - 70, 455, 42, "두 사물 · 한 줄기 빛 · 하나의 출구", 20, ink, false, TextAnchor.UpperRight);
        }
        void DrawIntegratedMenu()
        {
            bool won = game.state.won, started = game.state.started;
            Rect(new Rect(0, 0, width, height), new Color(.015f, .04f, .045f, started ? .72f : .20f));
            float x = 54, y = Mathf.Max(30, (height - 808) / 2);
            Rect(new Rect(x, y, 670, 808), panel); Rect(new Rect(x, y, 670, 4), ShadeVisuals.Teal);
            Text(x + 35, y + 31, 600, 30, "PARALLAX    /    그늘 속 방탈출  0.5", 19, ShadeVisuals.Teal);
            Text(x + 35, y + 85, 600, 76, won ? "그늘을 따라 탈출했습니다" : "그늘을 잇고, 되찾다", 42, ink, true);
            Text(x + 35, y + 174, 600, 72, won ? "그늘 길을 건너 강아지의 그림자를 돌려주었습니다." : started ? "햇빛과 그림자, 액자의 진행이 잠시 멈췄습니다." : "햇빛을 피하고 사물의 그림자로 길을 만드세요.\n잃어버린 강아지 그림자가 출구를 여는 열쇠입니다.", 22, muted);
            if (won)
            {
                int seconds = Mathf.FloorToInt(game.state.elapsed);
                Text(x + 35, y + 291, 600, 86, "플레이 시간  " + seconds / 60 + "분 " + seconds % 60 + "초\n햇빛으로 복귀한 횟수  " + game.state.respawns + "회", 24, ShadeVisuals.Teal);
                Text(x + 35, y + 417, 600, 119, "그늘 연결, 그림자 조합과 운반, 액자 복원,\n발판을 이용한 탈출까지 모두 완료했습니다.", 23, ink);
            }
            else
            {
                Text(x + 35, y + 274, 600, 31, "01  시작 그늘에서 강아지 그림자 완성", 23, ShadeVisuals.Teal, true);
                Text(x + 35, y + 313, 600, 57, "7개 도형을 클릭해 옮기세요. Shift 크기 고정 · H 힌트\n완성된 그림자만 집을 수 있습니다.", 19, ink);
                Text(x + 35, y + 393, 600, 31, "02  기둥·차광판의 그늘을 따라 운반", 23, ShadeVisuals.Teal, true);
                Text(x + 35, y + 432, 600, 59, "큰 사물로 시작 → 쉼터 → 도착 구역의 그늘을 잇습니다.\n그림자를 들고 있어도 햇빛에 노출되면 복귀합니다.", 19, ink);
                Text(x + 35, y + 511, 600, 31, "03  액자를 복원하고 발판을 밟아 탈출", 23, ShadeVisuals.Teal, true);
                Text(x + 35, y + 549, 600, 59, "도착 구역의 액자에 그림자를 넣으면 잠금이 풀립니다.\n그림자는 내려놓고 다시 집을 수 있습니다.", 19, ink);
            }
            if (Button(x + 35, y + 639, 600, 59, won ? "처음부터 다시 플레이" : started ? "계속 플레이" : "그늘로 들어가기", true)) game.Resume();
            if (started && !won)
            {
                if (Button(x + 35, y + 718, 293, 51, "전체 초기화")) { game.ResetGame(); game.Resume(); }
                if (Button(x + 342, y + 718, 293, 51, "게임 종료")) Application.Quit();
            }
            else if (Button(x + 35, y + 718, 600, 51, "게임 종료")) Application.Quit();
        }
    }
}
