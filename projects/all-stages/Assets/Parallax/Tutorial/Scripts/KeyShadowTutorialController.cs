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
        GalleryProp fixedReward;
        Vector3 rewardAnchor;
        Vector3 sculptureAnchor;
        float[] previousOverlapDepths = Array.Empty<float>();
        float[] alignmentOverlapDepths = Array.Empty<float>();
        Quaternion alignmentStartRotation;
        Quaternion lastAppliedSculptureRotation;
        float yaw = -.78f;
        float pitch = .10f;
        bool paused;
        bool interactionLocked;
        GalleryProp pendingRelease;
        float successFlash;
        bool swapping;
        bool alignmentBlocked;
        AudioSource alignmentAudio;
        AudioClip alignmentDing;

        public Camera Camera => view;
        public CharacterController Controller => player;
        public GalleryProp[] Props => props;
        public GalleryProp Held { get; set; }
        public GalleryProp Aimed { get; set; }
        public string PassageTestLabel { get; private set; } = "원본 · 넓은 통로";
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
                    lastAppliedSculptureRotation = prop.transform.rotation;
                    GameObject lamp = GameObject.Find("Only light source · modern wall lamp on right");
                    Light light = lamp != null ? lamp.GetComponent<Light>() : null;
                    if (light == null) Debug.LogError("Key-shadow alignment requires the tutorial spotlight.", this);
                    var mesh = prop.GetComponentInChildren<MeshFilter>();
                    var passageSettings = Resources.Load<KeyShadowPassageSettings>(KeyShadowPassageSettings.ResourcePath);
                    if (mesh != null && passageSettings != null && passageSettings.SelectedMesh != null)
                    {
                        mesh.sharedMesh = passageSettings.SelectedMesh;
                        PassageTestLabel = passageSettings.Label;
                    }
                    autoAlign = new KeyShadowAutoAlign(prop.transform, light != null ? light.transform : null,
                        mesh != null ? mesh.transform : null);
                }
                prop.placement.SetPlayer(player);
            }

            SyncView();
            SetPaused(false);
        }

        bool ApplyFixedSculptureRotation(Quaternion requested, bool alignmentStep)
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
            Vector3 position = Held == sculpture ? sculptureAnchor : sculpture.transform.position;
            sculpture.transform.SetPositionAndRotation(position, requested);
            Physics.SyncTransforms();
            for (int i = 0; i < parts.Length; i++)
            {
                float depth = PlayerOverlapDepth(parts[i]);
                bool worsensPrevious = depth > previousOverlapDepths[i] + .0001f;
                bool worsensAlignmentStart = alignmentStep && depth > alignmentOverlapDepths[i] + .0001f &&
                    depth >= previousOverlapDepths[i] - .000001f;
                if (!worsensPrevious && !worsensAlignmentStart) continue;
                sculpture.transform.rotation = previous;
                Physics.SyncTransforms();
                return false;
            }
            return true;
        }

        void TickSculptureAlignment(Quaternion requested, float dt)
        {
            if (sculpture == null || !sculpture.gameObject.activeInHierarchy || autoAlign == null) return;
            bool wasAligning = autoAlign.IsAligning;
            Quaternion output = autoAlign.Tick(requested, dt);
            bool alignmentStep = wasAligning && (autoAlign.IsAligning || autoAlign.HasPendingCompletion);
            bool applied = ApplyFixedSculptureRotation(output, alignmentStep);
            alignmentBlocked = !applied && alignmentStep;
            lastAppliedSculptureRotation = sculpture.transform.rotation;
            if (Held == sculpture) interaction.Rotation = lastAppliedSculptureRotation;
            if (!applied)
            {
                if (alignmentStep) autoAlign.RejectAlignmentStep(lastAppliedSculptureRotation);
                else autoAlign.Reset();
            }
            else if (!wasAligning && autoAlign.IsAligning)
            {
                alignmentStartRotation = lastAppliedSculptureRotation;
                PlayAlignmentDing(.14f, .85f);
            }
            if (!swapping && autoAlign.TryConsumeCompletion(out int direction, out Quaternion lightAdjustment))
                StartCoroutine(ReplaceSculpture(direction, lightAdjustment));
        }

        void ApplyFixedRewardRotation()
        {
            Quaternion previous = fixedReward.transform.rotation;
            var parts = fixedReward.parts;
            if (previousOverlapDepths.Length != parts.Length) previousOverlapDepths = new float[parts.Length];
            Physics.SyncTransforms();
            for (int i = 0; i < parts.Length; i++)
                previousOverlapDepths[i] = PlayerOverlapDepth(parts[i]);
            fixedReward.transform.SetPositionAndRotation(rewardAnchor, interaction.Rotation);
            Physics.SyncTransforms();
            for (int i = 0; i < parts.Length; i++)
                if (PlayerOverlapDepth(parts[i]) > previousOverlapDepths[i] + .0001f)
                {
                    fixedReward.transform.rotation = previous;
                    interaction.Rotation = previous;
                    Physics.SyncTransforms();
                    break;
                }
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
            // Inspector edits and other scripts can legitimately set the root's
            // rotation while it is held. Adopt that pose before applying input.
            if (Held == sculpture && sculpture != null &&
                Quaternion.Angle(sculpture.transform.rotation, lastAppliedSculptureRotation) > .01f)
            {
                interaction.Rotation = sculpture.transform.rotation;
                autoAlign?.Reset();
            }
            PlayerControls.Frame frame = controls.Read(dt, Aimed != null, Held != null);
            bool autoRotating = Held == sculpture && autoAlign != null && autoAlign.IsAligning;
            if (Held != null && frame.RotateHeld)
            {
                if (!autoRotating)
                {
                    Vector2 turn = PlayerControls.ObjectRotationDegrees(frame.Look);
                    interaction.Rotation = Quaternion.AngleAxis(turn.x, Vector3.up) * interaction.Rotation *
                        Quaternion.AngleAxis(turn.y, Vector3.right);
                }
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
                if ((Held == sculpture || Held == fixedReward) && frame.GrabPressed)
                {
                    interaction.Drop();
                    if (Held == null) autoAlign?.Reset();
                    return;
                }
                float turn = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
                float tilt = (Input.GetKey(KeyCode.X) ? 1 : 0) - (Input.GetKey(KeyCode.Z) ? 1 : 0);
                if (autoRotating) turn = tilt = 0f;
                if (turn != 0 || tilt != 0)
                    interaction.Rotation = Quaternion.AngleAxis(turn * 75f * dt, Vector3.up) *
                        interaction.Rotation * Quaternion.AngleAxis(tilt * 75f * dt, Vector3.right);

                interaction.Advance(dt);
                if (Held == sculpture) TickSculptureAlignment(interaction.Rotation, dt);
                else if (Held == fixedReward) { autoAlign?.Reset(); ApplyFixedRewardRotation(); }
                else { autoAlign?.Reset(); UpdateProjection(); }
            }
            else
            {
                // The light passage is the solution; holding the sculpture is
                // an input method, not an additional completion requirement.
                if (sculpture != null) TickSculptureAlignment(sculpture.transform.rotation, dt);
                interaction.UpdateAim();
            }

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
            PlayAlignmentDing(.4f, 1.15f);
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
                fixedReward = key;
                rewardAnchor = key.transform.position;
                var doorSequence = FindFirstObjectByType<KeyShadowSequence>();
                if (doorSequence != null) doorSequence.AcceptMatchedKey(key);
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

        void PlayAlignmentDing(float volume, float tone)
        {
            if (alignmentAudio == null)
            {
                alignmentAudio = gameObject.AddComponent<AudioSource>();
                alignmentAudio.playOnAwake = false;
                alignmentAudio.spatialBlend = 0f;
                alignmentAudio.dopplerLevel = 0f;
                alignmentAudio.priority = 40;
            }
            if (alignmentDing == null)
            {
                const int rate = 44100, count = 30870;
                var samples = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / rate;
                    float attack = Mathf.Clamp01(t / .004f);
                    float release = Mathf.Clamp01((.7f - t) / .08f);
                    float bell = Mathf.Sin(2f * Mathf.PI * 1046.5f * t) * Mathf.Exp(-7f * t) +
                        .35f * Mathf.Sin(2f * Mathf.PI * 2093f * t) * Mathf.Exp(-11f * t) +
                        .15f * Mathf.Sin(2f * Mathf.PI * 3139.5f * t) * Mathf.Exp(-17f * t);
                    samples[i] = bell * attack * release * .65f;
                }
                alignmentDing = AudioClip.Create("Key alignment · bell", count, 1, rate, false);
                alignmentDing.hideFlags = HideFlags.HideAndDontSave;
                alignmentDing.SetData(samples, 0);
            }
            alignmentAudio.pitch = tone;
            alignmentAudio.PlayOneShot(alignmentDing, volume);
        }

        void OnDestroy()
        {
            autoAlign?.Dispose();
            if (alignmentDing != null) Destroy(alignmentDing);
        }

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

        public void StartPlacement()
        {
            Held.BeginHold();
            if (Held == sculpture) lastAppliedSculptureRotation = sculpture.transform.rotation;
        }
        public void PrepareRelease() { pendingRelease = Held; if (Held != sculpture && Held != fixedReward) UpdateProjection(); }

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
            if (released && (releasedProp == sculpture || releasedProp == fixedReward))
            {
                Vector3 anchor = releasedProp == sculpture ? sculptureAnchor : rewardAnchor;
                PropReleasePhysics.Attach(releasedProp.body).CancelPendingRelease();
                releasedProp.body.isKinematic = true;
                releasedProp.body.useGravity = false;
                releasedProp.transform.position = anchor;
                releasedProp.body.position = anchor;
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
            float captureGlow = autoAlign != null && autoAlign.IsAligning && !paused && !alignmentBlocked
                ? .12f * Mathf.Sin(Mathf.PI * autoAlign.Progress) : 0f;
            float flash = Mathf.Max(successFlash, captureGlow);
            if (flash > 0f)
            {
                Color original = GUI.color;
                GUI.color = new Color(.9f, .95f, 1f, flash);
                GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
                GUI.color = original;
            }
            PlayerControls.DrawCursor(new Vector2(width * .5f, height * .5f),
                Aimed != null, Held != null, false, new Color(.16f, .42f, .95f), Color.white);

#if UNITY_EDITOR
            if (sculpture != null && sculpture.gameObject.activeInHierarchy && autoAlign != null)
            {
                float angle = autoAlign.NearestTargetAngle(sculpture.transform.rotation, out int direction);
                string state = paused ? "Paused - resume to solve" : interactionLocked ? "Locked" :
                    autoAlign.IsAligning && alignmentBlocked ? "Blocked - move away" : autoAlign.IsAligning ? "Aligning" :
                    autoAlign.PassageIsClear ? "Light passes through" : autoAlign.WithinCaptureRange ? "Within assist" : "Passage blocked";
                GUI.Box(new Rect(16, 16, 310, 122), GUIContent.none);
                GUI.Label(new Rect(26, 22, 290, 22), "Alignment check (Editor only)");
                GUI.Label(new Rect(26, 43, 290, 22), $"Face: {direction + 1}   Center-axis offset: {angle:F1} deg");
                GUI.Label(new Rect(26, 64, 290, 22), $"Assist: {KeyShadowAutoAlign.AllowedAngle:F0} deg   {state}");
                GUI.Label(new Rect(26, 85, 290, 22), $"Auto-align: {autoAlign.Progress:P0}   No hold required");
                GUI.Label(new Rect(26, 106, 290, 22), PassageTestLabel);
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
