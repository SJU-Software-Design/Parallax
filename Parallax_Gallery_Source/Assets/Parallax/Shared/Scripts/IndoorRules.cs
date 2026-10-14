using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink
{
    // Each ceiling fixture has a rectangular shutter. The projected silhouette
    // is shared by the floor mesh, route preview and exposure test.
    public static class IndoorRules
    {
        public const float HalfWidth = 4.6f, Length = 36, Speed = 3, Radius = .24f;
        public const float ExposureRate = .8f, RecoveryRate = .9f;
        public static readonly Vector2 Start = new Vector2(0, 2.5f);
        public static readonly FloorRect[] Beams = {
            new FloorRect(-HalfWidth, HalfWidth, 5, 13),
            new FloorRect(-HalfWidth, HalfWidth, 19, 27)
        };
        public static readonly Vector3[] Lamps = { new Vector3(0, 7.2f, 20), new Vector3(0, 7.2f, 35) };
        public static Block[] InitialBlocks() => new[] { new Block(0, 0, 6.6f, .9f), new Block(1, 0, 20.2f, .9f) };
        public static Vector3 Look(float yaw, float pitch) => new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));

        static List<Vector2> Clip(List<Vector2> poly, int axis, float value, float sign)
        {
            var result = new List<Vector2>();
            if (poly.Count == 0) return result;
            Vector2 a = poly[poly.Count - 1];
            foreach (Vector2 b in poly)
            {
                bool ia = sign * (a[axis] - value) >= -Rules.Epsilon, ib = sign * (b[axis] - value) >= -Rules.Epsilon;
                if (ia != ib) result.Add(Vector2.LerpUnclamped(a, b, (value - a[axis]) / (b[axis] - a[axis])));
                if (ib) result.Add(b);
                a = b;
            }
            return result;
        }
        public static List<Vector2> Shadow(Bounds box, int lamp)
        {
            Vector3 light = Lamps[lamp]; var points = new List<Vector2>();
            if (box.max.y >= light.y - .05f) return points;
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
            {
                Vector3 p = new Vector3(x == 0 ? box.min.x : box.max.x, y == 0 ? box.min.y : box.max.y, z == 0 ? box.min.z : box.max.z);
                Vector3 floor = light + (p - light) * (light.y / (light.y - p.y));
                points.Add(new Vector2(floor.x, floor.z));
            }
            var r = Beams[lamp]; var hull = Rules.Hull(points);
            hull = Clip(hull, 0, r.minX, 1); hull = Clip(hull, 0, r.maxX, -1);
            hull = Clip(hull, 1, r.minZ, 1); return Clip(hull, 1, r.maxZ, -1);
        }
        public static List<Vector2>[] Shadows(Block[] blocks)
        {
            var result = new List<Vector2>[Beams.Length * blocks.Length];
            for (int l = 0; l < Beams.Length; l++) for (int b = 0; b < blocks.Length; b++) result[l * blocks.Length + b] = Shadow(blocks[b].Bounds, l);
            return result;
        }
        public static bool Safe(Vector2 p, List<Vector2>[] shadows)
        {
            if (Mathf.Abs(p.x) > HalfWidth || p.y < 0 || p.y > Length) return false;
            for (int l = 0; l < Beams.Length; l++)
            {
                if (!Beams[l].Contains(p)) continue;
                bool blocked = false; int perLamp = shadows.Length / Beams.Length;
                for (int b = 0; b < perLamp; b++) blocked |= Rules.InPolygon(p, shadows[l * perLamp + b]);
                if (!blocked) return false;
            }
            return true;
        }
        public static bool FootSafe(Vector2 p, List<Vector2>[] shadows)
        {
            const float margin = .13f;
            return Safe(p, shadows) && Safe(p + Vector2.right * margin, shadows) && Safe(p - Vector2.right * margin, shadows)
                && Safe(p + Vector2.up * margin, shadows) && Safe(p - Vector2.up * margin, shadows);
        }
        public static bool CanWalk(Vector2 p, Block[] blocks)
        {
            if (Mathf.Abs(p.x) > HalfWidth - Radius || p.y < Radius || p.y > Length - Radius) return false;
            foreach (var block in blocks) if (Rules.Touches(p, block.Bounds, Radius)) return false;
            return true;
        }
        public static bool Valid(Block candidate, int index, Vector2 player, Block[] blocks)
        {
            Bounds b = candidate.Bounds;
            float minZ = index == 0 ? .6f : 16.5f, maxZ = index == 0 ? 18.4f : 33.5f;
            if (candidate.scale < .4f || candidate.scale > Rules.MaxScales[candidate.kind] + .001f
                || b.max.y > 6.6f || b.min.x < -HalfWidth + .05f || b.max.x > HalfWidth - .05f
                || b.min.z < minZ || b.max.z > maxZ || Rules.Touches(player, b, Radius + .05f)) return false;
            for (int i = 0; i < blocks.Length; i++) if (i != index && Rules.Overlap(b, blocks[i].Bounds)) return false;
            return true;
        }
        public static Grab Capture(Player player, float yaw, float pitch, int index, Block[] blocks)
        {
            Block b = blocks[index]; Vector2 d = b.Point - player.Point;
            return new Grab { index = index, kind = b.kind, scale = b.scale, distance = d.magnitude, eyeHeight = player.Eye.y,
                yawOffset = Mathf.Atan2(d.x, d.y) - yaw, pitchOffset = Mathf.Atan2(b.Bounds.center.y - player.Eye.y, d.magnitude) - pitch };
        }
        public static bool Placement(Player player, float yaw, float pitch, Grab grab, Block[] blocks, out Block candidate, out bool limited)
        {
            float ratio = grab.scale / grab.distance, requested = Rules.DistanceFromAim(pitch, grab);
            float distance = Mathf.Clamp(requested, .4f / ratio, Rules.MaxScales[grab.kind] / ratio);
            Vector2 direction = new Vector2(Mathf.Sin(yaw + grab.yawOffset), Mathf.Cos(yaw + grab.yawOffset));
            Vector2 p = player.Point + direction * distance;
            candidate = new Block(grab.kind, p.x, p.y, distance * ratio);
            limited = Mathf.Abs(distance - requested) > .02f;
            return Valid(candidate, grab.index, player.Point, blocks);
        }
        public static int Aim(Player player, float yaw, float pitch, Block[] blocks)
        {
            float nearest = 18; int hit = -1; Vector3 direction = Look(yaw, pitch);
            for (int i = 0; i < blocks.Length; i++)
                if (Rules.RayBox(player.Eye, direction, blocks[i].Bounds, out float distance) && distance < nearest) { hit = i; nearest = distance; }
            return hit;
        }
        // Routes are a preview aid; movement/exposure use continuous geometry.
        public static List<Vector2> Route(Block[] blocks, List<Vector2>[] shadows, Vector2 start, float goalZ)
        {
            const float step = .2f; const int cols = 44, rows = 179;
            var origin = new Vector2(-4.3f, .3f); int count = cols * rows;
            var parents = new int[count]; var queue = new int[count];
            for (int i = 0; i < count; i++) parents[i] = -1;
            int sx = Mathf.Clamp(Mathf.RoundToInt((start.x - origin.x) / step), 0, cols - 1);
            int sz = Mathf.Clamp(Mathf.RoundToInt((start.y - origin.y) / step), 0, rows - 1), first = sz * cols + sx;
            var firstPoint = origin + new Vector2(sx, sz) * step;
            if (!CanWalk(firstPoint, blocks) || !FootSafe(firstPoint, shadows)) return new List<Vector2>();
            int head = 0, tail = 0; queue[tail++] = first; parents[first] = first;
            int[] dx = { 0, -1, 1, 0 }, dz = { 1, 0, 0, -1 };
            while (head < tail)
            {
                int current = queue[head++]; var p = origin + new Vector2(current % cols, current / cols) * step;
                if (p.y >= goalZ)
                {
                    var route = new List<Vector2>(); int cursor = current;
                    while (true) { route.Add(origin + new Vector2(cursor % cols, cursor / cols) * step); if (parents[cursor] == cursor) break; cursor = parents[cursor]; }
                    route.Reverse(); return route;
                }
                for (int d = 0; d < 4; d++)
                {
                    int x = current % cols + dx[d], z = current / cols + dz[d];
                    if (x < 0 || x >= cols || z < 0 || z >= rows) continue;
                    int next = z * cols + x; if (parents[next] != -1) continue;
                    Vector2 np = origin + new Vector2(x, z) * step;
                    if (!CanWalk(np, blocks) || !FootSafe(np, shadows)) { parents[next] = -2; continue; }
                    Vector2 middle = (p + np) / 2;
                    if (!CanWalk(middle, blocks) || !FootSafe(middle, shadows)) continue;
                    parents[next] = current; queue[tail++] = next;
                }
            }
            return new List<Vector2>();
        }
    }
}
