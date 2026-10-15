using System;
using UnityEngine;

namespace ShadeLink
{
    public interface IInteractionProp
    {
        Rigidbody Body { get; }
        BoxCollider[] Parts { get; }
        PlacementSupport Placement { get; }
        Bounds WorldBounds { get; }
        float MinScale { get; }
        float MaxScale { get; }
        void InitializePhysics();
        void BeginHold();
        void Release();
        void RefreshMass();
        void Highlight(bool selected, bool invalid);
        Action CaptureAuxiliaryState();
    }

    // Maps retain their geometry, collision solver, special puzzles and typed public
    // fields. This adapter supplies only the operations needed by the shared lifecycle.
    public interface IObjectInteractionHost<T> where T : Component, IInteractionProp
    {
        Camera Camera { get; }
        CharacterController Controller { get; }
        T[] Props { get; }
        T Held { get; set; }
        T Aimed { get; set; }
        int InteractionMask { get; }
        bool CanInteract { get; }
        void AimChanged(RaycastHit hit);
        bool TrySpecialInteraction();
        void CancelSpecialInteraction();
        void StartPlacement();
        void PrepareRelease();
        bool ValidateRelease(out string reason);
        bool IntersectsPlayer(BoxCollider part);
        void FinishInteraction(bool released);
        void Tell(string message, float duration = 4);
    }

    public sealed class ObjectInteraction<T> where T : Component, IInteractionProp
    {
        readonly IObjectInteractionHost<T> host;
        RaycastHit[] hits = new RaycastHit[64];
        RaycastHit aimHit;
        Pickup pickup;
        PerspectivePlacement placement;
        bool placementFeasible;
        public bool PlacementFeasible => placementFeasible;
        public int PlacementQueries => placement==null?0:placement.Queries;
        Quaternion relativeRotation=Quaternion.identity;
        public float ReferenceDistance { get; private set; }
        public float ReferenceScale { get; private set; }
        public Vector3 GrabLocalPoint { get; private set; }
        public float Depth { get; set; }
        public Quaternion Rotation
        {
            get => host.Camera.transform.rotation*relativeRotation;
            set => relativeRotation=Quaternion.Inverse(host.Camera.transform.rotation)*value;
        }

        sealed class Pickup
        {
            public Vector3 position, scale, velocity, angularVelocity;
            public Quaternion rotation;
            public bool kinematic, gravity, sleeping;
            public PlacementSupport.Snapshot placement;
            public Collider[] colliders;
            public bool[] enabled;
            public Action restoreAuxiliary;
        }

        public ObjectInteraction(IObjectInteractionHost<T> host) { this.host = host; }

        public void UpdateAim()
        {
            T previous = host.Aimed; host.Aimed = null; aimHit = default;
            if (host.Held == null && InteractionRaycast.Nearest(host.Camera.transform.position,
                host.Camera.transform.forward, host.Controller, null, host.InteractionMask, ref hits, out aimHit, out _))
                host.Aimed = aimHit.collider.GetComponentInParent<T>();
            if (previous != null && previous != host.Held && previous != host.Aimed) previous.Highlight(false, false);
            if (host.Aimed != null) host.Aimed.Highlight(true, false);
            host.AimChanged(aimHit);
        }

        public void Interact()
        {
            if (!host.CanInteract) return;
            if (host.Held != null) { Drop(); return; }
            UpdateAim();
            if (host.TrySpecialInteraction() || host.Aimed == null) return;
            if (Physics.Raycast(host.Controller.transform.position + Vector3.up * .15f, Vector3.down,
                out var support, .3f, host.InteractionMask, QueryTriggerInteraction.Ignore) &&
                support.collider.GetComponentInParent<T>() == host.Aimed)
            { host.Tell("서 있는 사물은 잡을 수 없습니다. 먼저 바닥이나 다른 발판으로 이동하세요."); return; }
            foreach (var prop in host.Props)
            {
                if (prop == host.Aimed) continue;
                var bounds = prop.WorldBounds;
                if (Physics.Raycast(new Vector3(bounds.center.x, bounds.min.y + .04f, bounds.center.z), Vector3.down,
                    out var below, .14f, host.InteractionMask, QueryTriggerInteraction.Ignore) &&
                    below.collider.GetComponentInParent<T>() == host.Aimed)
                { host.Tell("위에 놓인 사물을 먼저 옮겨 주세요."); return; }
            }

            host.Held = host.Aimed;
            var held = host.Held; held.InitializePhysics();
            var body = held.Body;
            pickup = new Pickup
            {
                position = held.transform.position, rotation = held.transform.rotation, scale = held.transform.localScale,
                velocity = body.linearVelocity, angularVelocity = body.angularVelocity,
                kinematic = body.isKinematic, gravity = body.useGravity, sleeping = body.IsSleeping(),
                placement = held.Placement.Capture(), colliders = held.GetComponentsInChildren<Collider>(),
                restoreAuxiliary = held.CaptureAuxiliaryState()
            };
            pickup.enabled = new bool[pickup.colliders.Length];
            for (int i = 0; i < pickup.colliders.Length; i++) pickup.enabled[i] = pickup.colliders[i].enabled;
            GrabLocalPoint = held.transform.InverseTransformPoint(aimHit.point);
            ReferenceScale = held.transform.localScale.x;
            ReferenceDistance = Mathf.Max(.01f, aimHit.distance);
            Depth = ReferenceDistance; Rotation = held.transform.rotation;placementFeasible=true;
            placement=new PerspectivePlacement(held.transform,held.Parts,GrabLocalPoint,ReferenceScale,ReferenceDistance);
            host.StartPlacement();
        }

