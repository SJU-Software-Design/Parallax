using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink
{
    // X/Z geometry is shared by the visible floor, placement preview and safety.
    [Serializable]
    public struct FloorRect
    {
        public float minX, maxX, minZ, maxZ;
        public FloorRect(float x0, float x1, float z0, float z1)
        { minX = x0; maxX = x1; minZ = z0; maxZ = z1; }
        public bool Contains(Vector2 p) => p.x >= minX - Rules.Epsilon && p.x <= maxX + Rules.Epsilon
            && p.y >= minZ - Rules.Epsilon && p.y <= maxZ + Rules.Epsilon;
        public List<Vector2> Polygon() => new List<Vector2>
        { new Vector2(minX, minZ), new Vector2(maxX, minZ), new Vector2(maxX, maxZ), new Vector2(minX, maxZ) };
    }

    [Serializable]
    public struct Block
    {
        public int kind;
        public float x, z, scale;
        public Block(int type, float px, float pz, float size)
        { kind = type; x = px; z = pz; scale = size; }
        public Vector2 Point => new Vector2(x, z);
        public Bounds Bounds
        {
            get
            {
                Vector3 size = Rules.BaseSizes[kind] * scale;
                return new Bounds(new Vector3(x, Rules.FloorGap + size.y / 2, z), size);
            }
        }
    }

    public sealed class Player
    {
        public Vector3 feet;
        public float vy, coyote = .1f, jumpBuffer;
        public bool grounded = true;
        // -1 floor, 0/1 movable blocks, 2 low wall, 3 exit, -2 airborne.
        public int support = -1;
        public Vector2 Point => new Vector2(feet.x, feet.z);
        public Vector3 Eye => feet + Vector3.up * Rules.EyeHeight;
        public Player(Vector2 point) { feet = new Vector3(point.x, 0, point.y); }
    }

    public sealed class Grab
    {
        public int index, kind;
        public float scale, distance, eyeHeight, yawOffset, pitchOffset;
    }

    public sealed class GameState
    {
        public Player player = new Player(Rules.Start);
        public Block[] blocks = Rules.InitialBlocks();
        public Grab held;
        public float yaw, pitch = -.14f, exposure, elapsed;
        public int respawns;
        public bool shadeSolved, won, started, active, visitedRest;
        public bool pictureRequired, pictureRestored;
        public bool groundExit, keyRequired, keyUnlocked;
        public Vector2 checkpoint;
        public void Respawn(Bounds[] extraSolids = null)
        {
            Vector2 spawn = Rules.Start;
            if (shadeSolved)
            {
                float nearest = (1f/0f);
                if (Rules.CanOccupy(checkpoint, blocks, extraSolids, groundExit)) spawn = checkpoint;
                else
                {
                    spawn = Rules.Staging;
                    for (float x = Rules.End.minX + Rules.Radius; x <= 7 - Rules.Radius; x += .2f)
                        for (float z = -16 + Rules.Radius; z < Rules.End.maxZ - Rules.Radius; z += .2f)
                        {
                            var point = new Vector2(x, z);
                            float distance = (point - checkpoint).sqrMagnitude;
                            if (distance < nearest && Rules.CanOccupy(point, blocks, extraSolids, groundExit))
                            { nearest = distance; spawn = point; }
                        }
                }
            }
            if (!shadeSolved && !Rules.CanOccupy(spawn, blocks, extraSolids, groundExit))
            {
                float nearest = (1f/0f);
                for (float x = Rules.Entry.minX + Rules.Radius; x < Rules.Entry.maxX - Rules.Radius; x += .2f)
                    for (float z = Rules.Entry.minZ + Rules.Radius; z < Rules.Entry.maxZ - Rules.Radius; z += .2f)
                    {
                        var p = new Vector2(x, z); float distance = (p - Rules.Start).sqrMagnitude;
                        if (distance < nearest && Rules.CanOccupy(p, blocks, extraSolids, groundExit)) { spawn = p; nearest = distance; }
                    }
            }
            player = new Player(spawn);
            yaw = shadeSolved ? 3.14159274f / 2 : 0;
            pitch = -.14f;
            exposure = 0;
            held = null;
            visitedRest = shadeSolved;
            respawns++;
        }
        public bool ReachCheckpoint(bool connected)
        {
            if (shadeSolved || !connected || held != null || !player.grounded
                || player.feet.y > .01f || !(groundExit ? Rules.End : Rules.Checkpoint).Contains(player.Point)) return false;
            shadeSolved = true;
            checkpoint = player.Point;
            return true;
        }
        public bool CanFinish => shadeSolved && (!pictureRequired || pictureRestored) && (!keyRequired || keyUnlocked)
            && held == null && player.grounded && Mathf.Abs(player.feet.y - (groundExit ? 0 : Rules.ExitHeight)) < .02f
            && Rules.Exit.Contains(player.Point);
    }

    public static class Rules
    {
        public const float Epsilon = .00001f, FloorGap = .025f, EyeHeight = 1.65f, Height = 1.75f;
        public const float Radius = .24f, Speed = 3, Gravity = 16, JumpHeight = 1.1f, ExitHeight = 2.7f;
        public const float ExposureRate = .48f, RecoveryRate = .62f, MinScale = .4f;
        public static readonly FloorRect Room = new FloorRect(-7, 7, -16, 16);
        public static readonly FloorRect Entry = new FloorRect(-7, -1.3f, 11.4f, 16);
        public static readonly FloorRect Rest = new FloorRect(-5.5f, 5.4f, -1.8f, 1.5f);
        public static readonly FloorRect End = new FloorRect(1.2f, 7, -16, -11.4f);
        public static readonly FloorRect Exit = new FloorRect(3.4f, 5.9f, -15.8f, -14.9f);
        public static readonly FloorRect Checkpoint = new FloorRect(6.15f, 6.8f, -14.65f, -14.02f);
        public static readonly FloorRect[] FixedShades = { Entry, Rest, End };
        public static readonly Vector2 Start = new Vector2(-3.6f, 14), Staging = new Vector2(3.5f, 0);
        public static readonly Vector3 Sun = new Vector3(.16f, -1, 2.6f);
        public static readonly Vector3[] BaseSizes = { new Vector3(.85f, 1.9f, .65f), new Vector3(1.9f, 1.55f, .55f) };
        public static readonly float[] MaxScales = { 3, 3.4f };
        public static readonly string[] Names = { "기둥", "차광판" };
        public static readonly Bounds LowWall = new Bounds(new Vector3(0, .4f, .55f), new Vector3(1.6f, .8f, 1.9f));
        public static readonly Bounds ExitLedge = new Bounds(new Vector3(4.65f, 1.35f, -15.4f), new Vector3(2.5f, 2.7f, 1.2f));
        public static Block[] InitialBlocks() => new[] { new Block(0, -3.6f, 9.2f, 1), new Block(1, 3.5f, -4.5f, 1) };
        public static Vector3 Look(float yaw, float pitch) => new Vector3(-Mathf.Sin(yaw) * Mathf.Cos(pitch),
            Mathf.Sin(pitch), -Mathf.Cos(yaw) * Mathf.Cos(pitch));
        static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        public static List<Vector2> Hull(List<Vector2> points)
        {
            points.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var unique = new List<Vector2>();
            foreach (Vector2 p in points)
                if (unique.Count == 0 || (p - unique[unique.Count - 1]).sqrMagnitude > 1e-12f) unique.Add(p);
            if (unique.Count < 3) return unique;
            var lower = new List<Vector2>();
            var upper = new List<Vector2>();
            foreach (Vector2 p in unique)
            {
                while (lower.Count > 1 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= Epsilon) lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }
            for (int i = unique.Count - 1; i >= 0; i--)
            {
                Vector2 p = unique[i];
                while (upper.Count > 1 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= Epsilon) upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }
            lower.RemoveAt(lower.Count - 1); upper.RemoveAt(upper.Count - 1); lower.AddRange(upper);
            return lower;
        }
        static List<Vector2> Clip(List<Vector2> polygon, int axis, float boundary, int sign)
        {
            var output = new List<Vector2>();
            if (polygon.Count == 0) return output;
            Vector2 a = polygon[polygon.Count - 1];
            foreach (Vector2 b in polygon)
            {
                bool inA = sign * (a[axis] - boundary) >= -Epsilon, inB = sign * (b[axis] - boundary) >= -Epsilon;
                if (inA != inB) output.Add(Vector2.LerpUnclamped(a, b, (boundary - a[axis]) / (b[axis] - a[axis])));
                if (inB) output.Add(b);
                a = b;
            }
            return output;
        }
        public static List<Vector2> Shadow(Bounds box)
        {
            var points = new List<Vector2>();
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
            {
                Vector3 p = new Vector3(x == 0 ? box.min.x : box.max.x, y == 0 ? box.min.y : box.max.y, z == 0 ? box.min.z : box.max.z);
                p += Sun * (-p.y / Sun.y); points.Add(new Vector2(p.x, p.z));
            }
            var hull = Hull(points);
            hull = Clip(hull, 0, -7, 1); hull = Clip(hull, 0, 7, -1);
            hull = Clip(hull, 1, -16, 1); hull = Clip(hull, 1, 16, -1);
            return hull;
        }
        public static List<Vector2>[] Shadows(Block[] blocks)
        {
            var result = new List<Vector2>[blocks.Length + 1];
            for (int i = 0; i < blocks.Length; i++) result[i] = Shadow(blocks[i].Bounds);
            result[blocks.Length] = Shadow(LowWall); return result;
        }
        public static bool InPolygon(Vector2 point, List<Vector2> polygon)
        {
            if (polygon.Count < 3) return false;
            for (int i = 0; i < polygon.Count; i++)
                if (Cross(polygon[i], polygon[(i + 1) % polygon.Count], point) < -Epsilon) return false;
            return true;
        }
        public static bool InFixedShade(Vector2 point)
        { foreach (var rect in FixedShades) if (rect.Contains(point)) return true; return false; }
        public static bool Safe(Vector2 point, List<Vector2>[] shadows)
        {
            if (!Room.Contains(point)) return false;
            if (InFixedShade(point)) return true;
            foreach (var poly in shadows) if (InPolygon(point, poly)) return true;
            return false;
        }
        public static bool RayBox(Vector3 origin, Vector3 direction, Bounds bounds, out float near)
        {
            near = 0; float far = (1f/0f);
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(direction[axis]) < 1e-8f)
                { if (origin[axis] < bounds.min[axis] || origin[axis] > bounds.max[axis]) return false; continue; }
                float a = (bounds.min[axis] - origin[axis]) / direction[axis];
                float b = (bounds.max[axis] - origin[axis]) / direction[axis];
                near = Mathf.Max(near, Mathf.Min(a, b)); far = Mathf.Min(far, Mathf.Max(a, b));
            }
            return far >= near;
        }
        public static bool SafeAtHeight(Player player, Block[] blocks, List<Vector2>[] shadows)
        {
            if (player.feet.y <= Epsilon) return Safe(player.Point, shadows);
            if (!Room.Contains(player.Point)) return false;
            if (InFixedShade(player.Point)) return true;
            Vector3 origin = player.feet + Vector3.up * .0001f;
            foreach (Block block in blocks) if (RayBox(origin, -Sun, block.Bounds, out _)) return true;
            return RayBox(origin, -Sun, LowWall, out _);
        }
        public static bool Touches(Vector2 point, Bounds box, float radius = Radius)
        {
            float dx = point.x - Mathf.Clamp(point.x, box.min.x, box.max.x);
            float dz = point.y - Mathf.Clamp(point.y, box.min.z, box.max.z);
            return dx * dx + dz * dz < radius * radius;
        }
        public static Bounds[] Solids(Block[] blocks, Bounds[] extraSolids = null, bool groundExit = false)
        {
            var solids = new Bounds[blocks.Length + 2 + (extraSolids == null ? 0 : extraSolids.Length)];
            for (int i = 0; i < blocks.Length; i++) solids[i] = blocks[i].Bounds;
            solids[blocks.Length] = LowWall;
            // Preserve support indices for the archived raised-exit levels.
            solids[blocks.Length + 1] = groundExit ? new Bounds(new Vector3(100, -10, 100), Vector3.zero) : ExitLedge;
            if (extraSolids != null) Array.Copy(extraSolids, 0, solids, blocks.Length + 2, extraSolids.Length);
            return solids;
        }
        static bool WithinWalls(Vector2 point) => point.x >= -7 + Radius && point.x <= 7 - Radius && point.y >= -16 + Radius && point.y <= 16 - Radius;
        public static bool CanOccupy(Vector2 point, Block[] blocks, Bounds[] extraSolids = null, bool groundExit = false)
        {
            if (!WithinWalls(point)) return false;
            foreach (Bounds box in Solids(blocks, extraSolids, groundExit)) if (Touches(point, box)) return false;
            return true;
        }
        static bool Fits(Vector3 feet, Bounds[] solids)
        {
            var point = new Vector2(feet.x, feet.z);
            if (!WithinWalls(point)) return false;
            foreach (Bounds box in solids)
                if (feet.y < box.max.y - Epsilon && feet.y + Height > box.min.y + Epsilon && Touches(point, box)) return false;
            return true;
        }
        public static void Simulate(Player p, Vector2 velocity, float seconds, Block[] blocks, bool jump = false, Bounds[] extraSolids = null, bool groundExit = false)
        {
            if (jump) p.jumpBuffer = .12f;
            Bounds[] solids = Solids(blocks, extraSolids, groundExit);
            int steps = Mathf.Max(1, Mathf.CeilToInt(seconds * 120)); float dt = seconds / steps;
            for (int i = 0; i < steps; i++)
            {
                int support = -1;
                for (int j = 0; j < solids.Length; j++)
                    if (Mathf.Abs(p.feet.y - solids[j].max.y) < .001f && Touches(p.Point, solids[j], .1f)) { support = j; break; }
                p.grounded = p.vy <= Epsilon && (p.feet.y <= Epsilon || support >= 0);
                p.support = p.grounded ? support : -2;
                p.coyote = p.grounded ? .1f : Mathf.Max(0, p.coyote - dt);
                if (p.jumpBuffer > 0 && p.coyote > 0)
                { p.vy = Mathf.Sqrt(2 * Gravity * JumpHeight); p.grounded = false; p.support = -2; p.coyote = 0; p.jumpBuffer = 0; }
                p.jumpBuffer = Mathf.Max(0, p.jumpBuffer - dt);
                Vector3 trial = p.feet + new Vector3(velocity.x * dt, 0, 0);
                if (Fits(trial, solids)) p.feet.x = trial.x;
                trial = p.feet + new Vector3(0, 0, velocity.y * dt);
                if (Fits(trial, solids)) p.feet.z = trial.z;
                float y = p.feet.y + p.vy * dt - Gravity * dt * dt / 2;
                p.vy -= Gravity * dt;
                if (y + Height > 7.7f) { y = 7.7f - Height; p.vy = Mathf.Min(0, p.vy); }
                int landed = -1;
                if (p.vy <= 0)
                    for (int j = 0; j < solids.Length; j++)
                        if (p.feet.y >= solids[j].max.y - Epsilon && y <= solids[j].max.y && Touches(p.Point, solids[j], .1f)
                            && (landed < 0 || solids[j].max.y > solids[landed].max.y)) landed = j;
                p.grounded = landed >= 0 || y <= 0; p.support = p.grounded ? landed : -2;
                p.feet.y = landed >= 0 ? solids[landed].max.y : Mathf.Max(0, y);
                if (p.grounded) p.vy = 0;
            }
        }
        public static Grab Capture(Player player, float yaw, float pitch, int index, Block[] blocks)
        {
            Block block = blocks[index]; Vector2 delta = block.Point - player.Point;
            return new Grab { index = index, kind = block.kind, scale = block.scale, distance = delta.magnitude, eyeHeight = player.Eye.y,
                yawOffset = Mathf.Atan2(-delta.x, -delta.y) - yaw,
                pitchOffset = Mathf.Atan2(block.Bounds.center.y - player.Eye.y, delta.magnitude) - pitch };
        }
        public static float DistanceFromAim(float pitch, Grab grab)
        {
            float denominator = BaseSizes[grab.kind].y * grab.scale / (2 * grab.distance)
                - Mathf.Tan(Mathf.Clamp(pitch + grab.pitchOffset, -1.45f, 1.45f));
            return denominator > Epsilon ? (grab.eyeHeight - FloorGap) / denominator : 10000;
        }
        public static bool Overlap(Bounds a, Bounds b, float gap = .035f) => a.min.x < b.max.x + gap && a.max.x > b.min.x - gap
            && a.min.z < b.max.z + gap && a.max.z > b.min.z - gap;
        public static bool Placement(Player player, float yaw, Grab grab, float requested, Block[] blocks, out Block placed, out bool limited, bool groundExit = false)
        {
            placed = blocks[grab.index]; limited = false;
            Vector3 size = BaseSizes[grab.kind]; float ratio = grab.scale / grab.distance;
            Vector2 direction = new Vector2(-Mathf.Sin(yaw + grab.yawOffset), -Mathf.Cos(yaw + grab.yawOffset));
            float maximum = Mathf.Min(36, Mathf.Min(MaxScales[grab.kind] / ratio, 7.9f / (size.y * ratio)));
            for (int axis = 0; axis < 2; axis++)
            {
                float half = (axis == 0 ? size.x : size.z) * ratio / 2, low = axis == 0 ? -7 : -16, high = -low;
                float towardHigh = direction[axis] + half, towardLow = -direction[axis] + half;
                if (towardHigh > Epsilon) maximum = Mathf.Min(maximum, (high - player.Point[axis] - .045f) / towardHigh);
                if (towardLow > Epsilon) maximum = Mathf.Min(maximum, (player.Point[axis] - low - .045f) / towardLow);
            }
            float minimum = Mathf.Max(.05f, MinScale / ratio);
            if (maximum < minimum) return false;
            float distance = Mathf.Clamp(requested, minimum, maximum);
            var candidate = new Block(grab.kind, player.feet.x + direction.x * distance, player.feet.z + direction.y * distance, ratio * distance);
            Bounds box = candidate.Bounds;
            if (Touches(player.Point, box) || Overlap(box, LowWall) || (!groundExit && Overlap(box, ExitLedge))) return false;
            for (int i = 0; i < blocks.Length; i++) if (i != grab.index && Overlap(box, blocks[i].Bounds)) return false;
            placed = candidate; limited = Mathf.Abs(distance - requested) > .02f; return true;
        }
        public static int Aim(Player player, float yaw, float pitch, Block[] blocks, bool groundExit = false)
        {
            Vector3 direction = Look(yaw, pitch); float nearest = 40; int selected = -1;
            foreach (Bounds box in groundExit ? new[] { LowWall } : new[] { LowWall, ExitLedge })
                if (RayBox(player.Eye, direction, box, out float distance)) nearest = Mathf.Min(nearest, distance);
            for (int i = 0; i < blocks.Length; i++)
                if (RayBox(player.Eye, direction, blocks[i].Bounds, out float distance) && distance < nearest)
                { nearest = distance; selected = i; }
            return selected;
        }
        public static float Exposure(float value, float dt, bool safe) => Mathf.Clamp01(value + dt * (safe ? -RecoveryRate : ExposureRate));
        public static float ShadowLength(List<Vector2> polygon)
        {
            if (polygon.Count == 0) return 0; float min = (1f/0f), max = (-1f/0f);
            foreach (Vector2 p in polygon) { min = Mathf.Min(min, p.y); max = Mathf.Max(max, p.y); } return max - min;
        }

        // Grid is a connectivity aid only. Foot safety uses exact polygons, never sampled pixels.
        public static List<Vector2> Route(Block[] blocks, List<Vector2>[] shadows, FloorRect? startRegion = null, FloorRect? goalRegion = null, Bounds[] extraSolids = null, bool groundExit = false)
        {
            const float step = .25f; Vector2 origin = new Vector2(-7 + Radius, -16 + Radius);
            int cols = Mathf.FloorToInt((7 - Radius - origin.x) / step) + 1;
            int rows = Mathf.FloorToInt((16 - Radius - origin.y) / step) + 1;
            int count = cols * rows; var parent = new int[count]; var queue = new int[count];
            Bounds[] solids = Solids(blocks, extraSolids, groundExit); FloorRect goal = goalRegion ?? Checkpoint;
            for (int i = 0; i < count; i++) parent[i] = -1;
            Func<int, Vector2> pointAt = i => origin + new Vector2((i % cols) * step, (i / cols) * step);
            Func<Vector2, bool> walkable = p => { foreach (Bounds b in solids) if (Touches(p, b)) return false; return true; };
            int head = 0, tail = 0;
            if (startRegion.HasValue)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector2 point = pointAt(i);
                    if (startRegion.Value.Contains(point) && Safe(point, shadows) && walkable(point))
                    { queue[tail++] = i; parent[i] = i; }
                }
            }
            else
            {
                int x = Mathf.Clamp(Mathf.RoundToInt((Start.x - origin.x) / step), 0, cols - 1);
                int z = Mathf.Clamp(Mathf.RoundToInt((Start.y - origin.y) / step), 0, rows - 1); int first = z * cols + x;
                Vector2 point = pointAt(first);
                if (!Safe(point, shadows) || !walkable(point)) return new List<Vector2>();
                queue[tail++] = first; parent[first] = first;
            }
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            while (head < tail)
            {
                int current = queue[head++]; Vector2 point = pointAt(current);
                if (goal.Contains(point))
                {
                    var route = new List<Vector2> { point }; int cursor = current;
                    while (parent[cursor] != cursor) { cursor = parent[cursor]; route.Add(pointAt(cursor)); }
                    route.Reverse(); return route;
                }
                for (int n = 0; n < 4; n++)
                {
                    int x = current % cols + dx[n], z = current / cols + dz[n];
                    if (x < 0 || x >= cols || z < 0 || z >= rows) continue;
                    int index = z * cols + x; if (parent[index] != -1) continue;
                    Vector2 next = pointAt(index), middle = (point + next) / 2;
                    if (!Safe(next, shadows) || !walkable(next)) { parent[index] = -2; continue; }
                    if (!Safe(middle, shadows) || !walkable(middle)) continue;
                    parent[index] = current; queue[tail++] = index;
                }
            }
            return new List<Vector2>();
        }
    }
}
