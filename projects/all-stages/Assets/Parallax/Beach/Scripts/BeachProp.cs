using System;
using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachProp : MonoBehaviour, IInteractionProp
    {
        public string title;
        public int zone;
        public float minScale = .45f, maxScale = 3.4f;
        public bool platform, buoyant, isBall, upright, floating;
        public float driftDistance;
        public Vector3 lastDriftPosition;
        public SphereCollider sphere;
        public Vector3 homePosition, homeScale;
        public Quaternion homeRotation;
        public BoxCollider[] parts;
        public Renderer[] renderers;
        public Rigidbody body;
        public PlacementSupport placement;
        public void Remember()
        {
            homePosition = transform.position; homeRotation = transform.rotation; homeScale = transform.localScale;
            parts = GetComponentsInChildren<BoxCollider>(); renderers = GetComponentsInChildren<Renderer>();
        }
        public void Restore()
        {
            InitializePhysics(); PropReleasePhysics.Attach(body).CancelPendingRelease();
            HeldCollisionState.Attach(body).Restore();
            floating=false;driftDistance=0;
            StopMotion(); body.isKinematic = true; body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            transform.SetPositionAndRotation(homePosition, homeRotation); transform.localScale = homeScale;
            foreach (var p in parts) p.enabled = true;
            if(sphere!=null)sphere.enabled=false;
            body.position = homePosition; body.rotation = homeRotation;
            RefreshMass();
            Highlight(false, false); Physics.SyncTransforms();placement.ResetPlaced();
        }
        public void InitializePhysics()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            bool createdBody = body == null;
            if (createdBody) body = gameObject.AddComponent<Rigidbody>();
            if (parts == null) parts = GetComponentsInChildren<BoxCollider>();
            parts = Array.FindAll(parts, part => part != null && part.GetComponentInParent<Rigidbody>(true) == body);
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
            // Capture original CCD and mass before the temporary home/held modes.
            var physics = PropReleasePhysics.Attach(body);
            physics.InitializeProfile(createdBody, (platform ? 12f : 8f) * Mathf.Pow(transform.localScale.x, 3));
            if (createdBody)
            {
                body.isKinematic = true; body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
            placement=GetComponent<PlacementSupport>();if(placement==null)placement=gameObject.AddComponent<PlacementSupport>();
            placement.Initialize(body,renderers);
        }
        void StopMotion()
        {
            if (body.isKinematic) return;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        }
        public void RefreshMass()
        {
            PropReleasePhysics.Attach(body).RefreshMass();
        }
        public void BeginHold()
        {
            InitializePhysics(); PropReleasePhysics.Attach(body).BeginHold(); StopMotion();
            placement.BeginHold();floating=false;driftDistance=0;lastDriftPosition=transform.position;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true; body.useGravity = false;
            // Held colliders remain queryable; their layer isolates solver contacts.
            foreach (var p in parts) p.enabled = true;
            if(sphere!=null)sphere.enabled=false;
        }
        public void Release()
        {
            // Start current-driven travel at the release pose, excluding every
            // centimetre moved by the player's hand before release.
            lastDriftPosition=transform.position;
            // The last preview pose IS the release pose. Only physics ownership changes.
            foreach (var p in parts) p.enabled = true;
            if(sphere!=null)sphere.enabled=false;
            Physics.SyncTransforms();
            body.position = transform.position; body.rotation = transform.rotation;
            RefreshMass(); if (upright) body.constraints |= RigidbodyConstraints.FreezeRotation;
            if(isBall&&sphere!=null){foreach(var part in parts)part.enabled=false;sphere.enabled=true;}
            PropReleasePhysics.Attach(body).Release();
            placement.Released();
        }
        public Bounds WorldBounds
        {
            get
            {
                Bounds b = new Bounds(transform.position, Vector3.zero); bool first = true;
                foreach (var p in parts)
                {
                    Vector3 half = p.size * .5f;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 v = p.transform.TransformPoint(p.center + Vector3.Scale(half,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                        if (first) { b = new Bounds(v, Vector3.zero); first = false; } else b.Encapsulate(v);
                    }
                }
                return b;
            }
        }
        public void Highlight(bool selected, bool invalid)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_EmissionColor", selected ? (invalid ? new Color(.55f, .065f, .025f) : new Color(.025f, .16f, .24f)) : Color.black);
            foreach (var r in renderers) r.SetPropertyBlock(properties);
        }
        Rigidbody IInteractionProp.Body => body;
        BoxCollider[] IInteractionProp.Parts => parts;
        PlacementSupport IInteractionProp.Placement => placement;
        float IInteractionProp.MinScale => minScale;
        float IInteractionProp.MaxScale => maxScale;
        Action IInteractionProp.CaptureAuxiliaryState()
        {
            bool wasFloating=floating;float drift=driftDistance;Vector3 last=lastDriftPosition;
            Action restoreRelease=PropReleasePhysics.Attach(body).CaptureState();
            return ()=>{floating=wasFloating;driftDistance=drift;lastDriftPosition=last;restoreRelease();};
        }

    }
}
