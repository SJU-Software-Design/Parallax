using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Reference-package regressions. Callable in either project with -executeMethod.
    public static class ReferencePhysicsTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly GameObject root;
            public readonly Component prop;
            public readonly Rigidbody body;
            public readonly PhysicsMaterial material;

            public Fixture(string stage, bool existing = false, bool assignBody = false, float scale = 1, bool platform = false)
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Cube);
                root.name = "Reference physics " + stage;
                root.transform.position = new Vector3(0, 20, 0);
                root.transform.localScale = Vector3.one * scale;
                if (existing)
                {
                    body = root.AddComponent<Rigidbody>();
                    body.mass = 13; body.linearDamping = .21f; body.angularDamping = .37f;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.constraints = RigidbodyConstraints.FreezeRotationY;
                    body.maxAngularVelocity = 9; body.solverIterations = 9; body.solverVelocityIterations = 3;
                    body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    material = new PhysicsMaterial("Explicit fixture contact") { staticFriction = .12f, dynamicFriction = .19f };
                    root.GetComponent<Collider>().sharedMaterial = material;
                }
                prop = root.AddComponent(typeof(Gallery.GalleryProp).Assembly.GetType("ShadeLink." + stage + "." + stage + "Prop", true));
                Set(prop, "platform", platform);
                if (assignBody) Set(prop, "body", body);
                Call(prop, "Remember"); Call(prop, "InitializePhysics");
                body = root.GetComponent<Rigidbody>();
            }
            public Component Helper => root.GetComponent(prop.GetType().Assembly.GetType("ShadeLink.PropReleasePhysics", true));
            public void Hold() { Call(prop, "BeginHold"); }
            public void Release() { Call(prop, "Release"); }
            public void Tick(float dt) { Call(Helper, "Tick", dt); }
            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                Physics.SyncTransforms();
            }
        }

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var failures = new List<string>(); int passed = 0;
            foreach (string stage in new[] { "Gallery", "Library", "Beach", "Starship" })
            {
                if (typeof(Gallery.GalleryProp).Assembly.GetType("ShadeLink." + stage + "." + stage + "Prop") == null) continue;
                Check(failures, ref passed, stage + " new bodies use reference damping and fallback contact", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        Near(f.body.linearDamping, 0, "New linear damping must be 0 (previous .03)");
                        Near(f.body.angularDamping, .05f, "New angular damping must be .05 (previous .15)");
                        Near(f.body.maxAngularVelocity, 7, "New max angular velocity");
                        Require(f.body.interpolation == RigidbodyInterpolation.None, "New interpolation");
                        Require(f.body.solverIterations == 6 && f.body.solverVelocityIterations == 1, "New solver iterations must be 6/1");
                        Require(f.root.GetComponent<Collider>().sharedMaterial == null, "Ordinary collider must keep engine fallback material");
                    }
                });
                foreach (bool assigned in new[] { false, true })
                    Check(failures, ref passed, stage + " explicit body preserved, assigned=" + assigned, () =>
                    {
                        using (var f = new Fixture(stage, true, assigned, 2))
                        {
                            Require(Get(f.prop, "placement") != null, "Assigned body skipped placement initialization");
                            Call(f.prop, "InitializePhysics");
                            Near(f.body.mass, 13, "Initialization changed explicit mass");
                            Near(f.body.linearDamping, .21f, "Explicit linear damping");
                            Near(f.body.angularDamping, .37f, "Explicit angular damping");
                            Near(f.body.maxAngularVelocity, 9, "Explicit max angular velocity");
                            Require(f.body.solverIterations == 9 && f.body.solverVelocityIterations == 3, "Explicit solver iterations");
                            Require(f.body.interpolation == RigidbodyInterpolation.Interpolate, "Explicit interpolation");
                            Require(f.root.GetComponent<Collider>().sharedMaterial == f.material, "Explicit material overwritten");
                            f.Hold(); f.Release();
                            Require(f.body.constraints == RigidbodyConstraints.FreezeRotationY, "Ordinary release overwrote explicit constraints");
                            Near(f.body.linearDamping, .21f, "Release changed explicit damping");
                            f.root.transform.localScale = Vector3.one * 4; Call(f.prop, "RefreshMass");
                            Near(f.body.mass, 104, "Custom mass must use saved reference-scale ratio cubed");
                            f.root.transform.localScale = Vector3.one * .001f; Call(f.prop, "RefreshMass"); Near(f.body.mass, .25f, "Mass lower safety limit");
                            f.root.transform.localScale = Vector3.one * 100; Call(f.prop, "RefreshMass"); Near(f.body.mass, 5000, "Mass upper safety limit");
                        }
                    });
                Check(failures, ref passed, stage + " new platform density is captured at its initial scale", () =>
                {
                    using (var f = new Fixture(stage, scale: 2, platform: true))
                    {
                        Near(f.body.mass, 96, "Initial platform density");
                        f.root.transform.localScale = Vector3.one; Call(f.prop, "RefreshMass");
                        Near(f.body.mass, 12, "Reference-scale platform ratio");
                    }
                });
                Check(failures, ref passed, stage + " original Discrete survives initial kinematic setup and regrab", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Hold(); f.Release(); f.Tick(.7f); f.Hold(); f.Release(); f.Tick(1.999f);
                        Require(f.body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, "CCD ended before two scaled seconds");
                        f.Tick(.002f);
                        Require(f.body.collisionDetectionMode == CollisionDetectionMode.Discrete, "Temporary speculative mode replaced original Discrete");
                        Require(f.body.linearVelocity == Vector3.zero && f.body.angularVelocity == Vector3.zero, "Release introduced throw or spin");
                    }
                });
                Check(failures, ref passed, stage + " cancel restores complete custom mass and inertia policy plus pending CCD", () =>
                {
                    using (var f = new Fixture(stage, true, true))
                    {
                        f.body.centerOfMass = new Vector3(.1f, .2f, .3f);
                        f.body.inertiaTensor = new Vector3(2, 3, 4); f.body.inertiaTensorRotation = Quaternion.Euler(10, 20, 30);
                        f.Hold(); f.Release(); f.Tick(.6f);
                        float mass = f.body.mass; Vector3 com = f.body.centerOfMass, tensor = f.body.inertiaTensor;
                        Quaternion inertiaRotation = f.body.inertiaTensorRotation;
                        bool automaticCom = Automatic(f.body, "automaticCenterOfMass"), automaticInertia = Automatic(f.body, "automaticInertiaTensor");
                        Action restore = CaptureAuxiliary(f.prop);
                        f.Hold(); f.root.transform.localScale *= 2; Call(f.prop, "RefreshMass");
                        Require(f.body.centerOfMass == com && f.body.inertiaTensor == tensor, "RefreshMass destroyed custom inertia/COM");
                        f.body.linearDamping = 4; f.body.angularDamping = 5; f.body.constraints = RigidbodyConstraints.FreezeAll;
                        f.body.interpolation = RigidbodyInterpolation.Extrapolate; f.body.maxAngularVelocity = 2;
                        f.body.solverIterations = 11; f.body.solverVelocityIterations = 5;
                        f.body.ResetCenterOfMass(); f.body.ResetInertiaTensor();
                        // The interaction restores pose, motion, gravity and the kinematic flag before this callback.
                        f.root.transform.localScale = Vector3.one; f.body.isKinematic = false; restore();
                        Near(f.body.mass, mass, "Cancel mass"); Near(f.body.linearDamping, .21f, "Cancel linear damping"); Near(f.body.angularDamping, .37f, "Cancel angular damping");
                        Near(f.body.maxAngularVelocity, 9, "Cancel angular speed limit");
                        Require(f.body.constraints == RigidbodyConstraints.FreezeRotationY && f.body.interpolation == RigidbodyInterpolation.Interpolate, "Cancel constraints/interpolation");
                        Require(f.body.solverIterations == 9 && f.body.solverVelocityIterations == 3, "Cancel solver policy");
                        Require(f.body.centerOfMass == com && f.body.inertiaTensor == tensor && Quaternion.Angle(f.body.inertiaTensorRotation, inertiaRotation) < .01f, "Cancel custom COM/inertia values");
                        Require(Automatic(f.body, "automaticCenterOfMass") == automaticCom && Automatic(f.body, "automaticInertiaTensor") == automaticInertia, "Cancel automatic COM/inertia flags");
                        f.Tick(1.399f); Require(f.body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, "Cancel lost remaining CCD time");
                        f.Tick(.002f); Require(f.body.collisionDetectionMode == CollisionDetectionMode.Continuous, "Cancel lost original CCD mode");
                    }
                });
                Check(failures, ref passed, stage + " held lifecycle restores original owned layers", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.root.layer = 2;
                        var child = new GameObject("Inactive owned child"); child.transform.SetParent(f.root.transform); child.layer = 4; child.SetActive(false);
                        var nested = GameObject.CreatePrimitive(PrimitiveType.Cube); nested.transform.SetParent(f.root.transform); nested.layer = 0;
                        nested.AddComponent<Rigidbody>().isKinematic = true;
                        nested.GetComponent<Collider>().enabled = false;
                        Call(f.prop, "Remember"); Call(f.prop, "InitializePhysics");
                        foreach (var part in (BoxCollider[])Get(f.prop, "parts"))
                            Require(part.GetComponentInParent<Rigidbody>(true) == f.body, "Prop parts include a nested independent body's collider");
                        Action restore = CaptureAuxiliary(f.prop); f.Hold();
                        int heldLayer = LayerMask.NameToLayer("HeldProp");
                        Require(heldLayer >= 0 && f.root.layer == heldLayer && child.layer == heldLayer, "Hold did not isolate owned layers including inactive children");
                        Require(nested.layer == 0, "Hold changed nested independent body layer");
                        Require(!nested.GetComponent<Collider>().enabled, "Hold enabled a nested independent body's collider");
                        restore(); Require(f.root.layer == 2 && child.layer == 4, "Cancel did not restore layers");
                        f.Hold(); f.Release(); Require(f.root.layer == 2 && child.layer == 4, "Release did not restore layers");
                        f.Hold(); Call(f.prop, "Restore"); Require(f.root.layer == 2 && child.layer == 4, "Home restore did not restore layers");
                        f.Hold(); f.root.SetActive(false);
                        // Edit-mode fixtures do not dispatch runtime MonoBehaviour
                        // lifecycle messages. Exercise the real disable callback explicitly.
                        var disable = f.Helper.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
                        Require(disable != null, "Release physics disable callback is missing");
                        disable.Invoke(f.Helper, null);
                        Require(f.root.layer == 2 && child.layer == 4, "Disable callback did not restore layers");
                    }
                });
            }
            Check(failures, ref passed, "real freefall acceleration is independent of mass", Freefall);
            Check(failures, ref passed, "release CCD catches a high speed cube on a thin floor", ThinFloor);
            if (failures.Count > 0) throw new Exception("REFERENCE_PHYSICS_TESTS_FAILED " + failures.Count + "\n" + string.Join("\n", failures));
            Debug.Log("REFERENCE_PHYSICS_TESTS_OK " + passed);
        }

        static void Freefall()
        {
            SimulationMode mode = Physics.simulationMode; Vector3 gravity = Physics.gravity;
            try
            {
                Physics.simulationMode = SimulationMode.Script; Physics.gravity = Vector3.down * 9.81f;
                using (var light = new Fixture("Gallery"))
                using (var heavy = new Fixture("Gallery"))
                {
                    light.root.transform.position = new Vector3(-2, 20, 0); heavy.root.transform.position = new Vector3(2, 20, 0);
                    light.Hold(); light.Release(); heavy.Hold(); heavy.Release(); light.body.mass = .25f; heavy.body.mass = 5000;
                    for (int i = 0; i < 50; i++) Physics.Simulate(.02f);
                    Near(light.body.position.y, heavy.body.position.y, "Mass changed gravitational acceleration");
                    Require(Mathf.Abs(light.body.linearVelocity.y + 9.81f) < .03f, "One-second freefall did not match gravity without damping");
                }
            }
            finally { Physics.simulationMode = mode; Physics.gravity = gravity; }
        }

        static void ThinFloor()
        {
            SimulationMode mode = Physics.simulationMode; Vector3 gravity = Physics.gravity;
            GameObject floor = null;
            try
            {
                Physics.simulationMode = SimulationMode.Script; Physics.gravity = Vector3.down * 9.81f;
                using (var f = new Fixture("Gallery"))
                {
                    floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.localScale = new Vector3(10, .04f, 10); floor.transform.position = Vector3.zero;
                    f.root.transform.position = new Vector3(0, 2, 0); f.Hold(); f.Release(); f.body.linearVelocity = Vector3.down * 200;
                    Physics.Simulate(.02f);
                    Require(f.body.position.y > .35f, "Released cube tunneled through a 4cm floor during CCD window");
                }
            }
            finally
            {
                if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
                Physics.simulationMode = mode; Physics.gravity = gravity;
            }
        }

        static bool Automatic(Rigidbody body, string name)
        {
            var property = typeof(Rigidbody).GetProperty(name); Require(property != null, "Missing Rigidbody " + name);
            return (bool)property.GetValue(body);
        }
        static object Call(object target, string name, params object[] args)
        {
            Require(target != null, "Missing target for " + name);
            var method = target.GetType().GetMethod(name); Require(method != null, "Missing method " + name);
            try { return method.Invoke(target, args); } catch (TargetInvocationException error) { throw error.InnerException; }
        }
        static object Get(object target, string name) { return target.GetType().GetField(name).GetValue(target); }
        static void Set(object target, string name, object value) { target.GetType().GetField(name).SetValue(target, value); }
        static Action CaptureAuxiliary(Component prop)
        {
            foreach (var contract in prop.GetType().GetInterfaces())
                if (contract.Name == "IInteractionProp") return (Action)contract.GetMethod("CaptureAuxiliaryState").Invoke(prop, null);
            throw new Exception("Missing interaction contract");
        }
        static void Near(float actual, float expected, string message) { Require(Mathf.Abs(actual - expected) < .001f, message + ": " + actual + " != " + expected); }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Check(List<string> failures, ref int passed, string name, Action test)
        {
            try { test(); passed++; Debug.Log("REFERENCE_PASS " + name); }
            catch (Exception error) { failures.Add(name + ": " + error.Message); Debug.Log("REFERENCE_FAIL " + name + ": " + error.Message); }
        }
    }
}
