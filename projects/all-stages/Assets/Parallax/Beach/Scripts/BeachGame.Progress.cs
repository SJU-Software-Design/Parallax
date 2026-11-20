using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed partial class BeachGame : IGameProgressStage
    {
        public bool ProgressReady => controller != null && player != null && props != null && lighting != null;
        public bool ProgressPaused => !active;
        public bool ProgressTransitioning => skyShip != null && skyShip.Flying && !skyShip.EclipseTransitionFailed;
        public GameProgressRecord CaptureProgress()
        {
            int complete = 0;
            if (lightGate != null && lightGate.unlocked) complete |= 1;
            if (shadowHarbor != null && shadowHarbor.Solved && !shadowHarbor.Materializing) complete |= 2;
            if (boat != null && boat.Arrived) complete |= 4;
            if (counterweightFinale != null && counterweightFinale.Ready) complete |= 8;
            return new GameProgressRecord { stage = "Beach", checkpoint = Mathf.Clamp(checkpoint, 0, 3), milestones = complete };
        }
        static void RestoreGate(BeachLightGate gate)
        {
            if (gate == null) return;
            gate.unlocked = gate.illuminated = true; gate.charge = gate.opening = 1;
            gate.panel.transform.position = gate.closedPosition + gate.openOffset;
        }
        public void RestoreProgress(GameProgressRecord record)
        {
            ResetAll(); checkpoint = Mathf.Clamp(record.checkpoint, 0, 3);
            if (checkpoint >= 1 || (record.milestones & 1) != 0) RestoreGate(lightGate);
            bool arrived = checkpoint >= 3 || (record.milestones & 4) != 0;
            if (arrived || (record.milestones & 2) != 0) shadowHarbor?.RestoreCompletedForCheckpoint(arrived);
            if (arrived) RestoreGate(waterGate);
            if (counterweightFinale != null && (record.milestones & 8) != 0)
            { counterweightFinale.RestoreCompletedForCheckpoint(); Audio?.Ending?.RestoreCompletedHoistForCheckpoint(); }
            if (checkpoint == 3 && counterweightFinale != null) PlaceCounterweightObserver();
            else { PlacePlayer(BeachWorld.Checkpoints[checkpoint]); yaw = 0; pitch = -.08f; }
            exposure = 0; safe = true; lighting.Rebuild(); lighting.Synchronize(); SyncView();
            Tell("저장된 구간의 쉼터에서 이어합니다.", 4);
        }
        public void SetProgressPaused(bool value) { if (value) Pause(); else if (!won) Resume(); }
        public void RestartProgressCheckpoint() => ResetZone();
    }
}
