using UnityEngine;
using ShadeLink.Gallery;

namespace ShadeLink.Tutorial
{
    public sealed class TutorialGame : MonoBehaviour, IObjectInteractionHost<GalleryProp>
    {
        const float StartSize = .36f;
        const int LessonCount = 3;
        const float DoorWidth = 2.4f;
        const float DoorHeight = 3f;
        const float PassageDepth = 2.6f;
        const float DoorCenter = 4.5f;
        const float MintTargetSize = 12.5f;
        const float RoomHeight = 16f;
        const float TableTargetSize = .70f;
        public const float TableHeight = .82f;
        static readonly Vector3 TablePosition = new Vector3(1.35f, 0, 2.35f);
        static readonly Vector3 TableHome = TablePosition + Vector3.up * (TableHeight + StartSize * .5f);
        static readonly Color Accent = new Color(1f, .58f, .18f);
        static readonly Color TableAccent = new Color(.97f, .83f, .32f);
        static readonly Color Mint = new Color(.42f, .96f, .73f);
        static readonly Color Reference = new Color(.48f, .92f, 1f);
        static readonly Bounds Room = new Bounds(new Vector3(0, RoomHeight * .5f, 14.6f), new Vector3(32, RoomHeight, 34.8f));

        [SerializeField] Shader surfaceShader;
        [SerializeField] Shader guideShader;
        [SerializeField] Shader concreteShader;

        readonly FirstPersonMotor motor = new FirstPersonMotor();
        readonly PlayerControls controls = new PlayerControls();
        ObjectInteraction<GalleryProp> interaction;
        GalleryProp die;
        Material surface, white, dark, orange, cyan;
        Transform primaryTarget, tabletopTarget, secondaryTarget;
        Collider originalTable;
        GameObject firstSign, secondSign, shrinkSign;
        Material signInk;
        Transform door;
        Renderer doorLamp;
        Material unlockedLamp;
        bool exitUnlocked;
        Font font;
        Vector2 referenceViewportSize, firstReferenceViewportSize;
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
            ? new Vector3(0, 2.32f, Room.max.z)
            : new Vector3(0, MintTargetSize * .5f, Room.max.z);

        void Start()
        {
            var shader = surfaceShader;
            surface = new Material(concreteShader) { color = new Color(.69f, .67f, .63f) };
            white = new Material(shader) { color = new Color(.95f, .92f, .84f) };
            dark = new Material(shader) { color = new Color(.13f, .16f, .19f) };
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
            Camera.backgroundColor = surface.color;
            FirstPersonMotor.Configure(Controller, Camera);
            PlayerControls.ConfigureCamera(Camera);

            originalTable = BuildTable().GetComponentInChildren<Collider>();
            die = Box("Practice die", TableHome, Vector3.one * StartSize, white).AddComponent<GalleryProp>();
            AddPips(die.transform);
            die.Remember();
            die.InitializePhysics();
            die.placement.PreviewUntilSettled = false;
            Props = new[] { die };
            interaction = new ObjectInteraction<GalleryProp>(this);
            primaryTarget = WallTarget("First wall target", orange, new Vector3(0, 2.32f, Room.max.z), 4.65f);
            tabletopTarget = Outline("Tabletop target", new Material(guideShader) { color = TableAccent });
            tabletopTarget.position = TablePosition + Vector3.up * TableHeight;
            tabletopTarget.localScale = new Vector3(TableTargetSize, 1, TableTargetSize);
            secondaryTarget = WallTarget("Mint wall target", cyan, new Vector3(0, MintTargetSize * .5f, Room.max.z), MintTargetSize);
            // Keep the shared lower edge behind the orange outline so it does not flicker.
            secondaryTarget.position += Vector3.forward * .01f;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 28);
            BuildSigns();
            BuildDistanceMarks();
            ResetStep();
            SetPaused(false);
        }

