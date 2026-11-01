using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryProp : MonoBehaviour, IInteractionProp
    {
        public string title;
        public int zone;
        public float minScale = .45f, maxScale = 3.4f;
        public bool platform,mountedAtHome;
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
            StopMotion(); body.isKinematic = true; body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            transform.SetPositionAndRotation(homePosition, homeRotation); transform.localScale = homeScale;
            foreach (var p in parts) p.enabled = true;
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
            placement.Initialize(body,renderers,mountedAtHome);
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
            placement.BeginHold();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true; body.useGravity = false;
            // Held colliders remain queryable; their layer isolates solver contacts.
            foreach (var p in parts) p.enabled = true;
        }
        public void Release()
        {
            // The last preview pose IS the release pose. Only physics ownership changes.
            foreach (var p in parts) p.enabled = true;
            Physics.SyncTransforms();
            body.position = transform.position; body.rotation = transform.rotation;
            RefreshMass();
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
            properties.SetColor("_Selection", selected ? (invalid ? new Color(.55f, .065f, .025f) : new Color(.03f, .35f, .28f)) : Color.black);
            foreach (var r in renderers) r.SetPropertyBlock(properties);
        }
        Rigidbody IInteractionProp.Body => body;
        BoxCollider[] IInteractionProp.Parts => parts;
        PlacementSupport IInteractionProp.Placement => placement;
        float IInteractionProp.MinScale => minScale;
        // Legacy serialized maxScale values must not turn an open room into a
        // camera-relative carry distance. Gallery placement is bounded by actual
        // surfaces, full-volume checks and the finite room bounds instead.
        float IInteractionProp.MaxScale => float.PositiveInfinity;
        Action IInteractionProp.CaptureAuxiliaryState()
        {
            return PropReleasePhysics.Attach(body).CaptureState();
        }

    }
}
