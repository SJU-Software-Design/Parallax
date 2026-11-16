using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // The public class/key aliases are retained for the existing course/input
    // integration. The reward is now a separately verified physical sword:
    // balance -> brief statue light -> gravity drop -> pickup -> sword socket.
    // No audio is played here. Events are hooks for a later sound pass.
    public sealed class GalleryBottleBalanceKeyReward : MonoBehaviour
    {
        public enum RewardStage
        {
            WaitingBalance, HatchOpening, TrayExtending, KeyReady, Carrying,
            Inserting, Turning, Unlocked, Flashing, Falling, AssetUnavailable
        }
        public const float SwordLength = 1.05f, KeyLength = SwordLength;
        public const float InteractionDistance = 3.0f;
        public const float FlashSeconds = .65f, InsertSeconds = 1.50f;
        public const float InsertAlignmentFraction = .22f, TipClearance = .10f;
        public const float DoorReleaseDelaySeconds = .40f;
        // Compatibility constants only. No hatch, tray extension or key turn.
        public const float HatchSeconds = FlashSeconds, TraySeconds = .65f, TurnSeconds = 0;

        public RewardStage Stage { get; private set; }
        public bool Unlocked => Stage == RewardStage.Unlocked;
        public float SeatedTime { get; private set; }
        public bool DoorReleaseReady => Unlocked && SeatedTime >= DoorReleaseDelaySeconds;
        public bool Carrying => Stage == RewardStage.Carrying;
        public bool Busy => Stage == RewardStage.Inserting;
        public bool SwordAvailable => Stage == RewardStage.KeyReady;
        public bool KeyAvailable => SwordAvailable;
        public bool Available { get; private set; }
        public string AvailabilityReason { get; private set; }
        public bool TargetedKey { get; private set; }
        public bool TargetedLock { get; private set; }
        public bool Targeted => TargetedKey || TargetedLock;
        public float FlashProgress { get; private set; }
        public float ReleasePoseProgress { get; private set; }
        public float FallProgress { get; private set; }
        public float HatchProgress => FlashProgress;
        public float TrayProgress => FallProgress;
        public float InsertionProgress { get; private set; }
        public float AlignmentProgress => Mathf.Clamp01(InsertionProgress / InsertAlignmentFraction);
        public float BladeTravelProgress => Mathf.InverseLerp(InsertAlignmentFraction, 1, InsertionProgress);
        public float TurnProgress => Unlocked ? 1 : 0;
        public Transform SwordTransform => sword;
        public Transform KeyTransform => sword;
        public Transform HatchTransform => handAnchor;
        public Transform TrayTransform => catchPad;
        public Transform LockTransform => socket;
        public Rigidbody SwordBody => swordBody;
        public BoxCollider SwordPhysicalCollider => swordSolid;
        public Vector3 KeyPickupPosition => sword != null ? sword.position : Vector3.zero;
        public bool HasExternalKeyModel { get; private set; }
        public string KeySource => "Verified external Dike reward sword · see model credits";
        public int ReleaseCount { get; private set; }
        public int RecoveryCount { get; private set; }
        public bool FirstFallCompletedPhysically { get; private set; }
        public bool CarryPoseBlocked { get; private set; }
        public Vector3 ReleaseStartPosition { get; private set; }
        public float MaximumDropDistance { get; private set; }

        public event Action HatchOpened; // legacy alias: brief light completes
        public event Action TrayExtended; // legacy alias: sword has settled
        public event Action KeyTaken;
        public event Action KeyInserted;
        public event Action KeyTurned { add { } remove { } } // compatibility: no key-turn mechanic
        public event Action LockReleased;
        public event Action SwordReleased;
        public event Action SwordSettled;
        public event Action<float> SwordReleasePoseRequested;

        public string Status => !Available ? AvailabilityReason : Unlocked ?
            (DoorReleaseReady ? "검으로 봉인을 풀었어요 · 열린 문으로 이동하세요" :
                "검이 고정됐어요 · 봉인이 풀리고 있어요") : Busy ?
            "검을 받침대의 홈에 맞춰 넣고 있어요" : Carrying ?
            "검을 들었어요 · 방 가운데 대리석 받침대의 홈을 클릭하세요" : SwordAvailable ?
            "디케가 놓아준 검을 클릭해 집으세요" : Stage == RewardStage.Falling ?
            "검이 떨어지고 있어요 · 아래 받침에서 집을 수 있어요" : Stage == RewardStage.Flashing ?
            "균형을 되찾았어요 · 디케가 검을 놓아줍니다" :
            "저울의 수평을 2초간 유지하면 디케가 검을 놓아줘요";
        public string Hint => !Available ? AvailabilityReason : Carrying ?
            "방 가운데 대리석 받침대의 윗면을 겨냥해 클릭하세요.\n가까이 가져가면 위치와 각도를 맞춰 삽입해요.\nBackspace: 검을 회수 받침에 돌려놓기" : SwordAvailable ?
            "손 아래 받침에 내려온 검을 클릭하세요.\n검 손잡이와 같은 문양이 있는 출구 받침대에 사용할 수 있어요." : Status;

        GalleryBottleBalanceCourse course;
        GalleryGame game;
        GalleryBottleBalanceArt.View art;
        Transform sword, handAnchor, catchPad, socket;
        Rigidbody swordBody;
        BoxCollider swordPick, swordSolid, socketPick;
        Renderer[] swordRenderers = Array.Empty<Renderer>();
        Bounds swordBounds;
        Vector3 insertStart;
        Quaternion insertStartRotation;
        Vector3 heldLocalPosition;
        Quaternion heldLocalRotation;
        Transform heldParent;
        Vector3 swordWorldScale = Vector3.one;
        float fallElapsed, settleElapsed;
        readonly List<Renderer> flashRenderers = new List<Renderer>();
        readonly List<MaterialPropertyBlock> originalBlocks = new List<MaterialPropertyBlock>();
        MaterialPropertyBlock flashBlock;

        public static GalleryBottleBalanceKeyReward Install(GalleryBottleBalanceCourse course,
            Transform parent, GalleryBottleBalanceArt.View art, GalleryBottleBalanceDoor door)
        {
            if (course == null || parent == null || art == null || door == null)
                throw new ArgumentNullException("Sword reward needs the course, exhibit art and exit door.");
            var existing = course.GetComponent<GalleryBottleBalanceKeyReward>();
            if (existing != null) return existing;
            var reward = course.gameObject.AddComponent<GalleryBottleBalanceKeyReward>();
            reward.flashBlock = new MaterialPropertyBlock();
            reward.course = course; reward.game = course.game; reward.art = art;
            reward.handAnchor = art.rewardHandAnchor;
            reward.catchPad = art.rewardCatchPad;
            reward.socket = art.swordSocketAnchor;
            reward.PrepareSword();
            reward.PrepareSocket();
            reward.RememberStatueMaterials();
            reward.Reset();
            return reward;
        }

        void PrepareSword()
        {
            Available = false;
            AvailabilityReason = "검 보상 준비 중 · 여신상 원본에서 검의 독립 분리와 낙하를 확인해야 해요";
            if (handAnchor == null || catchPad == null || socket == null)
            { AvailabilityReason = "검 보상 준비 중 · 손·회수 받침·검 삽입구 연결이 필요해요"; return; }
            sword = art.verifiedRewardSword;
            if (sword == null)
            {
                var source = Resources.Load<GameObject>("BottleBalance/Dike/RewardSword");
                if (source == null)
                { AvailabilityReason = "검 보상 준비 중 · 정식 외부 검 원본이 아직 준비되지 않았어요"; return; }
                var root = new GameObject("Dike reward · independent external sword").transform;
                root.SetParent(handAnchor, false);
                var visual = Instantiate(source, root, false).transform;
                visual.name = "Verified external sword visual";
                Bounds sourceBounds;
                if (!TryLocalBounds(visual, out sourceBounds))
                { DestroyOwned(root.gameObject); AvailabilityReason = "검 원본에 검증할 메시가 없어요"; return; }
                // Import contract: sword length is local Y, grip at +Y, tip -Y.
                if (sourceBounds.size.y < Mathf.Max(sourceBounds.size.x, sourceBounds.size.z))
                { DestroyOwned(root.gameObject); AvailabilityReason = "검 원본의 Y축·손잡이 방향 정규화가 필요해요"; return; }
                float factor = SwordLength / Mathf.Max(.001f, sourceBounds.size.y);
                visual.localScale = Vector3.one * factor;
                visual.localPosition = -sourceBounds.center * factor;
                sword = root;
            }
            if (!TryLocalBounds(sword, out swordBounds) || swordBounds.size.magnitude < .05f)
            { AvailabilityReason = "검 메시의 실제 크기를 확인할 수 없어요"; return; }
            if (swordBounds.size.y < Mathf.Max(swordBounds.size.x, swordBounds.size.z))
            { AvailabilityReason = "검 파트의 Y축·손잡이 방향 정규화가 필요해요"; return; }
            HasExternalKeyModel = true;
            heldParent = sword.parent;
            heldLocalPosition = sword.localPosition; heldLocalRotation = sword.localRotation;
            swordWorldScale = sword.lossyScale;
            swordRenderers = sword.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in swordRenderers)
            { renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; }
            // The verified visual may carry imported colliders; do not let a
            // second collider or nested body pin the released sword to the hand.
            foreach (var collider in sword.GetComponentsInChildren<Collider>(true)) RemoveCollider(collider);
            foreach (var body in sword.GetComponentsInChildren<Rigidbody>(true))
                if (body.transform != sword) DestroyOwned(body);
            swordBody = sword.GetComponent<Rigidbody>();
            if (swordBody == null) swordBody = sword.gameObject.AddComponent<Rigidbody>();
            swordBody.mass = 2.8f;
            swordBody.interpolation = RigidbodyInterpolation.Interpolate;
            swordBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            swordBody.isKinematic = true; swordBody.useGravity = false;
            swordSolid = sword.gameObject.AddComponent<BoxCollider>();
            swordSolid.center = swordBounds.center;
            swordSolid.size = swordBounds.size + Vector3.one * .006f;
            swordSolid.enabled = false;
            swordPick = sword.gameObject.AddComponent<BoxCollider>();
            swordPick.center = swordBounds.center; swordPick.size = swordBounds.size + new Vector3(.09f, .05f, .09f);
            swordPick.isTrigger = true; swordPick.enabled = false;
            Available = art.SwordReleaseVerified;
            if (Available) AvailabilityReason = null;
        }

        void PrepareSocket()
        {
            if (socket == null) return;
            socketPick = socket.gameObject.AddComponent<BoxCollider>();
            socketPick.isTrigger = true;
            socketPick.center = new Vector3(0, .08f, 0);
            socketPick.size = new Vector3(.48f, .22f, .48f);
        }

        public void Reset()
        {
            Stage = Available ? RewardStage.WaitingBalance : RewardStage.AssetUnavailable;
            FlashProgress = FallProgress = InsertionProgress = ReleasePoseProgress = 0;
            SeatedTime = 0;
            fallElapsed = settleElapsed = 0;
            ReleaseCount = RecoveryCount = 0; MaximumDropDistance = 0;
            FirstFallCompletedPhysically = CarryPoseBlocked = false;
            TargetedKey = TargetedLock = false;
            StopPhysics();
            if (sword != null)
            {
                sword.SetParent(heldParent != null ? heldParent : handAnchor, false);
                sword.localPosition = heldLocalPosition; sword.localRotation = heldLocalRotation;
                RestoreWorldScale(); SetSwordVisible(true);
            }
            if (swordPick != null) swordPick.enabled = false;
            SetStatueFlash(0);
            SwordReleasePoseRequested?.Invoke(0);
            Physics.SyncTransforms();
        }

        public void CompleteBalance()
        {
            if (!Available || Stage != RewardStage.WaitingBalance) return;
            Stage = RewardStage.Flashing; FlashProgress = 0;
        }

        public void Tick(float dt)
        {
            if (game == null || !game.active || game.won || dt <= 0 || !Available) return;
            if (sword == null || swordBody == null)
            {
                Available = false; Stage = RewardStage.AssetUnavailable;
                AvailabilityReason = "검 원본 연결이 사라졌어요 · 전시실을 초기화해 주세요";
                SetStatueFlash(0); return;
            }
            if (Stage == RewardStage.Flashing)
            {
                FlashProgress = Mathf.Min(1, FlashProgress + dt / FlashSeconds);
                SetStatueFlash(Mathf.Sin(FlashProgress * Mathf.PI));
                ReleasePoseProgress = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.42f, 1, FlashProgress));
                SwordReleasePoseRequested?.Invoke(ReleasePoseProgress);
                if (FlashProgress >= 1)
                { SetStatueFlash(0); HatchOpened?.Invoke(); ReleaseSword(); }
            }
            else if (Stage == RewardStage.Falling) TickFall(dt);
            else if (Carrying) FollowView();
            else if (Busy) TickUse(dt);
            // The seated sword is already fixed and cannot be cancelled. Let
            // its impact read before the door mechanism starts moving.
            else if (Unlocked) SeatedTime += dt;
            else if (SwordAvailable && IsLost()) RecoverToPad(false);
            RefreshAim(game.view);
        }

        void ReleaseSword()
        {
            sword.SetParent(transform, true); RestoreWorldScale();
            swordPick.enabled = false; swordSolid.enabled = true;
            swordBody.isKinematic = false; swordBody.useGravity = true;
            swordBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            swordBody.linearVelocity = Vector3.zero;
            swordBody.angularVelocity = sword.right * .20f + sword.forward * .35f;
            swordBody.WakeUp();
            Stage = RewardStage.Falling; fallElapsed = settleElapsed = 0;
            ReleaseStartPosition = sword.position; ReleaseCount++;
            SwordReleased?.Invoke(); Physics.SyncTransforms();
        }

        void TickFall(float dt)
        {
            fallElapsed += dt;
            MaximumDropDistance = Mathf.Max(MaximumDropDistance, ReleaseStartPosition.y - sword.position.y);
            float expected = Mathf.Max(.1f, ReleaseStartPosition.y - catchPad.position.y);
            FallProgress = Mathf.Clamp01(MaximumDropDistance / expected);
            bool slow = swordBody.linearVelocity.sqrMagnitude < .035f && swordBody.angularVelocity.sqrMagnitude < .09f;
            settleElapsed = fallElapsed > .18f && slow && HasSupport() ? settleElapsed + dt : 0;
            if (settleElapsed >= .18f)
            {
                Stage = RewardStage.KeyReady; FallProgress = 1;
                swordPick.enabled = true;
                FirstFallCompletedPhysically = RecoveryCount == 0 && MaximumDropDistance > .08f;
                SwordSettled?.Invoke(); TrayExtended?.Invoke();
                game.Tell("디케가 놓아준 검을 집어 방 가운데 대리석 받침대에 꽂아 보세요.", 7);
            }
            else if (IsLost() || fallElapsed > 12)
                RecoverToPad(false);
        }

        bool HasSupport()
        {
            if (swordSolid == null) return false;
            Bounds b = swordSolid.bounds;
            float radius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * .75f, .012f, .08f);
            foreach (var hit in Physics.SphereCastAll(b.center, radius, Vector3.down,
                b.extents.y + .10f, ~0, QueryTriggerInteraction.Ignore))
                if (!IgnoreOwnOrPlayer(hit.collider)) return true;
            return false;
        }

        bool IsLost()
        {
            if (sword == null || catchPad == null) return true;
            Vector3 p = sword.position;
            return p.y < -1.0f || p.y > 8 || Mathf.Abs(p.x) > GalleryBottleBalanceLayout.HalfWidth + 1 ||
                p.z < GalleryBottleBalanceLayout.InsertAt - 1 || p.z > GalleryBottleBalanceLayout.EndZ + 1;
        }

        public void RefreshAim(Camera camera)
        {
            TargetedKey = CanPickupVisibleRay(camera);
            TargetedLock = Available && Carrying && VisibleRay(camera, socketPick, InteractionDistance);
        }
        public bool CanPickupVisibleRay(Camera camera) => Available && SwordAvailable && swordPick != null &&
            VisibleRay(camera, swordPick, InteractionDistance);

        public bool TryInteract(Camera camera)
        {
            if (game == null || !game.active || game.won || camera == null || !Available) return false;
            RefreshAim(camera);
            if (Busy) return true;
            if (Carrying)
            {
                if (TargetedLock && !CarryPoseBlocked) BeginUse();
                else game.Tell(CarryPoseBlocked ? "검을 움직일 공간이 필요해요 · 조금 물러나 주세요" :
                    "방 가운데 대리석 받침대의 윗면 홈을 겨냥해 클릭하세요.", 4);
                return true;
            }
            if (!TargetedKey || game.held != null) return false;
            StopPhysics(); swordPick.enabled = false;
            Stage = RewardStage.Carrying;
            sword.SetParent(transform, true); RestoreWorldScale();
            SetSwordVisible(true); FollowView(); KeyTaken?.Invoke();
            game.Tell("검을 집었어요. 방 가운데 대리석 받침대에 사용하세요. Backspace로 회수 받침에 돌려놓을 수 있어요.", 7);
            return true;
        }

        public bool Cancel()
        {
            if (!Carrying && !Busy) return false;
            RecoverToPad(true); game?.Tell("검을 회수 받침에 돌려놓았어요.", 4); return true;
        }
        public void ReturnKeyToTray() { if (!Unlocked && Available) RecoverToPad(true); }

        void RecoverToPad(bool requested)
        {
            if (sword == null || catchPad == null || Unlocked) return;
            StopPhysics(); sword.SetParent(transform, true); RestoreWorldScale();
            // A horizontal return lies on the recovery surface. This is an
            // explicit cancel/lost-object recovery, never the first-drop proof.
            Quaternion rotation = catchPad.rotation * Quaternion.Euler(0, 0, 90);
            Vector3 worldSize = Abs(swordWorldScale);
            float rise = Mathf.Max(swordBounds.extents.x * worldSize.x, swordBounds.extents.z * worldSize.z) + .025f;
            Vector3 centreOffset = rotation * Vector3.Scale(swordBounds.center, swordWorldScale);
            sword.SetPositionAndRotation(catchPad.position + Vector3.up * rise - centreOffset, rotation);
            swordSolid.enabled = true; swordPick.enabled = true;
            Stage = RewardStage.KeyReady; FlashProgress = FallProgress = 1; InsertionProgress = 0;
            SeatedTime = 0;
            TargetedKey = TargetedLock = CarryPoseBlocked = false; SetSwordVisible(true);
            if (!requested) { RecoveryCount++; game?.Tell("검이 회수 받침으로 돌아왔어요.", 4); }
            Physics.SyncTransforms();
        }

        void BeginUse()
        {
            Stage = RewardStage.Inserting; InsertionProgress = 0;
            SeatedTime = 0;
            insertStart = sword.position; insertStartRotation = sword.rotation;
            StopPhysics(); swordPick.enabled = false; sword.SetParent(transform, true);
            SetSwordVisible(true); game.Tell("검을 받침대의 홈에 맞춰 넣습니다.", 2);
        }

        void TickUse(float dt)
        {
            if (game.view == null || Vector3.Distance(game.view.transform.position, socket.position) > InteractionDistance + .6f)
            { RecoverToPad(true); return; }
            InsertionProgress = Mathf.Min(1, InsertionProgress + dt / InsertSeconds);
            // The insertion anchor is the TOP of the socket. Leave the hilt
            // above it; only the lower blade enters the receiving cavity.
            float scaleY = Mathf.Abs(swordWorldScale.y);
            float depth = Mathf.Min(.58f, swordBounds.size.y * scaleY * .58f);
            Vector3 aligned = socket.position + socket.up * (TipClearance - swordBounds.min.y * scaleY);
            Vector3 destination = socket.position - socket.up * (swordBounds.min.y * scaleY + depth);
            if (InsertionProgress < InsertAlignmentFraction)
            {
                // First stand the blade upright with its tip clear of the
                // opening. It should not enter the stone diagonally.
                float align = Mathf.SmoothStep(0, 1, AlignmentProgress);
                sword.SetPositionAndRotation(Vector3.Lerp(insertStart, aligned, align),
                    Quaternion.Slerp(insertStartRotation, socket.rotation, align));
            }
            else
            {
                // Keep the aligned axis fixed throughout the slower plunge;
                // zero velocity at the end gives the blade a gentle seat.
                float travel = Mathf.SmoothStep(0, 1, BladeTravelProgress);
                sword.SetPositionAndRotation(Vector3.Lerp(aligned, destination, travel), socket.rotation);
            }
            if (InsertionProgress < 1) return;
            sword.SetParent(socket, true); RestoreWorldScale();
            Stage = RewardStage.Unlocked; SeatedTime = 0;
            TargetedKey = TargetedLock = false;
            KeyInserted?.Invoke(); LockReleased?.Invoke();
            game.Tell("검이 홈에 고정됐어요. 봉인이 풀리며 문이 열립니다.", 6);
        }

        void FollowView()
        {
            if (game.view == null || sword == null) return;
            Transform camera = game.view.transform;
            Quaternion rotation = camera.rotation * Quaternion.Euler(5, -15, -12);
            Vector3 desired = camera.TransformPoint(new Vector3(.32f, -.22f, 1.02f));
            Vector3 delta = desired - camera.position;
            float distance = delta.magnitude, safeDistance = distance;
            Vector3 half = Vector3.Scale(swordSolid.size, Abs(swordWorldScale)) * .5f + Vector3.one * .012f;
            Vector3 centreOffset = rotation * Vector3.Scale(swordSolid.center, swordWorldScale);
            foreach (var hit in Physics.BoxCastAll(camera.position + centreOffset, half, delta.normalized,
                rotation, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IgnoreOwnOrPlayer(hit.collider)) continue;
                safeDistance = Mathf.Min(safeDistance, Mathf.Max(0, hit.distance - .03f));
            }
            Vector3 point = camera.position + delta.normalized * safeDistance;
            bool blocked = safeDistance < .55f || IntersectsPlayerKeepout(point, rotation, half);
            if (!blocked)
                foreach (var other in Physics.OverlapBox(point + centreOffset, half, rotation, ~0, QueryTriggerInteraction.Ignore))
                    if (!IgnoreOwnOrPlayer(other)) { blocked = true; break; }
            sword.SetPositionAndRotation(point, rotation); RestoreWorldScale();
            CarryPoseBlocked = blocked; SetSwordVisible(!blocked);
        }

        bool IntersectsPlayerKeepout(Vector3 point, Quaternion rotation, Vector3 half)
        {
            if (game.controller == null) return false;
            var controller = game.controller;
            Vector3 centre = controller.transform.TransformPoint(controller.center);
            float radius = controller.radius * Mathf.Max(Mathf.Abs(controller.transform.lossyScale.x), Mathf.Abs(controller.transform.lossyScale.z));
            float halfSegment = Mathf.Max(0, controller.height * Mathf.Abs(controller.transform.lossyScale.y) * .5f - radius);
            float swordRadius = Mathf.Max(half.x, half.z);
            for (int i = 0; i <= 8; i++)
            {
                Vector3 sample = point + rotation * (Vector3.Scale(swordBounds.center, swordWorldScale) + Vector3.up * Mathf.Lerp(-half.y, half.y, i / 8f));
                Vector3 closest = new Vector3(centre.x, Mathf.Clamp(sample.y, centre.y-halfSegment, centre.y+halfSegment), centre.z);
                if ((sample-closest).sqrMagnitude < (radius+swordRadius)*(radius+swordRadius)) return true;
            }
            return false;
        }

        bool IgnoreOwnOrPlayer(Collider collider) => collider == null || collider == game.controller ||
            (sword != null && collider.transform.IsChildOf(sword)) ||
            (game.controller != null && collider.transform.IsChildOf(game.controller.transform));

        bool VisibleRay(Camera camera, Collider target, float reach)
        {
            if (camera == null || target == null || !target.enabled) return false;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            if (!target.Raycast(ray, out var selected, reach)) return false;
            foreach (var hit in Physics.RaycastAll(ray, selected.distance + .01f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IgnoreOwnOrPlayer(hit.collider) || hit.collider == target) continue;
                if (hit.distance < selected.distance - .008f) return false;
            }
            return true;
        }

        void StopPhysics()
        {
            if (swordBody != null)
            {
                if (!swordBody.isKinematic)
                { swordBody.linearVelocity = Vector3.zero; swordBody.angularVelocity = Vector3.zero; }
                swordBody.isKinematic = true; swordBody.useGravity = false;
                swordBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
            if (swordSolid != null) swordSolid.enabled = false;
        }
        void RestoreWorldScale()
        {
            if (sword == null) return;
            Vector3 parentScale = sword.parent != null ? sword.parent.lossyScale : Vector3.one;
            sword.localScale = new Vector3(SafeRatio(swordWorldScale.x,parentScale.x),
                SafeRatio(swordWorldScale.y,parentScale.y),SafeRatio(swordWorldScale.z,parentScale.z));
        }
        static float SafeRatio(float a,float b) => Mathf.Abs(b)<.00001f?1:a/b;
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        void SetSwordVisible(bool visible) { foreach (var renderer in swordRenderers) if (renderer != null) renderer.enabled = visible; }

        void RememberStatueMaterials()
        {
            if (art.statueRenderers == null) return;
            foreach (var renderer in art.statueRenderers)
            {
                if (renderer == null) continue;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                flashRenderers.Add(renderer); originalBlocks.Add(block);
            }
        }
        void SetStatueFlash(float strength)
        {
            for (int i=0;i<flashRenderers.Count;i++)
            {
                var renderer = flashRenderers[i]; if (renderer == null) continue;
                if (strength <= .0001f) { renderer.SetPropertyBlock(originalBlocks[i]); continue; }
                renderer.GetPropertyBlock(flashBlock);
                flashBlock.SetColor("_EmissionColor", new Color(1,.64f,.20f,1) * strength * 1.7f);
                renderer.SetPropertyBlock(flashBlock);
            }
        }

        static bool TryLocalBounds(Transform root,out Bounds bounds)
        {
            bool first=true; bounds=new Bounds();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)continue;
                Bounds b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 q=root.InverseTransformPoint(filter.transform.TransformPoint(p));
                    if(first){bounds=new Bounds(q,Vector3.zero);first=false;}else bounds.Encapsulate(q);
                }
            }
            return !first;
        }
        static void RemoveCollider(Collider collider)
        { if(collider==null)return;collider.enabled=false;DestroyOwned(collider); }
        static void DestroyOwned(UnityEngine.Object item)
        { if(Application.isPlaying)Destroy(item);else DestroyImmediate(item); }
        void OnDisable() { SetStatueFlash(0); StopPhysics(); }
    }
}
