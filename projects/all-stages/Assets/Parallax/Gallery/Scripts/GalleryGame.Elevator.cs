using UnityEngine;
using ShadeLink.Tutorial;

namespace ShadeLink.Gallery
{
    public sealed partial class GalleryGame
    {
        public bool ElevatorArrival { get; private set; }

        public void BeginElevatorArrival(TutorialElevatorRide ride)
        {
            ElevatorArrival = true;
            started = active = true; won = false; checkpoint = 0;
            Time.timeScale = 1;
            ClearControlInput();
            ride.RestorePose(controller, view);
            Vector3 forward = view.transform.forward;
            yaw = Mathf.Atan2(forward.x, forward.z);
            pitch = Mathf.Asin(Mathf.Clamp(forward.y, -1, 1));
            player.feet = controller.transform.position;
            exposure = sunExposureTime = 0; safe = true;
            if (!automated) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            SyncView();
        }

        bool TickElevatorArrival(float dt)
        {
            if (!ElevatorArrival) return false;
            exposure = sunExposureTime = 0; safe = true;
            if (controller.transform.position.z > .65f)
            {
                ElevatorArrival = false;
                museumAudio?.SetCheckpoint(0);
                museumAudio?.SetGameplayRunning(true);
                Tell("B1 · 미술관에 도착했어요. 첫 전시실로 이동해 보세요.", 6);
                return false;
            }
            return true;
        }
    }
}
