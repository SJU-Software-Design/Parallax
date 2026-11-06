using ShadeLink.Gallery;
using UnityEngine;

namespace ShadeLink.Tutorial
{
    public sealed partial class TutorialGame
    {
        const float ClimbDoorHeight = 5.6f;
        const float ClimbRoomHeight = 21.5f;
        const float ClimbWallX = ClimbEntryX + 42f;
        const float ClimbLandingDepth = 2.2f;
        const float ClimbEntryX = RoomWidth * .5f + PassageDepth;
        const float ClimbMantleHeight = 3f;
        static readonly Bounds ClimbRoom = new Bounds(
            new Vector3((ClimbEntryX + ClimbWallX) * .5f, ClimbRoomHeight * .5f, DoorCenter),
            new Vector3(ClimbWallX - ClimbEntryX, ClimbRoomHeight, 28f));
        static readonly Vector3 ClimbSpawn = new Vector3(20f, .03f, DoorCenter);
        GalleryProp[] climbDice;
        Collider climbDoorFace, climbDoorLanding;
        bool inClimbRoom, climbComplete;
        bool climbArrivalPlaying;
        int nextClimbDrop;
        float climbDropDelay;

        void BuildClimbRoom()
        {
            float length = ClimbRoom.size.x;
            float width = ClimbRoom.size.z;
            float centerX = ClimbRoom.center.x;
            float halfDoor = DoorWidth * .5f;
            float wingWidth = width * .5f - halfDoor;
            float halfWall = DoorWallThickness * .5f;

            BuildFloor(ClimbRoom, "Climb room floor");
            Box("Climb room ceiling", new Vector3(centerX, ClimbRoomHeight + .2f, DoorCenter),
                new Vector3(length, .4f, width), frame);
            foreach (float side in new[] { -1f, 1f })
            {
                float edge = DoorCenter + side * width * .5f;
                Box("Climb side wall", new Vector3(centerX, ClimbRoomHeight * .5f, edge + side * .2f),
                    new Vector3(length, ClimbRoomHeight, .4f), wallSeam);
                BuildWallPanels(new Vector3(centerX, 0, edge), -side * Vector3.forward, length, ClimbRoomHeight);
                float wingZ = DoorCenter + side * (halfDoor + wingWidth * .5f);
                Box("Climb entry wing", new Vector3(ClimbEntryX - halfWall, ClimbRoomHeight * .5f, wingZ),
                    new Vector3(DoorWallThickness, ClimbRoomHeight, wingWidth), wallSeam);
                Box("Climb exit wing", new Vector3(ClimbWallX + halfWall, ClimbRoomHeight * .5f, wingZ),
                    new Vector3(DoorWallThickness, ClimbRoomHeight, wingWidth), wallSeam);
                BuildWallPanels(new Vector3(ClimbEntryX, 0, wingZ), Vector3.right, wingWidth, ClimbRoomHeight);
                BuildWallPanels(new Vector3(ClimbWallX, 0, wingZ), Vector3.left, wingWidth, ClimbRoomHeight);
            }
            Box("Climb entry lintel", new Vector3(ClimbEntryX - halfWall, (ClimbRoomHeight + DoorHeight) * .5f, DoorCenter),
                new Vector3(DoorWallThickness, ClimbRoomHeight - DoorHeight, DoorWidth), steel);
            climbDoorFace = Box("High doorway base", new Vector3(ClimbWallX + halfWall, ClimbDoorHeight * .5f, DoorCenter),
                new Vector3(DoorWallThickness, ClimbDoorHeight, DoorWidth), steel).GetComponent<Collider>();
            Box("High doorway base skirting", new Vector3(ClimbWallX - .031f, .13f, DoorCenter),
                new Vector3(.08f, .26f, DoorWidth), frame, false);
            float lintel = ClimbDoorHeight + DoorHeight;
            Box("High doorway lintel", new Vector3(ClimbWallX + halfWall, (ClimbRoomHeight + lintel) * .5f, DoorCenter),
                new Vector3(DoorWallThickness, ClimbRoomHeight - lintel, DoorWidth), steel);
            BuildClimbLanding();
            BuildRoomLights(ClimbRoom, Vector3.right);
            foreach (float side in new[] { -1f, 1f })
                RoomLight(new Vector3(ClimbWallX - 5.8f, ClimbDoorHeight + 4.4f,
                    DoorCenter + side * ClimbRoom.extents.z * .48f), LightType.Point, 13f, .95f);
            BuildClimbDice();
        }

        void BuildClimbDice()
        {
            climbDice = new GalleryProp[2];
            for (int i = 0; i < climbDice.Length; i++)
            {
                float dieSize = i == 0 ? 1.8f : 3.6f;
                var spawn = new Vector3(ClimbRoom.center.x - (i == 0 ? 5.8f : 2f), dieSize * .5f + .005f,
                    DoorCenter + (i == 0 ? 3.3f : -3.7f));
                var prop = BuildDie("Climb die " + (i + 1), spawn, dieSize);
                prop.transform.rotation = Quaternion.Euler(0, i == 0 ? 22f : -28f, 0);
                prop.Remember();
                prop.InitializePhysics();
                prop.placement.PreviewUntilSettled = false;
                prop.placement.SetPlayer(Controller);
                AttachLandingFeedback(prop);
                climbDice[i] = prop;
                SuspendClimbDie(prop);
            }
        }

