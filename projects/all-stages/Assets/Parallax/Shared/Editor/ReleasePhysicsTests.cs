using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Run with -executeMethod ShadeLink.Editor.ReleasePhysicsTests.Run in either project.
    public static class ReleasePhysicsTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly GameObject root;
            public readonly Component prop;
            public readonly Rigidbody body;

            public Fixture(string stage,bool platform=false)
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Cube);
                root.name = "Release physics fixture";
                root.transform.position = new Vector3(0, 20, 0);
                prop = root.AddComponent(typeof(Gallery.GalleryProp).Assembly.GetType("ShadeLink." + stage + "." + stage + "Prop", true));
                Set(prop,"platform",platform);Call(prop, "Remember"); Call(prop, "InitializePhysics");
                body = root.GetComponent<Rigidbody>();
            }
            public Component Helper
            {
                get
                {
                    var type = prop.GetType().Assembly.GetType("ShadeLink.PropReleasePhysics");
                    Require(type != null, "The prop release timer/feedback helper is missing");
                    var helper = root.GetComponent(type);
                    Require(helper != null, "InitializePhysics did not attach release physics");
                    return helper;
                }
            }
            public void StartDynamic(CollisionDetectionMode mode)
            { body.isKinematic = false; body.collisionDetectionMode = mode; }
            public void Hold() { Call(prop, "BeginHold"); }
            public void Release() { Call(prop, "Release"); }
            public void Tick(float dt) { Call(Helper, "Tick", dt); }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); Physics.SyncTransforms(); }
        }

        static object Call(object target, string name, params object[] arguments)
        {
            try { return target.GetType().GetMethod(name).Invoke(target, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        static void Set(object target, string name, object value) { target.GetType().GetField(name).SetValue(target, value); }
        static Action CaptureAuxiliary(Component prop)
        {
            foreach (var contract in prop.GetType().GetInterfaces())
                if (contract.Name == "IInteractionProp")
                    return (Action)contract.GetMethod("CaptureAuxiliaryState").Invoke(prop, null);
            return (Action)Call(prop, "CaptureAuxiliaryState");
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Near(float actual, float expected, string message)
        { Require(Mathf.Abs(actual - expected) < .001f, message + ": " + actual + " != " + expected); }

        public static void Run()
        {
            var failures = new List<string>(); int passed = 0;
            var assembly = typeof(Gallery.GalleryProp).Assembly;
            foreach (string stage in new[] { "Gallery", "Library", "Beach", "Starship" })
            {
                if (assembly.GetType("ShadeLink." + stage + "." + stage + "Prop") == null) continue;
                Check(failures, ref passed, stage + " cubic mass keeps the existing safety limits", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.root.transform.localScale = new Vector3(2, 3, .5f); Call(f.prop, "RefreshMass");
                        Near(f.body.mass, 64, "Mass did not scale cubically with reference scale");
                        Set(f.prop, "platform", true); Call(f.prop, "RefreshMass");
                        Near(f.body.mass, 64, "Changing a label rewrote the saved original mass");
                        using(var platform=new Fixture(stage,true))
                        {
                            platform.root.transform.localScale=Vector3.one*2;Call(platform.prop,"RefreshMass");
                            Near(platform.body.mass,96,"Initial platform base mass changed");
                        }
                        f.root.transform.localScale = Vector3.one * .01f; Call(f.prop, "RefreshMass");
                        Near(f.body.mass, .25f, "Minimum mass changed");
                        f.root.transform.localScale = Vector3.one * 100; Call(f.prop, "RefreshMass");
                        Near(f.body.mass, 5000, "Maximum mass changed");
                    }
                });
                Check(failures, ref passed, stage + " release preserves pose and clears motion without a throw", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.StartDynamic(CollisionDetectionMode.Continuous);
                        f.body.linearVelocity = new Vector3(5, 2, -3); f.body.angularVelocity = Vector3.one;
                        f.Hold(); f.root.transform.position = new Vector3(3, 4, 5);
                        f.root.transform.rotation = Quaternion.Euler(0, 37, 0);
                        f.root.transform.localScale = new Vector3(1, 2, .5f);
                        Vector3 position = f.root.transform.position, scale = f.root.transform.localScale;
                        Quaternion rotation = f.root.transform.rotation; f.Release();
                        Require(!f.body.isKinematic && f.body.useGravity, "Release did not enable dynamic gravity");
                        Require(f.body.linearVelocity == Vector3.zero && f.body.angularVelocity == Vector3.zero,
                            "Release inherited motion or introduced a throw");
                        Require(f.root.transform.position == position && f.root.transform.localScale == scale &&
                            Quaternion.Angle(f.root.transform.rotation, rotation) < .001f, "Release changed the preview pose");
                    }
                });
                Check(failures, ref passed, stage + " CCD expires after two seconds to the pre-hold dynamic mode", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.StartDynamic(CollisionDetectionMode.Continuous); f.Hold(); f.Release();
                        Require(f.body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, "Release did not enable CCD");
                        f.Tick(.8f); f.Tick(1.199f);
                        Require(f.body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, "CCD expired early");
                        f.Tick(.002f);
                        Require(f.body.collisionDetectionMode == CollisionDetectionMode.Continuous, "CCD did not restore the previous dynamic mode");
                    }
                });
                Check(failures, ref passed, stage + " regrab cancels old CCD work and retains the original mode", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.StartDynamic(CollisionDetectionMode.Discrete); f.Hold(); f.Release(); f.Tick(.4f); f.Hold(); f.Tick(3);
                        Require(f.body.isKinematic && f.body.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative,
                            "The old release timer modified a held prop");
                        f.Release(); f.Tick(2.01f);
                        Require(f.body.collisionDetectionMode == CollisionDetectionMode.Discrete, "Regrab saved temporary CCD as the original mode");
                    }
                });
                Check(failures, ref passed, stage + " reset cancels CCD restoration", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.StartDynamic(CollisionDetectionMode.Discrete); f.Hold(); f.Release(); Call(f.prop, "Restore"); f.Tick(3);
                        Require(f.body.isKinematic && !f.body.useGravity &&
                            f.body.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative,
                            "A release timer modified a reset prop");
                    }
                });
                Check(failures, ref passed, stage + " cancel resumes the captured CCD time and collision mode", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.StartDynamic(CollisionDetectionMode.Discrete); f.Hold(); f.Release(); f.Tick(.5f);
                        var restore = CaptureAuxiliary(f.prop);
                        f.Hold(); f.Tick(5);
                        f.body.isKinematic = false; f.body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        restore(); f.Tick(1.49f);
                        Require(f.body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, "Cancel lost remaining CCD time");
                        f.Tick(.02f);
                        Require(f.body.collisionDetectionMode == CollisionDetectionMode.Discrete, "Cancel lost the original collision mode");
                        restore = CaptureAuxiliary(f.prop); f.Hold();
                        f.body.isKinematic = false; f.body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        restore();
                        Require(f.body.collisionDetectionMode == CollisionDetectionMode.Discrete, "Cancel without a timer changed the original mode");
                    }
                });
                Check(failures, ref passed, stage + " impact feedback ignores tiny contacts and throttles repeated impacts", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Hold(); f.Release();
                        Require(!(bool)Call(f.Helper, "PlayImpact", .249f), "Sub-threshold contact played impact audio");
                        Require((bool)Call(f.Helper, "PlayImpact", .25f), "Threshold impact was ignored");
                        Require(!(bool)Call(f.Helper, "PlayImpact", 4f), "Repeated contacts were not throttled");
                        f.Tick(.11f);
                        Require((bool)Call(f.Helper, "PlayImpact", 4f), "Impact feedback did not recover after its cooldown");
                        f.Hold(); f.Tick(.11f);
                        Require(!(bool)Call(f.Helper, "PlayImpact", 4f), "Held props produced impact audio");
                    }
                });
                Check(failures, ref passed, stage + " larger props have lower modest pickup and release audio", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Hold(); f.Release(); var source = f.root.GetComponent<AudioSource>();
                        Require(source != null, "Prop feedback has no audio source");
                        float pitch = source.pitch, volume = source.volume;
                        f.Hold(); f.root.transform.localScale = Vector3.one * 5; f.Release();
                        Require(source.pitch < pitch && source.volume >= volume && source.volume <= .25f,
                            "Feedback did not express mass at a modest volume");
                        Require(f.root.GetComponents<AudioSource>().Length == 1, "Repeated pickup added duplicate audio sources");
                    }
                });
                if (stage == "Beach" || stage == "Starship")
                {
                    Check(failures, ref passed, stage + " upright props keep their rotation constraints", () =>
                    {
                        using (var f = new Fixture(stage))
                        {
                            Set(f.prop, "upright", true); f.Hold(); f.Release();
                            Require(f.body.constraints == RigidbodyConstraints.FreezeRotation && f.body.angularVelocity == Vector3.zero,
                                "Upright puzzle props can rotate after release");
                        }
                    });
                    Check(failures, ref passed, stage + " balls switch colliders and begin water drift at release", () =>
                    {
                        using (var f = new Fixture(stage))
                        {
                            var sphere = f.root.AddComponent<SphereCollider>(); sphere.enabled = false;
                            Set(f.prop, "sphere", sphere); Set(f.prop, "isBall", true); Set(f.prop, "floating", true);
                            f.Hold(); f.root.transform.position = new Vector3(7, 8, 9); f.Release();
                            Require(sphere.enabled && !f.root.GetComponent<BoxCollider>().enabled, "Ball release lost sphere collision");
                            Require(!(bool)f.prop.GetType().GetField("floating").GetValue(f.prop), "Holding left the prop floating");
                            Require((Vector3)f.prop.GetType().GetField("lastDriftPosition").GetValue(f.prop) == f.root.transform.position,
                                "Hand movement was included in water travel");
                            f.Hold(); Require(!sphere.enabled && f.root.GetComponent<BoxCollider>().enabled, "Regrab did not restore box previews");
                        }
                    });
                }
            }
            if (failures.Count > 0) throw new Exception("RELEASE_PHYSICS_TESTS_FAILED " + failures.Count + "\n" + string.Join("\n", failures));
            Debug.Log("RELEASE_PHYSICS_TESTS_OK " + passed);
        }

        static void Check(List<string> failures, ref int passed, string name, Action test)
        {
            try { test(); passed++; Debug.Log("RELEASE_PASS " + name); }
            catch (Exception error) { failures.Add(name + ": " + error.Message); Debug.Log("RELEASE_FAIL " + name + ": " + error.Message); }
        }
    }
}
