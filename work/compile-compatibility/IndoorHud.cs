using UnityEngine;

namespace ShadeLink
{
    public sealed class IndoorHud : MonoBehaviour
    {
        IndoorGame game; GUIStyle label, button; Texture2D map;
        float width, height;
        readonly Color panel = new Color(.025f, .045f, .063f, .95f), ink = new Color(.92f, .94f, .91f), muted = new Color(.58f, .70f, .72f);
        public void Initialize(IndoorGame owner) { game = owner; map = new Texture2D(92, 360, TextureFormat.RGB24, false); map.filterMode = FilterMode.Point; }
        void OnDestroy() { if (map != null) Destroy(map); }
        public void RefreshMap()
        {
            if (map == null) return; var pixels = new Color32[map.width * map.height];
            for (int y = 0; y < map.height; y++) for (int x = 0; x < map.width; x++)
            {
                Vector2 p = new Vector2(-4.6f + (x + .5f) * .1f, (y + .5f) * .1f);
                Color c = IndoorRules.Safe(p, game.shadows) ? new Color(.08f, .27f, .29f) : new Color(.87f, .64f, .29f);
                foreach (Block b in game.blocks) if (p.x > b.Bounds.min.x && p.x < b.Bounds.max.x && p.y > b.Bounds.min.z && p.y < b.Bounds.max.z) c = new Color(.035f, .06f, .08f);
                pixels[y * map.width + x] = c;
            }
            map.SetPixels32(pixels); map.Apply(false);
        }
        void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
        void Text(float x, float y, float w, float h, string value, int size = 20, Color? color = null, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            label.fontSize = size; label.normal.textColor = color ?? ink; label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; label.alignment = align;
            GUI.Label(new Rect(x, y, w, h), value, label);
        }
        bool Button(float x, float y, float w, string value, bool primary = false)
        {
            var rect = new Rect(x, y, w, 56); bool hover = rect.Contains(Event.current.mousePosition);
            Fill(rect, primary ? (hover ? new Color(.55f, .94f, .85f) : IndoorVisuals.SafeColor) : (hover ? new Color(.17f, .27f, .30f) : new Color(.1f, .17f, .20f)));
            button.normal.textColor = primary ? panel : ink; return GUI.Button(rect, value, button);
        }
        void OnGUI()
        {
            if (game == null) return;
            if (label == null) { label = new GUIStyle { font = game.font, wordWrap = true }; button = new GUIStyle { font = game.font, fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; }
            float scale = Mathf.Max(.25f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f)); width = Screen.width / scale; height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            if (game.started) Hud();
            if (!game.active) Menu();
            GUI.matrix = Matrix4x4.identity;
        }
        void Hud()
        {
            Fill(new Rect(24, 24, 485, 157), panel); Fill(new Rect(24, 24, 3, 157), IndoorVisuals.SafeColor);
            Text(46, 39, 440, 25, "PARALLAX   /   LIGHT CORRIDOR", 17, muted);
            Text(46, 76, 440, 35, game.checkpoint == 0 ? "01   첫 번째 그늘길 만들기" : game.checkpoint == 1 ? "02   두 번째 통로 건너기" : "03   정면 출구로 이동", 25, ink, true);
            Text(46, 119, 440, 46, game.checkpoint == 0 ? "사물을 클릭하고 먼 곳을 보세요.\n크기를 키워 쉼터까지 그림자를 연결합니다." : game.checkpoint == 1 ? "쉼터 앞쪽의 02 표시로 이동한 뒤 잡아보세요.\n가까이에서 잡으면 더 크게 만들 수 있습니다." : "두 통로를 통과했습니다.\n바닥 높이의 출구로 걸어가세요.", 18, muted);
            Fill(new Rect(24, 193, 485, 47), panel);
            Text(45, 205, 450, 28, (game.firstLinked ? "● 첫 번째 연결" : "○ 첫 번째 연결") + "     " + (game.secondLinked ? "● 두 번째 연결" : "○ 두 번째 연결"), 18, IndoorVisuals.SafeColor);
            float rx = width - 316; Fill(new Rect(rx, 24, 292, 122), panel);
            Color status = game.safe ? IndoorVisuals.SafeColor : IndoorVisuals.LightColor;
            Text(rx + 18, 40, 205, 30, game.safe ? "그늘 · 안전" : "조명 노출", 21, status, true);
            Text(rx + 211, 41, 62, 26, Mathf.CeilToInt(game.exposure * 100) + "%", 20, ink, false, TextAnchor.UpperRight);
            Fill(new Rect(rx + 18, 80, 255, 8), new Color(.15f, .23f, .26f)); Fill(new Rect(rx + 18, 80, 255 * game.exposure, 8), status);
            Text(rx + 18, 103, 255, 26, "저장 구역 " + (game.checkpoint + 1) + " / 3", 16, muted);
            float cx = width / 2, cy = height / 2;
            Color cross = game.aimed >= 0 || game.held != null ? IndoorVisuals.SafeColor : ink;
            Fill(new Rect(cx - 5, cy - 1, 10, 2), cross); Fill(new Rect(cx - 1, cy - 5, 2, 10), cross);
            if (game.held != null)
            {
                Fill(new Rect(cx - 275, cy + 34, 550, 102), panel);
                Text(cx - 257, cy + 46, 514, 29, Rules.Names[game.held.kind] + "  " + game.blocks[game.held.index].scale.ToString("0.00") + "배  ·  원근 조절 중", 22, cross, true, TextAnchor.UpperCenter);
                Text(cx - 257, cy + 82, 514, 46, game.invalid ? "벽·사물·작업 구역과 겹칩니다. 시선을 조금 되돌려주세요." : "먼 곳은 크게 · 가까운 곳은 작게\n클릭으로 놓기 · 오른쪽 클릭으로 취소 · 조절 중 위치 고정", 17, game.invalid ? IndoorVisuals.LightColor : ink, false, TextAnchor.UpperCenter);
            }
            else if (game.aimed >= 0) Text(cx - 220, cy + 28, 440, 32, "왼쪽 클릭 · " + Rules.Names[game.aimed] + " 잡기", 20, cross, true, TextAnchor.UpperCenter);
            if (game.hints)
            {
                Fill(new Rect(width - 189, 170, 165, 521), panel); Text(width - 172, 186, 131, 27, "H  배치 힌트", 17, muted);
                GUI.DrawTexture(new Rect(width - 157, 227, 101, 396), map);
                float px = width - 157 + (game.player.feet.x + 4.6f) / 9.2f * 101, py = 227 + (36 - game.player.feet.z) / 36 * 396;
                Fill(new Rect(px - 3, py - 3, 6, 6), Color.white);
                Text(width - 176, 637, 140, 46, "초록 표시 구역 쪽으로\n사물을 멀리 놓아보세요.", 15, ink, false, TextAnchor.UpperCenter);
            }
            if (!string.IsNullOrEmpty(game.notice) && Time.unscaledTime < game.noticeUntil)
            { Fill(new Rect(cx - 435, height - 157, 870, 55), panel); Text(cx - 416, height - 143, 832, 34, game.notice, 19, cross, false, TextAnchor.UpperCenter); }
            Fill(new Rect(24, height - 79, width - 48, 55), panel);
            Text(44, height - 62, width - 88, 35, "WASD 이동   ·   마우스 시점   ·   클릭 잡기 / 놓기   ·   오른쪽 클릭 취소   ·   H 배치 힌트   ·   R 처음부터   ·   Esc 메뉴", 18, ink, false, TextAnchor.UpperCenter);
        }
        void Menu()
        {
            Fill(new Rect(0, 0, width, height), new Color(.015f, .025f, .04f, game.started ? .76f : .28f));
            float x = 62, y = (height - 738) / 2;
            Fill(new Rect(x, y, 616, 738), panel); Fill(new Rect(x, y, 616, 4), IndoorVisuals.SafeColor);
            Text(x + 34, y + 31, 548, 31, "PARALLAX   /   실내 스테이지  0.7", 18, IndoorVisuals.SafeColor);
            Text(x + 32, y + 83, 548, 70, game.won ? "빛 사이를 건넜습니다" : "빛 사이를 건너다", game.won ? 39 : 46, ink, true);
            Text(x + 34, y + 178, 548, 69, game.won ? "원근으로 만든 두 그늘길을 따라\n실내 통로를 걸어서 탈출했습니다." : "원근으로 사물의 크기를 바꾸고,\n그림자로 안전한 길을 만드세요.", 25, muted);
            if (game.won)
            {
                Text(x + 34, y + 285, 548, 80, "플레이 시간  " + Mathf.FloorToInt(game.elapsed / 60) + "분 " + Mathf.FloorToInt(game.elapsed % 60) + "초\n쉼터로 돌아온 횟수  " + game.respawns + "회", 24, IndoorVisuals.SafeColor);
                Text(x + 34, y + 400, 548, 85, "다른 크기와 배치로도 길을 만들 수 있습니다.\n다시 플레이하며 그림자 변화를 살펴보세요.", 21);
            }
            else
            {
                Text(x + 34, y + 278, 548, 31, "01  사물을 잡고 먼 곳 바라보기", 22, IndoorVisuals.SafeColor, true);
                Text(x + 34, y + 319, 548, 65, "왼쪽 클릭으로 잡고 시선을 위아래로 움직이세요.\n먼 곳으로 커진 사물을 클릭해서 내려놓습니다.", 20);
                Text(x + 34, y + 405, 548, 31, "02  그늘을 따라 다음 쉼터까지", 22, IndoorVisuals.SafeColor, true);
                Text(x + 34, y + 446, 548, 68, "노란 조명 구간은 위험하고 진한 그늘은 안전합니다.\n중간 쉼터에 도착하면 진행이 저장됩니다.", 20);
                Text(x + 34, y + 533, 548, 31, "H 배치 힌트   ·   평평한 바닥에서 두 통로를 건넙니다.", 17, muted);
            }
            if (Button(x + 34, y + 595, 548, game.won ? "처음부터 다시 플레이" : game.started ? "계속 플레이" : "실내 통로 시작하기", true)) game.Resume();
            if (Button(x + 34, y + 665, 264, "처음부터")) { game.ResetGame(); game.Resume(); }
            if (Button(x + 318, y + 665, 264, "게임 종료")) Application.Quit();
            Text(width - 520, height - 67, 480, 38, "세 개의 쉼터  /  두 개의 조명 통로", 21, ink, false, TextAnchor.UpperRight);
        }
    }
}