        void BuildClimbLanding()
        {
            const float frameDepth = .16f;
            const float frameDrop = .13f;
            float liningDepth = ClimbLandingDepth - DoorWallThickness;
            float centerX = ClimbWallX + DoorWallThickness + liningDepth * .5f;
            float frameX = ClimbWallX - frameDepth * .5f;
            climbDoorLanding = Box("High doorway landing", new Vector3(centerX, ClimbDoorHeight - .20f, DoorCenter),
                new Vector3(liningDepth, .40f, DoorWidth), steel).GetComponent<Collider>();
            foreach (float side in new[] { -1f, 1f })
            {
                Box("High doorway jamb", new Vector3(centerX, ClimbDoorHeight + DoorHeight * .5f,
                        DoorCenter + side * (DoorWidth * .5f + .1f)),
                    new Vector3(liningDepth, DoorHeight, .2f), frame);
                Box("High doorway steel frame", new Vector3(frameX, ClimbDoorHeight + (DoorHeight - frameDrop) * .5f,
                        DoorCenter + side * (DoorWidth * .5f + .11f)),
                    new Vector3(frameDepth, DoorHeight + frameDrop, .22f), frame, false);
            }
            Box("High doorway ceiling", new Vector3(centerX, ClimbDoorHeight + DoorHeight + .1f, DoorCenter),
                new Vector3(liningDepth, .2f, DoorWidth + .4f), frame);
            Box("High doorway back wall", new Vector3(ClimbWallX + ClimbLandingDepth + .1f,
                    ClimbDoorHeight + DoorHeight * .5f, DoorCenter),
                new Vector3(.2f, DoorHeight, DoorWidth + .4f), frame);
            Box("High doorway steel header", new Vector3(frameX, ClimbDoorHeight + DoorHeight + .13f, DoorCenter),
                new Vector3(frameDepth, .26f, DoorWidth + .44f), frame, false);
            Box("High doorway sill marker", new Vector3(ClimbWallX - .025f, ClimbDoorHeight - .05f, DoorCenter),
                new Vector3(.025f, .075f, DoorWidth), cyan, false);
            Box("High doorway header marker", new Vector3(ClimbWallX - frameDepth - .01f, ClimbDoorHeight + DoorHeight + .13f, DoorCenter),
                new Vector3(.025f, .09f, 1.12f), cyan, false);
        }

        void EnterClimbRoom()
        {
            ClearLandingFeedback();
            if (Held != null) interaction.Cancel(true);
            if (Aimed != null) Aimed.Highlight(false, false);
            Aimed = null;
            inClimbRoom = true;
            Props = climbDice;
            motor.MantleReachFor = ClimbMantleReach;
            door.position = new Vector3(Room.max.x + .1f, DoorHeight * .5f, DoorCenter);
            BeginClimbArrival();
        }

        void TellClimbHint() => Tell("큰 주사위는 문 아래에, 낮은 주사위는 그 앞에 놓아 보세요. 모서리를 향해 앞으로 뛰면 잡고 올라가요.", 9f);

        void BeginClimbArrival()
        {
            foreach (var prop in climbDice)
                SuspendClimbDie(prop);
            nextClimbDrop = 0;
            climbDropDelay = .35f;
            climbArrivalPlaying = true;
            Tell("왼쪽, 오른쪽 주사위가 차례로 떨어져요. 착지할 때 소리와 진동을 비교해 보세요.", 7f);
        }

        void SuspendClimbDie(GalleryProp prop)
        {
            prop.Restore();
            prop.transform.rotation = prop.homeRotation * Quaternion.Euler(7f, 0, -5f);
            float topOffset = prop.WorldBounds.max.y - prop.transform.position.y;
            var position = prop.homePosition;
            position.y = ClimbRoom.max.y - topOffset - .12f;
            prop.transform.position = position;
            prop.body.position = position;
            prop.body.rotation = prop.transform.rotation;
            Physics.SyncTransforms();
            prop.placement.ResetPlaced();
        }

        void TickClimbArrival(float dt)
        {
            if (!climbArrivalPlaying || paused) return;
            if (nextClimbDrop == 1 && climbDice[0].GetComponent<TutorialLandingFeedback>().AwaitingLanding) return;
            if (nextClimbDrop == climbDice.Length && !climbDice[nextClimbDrop - 1].placement.Ready) return;
            climbDropDelay -= dt;
            if (climbDropDelay > 0f) return;
            if (nextClimbDrop == climbDice.Length)
            {
                climbArrivalPlaying = false;
                TellClimbHint();
                return;
            }

            var prop = climbDice[nextClimbDrop++];
            prop.Release();
            prop.GetComponent<TutorialLandingFeedback>().RecordPlacementResult(true, prop.transform.localScale.x);
            // Keep the shared size-dependent contact sound audible from the entrance.
            var audio = prop.GetComponent<AudioSource>();
            audio.minDistance = nextClimbDrop == 1 ? 6f : 8f;
            audio.maxDistance = 45f;
            climbDropDelay = .2f;
        }

        float ClimbMantleReach(Collider shape)
        {
            if (shape == climbDoorFace || shape == climbDoorLanding) return ClimbMantleHeight;
            foreach (var die in climbDice)
                if (shape.attachedRigidbody == die.body && die.placement.Ready) return ClimbMantleHeight;
            return 0f;
        }

        void ResetClimbRoom()
        {
            ClearLandingFeedback();
            if (Held != null) interaction.Cancel(true);
            if (Aimed != null) Aimed.Highlight(false, false);
            Aimed = null;
            climbComplete = false;
            Controller.enabled = false;
            Controller.transform.position = ClimbSpawn;
            Controller.enabled = true;
            motor.Reset();
            controls.Reset();
            BeginClimbArrival();
        }

        void FixedUpdate()
        {
            if (!inClimbRoom) return;
            foreach (var prop in climbDice)
                if (prop.gameObject.activeSelf) prop.placement.Tick(Time.fixedDeltaTime);
            TickClimbArrival(Time.fixedDeltaTime);
        }
    }
}
