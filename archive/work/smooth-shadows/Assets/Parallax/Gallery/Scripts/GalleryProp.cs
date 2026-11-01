using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryProp : MonoBehaviour
    {
        public string title;
        public int zone;
        public float minScale = .45f, maxScale = 3.4f;
        public bool platform;
        public Vector3 homePosition, homeScale;
        public Quaternion homeRotation;
        public BoxCollider[] parts;
        public Renderer[] renderers;
        public Rigidbody body;
        static PhysicsMaterial exhibitMaterial;
        public void Remember()
        {
            homePosition = transform.position; homeRotation = transform.rotation; homeScale = transform.localScale;
            parts = GetComponentsInChildren<BoxCollider>(); renderers = GetComponentsInChildren<Renderer>();
        }
        public void Restore()
        {
            InitializePhysics();
            StopMotion(); body.isKinematic = true; body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            transform.SetPositionAndRotation(homePosition, homeRotation); transform.localScale = homeScale;
            foreach (var p in parts) p.enabled = true;
            body.position = homePosition; body.rotation = homeRotation;
            RefreshMass();
            Highlight(false, false); Physics.SyncTransforms();
        }
        public void InitializePhysics()
        {
            if (body != null) return;
            body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            body.constraints = RigidbodyConstraints.None;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearDamping = .03f; body.angularDamping = .15f;
            body.solverIterations = 12; body.solverVelocityIterations = 6;
            if (exhibitMaterial == null)
                exhibitMaterial = new PhysicsMaterial("Exhibit contact") { staticFriction = .7f, dynamicFriction = .6f, bounciness = 0 };
            foreach (var p in parts) p.sharedMaterial = exhibitMaterial;
            RefreshMass();
        }
        void StopMotion()
        {
            if (body.isKinematic) return;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        }
        public void RefreshMass()
        {
            body.mass = Mathf.Clamp((platform ? 12f : 8f) * Mathf.Pow(transform.localScale.x, 3), .25f, 5000f);
            body.ResetCenterOfMass(); body.ResetInertiaTensor();
        }
        public void BeginHold()
        {
            InitializePhysics(); StopMotion();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true; body.useGravity = false;
            // Kinematic colliders keep other moving bodies from entering the held exhibit.
            // The placement solver explicitly ignores these colliders in its own rays/sweeps.
            foreach (var p in parts) p.enabled = true;
        }
        public void Release()
        {
            // The last preview pose IS the release pose. Only physics ownership changes.
            foreach (var p in parts) p.enabled = true;
            Physics.SyncTransforms();
            body.position = transform.position; body.rotation = transform.rotation;
            RefreshMass(); body.constraints = RigidbodyConstraints.None;
            body.isKinematic = false; body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.WakeUp();
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
            properties.SetColor("_EmissionColor", selected ? (invalid ? new Color(.18f,.025f,.01f) : new Color(.025f,.10f,.075f)) : Color.black);
            foreach (var r in renderers) r.SetPropertyBlock(properties);
        }
    }
}
