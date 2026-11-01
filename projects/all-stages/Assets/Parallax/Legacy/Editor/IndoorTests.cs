using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class IndoorTests
    {
        static void Require(bool value, string message) { if (!value) throw new Exception("INDOOR_RULES: " + message); }
        public static void Run()
        {
            var checks = new List<string>(); var initial = IndoorRules.InitialBlocks(); var shadows = IndoorRules.Shadows(initial);
            Require(IndoorRules.Safe(new Vector2(-4, 2), shadows) && IndoorRules.Safe(new Vector2(4, 16), shadows), "Natural dark areas must be safe.");
            Require(!IndoorRules.Safe(new Vector2(-4.25f, 10), shadows) && !IndoorRules.Safe(new Vector2(4.25f, 24), shadows), "Light must cover both wall edges.");
            Require(IndoorRules.Route(initial, shadows, IndoorRules.Start, 13.25f).Count == 0, "Initial first passage can be bypassed.");
            Require(IndoorRules.Route(initial, shadows, new Vector2(0, 17), 27.25f).Count == 0, "Initial second passage can be bypassed.");
            checks.Add("Dark shelters are safe; both unsolved passages disconnect the safe floor, including wall edges.");

            var blocks = IndoorRules.InitialBlocks();
            for (int index = 0; index < 2; index++)
            {
                var player = new Player(index == 0 ? IndoorRules.Start : new Vector2(0, 17));
                Block before = blocks[index]; Vector3 delta = before.Bounds.center - player.Eye;
                float yaw = Mathf.Atan2(delta.x, delta.z), pitch = Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude);
                Grab grab = IndoorRules.Capture(player, yaw, pitch, index, blocks);
                float targetZ = index == 0 ? 14.7f : 28.5f, distance = targetZ - player.feet.z, scale = grab.scale * distance / grab.distance;
                pitch = Mathf.Atan2(Rules.FloorGap + Rules.BaseSizes[index].y * scale / 2 - player.Eye.y, distance) - grab.pitchOffset;
                Require(IndoorRules.Placement(player, 0, pitch, grab, blocks, out Block candidate, out _), "Reachable perspective placement " + index);
                Require(Mathf.Abs(candidate.scale / (candidate.Point - player.Point).magnitude - before.scale / grab.distance) < .00001f, "Perspective ratio changed.");
                Require(Mathf.Abs(candidate.z - targetZ) < .001f, "Floor projection position.");
                blocks[index] = candidate;
                shadows = IndoorRules.Shadows(blocks);
                Require(IndoorRules.Route(blocks, shadows, player.Point, index == 0 ? 13.3f : 27.3f).Count > 0, "Playable shadow bridge " + index);
                Require(!IndoorRules.Valid(new Block(index, 9, targetZ, 1), index, player.Point, blocks), "Outside wall placement accepted.");
            }
            checks.Add("Both passages solve through perspective-preserving placement with ground-level walking routes.");
            checks.Add("Placements outside the room are rejected.");

            // Independent 3D segment test checks the analytical polygons against
            // the actual light-to-floor occlusion, including overlapping shadows.
            int samples = 0;
            foreach (var arrangement in new[] { initial, blocks })
            {
                var polygons = IndoorRules.Shadows(arrangement);
                for (int l = 0; l < 2; l++) for (float z = IndoorRules.Beams[l].minZ + .073f; z < IndoorRules.Beams[l].maxZ; z += .31f)
                    for (float x = -4.49f; x < 4.5f; x += .27f)
                    {
                        var floor = new Vector3(x, 0, z); Vector3 d = IndoorRules.Lamps[l] - floor; bool occluded = false;
                        foreach (var block in arrangement) if (Rules.RayBox(floor, d.normalized, block.Bounds, out float distance) && distance < d.magnitude) occluded = true;
                        Require(IndoorRules.Safe(new Vector2(x, z), polygons) == occluded, "Shadow/ray mismatch at " + floor); samples++;
                    }
            }
            checks.Add("Visible shadow polygons agree with point-light occlusion across " + samples + " floor samples.");
            Directory.CreateDirectory("Validation/Indoor"); File.WriteAllText("Validation/Indoor/rules-results.txt", string.Join("\n", checks));
            Debug.Log("INDOOR_TESTS_OK " + checks.Count);
        }
    }
}
