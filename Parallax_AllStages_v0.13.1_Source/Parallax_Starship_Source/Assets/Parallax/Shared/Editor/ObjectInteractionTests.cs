using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Run in an isolated project copy with -executeMethod ShadeLink.Editor.ObjectInteractionTests.Run.
    public static class ObjectInteractionTests
    {
        [Serializable] sealed class Results
        {
            public List<string> passed = new List<string>();
            public List<string> failed = new List<string>();
        }

        sealed class Fixture : IDisposable
        {
            public readonly Component game, prop;
            public readonly Camera view;
            public readonly CharacterController controller;
            readonly List<GameObject> objects = new List<GameObject>();

            public Fixture(string stage, float cameraZ = 0)
            {
                var assembly = typeof(ShadeLink.Starship.StarshipGame).Assembly;
                var gameObject = New("Interaction test game");
                gameObject.SetActive(false); // Configure the real game without running its scene-specific Awake.
                game = gameObject.AddComponent(assembly.GetType("ShadeLink." + stage + "." + stage + "Game", true));
                controller = New("Interaction test player").AddComponent<CharacterController>();
                controller.height = 1.8f; controller.radius = .24f; controller.center = Vector3.up * .9f;
                controller.transform.position = new Vector3(0, 0, cameraZ);
                view = New("Interaction test camera").AddComponent<Camera>(); view.enabled = false;
                view.transform.position = new Vector3(0, 2, cameraZ);
                Set(game, "controller", controller); Set(game, "view", view); Set(game, "active", true);
                var lighting = gameObject.AddComponent(assembly.GetType("ShadeLink." + stage + "." + stage + "Lighting", true));
                Set(game, "lighting", lighting);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(cube);
                cube.name = "Interaction test prop"; cube.transform.position = new Vector3(0, 2, 5);
                prop = cube.AddComponent(assembly.GetType("ShadeLink." + stage + "." + stage + "Prop", true));
                Set(prop, "title", "Test prop"); Call(prop, "Remember"); Call(prop, "InitializePhysics");
                var props = Array.CreateInstance(prop.GetType(), 1); props.SetValue(prop, 0); Set(game, "props", props);
                Physics.SyncTransforms();
            }

            public GameObject New(string name) { var value = new GameObject(name); objects.Add(value); return value; }
            public GameObject Blocker(Vector3 position)
            {
                var value = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(value);
                value.name = "Interaction test blocker"; value.transform.position = position;
                value.transform.localScale = new Vector3(4, 4, .2f); Physics.SyncTransforms(); return value;
            }
            public void Grab()
            {
                Call(game, "Interact");
                Require(ReferenceEquals(Get(game, "held"), prop), "Visible prop was not picked up");
            }
            public void Dispose()
            {
                if (Get(game, "held") != null) Call(game, "Cancel", true);
                for (int i = objects.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(objects[i]);
                Physics.SyncTransforms();
            }
        }

        static object Get(object target, string name)
        {
            var type = target.GetType(); var field = type.GetField(name);
            return field != null ? field.GetValue(target) : type.GetProperty(name).GetValue(target);
        }
        static void Set(object target, string name, object value) { target.GetType().GetField(name).SetValue(target, value); }
        static object Call(object target, string name, params object[] arguments)
        {
            try { return target.GetType().GetMethod(name).Invoke(target, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static void Near(Vector3 actual, Vector3 expected, string message)
        { Require(Vector3.Distance(actual, expected) < .002f, message + ": " + actual + " != " + expected); }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run these scene fixtures in a separate batch-mode project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var result = new Results();
            foreach (string stage in new[] { "Gallery", "Library", "Beach", "Starship" })
            {
                Check(result, stage, "near pickup and cancel preserve pose and physics", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        var body = (Rigidbody)Get(f.prop, "body"); body.isKinematic = false; body.useGravity = true;
                        body.linearVelocity = new Vector3(.4f, .2f, -.3f); body.angularVelocity = new Vector3(.1f, .2f, .3f);
                        var position = f.prop.transform.position; var size = f.prop.transform.localScale;
                        f.Grab(); Call(f.game, "Cancel", false);
                        Require(Get(f.game, "held") == null, "Cancel retained the held prop");
                        Near(f.prop.transform.position, position, "Cancel changed pickup position");
                        Near(f.prop.transform.localScale, size, "Cancel changed pickup size");
                        Require(!body.isKinematic && body.useGravity, "Cancel lost dynamic physics ownership");
                        Near(body.linearVelocity, new Vector3(.4f, .2f, -.3f), "Cancel lost linear velocity");
                        Near(body.angularVelocity, new Vector3(.1f, .2f, .3f), "Cancel lost angular velocity");
                    }
                });
                Check(result, stage, "visible pickup beyond 100 metres", () =>
                {
                    using (var f = new Fixture(stage, -120))
                    {
                        var position = f.prop.transform.position;
                        f.Grab();
                        Require((float)Get(f.game, "referenceDistance") > 120, "Pickup used a short replacement ray");
                        Near(f.prop.transform.position, position, "Distant pickup snapped the prop towards the old distance cap");
                    }
                });
                Check(result, stage, "walls still block distant pickup", () =>
                {
                    using (var f = new Fixture(stage, -120))
                    {
                        f.Blocker(new Vector3(0, 2, -60)); Call(f.game, "Interact");
                        Require(Get(f.game, "held") == null, "Picked up a prop through a wall");
                    }
                });
                Check(result, stage, "placement sees surfaces beyond 48 metres", () =>
                {
                    using (var f = new Fixture(stage, -120))
                    {
                        f.Grab(); f.Blocker(new Vector3(0, 2, 12));
                        for (int i = 0; i < 20; i++) Call(f.game, "UpdateHeldPose");
                        Require((bool)Get(f.game, "HasPlacementSurface"), "Distant surface was not detected");
                        Require(f.prop.transform.position.z > 8, "Prop could not be placed towards the distant surface");
                        Require((float)Get(f.game, "holdDistance") > 120, "Placement retained a distance cap");
                    }
                });
                Check(result, stage, "drop preserves the displayed pose and enables gravity", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Grab(); var position = new Vector3(1, 3, 8); var size = Vector3.one * 1.1f;
                        var rotation = Quaternion.Euler(0, 15, 0);
                        f.prop.transform.SetPositionAndRotation(position, rotation); f.prop.transform.localScale = size;
                        // Starship refreshes its requested carry pose on release. Settle that
                        // existing solver before recording the pose the player actually sees.
                        if (stage == "Starship")
                        {
                            for (int i = 0; i < 20; i++) Call(f.game, "UpdateHeldPose");
                            position = f.prop.transform.position; size = f.prop.transform.localScale; rotation = f.prop.transform.rotation;
                        }
                        Physics.SyncTransforms(); Require((bool)Call(f.game, "Drop"), "Valid airborne drop was rejected");
                        Require(Get(f.game, "held") == null && (int)Get(f.game, "drops") == 1, "Drop did not clear the hand exactly once");
                        Near(f.prop.transform.position, position, "Release resolved a different position");
                        Near(f.prop.transform.localScale, size, "Release changed the displayed size");
                        Require(Quaternion.Angle(f.prop.transform.rotation, rotation) < .01f, "Release changed the displayed rotation");
                        var body = (Rigidbody)Get(f.prop, "body"); Require(!body.isKinematic && body.useGravity, "Drop did not enable gravity");
                    }
                });
                Check(result, stage, "blocked drop keeps the prop in hand", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Grab(); f.Blocker(f.prop.transform.position);
                        Require(!(bool)Call(f.game, "Drop"), "Released a prop overlapping a wall");
                        Require(ReferenceEquals(Get(f.game, "held"), f.prop), "Invalid release lost the held prop");
                    }
                });
                if (stage == "Beach" || stage == "Starship")
                    Check(result, stage, "cancel restores floating state and ball colliders", () =>
                    {
                        using (var f = new Fixture(stage))
                        {
                            var body = (Rigidbody)Get(f.prop, "body"); body.isKinematic = false;
                            Set(f.prop, "floating", true); Set(f.prop, "driftDistance", 12.5f);
                            Set(f.prop, "lastDriftPosition", new Vector3(1, 2, 3)); Set(f.prop, "isBall", true);
                            var sphere = f.prop.gameObject.AddComponent<SphereCollider>(); Set(f.prop, "sphere", sphere);
                            foreach (var part in (BoxCollider[])Get(f.prop, "parts")) part.enabled = false;
                            Physics.SyncTransforms(); f.Grab(); Call(f.game, "Cancel", false);
                            Require((bool)Get(f.prop, "floating") && (float)Get(f.prop, "driftDistance") == 12.5f, "Cancel lost water state");
                            Near((Vector3)Get(f.prop, "lastDriftPosition"), new Vector3(1, 2, 3), "Cancel lost drift origin");
                            Require(sphere.enabled, "Cancel did not restore the rolling ball collider");
                            foreach (var part in (BoxCollider[])Get(f.prop, "parts")) Require(!part.enabled, "Cancel enabled box colliders on a rolling ball");
                        }
                    });
            }
            Check(result, "Beach", "movable mirror body uses normal pickup", () =>
            {
                using (var f = new Fixture("Beach"))
                {
                    // The real beach mirror has its body collider under a mirror
                    // head, with the movable prop on the parent root.
                    f.prop.GetComponent<BoxCollider>().enabled = false;
                    var head = f.New("Interaction test mirror head");
                    head.transform.SetParent(f.prop.transform, false);
                    var mirror = head.AddComponent<ShadeLink.Beach.BeachMirror>();
                    head.AddComponent<BoxCollider>(); Set(f.game, "mirror", mirror);
                    Physics.SyncTransforms(); f.Grab();
                    Require(!(bool)Get(f.game, "adjustingMirror"), "Movable mirror entered angle adjustment instead of pickup");
                }
            });
            Check(result, "Shared", "dense rays retain the nearest target and wall", () =>
            {
                using (var f = new Fixture("Gallery"))
                {
                    for (int i = 0; i < 70; i++) f.Blocker(new Vector3(0, 2, 10 + i));
                    var hits = new RaycastHit[1];
                    Require(InteractionRaycast.Nearest(f.view.transform.position, f.view.transform.forward, f.controller,
                        null, ~0, ref hits, out var hit, out int queries), "Dense ray missed all colliders");
                    Require(hit.collider.GetComponentInParent<ShadeLink.Gallery.GalleryProp>() == f.prop,
                        "Buffer saturation hid the nearest prop");
                    Require(queries > 1 && hits.Length > 70, "Fixture did not exercise buffer growth");
                    var wall = f.Blocker(new Vector3(0, 2, 3));
                    Require(InteractionRaycast.Nearest(f.view.transform.position, f.view.transform.forward, f.controller,
                        null, ~0, ref hits, out hit, out _), "Dense ray missed the near wall");
                    Require(hit.collider.gameObject == wall, "Dense ray selected a prop through the near wall");
                }
            });
            string report = Path.Combine(Path.GetTempPath(), "parallax-object-interaction-results.json");
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-interactionReport");
            if (at >= 0 && at + 1 < args.Length) report = args[at + 1];
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            if (result.failed.Count > 0) throw new Exception("OBJECT_INTERACTION_TESTS_FAILED: " + result.failed.Count + "; " + report);
            Debug.Log("OBJECT_INTERACTION_TESTS_OK " + result.passed.Count + "; " + report);
        }
        static void Check(Results result, string stage, string name, Action test)
        {
            string label = stage + ": " + name;
            try { test(); result.passed.Add(label); Debug.Log("PASS " + label); }
            catch (Exception error) { result.failed.Add(label + " — " + error.Message); Debug.Log("FAIL " + label + " — " + error.Message); }
        }
    }
}
