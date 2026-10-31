using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Numeric tests deliberately use reflection so a missing implementation produces a red report.
    public static class PlayerControlsTests
    {
        [Serializable] sealed class Results
        {
            public List<string> passed = new List<string>();
            public List<string> failed = new List<string>();
        }

        static Type Controls
        {
            get
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType("ShadeLink.PlayerControls");
                    if (type != null) return type;
                }
                throw new Exception("ShadeLink.PlayerControls is missing");
            }
        }
        static object Call(string name, params object[] args)
        {
            var method = Controls.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            if (method == null) throw new Exception("PlayerControls." + name + " is missing");
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Near(float actual, float expected, string message)
        { Require(Mathf.Abs(actual - expected) < .0001f, message + ": " + actual + " != " + expected); }
        static void Near(Vector2 actual, Vector2 expected, string message)
        { Require(Vector2.Distance(actual, expected) < .0001f, message + ": " + actual + " != " + expected); }

        public static void Run()
        {
            var results = new Results();
            Check(results, "options clamp sensitivity and FOV safely", () =>
            {
                Near((float)Call("ClampSensitivity", -2f), .1f, "Sensitivity minimum");
                Near((float)Call("ClampSensitivity", 20f), 10f, "Sensitivity maximum");
                Near((float)Call("ClampSensitivity", float.NaN), 2f, "Invalid sensitivity default");
                Near((float)Call("ClampFieldOfView", 60f), 70f, "FOV minimum");
                Near((float)Call("ClampFieldOfView", 110f), 90f, "FOV maximum");
                Near((float)Call("ClampFieldOfView", float.PositiveInfinity), 70f, "Invalid FOV default");
            });
            Check(results, "options defaults and persisted values are adjustable", () =>
            {
                var defaults = Controls.GetProperty("DefaultOptions").GetValue(null);
                var type = defaults.GetType();
                Near((float)type.GetField("Sensitivity").GetValue(defaults), 2f, "Default sensitivity");
                Near((float)type.GetField("FieldOfView").GetValue(defaults), 70f, "Default FOV");
                Require(!(bool)type.GetField("InvertX").GetValue(defaults) && !(bool)type.GetField("InvertY").GetValue(defaults), "Inversion must default off");
                var settings = Controls.GetProperty("CurrentOptions");
                var original = settings.GetValue(null);
                try
                {
                    type.GetField("Sensitivity").SetValue(defaults, 3f);
                    type.GetField("FieldOfView").SetValue(defaults, 82f);
                    type.GetField("InvertY").SetValue(defaults, true);
                    settings.SetValue(null, defaults);
                    var saved = settings.GetValue(null);
                    Near((float)type.GetField("Sensitivity").GetValue(saved), 3f, "Adjustable sensitivity");
                    Near((float)type.GetField("FieldOfView").GetValue(saved), 82f, "Adjustable FOV");
                    Require((bool)type.GetField("InvertY").GetValue(saved), "Adjustable inversion");
                }
                finally { settings.SetValue(null, original); }
            });
            Check(results, "radial deadzone preserves direction and caps diagonal movement", () =>
            {
                Near((Vector2)Call("ApplyRadialDeadzone", new Vector2(.1f, .05f), .15f), Vector2.zero, "Small stick drift");
                Near((Vector2)Call("ApplyRadialDeadzone", new Vector2(.575f, 0), .15f), new Vector2(.5f, 0), "Deadzone remap");
                Near((Vector2)Call("ApplyRadialDeadzone", Vector2.one, .15f), Vector2.one.normalized, "Diagonal cap");
                Near((Vector2)Call("ApplyRadialDeadzone", new Vector2(float.NaN, 0), .15f), Vector2.zero, "Invalid device sample");
            });
            Check(results, "mouse look is immediate and independent of frame count", () =>
            {
                var delta = (Vector2)Call("MouseLookDelta", new Vector2(2, -3), 2f, false, false);
                Near(delta, new Vector2(.046f, -.069f), "Practical legacy mouse scale");
                Near((Vector2)Call("MouseLookDelta", new Vector2(2, -3), 2f, true, true), -delta, "Axis inversion");
                var half = (Vector2)Call("MouseLookDelta", new Vector2(1, -1.5f), 2f, false, false);
                Near(half + half, delta, "Mouse delta was multiplied by frame duration");
            });
            Check(results, "camera yaw remains unbounded and pitch is clamped", () =>
            {
                object[] args = { 9f, 0f, new Vector2(.25f, 3f) };
                Call("ApplyLook", args);
                Near((float)args[0], 9.25f, "Yaw should continue through a complete turn");
                Near((float)args[1], Mathf.PI * .5f, "Upward pitch limit");
                args = new object[] { 0f, 0f, new Vector2(0, -3f) }; Call("ApplyLook", args);
                Near((float)args[1], -70f * Mathf.Deg2Rad, "Downward pitch limit");
            });
            Check(results, "stick ramp restarts only on neutral or direction change", () =>
            {
                var ramp = (Vector2)Call("StepLookRamp", new Vector2(.6f, .4f), Vector2.one, new Vector2(.2f, 1), .1f);
                Near(ramp, new Vector2(.7f, .5f), "Changing stick magnitude restarted ramp");
                ramp = (Vector2)Call("StepLookRamp", ramp, new Vector2(.2f, 1), new Vector2(-1, 0), .1f);
                Near(ramp, new Vector2(.1f, 0), "Reversal or neutral did not restart ramp");
                ramp = (Vector2)Call("StepLookRamp", ramp, new Vector2(-1, 0), new Vector2(-1, 1), 2f);
                Near(ramp, Vector2.one, "Ramp must reach full rate at one second");
            });
            Check(results, "gamepad look rates and assists are frame independent", () =>
            {
                var whole = (Vector2)Call("GamepadLookDelta", Vector2.one, Vector2.one, 1f, false, false, 2f, false, false);
                Near(whole * Mathf.Rad2Deg, new Vector2(300, 240), "Full stick rates");
                var frame = (Vector2)Call("GamepadLookDelta", Vector2.one, Vector2.one, 1f / 120, false, false, 2f, false, false);
                Near(frame * 120, whole, "Stick rate depends on frame count");
                var assisted = (Vector2)Call("GamepadLookDelta", Vector2.one, Vector2.one, 1f, true, true, 2f, false, false);
                Near(assisted * Mathf.Rad2Deg, new Vector2(195, 360), "Near-target and held assists");
                Near((Vector2)Call("GamepadLookDelta", Vector2.one, Vector2.one, 0f, false, false, 2f, false, false), Vector2.zero, "Paused stick look");
            });
            Check(results, "object rotation uses the same accumulated look without dt", () =>
            {
                var full = (Vector2)Call("ObjectRotationDegrees", new Vector2(.3f, .2f));
                var half = (Vector2)Call("ObjectRotationDegrees", new Vector2(.15f, .1f));
                Near(full, new Vector2(.3f, -.2f) * Mathf.Rad2Deg, "Camera-space object rotation signs");
                Near(full, half + half, "Object rotation changes with frame count");
            });
            string report = Path.Combine(Path.GetTempPath(), "parallax-player-controls-results.json");
            var commandLine = Environment.GetCommandLineArgs(); int at = Array.IndexOf(commandLine, "-controlsReport");
            if (at >= 0 && at + 1 < commandLine.Length) report = commandLine[at + 1];
            File.WriteAllText(report, JsonUtility.ToJson(results, true));
            if (results.failed.Count != 0) throw new Exception("PLAYER_CONTROLS_TESTS_FAILED: " + results.failed.Count + "; " + report);
            Debug.Log("PLAYER_CONTROLS_TESTS_OK " + results.passed.Count + "; " + report);
        }
        static void Check(Results results, string name, Action test)
        {
            try { test(); results.passed.Add(name); Debug.Log("PASS " + name); }
            catch (Exception error) { results.failed.Add(name + " — " + error.Message); Debug.Log("FAIL " + name + " — " + error.Message); }
        }
    }
}
