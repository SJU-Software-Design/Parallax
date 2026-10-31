using System;
using UnityEngine;

namespace ShadeLink
{
    // Owns each body's mass reference, reversible physics policy and release CCD
    // window. Puzzle, support, collider and buoyancy rules stay with each prop.
    public sealed class PropReleasePhysics : MonoBehaviour
    {
        Rigidbody body;
        CollisionDetectionMode previousDynamicMode;
        bool profileInitialized;
        float originalMass, initialLocalScaleX;
        float ccdRemaining, impactCooldown;
        AudioSource source;
        static AudioClip contactClip;

        public static PropReleasePhysics Attach(Rigidbody body)
        {
            var value = body.GetComponent<PropReleasePhysics>();
            if (value == null) value = body.gameObject.AddComponent<PropReleasePhysics>();
            if (value.body == null)
            {
                value.body = body;
                value.previousDynamicMode = body.collisionDetectionMode;
            }
            return value;
        }

        public void InitializeProfile(bool createdBody, float fallbackMass)
        {
            if (profileInitialized) return;
            if (createdBody)
            {
                body.mass = Mathf.Clamp(fallbackMass, .25f, 5000f);
                body.linearDamping = 0; body.angularDamping = .05f;
                body.interpolation = RigidbodyInterpolation.None;
                body.maxAngularVelocity = 7;
                body.solverIterations = 6; body.solverVelocityIterations = 1;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                previousDynamicMode = CollisionDetectionMode.Discrete;
            }
            originalMass = body.mass;
            initialLocalScaleX = transform.localScale.x;
            if (Mathf.Abs(initialLocalScaleX) < .000001f) initialLocalScaleX = 1;
            profileInitialized = true;
        }

        public void RefreshMass()
        {
            if (!profileInitialized) InitializeProfile(false, body.mass);
            body.mass = Mathf.Clamp(originalMass * Mathf.Pow(transform.localScale.x / initialLocalScaleX, 3), .25f, 5000f);
            // Recalculate engine-owned properties after resizing, while leaving
            // authored centres of mass and inertia tensors under their owner's control.
            if (body.automaticCenterOfMass) body.ResetCenterOfMass();
            if (body.automaticInertiaTensor) body.ResetInertiaTensor();
        }

        public void BeginHold()
        {
            // Regrabbing during CCD must retain its underlying mode, rather
            // than saving the temporary ContinuousDynamic mode as the default.
            if (ccdRemaining <= 0 && !body.isKinematic) previousDynamicMode = body.collisionDetectionMode;
            CancelPendingRelease();
            HeldCollisionState.Attach(body).Begin();
            PlayCue(1.18f, .75f);
        }

        public void Release()
        {
            HeldCollisionState.Attach(body).Restore();
            body.isKinematic = false; body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.WakeUp(); ccdRemaining = 2f;
            PlayCue(.9f, 1f);
        }

        public void CancelPendingRelease()
        {
            ccdRemaining = 0; impactCooldown = 0;
            if (source != null && Application.isPlaying) source.Stop();
        }

        // Capture before BeginHold. Invoke after the interaction restores the
        // Rigidbody's pose, kinematic/gravity flags and velocities on cancel.
        // Restore all mutable physics policy and resume a pending CCD window.
        public Action CaptureState()
        {
            float remaining = ccdRemaining;
            CollisionDetectionMode previous = previousDynamicMode, mode = body.collisionDetectionMode;
            float mass = body.mass, damping = body.linearDamping, angularDamping = body.angularDamping;
            float angularLimit = body.maxAngularVelocity;
            RigidbodyConstraints constraints = body.constraints;
            RigidbodyInterpolation interpolation = body.interpolation;
            int solver = body.solverIterations, velocitySolver = body.solverVelocityIterations;
            bool automaticCom = body.automaticCenterOfMass, automaticInertia = body.automaticInertiaTensor;
            Vector3 center = body.centerOfMass, inertia = body.inertiaTensor;
            Quaternion inertiaRotation = body.inertiaTensorRotation;
            return () =>
            {
                HeldCollisionState.Attach(body).Restore();
                CancelPendingRelease(); previousDynamicMode = previous;
                body.mass = mass; body.linearDamping = damping; body.angularDamping = angularDamping;
                body.maxAngularVelocity = angularLimit;
                body.constraints = constraints; body.interpolation = interpolation;
                body.solverIterations = solver; body.solverVelocityIterations = velocitySolver;
                body.automaticCenterOfMass = automaticCom;
                if (!automaticCom) body.centerOfMass = center;
                body.automaticInertiaTensor = automaticInertia;
                if (!automaticInertia) { body.inertiaTensor = inertia; body.inertiaTensorRotation = inertiaRotation; }
                ccdRemaining = body.isKinematic ? 0 : remaining;
                body.collisionDetectionMode = mode;
            };
        }

        public void Tick(float dt)
        {
            dt = Mathf.Max(0, dt); impactCooldown = Mathf.Max(0, impactCooldown - dt);
            if (ccdRemaining <= 0) return;
            if (body == null || body.isKinematic) { CancelPendingRelease(); return; }
            ccdRemaining = Mathf.Max(0, ccdRemaining - dt);
            if (ccdRemaining <= 0 && body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic)
                body.collisionDetectionMode = previousDynamicMode;
        }
        void Update() { Tick(Time.deltaTime); }

        public bool PlayImpact(float relativeSpeed)
        {
            if (body == null || body.isKinematic || relativeSpeed < .25f || impactCooldown > 0) return false;
            impactCooldown = .1f;
            PlayCue(.8f, Mathf.Clamp(relativeSpeed * .12f, .4f, 1.25f));
            return true;
        }
        void OnCollisionEnter(Collision collision) { PlayImpact(collision.relativeVelocity.magnitude); }

        void PlayCue(float pitchMultiplier, float volumeMultiplier)
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false; source.spatialBlend = 1; source.dopplerLevel = 0;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 1; source.maxDistance = 30; source.priority = 170;
            }
            float mass = Mathf.Log(Mathf.Max(.25f, body.mass) / 8f, 2);
            source.pitch = Mathf.Clamp(1.05f - mass * .055f, .65f, 1.35f) * pitchMultiplier;
            source.volume = Mathf.Clamp(.1f + mass * .009f, .06f, .18f) * volumeMultiplier;
            if (!Application.isPlaying) return;
            if (contactClip == null)
            {
                const int sampleRate = 22050, count = 1544;
                var samples = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / sampleRate, progress = (float)i / (count - 1);
                    float envelope = Mathf.Sin(Mathf.PI * progress) * Mathf.Exp(-progress * 7);
                    samples[i] = envelope * (Mathf.Sin(t * 2 * Mathf.PI * 240) * .7f + Mathf.Sin(t * 2 * Mathf.PI * 487) * .3f);
                }
                contactClip = AudioClip.Create("Parallax soft prop contact", count, 1, sampleRate, false);
                contactClip.hideFlags = HideFlags.HideAndDontSave; contactClip.SetData(samples, 0);
            }
            source.PlayOneShot(contactClip);
        }

        void OnDisable()
        {
            var held = GetComponent<HeldCollisionState>();
            if (held != null) held.Restore();
            if (body != null && !body.isKinematic && ccdRemaining > 0 &&
                body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic)
                body.collisionDetectionMode = previousDynamicMode;
            CancelPendingRelease();
        }
    }
}
