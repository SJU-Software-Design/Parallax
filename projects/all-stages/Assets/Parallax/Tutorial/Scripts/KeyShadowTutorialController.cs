using System;
using ShadeLink.Gallery;
using UnityEngine;

namespace ShadeLink.Tutorial
{
    /// <summary>
    /// Tutorial-only adapter that reuses Parallax's first-person and perspective
    /// object interaction systems without changing the shared gameplay code.
    /// </summary>
    public sealed class KeyShadowTutorialController : MonoBehaviour,
        IObjectInteractionHost<GalleryProp>
    {
        [SerializeField] Camera view;
        [SerializeField] CharacterController player;
        [SerializeField] GalleryProp[] props;
        [SerializeField] KeyShadowMatcher shadowMatcher;
        [SerializeField] Vector3 roomMinimum = new Vector3(-8.8f, .02f, -7.8f);
        [SerializeField] Vector3 roomMaximum = new Vector3(8.8f, 8.85f, 7.8f);

        readonly FirstPersonMotor motor = new FirstPersonMotor();
        readonly PlayerControls controls = new PlayerControls();
        ObjectInteraction<GalleryProp> interaction;
        Font uiFont;
        float yaw = -.78f;
        float pitch = .10f;
        float messageUntil;
        string message;
        bool paused;
        bool interactionLocked;
        GalleryProp pendingRelease;

        public Camera Camera => view;
        public CharacterController Controller => player;
        public GalleryProp[] Props => props;
        public GalleryProp Held { get; set; }
        public GalleryProp Aimed { get; set; }
        public int InteractionMask => (1 << 0) | (1 << HeldCollisionState.Layer);
        public bool CanInteract => !paused && !interactionLocked;
        public event Action<GalleryProp> PropReleased;

        public void Configure(Camera camera, CharacterController character,
            GalleryProp[] movableProps, KeyShadowMatcher matcher, Vector3 minimum, Vector3 maximum)
        {
            view = camera;
            player = character;
            props = movableProps;
            shadowMatcher = matcher;
            roomMinimum = minimum;
            roomMaximum = maximum;
        }

        void Start()
        {
            if (view == null || player == null || props == null)
            {
                Debug.LogError("Key-shadow tutorial player references are incomplete.", this);
                enabled = false;
                return;
            }

            FirstPersonMotor.Configure(player, view);
            PlayerControls.ConfigureCamera(view);
            interaction = new ObjectInteraction<GalleryProp>(this);
            foreach (GalleryProp prop in props)
            {
                if (prop == null) continue;
                prop.Remember();
                prop.InitializePhysics();
                prop.placement.SetPlayer(player);
            }

            uiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 22);
            SyncView();
            SetPaused(false);
            Tell("오른쪽 조명으로 왼쪽 벽의 흰 종이에 탁자와 도넛의 열쇠 그림자를 만드세요. 파란 도안은 참고용입니다. 도넛을 세워 고리 모양을 만들어 보세요.", 8);
        }

        void Update()
        {
            if (interaction == null) return;
            if (PlayerControls.PausePressed) { SetPaused(!paused); return; }
            if (paused) return;
            if (Cursor.lockState != CursorLockMode.Locked) { SetPaused(true); return; }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetPuzzle();
                return;
            }

            float dt = Time.deltaTime;
            PlayerControls.Frame frame = controls.Read(dt, Aimed != null, Held != null);
            if (Held != null && frame.RotateHeld)
            {
                Vector2 turn = PlayerControls.ObjectRotationDegrees(frame.Look);
                interaction.Rotation = Quaternion.AngleAxis(turn.x, Vector3.up) * interaction.Rotation *
                    Quaternion.AngleAxis(turn.y, Vector3.right);
            }
            else PlayerControls.ApplyLook(ref yaw, ref pitch, frame.Look);

            motor.SmoothKeyboardInput = !controls.UsingGamepad;
            interaction.PickupAssistRadius = controls.UsingGamepad ? .1f : .05f;
            motor.Step(player, frame.Move, yaw, frame.JumpPressed, frame.JumpHeld, dt);
            SyncView();

            if (interactionLocked) return;

            if (Held != null)
            {
                if (frame.CancelPressed) { interaction.Cancel(); return; }
                float turn = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
                float tilt = (Input.GetKey(KeyCode.X) ? 1 : 0) - (Input.GetKey(KeyCode.Z) ? 1 : 0);
                if (turn != 0 || tilt != 0)
                    interaction.Rotation = Quaternion.AngleAxis(turn * 75f * dt, Vector3.up) *
                        interaction.Rotation * Quaternion.AngleAxis(tilt * 75f * dt, Vector3.right);

                interaction.Advance(dt);
                UpdateProjection();
            }
            else interaction.UpdateAim();

            if (frame.GrabPressed)
            {
                if (Held == null) interaction.Interact();
                else interaction.Drop();
            }

