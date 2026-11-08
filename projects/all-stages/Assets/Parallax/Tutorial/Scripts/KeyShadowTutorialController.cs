using System;
using System.Collections;
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
        KeyShadowAutoAlign autoAlign;
        GalleryProp sculpture;
        Vector3 sculptureAnchor;
        float[] previousOverlapDepths = Array.Empty<float>();
        float[] alignmentOverlapDepths = Array.Empty<float>();
        Quaternion alignmentStartRotation;
        float yaw = -.78f;
        float pitch = .10f;
        bool paused;
        bool interactionLocked;
        GalleryProp pendingRelease;
        float successFlash;
        bool swapping;
        bool alignmentBlocked;

        public Camera Camera => view;
        public CharacterController Controller => player;
        public GalleryProp[] Props => props;
        public GalleryProp Held { get; set; }
        public GalleryProp Aimed { get; set; }
        // Use the same collision query mask as Beach, so every physical prop
        // layer remains targetable while shadow-only overlays stay excluded.
        public int InteractionMask => ShadeLink.Beach.BeachWorld.PhysicalMask;
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

            // A missing source prop must not poison every pickup attempt. Beach's
            // prop list is dense; this scene can contain optional empty slots.
            props = Array.FindAll(props, prop => prop != null);

            FirstPersonMotor.Configure(player, view);
            PlayerControls.ConfigureCamera(view);
            interaction = new ObjectInteraction<GalleryProp>(this);
            foreach (GalleryProp prop in props)
            {
                if (prop == null) continue;
                if (prop.name == "ShadowKey - Branch Sculpture Preview")
                    sculptureAnchor = prop.transform.position;
                prop.Remember();
                prop.InitializePhysics();
                if (prop.name == "ShadowKey - Branch Sculpture Preview")
                {
                    prop.placement.ShowOriginalWhileHeld = true;
                    prop.placement.PreviewUntilSettled = false;
                    sculpture = prop;
                    GameObject lamp = GameObject.Find("Only light source · modern wall lamp on right");
                    Light light = lamp != null ? lamp.GetComponent<Light>() : null;
                    if (light == null) Debug.LogError("Key-shadow alignment requires the tutorial spotlight.", this);
                    autoAlign = new KeyShadowAutoAlign(prop.transform, light != null ? light.transform : null);
                }
                prop.placement.SetPlayer(player);
            }

            SyncView();
            SetPaused(false);
        }

        bool ApplyFixedSculptureRotation(bool alignmentStep)
        {
            Quaternion previous = sculpture.transform.rotation;
            var parts = sculpture.parts;
            if (previousOverlapDepths.Length != parts.Length) previousOverlapDepths = new float[parts.Length];
            if (alignmentOverlapDepths.Length != parts.Length) alignmentOverlapDepths = new float[parts.Length];
            Physics.SyncTransforms();
            for (int i = 0; i < parts.Length; i++)
                previousOverlapDepths[i] = PlayerOverlapDepth(parts[i]);
            if (alignmentStep)
            {
                sculpture.transform.rotation = alignmentStartRotation;
                Physics.SyncTransforms();
                for (int i = 0; i < parts.Length; i++)
                    alignmentOverlapDepths[i] = PlayerOverlapDepth(parts[i]);
            }
            sculpture.transform.SetPositionAndRotation(sculptureAnchor, interaction.Rotation);
            Physics.SyncTransforms();
            for (int i = 0; i < parts.Length; i++)
            {
                float depth = PlayerOverlapDepth(parts[i]);
                bool worsensPrevious = depth > previousOverlapDepths[i] + .0001f;
                bool worsensAlignmentStart = alignmentStep && depth > alignmentOverlapDepths[i] + .0001f &&
                    depth >= previousOverlapDepths[i] - .000001f;
                if (!worsensPrevious && !worsensAlignmentStart) continue;
                sculpture.transform.rotation = previous;
                interaction.Rotation = previous;
                Physics.SyncTransforms();
                return false;
            }
            return true;
        }

        void Update()
        {
            if (interaction == null) return;
            if (PlayerControls.PausePressed) { SetPaused(!paused); return; }
            if (paused) return;
            if (Cursor.lockState != CursorLockMode.Locked) { SetPaused(true); return; }

            if (!interactionLocked && Input.GetKeyDown(KeyCode.R))
            {
                ResetPuzzle();
                return;
            }

            float dt = Time.deltaTime;
            PlayerControls.Frame frame = controls.Read(dt, Aimed != null, Held != null);
            bool manualRotation = Held == sculpture && frame.RotateHeld && frame.Look.sqrMagnitude > 0f;
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
                if (frame.CancelPressed)
                {
                    interaction.Cancel();
                    if (Held == null) autoAlign?.Reset();
                    return;
                }
                if (Held == sculpture && frame.GrabPressed)
                {
                    interaction.Drop();
                    if (Held == null) autoAlign?.Reset();
                    return;
                }
                float turn = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
                float tilt = (Input.GetKey(KeyCode.X) ? 1 : 0) - (Input.GetKey(KeyCode.Z) ? 1 : 0);
                manualRotation |= Held == sculpture && (turn != 0 || tilt != 0);
                if (turn != 0 || tilt != 0)
                    interaction.Rotation = Quaternion.AngleAxis(turn * 75f * dt, Vector3.up) *
                        interaction.Rotation * Quaternion.AngleAxis(tilt * 75f * dt, Vector3.right);

                bool autoStep = Held == sculpture && autoAlign != null && autoAlign.IsAligning && !manualRotation;
                if (Held == sculpture && autoAlign != null)
                    interaction.Rotation = autoAlign.Tick(interaction.Rotation, dt, manualRotation);
                else autoAlign?.Reset();
                bool alignmentStep = autoStep && (autoAlign.IsAligning || autoAlign.HasPendingCompletion);
                if (!autoStep && Held == sculpture && autoAlign != null && autoAlign.IsAligning)
                    alignmentStartRotation = sculpture.transform.rotation;
                interaction.Advance(dt);
                if (Held == sculpture)
                {
                    bool applied = ApplyFixedSculptureRotation(alignmentStep);
                    alignmentBlocked = !applied && alignmentStep;
                    if (!applied)
                    {
                        if (alignmentStep) autoAlign.RejectAlignmentStep(sculpture.transform.rotation);
                        else autoAlign?.Reset();
                    }
                }
                else UpdateProjection();
                if (Held == sculpture && autoAlign != null && !swapping &&
                    autoAlign.TryConsumeCompletion(out int direction, out Quaternion lightAdjustment))
                    StartCoroutine(ReplaceSculpture(direction, lightAdjustment));
            }
            else { autoAlign?.Reset(); interaction.UpdateAim(); }

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
            autoAlign?.Reset();
            if (Held != null) interaction.Cancel(true);
            foreach (GalleryProp prop in props)
                if (prop != null) prop.Restore();
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

        IEnumerator ReplaceSculpture(int direction, Quaternion lightAdjustment)
        {
            var model = Resources.Load<GameObject>("Tutorial/MatchedKey_Silver");
            var visual = sculpture != null ? sculpture.transform.Find("ShadowKey_Branch") : null;
            if (model == null || visual == null)
            {
                Debug.LogError("The matched key model or sculpture visual is missing; no replacement was made.", this);
                autoAlign?.Reset();
                yield break;
            }
            var sourceRenderer = visual.GetComponentInChildren<Renderer>();
            Material silver = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            swapping = true;
            SetInteractionLocked(true);
            float time = 0f;
            const float fadeIn = .22f, fadeOut = .55f, peak = .72f;
            while (time < fadeIn)
            {
                time += Time.deltaTime;
                successFlash = peak * Mathf.Clamp01(time / fadeIn);
                yield return null;
            }
            successFlash = peak;
            GalleryProp key = KeyShadowReplacement.Create(model, visual, direction, lightAdjustment, silver);
            if (key != null)
            {
                if (Held == sculpture) interaction.Cancel(true);
                sculpture.gameObject.SetActive(false);
                RegisterProp(key);
                key.placement.ShowOriginalWhileHeld = true;
                key.placement.PreviewUntilSettled = false;
            }
            else Debug.LogError("The silver key could not be created; the sculpture remains available.", this);
            time = 0f;
            while (time < fadeOut)
            {
                time += Time.deltaTime;
                successFlash = peak * (1f - Mathf.Clamp01(time / fadeOut));
                yield return null;
            }
            successFlash = 0f;
            swapping = false;
            SetInteractionLocked(false);
        }

        void SyncView() => view.transform.rotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0);

        void SetPaused(bool value)
        {
            paused = value;
            if (value) autoAlign?.Reset();
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
        public void PrepareRelease() { pendingRelease = Held; if (Held != sculpture) UpdateProjection(); }

        public void SetInteractionLocked(bool value) { interactionLocked = value; if (value) autoAlign?.Reset(); }

        public void PrepareRewardTransition()
        {
            if (Held != null && interaction != null) interaction.Cancel(true);
            SetInteractionLocked(true);
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

        float PlayerOverlapDepth(BoxCollider part)
        {
            return part != null && Physics.ComputePenetration(part, part.transform.position, part.transform.rotation,
                player, player.transform.position, player.transform.rotation, out _, out float depth) ? depth : 0f;
        }

        public void FinishInteraction(bool released)
        {
            GalleryProp releasedProp = pendingRelease;
            pendingRelease = null;
            if (released && releasedProp == sculpture)
            {
                PropReleasePhysics.Attach(sculpture.body).CancelPendingRelease();
                sculpture.body.isKinematic = true;
                sculpture.body.useGravity = false;
                sculpture.transform.position = sculptureAnchor;
                sculpture.body.position = sculptureAnchor;
                Physics.SyncTransforms();
            }
            autoAlign?.Reset();
            if (released) Tell("배치 완료.", 4);
            if (released && releasedProp != null) PropReleased?.Invoke(releasedProp);
        }

        public void AimChanged(RaycastHit hit) { }
        public bool TrySpecialInteraction() => false;
        public void CancelSpecialInteraction() { }

        public void Tell(string text, float duration = 4)
        {
            // Tutorial guidance text is intentionally hidden in this preview.
        }

        void OnGUI()
        {
            if (view == null) return;
            float width = Screen.width, height = Screen.height;
            if (successFlash > 0f)
            {
                Color original = GUI.color;
                GUI.color = new Color(.9f, .95f, 1f, successFlash);
                GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
                GUI.color = original;
            }
            PlayerControls.DrawCursor(new Vector2(width * .5f, height * .5f),
                Aimed != null, Held != null, false, new Color(.16f, .42f, .95f), Color.white);

#if UNITY_EDITOR
            if (sculpture != null && sculpture.gameObject.activeInHierarchy && autoAlign != null)
            {
                float angle = autoAlign.NearestTargetAngle(sculpture.transform.rotation, out int direction);
                bool held = Held == sculpture;
                string state = !held ? "Not held" : paused ? "Paused" : interactionLocked ? "Locked" :
                    autoAlign.IsAligning && alignmentBlocked ? "Blocked - move away" : autoAlign.IsAligning ? "Aligning" :
                    angle > KeyShadowAutoAlign.AllowedAngle ? "Outside range" : "Hold still";
                GUI.Box(new Rect(16, 16, 310, 100), GUIContent.none);
                GUI.Label(new Rect(26, 22, 290, 22), "Alignment check (Editor only)");
                GUI.Label(new Rect(26, 43, 290, 22), $"Nearest target: {direction + 1}   Difference: {angle:F1} deg");
                GUI.Label(new Rect(26, 64, 290, 22), $"Range: {KeyShadowAutoAlign.AllowedAngle:F0} deg   State: {state}");
                GUI.Label(new Rect(26, 85, 290, 22), $"Hold: {Mathf.Min(autoAlign.HeldTime, KeyShadowAutoAlign.RequiredHoldTime):F1} / {KeyShadowAutoAlign.RequiredHoldTime:F1} sec");
            }
#endif

            if (paused)
            {
                Rect menu = new Rect(width * .5f - 140, height * .5f - 44, 280, 88);
                GUI.Box(menu, GUIContent.none);
                if (GUI.Button(new Rect(menu.x + 15, menu.y + 15, 250, 58), "계속하기"))
                    SetPaused(false);
            }
        }
    }

}
