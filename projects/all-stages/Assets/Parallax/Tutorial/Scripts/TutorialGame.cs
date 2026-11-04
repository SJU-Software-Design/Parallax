using UnityEngine;
using ShadeLink.Gallery;

namespace ShadeLink.Tutorial
{
    public sealed partial class TutorialGame : MonoBehaviour, IObjectInteractionHost<GalleryProp>
    {
        const float StartSize = .36f;
        const int LessonCount = 3;
        const float DoorWidth = 2.4f;
        const float DoorHeight = 3f;
        const float DoorWallThickness = .4f;
        const float PassageDepth = 2.6f;
        const float DoorCenter = 4.5f;
        const float OrangeTargetSize = 4.65f;
        const float MintTargetSize = 10f;
        const float OrangeMinimumSize = 3.7f;
        const float ShrinkMaximumSize = .56f;
        const float MintMinimumSize = 8.5f;
        const float MintMaximumSize = 10.35f;
        const float RoomWidth = 32f;
        const float RoomHeight = 16f;
        const float TableTargetSize = .70f;
        public const float TableHeight = .82f;
        static readonly float LayoutShiftX = -16f / 3f - 4.8f;
        static readonly Vector3 PlayerStart = new Vector3(-16f / 3f, .03f, 0);
        static readonly Vector3 BoardPosition = new Vector3(-.8f + LayoutShiftX, 0, 4.3f);
        static readonly Vector3 TablePosition = new Vector3(3.35f + LayoutShiftX, 0, 2.35f);
        static readonly Vector3 TableHome = TablePosition + Vector3.up * (TableHeight + StartSize * .5f);
        static readonly Color Accent = new Color(1f, .58f, .18f);
        static readonly Color TableAccent = new Color(.97f, .83f, .32f);
        static readonly Color Mint = new Color(.42f, .96f, .73f);
        static readonly Color Reference = new Color(.48f, .92f, 1f);
        static readonly Color ReadyAccent = new Color(.6f, 1f, .35f);
        static readonly Bounds Room = new Bounds(new Vector3(0, RoomHeight * .5f, 14.6f), new Vector3(RoomWidth, RoomHeight, 34.8f));

        [SerializeField] Shader surfaceShader;
        [SerializeField] Shader guideShader;
        [SerializeField] Shader concreteShader;

        readonly FirstPersonMotor motor = new FirstPersonMotor();
        readonly PlayerControls controls = new PlayerControls();
        ObjectInteraction<GalleryProp> interaction;
        TutorialLandingShake landingShake;
        GalleryProp die;
        Material wallSeam, steel, paleSteel, frame, floorPlate, paleFloorPlate;
        Material white, dark, orange, cyan;
        Transform primaryTarget, tabletopTarget, secondaryTarget;
        GameObject firstSign, secondSign, shrinkSign;
        Material signInk;
        Transform door;
        Renderer doorLamp;
        Material unlockedLamp;
        bool exitUnlocked;
        Font font;
        Vector2 referenceViewportSize;
        float yaw, pitch, messageUntil, firstSize;
        int step;
        bool paused;
        string message;

        public Camera Camera { get; private set; }
        public CharacterController Controller { get; private set; }
        public GalleryProp[] Props { get; private set; }
        public GalleryProp Held { get; set; }
        public GalleryProp Aimed { get; set; }
        public int InteractionMask => (1 << 0) | (1 << HeldCollisionState.Layer);
        public bool CanInteract => !paused;

        Vector3 GoalCenter => step == 1 ? TableHome : step == 0
            ? new Vector3(0, OrangeTargetSize * .5f, Room.max.z)
            : new Vector3(0, MintTargetSize * .5f, Room.max.z);

        // Aim for the die's center just in front of the wall, where the placement ray stops.
        Vector3 AimPoint => step == 1 ? GoalCenter :
            GoalCenter + new Vector3(0, .10f, -(step == 0 ? OrangeTargetSize : MintTargetSize) * .5f - .32f);

