using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class ControlFeelValidation
    {
        public static void RunAll()
        {
            var failures = new List<string>();
            foreach (var entry in new[] { "ObjectInteractionTests.Run", "ObjectInteractionTests.RunFeel", "ProjectionFeelTests.Run", "ControlFeelTests.Run", "PlayerControlsTests.Run", "ReleasePhysicsTests.Run", "PlayerMovementAudioTests.Run", "RuleTests.Run", "IndoorTests.Run", "CourtyardPuzzleTests.Run", "KeyPuzzleTests.Run", "ShadowPuzzleTests.Run" })
            {
                int split = entry.LastIndexOf('.');
                try
                {
                    var type = typeof(ControlFeelValidation).Assembly.GetType("ShadeLink.Editor." + entry.Substring(0, split), true);
                    type.GetMethod(entry.Substring(split + 1), BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                    Debug.Log("CONTROL_SUITE_PASS " + entry);
                }
                catch (Exception error)
                {
                    var actual = error is TargetInvocationException ? error.InnerException : error;
                    failures.Add(entry + ": " + actual.Message);
                    Debug.LogError("CONTROL_SUITE_FAIL " + failures[failures.Count - 1]);
                }
            }
            if (failures.Count > 0) throw new Exception("CONTROL_FEEL_VALIDATION_FAILED\n" + string.Join("\n", failures));
            Debug.Log("CONTROL_FEEL_VALIDATION_OK");
        }
    }
}
