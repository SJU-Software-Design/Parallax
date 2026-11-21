using System;
using GlassTrial;

namespace ShadeLink.Editor
{
    // Also runs outside Unity: this suite tests the presentation state without scene dependencies.
    public static class MuseumStageGuideTests
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("MUSEUM_STAGE_GUIDE_FAILED: " + message);
        }

        public static void Run()
        {
            int[] checkpoints = { 0, 1, 1, 2, 3 };
            string[] numbers = { "01", "02", "03", "04", "05" };
            string[] titles = { "명화 전시실", "빛과 그림자", "병 속의 추", "시점의 방", "두 손 사이의 문" };
            string[] objectives = { "", "", "", "", "" };
            var state = new MuseumStageGuideState();
            for (int i = 0; i < numbers.Length; i++)
            {
                int stage = MuseumStageGuide.StageFor(true, false, checkpoints[i], i == 2);
                var content = MuseumStageGuide.Get(stage);
                Require(stage == i && content.Number == numbers[i] && content.Title == titles[i] && content.Objective == objectives[i], "mapping or content for stage " + numbers[i]);
                state.Update(stage, true, false, false, i * 10, 3);
                Require(state.Requested, "entry " + numbers[i]);
                Require(state.Entry && MuseumStageGuide.ProtectStageAnnouncement(stage, state.Requested, state.Entry), "entry protected " + numbers[i]);
                Require(!MuseumStageGuide.ShowGauge(stage, state.Requested, state.Entry, true), "entry suppresses gauge " + numbers[i]);
                state.Update(stage, true, false, true, i * 10 + 2.99f, 3);
                Require(state.Requested && state.Entry, "entry duration despite interruption " + numbers[i]);
                state.Update(stage, true, false, false, i * 10 + 3, 3);
                Require(!state.Requested && !state.Entry && MuseumStageGuide.ShowGauge(stage, state.Requested, state.Entry, true), "expiry resumes gauge " + numbers[i]);
                state.Update(stage, true, true, false, i * 10 + 4, 3);
                Require(state.Requested, "replay " + numbers[i]);
                state.Update(stage, true, false, true, i * 10 + 4.1f, 3);
                Require(state.Requested == (stage == 3), "action priority " + numbers[i]);
                state.Update(stage, true, true, false, i * 10 + 5, 3);
                state.Interrupt();
                Require(!state.Requested, "gauge interrupts " + numbers[i]);
                state.Update(stage, true, false, false, i * 10 + 5.1f, 3);
                Require(!state.Requested, "interruption does not auto-replay " + numbers[i]);
            }
            Require(MuseumStageGuide.StageFor(false, false, 0, false) == -1, "legacy layout excluded");
            Require(MuseumStageGuide.StageFor(true, true, 0, false) == -1, "exhibition preview excluded");
            Require(MuseumStageGuide.StageFor(true, false, -1, false) == -1, "invalid checkpoint excluded");
            string[] covered =
            {
                "액자가 맞물렸어요. 열린 문으로 들어가세요.",
                "얼굴이 이어졌어요. 잠깐 바라봐 주세요.",
                "시점의 초상이 완성되었습니다. 눈·코·입이 하나의 얼굴로 보입니다.",
                "디케가 놓아준 검을 집어 방 가운데 대리석 받침대에 꽂아 보세요.",
                "방 가운데 대리석 받침대의 윗면 홈을 겨냥해 클릭하세요.",
                "검을 집었어요. 방 가운데 대리석 받침대에 사용하세요. Backspace로 회수 받침에 돌려놓을 수 있어요.",
                "병 속의 추 · 병 가까이에서 잡고 뒤로 물러나 확대 · H 힌트"
            };
            int[] coveredStages = { 1, 3, 3, 2, 2, 2, 2 };
            for (int i = 0; i < covered.Length; i++)
            {
                Require(MuseumStageGuide.SuppressCoveredBottomNotice(covered[i], coveredStages[i], true), "covered notice suppressed " + i);
                Require(!MuseumStageGuide.SuppressCoveredBottomNotice(covered[i], coveredStages[i], false), "notice preserved without glass " + i);
                Require(!MuseumStageGuide.SuppressCoveredBottomNotice(covered[i], 0, true), "notice preserved outside target room " + i);
            }
            Require(!MuseumStageGuide.SuppressCoveredBottomNotice("검을 움직일 공간이 필요해요 · 조금 물러나 주세요", 2, true), "uncovered warning remains");
            Require(!MuseumStageGuide.SuppressCoveredBottomNotice("WASD로 들고 이동 · 시선으로 크기 조절 · 클릭으로 놓기", 2, true), "uncovered control hint remains");
            string[] commonSource = {
                "서 있는 사물은 잡을 수 없습니다. 먼저 바닥이나 다른 발판으로 이동하세요.",
                "위에 놓인 사물을 먼저 옮겨 주세요.", "현재 크기로 조준한 위치에 놓을 공간이 없습니다.",
                "잡기 전 위치에 서 있습니다. 옆으로 이동한 뒤 취소하세요.",
                "빛에 노출되어 현재 구역의 쉼터로 돌아왔습니다.",
                "현재 구역의 전시물과 위치를 초기화했습니다. 이전 구역은 유지됩니다.",
                "02 구역 저장 · R로 이 구역만 다시 시작할 수 있습니다."
            };
            string[] commonText = { "서 있는 사물은 잡을 수 없습니다.", commonSource[1], commonSource[2], commonSource[3], "쉼터로 돌아왔어요", "현재 구역의 전시물과 위치를 초기화했습니다.", "02 구역 저장" };
            float[] commonSeconds = { 2, 3, 3, 3, 3, 3, 2.5f };
            var common = new MuseumCommonNoticeState();
            for (int i = 0; i < commonSource.Length; i++)
            {
                var value = MuseumStageGuide.ClassifyNotice(commonSource[i]);
                Require(value.Text == commonText[i] && value.Seconds == commonSeconds[i], "common text and duration " + i);
                Require(value.Kind == (i < 4 ? MuseumStageGuide.NoticeKind.Warning : i < 6 ? MuseumStageGuide.NoticeKind.Reset : MuseumStageGuide.NoticeKind.Save), "common category " + i);
                Require(MuseumStageGuide.HideLegacyCommonNotice(commonSource[i], true) && !MuseumStageGuide.HideLegacyCommonNotice(commonSource[i], false), "common legacy fallback " + i);
                common.Trigger(value, 10, false);
                Require(common.Active && common.Remaining(10) == 1, "timer starts full " + i);
                Require(Math.Abs(common.Remaining(10 + value.Seconds / 2) - .5f) < .001f, "timer decreases " + i);
                common.Update(10 + value.Seconds, false);
                Require(!common.Active && common.Remaining(10 + value.Seconds) == 0, "common expiry " + i);
                common.Trigger(value, 20, true);
                Require(!common.Active, "entry discards notice " + i);
                common.Trigger(value, 30, false); common.Update(30.1f, true);
                Require(!common.Active, "common interrupt " + i);
            }
            common.Trigger(MuseumStageGuide.ClassifyNotice(commonSource[0]), 0, false);
            common.Trigger(MuseumStageGuide.ClassifyNotice(commonSource[6]), 1, false);
            Require(common.Active && common.Current.Kind == MuseumStageGuide.NoticeKind.Save && common.Remaining(1) == 1, "new notice replaces and restarts timer");
            common.OnNotice(MuseumStageGuide.ClassifyNotice(commonSource[6]), 5, true, false, 1);
            Require(!common.Active && common.HasPendingSave, "save waits for entry");
            common.AdvancePending(7, true, false, 1); Require(!common.Active && common.HasPendingSave, "save remains pending while entry shows");
            common.AdvancePending(8, false, false, 1); Require(common.Active && common.Remaining(8) == 1 && !common.HasPendingSave, "save starts with full duration after entry");
            common.OnNotice(MuseumStageGuide.ClassifyNotice(commonSource[6]), 9, true, false, 1);
            common.OnNotice(MuseumStageGuide.ClassifyNotice(commonSource[0]), 10, true, false, 1);
            Require(!common.Active && !common.HasPendingSave, "new entry-time notice cancels pending save");
            var portraitSave = MuseumStageGuide.ClassifyNotice("03 구역 저장 · R로 이 구역만 다시 시작할 수 있습니다.", 3);
            common.OnNotice(portraitSave, 10, true, false, 3);
            common.OnNotice(MuseumStageGuide.ClassifyNotice("눈·코·입이 얼굴로 보이면, 작품을 내려놓고 잠깐 바라봐 주세요.", 3), 10.1f, true, false, 3);
            Require(common.HasPendingSave && !common.Active, "portrait puzzle notice does not discard pending save");
            common.OnNotice(MuseumStageGuide.ClassifyNotice("WASD로 들고 이동 · 시선으로 크기 조절 · 클릭으로 놓기", 3), 10.2f, true, false, 3);
            Require(common.HasPendingSave && !common.Active, "hidden notice does not discard pending save");
            common.AdvancePending(13, false, false, 3);
            Require(common.Active && common.Current.Text == "04 구역 저장" && common.Remaining(13) == 1, "portrait save displays after entry for full duration");
            common.OnNotice(MuseumStageGuide.ClassifyNotice(commonSource[6]), 11, true, false, 1);
            common.AdvancePending(12, true, false, 2); Require(!common.HasPendingSave, "room change cancels pending save");
            common.OnNotice(MuseumStageGuide.ClassifyNotice(commonSource[6]), 13, true, false, 1);
            common.AdvancePending(14, true, true, 1); Require(!common.HasPendingSave, "danger cancels pending save");
            Require(MuseumStageGuide.HideLegacyCommonNotice("WASD로 들고 이동 · 시선으로 크기 조절 · 클릭으로 놓기", false), "hide movement notice");
            Require(MuseumStageGuide.HideLegacyCommonNotice("청동 조각의 그림자 통로를 건넜습니다. 다음 전시실로 이동하세요.", false), "hide bronze crossing notice");
            Require(!MuseumStageGuide.HideLegacyCommonNotice("02 구역 저장 · unrelated", true), "unrelated notice unaffected");
            const string bottleReset = "병과 저울을 초기화했습니다. 니케 전시실의 진행은 유지됩니다.";
            var bottleNotice = MuseumStageGuide.ClassifyNotice(bottleReset, 2);
            Require(bottleNotice.Kind == MuseumStageGuide.NoticeKind.Reset && bottleNotice.Text == bottleReset && bottleNotice.Seconds == 3, "bottle reset uses cyan notice without losing context");
            Require(MuseumStageGuide.HideLegacyCommonNotice(bottleReset, true) && !MuseumStageGuide.HideLegacyCommonNotice(bottleReset, false), "bottle reset legacy fallback");
            Require(MuseumStageGuide.Get(MuseumStageGuide.StageFor(true, false, 1, true)).Number == "03", "bottle room display number independent of checkpoint");
            Require(MuseumStageGuide.ClassifyNotice("03 구역 저장 · R로 이 구역만 다시 시작할 수 있습니다.", 3).Text == "04 구역 저장", "portrait save uses display number");
            Require(MuseumStageGuide.ClassifyNotice("04 구역 저장 · R로 이 구역만 다시 시작할 수 있습니다.", 4).Text == "05 구역 저장", "cathedral save uses display number");
            Require(MuseumStageGuide.ClassifyNotice("02 구역 저장 · R로 이 구역만 다시 시작할 수 있습니다.", -1).Text == "02 구역 저장", "legacy save uses checkpoint number");
            const string resetNotice = "현재 구역의 전시물과 위치를 초기화했습니다. 이전 구역은 유지됩니다.";
            var portraitReset = MuseumStageGuide.ClassifyNotice(resetNotice, 3);
            Require(portraitReset.Kind == MuseumStageGuide.NoticeKind.Reset && portraitReset.Text == "현재 구역의 전시물과 위치를 초기화했습니다." && portraitReset.Seconds == 3f, "portrait reset displays reset only");
            var otherReset = MuseumStageGuide.ClassifyNotice(resetNotice, 1);
            Require(otherReset.Kind == MuseumStageGuide.NoticeKind.Reset && otherReset.Seconds == 3f, "other rooms retain reset notice");
            Require(MuseumStageGuide.HideLegacyCommonNotice(resetNotice, true), "portrait reset does not duplicate legacy text");
            var forcedReset = new MuseumCommonNoticeState();
            forcedReset.OnNotice(portraitReset, 0, false, false, 3);
            Require(forcedReset.Active && forcedReset.Current.Kind == MuseumStageGuide.NoticeKind.Reset, "04 reset starts over active puzzle gauge");
            forcedReset.OnNotice(MuseumStageGuide.ClassifyNotice("눈·코·입을 하나의 얼굴로 이어 보세요."), 1, false, false, 3);
            Require(forcedReset.Active && forcedReset.Current.Text == portraitReset.Text, "04 reset survives puzzle notice");
            forcedReset.OnNotice(MuseumStageGuide.ClassifyNotice("WASD로 들고 이동 · 시선으로 크기 조절 · 클릭으로 놓기"), 2, false, false, 3);
            Require(forcedReset.Active && forcedReset.Current.Text == portraitReset.Text, "04 reset survives hidden notice");
            forcedReset.Update(2.99f, false); Require(forcedReset.Active, "04 reset lasts 3 seconds");
            forcedReset.Update(3f, false); Require(!forcedReset.Active, "04 reset expires and gauge can resume");
            forcedReset.OnNotice(portraitReset, 4, false, true, 3);
            Require(!forcedReset.Active && forcedReset.PendingIsReset, "04 reset waits while danger is active");
            forcedReset.AdvancePending(5, false, true, 3); Require(forcedReset.PendingIsReset, "danger does not discard 04 reset");
            forcedReset.AdvancePending(6, false, false, 3);
            Require(forcedReset.Active && forcedReset.Current.Text == portraitReset.Text && forcedReset.Remaining(6) == 1, "04 reset gets full duration after danger");
            forcedReset.OnNotice(portraitReset, 7, true, false, 3);
            forcedReset.AdvancePending(8, true, false, 3); Require(forcedReset.PendingIsReset, "04 reset waits during entry");
            forcedReset.AdvancePending(10, false, false, 3); Require(forcedReset.Active && forcedReset.Remaining(10) == 1, "04 reset starts after entry");
            forcedReset.OnNotice(portraitReset, 11, true, false, 3);
            forcedReset.AdvancePending(12, false, false, 4); Require(!forcedReset.HasPendingSave, "04 pending reset is discarded on room change");
            common.Trigger(portraitReset, 20, false);
            common.Update(22.99f, false); Require(common.Active && common.Current.Kind == MuseumStageGuide.NoticeKind.Reset, "portrait reset remains for three seconds");
            common.Update(23f, false); Require(!common.Active, "portrait reset expires without save");
            common.OnNotice(portraitReset, 26, true, false, 3);
            Require(!common.Active && common.HasPendingSave, "portrait reset waits for stage announcement");
            common.AdvancePending(29, false, false, 3);
            Require(common.Active && common.Current.Kind == MuseumStageGuide.NoticeKind.Reset, "portrait reset shown after stage announcement");
            common.Update(29.1f, true); common.Update(33, false);
            Require(!common.Active, "interruption clears reset");
            for (int stage = 0; stage < 5; stage++)
            {
                var zoneReset = MuseumStageGuide.ClassifyNotice(stage == 2 ? bottleReset : resetNotice, stage);
                Require(zoneReset.Kind == MuseumStageGuide.NoticeKind.Reset, "reset category in stage " + stage);
                common.OnNotice(MuseumStageGuide.ClassifyNotice("02 구역 저장 · R로 이 구역만 다시 시작할 수 있습니다.", stage), 40, true, false, stage);
                common.OnNotice(zoneReset, 41, true, false, stage);
                Require(common.PendingIsReset && !common.Active, "reset replaces pending save in stage " + stage);
                common.AdvancePending(43, false, false, stage);
                Require(common.Active && common.Current.Kind == MuseumStageGuide.NoticeKind.Reset, "reset shows after entry in stage " + stage);
                common.Update(46, false);
                Require(!common.Active && !common.HasPendingSave, "no save follows reset in stage " + stage);
            }
            var portrait = new MuseumStageGuideState();
            portrait.Update(3, true, false, false, 0, 3);
            Require(MuseumStageGuide.ProtectStageAnnouncement(3, portrait.Requested, portrait.Entry), "portrait entry protects announcement from gauge");
            Require(!MuseumStageGuide.ShowGauge(3, portrait.Requested, portrait.Entry, true), "active gauge hidden during portrait announcement");
            portrait.Update(3, true, false, true, 2.99f, 3);
            Require(portrait.Requested, "portrait entry remains visible despite interrupts");
            portrait.Update(3, true, false, true, 3f, 3);
            Require(!portrait.Requested && !MuseumStageGuide.ProtectStageAnnouncement(3, portrait.Requested, portrait.Entry), "portrait gauge resumes after 3 seconds");
            Require(MuseumStageGuide.ShowGauge(3, portrait.Requested, portrait.Entry, true), "active gauge shown after portrait announcement");
            portrait.Update(3, true, true, true, 4, 3);
            Require(portrait.Requested && !portrait.Entry && MuseumStageGuide.ProtectStageAnnouncement(3, portrait.Requested, portrait.Entry), "J replay overrides active gauge");
            Require(!MuseumStageGuide.ShowGauge(3, portrait.Requested, portrait.Entry, true), "J hides active gauge again");
            portrait.Update(3, true, false, true, 6.99f, 3);
            Require(portrait.Requested, "J replay remains protected for 3 seconds");
            portrait.Update(3, true, false, true, 7, 3);
            Require(!portrait.Requested, "J replay expires");
            var repeatedEntry = new MuseumStageGuideState();
            repeatedEntry.Update(0, true, false, false, 0, 3);
            repeatedEntry.Update(0, true, true, false, 1, 3);
            repeatedEntry.Update(0, true, false, true, 3.5f, 3);
            Require(repeatedEntry.Requested && repeatedEntry.Entry, "J during entry preserves entry priority");
            repeatedEntry.Update(1, true, false, true, 3.6f, 3);
            Require(repeatedEntry.Requested && repeatedEntry.Entry, "new room restarts protected entry");
            Require(MuseumStageGuide.BottleObjective(true, "병을 옮겨 키우고 좁은 입구를 넓혀 보세요") == "", "narrow bottle opening objective stays hidden");
            string[] bottleStatuses =
            {
                "입구가 넓어졌어요 · 병을 들어 아래로 기울여 보세요",
                "입구를 향해 움직이고 있어요 · 조금 더 기울여 보세요",
                "조각이 나올 수 있도록 병 입구 아래 공간을 비워 주세요",
                "꺼낸 법전을 오른쪽 저울에 올려 보세요",
                "수평이에요 · 조각을 내려놓고 잠깐 기다리세요",
                "검으로 봉인을 풀었어요 · 열린 문으로 이동하세요"
            };
            foreach (string status in bottleStatuses)
                Require(MuseumStageGuide.BottleObjective(false, status) == MuseumStageGuide.DisplayCaption(2, status), "bottle objective follows status: " + status);
            Require(MuseumStageGuide.BottleObjective(true, bottleStatuses[0]) == "", "bottle reset keeps opening objective hidden");
            Require(MuseumStageGuide.CathedralInitialCaption("조각을 조금 더 키워 보세요") == null, "cathedral opening hint stays off the caption");
            Require(MuseumStageGuide.CathedralInitialCaption("충분히 커졌어요 · 이제 내려놓으세요") == null, "cathedral sizing complete clears initial caption");
            Require(MuseumStageGuide.CathedralInitialCaption("주변이 넓은 바닥으로 옮겨 주세요") == null, "cathedral obstruction clears initial caption");
            Require(MuseumStageGuide.CathedralInitialCaption("계단을 올라 두 손 사이로 들어가세요") == null, "cathedral opened clears initial caption");
            Require(MuseumStageGuide.DisplayCaption(3, "\uC644\uC131\uB41C \uCD08\uC0C1\uC774 \uCEE4\uD2BC\uC5D0 \uC2A4\uBA70\uB4E4\uACE0 \uC788\uC5B4\uC694.") == "\uCD08\uC0C1\uC774 \uCEE4\uD2BC\uC5D0 \uC2A4\uBA70\uB4E4\uC5B4\uC694", "portrait caption is concise");
            Require(MuseumStageGuide.DisplayCaption(4, "\uC870\uAC01\uC744 \uC870\uAE08 \uB354 \uD0A4\uC6CC \uBCF4\uC138\uC694") == "", "portrait growth hint stays off the opening caption");
            float[] cueStarts = { 0f, 4.5f, 9f };
            int[] cueStages = { 1, 3, 4 };
            for (int i = 0; i < cueStarts.Length; i++)
            {
                float start = cueStarts[i];
                Require(MuseumStageGuide.PortraitPerformanceCue(start, cueStages[i]) == i, "portrait cue starts " + i);
                Require(MuseumStageGuide.PortraitPerformanceCue(start + 1.99f, cueStages[i]) == i, "portrait cue lasts 2 seconds " + i);
                Require(MuseumStageGuide.PortraitPerformanceCue(start + 2f, cueStages[i]) == -1, "portrait gauge interval " + i);
                Require(MuseumStageGuide.PortraitPerformanceCue(start, 0) == -1, "cue never shows for wrong stage " + i);
            }
            Require(MuseumStageGuide.PortraitPerformanceCue(2.5f, 2) == -1, "removed red-stage message stays hidden");
            Require(MuseumStageGuide.PortraitPerformanceCue(4.49f, 2) == -1, "first gauge interval lasts until smile");
            Require(MuseumStageGuide.PortraitPerformanceCue(7.5f, 4) == -1, "opening waits for next regular beat");
            Require(MuseumStageGuide.PortraitPerformanceCue(8.99f, 4) == -1, "second gauge interval lasts until opening cue");
            Require(MuseumStageGuide.PortraitPerformanceCue(11f, 4) == -1, "gauge remains after final cue");
            Require(MuseumStageGuide.PortraitPerformanceCue(12f, 5) == -1, "open state shows no stale cue");
            Require(MuseumStageGuide.PortraitPerformanceCue(-.1f, 1) == -1, "negative time ignored");
            var reminder = new MuseumPuzzleReminderState();
            Require(!reminder.Update(2, true, 4.99f), "puzzle gauge shows for first five seconds");
            Require(reminder.Update(2, true, .01f), "puzzle reminder starts at five seconds");
            Require(reminder.Update(2, true, 2.49f), "puzzle reminder lasts 2.5 seconds");
            Require(!reminder.Update(2, true, .02f), "puzzle gauge returns after reminder");
            Require(!reminder.Update(2, true, 4.98f), "second gauge interval has not yet elapsed");
            Require(reminder.Update(2, true, .03f), "puzzle reminder repeats after five more gauge seconds");
            Require(!reminder.Update(2, false, .1f), "higher priority interrupts reminder");
            Require(!reminder.Update(2, true, 4.99f), "resuming starts a fresh gauge interval");
            Require(!reminder.Update(3, true, .01f), "stage change resets reminder interval");
            Require(!reminder.Update(3, false, 10f), "disabled or completed gauge does not count");
            Require(!reminder.Update(0, true, 10f), "non-puzzle context does not show reminders");
            var transition = new MuseumHudTextTransition();
            var first = new MuseumHudTextTransition.View { Mode = 0, Caption = "첫 문구" };
            var second = new MuseumHudTextTransition.View { Mode = 0, Caption = "새 문구" };
            var gaugeView = new MuseumHudTextTransition.View { Mode = 1 };
            transition.Update(first, .01f, false);
            Require(transition.Current.Caption == first.Caption && transition.Alpha == 1, "initial text visible");
            transition.Update(second, .05f, false);
            Require(transition.Current.Caption == first.Caption && transition.Alpha > 0 && transition.Alpha < 1, "old text fades before swap");
            transition.Update(second, .06f, false);
            Require(transition.Current.Caption == second.Caption && transition.Alpha == 0 && transition.Resizing, "new text waits during resize");
            transition.Update(second, .14f, false);
            Require(transition.Resizing && transition.Alpha == 0 && transition.ResizeAmount > 0, "resize occurs while text hidden");
            transition.Update(second, .15f, false);
            Require(!transition.Resizing && transition.Alpha == 0, "fade in starts after resize");
            transition.Update(second, .12f, false);
            Require(transition.Alpha == 1 && transition.Current.Caption == second.Caption, "new text becomes visible");
            transition.Update(gaugeView, .02f, true);
            Require(transition.Current.Mode == 1 && transition.Alpha == 1, "urgent gauge switches immediately");
            transition.Update(first, .05f, false);
            transition.Update(second, .06f, false);
            Require(transition.Current.Caption == second.Caption && transition.Resizing, "latest request replaces pending text");
            transition.Reset(); transition.Update(gaugeView, .01f, false);
            Require(transition.Current.Mode == 1 && transition.Alpha == 1, "reset removes old content");
            var handoff = new MuseumResetGaugeHandoff();
            Require(!handoff.Update(true, false, false, true, 0), "reset caption remains visible");
            Require(handoff.Update(false, true, false, true, .05f), "hide puzzle caption when reset becomes gauge");
            Require(handoff.Update(false, true, false, true, .49f), "hide caption during gauge morph");
            Require(!handoff.Update(false, true, false, true, .5f), "handoff ends after caption has faded");
            Require(!handoff.Update(false, true, false, true, 1f), "normal gauge remains unaffected");
            handoff.Update(true, false, false, true, 0);
            Require(!handoff.Update(false, false, false, true, 0), "puzzle caption remains if no gauge is active");
            handoff.Update(true, false, false, true, 0);
            Require(!handoff.Update(false, true, true, true, 0), "higher priority clears handoff");
            handoff.Update(true, false, false, true, 0);
            Require(!handoff.Update(false, true, false, false, 0), "hidden HUD clears handoff");
            state.Update(4, true, true, false, 60, 3);
            state.Update(4, false, false, false, 61, 3);
            Require(!state.Requested, "pause hides guide");
            state.Update(4, true, false, false, 62, 3);
            Require(!state.Requested, "resume does not replay");
            state.Update(4, true, true, false, 63, 3);
            Require(state.Requested, "replay after resume");
            state.Update(-1, true, false, false, 64, 3);
            state.Update(4, true, false, false, 65, 3);
            Require(state.Requested, "entry after leaving supported course");
            Console.WriteLine("MUSEUM_STAGE_GUIDE_TESTS_PASSED");
        }
    }
}