        void BuildRoom()
        {
            Vector3 min = Room.min;
            Vector3 max = Room.max;
            float length = Room.size.z;
            Box("Concrete floor", new Vector3(0, -.25f, Room.center.z), new Vector3(Room.size.x, .5f, length), surface);
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
                for (float z = 2; z < max.z; z += 4)
                {
                    if (outward > 0 && Mathf.Abs(z - DoorCenter) < DoorWidth * .5f) continue;
                    Box("Wall formwork joint", new Vector3(x - outward * .004f, Room.center.y, z),
                        new Vector3(.006f, RoomHeight - .02f, .026f), dark, false);
                }
            }
            foreach (float z in new[] { min.z, max.z })
            {
                float outward = Mathf.Sign(z - Room.center.z);
                Box("Concrete target wall", new Vector3(0, Room.center.y, z + outward * .2f), new Vector3(Room.size.x, RoomHeight, .4f), surface);
                for (int x = -12; x <= 12; x += 4)
                    Box("Wall formwork joint", new Vector3(x, Room.center.y, z - outward * .004f),
                        new Vector3(.024f, RoomHeight - .02f, .006f), dark, false);
            }
            Box("Ceiling", new Vector3(0, RoomHeight + .2f, Room.center.z), new Vector3(Room.size.x, .4f, length), surface);
            for (int z = 0; z < max.z; z += 4)
                Box("Floor expansion joint", new Vector3(0, .002f, z), new Vector3(Room.size.x - .1f, .004f, .021f), dark, false);
            for (int x = -12; x <= 12; x += 4)
                Box("Longitudinal floor joint", new Vector3(x, .002f, Room.center.z), new Vector3(.02f, .004f, length - .03f), dark, false);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.86f, .86f, .86f);

            var diffuser = new Material(guideShader) { color = new Color(.68f, .69f, .67f) };
            var fixture = Box("Central ceiling light", new Vector3(0, RoomHeight - .10f, Room.center.z),
                new Vector3(8f, .14f, 2.2f), diffuser, false);
            var fill = new GameObject("Diffuse room light").AddComponent<Light>();
            fill.transform.SetParent(fixture.transform, false);
            fill.transform.localRotation = Quaternion.Euler(90, 0, 0);
            fill.type = LightType.Directional;
            fill.intensity = .46f;
            fill.shadows = LightShadows.None;
        }

        void SideWallSection(float x, float from, float to)
        {
            float outward = Mathf.Sign(x);
            float z = (from + to) * .5f;
            Box("Concrete side wall", new Vector3(x + outward * .2f, Room.center.y, z),
                new Vector3(.4f, RoomHeight, to - from), surface);
            Box("Recessed base trim", new Vector3(x - outward * .018f, .18f, z),
                new Vector3(.034f, .36f, to - from), dark, false);
        }

        void BuildPassage()
        {
            float x = Room.max.x;
            float z = DoorCenter;
            float halfWidth = DoorWidth * .5f;
            Box("Door lintel", new Vector3(x + .2f, (Room.max.y + DoorHeight) * .5f, z),
                new Vector3(.4f, Room.max.y - DoorHeight, DoorWidth), surface);
            Box("Threshold", new Vector3(x + PassageDepth * .5f, -.25f, z),
                new Vector3(PassageDepth, .5f, DoorWidth), surface);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Passage side", new Vector3(x + PassageDepth * .5f, DoorHeight * .5f, z + side * (halfWidth + .1f)),
                    new Vector3(PassageDepth, DoorHeight, .2f), surface);
                Box("Door frame", new Vector3(x - .08f, DoorHeight * .5f, z + side * halfWidth),
                    new Vector3(.14f, DoorHeight, .12f), dark, false);
            }
            Box("Passage ceiling", new Vector3(x + PassageDepth * .5f, DoorHeight + .1f, z),
                new Vector3(PassageDepth, .2f, DoorWidth + .4f), surface);
            Box("Passage end", new Vector3(x + PassageDepth + .1f, DoorHeight * .5f, z),
                new Vector3(.2f, DoorHeight, DoorWidth + .4f), surface);
            door = Box("Sliding exit door", new Vector3(x + .1f, DoorHeight * .5f, z),
                new Vector3(.18f, DoorHeight, DoorWidth), dark).transform;
            doorLamp = Box("Door status lamp", new Vector3(x - .08f, DoorHeight + .21f, z),
                new Vector3(.13f, .12f, .78f), orange, false).GetComponent<Renderer>();
        }

        void UpdateExit(float dt)
        {
            if (!exitUnlocked || step >= LessonCount) return;

            Vector3 position = door.position;
            position.y = Mathf.MoveTowards(position.y, DoorHeight * 1.5f + .12f, dt * 3.3f);
            door.position = position;

            Vector3 feet = Controller.transform.position;
            if (feet.x < Room.max.x + .8f || Mathf.Abs(feet.z - DoorCenter) >= DoorWidth * .5f) return;

            step = LessonCount;
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

        void AddPips(Transform cube)
        {
            foreach (Vector3 face in new[] { Vector3.back, Vector3.up, Vector3.right, Vector3.forward, Vector3.down, Vector3.left })
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pip.name = "Die pip";
                pip.GetComponent<Collider>().enabled = false;
                pip.GetComponent<Renderer>().sharedMaterial = dark;
                pip.transform.SetParent(cube, false);
                pip.transform.localPosition = face * .5f;
                pip.transform.localRotation = Quaternion.FromToRotation(Vector3.up, face);
                pip.transform.localScale = new Vector3(.18f, .012f, .18f);
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

        GameObject BuildTable()
        {
            var prefab = Resources.Load<GameObject>("Tutorial/WoodenPracticeTable");
            var table = Instantiate(prefab, transform);
            table.transform.localPosition = TablePosition;
            return table;
        }

        Transform WallTarget(string name, Material material, Vector3 center, float size)
        {
            Transform outline = Outline(name, material);
            outline.position = center - Vector3.forward * .028f;
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
                "주황색 윤곽에 맞춰 내려놓으세요.", enamel, ink, clay);
            shrinkSign = BuildPlacard("02   원근 훈련", "탁자에 돌려놓아 줄이기",
                "먼 벽에 놓인 주사위를 여기서 다시 집어요.\n" +
                "그 크기로 보이던 모습을 유지한 채\n" +
                "가까운 곳으로 가져오면 실제로 작아져요.\n\n" +
                "같은 탁자의 노란 윤곽에 맞춰 놓으세요.", enamel, ink, clay);
            secondSign = BuildPlacard("03   원근 훈련", "가까이서 집어 더 키우기",
                "탁자에 가까이 다가가 주사위를 집어요.\n" +
                "같은 주사위라도 가까우면 크게 보이죠.\n" +
                "그 모습이 기준이 돼 멀리 놓으면\n" +
                "첫 번째보다 더 크게 만들 수 있어요.\n\n" +
                "든 채 앞뒤로 움직여 민트색 윤곽에 맞춰요.", enamel, ink, teal);
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

            var offset = new Vector3(-2.85f, 0, 4.30f);
            stand.localScale = Vector3.one * 1.18f;
            stand.SetLocalPositionAndRotation(offset, Quaternion.LookRotation(offset));
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
        }

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
            if (Held != null) interaction.Cancel(true);
            die.Restore();
            RefreshLesson();
            Controller.enabled = false;
            Controller.transform.position = new Vector3(0, .03f, 0);
            Controller.enabled = true;
            motor.Reset();
            controls.Reset();
            yaw = 0;
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
            motor.ClearInput();
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }

        void RestartLesson()
        {
            if (step == LessonCount)
            {
                step = 0;
                firstSize = 0;
                die.homePosition = TableHome;
                die.homeScale = Vector3.one * StartSize;
                die.homeRotation = Quaternion.identity;
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

            if (frame.GrabPressed) interaction.Interact();
        }

        void UpdateProjection()
        {
            Physics.SyncTransforms();
            interaction.ResolvePlacement(Room.min + Vector3.one * .005f, Room.max - Vector3.one * .005f);
            interaction.TryApplyPlacement();
        }

        public void StartPlacement()
        {
            referenceViewportSize = ProjectedSize(die.WorldBounds);
            die.BeginHold();
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
            bool overlaps = IntersectsPlayer(die.parts[0]);
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

            Bounds bounds = die.WorldBounds;
            float size = die.transform.localScale.x;
            if (step == 1)
            {
                float half = TableTargetSize * .5f;
                bool fitsTable = Mathf.Abs(bounds.center.x - TablePosition.x) + bounds.extents.x <= half &&
                    Mathf.Abs(bounds.center.z - TablePosition.z) + bounds.extents.z <= half;
                var down = new Ray(new Vector3(bounds.center.x, bounds.min.y + .06f, bounds.center.z), Vector3.down);
                bool onTable = originalTable.Raycast(down, out var hit, .18f) &&
                    Mathf.Abs(bounds.min.y - hit.point.y) <= .12f;
                if (!fitsTable || !onTable || size > .56f)
                {
                    Tell("탁자 근처에서 먼 주사위를 집어 탁자의 노란 윤곽 안에 놓아 보세요.");
                    return;
                }
                AdvanceLesson("탁자 가까이서 집고, 든 채 움직여 민트색 윤곽에 크기를 맞춰 보세요.", hit.point.y);
                return;
            }

            float halfGoal = step == 0 ? 4.65f * .5f : MintTargetSize * .5f + .23f;
            float wallGap = Room.max.z - bounds.max.z;
            bool fitsOutline = Mathf.Abs(bounds.center.x - GoalCenter.x) + bounds.extents.x <= halfGoal &&
                Mathf.Abs(bounds.center.y - GoalCenter.y) + bounds.extents.y <= halfGoal;
            bool sizeMatches = step == 0 ? size >= 3.7f && size <= 4.45f :
                size >= MintTargetSize * .72f && size <= MintTargetSize + .35f;
            if (wallGap < -.01f || wallGap > (step == 0 ? .43f : 1.25f))
                Tell("주사위가 목표 벽에 거의 닿도록 조준하세요.");
            else if (!fitsOutline)
                Tell("주사위 전체가 벽 윤곽 안에 들어와야 해요.");
            else if (!sizeMatches)
                Tell(step == 0 ? "벽 윤곽에 맞게 크기를 조절해 보세요." :
                    "탁자에 더 가까이 다가가 주사위를 집어 보세요.");
            else if (step == 0)
            {
                firstSize = size;
                firstReferenceViewportSize = referenceViewportSize;
                AdvanceLesson("먼 벽의 주사위를 여기서 다시 집어 노란 윤곽이 있는 탁자에 놓아 보세요.", 0);
            }
            else
            {
                exitUnlocked = true;
                doorLamp.sharedMaterial = unlockedLamp;
                messageUntil = 0;
            }
        }

        void AdvanceLesson(string instruction, float supportHeight)
        {
            die.homePosition = die.transform.position + Vector3.up * (supportHeight - die.WorldBounds.min.y);
            die.homeScale = die.transform.localScale;
            die.homeRotation = die.transform.rotation;
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
            if (step < LessonCount && !exitUnlocked)
            {
                Vector3 aim = Camera.WorldToScreenPoint(GoalCenter);
                if (aim.z > 0)
                {
                    var marker = new Rect(aim.x - 16, height - aim.y - 16, 32, 32);
                    Color previous = GUI.color;
                    GUI.color = step == 1 ? TableAccent : step == 2 ? Mint : Accent;
                    GUI.Label(marker, "◇", style);
                    GUI.color = previous;
                }
            }

            if (Held != null)
            {
                Vector2 size = Vector2.Scale(referenceViewportSize, new Vector2(width, height));
                var rect = new Rect((width - size.x) / 2, (height - size.y) / 2, size.x, size.y);
                if (step == 2)
                {
                    Vector2 first = Vector2.Scale(firstReferenceViewportSize, new Vector2(width, height));
                    var firstRect = new Rect((width - first.x) / 2, (height - first.y) / 2, first.x, first.y);
                    DrawReference(firstRect, Accent);
                }
                DrawReference(rect, Reference);
                float labelY = Mathf.Min(height - 132, rect.yMax + 6);
                string referenceLabel = step == 2 ? "주황  첫 단계에서 잡은 크기   ·   하늘색  이번에 잡은 크기" :
                    "점선은 잡을 때 화면 크기";
                GUI.Label(new Rect(0, labelY, width, 30), referenceLabel, style);
                GUI.Label(new Rect(0, labelY + 28, width, 34),
                    $"실제 한 변 {interaction.ReferenceScale:0.00} → {die.transform.localScale.x:0.00} m", style);
            }
            PlayerControls.DrawCursor(new Vector2(width / 2, height / 2), Aimed != null, Held != null, false, Accent, Color.white);
            string notice = Time.time < messageUntil ? message :
                step == LessonCount ? "튜토리얼 완료  ·  계속 연습하거나 R로 다시 시작" :
                exitUnlocked && step == 2 ? $"같은 벽에서 {firstSize:0.00} m → {die.transform.localScale.x:0.00} m  ·  오른쪽 벽의 문으로 이동하세요." :
                exitUnlocked ? "오른쪽 벽에 초록불이 켜진 문이 열렸어요." : null;
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
