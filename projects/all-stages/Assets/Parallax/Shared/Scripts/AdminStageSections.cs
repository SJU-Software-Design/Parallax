using System;
using System.Collections.Generic;
using UnityEngine;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;
using ShadeLink.Beach;
using ShadeLink.Eclipse;

namespace ShadeLink
{
    public sealed class AdminStageSection
    {
        public string Scene { get; }
        public int Index { get; }
        public string Label { get; }
        internal AdminStageSection(string scene, int index, string label)
        { Scene = scene; Index = index; Label = label; }
    }

    // A catalog of authored puzzle starts, shared by the administrator menu and
    // its loader. Records restore preceding gates; the selected puzzle stays
    // fresh. Normal new-game and continuing-game entry never call this adapter.
    public static class AdminStageSections
    {
        static readonly IReadOnlyList<AdminStageSection> Empty = Array.AsReadOnly(new AdminStageSection[0]);
        static readonly IReadOnlyList<AdminStageSection> Tutorial = Make("Tutorial", new[] {
            "0-1  크기 조절", "0-2  주사위 발판", "0-3  햇빛과 그늘" });
        static readonly IReadOnlyList<AdminStageSection> Gallery = Make("Gallery", new[] {
            "1-1  청동판의 그늘", "1-2  니케의 그림자", "1-3  병 속의 추 · 디케의 저울",
            "1-4  눈·코·입 초상", "1-5  성당 · 두 손 사이의 문" });
        static readonly IReadOnlyList<AdminStageSection> Beach = Make("Beach", new[] {
            "2-1  태양광 산책로", "2-2  두 섬을 잇는 비치 가든", "2-3  그림자에서 꺼낸 그늘 배",
            "2-4  바닷속 천막 다리 인양" });
        static readonly IReadOnlyList<AdminStageSection> Eclipse = Make("Eclipse", new[] {
            "크레딧 전 · 일식 퍼즐", "크레딧 후 · EXIT 찾기" });

        static IReadOnlyList<AdminStageSection> Make(string scene, string[] labels)
        {
            var entries = new AdminStageSection[labels.Length];
            for (int index = 0; index < entries.Length; index++)
                entries[index] = new AdminStageSection(scene, index, labels[index]);
            return Array.AsReadOnly(entries);
        }
        public static IReadOnlyList<AdminStageSection> ForStage(string scene)
        {
            switch (scene)
            {
                case "Tutorial": return Tutorial;
                case "Gallery": return Gallery;
                case "Beach": return Beach;
                case "Eclipse": return Eclipse;
                default: return Empty;
            }
        }
        public static string StageLabel(string scene)
        {
            switch (scene)
            {
                case "Tutorial": return "튜토리얼";
                case "Gallery": return "미술관";
                case "Beach": return "해변";
                case "Eclipse": return "일식";
                default: return "";
            }
        }
        public static bool IsValid(string scene, int index) => index >= 0 && index < ForStage(scene).Count;

        public static GameProgressRecord CreateRecord(string scene, int index)
        {
            if (!IsValid(scene, index)) throw new ArgumentOutOfRangeException(nameof(index), "선택한 맵의 구간이 없습니다.");
            GameProgressRecord record;
            switch (scene)
            {
                case "Tutorial":
                    record = new GameProgressRecord { stage = scene, tutorialStage = index + 1, checkpoint = 0,
                        tutorialStep = 0, tutorialFirstSize = 0 };
                    break;
                case "Gallery":
                    record = new GameProgressRecord { stage = scene, galleryRoom = index,
                        checkpoint = index == 0 ? 0 : index < 3 ? 1 : index - 1,
                        milestones = (1 << index) - 1, swordEncountered = index >= 3 };
                    break;
                case "Beach":
                    record = new GameProgressRecord { stage = scene, checkpoint = index,
                        milestones = index == 0 ? 0 : index < 3 ? 1 : 7 };
                    break;
                case "Eclipse":
                    // The dedicated Eclipse adapter owns its legitimate
                    // accepted-release/credits state; a checkpoint cannot encode it.
                    return null;
                default: throw new ArgumentException("지원하지 않는 맵입니다.", nameof(scene));
            }
            if (!GameProgress.Validate(record, out string reason)) throw new InvalidOperationException(reason);
            return record;
        }

        public static bool ApplyAfterLoad(IGameProgressStage stage, int index)
        {
            if (!ParallaxGameShell.Administrator || !GameProgressRuntime.TestSession || stage == null || !stage.ProgressReady)
                return false;
            if (stage is TutorialGame)
            {
                if (!IsValid("Tutorial", index)) return false;
            }
            else if (stage is GalleryGame gallery)
            {
                if (!IsValid("Gallery", index) || !gallery.conceptLayout || gallery.bottleBalance == null) return false;
                // Gallery's save slot shares Nike's checkpoint with the inserted
                // balance room. Use that room's own authored demo position while
                // retaining all preceding gates restored by RestoreProgress.
                if (index == 2)
                {
                    gallery.PlacePlayer(gallery.bottleBalance.DemoPosition);
                    gallery.yaw = 0; gallery.pitch = -.08f; gallery.SyncView();
                }
                else if (index == 4)
                {
                    // Same authored clear approach used by StartConceptDemo(3),
                    // without its ResetAll, which would close earlier gates.
                    gallery.PlacePlayer(GalleryBottleBalanceCourse.ShiftLegacy(new Vector3(0, 0, 49.1f)));
                    gallery.yaw = 0; gallery.pitch = 0; gallery.SyncView();
                }
            }
            else if (stage is BeachGame)
            {
                if (!IsValid("Beach", index)) return false;
            }
            else if (stage is EclipseFinaleGame eclipse)
            {
                if (!IsValid("Eclipse", index) || !eclipse.TryStartAdminSection(index)) return false;
            }
            else return false;
            stage.SetProgressPaused(true);
            return true;
        }
    }
}