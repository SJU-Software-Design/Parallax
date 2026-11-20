using System;
using GlassTrial;
using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class BeachGlassHudTests
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception("BEACH_GLASS_HUD_FAILED: " + message); }
        public static void Run()
        {
            string[] deleted = {
                "거울을 내려놓으세요 · 안착한 거울만 빛을 연결합니다", "거울이 안착하면 반사광이 연결됩니다",
                "두 수신기 연결 완료 · 열린 게이트 너머로 그늘을 이으세요", "판자는 통로 · 파라솔은 그늘",
                "그림자 완성 · 보드의 배가 작아지고 있습니다", "작은 배가 보드 앞 받침으로 떨어집니다",
                "보드 앞에 떨어진 작은 배를 직접 잡아 물가로 가져가세요", "손에 든 배를 출항 부두 앞 물에 크게 놓으세요",
                "물에 놓인 배의 그늘진 갑판에 올라타세요", "배의 실제 그늘 안에서 항해 중",
                "그늘 배 도착 완료 · 열린 길을 따라 돌과 그림자의 항구로", "해안의 바위를 키워 옆 추 받침에 올려 보세요",
                "키운 바위의 밑부분을 옆 추 받침 안에 놓으세요", "바위가 추 받침에 안착하는 중이에요",
                "무게가 부족해요 · 바위를 원근으로 더 키워 놓으세요", "무게가 걸렸어요 · 다리와 천막이 함께 올라옵니다."
            };
            foreach (string text in deleted) Require(BeachHudState.FilterNotice(text) == null, "deleted notice " + text);
            Require(BeachHudState.FilterNotice("두 수신기 연결 완료 · 게이트가 열렸습니다. 그늘을 이어 모래성으로 가세요.") == "수신기가 활성화 되었어요", "receiver notice");
            Require(BeachHudState.FilterNotice("바다에 떨어진 바위를 원래 해안가로 돌려놓았어요.") == "바위가 떨어지지 않게 조심해요", "rock warning");
            Require(BeachHudState.FilterNotice("알 수 없는 기존 경고") == "알 수 없는 기존 경고", "unlisted notices preserved");
            var bodyWarning = BeachGlassHud.ClassifyInteractionWarning("몸에서 조금 떨어진 곳에 놓으세요.");
            Require(bodyWarning.Kind == MuseumStageGuide.NoticeKind.Warning && bodyWarning.Seconds == 2f, "body overlap warning stays amber");
            string[] commonWarnings = {
                "위에 놓인 사물을 먼저 옮겨 주세요.", "현재 크기로 조준한 위치에 놓을 공간이 없습니다.",
                "잡기 전 위치에 서 있습니다. 옆으로 이동한 뒤 취소하세요.", "해변 경계 또는 현재 구역을 벗어납니다.",
                "플레이어와 겹칩니다.", "다른 사물이나 구조물과 겹칩니다.",
                "배의 갑판에 먼저 올라 주세요.", "갑판의 실제 그늘 안으로 이동한 뒤 출항하세요.", "바위가 떨어지지 않게 조심해요"
            };
            foreach (string warning in commonWarnings)
            {
                var commonWarning = BeachHudState.ClassifyCommonNotice(warning);
                Require(commonWarning.Kind == MuseumStageGuide.NoticeKind.Warning && commonWarning.Seconds == 3 && commonWarning.Text == warning, "amber warning " + warning);
            }
            var standing = BeachHudState.ClassifyCommonNotice("서 있는 사물은 잡을 수 없습니다. 먼저 바닥이나 다른 발판으로 이동하세요.");
            Require(standing.Kind == MuseumStageGuide.NoticeKind.Warning && standing.Seconds == 2 && standing.Text == "서 있는 사물은 잡을 수 없습니다.", "standing warning follows museum");
            var save = BeachHudState.ClassifyCommonNotice("02 구역 저장 · R 구역 재시도");
            Require(save.Kind == MuseumStageGuide.NoticeKind.Save && save.Text == "02 구역 저장" && save.Seconds == 2.5f, "save follows museum duration");
            Require(BeachGlassHud.CheckpointNoticeSeconds(false, save.Text) == 2.5f && BeachGlassHud.CheckpointNoticeSeconds(true, "초기화") == 3f, "common durations");
            var reset = BeachHudState.ClassifyCommonNotice("현재 구역의 사물과 위치를 초기화했습니다. 이전 구역은 유지됩니다.");
            Require(reset.Kind == MuseumStageGuide.NoticeKind.Reset && reset.Text == "현재 구역의 사물과 위치를 초기화했습니다." && reset.Seconds == 3f, "reset notice follows museum");
            var recovery = BeachHudState.ClassifyCommonNotice("빛·물 위험에서 현재 쉼터로 복귀했습니다. 놓은 사물 배치는 유지됩니다.");
            Require(recovery.Kind == MuseumStageGuide.NoticeKind.Reset && recovery.Seconds == 3f, "danger recovery notice");
            Require(BeachHudState.ClassifyCommonNotice("복귀 위치에서 오른쪽으로 이동하세요").Kind == MuseumStageGuide.NoticeKind.None, "unrelated text is not classified by substring");
            Require(BeachHudState.Select(true, 0, null, false, 0, null, null).Text == "그늘 · 안전", "safe default");
            Require(BeachHudState.Select(false, .4f, "경고", true, .8f, "인양중", null).Mode == BeachHudState.Mode.Sunlight, "sunlight priority");
            Require(BeachHudState.Select(true, 0, "경고", true, .8f, null, null).Text == "경고", "warning before puzzle");
            Require(BeachHudState.Select(true, 0, null, true, .8f, null, null).Mode == BeachHudState.Mode.Puzzle, "puzzle gauge");
            Require(BeachHudState.Select(true, 0, null, true, .8f, "인양중", null).Text == "인양중", "reminder over puzzle");
            Require(Math.Abs(BeachHudState.ShadowProgress(.42f, .42f, .43f, .44f, 0) - .350625f) < .001f, "shadow fit progress");
            float onePiece = BeachHudState.ShadowProgress(.84f, 0, .43f, .88f, 0);
            Require(Math.Abs(onePiece - .425f) < .001f, "one matched piece contributes half of fit progress");
            Require(BeachHudState.ShadowProgress(0, .84f, .43f, .88f, 0) == onePiece, "either piece contributes equally");
            Require(BeachHudState.ShadowProgress(.84f, 0, .43f, 0, 0) < onePiece, "outside shadow reduces progress");
            Require(BeachHudState.ShadowProgress(.84f, .84f, .86f, .88f, 0) < 1f, "unconfirmed fit does not show completion");
            Require(Math.Abs(BeachHudState.ShadowProgress(.84f, .84f, .86f, .88f, 1) - 1f) < .001f, "shadow completion");
            var reminder = new BeachHoistReminder();
            Require(reminder.Update(true, true, .99f) == null, "initial gauge");
            Require(reminder.Update(true, true, .01f) == "인양중", "two second lifting reminder begins");
            Require(reminder.Update(true, false, 2f) == null, "interrupted reminder pauses");
            Require(reminder.Update(true, true, 2f) == null, "lifting reminder ends");
            Require(reminder.Update(true, true, .5f) == "다리가 올라와요", "three second bridge reminder begins");
            Require(reminder.Update(true, true, 3f) == null, "bridge reminder ends");
            Require(reminder.Update(false, true, 1) == null && reminder.Update(true, true, .5f) == null, "reminder resets");
            Require(BeachGlassHover.ShortName("03 · 그늘 배 · 조형물") == "그늘 배", "prop short name");
            foreach (string name in new[] { "TutorialIslandGlass", "TutorialIslandShadow", "GlassExposureGauge" })
            {
                var shader = Resources.Load<Shader>(name);
                Require(shader != null && !ShaderUtil.ShaderHasError(shader), "shader " + name);
            }
            Debug.Log("BEACH_GLASS_HUD_TESTS_PASSED");
        }
    }
}