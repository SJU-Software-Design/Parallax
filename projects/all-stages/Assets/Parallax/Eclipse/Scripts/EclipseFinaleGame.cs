using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Eclipse
{
    public enum FinalePhase { Chamber, Corridor, Drifting, Complete }
    public enum FinaleItem { None, Moon, Exit }

    public sealed partial class EclipseFinaleGame : MonoBehaviour
    {
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse") >= 0 ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-test") >= 0;
        public static EclipseFinaleGame Instance;
        public static readonly Vector3 PuzzleSunPosition = new Vector3(0, 30, 170);
        public static readonly Vector3 CinematicSunPosition = new Vector3(0, 30, 170);
        public static Vector3 SunPosition => Instance != null && Instance.world != null
            ? Instance.world.CurrentSunPosition : PuzzleSunPosition;
        public const float SunRadius = 18f;
        public const float WalkSpeed = FirstPersonMotor.Speed, EyeHeight = 1.55f;
        public const float PropGravity = 6f, PropFallSpeed = 18f;
        public Camera view;
        public CharacterController body;
        public EclipseFinaleWorld world;
        public Transform moon, exitSign;
        public FinalePhase phase;
        public FinaleItem held, aimed;
        public bool started, paused = true, automated, totality, discovered;
        public bool MoonAnchored { get; private set; }
        public bool MoonFalling { get; private set; }
        public bool ExitFalling { get; private set; }
        public float MoonPreviewCoverage => CoverageAt(view.transform.position);
        public float coverage, darkness, exposure, settle, elapsed, driftTime;
        public int respawns, hint;
        public string notice;
        public float noticeUntil;
        public Vector3 Spawn => new Vector3(-3.2f, .06f, 1.0f);
        public bool InShelter => phase != FinalePhase.Chamber || world != null && body != null && world.IsShelterShadowAt(new Vector3(body.transform.position.x, .03f, body.transform.position.z));
        public bool Safe => InShelter || IsShadowed(body.transform.position + Vector3.up * .5f);
        public float MoonRadius => moon.localScale.x * .5f;
        public float ExitScale => exitSign.localScale.x;
        public bool DoorFits => ExitScale * EclipseFinaleWorld.DoorWidth > body.radius * 2 + .10f &&
            ExitScale * EclipseFinaleWorld.DoorHeight > body.height + .10f;
        public Vector3 DoorCenter => exitSign.TransformPoint(new Vector3(EclipseFinaleWorld.DoorX, .36f, 0));
        Quaternion carryRotation;
        Vector3 savedPosition, savedScale;
        Quaternion savedRotation;
        float referenceDistance, referenceScale, yaw, pitch, vertical;
        static readonly Vector3 ExitGrabPoint = new Vector3(0, .52f, 0);
        PerspectivePlacement exitPlacement;
        BoxCollider[] exitPlacementParts;
        PerspectivePlacement.Result exitPlacementResult;
        bool exitPlacementFeasible;
        Vector3 exitInitialDirection;
        float exitAlignmentTime, exitCarryDepth;
        public PerspectivePlacement.Result ExitPlacement => exitPlacementResult;
        public bool ExitPlacementBlocked => held == FinaleItem.Exit && !exitPlacementFeasible;
        Vector3 moonInitialDirection;
        float moonAlignmentTime, moonCarryDepth;
        bool moonPlacementFeasible;
        public bool MoonPlacementBlocked => held == FinaleItem.Moon && !moonPlacementFeasible;
        Vector3 driftStart;
        Quaternion driftStartRotation;
        Vector3 previousFeet;

        bool logKeyboard;
        Vector2 previousKeyboard;
        float anchoredCoverage, moonFallVelocity, moonFallTime;
        bool savedMoonAnchored, savedMoonFalling;
        float savedMoonCoverage, savedMoonFallVelocity, savedMoonFallTime;
        float exitFallVelocity, savedExitFallVelocity;
        bool savedExitFalling;
        readonly PlayerControls controls = new PlayerControls();
        public EclipseEndingCredits Credits { get; private set; }
        public EclipseExitPresentation ExitPresentation { get; private set; }
        public float VisualCoverage { get; private set; }
        public float TotalityStableTime { get; private set; }
        public bool EndingReturnReady => ExitPresentation != null && ExitPresentation.ReturnReady;
        public bool EndingSequenceActive => (Credits != null && Credits.Active) || (ExitPresentation != null && ExitPresentation.Active);
        public bool ExitUnlocked => Credits != null && Credits.Section == EndingCreditSection.Finished && Credits.TeamComplete && Credits.ThanksComplete;
        public bool PresentationSuspended => !applicationFocused && !automated && !ValidationQuitSuppressed;
        public bool InputEnabled => started && !paused && !EndingSequenceActive && !ParallaxGameShell.BlocksGameplay;
        public bool QuitRequested { get; private set; }
        public bool ValidationQuitSuppressed => Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-test") >= 0;
        public float QuitFadeAlpha => ExitPresentation != null ? ExitPresentation.BlackAlpha : 0;
        public bool FinalQuitFadeActive => ExitPresentation != null && ExitPresentation.Active;
        bool applicationFocused = true;

        public static void Bootstrap()
        {
            if (Instance != null) return;
            // Legacy Editor launch only. Never clear persistent Shell/Progress/Settings hosts.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.GetComponentInChildren<ParallaxGameShell>(true) != null) continue;
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
            }
            Time.timeScale = 1;
            var host = new GameObject("PARALLAX — ECLIPSE / finale");
            Instance = host.AddComponent<EclipseFinaleGame>();
        }

        void Awake()
        {
            Instance = this;
            automated = Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-test") >= 0 || ValidationQuitSuppressed;
            logKeyboard = Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-input-check") >= 0;
            Application.runInBackground = true;
            if (automated) { Application.targetFrameRate = 120; QualitySettings.vSyncCount = 0; }
            // Normal quality, frame cap and vSync remain owned by the shared settings runtime.
            Shader.SetGlobalFloat("_PlacementPreviewActive", 0);
            world = gameObject.AddComponent<EclipseFinaleWorld>();
            world.Build(this);
            var player = new GameObject("Player");
            player.transform.SetParent(transform);
            body = player.AddComponent<CharacterController>();
            // Shared movement configures the same capsule used in the other maps.
            var cameraObject = new GameObject("Eclipse camera");
            cameraObject.transform.SetParent(transform);
            view = cameraObject.AddComponent<Camera>();
            view.nearClipPlane = .035f; view.farClipPlane = 1200; view.fieldOfView = 70;
            cameraObject.tag = "MainCamera";
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.005f, .009f, .022f);
            view.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            ConfigureSharedControls(); PlayerControls.ConfigureCamera(view);
            Credits = gameObject.AddComponent<EclipseEndingCredits>();
            Credits.Initialize(this);
            gameObject.AddComponent<EclipseEndingAmbience>().Initialize(this);
            gameObject.AddComponent<EclipseEndingMusic>().Initialize(this);
            ExitPresentation = gameObject.AddComponent<EclipseExitPresentation>();
            ExitPresentation.Initialize(this);
            gameObject.AddComponent<EclipseFinaleHud>().game = this;
            gameObject.AddComponent<GlassTrial.EclipseGlassHud>();
            gameObject.AddComponent<GlassTrial.EclipseGlassHover>();
            ResetGame(false);
            // The old validator takes screenshots; the new focused mode never attaches it.
            if (automated && !ValidationQuitSuppressed) gameObject.AddComponent<EclipseFinaleValidation>().game = this;
        }

        public void ResetGame(bool play = true)
        {
            Time.timeScale = 1;
            if (Credits != null) Credits.ResetSequence();
            if (world != null) world.SetSunPresentationPosition(PuzzleSunPosition);
            ExitPresentation?.ResetPresentation();
            QuitRequested = false; VisualCoverage = TotalityStableTime = 0;
            ResetSharedMotion();
            held = aimed = FinaleItem.None; phase = FinalePhase.Chamber;
            ClearExitPlacement();
            totality = discovered = false; coverage = darkness = exposure = settle = elapsed = driftTime = 0;
            respawns = hint = 0; noticeUntil = 0;
            RestoreSmallMoon();
            exitSign.SetPositionAndRotation(new Vector3(0, 0, 54), Quaternion.identity);
            exitSign.localScale = Vector3.one * .72f;
            ExitFalling = false; exitFallVelocity = 0;
            world.SetMoonColliders(true); world.SetSignColliders(true);
            world.SetEnding(false); world.SetLight(0, false);
            Teleport(Spawn); PlayerControls.ConfigureCamera(view);
            FrameOpeningSun();
            started = play; paused = !play; SetCursor(play);
            Physics.SyncTransforms();
        }

        public void FrameOpeningSun()
        {
            if (view == null) return;
            Vector3 openingSun = PuzzleSunPosition - view.transform.position;
            float openingElevation = Mathf.Atan2(openingSun.y, new Vector2(openingSun.x, openingSun.z).magnitude) * Mathf.Rad2Deg;
            float openingRadius = MoonAlignmentRules.SolarAngularRadius(view.transform.position, PuzzleSunPosition, SunRadius) * Mathf.Rad2Deg;
            yaw = 0; pitch = -Mathf.Max(0, openingElevation + openingRadius + 2 - view.fieldOfView * .5f);
            SyncCamera();
        }

        public void Play()
        {
            if (phase == FinalePhase.Complete) { ResetGame(); return; }
            started = true; paused = false; Time.timeScale = 1;
            ClearSharedControlInput(); PlayerControls.ConfigureCamera(view); SetCursor(true);
        }
        public void Pause() { paused = true; ClearSharedControlInput(); SetCursor(false); }
        void SetCursor(bool play)
        {
            Cursor.lockState = play && !automated ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !play || automated;
        }
        void OnApplicationFocus(bool value)
        {
            applicationFocused = value;
            if (!value && started && !automated && !EndingSequenceActive) Pause();
            if (value) { ClearSharedControlInput(); }
        }
        public void Tell(string text, float seconds = 3.5f) { notice = text; noticeUntil = Time.unscaledTime + seconds; }
        public void Teleport(Vector3 feet)
        {
            body.enabled = false; body.transform.position = feet; body.enabled = true;
            ResetSharedMotion(); previousFeet = feet; SyncCamera(); Physics.SyncTransforms();
        }
        public void AimAt(Vector3 point)
        {
            var d = (point - (body.transform.position + Vector3.up * EyeHeight)).normalized;
            yaw = Mathf.Atan2(d.x, d.z) * EclipseMath.Rad2Deg;
            pitch = -Mathf.Asin(d.y) * EclipseMath.Rad2Deg;
            SyncCamera();
        }
        void SyncCamera()
        {
            if (view == null) return;
            view.transform.SetPositionAndRotation(body.transform.position + Vector3.up * EyeHeight,
                Quaternion.Euler(pitch, yaw, 0));
        }
        void Update()
        {
            if (automated) return;
            // Credits consume Space/Esc before the shared gameplay/pause gate.
            if (EndingSequenceActive)
            {
                if ((applicationFocused || ValidationQuitSuppressed) && !paused)
                    TickEndingSequence(Mathf.Min(Time.deltaTime, .05f), Input.GetKey(KeyCode.Space), Input.GetKeyDown(KeyCode.Escape));
                return;
            }
            if (ParallaxGameShell.BlocksGameplay) return;
            if (ParallaxGameShell.AllowStagePauseInput && PlayerControls.PausePressed)
            { if (paused) Play(); else Pause(); return; }
            if (paused) return;
            if (PlayerControls.Pressed(PlayerControls.Action.Reset)) { ResetGame(); return; }
            if (PlayerControls.Pressed(PlayerControls.Action.Hints) && GameShellSettingsRuntime.ShowControlHints && !GlassTrial.HelpHudController.IsPresented && !GlassTrial.HelpHudController.EclipseHintTransitioning) hint = (hint + 1) % 4;
            if (Cursor.lockState != CursorLockMode.Locked && !GlassTrial.HelpHudController.PointerInUse) { Pause(); return; }
            float dt = Time.deltaTime;
            UpdateAim();
            PlayerControls.Frame frame = controls.Read(dt, aimed != FinaleItem.None, held != FinaleItem.None);
            ApplySharedControlFrame(frame);
            Tick(dt, frame.Move, frame.JumpPressed);
            if (logKeyboard && (frame.Move.x != previousKeyboard.x || frame.Move.y != previousKeyboard.y))
                Debug.Log("ECLIPSE_KEYBOARD movement=" + frame.Move + " feet=" + body.transform.position + " dt=" + dt + " timeScale=" + Time.timeScale);
            previousKeyboard = frame.Move;
        }

        public void SetEndingRotation(Quaternion rotation)
        {
            view.transform.rotation = rotation;
            Vector3 euler = rotation.eulerAngles;
            yaw = euler.y; pitch = Mathf.DeltaAngle(0, euler.x);
        }

        public void CompleteCredits()
        {
            ClearSharedControlInput(); exposure = 0; hint = 0;
            SetCursor(true);
        }

        public void TickEndingSequence(float dt, bool spaceHeld, bool escapePressed)
        {
            if (paused) return;
            if (ExitPresentation != null && ExitPresentation.Active)
            {
                ExitPresentation.TickExit(dt);
                world.TickShelterVisibility(dt);
                return;
            }
            if (Credits != null && Credits.Active) Credits.TickCredits(dt, spaceHeld, escapePressed);
            world.TickShelterVisibility(dt);
        }

        bool TryBeginCredits()
        {
            if (!totality || TotalityStableTime < 1.8f || darkness < .985f || held != FinaleItem.None || MoonFalling || !body.isGrounded || Credits == null ||
                Credits.Section != EndingCreditSection.Inactive) return false;
            if (!Credits.Begin()) return false;
            exposure = 0; hint = 0; noticeUntil = 0; aimed = FinaleItem.None;
            ClearSharedControlInput(); SetCursor(true); return true;
        }

        void BeginExitFade()
        {
            if (!ExitUnlocked || !Credits.ThanksComplete || ExitPresentation == null || ExitPresentation.Active) return;
            held = aimed = FinaleItem.None; exposure = 0; hint = 0; noticeUntil = 0;
            ClearExitPlacement();
            ClearSharedControlInput();
            phase = FinalePhase.Drifting; body.enabled = false;
            ExitPresentation.BeginEntrance();
        }

        public void Tick(float dt, Vector2 movement, bool jump)
        {
            if (paused) return;
            if (EndingSequenceActive) { TickEndingSequence(dt, false, false); return; }
            elapsed += dt;
            if (phase >= FinalePhase.Drifting) { TickDrift(dt); world.TickShelterVisibility(dt); return; }
            previousFeet = body.transform.position;
            StepSharedMovement(dt, movement, jump);
            SyncCamera();
            UpdateHeld(dt);
            FlushSharedControlDrop();
            UpdateMoonMotion(dt);
            UpdateExitMotion(dt);
            UpdateEclipse(dt);
            world.TickShelterVisibility(dt);
            if (TryBeginCredits()) return;
            if (phase == FinalePhase.Chamber)
            {
                if (!Safe) exposure += dt / 1.35f; else exposure = Mathf.MoveTowards(exposure, 0, dt * 2.4f);
                if (exposure >= 1 || body.transform.position.y < -5)
                {
                    Cancel(); Teleport(Spawn); exposure = 0; respawns++;
                    Tell("빛 속에서는 오래 버틸 수 없습니다. 출발 지점으로 돌아왔습니다.");
                }
                if (totality && Vector3.Distance(body.transform.position, exitSign.position) < 9) discovered = true;
                CheckDoor();
            }
            else if (phase == FinalePhase.Corridor && body.transform.position.z > 23)
            {
                phase = FinalePhase.Drifting; driftTime = 0;
                driftStart = view.transform.position; driftStartRotation = view.transform.rotation;
                body.enabled = false; noticeUntil = 0;
            }
            if (!EndingSequenceActive) TryBeginCredits();
            UpdateAim();
        }

        public void UpdateAim()
        {
            aimed = FinaleItem.None;
            if (phase != FinalePhase.Chamber || held != FinaleItem.None || EndingSequenceActive) return;
            var ray = new Ray(view.transform.position, view.transform.forward);
            bool hitsMoon = RaySphere(ray, moon.position, MoonRadius, out float moonDistance);
            if (ExitUnlocked && totality && RaySign(ray, out float signDistance) && (!hitsMoon || signDistance < moonDistance))
                aimed = FinaleItem.Exit;
            else if (hitsMoon && (Credits == null || !Credits.TeamComplete)) aimed = FinaleItem.Moon;
        }
        bool RaySign(Ray ray, out float distance)
        {
            distance = 0;
            if (Mathf.Abs(ray.direction.z) < .0001f) return false;
            distance = (exitSign.position.z - ray.origin.z) / ray.direction.z;
            if (distance <= 0 || distance > 90) return false;
            Vector3 local = exitSign.InverseTransformPoint(ray.origin + ray.direction * distance);
            return Mathf.Abs(local.x) < EclipseFinaleWorld.SignWidth * .5f && local.y >= 0 && local.y <= EclipseFinaleWorld.SignHeight;
        }
        public void Interact()
        {
            if (EndingSequenceActive) return;
            if (held != FinaleItem.None) { Drop(); return; }
            UpdateAim(); if (aimed == FinaleItem.None) return;
            held = aimed; aimed = FinaleItem.None;
            var target = held == FinaleItem.Moon ? moon : exitSign;
            savedPosition = target.position; savedScale = target.localScale; savedRotation = target.rotation;
            carryRotation = target.rotation; referenceScale = target.localScale.x;
            Vector3 anchor = held == FinaleItem.Moon ? target.position : target.TransformPoint(ExitGrabPoint);
            referenceDistance = Mathf.Max(.01f, Vector3.Distance(view.transform.position, anchor));
            if (held == FinaleItem.Moon)
            {
                savedMoonAnchored = MoonAnchored; savedMoonFalling = MoonFalling;
                savedMoonCoverage = anchoredCoverage; savedMoonFallVelocity = moonFallVelocity; savedMoonFallTime = moonFallTime;
                BeginMoonAlignmentGrab();
                moonInitialDirection = Quaternion.Inverse(view.transform.rotation) * (anchor - view.transform.position).normalized;
                moonAlignmentTime = 0; moonCarryDepth = referenceDistance; moonPlacementFeasible = true; moonFloorConstrained = false;
                MoonAnchored = MoonFalling = false; anchoredCoverage = moonFallVelocity = moonFallTime = 0;
                world.SetMoonColliders(false); settle = 0;
            }
            else
            {
                savedExitFalling = ExitFalling; savedExitFallVelocity = exitFallVelocity;
                BeginExitPlacement(anchor);
                ExitFalling = false; exitFallVelocity = 0;
                world.SetSignColliders(false);
            }
        }
        public void UpdateHeld(float dt)
        {
            if (held == FinaleItem.None) return;
            if (held == FinaleItem.Moon) UpdateHeldMoon(dt);
            else UpdateHeldExit(dt);
            Physics.SyncTransforms();
        }
        void UpdateHeldMoon(float dt)
        {
            Vector3 eye = view.transform.position;
            moonAlignmentTime = Mathf.Min(.8f, moonAlignmentTime + Mathf.Max(0, dt));
            float alignment = 1 - Mathf.Pow(1 - Mathf.Clamp01(moonAlignmentTime / .8f), 2);
            Vector3 direction = view.transform.rotation * Vector3.Slerp(moonInitialDirection, Vector3.forward, alignment);
            float ratio = referenceScale / referenceDistance;
            // Assistance may nudge direction near the Sun, but never changes the
            // carried angular size or the camera. Size is judged only on release.
            Vector3 unitTarget = eye + direction;
            float unitScale = ratio;
            ApplyMoonHeldAlignment(ref unitTarget, unitScale);
            direction = (unitTarget - eye).normalized;
            moonPlacementFeasible = ResolveMoonPlacement(eye, direction, ratio, out float minimum, out float target);
            moonFloorConstrained = false;
            if (!moonPlacementFeasible && direction.y < 0)
            {
                // The ordinary physical minimum can prevent a centred floor
                // contact. Try its bounded ground exception on the same ray
                // before considering a safe direction still inside the view.
                moonPlacementFeasible = ResolveMoonFloorContact(eye, direction, ratio,
                    out direction, out minimum, out target);
                moonFloorConstrained = moonPlacementFeasible;
            }
            if (!moonPlacementFeasible) return;
            float t = 1 - Mathf.Exp(-14 * Mathf.Max(0, dt));
            moonCarryDepth = Mathf.Clamp(Mathf.Lerp(moonCarryDepth, target, t), minimum, target);
            if (!MoonSphereClearAt(eye, direction, ratio, moonCarryDepth)) moonCarryDepth = target;
            moon.localScale = Vector3.one * (ratio * moonCarryDepth);
            moon.SetPositionAndRotation(eye + direction * moonCarryDepth, carryRotation);
        }

        bool ResolveMoonPlacement(Vector3 eye, Vector3 direction, float ratio, out float minimum, out float depth, float minimumScale = .45f, float minimumZ = 2)
        {
            // Radius grows with distance. Bound that same cone rather than
            // clamping scale or pushing the final centre off the view ray.
            float radiusRatio = ratio * MoonContactRadiusFactor;
            minimum = ratio > 0 ? minimumScale / ratio : 0;
            depth = ratio > 0 ? Mathf.Min(160, 48 / ratio) : 0;
            if (radiusRatio <= 0 || radiusRatio >= .99f || minimum > depth ||
                Physics.CheckSphere(eye, .0001f, EclipseFinaleWorld.MoonSupportMask, QueryTriggerInteraction.Ignore)) return false;
            ClipMoonAxis(eye.x, direction.x, radiusRatio, -45, 45, ref minimum, ref depth);
            ClipMoonAxis(eye.y, direction.y, radiusRatio, .020f, 250, ref minimum, ref depth);
            ClipMoonAxis(eye.z, direction.z, radiusRatio, minimumZ, 150, ref minimum, ref depth);
            if (minimum > depth) return false;
            Vector3 right = Vector3.Cross(direction, Mathf.Abs(direction.y) < .98f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(right, direction).normalized;
            LimitMoonRay(eye, direction, 1 + radiusRatio, ref depth);
            float angle = Mathf.Asin(radiusRatio);
            for (int ring = 1; ring <= 4; ring++)
            {
                float theta = angle * (ring == 4 ? .999f : ring * .25f);
                float cosine = Mathf.Cos(theta), sine = Mathf.Sin(theta);
                float back = cosine + Mathf.Sqrt(Mathf.Max(0, radiusRatio * radiusRatio - sine * sine));
                for (int i = 0; i < 32; i++)
                {
                    float azimuth = i * (Mathf.PI * 2 / 32);
                    Vector3 ray = direction * cosine + (right * Mathf.Cos(azimuth) + up * Mathf.Sin(azimuth)) * sine;
                    LimitMoonRay(eye, ray, back, ref depth);
                }
            }
            if (depth < minimum) return false;
            // The full sphere refines finite silhouette samples at thin edges.
            if (!MoonSphereClearAt(eye, direction, ratio, depth))
            {
                if (!MoonSphereClearAt(eye, direction, ratio, minimum)) return false;
                float lo = minimum, hi = depth;
                for (int i = 0; i < 18; i++)
                {
                    float middle = (lo + hi) * .5f;
                    if (MoonSphereClearAt(eye, direction, ratio, middle)) lo = middle; else hi = middle;
                }
                depth = lo;
            }
            return true;
        }

        static void ClipMoonAxis(float eye, float direction, float radiusRatio, float minimum, float maximum,
            ref float lo, ref float hi)
        {
            InteractionRaycast.ClipDepth(eye, direction - radiusRatio, minimum, maximum, ref lo, ref hi);
            InteractionRaycast.ClipDepth(eye, direction + radiusRatio, minimum, maximum, ref lo, ref hi);
        }

        static void LimitMoonRay(Vector3 eye, Vector3 ray, float back, ref float depth)
        {
            if (Physics.Raycast(eye, ray, out var hit, float.PositiveInfinity,
                EclipseFinaleWorld.MoonSupportMask, QueryTriggerInteraction.Ignore))
                depth = Mathf.Min(depth, Mathf.Max(0, hit.distance - .002f) / back);
        }

        bool MoonSphereClearAt(Vector3 eye, Vector3 direction, float ratio, float depth)
        {
            return !Physics.CheckSphere(eye + direction * depth, Mathf.Max(.0001f, ratio * depth * MoonContactRadiusFactor - .001f),
                EclipseFinaleWorld.MoonSupportMask, QueryTriggerInteraction.Ignore);
        }
        void BeginExitPlacement(Vector3 anchor)
        {
            // Use the same compound silhouette resolver as the other maps. These
            // are the four original physical panels; the empty i stays empty.
            exitPlacementParts = Array.FindAll(exitSign.GetComponentsInChildren<BoxCollider>(),
                part => part.enabled && !part.isTrigger);
            exitPlacement = new PerspectivePlacement(exitSign, exitPlacementParts, ExitGrabPoint, referenceScale, referenceDistance);
            exitInitialDirection = Quaternion.Inverse(view.transform.rotation) * (anchor - view.transform.position).normalized;
            exitAlignmentTime = 0; exitCarryDepth = referenceDistance;
            exitPlacementFeasible = true; exitPlacementResult = default;
        }

        void ClearExitPlacement()
        {
            exitPlacement = null; exitPlacementParts = null; exitPlacementResult = default;
            exitPlacementFeasible = false; exitAlignmentTime = exitCarryDepth = 0;
        }

        void UpdateHeldExit(float dt)
        {
            if (exitPlacement == null) return;
            exitAlignmentTime = Mathf.Min(.8f, exitAlignmentTime + Mathf.Max(0, dt));
            float alignment = 1 - Mathf.Pow(1 - Mathf.Clamp01(exitAlignmentTime / .8f), 2);
            Vector3 direction = view.transform.rotation * Vector3.Slerp(exitInitialDirection, Vector3.forward, alignment);
            // A size or deck edge restricts distance BEFORE scale is calculated.
            // Clamping them independently changes apparent size on close pickup.
            exitPlacementResult = exitPlacement.Resolve(view, Quaternion.identity, body, EclipseFinaleWorld.MoonSupportMask,
                .001f, 6, new Vector3(-12.5f, 0, 2), new Vector3(12.5f, 100, 65), direction: direction);
            exitPlacementFeasible = exitPlacementResult.Feasible;
            if (!exitPlacementFeasible) return;
            float previousDepth = exitCarryDepth;
            Vector3 previousPosition = exitSign.position, previousScale = exitSign.localScale;
            Quaternion previousRotation = exitSign.rotation;
            float t = 1 - Mathf.Exp(-12 * Mathf.Max(0, dt));
            exitCarryDepth = Mathf.Clamp(Mathf.Lerp(exitCarryDepth, exitPlacementResult.Depth, t),
                exitPlacementResult.RoomMinimum, exitPlacementResult.Depth);
            ApplyExitCarryPose(direction, exitCarryDepth);
            if (!HeldExitClear())
            {
                // A shorter interpolated pose can cross a small obstacle even
                // though the resolver's endpoint is clear. Prefer that endpoint.
                exitCarryDepth = exitPlacementResult.Depth;
                ApplyExitCarryPose(direction, exitCarryDepth);
                if (!HeldExitClear())
                {
                    exitSign.localScale = previousScale;
                    exitSign.SetPositionAndRotation(previousPosition, previousRotation);
                    exitCarryDepth = previousDepth; exitPlacementFeasible = false;
                }
            }
        }

        void ApplyExitCarryPose(Vector3 direction, float depth)
        {
            float scale = referenceScale * depth / referenceDistance;
            exitSign.localScale = Vector3.one * scale;
            exitSign.SetPositionAndRotation(view.transform.position + direction * depth - ExitGrabPoint * scale,
                Quaternion.identity);
        }

        bool HeldExitClear()
        {
            if (exitPlacementParts == null || exitPlacementParts.Length == 0) return false;
            foreach (var part in exitPlacementParts)
            {
                Vector3 half = Vector3.Scale(part.size, part.transform.lossyScale) * .5f;
                half = Vector3.Max(Vector3.one * .0001f, half - Vector3.one * .001f);
                if (Physics.CheckBox(part.transform.TransformPoint(part.center), half, part.transform.rotation,
                    EclipseFinaleWorld.MoonSupportMask, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }
        public bool Drop()
        {
            if (held == FinaleItem.None) return false;
            if (held == FinaleItem.Moon)
            {
                if (!moonPlacementFeasible)
                { Tell("이 방향에는 달을 놓을 공간이 없습니다. 조준 방향을 바꿔 주세요."); return false; }
                if (Vector3.Distance(body.transform.position + Vector3.up, moon.position) < MoonRadius + .45f ||
                    MoonOverlapsPlayerCapsule())
                { Tell("달이 몸이나 발과 겹칩니다. 조금 먼 바닥을 바라보고 놓아 주세요."); return false; }
                world.SetMoonColliders(true); settle = 0;
                MoonAnchored = CanAnchorMoon();
                // Preserve actual coverage in the release snapshot. Accepted eclipses
                // ease the presentation to darkness even with a remaining rim.
                anchoredCoverage = MoonAnchored && MoonAlignmentConfirmed ? 1 : 0;
                MoonFalling = !MoonAnchored; moonFallVelocity = moonFallTime = 0;
                if (!MoonAlignmentConfirmed && !MoonFloorConstrained) Tell(MoonAlignmentHint, 4);
            }
            else
            {
                if (!exitPlacementFeasible)
                { Tell("조준한 위치에 표지를 놓을 공간이 없습니다. 방향을 조금 바꿔 주세요."); return false; }
                Vector3 p = exitSign.InverseTransformPoint(body.transform.position + Vector3.up * .9f);
                if (Mathf.Abs(p.z) < .15f + .3f / ExitScale && Mathf.Abs(p.x) < EclipseFinaleWorld.SignWidth * .5f &&
                    p.y > -.9f / ExitScale && p.y < EclipseFinaleWorld.SignHeight + .9f / ExitScale)
                { Tell("몸과 표지가 겹칩니다. 한 걸음 뒤로 이동해 주세요."); return false; }
                world.SetSignColliders(true);
                ExitFalling = exitSign.position.y > .01f; exitFallVelocity = 0;
                if (!ExitFalling) exitSign.position = new Vector3(exitSign.position.x, 0, exitSign.position.z);
            }
            held = FinaleItem.None; ClearExitPlacement(); Physics.SyncTransforms(); return true;
        }
        public void Cancel()
        {
            if (held == FinaleItem.None) return;
            var target = held == FinaleItem.Moon ? moon : exitSign;
            target.SetPositionAndRotation(savedPosition, savedRotation); target.localScale = savedScale;
            if (held == FinaleItem.Moon)
            {
                MoonAnchored = savedMoonAnchored; MoonFalling = savedMoonFalling;
                anchoredCoverage = savedMoonCoverage; moonFallVelocity = savedMoonFallVelocity; moonFallTime = savedMoonFallTime;
                CancelMoonAlignmentGrab();
                settle = 0;
            }
            else { ExitFalling = savedExitFalling; exitFallVelocity = savedExitFallVelocity; }
            world.SetMoonColliders(true); world.SetSignColliders(true);
            held = FinaleItem.None; ClearExitPlacement(); Physics.SyncTransforms();
        }

        bool CanAnchorMoon()
        {
            return CaptureMoonAlignmentRelease();
        }

        void RestoreSmallMoon()
        {
            moon.SetPositionAndRotation(new Vector3(-2.6f, .292f, 3.4f), Quaternion.Euler(0, 16, 0));
            moon.localScale = Vector3.one * .55f;
            MoonAnchored = MoonFalling = false; moonFloorConstrained = false; anchoredCoverage = moonFallVelocity = moonFallTime = 0; settle = 0;
            ResetMoonAlignment();
        }

        void UpdateMoonMotion(float dt)
        {
            if (held == FinaleItem.Moon || !MoonFalling) return;
            moonFallTime += dt; moonFallVelocity = Mathf.Max(-PropFallSpeed, moonFallVelocity - PropGravity * dt);
            float distance = -moonFallVelocity * dt;
            // The mesh's raised crater rims slightly exceed the nominal .5
            // collider. Land its unchanged visible envelope above the floor.
            if (Physics.SphereCast(moon.position, moon.localScale.x * MoonContactRadiusFactor, Vector3.down, out var floor,
                distance + .015f, EclipseFinaleWorld.MoonSupportMask, QueryTriggerInteraction.Ignore))
            {
                moon.position += Vector3.down * Mathf.Max(0, floor.distance - .008f);
                MoonFalling = false; moonFallVelocity = 0; settle = 0;
            }
            else moon.position += Vector3.down * distance;
            if (moon.position.y + MoonRadius < -6 || moonFallTime > 40)
            {
                RestoreSmallMoon();
                Tell("달이 출발 구역으로 돌아왔습니다.", 3);
            }
            Physics.SyncTransforms();
        }

        void UpdateExitMotion(float dt)
        {
            if (held == FinaleItem.Exit || !ExitFalling) return;
            exitFallVelocity = Mathf.Max(-PropFallSpeed, exitFallVelocity - PropGravity * dt);
            float distance = -exitFallVelocity * dt;
            // Placement keeps the upright sign inside the continuous deck. Its origin
            // is the bottom edge, so a downward support ray lands the real doorway at floor level.
            if (Physics.Raycast(exitSign.position + Vector3.up * .02f, Vector3.down, out var floor,
                distance + .04f, EclipseFinaleWorld.MoonSupportMask, QueryTriggerInteraction.Ignore))
            {
                exitSign.position = new Vector3(exitSign.position.x, floor.point.y, exitSign.position.z);
                ExitFalling = false; exitFallVelocity = 0;
            }
            else exitSign.position += Vector3.down * distance;
            Physics.SyncTransforms();
        }

        public float CoverageAt(Vector3 point)
        {
            Vector3 toMoon = moon.position - point, toSun = SunPosition - point;
            if (toMoon.magnitude >= toSun.magnitude || toMoon.magnitude < MoonRadius) return 0;
            // Leave a small margin for the real mesh's recessed crater silhouette.
            float a = Mathf.Asin(Mathf.Clamp01(MoonRadius * .97f / toMoon.magnitude));
            float b = Mathf.Asin(SunRadius / toSun.magnitude);
            float separation = Vector3.Angle(toMoon, toSun) * EclipseMath.Deg2Rad;
            if (separation >= a + b) return 0;
            if (a >= b + separation) return 1;
            if (b >= a + separation) return Mathf.Clamp01(a * a / (b * b));
            float aa = a * a, bb = b * b, dd = separation * separation;
            float area = aa * Mathf.Acos(Mathf.Clamp((dd + aa - bb) / (2 * separation * a), -1, 1)) +
                bb * Mathf.Acos(Mathf.Clamp((dd + bb - aa) / (2 * separation * b), -1, 1)) -
                .5f * Mathf.Sqrt(Mathf.Max(0, (-separation+a+b)*(separation+a-b)*(separation-a+b)*(separation+a+b)));
            return Mathf.Clamp01(area / (EclipseMath.PI * bb));
        }
        public bool IsShadowed(Vector3 point)
        {
            if (held == FinaleItem.Moon || MoonFalling || settle < .35f) return false;
            if (totality) return true;
            if (MoonAnchored) return false; // A visible ring of sunlight is not a safe eclipse.
            return RaySphere(new Ray(point, (SunPosition-point).normalized), moon.position, MoonRadius * .98f, out _);
        }
        public void UpdateEclipse(float dt)
        {
            if (held != FinaleItem.Moon && !MoonFalling) settle += dt;
            VisualCoverage = CoverageAt(view.transform.position);
            // The visible partial eclipse changes the environment while aiming.
            // Only a released, anchored moon confirms the puzzle completion.
            float target = MoonAnchored && held != FinaleItem.Moon && !MoonFalling ? anchoredCoverage : VisualCoverage;
            bool before = totality;
            totality = held != FinaleItem.Moon && !MoonFalling && MoonAnchored && MoonAlignmentConfirmed && settle >= .35f;
            coverage = Mathf.Clamp01(target);
            darkness = Mathf.MoveTowards(darkness, coverage, Mathf.Max(0, dt) / 2.3f);
            TotalityStableTime = totality ? TotalityStableTime + Mathf.Max(0, dt) : 0;
            world.SetMoonShadow(held != FinaleItem.Moon && !MoonFalling && (!MoonAnchored || totality));
            world.SetLight(darkness, totality);
            if (!before && totality) Tell("태양이 가려졌습니다. 빛이 잦아드는 동안 바닥을 살펴보세요.", 4);
        }
        void CheckDoor()
        {
            if (!ExitUnlocked || !totality || held != FinaleItem.None || ExitFalling || !DoorFits) return;
            Vector3 current = exitSign.InverseTransformPoint(body.transform.position);
            Vector3 previous = exitSign.InverseTransformPoint(previousFeet);
            float clearance = EclipseFinaleWorld.DoorWidth * .5f - body.radius / ExitScale;
            if (Mathf.Abs(current.x - EclipseFinaleWorld.DoorX) > clearance) return;
            if (current.y < -.08f || current.y + body.height / ExitScale > EclipseFinaleWorld.DoorBottom + EclipseFinaleWorld.DoorHeight) return;
            if (previous.z < .16f && current.z >= -.015f)
            {
                BeginExitFade();
            }
        }
        void TickDrift(float dt)
        {
            driftTime += dt;
            float t = Mathf.Clamp01(driftTime / 13), ease = t * t * (3 - 2 * t);
            view.transform.position = Vector3.Lerp(driftStart, new Vector3(14, -35, 72), ease);
            Quaternion endingLook = Quaternion.LookRotation(new Vector3(0, -67, 8) - view.transform.position);
            view.transform.rotation = Quaternion.Slerp(driftStartRotation, endingLook, Mathf.SmoothStep(0, 1, Mathf.Clamp01((driftTime - 2.5f) / 7)));
            view.fieldOfView = Mathf.Lerp(70, 79, t);
            if (driftTime >= 13) { phase = FinalePhase.Complete; SetCursor(false); }
        }
        public static bool RaySphere(Ray ray, Vector3 center, float radius, out float distance)
        {
            Vector3 oc = ray.origin - center; float b = Vector3.Dot(oc, ray.direction);
            float c = oc.sqrMagnitude - radius * radius, d = b * b - c;
            distance = d >= 0 ? -b - Mathf.Sqrt(d) : -1;
            if (distance < 0 && d >= 0) distance = -b + Mathf.Sqrt(d);
            return distance > 0;
        }
        void OnDestroy() { Time.timeScale = 1; Cursor.visible = true; Cursor.lockState = CursorLockMode.None; if (Instance == this) Instance = null; }
    }
}
