using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed partial class GalleryGame : IGameProgressStage
    {
        bool swordEncountered;
        public bool ProgressReady => controller != null && player != null && props != null && lighting != null;
        public bool ProgressPaused => !active;
        public bool ProgressTransitioning => Transitioning || ElevatorArrival || ShadeLink.Tutorial.TutorialMuseumElevatorFlow.ArrivalPending;
        public GameProgressRecord CaptureProgress()
        {
            // Encounter history survives the ordinary death/checkpoint resets.
            if (bottleBalance != null && bottleBalance.InRoom) swordEncountered = true;
            int room = checkpoint >= 3 ? 4 : checkpoint >= 2 ? 3 :
                bottleBalance != null && bottleBalance.InRoom ? 2 :
                checkpoint >= 1 ? 1 : 0;
            int complete = 0;
            if (shadowCourse != null && shadowCourse.TutorialCleared) complete |= 1;
            if (shadowCourse != null && shadowCourse.NikeUnlocked && shadowCourse.NikeGateOpen) complete |= 2;
            if (bottleBalance != null && bottleBalance.Solved && bottleBalance.Reward != null &&
                bottleBalance.Reward.Unlocked && bottleBalance.Door != null && bottleBalance.Door.Opening >= .995f) complete |= 4;
            if (portraitGate != null && portraitGate.Opened) complete |= 8;
            return new GameProgressRecord { stage = "Gallery", galleryRoom = room,
                checkpoint = room == 0 ? 0 : room < 3 ? 1 : room - 1, milestones = complete, swordEncountered = this.swordEncountered };
        }
        public void RestoreProgress(GameProgressRecord record)
        {
            ResetAll();
            swordEncountered = record.swordEncountered;
            int room = Mathf.Clamp(record.galleryRoom, 0, 4), complete = record.milestones;
            checkpoint = room == 0 ? 0 : room < 3 ? 1 : room - 1;
            bool bronzeDone = room >= 1 || (complete & 1) != 0;
            bool nikeDone = room >= 2 || (complete & 2) != 0;
            bool dikeDone = room >= 3 || (complete & 4) != 0;
            bool portraitDone = room >= 4 || (complete & 8) != 0;
            shadowCourse?.RestoreForCheckpoint(bronzeDone, nikeDone);
            if (dikeDone) bottleBalance?.RestoreCompletedForCheckpoint();
            if (portraitDone) portraitGate?.RestoreCompletedForCheckpoint();
            // Match the existing death respawn, including the shared Nike/Dike checkpoint.
            Vector3 spawn = SpawnPoints[checkpoint];
            PlacePlayer(spawn); yaw = 0; pitch = -.08f; exposure = sunExposureTime = 0; safe = true;
            lighting.Rebuild(); lighting.Synchronize(); SyncView();
            Tell("저장된 구간의 쉼터에서 이어합니다.", 4);
        }
        public void SetProgressPaused(bool value) { if (value) Pause(); else if (!won) Resume(); }
        public void RestartProgressCheckpoint() => ResetZone();
    }
}
