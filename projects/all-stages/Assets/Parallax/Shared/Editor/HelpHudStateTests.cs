using System;
using GlassTrial;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class HelpHudStateTests
    {
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("HELP_HUD_STATE_FAILED: " + message); }
        public static void Run()
        {
            var state = new HelpHudState();
            Check(state.Open() && !state.Turn(1), "pages cannot turn while expanding");
            state.Tick(HelpHudState.ExpandSeconds);
            Check(state.IsInteractive && state.Expansion == 1, "open");
            Check(state.Turn(-1) && state.NextPage == 2 && !state.Turn(1), "wrap and reject repeated clicks");
            state.Tick(HelpHudState.TurnSeconds * .5f);
            Check(state.Close() && state.Direction == 0 && state.NextPage == state.Page && state.TurnProgress == 0, "close cancels turn");
            state.Tick(HelpHudState.CollapseSeconds);
            Check(!state.IsActive && state.Open(), "reopen after interrupted turn");
            state.Tick(HelpHudState.ExpandSeconds);
            Check(state.Page == 0 && state.Turn(1), "reopen starts at first page");
            state.Tick(HelpHudState.TurnSeconds);
            Check(state.Page == 1 && state.IsInteractive && state.Turn(1), "turn completes and allows next turn");
            state.Reset();
            Check(!state.IsActive && state.Direction == 0 && state.Expansion == 0, "scene cleanup");
            Check(Math.Abs(HelpHudState.ExpandSeconds - .42f) < .0001f && Math.Abs(HelpHudState.TurnSeconds - .44f) < .0001f, "reference timing");
            foreach (int direction in new[] { -1, 1 })
            {
                var target = Quaternion.Euler(0, HelpHudFaces.PivotRotation(direction, 1), 0) * HelpHudFaces.FacePosition(direction);
                Check(Vector3.Distance(target, HelpHudFaces.FacePosition(0)) < .01f, "adjacent face reaches front " + direction);
                var rotation = Quaternion.Euler(0, HelpHudFaces.PivotRotation(direction, 1), 0) * Quaternion.Euler(0, HelpHudFaces.FaceRotation(direction), 0);
                Check(Quaternion.Angle(rotation, Quaternion.identity) < .01f, "adjacent face finishes facing forward " + direction);
            }
            foreach (string resource in new[] { "key", "key-pressed", "shift", "shift-pressed", "space", "space-pressed", "mouse", "mouse-left", "mouse-right", "pawn", "rotate-object" })
                Check(Resources.Load<Texture2D>("HelpHud/" + resource) != null, "missing help artwork " + resource);
            Debug.Log("HELP_HUD_STATE_TESTS_PASSED");
        }
    }
}
