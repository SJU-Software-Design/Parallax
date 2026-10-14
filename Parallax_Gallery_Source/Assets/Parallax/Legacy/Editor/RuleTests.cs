using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class RuleTests
    {
        static readonly List<string> passed = new List<string>();
        static void Assert(bool value, string message) { if (!value) throw new Exception("SHADE_TEST_FAILED: " + message); }
        static void Near(float a, float b, float tolerance = .001f) => Assert(Mathf.Abs(a - b) < tolerance, a + " != " + b);
        static void Check(string name, Action body) { body(); passed.Add(name); Debug.Log("PASS " + name); }
        static Block[] Solved()
        {
            Block[] blocks = Rules.InitialBlocks(); var p = new Player(Rules.Start);
            var g = Rules.Capture(p, 0, -.14f, 0, blocks);
            Assert(Rules.Placement(p, 0, g, 14.4f, blocks, out blocks[0], out _), "first placement");
            p = new Player(Rules.Staging); g = Rules.Capture(p, 0, -.18f, 1, blocks);
            Assert(Rules.Placement(p, 0, g, 13.05f, blocks, out blocks[1], out _), "second placement");
            return blocks;
        }
        static Block[] Steps() => new[] { new Block(0, 4.7f, -14.05f, (1.8f - Rules.FloorGap) / 1.9f), new Block(1, 4.7f, -12.85f, (.9f - Rules.FloorGap) / 1.55f) };
        public static void Run()
        {
            passed.Clear();
            Check("10,000 independent floor sunlight rays agree with projected safety silhouettes", () =>
            {
                var random = new System.Random(82311); var boxes = new List<Bounds>();
                foreach (var b in Rules.InitialBlocks()) boxes.Add(b.Bounds);
                foreach (var b in Solved()) boxes.Add(b.Bounds); boxes.Add(Rules.LowWall);
                foreach (Bounds box in boxes)
                {
                    var polygon = Rules.Shadow(box);
                    for (int i = 0; i < 2000; i++)
                    {
                        var p = new Vector2(-7 + (float)random.NextDouble() * 14, -16 + (float)random.NextDouble() * 32);
                        bool ray = Rules.RayBox(new Vector3(p.x, 0, p.y), -Rules.Sun, box, out _);
                        Assert(Rules.InPolygon(p, polygon) == ray, "ray/silhouette disagreement at " + p);
                    }
                }
            });
            Check("Grab offsets preserve position; near/far placement scales geometry and shadows", () =>
            {
                var blocks = Rules.InitialBlocks(); var p = new Player(Rules.Start);
                foreach (var angles in new[] { new Vector2(0, -.14f), new Vector2(.05f, -.2f), new Vector2(-.04f, .03f) })
                {
                    Grab g = Rules.Capture(p, angles.x, angles.y, 0, blocks);
                    Assert(Rules.Placement(p, angles.x, g, Rules.DistanceFromAim(angles.y, g), blocks, out Block same, out _), "same placement");
                    Near(same.scale, 1); Near(same.x, blocks[0].x); Near(same.z, blocks[0].z);
                    Assert(Rules.Placement(p, -g.yawOffset, g, 2.5f, blocks, out Block close, out _), "close placement");
                    Assert(Rules.Placement(p, -g.yawOffset, g, 14.4f, blocks, out Block far, out _), "far placement");
                    Near(close.scale / 2.5f, far.scale / 14.4f);
                    Assert(Rules.ShadowLength(Rules.Shadow(far.Bounds)) > Rules.ShadowLength(Rules.Shadow(close.Bounds)) * 4, "shadow growth");
                    blocks[0] = close;
                    g = Rules.Capture(p, angles.x, angles.y, 0, blocks);
                    Assert(Rules.Placement(p, angles.x, g, Rules.DistanceFromAim(angles.y, g), blocks, out Block regrab, out _), "regrab");
                    Near(regrab.scale, close.scale); blocks = Rules.InitialBlocks();
                }
            });
            Check("Placement limits stay inside walls and reject other objects and exit ledge", () =>
            {
                var blocks = Solved(); var p = new Player(Rules.Start); Grab grab = Rules.Capture(p, 0, 0, 0, blocks); int valid = 0;
                for (float yaw = -Mathf.PI; yaw < Mathf.PI; yaw += .13f)
                    for (float pitch = -1.2f; pitch <= 1.2f; pitch += .3f)
                    {
                        if (!Rules.Placement(p, yaw, grab, Rules.DistanceFromAim(pitch, grab), blocks, out Block b, out _)) continue;
                        valid++; Assert(b.Bounds.min.x >= -7 && b.Bounds.max.x <= 7 && b.Bounds.min.z >= -16 && b.Bounds.max.z <= 16, "walls");
                        Assert(b.scale >= .399f && b.scale <= 3.001f, "scale bounds");
                        Assert(!Rules.Overlap(b.Bounds, Rules.ExitLedge) && !Rules.Overlap(b.Bounds, blocks[1].Bounds), "overlap");
                    }
                Assert(valid > 100, "sweep exercised placements");
            });
            Check("Two objects are required; connected route detours around divider and right of screen", () =>
            {
                Block[] initial = Rules.InitialBlocks(), solved = Solved();
                foreach (Block[] blocks in new[] { initial, new[] { solved[0], initial[1] }, new[] { initial[0], solved[1] } })
                    Assert(Rules.Route(blocks, Rules.Shadows(blocks)).Count == 0, "partial route unlocked");
                var route = Rules.Route(solved, Rules.Shadows(solved)); Assert(route.Count > 120, "complete route");
                bool detour = false, right = false;
                foreach (Vector2 p in route)
                {
                    Assert(Rules.Safe(p, Rules.Shadows(solved)) && Rules.CanOccupy(p, solved), "unsafe route point");
                    detour |= Mathf.Abs(p.x) < .8f && p.y < -.64f && p.y > -1.8f;
                    right |= p.x > 6.4f && p.y < -12;
                }
                Assert(detour && right, "required detours");
            });
            Check("Jump reaches 1.1m at 144/60/20 FPS; air input cannot double jump", () =>
            {
                foreach (float dt in new[] { 1 / 144f, 1 / 60f, 1 / 20f })
                {
                    var p = new Player(Rules.Start); float apex = 0;
                    for (int frame = 0; frame < Mathf.CeilToInt(.82f / dt); frame++)
                    { Rules.Simulate(p, Vector2.zero, dt, new Block[0], frame == 0 || frame == Mathf.RoundToInt(.2f / dt)); apex = Mathf.Max(apex, p.feet.y); }
                    Near(apex, 1.1f, .008f); Near(p.feet.y, 0); Assert(p.grounded, "landing");
                }
            });
            Check("Three jumps climb the same two resized objects to the 2.7m exit", () =>
            {
                Block[] blocks = Steps(); var state = new GameState { blocks = blocks, player = new Player(new Vector2(4.7f, -11.85f)) };
                float[] targets = { -12.85f, -14.05f, -15.1f }, heights = { .9f, 1.8f, 2.7f };
                for (int stage = 0; stage < 3; stage++)
                {
                    for (int f = 0; f < 70; f++) Rules.Simulate(state.player, new Vector2(0, state.player.feet.z > targets[stage] + .025f ? -3 : 0), 1 / 120f, blocks, f == 0);
                    Near(state.player.feet.y, heights[stage]); Assert(state.player.grounded, "step landing " + stage);
                    Assert(Rules.SafeAtHeight(state.player, blocks, Rules.Shadows(blocks)), "step shade");
                }
                Assert(!state.CanFinish, "checkpoint needed"); state.shadeSolved = true; Assert(state.CanFinish, "unlocked finish");
                state.held = new Grab(); Assert(!state.CanFinish, "release required");
            });
            Check("Walls stop long movement; leaving a platform falls; buffered jump fires once", () =>
            {
                var p = new Player(Rules.Start); var blocks = Rules.InitialBlocks();
                Rules.Simulate(p, new Vector2(0, -3), 20, blocks); Assert(p.feet.z > blocks[0].Bounds.max.z, "tunnelling");
                blocks = Steps(); p = new Player(blocks[0].Point); p.feet.y = 1.8f;
                Rules.Simulate(p, new Vector2(-3, 0), .8f, blocks); Near(p.feet.y, 0);
                p = new Player(Rules.Start) { vy = -2, grounded = false, support = -2, coyote = 0 }; p.feet.y = .1f;
                Rules.Simulate(p, Vector2.zero, .1f, new Block[0], true); Assert(p.vy > 0 && p.feet.y > .1f, "buffered landing");
                Rules.Simulate(p, Vector2.zero, 1, new Block[0]); Near(p.feet.y, 0); Near(p.jumpBuffer, 0);
            });
            Check("Height-aware shade does not treat a sunlit object top as shelter", () =>
            {
                var blocks = new[] { new Block(0, -3.6f, 6, .4f) }; var p = new Player(new Vector2(-3.5f, 7));
                var shadows = Rules.Shadows(blocks); Assert(Rules.SafeAtHeight(p, blocks, shadows), "floor shade");
                p.feet.y = 1; Assert(!Rules.SafeAtHeight(p, blocks, shadows), "above caster");
                p.feet = new Vector3(-3.6f, blocks[0].Bounds.max.y, 6); Assert(!Rules.SafeAtHeight(p, blocks, shadows), "caster top");
            });
            Check("Checkpoint requires grounded arrival with connected shadows; respawn preserves placements", () =>
            {
                var state = new GameState(); Assert(!state.ReachCheckpoint(true), "remote checkpoint");
                state.player = new Player(new Vector2(6.5f, -14.3f)); Assert(!state.ReachCheckpoint(false), "unconnected checkpoint");
                state.held = new Grab(); Assert(!state.ReachCheckpoint(true), "held checkpoint"); state.held = null;
                Assert(state.ReachCheckpoint(true), "arrival"); state.blocks = Steps();
                state.blocks[1] = new Block(1, 6.4f, -14.3f, .4f); state.Respawn();
                Assert(state.shadeSolved && Rules.CanOccupy(state.player.Point, state.blocks) && Rules.End.Contains(state.player.Point), "saved safe respawn");
                Near(state.blocks[1].scale, .4f); Assert(!new GameState().shadeSolved, "reset");
            });
            Check("Exposure and recovery retain v0.3 rates and clamps", () =>
            { Near(Rules.Exposure(0, 1, false), .48f); Near(Rules.Exposure(.9f, 1, true), .28f); Near(Rules.Exposure(0, 3, false), 1); Near(Rules.Exposure(.5f, 2, true), 0); });
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/rules-results.txt", "Unity " + Application.unityVersion + "\nPassed: " + passed.Count + "\n" + string.Join("\n", passed));
            Debug.Log("SHADE_TESTS_OK " + passed.Count);
        }
    }
}
