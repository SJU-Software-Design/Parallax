using UnityEngine;
using ShadeLink.Gallery;

namespace ShadeLink.Tutorial
{
    public sealed partial class TutorialGame
    {
        public TutorialElevatorRide MuseumElevator { get; private set; }
        public bool ElevatorPaused => paused;
        public bool ElevatorDeparted => elevatorDeparted;
        bool elevatorDeparted;

        void ResetMuseumElevator()
        {
            elevatorDeparted = false;
            if (MuseumElevator == null) return;
            MuseumElevator.ResetRide();
            MuseumElevator.BindPlayer(Controller, Camera);
        }

        void BuildMuseumElevator()
        {
            var prefab = Resources.Load<GameObject>("Tutorial/ElevatorE01");
            if (prefab == null) { Debug.LogError("E01 elevator prefab missing. Run ElevatorAssetPreparation.Prepare."); return; }
            var cabin = Instantiate(prefab);
            cabin.name = "E01 · Tutorial to museum elevator";
            cabin.transform.SetPositionAndRotation(SunlightOrigin + new Vector3(27, 0, 33.1f), Quaternion.Euler(0, 180, 0));
            MuseumElevator = cabin.AddComponent<TutorialElevatorRide>();
            MuseumElevator.Initialize(cabin.GetComponent<ElevatorE01Asset>());
            MuseumElevator.BindPlayer(Controller, Camera);
            TutorialMuseumElevatorFlow.Install(this, MuseumElevator);
            // Close the architectural surround above/beside the actual imported
            // frame, and enclose the shaft outside the frosted rear window.
            SunBox("Elevator approach upper surround", new Vector3(27, 5.0f, 33.1f), new Vector3(3.3f, 4.1f, .22f), frame);
            for (int side = -1; side <= 1; side += 2)
            {
                SunBox("Elevator approach frame surround", new Vector3(27 + side * 1.43f, 1.48f, 33.1f), new Vector3(.44f, 2.96f, .22f), frame);
                SunBox("Elevator shaft side enclosure", new Vector3(27 + side * 2.4f, 3.5f, 36.7f), new Vector3(.2f, 7.0f, 7.0f), frame);
            }
            SunBox("Elevator shaft front enclosure", new Vector3(27, 4.98f, 33.5f), new Vector3(4.9f, 4.04f, .2f), frame);
            SunBox("Elevator shaft roof enclosure", new Vector3(27, 7.0f, 36.7f), new Vector3(5.0f, .2f, 7.0f), frame);
        }

        public void BeginElevatorDeparture()
        {
            if (Held != null) interaction.Cancel(true);
            if (Aimed != null) Aimed.Highlight(false, false);
            Aimed = null;
            elevatorDeparted = true;
            sunlightComplete = true;
            sunlightExposure = 0;
            motor.ClearInput();
            ClearLandingFeedback();
            ClearShadowPreview();
            Debug.Log("TUTORIAL_SUNLIGHT_COMPLETE · elevator doors closed");
        }

        public void RecoverElevatorDeparture()
        {
            elevatorDeparted = false; sunlightComplete = false; sunlightExposure = 0;
            MuseumElevator.ResetRide();
            MuseumElevator.BindPlayer(Controller, Camera);
            SetPaused(false);
            Tell("미술관을 불러오지 못했어요. 문 밖으로 나갔다가 다시 탑승해 주세요.", 7);
        }

        // Review entry point; the ordinary executable still begins lesson one.
        public void JumpToElevatorReview()
        {
            StartTutorialStage(3);
            sunlightCheckpoint = 3;
            Controller.enabled = false;
            Controller.transform.position = SunlightOrigin + new Vector3(27, .03f, 29.6f);
            Controller.enabled = true;
            motor.Reset(); controls.Reset(); yaw = 0; pitch = 0;
            SyncView();
            Tell("엘리베이터 앞으로 다가가 탑승해 보세요.", 7);
        }
    }
}
