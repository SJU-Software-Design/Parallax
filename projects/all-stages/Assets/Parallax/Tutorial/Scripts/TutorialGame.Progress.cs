using UnityEngine;

namespace ShadeLink.Tutorial
{
    public sealed partial class TutorialGame : IGameProgressStage
    {
        public bool ProgressReady => Controller != null && Camera != null && interaction != null;
        public bool ProgressPaused => paused;
        public bool ProgressTransitioning => elevatorDeparted || TutorialMuseumElevatorFlow.ArrivalPending;
        public GameProgressRecord CaptureProgress()
        {
            int stage = inSunlightRoom ? 3 : inClimbRoom ? 2 : 1;
            return new GameProgressRecord { stage = "Tutorial", checkpoint = stage == 3 ? sunlightCheckpoint : 0,
                tutorialStage = stage, tutorialStep = stage == 1 ? exitUnlocked ? 3 : Mathf.Clamp(step, 0, 2) : 0,
                tutorialFirstSize = stage == 1 && firstSize >= OrangeMinimumSize ? Mathf.Min(firstSize, OrangeTargetSize) : 0 };
        }
        public void RestoreProgress(GameProgressRecord record)
        {
            StartTutorialStage(record.tutorialStage);
            if (record.tutorialStage == 1)
            {
                step = Mathf.Clamp(record.tutorialStep, 0, 3);
                firstSize = record.tutorialFirstSize;
                if (step == 1)
                {
                    float size = Mathf.Clamp(firstSize, OrangeMinimumSize, OrangeTargetSize);
                    die.homePosition = new Vector3(0, size * .5f, Room.max.z - size * .5f - .32f);
                    die.homeScale = Vector3.one * size;
                }
                else if (step >= 3)
                {
                    die.homePosition = new Vector3(0, MintTargetSize * .5f, Room.max.z - MintTargetSize * .5f - .32f);
                    die.homeScale = Vector3.one * MintTargetSize;
                }
                die.Restore(); RefreshLesson();
                if (step >= 3) { exitUnlocked = true; doorLamp.sharedMaterial = unlockedLamp; }
                ResetRotationHint(); SyncView();
            }
            else if (record.tutorialStage == 3)
            {
                sunlightCheckpoint = Mathf.Clamp(record.checkpoint, 0, SunlightStops.Length - 2);
                MoveToSunlightCheckpoint();
                yaw = Mathf.PI * .5f; pitch = -.04f; SyncView();
            }
            SetPaused(false);
        }
        public void SetProgressPaused(bool value) { SetPaused(value); Time.timeScale = value ? 0 : 1; }
        public void RestartProgressCheckpoint() => RestartLesson();
    }
}