        void Start()
        {
            BuildFinishes();
            white = new Material(surfaceShader) { color = new Color(.96f, .95f, .91f) };
            white.SetFloat("_Glossiness", .64f);
            dark = new Material(surfaceShader) { color = new Color(.13f, .16f, .19f) };
            dark.SetFloat("_Glossiness", .38f);
            orange = new Material(guideShader) { color = Accent };
            cyan = new Material(guideShader) { color = Mint };
            unlockedLamp = new Material(guideShader) { color = new Color(.32f, .95f, .56f) };
            BuildRoom();

            var player = new GameObject("Tutorial player");
            player.transform.SetParent(transform);
            Controller = player.AddComponent<CharacterController>();
            Controller.gameObject.layer = 2;
            Camera = new GameObject("Tutorial camera").AddComponent<Camera>();
            Camera.transform.SetParent(player.transform, false);
            Camera.gameObject.AddComponent<AudioListener>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = steel.color;
            FirstPersonMotor.Configure(Controller, Camera);
            PlayerControls.ConfigureCamera(Camera);
            landingShake = new TutorialLandingShake(Camera);

            BuildTable();
            die = BuildDie("Practice die", TableHome, StartSize);
            die.Remember();
            die.InitializePhysics();
            die.placement.PreviewUntilSettled = false;
            AttachLandingFeedback(die);
            Props = new[] { die };
            interaction = new ObjectInteraction<GalleryProp>(this);
            primaryTarget = WallTarget("First wall target", orange, new Vector3(0, OrangeTargetSize * .5f, Room.max.z), OrangeTargetSize);
            tabletopTarget = Outline("Tabletop target", new Material(guideShader) { color = TableAccent });
            tabletopTarget.position = TablePosition + Vector3.up * TableHeight;
            tabletopTarget.localScale = new Vector3(TableTargetSize, 1, TableTargetSize);
            secondaryTarget = WallTarget("Mint wall target", cyan, new Vector3(0, MintTargetSize * .5f, Room.max.z), MintTargetSize);
            // Keep the shared lower edge behind the orange outline so it does not flicker.
            secondaryTarget.position += Vector3.forward * .01f;
            var installedFonts = Font.GetOSInstalledFontNames();
            var availableFonts = new System.Collections.Generic.List<string>();
            foreach (string preferred in new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" })
                if (System.Array.Exists(installedFonts, name => string.Equals(name, preferred, System.StringComparison.OrdinalIgnoreCase)))
                    availableFonts.Add(preferred);
            font = availableFonts.Count > 0
                ? Font.CreateDynamicFontFromOSFont(availableFonts.ToArray(), 28)
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildSigns();
            BuildDistanceMarks();
            BuildClimbRoom();
            ResetStep();
            SetPaused(false);
        }

        void BuildRoom()
        {
            Vector3 min = Room.min;
            Vector3 max = Room.max;
            float length = Room.size.z;
            BuildFloor(Room, "Training room floor");
            foreach (float x in new[] { min.x, max.x })
            {
                float outward = Mathf.Sign(x);
                if (outward < 0)
                    SideWallSection(x, min.z, max.z);
                else
                {
                    float halfDoor = DoorWidth * .5f;
                    SideWallSection(x, min.z, DoorCenter - halfDoor);
                    SideWallSection(x, DoorCenter + halfDoor, max.z);
                    BuildPassage();
                }
                Box("Safety stripe", new Vector3(x - outward * .5f, .009f, Room.center.z),
                    new Vector3(.075f, .012f, length - .5f), orange, false);
            }
            foreach (float z in new[] { min.z, max.z })
            {
                float outward = Mathf.Sign(z - Room.center.z);
                Box("Training end wall", new Vector3(0, Room.center.y, z + outward * .2f),
                    new Vector3(Room.size.x, RoomHeight, .4f), wallSeam);
                BuildWallPanels(new Vector3(0, 0, z), -outward * Vector3.forward, Room.size.x, RoomHeight);
            }
            Box("Training room ceiling", new Vector3(0, RoomHeight + .2f, Room.center.z),
                new Vector3(Room.size.x, .4f, length), frame);
            BuildRoomLights(Room, Vector3.forward);
        }

        void SideWallSection(float x, float from, float to)
        {
            float outward = Mathf.Sign(x);
            float z = (from + to) * .5f;
            Box("Training side wall", new Vector3(x + outward * DoorWallThickness * .5f, Room.center.y, z),
                new Vector3(DoorWallThickness, RoomHeight, to - from), wallSeam);
            BuildWallPanels(new Vector3(x, 0, z), -outward * Vector3.right, to - from, RoomHeight);
        }

        void BuildPassage()
        {
            float x = Room.max.x;
            float z = DoorCenter;
            float halfWidth = DoorWidth * .5f;
            float center = x + PassageDepth * .5f;
            // The two wall openings already form the ends of the passage.
            float liningDepth = PassageDepth - 2f * DoorWallThickness;
            Box("Door lintel", new Vector3(x + DoorWallThickness * .5f, (Room.max.y + DoorHeight) * .5f, z),
                new Vector3(DoorWallThickness, Room.max.y - DoorHeight, DoorWidth), steel);
            Box("Threshold", new Vector3(center, -.25f, z),
                new Vector3(PassageDepth, .5f, DoorWidth), floorPlate);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Passage side", new Vector3(center, DoorHeight * .5f, z + side * (halfWidth + .1f)),
                    new Vector3(liningDepth, DoorHeight, .2f), steel);
                Box("Door frame", new Vector3(x - .08f, DoorHeight * .5f, z + side * halfWidth),
                    new Vector3(.14f, DoorHeight, .12f), frame, false);
            }
            Box("Passage ceiling", new Vector3(center, DoorHeight + .1f, z),
                new Vector3(liningDepth, .2f, DoorWidth + .4f), frame);
            door = Box("Sliding exit door", new Vector3(x + .1f, DoorHeight * .5f, z),
                new Vector3(.18f, DoorHeight, DoorWidth), frame).transform;
            doorLamp = Box("Door status lamp", new Vector3(x - .08f, DoorHeight + .21f, z),
                new Vector3(.13f, .12f, .78f), orange, false).GetComponent<Renderer>();
        }

