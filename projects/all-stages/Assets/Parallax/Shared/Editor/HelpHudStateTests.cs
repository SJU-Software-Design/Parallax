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
            Check(NikeHintGuide.Stage(false, false) == 0 && NikeHintGuide.Stage(true, false) == 1 && NikeHintGuide.Stage(true, true) == 2, "Nike hint stages follow artwork and door state");
            for (int stage = 0; stage < 3; stage++) for (int page = 1; page <= 3; page++)
                Check(!string.IsNullOrWhiteSpace(NikeHintGuide.Text(stage, page)), "Nike hint content " + stage + "/" + page);
            foreach (string key in new[] { "tutorial.size.0", "tutorial.size.1", "tutorial.size.2", "tutorial.size.3", "tutorial.climb", "tutorial.sun", "gallery.first", "gallery.nike.alternate", "gallery.bottle.extract", "gallery.bottle.balance", "gallery.bottle.sword", "gallery.bottle.exit", "gallery.portrait", "gallery.portrait.exit", "gallery.hands", "gallery.hands.exit", "beach.receivers", "beach.receivers.exit", "beach.bridges", "beach.boat.shadow", "beach.boat.prepare", "beach.boat.travel", "beach.boat.exit", "beach.lift", "beach.lift.exit", "eclipse.moon", "eclipse.exit" })
                for (int page = 1; page <= 3; page++) Check(!string.IsNullOrWhiteSpace(HintCatalogue.Text(key, page)), "hint content " + key + "/" + page);
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
