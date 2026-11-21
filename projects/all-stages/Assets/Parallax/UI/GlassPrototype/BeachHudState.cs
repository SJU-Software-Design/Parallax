using System;

namespace GlassTrial
{
    // Presentation-only decisions; no puzzle or checkpoint state is modified here.
    public static class BeachHudState
    {
        public enum Mode { Safe, Sunlight, Puzzle, Notice }
        public struct View
        {
            public Mode Mode;
            public string Text;
            public float Progress;
            public View(Mode mode, string text, float progress) { Mode = mode; Text = text; Progress = Math.Max(0, Math.Min(1, progress)); }
        }

        public static string FilterNotice(string text)
        {
            switch (text)
            {
                case "두 수신기 연결 완료 · 게이트가 열렸습니다. 그늘을 이어 모래성으로 가세요.": return "수신기가 활성화 되었어요";
                case "바다에 떨어진 바위를 원래 해안가로 돌려놓았어요.": return "바위가 떨어지지 않게 조심해요";
                case "빛 → 거울 → 수신기":
                case "거울을 내려놓으세요 · 안착한 거울만 빛을 연결합니다":
                case "거울이 안착하면 반사광이 연결됩니다":
                case "두 수신기 연결 완료 · 열린 게이트 너머로 그늘을 이으세요":
                case "판자는 통로 · 파라솔은 그늘":
                case "그림자 완성 · 보드의 배가 작아지고 있습니다":
                case "작은 배가 보드 앞 받침으로 떨어집니다":
                case "보드 앞에 떨어진 작은 배를 직접 잡아 물가로 가져가세요":
                case "손에 든 배를 출항 부두 앞 물에 크게 놓으세요":
                case "물에 놓인 배의 그늘진 갑판에 올라타세요":
                case "배의 실제 그늘 안에서 항해 중":
                case "그늘 배 도착 완료 · 열린 길을 따라 돌과 그림자의 항구로":
                case "해안의 바위를 키워 옆 추 받침에 올려 보세요":
                case "키운 바위의 밑부분을 옆 추 받침 안에 놓으세요":
                case "바위가 추 받침에 안착하는 중이에요":
                case "무게가 부족해요 · 바위를 원근으로 더 키워 놓으세요":
                case "그림자 배 완성 · 보드에서 떨어진 작은 배를 직접 잡아 출항 부두로 가져가세요.":
                case "배가 내려왔습니다 · 보드 앞 작은 배를 직접 잡아 출항 부두로 가져가세요.":
                case "그늘 배로 도착했습니다 · 열린 길을 따라 모형 항구로 가세요.":
                case "무게가 걸렸어요 · 다리와 천막이 함께 올라옵니다.":
                case "WASD로 들고 이동 · 시선으로 크기 조절 · 클릭으로 놓기": return null;
                default: return text;
            }
        }

        public static MuseumStageGuide.Notice ClassifyCommonNotice(string text)
        {
            if (string.IsNullOrEmpty(text)) return new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.None, null, 0);
            var museum = MuseumStageGuide.ClassifyNotice(text);
            if (museum.Kind != MuseumStageGuide.NoticeKind.None) return museum;
            switch (text)
            {
                case "몸에서 조금 떨어진 곳에 놓으세요.":
                    return new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.Warning, text, 2f);
                case "해변 경계 또는 현재 구역을 벗어납니다.":
                case "플레이어와 겹칩니다.":
                case "다른 사물이나 구조물과 겹칩니다.":
                case "바위가 떨어지지 않게 조심해요":
                case "고정 부두로 돌아가 돌을 조정하세요. 승선교 위에서는 돌을 잡을 수 없습니다.":
                case "접속 걸쇠가 섬과 부두를 고정하고 있어요. R로 다시 시작할 수 있어요.":
                case "이미 도착 부두에 정박했습니다. 그늘을 따라 내려 주세요.":
                case "배를 내려놓고 갑판에 오른 뒤 출항하세요.":
                case "출항 부두 앞 물에 배를 놓고, 떠서 안착할 때까지 기다려 주세요.":
                case "출항 부두 앞 물길에서 출항할 수 있습니다. 배를 부두 가까이 놓아 주세요.":
                case "배의 갑판에 먼저 올라 주세요.":
                case "갑판의 실제 그늘 안으로 이동한 뒤 출항하세요.":
                    return new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.Warning, text, 3f);
                case "현재 구역의 사물과 위치를 초기화했습니다. 이전 구역은 유지됩니다.":
                    return new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.Reset, "현재 구역의 사물과 위치를 초기화했습니다.", 3f);
                case "쉼터로 복귀했습니다 · 배는 출발 위치에 다시 정박했습니다.":
                case "중간 성벽으로 복귀 · 복구한 첫 성벽은 유지됩니다.":
                case "빛·물 위험에서 현재 쉼터로 복귀했습니다. 놓은 사물 배치는 유지됩니다.":
                    return new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.Reset, text, 3f);
            }
            const string suffix = " 구역 저장 · R 구역 재시도";
            if (text.EndsWith(suffix, StringComparison.Ordinal))
            {
                string number = text.Substring(0, text.Length - suffix.Length);
                if (number.Length >= 2 && int.TryParse(number, out int parsed) && parsed > 0)
                    return new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.Save, number + " 구역 저장", 2.5f);
            }
            return new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.None, null, 0);
        }

        // Each region contributes independently so one matched piece is visible.
        // Exterior shadow reduces the displayed fit, and the final 15% follows
        // the real charge. This never changes the actual matching conditions.
        public static float ShadowProgress(float hull, float canopy, float coverage, float clearance, float charge)
        {
            float fit = (Clamp(hull / .84f) + Clamp(canopy / .84f)) * .5f;
            float cleanliness = .65f + .35f * Clamp(clearance / .88f);
            return fit * cleanliness * .85f + Clamp(charge) * .15f;
        }
        static float Clamp(float value) => Math.Max(0, Math.Min(1, value));

        public static View Select(bool safe, float exposure, string notice, bool showPuzzle, float progress, string reminder, string boarded)
        {
            if (!safe || exposure > 0) return new View(Mode.Sunlight, null, exposure);
            if (!string.IsNullOrEmpty(notice)) return new View(Mode.Notice, notice, 0);
            if (!string.IsNullOrEmpty(reminder)) return new View(Mode.Notice, reminder, 0);
            if (!string.IsNullOrEmpty(boarded)) return new View(Mode.Notice, boarded, 0);
            if (showPuzzle) return new View(Mode.Puzzle, null, progress);
            return new View(Mode.Safe, "그늘 · 안전", 0);
        }
    }

    public sealed class BeachHoistReminder
    {
        float elapsed;
        bool wasMoving;
        public string Update(bool moving, bool visible, float dt)
        {
            if (!moving) { Reset(); return null; }
            if (!wasMoving) { wasMoving = true; elapsed = 0; }
            if (!visible) return null;
            elapsed += Math.Max(0, dt);
            if (elapsed >= 1f && elapsed < 3f) return "인양중";
            if (elapsed >= 3.5f && elapsed < 6.5f) return "다리가 올라와요";
            return null;
        }
        public void Reset() { elapsed = 0; wasMoving = false; }
    }
}