        void UpdateExit(float dt)
        {
            Vector3 feet = Controller.transform.position;
            if (inClimbRoom)
            {
                if (feet.x > ClimbRoom.max.x + .65f && feet.y >= ClimbDoorHeight - .05f &&
                    Mathf.Abs(feet.z - DoorCenter) < DoorWidth * .5f)
                    climbComplete = true;
                return;
            }
            if (!exitUnlocked) return;

            Vector3 position = door.position;
            position.y = Mathf.MoveTowards(position.y, DoorHeight * 1.5f + .12f, dt * 3.3f);
            door.position = position;

            if (feet.x < ClimbEntryX + .55f) return;

            step = LessonCount;
            EnterClimbRoom();
        }

        GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(transform);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            box.GetComponent<Collider>().enabled = solid;
            return box;
        }

        GalleryProp BuildDie(string name, Vector3 position, float side)
        {
            var root = new GameObject(name);
            var cube = root.transform;
            cube.SetParent(transform, false);
            cube.localPosition = position;
            cube.localScale = Vector3.one * side;
            root.AddComponent<BoxCollider>();
            var shell = GalleryConceptArchitecture.Block(cube, "Polished die shell", Vector3.zero, Vector3.one, white, .036f);
            shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            shell.receiveShadows = true;
            AddPips(cube);
            return root.AddComponent<GalleryProp>();
        }

        void AddPips(Transform cube)
        {
            var faces = new[]
            {
                (normal: Vector3.up, value: 1),
                (normal: Vector3.down, value: 6),
                (normal: Vector3.forward, value: 2),
                (normal: Vector3.back, value: 5),
                (normal: Vector3.right, value: 3),
                (normal: Vector3.left, value: 4)
            };

            foreach (var face in faces)
            {
                Vector3 axisU = Vector3.Cross(face.normal, Vector3.up);
                if (axisU.sqrMagnitude < .001f) axisU = Vector3.right;
                axisU.Normalize();
                Vector3 axisV = Vector3.Cross(face.normal, axisU).normalized;

                foreach (Vector2 point in PipLayout(face.value))
                {
                    var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    pip.name = $"Die pip {face.value}";
                    pip.GetComponent<Collider>().enabled = false;
                    pip.GetComponent<Renderer>().sharedMaterial = dark;
                    pip.transform.SetParent(cube, false);
                    pip.transform.localPosition = face.normal * .5f + axisU * point.x + axisV * point.y;
                    pip.transform.localRotation = Quaternion.FromToRotation(Vector3.up, face.normal);
                    pip.transform.localScale = new Vector3(.18f, .012f, .18f);
                }
            }
        }

        static Vector2[] PipLayout(int value)
        {
            const float offset = .22f;
            switch (value)
            {
                case 1: return new[] { Vector2.zero };
                case 2: return new[] { new Vector2(-offset, -offset), new Vector2(offset, offset) };
                case 3: return new[] { new Vector2(-offset, -offset), Vector2.zero, new Vector2(offset, offset) };
                case 4: return new[]
                {
                    new Vector2(-offset, -offset), new Vector2(-offset, offset),
                    new Vector2(offset, -offset), new Vector2(offset, offset)
                };
                case 5: return new[]
                {
                    new Vector2(-offset, -offset), new Vector2(-offset, offset), Vector2.zero,
                    new Vector2(offset, -offset), new Vector2(offset, offset)
                };
                case 6: return new[]
                {
                    new Vector2(-offset, -offset), new Vector2(-offset, 0), new Vector2(-offset, offset),
                    new Vector2(offset, -offset), new Vector2(offset, 0), new Vector2(offset, offset)
                };
                default: return new[] { Vector2.zero };
            }
        }

        Transform Outline(string name, Material material)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform);
            for (int side = 0; side < 4; side++)
            {
                var angle = Quaternion.Euler(0, 90 * side, 0);
                var edge = Box(name + " edge", angle * new Vector3(0, .014f, .5f),
                    new Vector3(1, .018f, .035f), material, false);
                edge.transform.SetParent(root, false);
                edge.transform.localRotation = angle;
            }
            return root;
        }

        void BuildTable()
        {
            var prefab = Resources.Load<GameObject>("Tutorial/WoodenPracticeTable");
            var table = Instantiate(prefab, transform);
            table.transform.localPosition = TablePosition;
        }

        Transform WallTarget(string name, Material material, Vector3 center, float size)
        {
            Transform outline = Outline(name, material);
            outline.position = center - Vector3.forward * .082f;
            outline.rotation = Quaternion.Euler(-90, 0, 0);
            outline.localScale = new Vector3(size, 1, size);
            return outline;
        }

        void BuildDistanceMarks()
        {
            var paint = new Material(concreteShader) { color = new Color(.53f, .51f, .48f) };
            var ink = new Color(.22f, .23f, .24f);
            for (int distance = 5; distance <= 30; distance += 5)
            {
                Box("Range floor line", new Vector3(0, .004f, distance),
                    new Vector3(18f, .004f, .052f), paint, false);
                var label = SignText(transform, "Range " + distance,
                    new Vector3(-3.9f, .58f, distance), .053f, ink);
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.fontStyle = FontStyle.Bold;
                label.text = $"{distance} m";
            }
        }

        void BuildSigns()
        {
            signInk = new Material(Shader.Find("ShadeLink/WorldText"));
            signInk.mainTexture = font.material.mainTexture;
            Font.textureRebuilt += RefreshSignFont;

            var enamel = new Material(surfaceShader) { color = new Color(.88f, .87f, .82f) };
            enamel.SetFloat("_Glossiness", .12f);
            var ink = new Color(.035f, .045f, .055f);
            var clay = new Color(.44f, .19f, .075f);
            var teal = new Color(.06f, .32f, .37f);

            firstSign = BuildPlacard("01   원근 훈련", "멀리 놓아 키우기",
                "탁자 위 주사위를 집고 먼 벽을 보세요.\n" +
                "집을 때 화면에 보인 크기가 기준이에요.\n" +
                "멀리서도 그만큼 크게 보이려면\n" +
                "실제 주사위가 더 커져야 해요.\n\n" +
                "주황색 윤곽을 참고해 크기를 맞춰요.", enamel, ink, clay);
            shrinkSign = BuildPlacard("02   원근 훈련", "탁자에 돌려놓아 줄이기",
                "키워 놓은 주사위를 멀리서 다시 집어요.\n" +
                "그 크기로 보이던 모습을 유지한 채\n" +
                "가까운 곳으로 가져오면 실제로 작아져요.\n\n" +
                "가까운 탁자 위에 놓아 작게 만들어 봐요.", enamel, ink, clay);
            secondSign = BuildPlacard("03   원근 훈련", "가까이서 집어 더 키우기",
                "탁자에 가까이 다가가 주사위를 집어요.\n" +
                "같은 주사위라도 가까우면 크게 보이죠.\n" +
                "그 모습이 기준이 돼 멀리 놓으면\n" +
                "첫 번째보다 더 크게 만들 수 있어요.\n\n" +
                "민트색 윤곽을 참고해 크기를 맞춰요.", enamel, ink, teal);
        }

        GameObject BuildPlacard(string station, string title, string instructions,
            Material enamel, Color ink, Color accent)
        {
            var stand = new GameObject(station).transform;
            stand.SetParent(transform, false);
            Box("Enamel panel", new Vector3(0, 1.54f, 0), new Vector3(2.42f, 1.46f, .075f),
                enamel, false).transform.SetParent(stand, false);
            Box("Panel rule", new Vector3(0, 1.89f, -.041f), new Vector3(2.12f, .009f, .006f),
                dark, false).transform.SetParent(stand, false);
            foreach (float x in new[] { -.88f, .88f })
            {
                Box("Steel upright", new Vector3(x, .44f, .015f), new Vector3(.065f, .88f, .065f),
                    dark, false).transform.SetParent(stand, false);
                Box("Steel foot", new Vector3(x, .024f, .015f), new Vector3(.25f, .048f, .31f),
                    dark, false).transform.SetParent(stand, false);
            }

            var number = SignText(stand, "Station number", new Vector3(-1.06f, 2.16f, -.047f), .0105f, accent);
            number.fontStyle = FontStyle.Bold;
            number.text = station;
            var heading = SignText(stand, "Lesson title", new Vector3(-1.06f, 2.05f, -.047f), .0145f, ink);
            heading.fontStyle = FontStyle.Bold;
            heading.text = title;
            var body = SignText(stand, "Lesson instructions", new Vector3(-1.06f, 1.82f, -.047f), .0115f, ink);
            body.fontStyle = FontStyle.Bold;
            body.text = instructions;

            var offset = BoardPosition;
            stand.localScale = Vector3.one * 1.18f;
            Vector3 boardFacing = offset - new Vector3(PlayerStart.x, 0, PlayerStart.z);
            stand.SetLocalPositionAndRotation(offset, Quaternion.LookRotation(boardFacing));
            return stand.gameObject;
        }

        TextMesh SignText(Transform stand, string name, Vector3 position, float characterSize, Color color)
        {
            var label = new GameObject(name).AddComponent<TextMesh>();
            label.transform.SetParent(stand, false);
            label.transform.localPosition = position;
            label.font = font;
            label.fontSize = 96;
            label.characterSize = characterSize;
            label.anchor = TextAnchor.UpperLeft;
            label.alignment = TextAlignment.Left;
            label.color = color;
            label.GetComponent<Renderer>().sharedMaterial = signInk;
            return label;
        }

        void RefreshSignFont(Font rebuilt)
        {
            if (rebuilt == font) signInk.mainTexture = font.material.mainTexture;
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= RefreshSignFont;
            if (signInk != null) Destroy(signInk);
            landingShake?.Dispose();
        }

        void OnDisable() => ClearLandingFeedback();

        void RefreshLesson()
        {
            firstSign.SetActive(step == 0);
            shrinkSign.SetActive(step == 1);
            secondSign.SetActive(step >= 2);
            tabletopTarget.gameObject.SetActive(step >= 1);
            secondaryTarget.gameObject.SetActive(step >= 2);
        }

        void ResetStep()
        {
            ClearLandingFeedback();
            if (Held != null) interaction.Cancel(true);
            die.Restore();
            RefreshLesson();
            Controller.enabled = false;
            Controller.transform.position = PlayerStart;
            Controller.enabled = true;
            motor.Reset();
            controls.Reset();
            yaw = -.36f;
            pitch = -.23f;
            SyncView();
            messageUntil = 0;
            exitUnlocked = false;
            door.position = new Vector3(Room.max.x + .1f, DoorHeight * .5f, DoorCenter);
            doorLamp.sharedMaterial = orange;
        }

        void SyncView() => Camera.transform.rotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0);

        void SetPaused(bool value)
        {
            paused = value;
            if (paused) landingShake?.Clear();
            motor.ClearInput();
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }

        void RestartLesson()
        {
            if (inClimbRoom)
            {
                ResetClimbRoom();
                return;
            }
            ResetStep();
        }

        void Update()
        {
            if (PlayerControls.PausePressed) { SetPaused(!paused); return; }
            if (paused) return;
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartLesson();
                return;
            }

            var frame = controls.Read(Time.deltaTime, Aimed != null, Held != null);
            if (Held != null && frame.RotateHeld)
            {
                Vector2 rotation = PlayerControls.ObjectRotationDegrees(frame.Look);
                interaction.Rotation = Quaternion.AngleAxis(rotation.x, Vector3.up) * interaction.Rotation *
                    Quaternion.AngleAxis(rotation.y, Vector3.right);
            }
            else PlayerControls.ApplyLook(ref yaw, ref pitch, frame.Look);
            motor.Step(Controller, frame.Move, yaw, frame.JumpPressed, frame.JumpHeld, Time.deltaTime);
            SyncView();
            UpdateExit(Time.deltaTime);
            if (!CanInteract) return;

            if (Held != null)
            {
                if (frame.CancelPressed) { interaction.Cancel(); return; }
                interaction.Advance(Time.deltaTime);
                UpdateProjection();
            }
            else interaction.UpdateAim();

            if (frame.GrabPressed) InteractWithLandingFeedback();
        }

        void InteractWithLandingFeedback()
        {
            var prop = Held;
            if (prop == null)
            {
                interaction.Interact();
                return;
            }

            bool placed = interaction.Drop();
            prop.GetComponent<TutorialLandingFeedback>().RecordPlacementResult(placed, prop.transform.localScale.x);
        }

        void AttachLandingFeedback(GalleryProp prop)
        {
            prop.gameObject.AddComponent<TutorialLandingFeedback>().Configure(PlayLandingShake);
        }

        void PlayLandingShake(float pitch)
        {
            if (!paused && isActiveAndEnabled) landingShake.Play(pitch, Time.unscaledTime);
        }

        void ClearLandingFeedback()
        {
            landingShake?.Clear();
            if (Props == null) return;
            foreach (var prop in Props)
                if (prop != null) prop.GetComponent<TutorialLandingFeedback>()?.ClearPending();
        }

        void UpdateProjection()
        {
            Physics.SyncTransforms();
            Bounds bounds = inClimbRoom ? ClimbRoom : Room;
            interaction.ResolvePlacement(bounds.min + Vector3.one * .005f, bounds.max - Vector3.one * .005f);
            interaction.TryApplyPlacement();
        }

        public void StartPlacement()
        {
            Held.GetComponent<TutorialLandingFeedback>().ClearPending();
            referenceViewportSize = ProjectedSize(Held.WorldBounds);
            Held.BeginHold();
        }

        Vector2 ProjectedSize(Bounds bounds)
        {
            Vector2 min = Vector2.one * float.PositiveInfinity;
            Vector2 max = Vector2.one * float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 point = Camera.WorldToViewportPoint(corner);
                if (point.z <= Camera.nearClipPlane) return Vector2.one * .9f;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return max - min;
        }

        public void PrepareRelease() => UpdateProjection();

        public bool ValidatePreview(out string reason)
        {
            reason = null;
            return true;
        }

        public bool ValidateRelease(out string reason)
        {
            bool overlaps = Held != null && IntersectsPlayer(Held.parts[0]);
            reason = overlaps ? "몸에서 조금 떨어진 곳에 놓으세요." : null;
            return !overlaps;
        }

        public bool IntersectsPlayer(BoxCollider part)
        {
            return Physics.ComputePenetration(part, part.transform.position, part.transform.rotation,
                Controller, Controller.transform.position, Controller.transform.rotation, out _, out _);
        }

        public void FinishInteraction(bool released)
        {
            if (!released || exitUnlocked || step >= LessonCount) return;

            float size = die.transform.localScale.x;
            string feedback = SizeFeedback(size);
            if (feedback != null)
            {
                Tell(feedback);
                return;
            }

            if (step == 0)
            {
                firstSize = size;
                var wallCheckpoint = new Vector3(0, size * .5f, Room.max.z - size * .5f - .32f);
                AdvanceLesson(wallCheckpoint, size, "키운 주사위를 멀리서 다시 집어 가까운 탁자에 놓아 보세요.");
            }
            else if (step == 1)
            {
                AdvanceLesson(TableHome, StartSize, "주사위를 가까이서 집고 먼 벽에 더 크게 놓아 보세요.");
            }
            else
            {
                exitUnlocked = true;
                doorLamp.sharedMaterial = unlockedLamp;
                messageUntil = 0;
            }
        }

        string SizeFeedback(float size)
        {
            if (step == 1)
                return size > ShrinkMaximumSize ? "주사위를 더 가까운 곳에 놓아 작게 만들어 보세요." : null;
            if (size > (step == 0 ? OrangeTargetSize : MintMaximumSize))
                return "주사위가 커요. 든 채 벽 쪽으로 조금 다가가 보세요.";
            if (size < (step == 0 ? OrangeMinimumSize : MintMinimumSize))
                return "주사위가 작아요. 더 가까이서 집거나, 든 채 벽에서 조금 물러나 보세요.";

            return null;
        }

        void AdvanceLesson(Vector3 checkpoint, float checkpointSize, string instruction)
        {
            // A retry starts from the intended practice layout, even after an off-target drop.
            die.homePosition = checkpoint;
            die.homeScale = Vector3.one * checkpointSize;
            die.homeRotation = Quaternion.identity;
            step++;
            RefreshLesson();
            Tell(instruction, 8);
        }

        public void AimChanged(RaycastHit hit) { }
        public bool TrySpecialInteraction() => false;
        public void CancelSpecialInteraction() { }

        public void Tell(string text, float duration = 4)
        {
            message = text;
            messageUntil = Time.time + duration;
        }

        void OnGUI()
        {
            if (Camera == null) return;
            float width = Screen.width;
            float height = Screen.height;
            var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            bool sizeReady = Held != null && step < LessonCount && !exitUnlocked &&
                interaction.ProjectionApplied && interaction.ReleaseRestriction == null &&
                SizeFeedback(die.transform.localScale.x) == null;
            if (step < LessonCount && !exitUnlocked)
            {
                Vector3 aim = Camera.WorldToScreenPoint(AimPoint);
                if (aim.z > 0)
                {
                    var marker = new Rect(aim.x - 16, height - aim.y - 16, 32, 32);
                    Color previous = GUI.color;
                    GUI.color = sizeReady ? ReadyAccent : step == 1 ? TableAccent : step == 2 ? Mint : Accent;
                    GUI.Label(marker, sizeReady ? "◆" : "◇", style);
                    GUI.color = previous;
                }
            }

            if (Held != null)
            {
                Vector2 size = Vector2.Scale(referenceViewportSize, new Vector2(width, height));
                var rect = new Rect((width - size.x) / 2, (height - size.y) / 2, size.x, size.y);
                DrawReference(rect, Reference);
            }
            PlayerControls.DrawCursor(new Vector2(width / 2, height / 2), Aimed != null, Held != null, false, Accent, Color.white);
            string notice = climbComplete ? "튜토리얼 2스테이지 완료  ·  R로 발판 연습 다시 시작" :
                Time.time < messageUntil ? message :
                exitUnlocked && !inClimbRoom ? $"주사위 한 변 {firstSize:0.00} m → {die.transform.localScale.x:0.00} m  ·  오른쪽 문으로 다음 스테이지에 들어가세요." :
                null;
            if (notice != null)
            {
                float bannerWidth = Mathf.Min(width - 36, 1040);
                var banner = new Rect((width - bannerWidth) * .5f, height - 92, bannerWidth, 64);
                GUI.Box(banner, GUIContent.none);
                GUI.Label(new Rect(banner.x + 12, banner.y, banner.width - 24, banner.height), notice, style);
            }
            if (paused)
            {
                var menu = new Rect(width / 2 - 130, height / 2 - 44, 260, 88);
                GUI.Box(menu, GUIContent.none);
                if (GUI.Button(new Rect(menu.x + 15, menu.y + 15, 230, 58), "계속하기", style))
                    SetPaused(false);
            }
        }

        static void DrawReference(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            for (float x = rect.xMin; x < rect.xMax; x += 14)
            {
                float length = Mathf.Min(8, rect.xMax - x);
                GUI.DrawTexture(new Rect(x, rect.yMin, length, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x, rect.yMax, length, 2), Texture2D.whiteTexture);
            }
            for (float y = rect.yMin; y < rect.yMax; y += 14)
            {
                float length = Mathf.Min(8, rect.yMax - y);
                GUI.DrawTexture(new Rect(rect.xMin, y, 2, length), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.xMax, y, 2, length), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }
    }
}