        public bool Drop()
        {
            var held = host.Held;
            if (held == null) return false;
            host.PrepareRelease();
            if(!placementFeasible)
            { held.Highlight(true,true);host.Tell("현재 크기로 조준한 위치에 놓을 공간이 없습니다.");return false; }
            if (!host.ValidateRelease(out var reason))
            { held.Highlight(true, true); host.Tell(reason); return false; }
            held.Release(); Finish(true); return true;
        }

        public void Cancel(bool force = false)
        {
            host.CancelSpecialInteraction();
            var held = host.Held;
            if (held == null) return;
            var currentPosition = held.transform.position; var currentRotation = held.transform.rotation;
            var currentScale = held.transform.localScale;
            held.transform.SetPositionAndRotation(pickup.position, pickup.rotation); held.transform.localScale = pickup.scale;
            if (!force)
                foreach (var part in held.Parts)
                    if (host.IntersectsPlayer(part))
                    {
                        held.transform.SetPositionAndRotation(currentPosition, currentRotation); held.transform.localScale = currentScale;
                        Physics.SyncTransforms(); host.Tell("잡기 전 위치에 서 있습니다. 옆으로 이동한 뒤 취소하세요."); return;
                    }
            for (int i = 0; i < pickup.colliders.Length; i++) pickup.colliders[i].enabled = pickup.enabled[i];
            var body = held.Body; body.position = pickup.position; body.rotation = pickup.rotation; held.RefreshMass();
            body.isKinematic = pickup.kinematic; body.useGravity = pickup.gravity;
            body.collisionDetectionMode = pickup.kinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
            if (!pickup.kinematic)
            {
                body.linearVelocity = pickup.velocity; body.angularVelocity = pickup.angularVelocity;
                if (pickup.sleeping) body.Sleep(); else body.WakeUp();
            }
            pickup.restoreAuxiliary?.Invoke(); held.Placement.Restore(pickup.placement); Finish(false);
        }

        void Finish(bool released)
        {
            host.Held.Highlight(false, false); host.Held = null; host.Aimed = null; pickup = null;placement=null;
            Physics.SyncTransforms(); host.FinishInteraction(released);
        }

        public PerspectivePlacement.Result ResolvePlacement(Vector3 minimum,Vector3 maximum,float maximumIntent=float.PositiveInfinity)
        {
            var result=placement.Resolve(host.Camera,Rotation,host.Controller,host.InteractionMask,host.Held.MinScale,host.Held.MaxScale,
                minimum,maximum,Depth,maximumIntent);
            Depth=result.Depth;placementFeasible=result.Feasible;return result;
        }

        public bool TryApplyPlacement()
        {
            if(!placementFeasible)return false;
            var prop=host.Held;Vector3 position=prop.transform.position,size=prop.transform.localScale;
            Quaternion rotation=prop.transform.rotation,requested=Rotation;
            float scale=ReferenceScale*Depth/ReferenceDistance;
            prop.transform.localScale=Vector3.one*scale;
            prop.transform.SetPositionAndRotation(host.Camera.transform.position+host.Camera.transform.forward*Depth-
                requested*(GrabLocalPoint*scale),requested);
            Physics.SyncTransforms();
            if(host.ValidateRelease(out _))return true;
            prop.transform.localScale=size;prop.transform.SetPositionAndRotation(position,rotation);Physics.SyncTransforms();
            return false;
        }

        public float ResolveDepth(Vector3 minimum, Vector3 maximum,
            out bool hasSurface, float maximumIntent = float.PositiveInfinity)
        {
            var result=ResolvePlacement(minimum,maximum,maximumIntent);hasSurface=result.HasSurface;return result.Depth;
        }
    }
}
