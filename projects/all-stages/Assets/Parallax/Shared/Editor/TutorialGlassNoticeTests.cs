using System;
using System.Reflection;
using GlassTrial;
using ShadeLink.Tutorial;
using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class TutorialGlassNoticeTests
    {
        static void Require(bool value, string message)
        {
            if (!value) throw new Exception("TUTORIAL_GLASS_NOTICE_FAILED: " + message);
        }
        static void Set(TutorialGame game, string name, object value)
        {
            var field = typeof(TutorialGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, "field " + name);
            field.SetValue(game, value);
        }
        public static void Run()
        {
            var shader = Resources.Load<Shader>("GlassPanel");
            Require(shader != null && !ShaderUtil.ShaderHasError(shader), "glass shader");
            var tutorialShader = Resources.Load<Shader>("TutorialIslandGlass");
            Require(tutorialShader != null && tutorialShader != shader && !ShaderUtil.ShaderHasError(tutorialShader), "tutorial-only glass shader");
            var shadowShader = Resources.Load<Shader>("TutorialIslandShadow");
            Require(shadowShader != null && !ShaderUtil.ShaderHasError(shadowShader), "tutorial-only shadow shader");
            Require(TutorialGlassNotice.NoticeHeight(20) == 44f, "single-line minimum height");
            Require(TutorialGlassNotice.NoticeHeight(60) == 78f, "multiline notice grows with padding");
            var shadowMaterial = new Material(shadowShader);
            try { Require(shadowMaterial.GetFloat("_Padding") == 13f && shadowMaterial.GetFloat("_FadeDistance") == 11f, "compact shadow defaults"); }
            finally { UnityEngine.Object.DestroyImmediate(shadowMaterial); }
            var glassMaterial = new Material(tutorialShader);
            try
            {
                Require(Math.Abs(glassMaterial.GetFloat("_DarkTintStrength") - .26f) < .001f, "dark tint default");
                Require(Math.Abs(glassMaterial.GetFloat("_WhiteTintStrength") - .14f) < .001f, "white tint default");
                Require(Math.Abs(glassMaterial.GetFloat("_Saturation") - 1.45f) < .001f, "saturation default");
                Require(glassMaterial.GetFloat("_ExtraBlurPixels") > 0, "tutorial-only extra blur");
            }
            finally { UnityEngine.Object.DestroyImmediate(glassMaterial); }
            var root = new GameObject("Tutorial notice test");
            try
            {
                var game = root.AddComponent<TutorialGame>();
                root.AddComponent<TutorialGlassNotice>();
                var notify = typeof(TutorialGame).GetMethod("NotifyCheckpoint", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(notify != null, "checkpoint notifier exists");
                notify.Invoke(game, new object[] { false, "03 구역 저장" });
                int pendingCount = 0;
                string pendingText = null;
                game.ReplayPendingCheckpointNotices((reset, text) => { pendingCount++; pendingText = text; });
                game.ReplayPendingCheckpointNotices((reset, text) => pendingCount++);
                Require(pendingCount == 1 && pendingText == "03 구역 저장", "startup checkpoint event delivered once");
                Require(game.BottomNoticeVisible, "visible during gameplay");
                Require(game.CurrentBottomNotice == "주사위를 잡아보세요", "initial pickup guidance");
                Set(game, "firstDiePickedUp", true);
                Require(game.CurrentBottomNotice == null, "pickup guidance disappears after pickup");
                Set(game, "firstDiePickedUp", false);
                Set(game, "message", "existing message");
                Set(game, "messageUntil", Time.time + 20);
                Require(game.CurrentBottomNotice == "existing message", "timed notice preserved");
                typeof(TutorialGame).GetMethod("StartCornerGuide", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
                Require(game.CurrentBottomNotice == "길이 왼쪽으로 꺾였어요", "first corner message");
                game.AdvanceCornerGuide(2.99f);
                Require(game.CurrentBottomNotice == "길이 왼쪽으로 꺾였어요", "first corner message lasts three seconds");
                game.AdvanceCornerGuide(.02f);
                Require(game.CurrentBottomNotice == "햇빛이 오는 쪽에 주사위를 놓아보세요", "second corner message begins");
                game.AdvanceCornerGuide(3.98f);
                Require(game.CurrentBottomNotice == "햇빛이 오는 쪽에 주사위를 놓아보세요", "second corner message lasts four seconds");
                game.AdvanceCornerGuide(.02f);
                Require(!game.CornerGuideActive, "corner sequence ends");
                typeof(TutorialGame).GetMethod("StartCornerGuide", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
                game.Tell("replacement message", 2);
                Require(!game.CornerGuideActive && game.CurrentBottomNotice == "replacement message", "new notice cancels stale corner sequence");
                game.Tell("existing message", 20);
                string[] warnings = { "위에 놓인 사물을 먼저 옮겨 주세요.", "현재 크기로 조준한 위치에 놓을 공간이 없습니다.", "잡기 전 위치에 서 있습니다. 옆으로 이동한 뒤 취소하세요." };
                const string standing = "서 있는 사물은 잡을 수 없습니다. 먼저 바닥이나 다른 발판으로 이동하세요.";
                var standingNotice = MuseumStageGuide.ClassifyNotice(standing);
                Require(TutorialGlassNotice.IsInteractionWarning(standing) && standingNotice.Kind == MuseumStageGuide.NoticeKind.Warning && standingNotice.Seconds == 2f && standingNotice.Text == "서 있는 사물은 잡을 수 없습니다.", "standing warning matches museum presentation");
                foreach (var warning in warnings)
                {
                    var classified = MuseumStageGuide.ClassifyNotice(warning);
                    Require(TutorialGlassNotice.IsInteractionWarning(warning) && classified.Kind == MuseumStageGuide.NoticeKind.Warning && classified.Seconds == 3f && classified.Text == warning, "warning text and duration");
                }
                const string bodyWarning = "몸에서 조금 떨어진 곳에 놓으세요.";
                var bodyNotice = TutorialGlassNotice.ClassifyInteractionWarning(bodyWarning);
                Require(TutorialGlassNotice.IsInteractionWarning(bodyWarning) && bodyNotice.Kind == MuseumStageGuide.NoticeKind.Warning && bodyNotice.Text == bodyWarning && bodyNotice.Seconds == 2f, "body overlap warning lasts two seconds");
                Require(!TutorialGlassNotice.IsInteractionWarning(null), "null is not a warning");
                Require(Math.Abs(TutorialGlassNotice.CheckpointNoticeSeconds(false, "상태 저장") - 1f) < .001f, "state save lasts one second");
                Require(Math.Abs(TutorialGlassNotice.CheckpointNoticeSeconds(false, "02 구역 저장") - 1f) < .001f, "zone save lasts one second");
                Require(Math.Abs(TutorialGlassNotice.CheckpointNoticeSeconds(false, "마지막 쉼터 저장") - 1f) < .001f, "shelter save lasts one second");
                Require(Math.Abs(TutorialGlassNotice.CheckpointNoticeSeconds(true, "상태 저장") - 3f) < .001f, "reset duration unchanged");
                var common = new MuseumCommonNoticeState();
                common.Trigger(new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.Save, "마지막 쉼터 저장", 1f), 10, false);
                Require(common.Active && common.Current.Text == "마지막 쉼터 저장" && Math.Abs(common.Remaining(10.5f) - .5f) < .001f, "save effect duration");
                common.Trigger(new MuseumStageGuide.Notice(MuseumStageGuide.NoticeKind.Reset, "쉼터로 돌아왔어요", 3), 12, false);
                Require(common.Active && common.Current.Kind == MuseumStageGuide.NoticeKind.Reset, "reset replaces save");
                common.Update(15, false);
                Require(!common.Active, "reset effect expires");
                Set(game, "climbComplete", true);
                Require(game.CurrentBottomNotice == "발판 연습 완료 · R로 다시 시작", "completion beats timed message");
                Set(game, "inSunlightRoom", true);
                Set(game, "sunlightComplete", true);
                Require(game.CurrentBottomNotice == "그늘 연습 완료 · Esc에서 다시 시작", "sunlight completion priority");
                Set(game, "sunlightComplete", false);
                Require(game.CurrentBottomNotice == "existing message", "timed message in sunlight room");
                Set(game, "paused", true);
                Require(!game.BottomNoticeVisible, "pause hides glass");
                Set(game, "paused", false);
                Set(game, "elevatorDeparted", true);
                Require(!game.BottomNoticeVisible, "elevator departure hides glass");
                var transition = new MuseumHudTextTransition();
                transition.Update(new MuseumHudTextTransition.View { Caption = "old" }, 0, false);
                transition.Update(new MuseumHudTextTransition.View { Caption = "new" }, .05f, false);
                Require(transition.Current.Caption == "old" && transition.Alpha < 1, "old text fades before swap");
                transition.Update(new MuseumHudTextTransition.View { Caption = "new" }, .06f, false);
                Require(transition.Current.Caption == "new" && transition.Resizing && transition.Alpha == 0, "new text appears after fade");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("TUTORIAL_GLASS_NOTICE_TESTS_PASSED");
        }
    }
}