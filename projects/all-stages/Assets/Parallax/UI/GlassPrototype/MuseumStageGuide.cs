namespace GlassTrial
{
    // Display numbering is independent of GalleryGame's internal checkpoints.
    public static class MuseumStageGuide
    {
        public struct Content
        {
            public readonly string Number, Title, Objective;
            public Content(string number, string title, string objective)
            {
                Number = number; Title = title; Objective = objective;
            }
        }

        static readonly Content[] Entries =
        {
            new Content("01", "명화 전시실", ""),
            new Content("02", "빛과 그림자", ""),
            new Content("03", "병 속의 추", ""),
            new Content("04", "시점의 방", ""),
            new Content("05", "두 손 사이의 문", "")
        };

        public static int StageFor(bool conceptLayout, bool exhibitionPreview, int checkpoint, bool inBottleRoom)
        {
            if (!conceptLayout || exhibitionPreview) return -1;
            if (inBottleRoom) return 2;
            switch (checkpoint)
            {
                case 0: return 0;
                case 1: return 1;
                case 2: return 3;
                case 3: return 4;
                default: return -1;
            }
        }

        public static Content Get(int stage) => Entries[stage];

        public static string SafeCaption(int stage, string caption)
        {
            if (stage > 0 && (string.IsNullOrWhiteSpace(caption) || caption == "그늘 · 안전")) return "";
            return string.IsNullOrWhiteSpace(caption) ? "그늘 · 안전" : caption;
        }

        public enum NoticeKind { None, Hidden, Warning, Reset, Save }
        public struct Notice
        {
            public NoticeKind Kind;
            public string Text;
            public float Seconds;
            public Notice(NoticeKind kind, string text, float seconds) { Kind = kind; Text = text; Seconds = seconds; }
        }

        public static Notice ClassifyNotice(string message)
        {
            switch (message)
            {
                case "WASD로 들고 이동 · 시선으로 크기 조절 · 클릭으로 놓기":
                case "청동 조각의 그림자 통로를 건넜습니다. 다음 전시실로 이동하세요.": return new Notice(NoticeKind.Hidden, "", 0);
                case "서 있는 사물은 잡을 수 없습니다. 먼저 바닥이나 다른 발판으로 이동하세요.": return new Notice(NoticeKind.Warning, "서 있는 사물은 잡을 수 없습니다.", 2);
                case "위에 놓인 사물을 먼저 옮겨 주세요.":
                case "현재 크기로 조준한 위치에 놓을 공간이 없습니다.":
                case "잡기 전 위치에 서 있습니다. 옆으로 이동한 뒤 취소하세요.": return new Notice(NoticeKind.Warning, message, 3);
                case "빛에 노출되어 현재 구역의 쉼터로 돌아왔습니다.": return new Notice(NoticeKind.Reset, "쉼터로 돌아왔어요", 3);
                case "병과 저울을 초기화했습니다. 니케 전시실의 진행은 유지됩니다.": return new Notice(NoticeKind.Reset, message, 3);
                case "현재 구역의 전시물과 위치를 초기화했습니다. 이전 구역은 유지됩니다.": return new Notice(NoticeKind.Reset, "현재 구역의 전시물과 위치를 초기화했습니다.", 3);
            }
            const string suffix = " 구역 저장 · R로 이 구역만 다시 시작할 수 있습니다.";
            if (message != null && message.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                string number = message.Substring(0, message.Length - suffix.Length);
                int parsed;
                if (number.Length >= 2 && int.TryParse(number, out parsed) && parsed > 0)
                    return new Notice(NoticeKind.Save, number + " 구역 저장", 2.5f);
            }
            return new Notice(NoticeKind.None, null, 0);
        }

        // The display's five stage numbers do not alter the game's checkpoints.
        public static Notice ClassifyNotice(string message, int displayStage)
        {
            var notice = ClassifyNotice(message);
            if (notice.Kind == NoticeKind.Save && displayStage >= 0 && displayStage < Entries.Length)
                notice.Text = Entries[displayStage].Number + " 구역 저장";
            return notice;
        }

        public static bool IsPortraitReset(Notice notice, int stage) =>
            stage == 3 && notice.Kind == NoticeKind.Reset &&
            notice.Text == "현재 구역의 전시물과 위치를 초기화했습니다.";

        public static bool HideLegacyCommonNotice(string message, bool glassAvailable)
        {
            var kind = ClassifyNotice(message).Kind;
            return kind == NoticeKind.Hidden || (glassAvailable && kind != NoticeKind.None);
        }

        // Suppress only notices previously identified as covered by the glass HUD.
        // When the glass HUD is unavailable, preserve the legacy notice as a fallback.
        public static bool SuppressCoveredBottomNotice(string message, int stage, bool glassAvailable)
        {
            if (!glassAvailable) return false;
            switch (message)
            {
                case "액자가 맞물렸어요. 열린 문으로 들어가세요.": return stage == 1;
                case "얼굴이 이어졌어요. 잠깐 바라봐 주세요.":
                case "시점의 초상이 완성되었습니다. 눈·코·입이 하나의 얼굴로 보입니다.": return stage == 3;
                case "디케가 놓아준 검을 집어 방 가운데 대리석 받침대에 꽂아 보세요.":
                case "방 가운데 대리석 받침대의 윗면 홈을 겨냥해 클릭하세요.":
                case "검을 집었어요. 방 가운데 대리석 받침대에 사용하세요. Backspace로 회수 받침에 돌려놓을 수 있어요.":
                case "병 속의 추 · 병 가까이에서 잡고 뒤로 물러나 확대 · H 힌트": return stage == 2;
                default: return false;
            }
        }

        // Entry takes priority in every room; perspective-room replays also retain priority.
        public static bool ProtectStageAnnouncement(int stage, bool requested, bool entry) =>
            requested && stage >= 0 && (entry || stage == 3);
        public static bool ShowGauge(int stage, bool requested, bool entry, bool gaugeNeeded) =>
            gaugeNeeded && !ProtectStageAnnouncement(stage, requested, entry);

        // The initial objective is persistent only while the actual opening is too narrow.
        public static string BottleObjective(bool needsEnlargement, string currentStatus)
        {
            return needsEnlargement ? "" : DisplayCaption(2, currentStatus);
        }

        public static string CathedralInitialCaption(string currentStatus)
        {
            return null;
        }

        // Convert only presentation text; puzzle Status strings remain unchanged.
        public static string DisplayCaption(int stage, string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (stage == 3)
            {
                switch (text)
                {
                    case "완성된 초상이 커튼에 스며들고 있어요.": return "초상이 커튼에 스며들어요";
                    case "초상이 천천히 미소 짓고 있어요.": return "초상이 미소 짓고 있어요";
                    case "웃는 커튼이 천천히 열리고 있어요.": return "웃는 커튼이 열리고 있어요";
                    default: return "";
                }
            }
            if (stage == 1 && (text == "빈 캔버스와 주변 전시물을 살펴보세요" || text == "빈 캔버스와 주변 전시물을 살펴보세요.")) return "";
            if (stage == 2)
            {
                switch (text)
                {
                    case "병을 옮겨 키우고 좁은 입구를 넓혀 보세요":
                    case "입구가 넓어졌어요 · 병을 들어 아래로 기울여 보세요":
                    case "입구를 향해 움직이고 있어요 · 조금 더 기울여 보세요": return "";
                    case "꺼낸 법전을 오른쪽 저울에 올려 보세요": return "법전을 오른쪽 저울에 올려보세요";
                    case "수평이에요 · 조각을 내려놓고 잠깐 기다리세요": return "수평이에요 · 내려놓고 기다려주세요";
                    case "검이 떨어지고 있어요 · 아래 받침에서 집을 수 있어요":
                    case "디케가 놓아준 검을 클릭해 집으세요":
                    case "균형을 되찾았어요 · 디케가 검을 놓아줍니다": return "";
                    case "검을 들었어요 · 방 가운데 대리석 받침대의 홈을 클릭하세요": return "";
                    case "검이 고정됐어요 · 봉인이 풀리고 있어요": return "";
                }
            }
            if (stage == 4)
            {
                switch (text)
                {
                    case "조각을 조금 더 키워 보세요":
                    case "조각을 좀 더 키워보세요": return "";
                    case "조각을 조금 더 바로 세워 주세요": return "조각을 바로 세워주세요";
                    case "주변이 넓은 바닥으로 옮겨 주세요": return "넓은 바닥으로 옮겨주세요";
                    case "두 손 사이로 해변이 보입니다": return "두 손 사이로 해변이 보여요";
                }
            }
            return text;
        }

        // Three notices follow the actual performance clock at a 4.5s cadence.
        // Each notice lasts 2s; the gauge occupies the 2.5s between notices.
        public static int PortraitPerformanceCue(float elapsed, int revealStage)
        {
            if (elapsed < 0f) return -1;
            int cue = (int)(elapsed / 4.5f);
            if (cue < 0 || cue > 2 || revealStage != (cue == 0 ? 1 : cue + 2)) return -1;
            return elapsed - cue * 4.5f < 2f ? cue : -1;
        }
    }

    public sealed class MuseumCommonNoticeState
    {
        public MuseumStageGuide.Notice Current { get; private set; }
        public float StartedAt { get; private set; }
        public bool Active { get; private set; }
        public bool HasPendingSave { get; private set; }
        public bool PendingIsReset => HasPendingSave && pendingSave.Kind == MuseumStageGuide.NoticeKind.Reset;
        int pendingStage;
        int activeStage = -1;
        MuseumStageGuide.Notice pendingSave;
        public void OnNotice(MuseumStageGuide.Notice notice, float now, bool entry, bool blocked, int stage)
        {
            Update(now, false);
            // The 04 reset is readable for its full duration despite ordinary
            // puzzle notices emitted during the reset or subsequent frames.
            if (Active && activeStage == stage && MuseumStageGuide.IsPortraitReset(Current, stage) &&
                !blocked && !entry && notice.Kind != MuseumStageGuide.NoticeKind.Reset) return;
            if (PendingIsReset && MuseumStageGuide.IsPortraitReset(pendingSave, pendingStage) &&
                stage == pendingStage && notice.Kind != MuseumStageGuide.NoticeKind.Reset) return;
            // A stage's ordinary puzzle/hidden notices can arrive during the
            // entry animation. They must not silently discard its save notice.
            if (HasPendingSave && entry && !blocked && stage == pendingStage &&
                (notice.Kind == MuseumStageGuide.NoticeKind.None || notice.Kind == MuseumStageGuide.NoticeKind.Hidden)) return;
            HasPendingSave = false;
            if (MuseumStageGuide.IsPortraitReset(notice, stage) && (entry || blocked))
            {
                Active = false; pendingSave = notice; pendingStage = stage; HasPendingSave = true;
                return;
            }
            if (entry && !blocked && (notice.Kind == MuseumStageGuide.NoticeKind.Save || notice.Kind == MuseumStageGuide.NoticeKind.Reset))
            {
                Active = false; pendingSave = notice; pendingStage = stage; HasPendingSave = true;
            }
            else { Trigger(notice, now, entry || blocked); activeStage = stage; }
        }
        public void AdvancePending(float now, bool entry, bool blocked, int stage)
        {
            if (!HasPendingSave) return;
            if (stage != pendingStage) { HasPendingSave = false; return; }
            if (blocked)
            {
                if (!MuseumStageGuide.IsPortraitReset(pendingSave, pendingStage)) HasPendingSave = false;
                return;
            }
            if (!entry) { HasPendingSave = false; Trigger(pendingSave, now, false); activeStage = stage; }
        }
        public void CancelPending() { HasPendingSave = false; }
        public void Trigger(MuseumStageGuide.Notice notice, float now, bool blocked)
        {
            Active = !blocked && notice.Kind != MuseumStageGuide.NoticeKind.None && notice.Kind != MuseumStageGuide.NoticeKind.Hidden;
            Current = notice; StartedAt = now;
        }
        public void Update(float now, bool interrupted)
        {
            if (interrupted || (Active && now - StartedAt >= Current.Seconds)) Active = false;
        }
        public float Remaining(float now)
        {
            if (!Active || Current.Seconds <= 0) return 0;
            return System.Math.Max(0, System.Math.Min(1, 1 - (now - StartedAt) / Current.Seconds));
        }
    }

    // The clock advances only while an uninterrupted puzzle gauge is active.
    public sealed class MuseumPuzzleReminderState
    {
        const float GaugeSeconds = 5f, ReminderSeconds = 2.5f;
        int context = -1;
        float elapsed;

        public bool Update(int currentContext, bool eligible, float deltaTime)
        {
            if (!eligible || currentContext <= 0)
            {
                Reset();
                return false;
            }
            if (currentContext != context) { context = currentContext; elapsed = 0; }
            elapsed += System.Math.Max(0, deltaTime);
            float cycle = GaugeSeconds + ReminderSeconds;
            if (elapsed >= cycle) elapsed %= cycle;
            return elapsed >= GaugeSeconds;
        }

        public void Reset() { context = -1; elapsed = 0; }
    }

    // Text changes are staged separately from game-state and timer updates.
    public sealed class MuseumHudTextTransition
    {
        public struct View
        {
            public int Mode;
            public string Caption, Number, Title, Objective;
            public bool SameContent(View other) => Mode == other.Mode && Caption == other.Caption &&
                Number == other.Number && Title == other.Title && Objective == other.Objective;
        }

        const float FadeOutSeconds = .10f, ResizeSeconds = .28f, FadeInSeconds = .12f;
        View current, pending;
        int phase; // 0 steady, 1 fade out, 2 resize, 3 fade in
        float elapsed, fadeStart;
        bool initialized;
        public View Current => current;
        public float Alpha { get; private set; }
        public float ResizeAmount { get; private set; }
        public bool Resizing => phase == 2;

        public void Update(View target, float dt, bool immediate)
        {
            dt = System.Math.Max(0, dt);
            if (!initialized || immediate)
            {
                initialized = true; current = pending = target; phase = 0; elapsed = 0; Alpha = 1; ResizeAmount = 1;
                return;
            }
            if (!target.SameContent(phase == 1 ? pending : current))
            {
                pending = target;
                if (phase != 1) { phase = 1; elapsed = 0; fadeStart = Alpha; }
            }
            else if (phase == 1 && target.SameContent(current))
            {
                phase = 3; elapsed = Alpha * FadeInSeconds;
            }
            if (phase == 0) { Alpha = 1; return; }
            elapsed += dt;
            if (phase == 1)
            {
                Alpha = fadeStart * System.Math.Max(0, 1 - elapsed / FadeOutSeconds);
                if (elapsed >= FadeOutSeconds) { current = pending; phase = 2; elapsed = 0; Alpha = 0; ResizeAmount = 0; }
            }
            else if (phase == 2)
            {
                Alpha = 0;
                // The renderer keeps the old view for the entire fade-out; only
                // then does the new content determine the target panel size.
                current = pending;
                ResizeAmount = System.Math.Min(1, elapsed / ResizeSeconds);
                if (elapsed >= ResizeSeconds) { phase = 3; elapsed = 0; ResizeAmount = 1; }
            }
            else
            {
                Alpha = System.Math.Min(1, elapsed / FadeInSeconds);
                if (elapsed >= FadeInSeconds) { phase = 0; Alpha = 1; }
            }
        }

        public void Reset() { initialized = false; phase = 0; elapsed = 0; Alpha = 0; ResizeAmount = 0; }
    }

    // Hide the underlying puzzle caption until the gauge has morphed back in.
    public sealed class MuseumResetGaugeHandoff
    {
        bool previousPortraitReset, hidingCaption;

        public bool Update(bool portraitReset, bool puzzleGauge, bool higherPriority, bool visible, float gaugeMorph)
        {
            if (!visible || higherPriority) { Reset(); return false; }
            if (previousPortraitReset && !portraitReset && puzzleGauge) hidingCaption = true;
            if (portraitReset || !puzzleGauge || gaugeMorph >= .5f) hidingCaption = false;
            previousPortraitReset = portraitReset;
            return hidingCaption;
        }

        public void Reset() { previousPortraitReset = false; hidingCaption = false; }
    }

    // Timing and priority are independent of rendering so they can be tested deterministically.
    public sealed class MuseumStageGuideState
    {
        int lastStage = -1;
        float shownAt;
        public bool Requested { get; private set; }
        public bool Entry { get; private set; }
        public int RequestSerial { get; private set; }

        public void Update(int stage, bool active, bool replay, bool interrupted, float now, float seconds)
        {
            if (!active) { Requested = false; Entry = false; return; }
            if (stage < 0) { Requested = false; Entry = false; lastStage = -1; return; }
            bool entered = stage != lastStage;
            lastStage = stage;
            if (entered || replay)
            {
                Entry = entered || (Requested && Entry);
                Requested = true; shownAt = now; RequestSerial++;
            }
            else if (interrupted && !MuseumStageGuide.ProtectStageAnnouncement(stage, Requested, Entry)) Interrupt();
            if (now - shownAt >= seconds) Interrupt();
        }

        public void Interrupt() { Requested = false; Entry = false; }
    }
}