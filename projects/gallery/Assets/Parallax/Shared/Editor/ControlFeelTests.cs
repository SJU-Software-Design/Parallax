using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    // -executeMethod ShadeLink.Editor.ControlFeelTests.Run [-controlFeelReport path]
    public static class ControlFeelTests
    {
        [Serializable] sealed class Results
        {
            public List<string> passed = new List<string>();
            public List<string> failed = new List<string>();
        }

        sealed class Motor
        {
            readonly Type type;
            readonly object instance;
            public Motor()
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType("ShadeLink.FirstPersonMotor");
                    if (type != null) break;
                }
                Require(type != null, "FirstPersonMotor is missing: implement the shared movement contract");
                instance = Activator.CreateInstance(type);
            }
            public object Call(string method, params object[] args)
            {
                var member = type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                Require(member != null, "Missing motor method " + method);
                try { return member.Invoke(member.IsStatic ? null : instance, args); }
                catch (TargetInvocationException error) { throw error.InnerException ?? error; }
            }
            public T Get<T>(string name) { return (T)type.GetProperty(name).GetValue(instance); }
            public void Set(string name, object value)
            {
                var property = type.GetProperty(name); Require(property != null, "Missing motor property " + name);
                property.SetValue(instance, value);
            }
            public Vector3 Velocity { get { return Get<Vector3>("Velocity"); } }
            public bool Grounded { get { return Get<bool>("Grounded"); } }
            public bool Mantling { get { return Get<bool>("Mantling"); } }
            public void Step(CharacterController controller, Vector2 input, float dt, bool press = false, bool held = false)
            {
                Call("Step", controller, input, 0f, press, held, dt, ~0, Vector3.zero);
                Physics.SyncTransforms();
            }
        }

        sealed class Fixture : IDisposable
        {
            readonly List<GameObject> objects = new List<GameObject>();
            public readonly Motor motor = new Motor();
            public readonly CharacterController controller;
            public readonly Camera camera;
            public Fixture(bool floor = true)
            {
                controller = New("Motor test player").AddComponent<CharacterController>();
                camera = New("Motor test camera").AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.SetParent(controller.transform, false);
                motor.Call("Configure", controller, camera);
                motor.Set("SmoothKeyboardInput", false);
                if (floor) Box("Floor", new Vector3(0, -.5f, 0), new Vector3(50, 1, 50));
                Physics.SyncTransforms();
                if (floor) for (int i = 0; i < 3; i++) Step(Vector2.zero, .01f);
            }
            GameObject New(string name)
            {
                var gameObject = new GameObject(name); objects.Add(gameObject); return gameObject;
            }
            public GameObject Box(string name, Vector3 centre, Vector3 size)
            {
                var gameObject = New(name); gameObject.transform.position = centre;
                gameObject.AddComponent<BoxCollider>().size = size;
                Physics.SyncTransforms(); return gameObject;
            }
            public void Teleport(Vector3 feet)
            {
                controller.enabled = false; controller.transform.position = feet; controller.enabled = true;
                Physics.SyncTransforms();
            }
            public void Step(Vector2 input, float dt, bool press = false, bool held = false)
            {
                motor.Step(controller, input, dt, press, held);
            }
            public void Advance(float seconds, float dt, Vector2 input, bool held = false)
            {
                for (float t = 0; t < seconds - .00001f; t += dt) Step(input, Mathf.Min(dt, seconds - t), false, held);
            }
            public void Dispose()
            {
                motor.Call("Reset");
                for (int i = objects.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(objects[i]);
                Physics.SyncTransforms();
            }
        }

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var result = new Results();
            Check(result, "keyboard ramp, decay and reversal snap", () =>
            {
                var motor = new Motor();
                var ramp = (Vector2)motor.Call("FilterKeyboard", Vector2.zero, Vector2.up, .1f);
                Near(ramp.y, .3f, .0001f, "Keyboard ramp");
                var decay = (Vector2)motor.Call("FilterKeyboard", new Vector2(0, .6f), Vector2.zero, .1f);
                Near(decay.y, .3f, .0001f, "Keyboard return");
                var reverse = (Vector2)motor.Call("FilterKeyboard", new Vector2(0, .8f), Vector2.down, .1f);
                Near(reverse.y, -.3f, .0001f, "Reversal must snap to zero before ramping");
            });
            Check(result, "analog squared response and diagonal cap", () =>
            {
                var motor = new Motor();
                var half = (Vector2)motor.Call("ShapeInput", new Vector2(.5f, 0));
                Near(half.x, .25f, .0001f, "Half stick");
                var diagonal = (Vector2)motor.Call("ShapeInput", Vector2.one);
                Near(diagonal.magnitude, 1f, .0001f, "Diagonal input cap");
                using (var f = new Fixture())
                {
                    f.Advance(.3f, .01f, new Vector2(.5f, 0));
                    Near(f.motor.Velocity.x, 1, .08f, "Half stick walking speed");
                    f.Advance(.3f, .01f, Vector2.one);
                    Near(new Vector2(f.motor.Velocity.x, f.motor.Velocity.z).magnitude, 4, .08f, "Diagonal walking speed");
                }
            });
            Check(result, "feet geometry and eye position", () =>
            {
                using (var f = new Fixture())
                {
                    Near(f.controller.height, 1.6f, .001f, "Height");
                    Near(f.controller.radius, .3f, .001f, "Radius");
                    Near(f.controller.center.y, .8f, .001f, "Feet-relative capsule centre");
                    Near(f.controller.stepOffset, .2f, .001f, "Step offset");
                    Near(f.controller.skinWidth, .05f, .001f, "Skin width");
                    Near(f.controller.slopeLimit, 60, .001f, "Slope limit");
                    Near(f.camera.transform.localPosition.y, 1.55f, .001f, "Eye height");
                    Near(f.camera.fieldOfView, 70, .001f, "Default FOV");
                    Near(f.camera.nearClipPlane, .05f, .001f, "Near plane");
                }
            });
            Check(result, "ground and air acceleration differ", () =>
            {
                using (var f = new Fixture())
                {
                    Require(f.motor.Grounded, "Floor fixture must settle");
                    f.Step(Vector2.right, .02f);
                    Near(f.motor.Velocity.x, 1.132f, .08f, "Ground acceleration");
                }
                using (var f = new Fixture(false))
                {
                    f.Teleport(Vector3.up * 20); f.Step(Vector2.right, .02f);
                    Near(f.motor.Velocity.x, .3f, .03f, "Air acceleration");
                }
            });
            foreach (int fps in new[] { 30, 60, 120 })
            {
                int rate = fps;
                Check(result, "keyboard ramp distance at " + fps + " fps", () =>
                {
                    using (var f = new Fixture())
                    {
                        f.motor.Set("SmoothKeyboardInput", true);
                        f.Advance(1f / 3f, 1f / rate, Vector2.up);
                        Near(f.motor.Velocity.z, 4, .06f, "Full keyboard speed after one third second");
                        Near(f.controller.transform.position.z, .444f, .085f, "Integrated keyboard ramp distance");
                    }
                });
                Check(result, "short and held jump heights at " + fps + " fps", () =>
                {
                    float shortHeight = JumpHeight(rate, false), longHeight = JumpHeight(rate, true);
                    Near(shortHeight, .9f, .13f, "Short jump apex");
                    Near(longHeight, 1.4f, .14f, "Held jump apex");
                    Require(longHeight - shortHeight > .35f, "Holding jump must add height");
                });
            }
            Check(result, "jump buffer accepts 50 ms landing approach", () =>
            {
                using (var f = new Fixture())
                {
                    f.motor.Call("Reset"); f.Teleport(Vector3.up * .1f); f.motor.Set("VerticalVelocity", -3f);
                    f.Step(Vector2.zero, .01f, true); f.Advance(.08f, .01f, Vector2.zero);
                    Require(f.motor.Velocity.y > 3, "A press just before landing must launch on contact");
                }
            });
            Check(result, "expired jump buffer and held button do not auto jump", () =>
            {
                using (var f = new Fixture())
                {
                    f.motor.Call("Reset"); f.Teleport(Vector3.up * 1f);
                    f.Step(Vector2.zero, .01f, true, true); f.Advance(1f, .01f, Vector2.zero, true);
                    Require(f.motor.Grounded && f.controller.transform.position.y < .08f, "Expired press or holding jump repeated a jump");
                }
            });
            Check(result, "no coyote jump after leaving support", () =>
            {
                using (var f = new Fixture(false))
                {
                    f.Box("Small floor", new Vector3(0, -.5f, 0), new Vector3(2, 1, 2));
                    f.Advance(.03f, .01f, Vector2.zero);
                    f.Teleport(new Vector3(3, 0, 0)); f.Step(Vector2.zero, .01f, true);
                    Require(f.motor.Velocity.y <= 0 && !f.motor.Grounded, "Stale grounded state allowed a coyote jump");
                }
            });
            Check(result, "cooldown starts at takeoff without corrupting grounded state", () =>
            {
                using (var f = new Fixture())
                {
                    f.Step(Vector2.zero, .01f, true);
                    f.Advance(.66f, .01f, Vector2.zero);
                    Require(f.motor.Grounded, "Short jump should land before cooldown finishes");
                    f.Step(Vector2.zero, .01f, true);
                    Require(f.motor.Grounded && f.motor.Velocity.y <= 0, "Cooldown rejection changed grounded state or applied impulse");
                    f.Advance(.12f, .01f, Vector2.zero);
                    f.Step(Vector2.zero, .01f, true);
                    Require(f.motor.Velocity.y > 5, "Cooldown was measured from landing instead of takeoff");
                }
            });
            Check(result, "low ceiling cancels rising velocity", () =>
            {
                using (var f = new Fixture())
                {
                    f.Box("Ceiling", new Vector3(0, 1.95f, 0), new Vector3(5, .2f, 5));
                    f.Step(Vector2.zero, .01f, true, true);
                    f.Advance(.12f, .01f, Vector2.zero, true);
                    Require(f.controller.transform.position.y < .35f, "Jump passed through ceiling");
                    Require(f.motor.Velocity.y <= .01f, "Ceiling collision retained rising impulse");
                }
            });
            Check(result, "falling speed is terminal limited", () =>
            {
                using (var f = new Fixture(false))
                {
                    f.Teleport(Vector3.up * 100); f.Advance(3, .02f, Vector2.zero);
                    Near(f.motor.Velocity.y, -20, .05f, "Terminal speed");
                }
            });
            Check(result, "wall collision removes blocked velocity", () =>
            {
                using (var f = new Fixture())
                {
                    f.Box("Wall", new Vector3(.8f, 1, 0), new Vector3(.2f, 3, 20));
                    f.Advance(.5f, .01f, Vector2.right);
                    Require(f.controller.transform.position.x < .5f && Mathf.Abs(f.motor.Velocity.x) < .05f, "Blocked velocity accumulated against wall");
                    f.Step(Vector2.left, .02f);
                    Require(f.motor.Velocity.x < -.9f, "Wall stored momentum prevented immediate acceleration away");
                }
            });
            foreach(int fps in new[]{30,60,120})
                Check(result,"diagonal wall contact preserves tangent speed at "+fps+" fps",()=>
                {
                    using(var f=new Fixture())
                    {
                        f.Box("Slide wall",new Vector3(.8f,1,0),new Vector3(.2f,3,50));
                        f.Advance(1.2f,1f/fps,Vector2.one);
                        float start=f.controller.transform.position.z;
                        f.Advance(.5f,1f/fps,Vector2.one);
                        float speed=(f.controller.transform.position.z-start)/.5f;
                        Near(speed,4/Mathf.Sqrt(2),.15f,"Wall sliding lost the unblocked component");
                    }
                });
            Check(result, "ground adhesion follows a small descending step", () =>
            {
                using (var f = new Fixture(false))
                {
                    f.Box("Upper floor", new Vector3(0, -.5f, -2), new Vector3(5, 1, 4));
                    f.Box("Lower floor", new Vector3(0, -.65f, 3), new Vector3(5, 1, 6));
                    f.Teleport(new Vector3(0, 0, -.6f)); f.Advance(.5f, .01f, Vector2.up);
                    Require(f.motor.Grounded, "Descending a .15 step lost ground adhesion");
                    Near(f.controller.transform.position.y, -.15f, .065f, "Descending step feet height");
                }
            });
            Check(result, "pause input clearing preserves falling trajectory and jump cooldown", () =>
            {
                using(var f=new Fixture())
                {
                    f.Step(Vector2.up,.01f,true);f.Advance(.1f,.01f,Vector2.up);
                    float vertical=f.motor.Velocity.y;
                    f.motor.Call("ClearInput");
                    Near(f.motor.Velocity.y,vertical,.0001f,"Pause changed vertical momentum");
                    Require(new Vector2(f.motor.Velocity.x,f.motor.Velocity.z).magnitude<.001f,"Pause retained movement input");
                    f.Advance(.51f,.01f,Vector2.zero);
                    f.Step(Vector2.zero,.01f,true);
                    Require(f.motor.Grounded&&f.motor.Velocity.y<=0,"Pause reset the takeoff cooldown");
                }
            });
            Check(result, "reset clears input, velocity, jump buffer and mantle", () =>
            {
                using (var f = new Fixture())
                {
                    f.motor.Set("SmoothKeyboardInput", true);
                    f.Advance(.4f, .01f, Vector2.up); f.Step(Vector2.up, .01f, true, true);
                    f.motor.Call("Reset"); f.Teleport(Vector3.zero);
                    f.Advance(.1f, .01f, Vector2.zero);
                    Require(f.motor.Grounded && !f.motor.Mantling, "Reset retained airborne/mantle state");
                    Require(new Vector2(f.motor.Velocity.x, f.motor.Velocity.z).magnitude < .01f, "Reset retained movement input");
                    f.Advance(.4f, .01f, Vector2.up);
                    Require(f.controller.transform.position.z > .4f, "Movement stopped working after respawn reset");
                }
            });
            Check(result, "mantle requires jump and clear landing space", () =>
            {
                using (var f = MantleFixture(false))
                {
                    f.Advance(1, .01f, Vector2.up);
                    Require(!f.motor.Mantling && f.controller.transform.position.y < .1f, "Walking into ledge triggered mantle without jump");
                }
                using (var f = MantleFixture(true))
                {
                    f.Step(Vector2.up, .01f, true); f.Advance(1.1f, .01f, Vector2.up);
                    Require(!f.motor.Mantling && f.controller.transform.position.y < 1, "Mantle entered a low ceiling");
                }
            });
            Check(result, "falling forward jump mantles over .3 seconds without disabling controller", () =>
            {
                using (var f = MantleFixture(false))
                {
                    f.Step(Vector2.up, .01f, true);
                    bool began = false; float duration = 0;
                    for (int i = 0; i < 120; i++)
                    {
                        f.Step(Vector2.up, .01f);
                        Require(f.controller.enabled, "Mantle disabled collision controller");
                        if (f.motor.Mantling) { began = true; duration += .01f; }
                        else if (began) break;
                    }
                    Require(began, "Falling jump at reachable ledge never started mantle");
                    Near(duration, .3f, .035f, "Mantle duration");
                    Require(f.controller.transform.position.y > 1.05f && f.controller.transform.position.z > .7f, "Mantle failed to reach ledge top");
                }
            });
            Check(result, "game configures feet geometry, carries while moving and resets movement", () =>
            {
                using (var f = new Fixture())
                {
                    var gameObject = new GameObject("Control integration game"); gameObject.SetActive(false);
                    var propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    try
                    {
                        var assembly = typeof(ShadeLink.Gallery.GalleryGame).Assembly;
                        var game = gameObject.AddComponent(assembly.GetType("ShadeLink.Gallery.GalleryGame", true));
                        var lighting = gameObject.AddComponent(assembly.GetType("ShadeLink.Gallery.GalleryLighting", true));
                        propObject.transform.position = new Vector3(0, 1.55f, 5);
                        var prop = propObject.AddComponent(assembly.GetType("ShadeLink.Gallery.GalleryProp", true));
                        SetField(prop, "title", "Control integration prop"); Invoke(prop, "Remember");
                        var props = Array.CreateInstance(prop.GetType(), 1); props.SetValue(prop, 0);
                        SetField(game, "controller", f.controller); SetField(game, "view", f.camera);
                        SetField(game, "lighting", lighting); SetField(game, "props", props);
                        f.controller.height = 1.9f; f.controller.center = Vector3.up * .95f;
                        Invoke(game, "Awake");
                        var font=(Font)game.GetType().GetField("font").GetValue(game);
                        font.RequestCharactersInTexture("가A",32,FontStyle.Normal);
                        Require(font.GetCharacterInfo('가',out var korean,32,FontStyle.Normal)&&korean.advance>0,
                            "Platform HUD font cannot render Korean controls");
                        Near(f.controller.height, 1.6f, .001f, "Game must override stale serialized capsule height");
                        Near(f.controller.center.y, .8f, .001f, "Game must preserve feet-relative origin");
                        SetField(game, "automated", true); SetField(game, "pitch", 0f); Invoke(game, "Resume");
                        Invoke(game, "SyncView"); Physics.SyncTransforms(); Invoke(game, "Interact");
                        Require(ReferenceEquals(game.GetType().GetField("held").GetValue(game), prop), "Game fixture failed to pick up prop");
                        SetField(game, "yaw", Mathf.PI * .5f);
                        for (int i = 0; i < 40; i++) Invoke(game, "Tick", .01f, Vector2.right, false);
                        Require(f.controller.transform.position.x > .5f, "Holding a prop stopped walking");
                        Require(Mathf.Abs(f.controller.transform.position.z) < .06f, "Public Tick changed WORLDmove into local input");
                        Require(ReferenceEquals(game.GetType().GetField("held").GetValue(game), prop), "Walking dropped the held prop");
                        Invoke(game, "PlacePlayer", Vector3.zero);
                        for (int i = 0; i < 10; i++) Invoke(game, "Tick", .01f, Vector2.zero, false);
                        Require(Mathf.Abs(f.controller.transform.position.x) < .01f, "Respawn retained filtered movement input");
                        Near(f.camera.transform.position.y - f.controller.transform.position.y, 1.55f, .001f, "Game eye height");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(propObject); UnityEngine.Object.DestroyImmediate(gameObject);
                        Time.timeScale = 1; Physics.SyncTransforms();
                    }
                }
            });
            Check(result, "unready placement supports cannot ground the motor", () =>
            {
                using (var f = new Fixture(false))
                {
                    var floor = f.Box("Unready support", new Vector3(0, -.5f, 0), new Vector3(4, 1, 4));
                    floor.AddComponent<PlacementSupport>();
                    Physics.IgnoreCollision(f.controller, floor.GetComponent<Collider>(), true);
                    f.motor.Set("GroundFilter", new Func<Collider, bool>(PlacementSupport.IsUsableGround));
                    f.Step(Vector2.zero, .01f, true);
                    Require(!f.motor.Grounded && f.motor.Velocity.y <= 0, "Motor jumped from an unready preview support");
                }
            });
            string report = Path.Combine(Path.GetTempPath(), "parallax-control-feel-results.json");
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-controlFeelReport");
            if (at >= 0 && at + 1 < args.Length) report = args[at + 1];
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            if (result.failed.Count > 0) throw new Exception("CONTROL_FEEL_TESTS_FAILED: " + result.failed.Count + "; " + report);
            Debug.Log("CONTROL_FEEL_TESTS_OK " + result.passed.Count + "; " + report);
        }

        static Fixture MantleFixture(bool ceiling)
        {
            var f = new Fixture();
            f.Box("Mantle ledge", new Vector3(0, .575f, 1.65f), new Vector3(4, 1.15f, 2));
            if (ceiling) f.Box("Mantle ceiling", new Vector3(0, 2.55f, 1.65f), new Vector3(4, .2f, 2));
            return f;
        }
        static void SetField(object target, string name, object value) { target.GetType().GetField(name).SetValue(target, value); }
        static object Invoke(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        static float JumpHeight(int fps, bool held)
        {
            using (var f = new Fixture())
            {
                float dt = 1f / fps, apex = 0;
                f.Step(Vector2.zero, dt, true, held);
                for (int i = 0; i < fps; i++)
                {
                    apex = Mathf.Max(apex, f.controller.transform.position.y);
                    f.Step(Vector2.zero, dt, false, held);
                }
                return apex;
            }
        }
        static void Near(float actual, float expected, float tolerance, string label)
        {
            Require(Mathf.Abs(actual - expected) <= tolerance, label + ": expected " + expected + " ± " + tolerance + ", got " + actual);
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Check(Results result, string name, Action test)
        {
            try { test(); result.passed.Add(name); Debug.Log("PASS " + name); }
            catch (Exception error) { result.failed.Add(name + " — " + error.Message); Debug.Log("FAIL " + name + " — " + error.Message); }
        }
    }
}