            foreach (GalleryProp prop in props)
                if (prop != null && prop.placement != null) prop.placement.Tick(dt);
        }

        void ResetPuzzle()
        {
            if (shadowMatcher != null && shadowMatcher.Matched)
            {
                Tell("열쇠가 만들어진 뒤에는 이 장면을 다시 시작할 수 없습니다.", 4);
                return;
            }
            if (Held != null) interaction.Cancel(true);
            foreach (GalleryProp prop in props)
                if (prop != null) prop.Restore();
            shadowMatcher?.ResetMatch();
            motor.Reset();
            controls.Reset();
            player.enabled = false;
            player.transform.position = new Vector3(2.4f, .03f, -6.2f);
            player.enabled = true;
            yaw = -.78f;
            pitch = .10f;
            SyncView();
            Tell("처음 배치로 돌아왔습니다. 벽 종이에 열쇠 모양 그림자를 다시 만들어 보세요.", 4);
        }

        void SyncView() => view.transform.rotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0);

        void SetPaused(bool value)
        {
            paused = value;
            motor.ClearInput();
            controls.Reset();
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }

        void UpdateProjection()
        {
            Physics.SyncTransforms();
            interaction.ResolvePlacement(roomMinimum, roomMaximum);
            interaction.TryApplyPlacement();
        }

        public void StartPlacement() => Held.BeginHold();
        public void PrepareRelease() { pendingRelease = Held; UpdateProjection(); }

        public void SetInteractionLocked(bool value) => interactionLocked = value;

        public void PrepareRewardTransition()
        {
            if (Held != null && interaction != null) interaction.Cancel(true);
            interactionLocked = true;
        }

        public void RegisterProp(GalleryProp prop)
        {
            if (prop == null) return;
            prop.Remember();
            prop.InitializePhysics();
            prop.placement.SetPlayer(player);
            props = new[] { prop };
        }

        public bool ValidatePreview(out string reason)
        {
            reason = null;
            return true;
        }

        public bool ValidateRelease(out string reason)
        {
            bool overlaps = false;
            if (Held != null)
                foreach (BoxCollider part in Held.parts)
                    if (part != null && IntersectsPlayer(part)) { overlaps = true; break; }
            reason = overlaps ? "플레이어와 겹치지 않는 곳에 놓아 주세요." : null;
            return !overlaps;
        }

        public bool IntersectsPlayer(BoxCollider part)
        {
            return part != null && Physics.ComputePenetration(part, part.transform.position, part.transform.rotation,
                player, player.transform.position, player.transform.rotation, out _, out _);
        }

        public void FinishInteraction(bool released)
        {
            GalleryProp releasedProp = pendingRelease;
            pendingRelease = null;
            if (released)
                Tell(shadowMatcher != null && shadowMatcher.Matched
                    ? "열쇠 그림자가 완성됐습니다."
                    : "배치 완료. 빛 안에서 그림자 윤곽을 확인하세요.", 4);
            if (released && releasedProp != null) PropReleased?.Invoke(releasedProp);
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
            if (view == null) return;
            float width = Screen.width, height = Screen.height;
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 20,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            PlayerControls.DrawCursor(new Vector2(width * .5f, height * .5f),
                Aimed != null, Held != null, false, new Color(.16f, .42f, .95f), Color.white);

            string status = shadowMatcher != null && shadowMatcher.Matched
                ? "열쇠 그림자 완성"
                : shadowMatcher != null && !shadowMatcher.BothPropsAdjusted
                    ? "탁자와 도넛을 각각 움직여 그림자를 만들어 보세요"
                    : $"열쇠 실루엣 {Mathf.RoundToInt((shadowMatcher?.Coverage ?? 0) * 100)}% / 65%  ·  탁자 {Mathf.RoundToInt((shadowMatcher?.TableContribution ?? 0) * 100)}%  ·  도넛 {Mathf.RoundToInt((shadowMatcher?.DonutContribution ?? 0) * 100)}% (각 10% 이상 필요)";
            Rect header = new Rect(24, 18, width - 48, 50);
            GUI.Box(header, GUIContent.none);
            GUI.Label(header, status, style);

            if (shadowMatcher != null)
            {
                GUIStyle detailStyle = new GUIStyle(style) { fontSize = 15 };
                Rect detail = new Rect(24, 70, width - 48, 28);
                string tableShadowState = !shadowMatcher.TableReady ? "운반/안착 중" :
                    shadowMatcher.TableShadowSamples > 0 ? "종이에 그림자 있음" : "종이에 그림자 없음";
                string donutShadowState = !shadowMatcher.DonutReady ? "운반/안착 중" :
                    shadowMatcher.DonutShadowSamples > 0 ? "종이에 그림자 있음" : "종이에 그림자 없음";
                string sourceState = $"실제 그림자 · 탁자 {tableShadowState} · 도넛 {donutShadowState}";
                GUI.Label(detail, sourceState, detailStyle);
            }

            string notice = Time.time < messageUntil ? message :
                "오른쪽 조명으로 탁자와 도넛 그림자를 함께 만드세요. 전체 열쇠 실루엣은 65%, 두 물체 그림자는 각각 10% 이상이어야 합니다. 파란 열쇠는 참고 그림입니다.   왼쪽 클릭 잡기/놓기 · WASD 이동 · 마우스 시점 · Space 점프 · Q/E 회전 · 우클릭+마우스 회전 · Backspace 취소 · R 다시 시작";
            Rect banner = new Rect(24, height - 100, width - 48, 76);
            GUI.Box(banner, GUIContent.none);
            GUI.Label(new Rect(banner.x + 12, banner.y + 3, banner.width - 24, banner.height - 6), notice, style);

            if (paused)
            {
                Rect menu = new Rect(width * .5f - 140, height * .5f - 44, 280, 88);
                GUI.Box(menu, GUIContent.none);
                if (GUI.Button(new Rect(menu.x + 15, menu.y + 15, 250, 58), "계속하기", style))
                    SetPaused(false);
            }
        }
    }
}
