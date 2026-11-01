using UnityEngine;

namespace ShadeLink
{
    public sealed partial class ShadeHud
    {
        void DrawKeyHud()
        {
            var s = game.state; var p = game.keyPuzzle; var k = p.state;
            Rect(new Rect(28, 26, 475, 157), panel); Rect(new Rect(28, 26, 3, 157), ShadeVisuals.Teal);
            Text(49, 42, 430, 25, "PARALLAX   /   원근의 열쇠  0.6", 18, muted);
            Text(49, 78, 430, 36, s.keyUnlocked ? "문이 열렸습니다 · 걸어서 탈출" : s.shadeSolved ? "열쇠 크기를 맞춰 자물쇠 열기" : "그늘을 이어 열쇠를 운반하세요", 23, ink, true);
            Text(49, 122, 430, 48, "시작 그늘 → 쉼터 → 도착 그늘\n출구는 바닥 높이입니다.", 18, muted);
            Rect(new Rect(28, 196, 475, 51), panel);
            Text(49, 210, 434, 30, s.shadeSolved ? "● 도착 그늘 저장 완료" : (game.firstLinked ? "● 시작 → 쉼터" : "○ 시작 → 쉼터") + "    " + (game.secondLinked ? "● 쉼터 → 도착" : "○ 쉼터 → 도착"), 19, s.shadeSolved || game.connected ? ShadeVisuals.Teal : muted);

            Rect(new Rect(28, 261, 475, 184), panel);
            string status = k.stage == KeyStage.Floor ? "열쇠 · 바닥에 놓여 있음" : k.stage == KeyStage.Resizing ? "열쇠 · 원근으로 크기 조절 중" : k.stage == KeyStage.Carrying ? "열쇠 · 크기를 고정하여 운반 중" : s.keyUnlocked ? "열쇠 · 잠금 해제 완료" : "열쇠 · 잠금 해제 중";
            Color fit = k.Fits ? ShadeVisuals.Teal : ShadeVisuals.Amber;
            Text(49, 278, 433, 30, status, 21, fit, true);
            Text(49, 319, 433, 30, "자물쇠 대비 크기  " + Mathf.RoundToInt(k.Ratio * 100) + "%   /   목표 100%", 20, ink);
            const float meter = 431;
            Rect(new Rect(49, 360, meter, 9), new Color(.16f, .24f, .25f));
            Rect(new Rect(49 + meter * .46f, 357, meter * .08f, 15), new Color(.20f, .55f, .46f));
            float marker = 49 + Mathf.Clamp01(k.Ratio / 2) * meter;
            Rect(new Rect(marker - 2, 354, 4, 21), fit);
            Text(49, 391, 433, 39, k.Fits ? "맞는 크기입니다 · 운반 후 E로 꽂기" : k.Ratio < 1 ? "너무 작음 · F → 먼 바닥을 보고 키우세요" : "너무 큼 · F → 가까운 바닥을 보고 줄이세요", 18, fit);

            float gx = width - 350;
            Rect(new Rect(gx, 26, 322, 137), panel);
            Color safeColor = game.safe ? ShadeVisuals.Teal : ShadeVisuals.Amber;
            Text(gx + 20, 43, 240, 28, game.safe ? "그늘 안 · 안전" : "햇빛 노출", 20, safeColor, true);
            Text(gx + 233, 44, 70, 26, Mathf.CeilToInt(s.exposure * 100) + "%", 20, ink, false, TextAnchor.UpperRight);
            Rect(new Rect(gx + 20, 81, 282, 8), new Color(.20f, .27f, .28f));
            Rect(new Rect(gx + 20, 81, Mathf.Max(1, s.exposure * 282), 8), safeColor);
            Text(gx + 20, 106, 287, 35, "열쇠 운반 중에도 햇빛을 피하세요", 17, muted);
            if (showMap)
            {
                float mx = width - 218, my = 209;
                Rect(new Rect(mx - 15, my - 18, 205, 415), panel);
                Text(mx, my - 5, 175, 30, "그늘과 이동 경로", 17, muted);
                GUI.DrawTexture(new Rect(mx, my + 34, 175, 350), map);
                Vector2 point = s.player.Point;
                Rect(new Rect(mx + (point.x + 7) / 14 * 175 - 4, my + 34 + (point.y + 16) / 32 * 350 - 4, 8, 8), Color.white);
                if (k.stage == KeyStage.Floor || k.stage == KeyStage.Resizing)
                    Rect(new Rect(mx + (k.position.x + 7) / 14 * 175 - 3, my + 34 + (k.position.y + 16) / 32 * 350 - 3, 6, 6), ShadeVisuals.Amber);
            }
            float cx = width / 2, cy = height / 2;
            Color cross = p.AimKey() || p.AimLock() || game.aimed >= 0 ? ShadeVisuals.Teal : ink;
            Rect(new Rect(cx - 6, cy - 1, 12, 2), cross); Rect(new Rect(cx - 1, cy - 6, 2, 12), cross);
            string prompt = p.Prompt();
            if (s.held != null)
            {
                var b = s.blocks[s.held.index];
                prompt = Rules.Names[b.kind] + " " + b.scale.ToString("0.00") + "배 · 먼 바닥 크게 / 가까운 바닥 작게 · 클릭 놓기";
                if (game.blocked) prompt = "겹치는 위치입니다 · 마지막 유효 배치 유지 · 클릭 놓기";
            }
            else if (prompt == null && game.aimed >= 0) prompt = "클릭 · " + Rules.Names[s.blocks[game.aimed].kind] + " 잡기";
            if (prompt != null)
            {
                Rect(new Rect(cx - 366, cy + 96, 732, 64), panel);
                Text(cx - 350, cy + 109, 700, 43, prompt, 19, p.blocked ? ShadeVisuals.Amber : ink, true, TextAnchor.UpperCenter);
            }
            if (!string.IsNullOrEmpty(game.notice) && Time.unscaledTime < game.noticeUntil)
            {
                Rect(new Rect(cx - 465, height - 166, 930, 67), panel);
                Text(cx - 448, height - 153, 896, 47, game.notice, 19, ShadeVisuals.Teal, false, TextAnchor.UpperCenter);
            }
            Rect(new Rect(28, height - 78, width - 56, 50), panel);
            Text(44, height - 64, width - 88, 34, "WASD 이동  ·  마우스 시점  ·  클릭 집기 / 놓기  ·  F 열쇠 원근 조절 / 운반  ·  E 열쇠 꽂기  ·  R 초기화  ·  Esc 메뉴", 18, ink, false, TextAnchor.UpperCenter);
        }
        void DrawKeyMenu()
        {
            bool won = game.state.won, started = game.state.started;
            Rect(new Rect(0, 0, width, height), new Color(.015f, .04f, .045f, started ? .76f : .24f));
            float x = 54, y = Mathf.Max(28, (height - 800) / 2);
            Rect(new Rect(x, y, 687, 800), panel); Rect(new Rect(x, y, 687, 4), ShadeVisuals.Amber);
            Text(x + 35, y + 32, 617, 30, "PARALLAX   /   KEY PROTOTYPE  0.6", 19, ShadeVisuals.Amber);
            Text(x + 35, y + 86, 617, 70, won ? "딸깍, 빛 너머로" : "원근의 열쇠", 48, ink, true);
            Text(x + 35, y + 175, 617, 70, won ? "그늘을 건너고, 크기를 맞추고, 잠긴 문을 열었습니다." : started ? "게임과 열쇠의 움직임이 잠시 멈췄습니다." : "그늘을 따라 운반한 열쇠로 잠긴 문을 여세요.\n맞는 크기의 열쇠만 자물쇠에 들어갑니다.", 22, muted);
            if (won)
            {
                int seconds = Mathf.FloorToInt(game.state.elapsed);
                Text(x + 35, y + 285, 617, 85, "플레이 시간  " + seconds / 60 + "분 " + seconds % 60 + "초\n햇빛으로 복귀한 횟수  " + game.state.respawns + "회", 24, ShadeVisuals.Teal);
                Text(x + 35, y + 416, 617, 83, "원근 조절 → 크기 고정 → 운반 → 열쇠 삽입\n열쇠를 돌리면 자물쇠가 풀리고 문이 열립니다.", 22, ink);
            }
            else
            {
                Text(x + 35, y + 273, 617, 32, "01  열쇠를 들고 그늘 길을 건너기", 23, ShadeVisuals.Teal, true);
                Text(x + 35, y + 313, 617, 62, "시작 그늘의 열쇠를 클릭해 집고 WASD로 운반합니다.\n기둥·차광판을 조절할 때는 열쇠를 잠시 내려놓으세요.", 20, ink);
                Text(x + 35, y + 393, 617, 32, "02  F로 원근 조절 → F로 크기 고정", 23, ShadeVisuals.Amber, true);
                Text(x + 35, y + 433, 617, 62, "먼 바닥을 보면 커지고 가까운 바닥을 보면 작아집니다.\n자물쇠 대비 100%에 맞춘 뒤 크기를 고정해 운반하세요.", 20, ink);
                Text(x + 35, y + 513, 617, 32, "03  자물쇠를 조준하고 E · 걸어서 탈출", 23, ShadeVisuals.Teal, true);
                Text(x + 35, y + 553, 617, 54, "열쇠가 꽂히고 돌아가면 딸깍, 문이 열립니다.\n도착 구역은 자동 저장되며 출구에 올라갈 필요가 없습니다.", 20, ink);
            }
            if (Button(x + 35, y + 638, 617, 57, won ? "처음부터 다시 플레이" : started ? "계속 플레이" : "열쇠를 찾아 시작하기", true)) game.Resume();
            if (started && !won)
            {
                if (Button(x + 35, y + 716, 300, 49, "전체 초기화")) { game.ResetGame(); game.Resume(); }
                if (Button(x + 352, y + 716, 300, 49, "게임 종료")) Application.Quit();
            }
            else if (Button(x + 35, y + 716, 617, 49, "게임 종료")) Application.Quit();
        }
    }
}
