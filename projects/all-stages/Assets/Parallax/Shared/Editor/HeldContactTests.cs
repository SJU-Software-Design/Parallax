using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Run against the real map adapters, shared solver and native PhysX queries.
    public static class HeldContactTests
    {
        static readonly List<string> failures = new List<string>();
        static int passed;

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            failures.Clear(); passed = 0;
            foreach (var stage in new[] { "Gallery", "Library", "Beach", "Starship" })
            {
                if (typeof(Gallery.GalleryGame).Assembly.GetType("ShadeLink." + stage + "." + stage + "Game") == null) continue;
                Check(stage, "small contact preview releases and can be regrabbed to regrow", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Grab(); var blocker = f.Box(new Vector3(0, 2, 1), new Vector3(4, 4, .2f));
                        f.Update();
                        Near(f.prop.transform.position.z, .8082f, .006f, "Held centre froze instead of retracting to the front surface");
                        Near(f.prop.transform.localScale.x, .16164f, .002f, "Contact did not preserve the captured scale/depth ratio");
                        Require(!(bool)Get(f.game, "PlacementBlocked"), "A feasible shrunken preview was marked blocked");
                        Require(!(bool)Get(f.game, "invalid"), "A clear small preview was incorrectly rejected");
                        var smallPosition=f.prop.transform.position;var smallScale=f.prop.transform.localScale;
                        Require((bool)Call(f.game,"Drop"),"Clear small preview could not be released");
                        Require(Get(f.game,"held")==null,"Small release did not finish the hold");
                        Near(f.prop.transform.localScale.x,smallScale.x,.00001f,"Small release changed scale");
                        Near(Vector3.Distance(f.prop.transform.position,smallPosition),0,.00001f,"Small release changed position");
                        Require(!((IInteractionProp)f.prop).Body.isKinematic&&((IInteractionProp)f.prop).Body.useGravity,"Small release did not restore falling physics");
                        f.Grab();
                        blocker.transform.position = new Vector3(0, 2, 3); Physics.SyncTransforms(); f.Update();
                        Near(f.prop.transform.position.z, 2.6082f, .008f, "Retreat did not restore projection depth");
                        Near(f.prop.transform.localScale.x, .52164f, .003f, "Retreat did not regrow from the original reference");
                        Require((bool)Call(f.game, "Drop"), "Regrown clear pose could not be released");
                    }
                });
                if(stage=="Gallery")Check(stage,"an open passage reaches a farther wall beyond the original exhibit zone",()=>
                {
                    using(var f=new Fixture(stage))
                    {
                        Set(f.prop,"maxScale",100f);f.Grab();
                        f.Box(new Vector3(0,2,22.1f),new Vector3(100,100,.2f));f.Update();
                        Near(f.prop.transform.position.z,19.7982f,.01f,"Invisible exhibition boundary stopped a clear farther-wall projection");
                        Require((bool)Call(f.game,"Drop"),"A clear pose beyond the original exhibit zone could not be released");
                    }
                });
                if(stage=="Gallery")Check(stage,"backward movement keeps a low plinth at the physical far wall beyond the legacy size cap",()=>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.prop.GetComponent<BoxCollider>().size=new Vector3(1.25f,.45f,1.25f);
                        f.prop.transform.position=new Vector3(0,2,2.85f);
                        Set(f.prop,"maxScale",3.2f);Call(f.prop,"Remember");Physics.SyncTransforms();
                        var wall=f.Box(new Vector3(0,2,22.1f),new Vector3(100,100,.2f));wall.name="Backward test far wall";
                        f.Grab();f.Update();
                        var game=(Gallery.GalleryGame)f.game;var prop=(Gallery.GalleryProp)f.prop;
                        float ratio=game.referenceScale/game.referenceDistance;
                        Debug.Log("BACKWARD_REPRO initial depth="+f.Depth+" scale="+prop.transform.localScale.x+" limiter="+game.PlacementDiagnostic.Limiter+" wall="+game.PlacementDiagnostic.SurfaceDepth+" size="+game.PlacementDiagnostic.SizeDepth);
                        Require(prop.transform.localScale.x>3.2f,"Legacy 3.20 scale cap still stops the object in empty space");
                        Require(game.PlacementDiagnostic.Limiter==PerspectivePlacement.LimitKind.Surface&&game.PlacementDiagnostic.Source==wall.GetComponent<Collider>(),"Far wall must determine distance");
                        float firstDepth=f.Depth,firstScale=prop.transform.localScale.x,backFace=prop.WorldBounds.max.z;
                        f.view.transform.position+=Vector3.back*2;f.controller.transform.position+=Vector3.back*2;Physics.SyncTransforms();f.Update();
                        Require(f.Depth>firstDepth+1.5f&&prop.transform.localScale.x>firstScale+.5f,"Backward movement keeps a fixed camera-relative distance or size");
                        Near(prop.WorldBounds.max.z,backFace,.04f,"Object travelled with the player instead of remaining by the physical far wall");
                        Near(prop.transform.localScale.x/f.Depth,ratio,.0001f,"Backward movement broke apparent-size ratio");
                        Require(game.ProjectionApplied&&!game.PlacementBlocked,"Backward movement used a stale safety pose");
                        Vector3 release=prop.transform.position;float releaseScale=prop.transform.localScale.x;
                        Require(game.Drop(),"A clear pose larger than the old 3.20 cap cannot be released");
                        Near(Vector3.Distance(prop.transform.position,release),0,.00001f,"Release moved the enlarged object");
                        f.Grab();f.Update();
                        Require(game.PlacementDiagnostic.Limiter==PerspectivePlacement.LimitKind.Surface,"Regrab restored an artificial size cap");
                        float regrabDepth=f.Depth,regrabScale=prop.transform.localScale.x;
                        f.view.transform.position+=Vector3.back*2;f.controller.transform.position+=Vector3.back*2;Physics.SyncTransforms();f.Update();
                        Require(f.Depth>regrabDepth+1.5f&&prop.transform.localScale.x>regrabScale+.5f,"Regrab changes backward movement into a camera-fixed carry mode");
                        Debug.Log("BACKWARD_REPRO_PASS first="+firstDepth+"/"+firstScale+" after="+regrabDepth+"/"+regrabScale+" release="+releaseScale);
                    }
                });
                Check(stage, "a scripted moving obstacle is queried at its current transform", () =>
                {
                    bool previous=Physics.autoSyncTransforms;
                    try
                    {
                        Physics.autoSyncTransforms=false;
                        using(var f=new Fixture(stage))
                        {
                            f.Grab();var blocker=f.Box(new Vector3(0,2,1),new Vector3(4,4,.2f));f.Update();
                            blocker.transform.position=new Vector3(0,2,3);
                            // No external SyncTransforms: the held resolver owns this query boundary.
                            f.Update();Near(f.prop.transform.position.z,2.6082f,.008f,"Projection queried the obstacle's previous physics pose");
                        }
                    }
                    finally{Physics.autoSyncTransforms=previous;}
                });
                Check(stage, "side contact follows camera movement without a stale accepted pose", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Grab(); f.Box(new Vector3(0, 2, 9.1f), new Vector3(100, 100, .2f));
                        f.Box(new Vector3(2.25f, 2, 6.5f), new Vector3(.5f, 6, 12));
                        f.Update(); float initial = f.prop.transform.localScale.x;
                        f.view.transform.position = new Vector3(1.9f, 2, 0);
                        f.controller.transform.position = new Vector3(1.9f, 0, 0); Physics.SyncTransforms(); f.Update();
                        Near(f.prop.transform.position.x, 1.9f, .002f, "Object remained tied to the old camera ray");
                        Require(f.prop.transform.localScale.x < .21f, "Side obstruction did not shrink the carried silhouette");
                        Require(!(bool)Get(f.game, "PlacementBlocked"), "Side contact froze a feasible preview");
                        Near(f.prop.transform.localScale.x / f.Depth, .2f, .0001f, "Side contact broke apparent size");
                        f.view.transform.position = new Vector3(0, 2, 0); f.controller.transform.position = Vector3.zero;
                        Physics.SyncTransforms(); f.Update();
                        Near(f.prop.transform.localScale.x, initial, .004f, "Moving away did not restore the original size");
                    }
                });
                Check(stage, "contact stays responsive at actual gameplay eye and capsule dimensions", () =>
                {
                    using(var f=new Fixture(stage,true))
                    {
                        f.Grab();f.Box(new Vector3(0,1.55f,.4f),new Vector3(4,4,.2f));f.Update();
                        Near(f.prop.transform.position.z,.2682f,.006f,"Real player capsule froze near-wall projection");
                        Require(!(bool)Get(f.game,"PlacementBlocked"),"Real capsule blocked feasible contact");
                    }
                });
                Check(stage, "looking down at the floor keeps a held preview on the current ray", () =>
                {
                    using(var f=new Fixture(stage,true))
                    {
                        Set(f.prop,"minScale",.01f);f.Grab();f.Box(new Vector3(0,-.1f,0),new Vector3(100,.2f,100));
                        f.view.transform.position=new Vector3(0,1.55f,2);f.controller.transform.position=new Vector3(0,0,2);
                        f.view.transform.rotation=Quaternion.Euler(80,0,0);Physics.SyncTransforms();f.Update();
                        var offset=f.prop.transform.position-f.view.transform.position;
                        Require(Vector3.Angle(offset,f.view.transform.forward)<.02f,"Player overlap left the preview on a stale camera ray");
                        Require(!(bool)Get(f.game,"PlacementBlocked"),"Looking down froze the held preview");
                        Require((bool)Get(f.game,"invalid"),"Player-overlapping preview advertised a safe release");
                        Require(!(bool)Call(f.game,"Drop"),"Released the preview inside the player capsule");
                    }
                });
                Check(stage, "a camera inside a solid does not project through its containing obstacle", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Grab(); f.Box(f.view.transform.position, Vector3.one);
                        f.Update();
                        Require((bool)Get(f.game, "PlacementBlocked"), "An obstacle containing the ray origin was ignored");
                        Require(!(bool)Call(f.game, "Drop"), "Released through an obstacle containing the camera");
                    }
                });
                Check(stage, "repeated close contact does not accumulate shrink or lose child collider layers", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        // The actual collision shape belongs to an offset child.
                        UnityEngine.Object.DestroyImmediate(f.prop.GetComponent<BoxCollider>());
                        var child = f.New("Owned contact shape"); child.transform.SetParent(f.prop.transform, false);
                        child.layer = 8; child.AddComponent<BoxCollider>(); Call(f.prop, "Remember");
                        f.Grab(); var blocker = f.Box(new Vector3(0, 2, 1), new Vector3(4, 4, .2f));
                        for (int i = 0; i < 40; i++) f.Update();
                        Near(f.prop.transform.localScale.x, .16164f, .002f, "Contact clearance compounded across frames");
                        Require(child.layer == HeldCollisionState.Layer && child.GetComponent<Collider>().enabled,
                            "Held child lost solver isolation or query geometry");
                        blocker.transform.position = new Vector3(0, 2, 3); Physics.SyncTransforms();
                        Call(f.game, "Cancel", false);
                        Near(f.prop.transform.position.z, 5, .001f, "Cancel did not restore the pickup pose");
                        Near(f.prop.transform.localScale.x, 1, .001f, "Cancel did not restore pickup scale");
                        Require(child.layer == 8, "Cancel did not restore the child layer");
                    }
                });
            }
            Debug.Log("HELD_CONTACT_RESULTS passed=" + passed + " failed=" + failures.Count);
            if (failures.Count > 0) throw new Exception("HELD_CONTACT_FAILED\n" + string.Join("\n", failures));
        }

        sealed class Fixture : IDisposable
        {
            readonly List<GameObject> objects = new List<GameObject>();
            public Component game, prop;
            public Camera view;
            public CharacterController controller;
            public float Depth => (float)Get(game, "holdDistance");
            public Fixture(string stage, bool realistic=false)
            {
                var assembly = typeof(Gallery.GalleryGame).Assembly;
                var host = New("Contact test host"); host.SetActive(false);
                game = host.AddComponent(assembly.GetType("ShadeLink." + stage + "." + stage + "Game", true));
                controller = New("Contact player").AddComponent<CharacterController>();
                controller.height = realistic?1.6f:1.8f; controller.radius = realistic?.3f:.24f; controller.center = Vector3.up*(realistic?.8f:.9f);
                view = New("Contact camera").AddComponent<Camera>(); view.enabled = false;
                view.fieldOfView = 60; view.aspect = 1; view.transform.position = new Vector3(0, realistic?1.55f:2, 0);
                Set(game, "controller", controller); Set(game, "view", view); Set(game, "active", true);
                Set(game, "lighting", host.AddComponent(assembly.GetType("ShadeLink." + stage + "." + stage + "Lighting", true)));
                var cube = Box(new Vector3(0, realistic?1.55f:2, 5), Vector3.one);
                prop = cube.AddComponent(assembly.GetType("ShadeLink." + stage + "." + stage + "Prop", true));
                Set(prop, "title", "Contact prop"); Call(prop, "Remember"); Call(prop, "InitializePhysics");
                var props = Array.CreateInstance(prop.GetType(), 1); props.SetValue(prop, 0); Set(game, "props", props);
                Physics.SyncTransforms();
            }
            public GameObject New(string name) { var value = new GameObject(name); objects.Add(value); return value; }
            public GameObject Box(Vector3 at, Vector3 size)
            {
                var value = New("Contact geometry"); value.transform.position = at; value.AddComponent<BoxCollider>().size = size;
                Physics.SyncTransforms(); return value;
            }
            public void Grab() { Call(game, "Interact"); Require(ReferenceEquals(Get(game, "held"), prop), "Fixture pickup failed"); }
            public void Update() { Call(game, "UpdateHeldPose"); }
            public void Dispose()
            {
                if (Get(game, "held") != null) Call(game, "Cancel", true);
                for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
                Physics.SyncTransforms();
            }
        }
        static object Get(object target, string name)
        {
            var field = target.GetType().GetField(name);
            return field != null ? field.GetValue(target) : target.GetType().GetProperty(name).GetValue(target);
        }
        static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
        static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name).Invoke(target, args); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }
        static void Check(string stage, string name, Action test)
        {
            try { test(); passed++; Debug.Log("HELD_CONTACT_PASS " + stage + ": " + name); }
            catch (Exception e) { failures.Add(stage + ": " + name + ": " + e.Message); Debug.LogError("HELD_CONTACT_FAIL " + failures[failures.Count - 1]); }
        }
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static void Near(float actual, float expected, float tolerance, string message)
        { Require(Mathf.Abs(actual - expected) <= tolerance, message + ": " + actual + " expected " + expected); }
    }
}
