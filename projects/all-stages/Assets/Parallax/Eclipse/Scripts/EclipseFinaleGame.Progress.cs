using System;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Checkpoint saves intentionally reconstruct the opening puzzle. Transform,
    // carry and cinematic states are never serialized halfway through an ending.
    public sealed partial class EclipseFinaleGame : IGameProgressStage
    {
        public bool ProgressReady => view != null && body != null && world != null && moon != null && exitSign != null;
        public bool ProgressPaused => paused;
        public bool ProgressTransitioning => EndingSequenceActive;
        public GameProgressRecord CaptureProgress() => new GameProgressRecord { stage = "Eclipse", checkpoint = 0 };

        public void RestoreProgress(GameProgressRecord record)
        {
            if (record == null || record.stage != "Eclipse" || record.checkpoint != 0)
                throw new ArgumentException("일식 체크포인트가 올바르지 않습니다.");
            ResetGame(false);
            PlayerControls.ConfigureCamera(view);
        }
        public void SetProgressPaused(bool value)
        {
            if (value) { Pause(); Time.timeScale = 0; }
            else { Play(); PlayerControls.ConfigureCamera(view); Time.timeScale = 1; }
        }
        public void RestartProgressCheckpoint()
        {
            ResetGame(true);
            PlayerControls.ConfigureCamera(view);
        }
    }
}
