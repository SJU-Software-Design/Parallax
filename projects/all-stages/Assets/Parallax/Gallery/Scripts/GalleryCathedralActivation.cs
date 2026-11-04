using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // The scan keeps its original pose while small. A slight parting of the
    // palms makes a genuine human-sized opening after enlargement and settling.
    public sealed class GalleryCathedralActivation : MonoBehaviour
    {
        public GalleryOrcusExit entrance;
        public GalleryCathedralPortal portal;
        public SkinnedMeshRenderer[] morphTargets;
        public BoxCollider[] movingParts;
        public Vector3[] closedCenters, openedCenters;
        public Bounds closedOpening, openedOpening;
        [Min(.1f)] public float duration = .65f;
        [Range(100, 150)] public float openedMorphWeight = 100;
        [Range(.75f, 1)] public float assistanceRatio = .90f;
        [Range(0, 15)] public float maximumAssistTilt = 10f;
        [Min(.1f)] public float assistanceDuration = .55f;
        public float Progress { get; private set; }
        public bool Opened => Progress >= .999f;
        public bool OpeningBlocked { get; private set; }
        public bool Assisting { get; private set; }
        public bool AssistanceBlocked { get; private set; }
        public string AssistanceReason { get; private set; }
        public float MinimumAssistedScale => entrance == null ? 0 : entrance.RequiredScale * assistanceRatio;
        public float ScaleReadiness => entrance == null ? 0 : Mathf.Clamp01(CurrentScale / Mathf.Max(.001f, entrance.RequiredScale));
        public bool NearEnough => entrance != null && CurrentScale + .0001f >= MinimumAssistedScale;
        public string Status => AssistanceHint;
        public string AssistanceHint
        {
            get
            {
                if (Assisting) return "통로에 맞게 조각을 맞추고 있어요";
                if (AssistanceBlocked || OpeningBlocked) return "주변이 넓은 바닥으로 옮겨 주세요";
                if (!NearEnough) return "조각을 조금 더 키워 보세요";
                if (IsHeld) return "충분히 커졌어요 · 이제 내려놓으세요";
                if (Tilt > maximumAssistTilt && NeedsAssistance) return "조각을 조금 더 바로 세워 주세요";
                if (entrance != null && entrance.prop != null && entrance.prop.placement != null && !entrance.prop.placement.Ready)
                    return "내려놓은 조각이 안착하면 문이 열려요";
                return Opened ? "계단을 올라 두 손 사이로 들어가세요" : "손이 열리고 있습니다";
            }
        }
        Vector3[] probeCenters;
        bool openingApproved;
        float assistanceTime, retryDelay;
        Vector3 assistancePosition, assistanceStartScale, assistanceTargetScale, lastAppliedScale;
        Quaternion assistanceStartRotation, assistanceTargetRotation, lastAppliedRotation;
        bool ownedPhysics, ownsPreviewPolicy, wasKinematic, usedGravity, wasSleeping, oldPreviewUntilSettled;
        Vector3 oldVelocity, oldAngularVelocity;
        Action restorePhysicsProfile;

        float CurrentScale => Mathf.Min(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));
        float Tilt => Vector3.Angle(transform.up, Vector3.up);
        bool IsHeld => entrance != null && entrance.prop != null &&
            (entrance.game != null && entrance.game.held == entrance.prop || entrance.prop.placement != null && entrance.prop.placement.Held);
        bool NeedsAssistance => entrance != null && (CurrentScale < entrance.RequiredScale || Tilt > 1.5f);

        void LateUpdate()
        {
            if (Application.isPlaying && entrance != null && entrance.game != null && !entrance.game.active && !entrance.game.automated) return;
            Tick(Time.unscaledDeltaTime);
        }

        // Public for deterministic integration checks; normal play uses LateUpdate.
        public void Tick(float dt)
        {
            if (entrance == null || entrance.game == null || entrance.game.Transitioning) return;
            dt = Mathf.Clamp(dt, 0, .1f);
            if (Assisting)
            {
                AdvanceAssistance(dt);
                return;
            }
            RestorePreviewPolicyWhenReady();
            retryDelay = Mathf.Max(0, retryDelay - dt);
            bool candidate = CanConsiderAssistance();
            if (!candidate) { AssistanceBlocked = false; AssistanceReason = null; retryDelay = 0; }
            else if (NeedsAssistance && retryDelay <= 0)
            {
                if (CanAssistAtCurrentPlacement(out var reason))
                {
                    BeginAssistance();
                    AdvanceAssistance(dt);
                    return;
                }
                AssistanceBlocked = true; AssistanceReason = reason; retryDelay = .12f;
            }
            bool eligible = entrance.ScaleAndPlacementReady;
            if (!eligible) openingApproved = false;
            else if (!openingApproved) openingApproved = CanOpenAtCurrentPlacement();
            OpeningBlocked = eligible && !openingApproved;
            float target = eligible && !OpeningBlocked ? 1 : 0;
            float next = Mathf.MoveTowards(Progress, target, dt / duration);
            if (Mathf.Approximately(next, Progress)) return;
            SetProgress(next);
        }

        public void ResetToRest()
        {
            if (Assisting) CancelAssistance(false, PoseStillOwned());
            openingApproved = false; OpeningBlocked = AssistanceBlocked = false; AssistanceReason = null;
            retryDelay = 0; SetProgress(0); RestorePreviewPolicy();
        }

        bool CanConsiderAssistance()
        {
            return entrance != null && !entrance.Entering && entrance.game != null && entrance.prop != null &&
                entrance.prop.placement != null && entrance.prop.placement.Ready && !IsHeld && NearEnough &&
                Tilt <= maximumAssistTilt;
        }

        void TargetPose(out Vector3 scale, out Quaternion rotation)
        {
            // A tiny margin avoids floating-point rejection at exactly 2.7.
            float factor = CurrentScale < entrance.RequiredScale ? (entrance.RequiredScale + .006f) / Mathf.Max(.001f, CurrentScale) : 1;
            scale = transform.localScale * factor;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            rotation = forward.sqrMagnitude > .0001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : transform.rotation;
        }

        public bool CanAssistAtCurrentPlacement() { return CanAssistAtCurrentPlacement(out _); }

        public bool CanAssistAtCurrentPlacement(out string reason)
        {
            if (!CanConsiderAssistance()) { reason = "조각을 내려놓고 안착시켜 주세요"; return false; }
            TargetPose(out var targetScale, out var targetRotation);
            // Closed palms also matter: bringing an already-open sculpture
            // upright must not close its fingers around somebody in the gap.
            if (!ProbePose(transform.localScale, transform.rotation, false, out reason) ||
                !ProbePose(targetScale, targetRotation, false, out reason)) return false;
            return ProbePose(targetScale, targetRotation, true, out reason);
        }

        bool ProbePose(Vector3 scale, Quaternion rotation, bool fullyOpened, out string reason)
        {
            if (entrance == null || entrance.game == null || entrance.prop == null)
            { reason = "조각을 확인할 수 없습니다"; return false; }
            Vector3[] authoredCenters = fullyOpened ? openedCenters : closedCenters;
            if (movingParts == null || authoredCenters == null || authoredCenters.Length != movingParts.Length)
            { reason = "조각의 통로를 준비하고 있습니다"; return false; }
            Vector3 oldScale = transform.localScale; Quaternion oldRotation = transform.rotation;
            var body = entrance.prop.body; bool sleeping = body != null && !body.isKinematic && body.IsSleeping();
            if (probeCenters == null || movingParts == null || probeCenters.Length != movingParts.Length)
                probeCenters = movingParts == null ? Array.Empty<Vector3>() : new Vector3[movingParts.Length];
            try
            {
                transform.localScale = scale; transform.rotation = rotation;
                if (movingParts != null)
                    for (int i = 0; i < movingParts.Length; i++)
                        if (movingParts[i] != null) { probeCenters[i] = movingParts[i].center; movingParts[i].center = authoredCenters[i]; }
                // The existing complete-volume validator checks the museum
                // bounds, every physical part, other exhibits and the player.
                // No physics step or visible frame occurs at this probe pose.
                return entrance.game.ValidPlacement(entrance.prop, out reason);
            }
            finally
            {
                transform.localScale = oldScale; transform.rotation = oldRotation;
                if (movingParts != null)
                    for (int i = 0; i < movingParts.Length; i++) if (movingParts[i] != null) movingParts[i].center = probeCenters[i];
                Physics.SyncTransforms();
                if (sleeping && body != null && !body.isKinematic) body.Sleep();
            }
        }

        void BeginAssistance()
        {
            var prop = entrance.prop; var body = prop.body;
            assistancePosition = transform.position; assistanceStartScale = transform.localScale; assistanceStartRotation = transform.rotation;
            TargetPose(out assistanceTargetScale, out assistanceTargetRotation);
            lastAppliedScale = assistanceStartScale; lastAppliedRotation = assistanceStartRotation;
            oldPreviewUntilSettled = prop.placement.PreviewUntilSettled; ownsPreviewPolicy = true;
            prop.placement.PreviewUntilSettled = false;
            if (body != null)
            {
                wasKinematic = body.isKinematic; usedGravity = body.useGravity; wasSleeping = !wasKinematic && body.IsSleeping();
                oldVelocity = wasKinematic ? Vector3.zero : body.linearVelocity;
                oldAngularVelocity = wasKinematic ? Vector3.zero : body.angularVelocity;
                var policy = PropReleasePhysics.Attach(body); restorePhysicsProfile = policy.CaptureState(); policy.CancelPendingRelease();
                if (!wasKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.isKinematic = true; body.useGravity = false; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                ownedPhysics = true;
            }
            assistanceTime = 0; Assisting = true; AssistanceBlocked = OpeningBlocked = false; AssistanceReason = null;
            openingApproved = false; SetProgress(0);
        }

        bool PoseStillOwned()
        {
            return Vector3.Distance(transform.position, assistancePosition) < .002f &&
                Vector3.Distance(transform.localScale, lastAppliedScale) < .002f && Quaternion.Angle(transform.rotation, lastAppliedRotation) < .2f;
        }

        void AdvanceAssistance(float dt)
        {
            // Reset/regrab is a new owner of the prop; do not undo its pose or
            // its newly established hold/home physics flags.
            if (IsHeld || !PoseStillOwned()) { CancelAssistance(false, false); return; }
            assistanceTime = Mathf.Min(assistanceDuration, assistanceTime + dt);
            float t = Mathf.SmoothStep(0, 1, assistanceTime / assistanceDuration);
            Vector3 scale = Vector3.Lerp(assistanceStartScale, assistanceTargetScale, t);
            Quaternion rotation = Quaternion.Slerp(assistanceStartRotation, assistanceTargetRotation, t);
            if (!ProbePose(scale, rotation, false, out var reason) || !ProbePose(scale, rotation, true, out reason))
            {
                CancelAssistance(true, true); AssistanceBlocked = true; AssistanceReason = reason; retryDelay = .25f; return;
            }
            transform.localScale = scale; transform.rotation = rotation;
            var prop = entrance.prop;
            if (prop.body != null) { prop.body.position = transform.position; prop.body.rotation = rotation; }
            lastAppliedScale = scale; lastAppliedRotation = rotation;
            Physics.SyncTransforms();
            if (assistanceTime >= assistanceDuration)
            {
                Assisting = false; RestoreOwnedPhysics(false); prop.RefreshMass();
                // Resume ordinary gravity and support settling before the hand
                // animation or traversal is permitted.
                if (prop.body != null && prop.body.isKinematic) prop.placement.ResetPlaced(); else prop.placement.Released();
            }
        }

        void CancelAssistance(bool rollback, bool restoreOwner)
        {
            if (rollback && ProbePose(assistanceStartScale, assistanceStartRotation, false, out _))
            {
                transform.localScale = assistanceStartScale; transform.rotation = assistanceStartRotation;
                if (entrance != null && entrance.prop != null && entrance.prop.body != null)
                { entrance.prop.body.position = transform.position; entrance.prop.body.rotation = transform.rotation; }
            }
            Assisting = false;
            if (restoreOwner) RestoreOwnedPhysics(true);
            else { ownedPhysics = false; restorePhysicsProfile = null; }
            RestorePreviewPolicy();
            if (restoreOwner && entrance != null && entrance.prop != null)
            { entrance.prop.RefreshMass(); entrance.prop.placement.Released(); }
            Physics.SyncTransforms();
        }

        void RestoreOwnedPhysics(bool cancelled)
        {
            if (!ownedPhysics) return; ownedPhysics = false;
            var body = entrance != null && entrance.prop != null ? entrance.prop.body : null;
            if (body == null) { restorePhysicsProfile = null; return; }
            body.isKinematic = wasKinematic; body.useGravity = usedGravity;
            restorePhysicsProfile?.Invoke(); restorePhysicsProfile = null;
            if (!wasKinematic)
            {
                body.linearVelocity = cancelled ? oldVelocity : Vector3.zero;
                body.angularVelocity = cancelled ? oldAngularVelocity : Vector3.zero;
                if (cancelled && wasSleeping) body.Sleep(); else body.WakeUp();
            }
        }

        void RestorePreviewPolicyWhenReady()
        {
            if (entrance != null && entrance.prop != null && entrance.prop.placement != null &&
                (entrance.prop.placement.Ready || IsHeld)) RestorePreviewPolicy();
        }

        void RestorePreviewPolicy()
        {
            if (!ownsPreviewPolicy) return;
            if (entrance != null && entrance.prop != null && entrance.prop.placement != null)
            { entrance.prop.placement.PreviewUntilSettled = oldPreviewUntilSettled; ownsPreviewPolicy = false; }
        }

        void OnDisable() { if (Assisting) CancelAssistance(false, true); RestorePreviewPolicy(); }

        public bool CanOpenAtCurrentPlacement()
        {
            if (entrance == null || entrance.game == null || entrance.prop == null || movingParts == null ||
                openedCenters == null || openedCenters.Length != movingParts.Length) return false;
            if (probeCenters == null || probeCenters.Length != movingParts.Length) probeCenters = new Vector3[movingParts.Length];
            // Probe the complete opened envelope before animating near a wall.
            // Queries ignore this prop's own shapes; no physics step occurs
            // while the temporary authored centres are being inspected.
            for (int i = 0; i < movingParts.Length; i++)
                if (movingParts[i] != null) { probeCenters[i] = movingParts[i].center; movingParts[i].center = openedCenters[i]; }
            try { return entrance.game.ValidPlacement(entrance.prop, out _); }
            finally
            {
                for (int i = 0; i < movingParts.Length; i++) if (movingParts[i] != null) movingParts[i].center = probeCenters[i];
                Physics.SyncTransforms();
            }
        }

        void SetProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            float blend = Mathf.SmoothStep(0, 1, Progress);
            if (morphTargets != null)
                foreach (var renderer in morphTargets)
                    if (renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0)
                        renderer.SetBlendShapeWeight(0, blend * openedMorphWeight);
            if (movingParts != null && closedCenters != null && openedCenters != null)
                for (int i = 0; i < movingParts.Length && i < closedCenters.Length && i < openedCenters.Length; i++)
                    if (movingParts[i] != null) movingParts[i].center = Vector3.Lerp(closedCenters[i], openedCenters[i], blend);
            if (portal != null && closedOpening.size.x > 0 && openedOpening.size.x > 0)
                portal.apertureBounds = new Bounds(Vector3.Lerp(closedOpening.center, openedOpening.center, blend),
                    Vector3.Lerp(closedOpening.size, openedOpening.size, blend));
            Physics.SyncTransforms();
        }
    }
}
