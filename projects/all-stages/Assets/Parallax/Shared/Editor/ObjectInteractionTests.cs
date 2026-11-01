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

        static Vector2 ProjectedSize(Component prop, Camera camera)
        {
            Vector2 minimum=Vector2.one*float.PositiveInfinity,maximum=Vector2.one*float.NegativeInfinity;
            foreach(var part in (BoxCollider[])Get(prop,"parts"))
                for(int i=0;i<8;i++)
                {
                    Vector3 point=part.transform.TransformPoint(part.center+Vector3.Scale(part.size*.5f,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    Vector2 projected=camera.WorldToViewportPoint(point);
                    minimum=Vector2.Min(minimum,projected);maximum=Vector2.Max(maximum,projected);
                }
            return maximum-minimum;
        }

        static Vector2 ReferenceCubeSize(Camera camera)
        {
            // The fixture starts as a unit cube with its centre at z=5.
            // Keep that geometry upright on the current aim ray so this
            // comparison measures perspective scaling independently of camera angle.
            Vector3 center=camera.transform.position+camera.transform.forward*5;
            Quaternion orientation=Quaternion.LookRotation(Vector3.Cross(camera.transform.right,Vector3.up),Vector3.up);
            Vector2 minimum=Vector2.one*float.PositiveInfinity,maximum=Vector2.one*float.NegativeInfinity;
            for(int i=0;i<8;i++)
            {
                Vector3 corner=center+orientation*new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f);
                Vector2 projected=camera.WorldToViewportPoint(corner);
                minimum=Vector2.Min(minimum,projected);maximum=Vector2.Max(maximum,projected);
            }
            return maximum-minimum;
        }

        public static void RunPhysicsPolicies()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var result=new Results();
            foreach(string stage in new[]{"Gallery","Library","Beach","Starship"})
            {
                Check(result,stage,"held layers stay queryable and restore on cancel and release",()=>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.prop.gameObject.layer=8;f.Grab();
                        Require(f.prop.gameObject.layer==29,"Grab did not separate ordinary solver contacts");
                        foreach(var part in ((IInteractionProp)f.prop).Parts)Require(part.enabled,"Grab disabled an owned query collider");
                        Call(f.game,"Cancel",false);Require(f.prop.gameObject.layer==8,"Cancel lost the source layer");
                        f.Grab();Require((bool)Call(f.game,"Drop"),"Valid release was rejected");
                        Require(f.prop.gameObject.layer==8,"Drop did not return the physical world layer");
                    }
                });
                Check(result,stage,"rejected preview preserves accepted mass and pose",()=>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.Grab();var body=((IInteractionProp)f.prop).Body;
                        var position=f.prop.transform.position;var scale=f.prop.transform.localScale;float mass=body.mass;
                        f.Blocker(new Vector3(0,2,3));Physics.SyncTransforms();
                        var shared=f.game.GetType().GetField("interaction",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f.game);
                        shared.GetType().GetProperty("Depth").SetValue(shared,3f);
                        Require(!(bool)shared.GetType().GetMethod("TryApplyPlacement").Invoke(shared,null),"Fixture preview should overlap the wall");
                        Near(f.prop.transform.position,position,"Rejected preview moved the prop");Near(f.prop.transform.localScale,scale,"Rejected preview changed scale");
                        Require(Mathf.Abs(body.mass-mass)<.001f,"Rejected preview left the candidate mass on the accepted body");
                    }
                });
                Check(result,stage,"disabling the interaction host cancels carried physics",()=>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.prop.gameObject.layer=8;f.Grab();
                        var disabled=f.game.GetType().GetMethod("OnDisable",BindingFlags.NonPublic|BindingFlags.Instance);
                        Require(disabled!=null,"Host has no held-state shutdown cleanup");disabled.Invoke(f.game,null);
                        Require(Get(f.game,"held")==null&&f.prop.gameObject.layer==8,"Host shutdown stranded a carried object");
                    }
                });
            }
            if(result.failed.Count>0)throw new Exception("INTERACTION_PHYSICS_POLICIES_FAILED\n"+string.Join("\n",result.failed));
            Debug.Log("INTERACTION_PHYSICS_POLICIES_OK "+result.passed.Count);
        }

        public static void RunFeel()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var result = new Results();
            foreach (string stage in new[] { "Gallery", "Library", "Beach", "Starship" })
            {
                Check(result, stage, "pickup uses centre distance rather than front-face distance", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Grab();
                        Require(Mathf.Abs((float)Get(f.game,"referenceDistance")-5)<.001f,
                            "Reference distance must measure the object's centre");
                        Near((Vector3)Get(f.game,"GrabLocalPoint"),Vector3.zero,"Perspective anchor must be the centre");
                    }
                });
                Check(result, stage, "mouse pickup assistance catches a visible near miss", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.prop.transform.position += Vector3.right*.52f;
                        Physics.SyncTransforms(); f.Grab();
                    }
                });
                Check(result, stage, "assisted pickup respects a foreground wall", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.prop.transform.position += Vector3.right*.52f;
                        f.Blocker(new Vector3(0,2,3)); Physics.SyncTransforms();
                        Call(f.game,"Interact");
                        Require(Get(f.game,"held")==null,"Pickup assistance selected through a wall");
                    }
                });
                Check(result, stage, "off-centre pickup eases to the aim over .8 seconds", () =>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.prop.transform.position+=Vector3.right*.3f;
                        Physics.SyncTransforms();f.Grab();
                        object shared=f.game.GetType().GetField("interaction",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f.game);
                        float start=Vector3.Angle(f.prop.transform.position-f.view.transform.position,f.view.transform.forward);
                        Require(start>1,"Pickup jumped straight to the screen centre");
                        shared.GetType().GetMethod("Advance").Invoke(shared,new object[]{.4f});Call(f.game,"UpdateHeldPose");
                        float halfway=Vector3.Angle(f.prop.transform.position-f.view.transform.position,f.view.transform.forward);
                        Require(Mathf.Abs(halfway-start*.25f)<.05f,"Initial alignment is not quadratic ease-out");
                        shared.GetType().GetMethod("Advance").Invoke(shared,new object[]{.4f});Call(f.game,"UpdateHeldPose");
                        Require(Vector3.Angle(f.prop.transform.position-f.view.transform.position,f.view.transform.forward)<.05f,"Alignment did not finish after .8 seconds");
                    }
                });
                Check(result, stage, "player overlap shrinks the object at its projected centre", () =>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.prop.GetComponent<BoxCollider>().size=new Vector3(20,20,8);
                        Set(f.prop,"minScale",.05f);Call(f.prop,"Remember");Physics.SyncTransforms();f.Grab();
                        object shared=f.game.GetType().GetField("interaction",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f.game);
                        shared.GetType().GetProperty("Depth").SetValue(shared,1f);
                        Require((bool)shared.GetType().GetMethod("TryApplyPlacement").Invoke(shared,null),"Overlap correction found no valid pose");
                        Near(f.prop.transform.position,f.view.transform.position+f.view.transform.forward,"Overlap correction moved the projected centre");
                        Require(f.prop.transform.localScale.x<.199f&&f.prop.transform.localScale.x>=.05f,"Overlap did not shrink safely above minimum size");
                    }
                });
                Check(result, stage, "drop resolves the current view before enabling physics", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Grab();
                        f.view.transform.rotation=Quaternion.Euler(0,25,0);
                        var wall=f.Blocker(f.view.transform.position+f.view.transform.forward*12);
                        wall.transform.rotation=f.view.transform.rotation;
                        wall.transform.localScale=new Vector3(100,100,.2f);
                        Physics.SyncTransforms();
                        Require((bool)Call(f.game,"Drop"),"Current-view release was rejected");
                        Require(Vector3.Angle(f.prop.transform.position-f.view.transform.position,f.view.transform.forward)<.1f,
                            "Drop used the previous frame's projection");
                    }
                });
            }
            string report=Path.Combine(Path.GetTempPath(),"parallax-control-interaction-results.json");
            File.WriteAllText(report,JsonUtility.ToJson(result,true));
            if(result.failed.Count>0)throw new Exception("CONTROL_INTERACTION_TESTS_FAILED "+result.failed.Count+"; "+report);
            Debug.Log("CONTROL_INTERACTION_TESTS_OK "+result.passed.Count);
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run these scene fixtures in a separate batch-mode project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var result = new Results();
            Check(result,"Starship","successful dense placement reports its physics queries",()=>
            {
                using(var f=new Fixture("Starship"))
                {
                    f.Grab();var wall=f.Blocker(new Vector3(0,2,12));wall.transform.localScale=new Vector3(100,100,.2f);
                    Physics.SyncTransforms();Call(f.game,"UpdateHeldPose");
                    Require(StarshipHold.QueryCount((ShadeLink.Starship.StarshipGame)f.game)>8,"Dense placement physics work was omitted from the query report");
                }
            });
            foreach (string stage in new[] { "Gallery", "Library", "Beach", "Starship" })
            {
                Check(result, stage, "tilted pickup levels the object and cancel restores its original rotation", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        Quaternion original = Quaternion.Euler(20, 37, 25);
                        f.prop.transform.rotation = original; Physics.SyncTransforms(); f.Grab();
                        Require(Vector3.Dot(f.prop.transform.up, Vector3.up) > .99999f, "Picked object retained its tilt");
                        Require(Vector3.Dot(f.prop.transform.forward, Quaternion.Euler(0, 37, 0) * Vector3.forward) > .99999f,
                            "Leveling changed the object's horizontal facing");
                        Call(f.game, "Cancel", false);
                        Require(Quaternion.Angle(f.prop.transform.rotation, original) < .01f, "Cancel lost original tilt");
                    }
                });
                Check(result, stage, "looking up down and sideways keeps the held object level", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        f.Grab();
                        foreach (var angle in new[] { new Vector3(25, 0, 0), new Vector3(-25, 0, 0), new Vector3(0, 40, 0) })
                        {
                            f.view.transform.rotation = Quaternion.Euler(angle);
                            Call(f.game, "UpdateHeldPose");
                            Require(Vector3.Dot(f.prop.transform.up, Vector3.up) > .99999f, "Camera movement tilted the held object");
                            var expected=Quaternion.LookRotation(Vector3.Cross(f.view.transform.right,Vector3.up),Vector3.up);
                            Require(Quaternion.Angle(f.prop.transform.rotation, expected) < .01f,
                                "Camera yaw did not carry the object's horizontal orientation");
                        }
                    }
                });
                Check(result, stage, "pickup and distance scaling preserve nonuniform compound proportions", () =>
                {
                    using (var f = new Fixture(stage))
                    {
                        Vector3 original = new Vector3(1, 1.4f, .65f);
                        f.prop.transform.localScale = original;
                        var detail = f.New("Offset compound detail"); detail.transform.SetParent(f.prop.transform, false);
                        detail.transform.localPosition = new Vector3(.6f, .2f, 0); detail.transform.localScale = Vector3.one * .2f;
                        detail.AddComponent<BoxCollider>(); Call(f.prop, "Remember"); Physics.SyncTransforms(); f.Grab();
                        f.Blocker(new Vector3(0, 2, 10));
                        Call(f.game, "UpdateHeldPose");
                        Vector3 size = f.prop.transform.localScale;
                        Near(size / size.x, original, "Holding changed width height or depth proportions");
                        Require(Mathf.Abs(size.x - 1) > .05f, "Fixture did not exercise perspective resizing");
                        Near(detail.transform.localPosition, new Vector3(.6f, .2f, 0), "Compound detail moved");
                        Require(!(bool)Get(f.game, "invalid"), "Preserved compound shape became invalid");
                        Call(f.game, "Cancel", false); Near(f.prop.transform.localScale, original, "Cancel lost original proportions");
                    }
                });
                Check(result, stage, "a close surface retracts and releases the clear small preview", () =>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.Grab();f.view.transform.position+=Vector3.forward*2;f.controller.transform.position+=Vector3.forward*2;
                        f.view.transform.rotation=Quaternion.Euler(0,60,0);
                        var wall=f.Blocker(f.view.transform.position+f.view.transform.forward*.5f);
                        wall.transform.rotation=f.view.transform.rotation;wall.transform.localScale=new Vector3(100,100,.2f);
                        Physics.SyncTransforms();Call(f.game,"UpdateHeldPose");
                        float projectedDepth=Vector3.Dot(f.prop.transform.position-f.view.transform.position,f.view.transform.forward);
                        Require(projectedDepth>0&&projectedDepth<.4f,"Close contact projected the preview behind its occluding wall");
                        Require(!(bool)Get(f.game,"invalid"),"Clear small preview was rejected");
                        Require((bool)Call(f.game,"Drop"),"A clear small preview could not be released");
                    }
                });
                Check(result, stage, "a post visible through a compound frame hole does not block placement", () =>
                {
                    using(var f=new Fixture(stage))
                    {
                        UnityEngine.Object.DestroyImmediate(f.prop.GetComponent<BoxCollider>());
                        foreach(float x in new[]{-.75f,.75f})
                        {
                            var leg=f.New("Frame upright");leg.transform.SetParent(f.prop.transform,false);leg.transform.localPosition=new Vector3(x,0,0);
                            leg.AddComponent<BoxCollider>().size=new Vector3(.3f,1,.4f);
                        }
                        var top=f.New("Frame top");top.transform.SetParent(f.prop.transform,false);top.transform.localPosition=new Vector3(0,.45f,0);
                        top.AddComponent<BoxCollider>().size=new Vector3(1.8f,.1f,.4f);Call(f.prop,"Remember");
                        f.view.transform.position=new Vector3(-.75f,2,0);Physics.SyncTransforms();f.Grab();
                        var wall=f.Blocker(new Vector3(0,2,12));wall.transform.localScale=new Vector3(100,100,.2f);
                        var post=f.New("Post through empty frame");post.AddComponent<BoxCollider>();
                        post.transform.position=new Vector3(.5f,2,8);post.transform.localScale=new Vector3(.08f,.08f,.2f);
                        Physics.SyncTransforms();for(int i=0;i<120;i++)Call(f.game,"UpdateHeldPose");
                        Vector3 anchor=f.prop.transform.TransformPoint((Vector3)Get(f.game,"GrabLocalPoint"))-f.view.transform.position;
                        Require(anchor.magnitude>10.9f,"The empty silhouette hole was treated as solid");
                        object[] validation={f.prop,null};Require((bool)Call(f.game,"ValidPlacement",validation),"Frame overlaps the post");
                    }
                });
                Check(result, stage, "unobstructed quick camera turns keep the prop on the aim immediately", () =>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.Grab();
                        f.view.transform.rotation=Quaternion.Euler(0,60,0);Call(f.game,"UpdateHeldPose");
                        Vector3 anchor=f.prop.transform.TransformPoint((Vector3)Get(f.game,"GrabLocalPoint"))-f.view.transform.position;
                        Require(Vector3.Angle(anchor,f.view.transform.forward)<.1f,"Free-space projection lagged behind the aim");
                        Vector2 original=ReferenceCubeSize(f.view),actual=ProjectedSize(f.prop,f.view);
                        Require(Mathf.Abs(actual.x/original.x-1)<.005f&&Mathf.Abs(actual.y/original.y-1)<.005f,"Quick turn changed screen size");
                    }
                });
                Check(result, stage, "camera yaw and pitch preserve perspective scaling with an upright object", () =>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.Grab();
                        var wall=f.Blocker(new Vector3(0,2,12));wall.transform.localScale=new Vector3(100,100,.2f);
                        f.view.transform.rotation=Quaternion.Euler(-5,20,0);Physics.SyncTransforms();
                        for(int i=0;i<120;i++)Call(f.game,"UpdateHeldPose");
                        Vector2 original=ReferenceCubeSize(f.view),actual=ProjectedSize(f.prop,f.view);
                        Require(Mathf.Abs(actual.x/original.x-1)<.005f&&Mathf.Abs(actual.y/original.y-1)<.005f,
                            "Camera movement changed the apparent size: "+actual.x/original.x+", "+actual.y/original.y);
                    }
                });
                Check(result, stage, "narrow posts allow the maximum non-overlapping perspective depth", () =>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.prop.transform.rotation=Quaternion.Euler(0,20,0);Physics.SyncTransforms();
                        Vector2 original=ProjectedSize(f.prop,f.view);f.Grab();
                        var post=f.Blocker(new Vector3(0,2,12));post.transform.localScale=new Vector3(.08f,4,.2f);Physics.SyncTransforms();
                        for(int i=0;i<120;i++)Call(f.game,"UpdateHeldPose");
                        Vector3 anchor=f.prop.transform.TransformPoint((Vector3)Get(f.game,"GrabLocalPoint"));
                        float depth=Vector3.Distance(anchor,f.view.transform.position);
                        float expected=(11.9f-.002f-.04f*Mathf.Tan(20*Mathf.Deg2Rad))*.99f/
                            (1+.5f/(5*Mathf.Cos(20*Mathf.Deg2Rad)));
                        Require(Mathf.Abs(depth-expected)<.02f,"Stopped short of the physically available centre depth: "+depth);
                        object[] validation={f.prop,null};Require((bool)Call(f.game,"ValidPlacement",validation),"Maximum-depth pose overlaps the post");
                        Vector2 actual=ProjectedSize(f.prop,f.view);
                        Require(Mathf.Abs(actual.x/original.x-1)<.005f&&Mathf.Abs(actual.y/original.y-1)<.005f,"Depth search changed screen size");
                    }
                });
                Check(result, stage, "interior-face bumps limit the requested depth before movement", () =>
                {
                    using(var f=new Fixture(stage))
                    {
                        f.Grab();f.Blocker(new Vector3(0,2,12));
                        var bump=f.New("Interior face bump");bump.AddComponent<BoxCollider>();
                        bump.transform.position=new Vector3(.35f,2.35f,8);bump.transform.localScale=new Vector3(.12f,.12f,.2f);
                        Physics.SyncTransforms();Call(f.game,"UpdateHeldPose");
                        float expected=(8-.1f-.002f)*.99f/1.1f;
                        Require(Mathf.Abs((float)Get(f.game,"holdDistance")-expected)<.03f,"Surface search missed an obstruction inside the visible face");
                    }
                });
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
                        Vector2 size = ProjectedSize(f.prop,f.view);
                        f.Grab();
                        Require((float)Get(f.game, "referenceDistance") > 120, "Pickup used a short replacement ray");
                        Vector2 actual=ProjectedSize(f.prop,f.view);
                        Require(Vector2.Distance(size,actual)<.0001f,"Initial projection changed the distant object's angular size");
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
                        f.Grab();
                        for (int i = 0; i < 20; i++) Call(f.game, "UpdateHeldPose");
                        var position=f.prop.transform.position;var size=f.prop.transform.localScale;var rotation=f.prop.transform.rotation;
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
                        f.Grab();var wall=f.Blocker(f.view.transform.position);
                        wall.transform.localScale=new Vector3(100,100,.2f);Physics.SyncTransforms();Call(f.game,"UpdateHeldPose");
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
