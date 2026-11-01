using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class KeyPuzzleTests
    {
        static readonly List<string> passed = new List<string>();
        static void Assert(bool value, string why) { if (!value) throw new Exception("KEY_TEST_FAILED: " + why); }
        static void Check(string name, Action test) { test(); passed.Add(name); Debug.Log("PASS " + name); }
        public static void Run()
        {
            passed.Clear();
            Check("Lock rejects undersized and oversized keys; inclusive 8% tolerance", () =>
            {
                foreach (float ratio in new[] { .5f, .90f, .919f, 1.081f, 1.5f })
                { var k = new KeyState { stage = KeyStage.Carrying, scale = KeyState.TargetScale * ratio }; Assert(!k.Insert() && k.stage == KeyStage.Carrying, "wrong key accepted"); }
                foreach (float ratio in new[] { .92f, 1f, 1.08f })
                { var k = new KeyState { stage = KeyStage.Carrying, scale = KeyState.TargetScale * ratio }; Assert(k.Insert(), "valid key rejected"); }
            });
            Check("Key must be carried; insertion, turn, click and opening happen once in order", () =>
            {
                var k = new KeyState { scale = KeyState.TargetScale };
                Assert(!k.Insert(), "floor key inserted"); k.stage = KeyStage.Resizing; Assert(!k.Insert(), "resizing key inserted");
                k.stage = KeyStage.Carrying; Assert(k.Insert() && !k.Insert(), "duplicate insert");
                Assert(!k.Advance(KeyState.InsertSeconds) && k.stage == KeyStage.Turning, "insertion phase");
                Assert(k.Advance(KeyState.TurnSeconds) && k.stage == KeyStage.Opening && k.clicks == 1, "turn click");
                Assert(!k.Advance(KeyState.OpenSeconds) && k.stage == KeyStage.Unlocked, "opening phase");
                k.Advance(10); Assert(k.clicks == 1 && !k.Insert(), "duplicate unlock");
            });
            Check("Animation tolerates a long frame and reset constructs a fully locked key", () =>
            {
                var k = new KeyState { stage = KeyStage.Carrying, scale = KeyState.TargetScale }; k.Insert();
                Assert(k.Advance(10) && k.stage == KeyStage.Unlocked && k.clicks == 1, "long frame");
                k = new KeyState(); Assert(k.stage == KeyStage.Floor && k.scale == KeyState.InitialScale && k.clock == 0 && k.clicks == 0, "reset");
            });
            Check("Flat exit needs both shade arrival and fully opened lock; old raised exit stays unchanged", () =>
            {
                var s = new GameState { groundExit = true, keyRequired = true, player = new Player(new Vector2(4.65f, -15.1f)) };
                Assert(!s.CanFinish, "closed finish"); s.shadeSolved = true; Assert(!s.CanFinish, "key required");
                s.keyUnlocked = true; Assert(s.CanFinish, "flat finish");
                s.shadeSolved = false; Assert(!s.CanFinish, "shade required"); s.shadeSolved = true;
                s.player.grounded = false; Assert(!s.CanFinish, "airborne finish"); s.player.grounded = true;
                s.held = new Grab(); Assert(!s.CanFinish, "held block finish");
                Assert(Rules.CanOccupy(s.player.Point, new Block[0], null, true), "invisible ledge remains");
                Assert(!Rules.CanOccupy(s.player.Point, new Block[0]), "archived ledge changed");
            });
            Check("Connected shade saves automatically on flat arrival and ground movement reaches exit without jumps", () =>
            {
                var s = new GameState { groundExit = true, keyRequired = true, blocks = new Block[0], player = new Player(new Vector2(6.6f, -14.3f)) };
                Assert(!s.ReachCheckpoint(false) && s.ReachCheckpoint(true), "connected shade checkpoint");
                Rules.Simulate(s.player, new Vector2(-3, 0), .65f, s.blocks, false, null, true);
                Rules.Simulate(s.player, new Vector2(0, -3), .27f, s.blocks, false, null, true);
                Assert(s.player.grounded && s.player.feet.y == 0 && Rules.Exit.Contains(s.player.Point), "walk to door");
                s.keyUnlocked = true; Assert(s.CanFinish, "walk finish"); s.Respawn();
                Assert(s.shadeSolved && s.keyUnlocked && Rules.End.Contains(s.player.Point), "checkpoint preserves unlock");
            });
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/key-rules-results.txt", "Unity " + Application.unityVersion + "\nPassed: " + passed.Count + "\n" + string.Join("\n", passed));
            Debug.Log("KEY_TESTS_OK " + passed.Count);
        }
    }
}
