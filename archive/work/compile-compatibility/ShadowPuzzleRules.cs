using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink
{
    public enum ShadowStage { Arranging, Ready, Carrying, Restored }

    public sealed class ShadowShape
    {
        public string name;
        public Color color;
        public List<Vector2> outline;
        public Vector3[] vertices;
        public int[] triangles;
        public Bounds bounds;
        public Vector2 solution, initial;
        public ShadowShape(string label, Color tint, List<Vector2> points, Vector2 solved, Vector2 start)
        {
            name = label; color = tint; outline = Rules.Hull(points); solution = solved; initial = start;
            // Upright, solid geometric plates. Their actual vertices project onto
            // the floor; both the renderer and matcher use that same projection.
            int n = outline.Count;
            vertices = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                float y = .035f + outline[i].y / Rules.Sun.z;
                float x = outline[i].x - Rules.Sun.x * y;
                vertices[i] = new Vector3(x, y, -.045f);
                vertices[i + n] = new Vector3(x, y, .045f);
            }
            var faces = new List<int>();
            for (int i = 1; i < n - 1; i++)
            { faces.AddRange(new[] { 0, i + 1, i, n, n + i, n + i + 1 }); }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                faces.AddRange(new[] { i, j, n + j, i, n + j, n + i });
            }
            triangles = faces.ToArray(); bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 v in vertices) bounds.Encapsulate(v);
        }
    }

    [Serializable]
    public struct ShadowPiece
    {
        public int shape;
        public Vector2 position;
        public float scale;
        public ShadowPiece(int index, Vector2 at, float size = 1) { shape = index; position = at; scale = size; }
        public Bounds Bounds => new Bounds(new Vector3(position.x, 0, position.y) + ShadowPuzzleRules.Shapes[shape].bounds.center * scale,
            ShadowPuzzleRules.Shapes[shape].bounds.size * scale);
    }

    public struct ShadowMatch
    {
        public float coverage, precision, iou, leastPart;
        public bool complete => coverage >= .94f && precision >= .92f && iou >= .90f && leastPart >= .82f;
    }

    public sealed class ShadowPuzzleState
    {
        public ShadowPiece[] pieces = ShadowPuzzleRules.InitialPieces();
        public Player player = new Player(new Vector2(0, 6.5f));
        public float yaw, pitch = -.43f, elapsed, stable;
        public Grab held;
        public bool fixedSize, active, started, won;
        public ShadowStage stage;
        public ShadowMatch match;
        public List<Vector2>[] frozen;
        public Vector2 tokenOffset;
        public float tokenScale = 1;
        public bool DoorUnlocked => stage == ShadowStage.Restored;
        public bool Evaluate(float dt)
        {
            if (stage != ShadowStage.Arranging) return false;
            match = ShadowPuzzleRules.Match(ShadowPuzzleRules.Shadows(pieces));
            stable = held == null && match.complete ? stable + dt : 0;
            if (stable < .65f) return false;
            frozen = ShadowPuzzleRules.Shadows(pieces); // Immutable earned silhouette.
            stage = ShadowStage.Ready; return true;
        }
        public bool PickShadow()
        {
            if (stage != ShadowStage.Ready || held != null) return false;
            stage = ShadowStage.Carrying; return true;
        }
        public bool DropShadow(Vector2 at)
        {
            if (stage != ShadowStage.Carrying) return false;
            tokenOffset = new Vector2(Mathf.Clamp(at.x, -4, 4), Mathf.Clamp(at.y, -5.2f, 6.3f));
            tokenScale = .6f; stage = ShadowStage.Ready; return true;
        }
        public bool Restore()
        {
            if (stage != ShadowStage.Carrying) return false;
            stage = ShadowStage.Restored; return true;
        }
    }

    public static class ShadowPuzzleRules
    {
        public const float MinScale = .45f, MaxScale = 1.7f, Reach = 15, Sample = .10f;
        public static readonly FloorRect Room = new FloorRect(-7, 7, -8.6f, 8.6f);
        public static readonly Bounds Frame = new Bounds(new Vector3(0, 2.45f, -8.28f), new Vector3(5.8f, 3.8f, .24f));
        public static readonly Bounds Door = new Bounds(new Vector3(5, 1.6f, -8.3f), new Vector3(2.2f, 3.2f, .25f));
        public static readonly ShadowShape[] Shapes = CreateShapes();
        public static readonly List<Vector2>[] Target = Shadows(SolvedPieces());
        static readonly Vector2[] samples;
        static readonly byte[] targetMasks;
        static readonly int targetArea;

        static ShadowPuzzleRules()
        {
            var points = new List<Vector2>(); var masks = new List<byte>(); int area = 0;
            // Cover the entire room: oversize and out-of-target shadows count as
            // false positives, even when they completely cover the dog.
            for (float z = Room.minZ + Sample / 2; z < Room.maxZ; z += Sample)
                for (float x = Room.minX + Sample / 2; x < Room.maxX; x += Sample)
                {
                    var p = new Vector2(x, z); byte mask = 0;
                    for (int i = 0; i < Target.Length; i++) if (Rules.InPolygon(p, Target[i])) mask |= (byte)(1 << i);
                    points.Add(p); masks.Add(mask); if (mask != 0) area++;
                }
            samples = points.ToArray(); targetMasks = masks.ToArray(); targetArea = area;
        }
        static List<Vector2> Ellipse(float xRadius, float zRadius)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < 32; i++)
            { float a = i * 3.14159274f * 2 / 32; points.Add(new Vector2(Mathf.Cos(a) * xRadius, zRadius + Mathf.Sin(a) * zRadius)); }
            return points;
        }
        static List<Vector2> Rectangle(float width, float depth) => new FloorRect(-width / 2, width / 2, 0, depth).Polygon();
        static ShadowShape[] CreateShapes() => new[]
        {
            new ShadowShape("몸통 · 타원판", new Color(.88f,.62f,.29f), Ellipse(1.45f,.70f), new Vector2(-.45f,-.95f), new Vector2(-3.9f,2.9f)),
            new ShadowShape("머리 · 원판", new Color(.41f,.72f,.73f), Ellipse(.67f,.67f), new Vector2(1,-2), new Vector2(3.5f,2.5f)),
            new ShadowShape("주둥이 · 직사각판", new Color(.92f,.77f,.50f), Rectangle(.94f,.50f), new Vector2(1.63f,-1.24f), new Vector2(4.8f,.4f)),
            new ShadowShape("귀 · 삼각판", new Color(.67f,.55f,.81f), new List<Vector2> { new Vector2(-.30f,0),new Vector2(.40f,.65f),new Vector2(-.25f,.90f) }, new Vector2(.70f,-2.43f), new Vector2(2.8f,-4.4f)),
            new ShadowShape("앞다리 · 발판", new Color(.49f,.72f,.49f), new List<Vector2> { new Vector2(-.19f,0),new Vector2(.19f,0),new Vector2(.19f,.78f),new Vector2(.43f,.90f),new Vector2(.43f,1.10f),new Vector2(-.19f,1.10f) }, new Vector2(.60f,.22f), new Vector2(4.5f,4.5f)),
            new ShadowShape("뒷다리 · 발판", new Color(.83f,.48f,.39f), Rectangle(.43f,1.13f), new Vector2(-1.05f,.20f), new Vector2(-4.6f,-.7f)),
            new ShadowShape("꼬리 · 삼각판", new Color(.56f,.65f,.88f), new List<Vector2> { new Vector2(-.75f,0),new Vector2(.55f,1.03f),new Vector2(.25f,1.35f) }, new Vector2(-1.72f,-1.65f), new Vector2(-3.8f,-3.5f))
        };
        public static ShadowPiece[] InitialPieces()
        {
            var result = new ShadowPiece[Shapes.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new ShadowPiece(i, Shapes[i].initial);
            return result;
        }
        public static ShadowPiece[] SolvedPieces()
        {
            var result = new ShadowPiece[Shapes.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new ShadowPiece(i, Shapes[i].solution);
            return result;
        }
        public static List<Vector2> Shadow(ShadowPiece piece)
        {
            var points = new List<Vector2>();
            foreach (Vector3 v in Shapes[piece.shape].vertices)
            {
                Vector3 p = v * piece.scale + new Vector3(piece.position.x, 0, piece.position.y);
                p += Rules.Sun * (-p.y / Rules.Sun.y); points.Add(new Vector2(p.x, p.z));
            }
            return Rules.Hull(points);
        }
        public static List<Vector2>[] Shadows(ShadowPiece[] pieces)
        {
            var result = new List<Vector2>[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) result[i] = Shadow(pieces[i]);
            return result;
        }
        public static bool Contains(Vector2 p, List<Vector2>[] polygons)
        { foreach (var polygon in polygons) if (Rules.InPolygon(p, polygon)) return true; return false; }
        public static ShadowMatch Match(List<Vector2>[] polygons)
        {
            int intersection = 0, actualArea = 0;
            var exclusive = new int[Shapes.Length]; var covered = new int[Shapes.Length];
            // Bounds reject most samples before the more expensive polygon test.
            var bounds = new FloorRect[polygons.Length];
            for (int i = 0; i < polygons.Length; i++)
            {
                bounds[i] = new FloorRect((1f/0f),(-1f/0f),(1f/0f),(-1f/0f));
                foreach (Vector2 p in polygons[i])
                { bounds[i].minX = Mathf.Min(bounds[i].minX,p.x); bounds[i].maxX = Mathf.Max(bounds[i].maxX,p.x); bounds[i].minZ = Mathf.Min(bounds[i].minZ,p.y); bounds[i].maxZ = Mathf.Max(bounds[i].maxZ,p.y); }
            }
            for (int n = 0; n < samples.Length; n++)
            {
                Vector2 p = samples[n]; bool actual = false;
                for (int i = 0; i < polygons.Length && !actual; i++) actual = bounds[i].Contains(p) && Rules.InPolygon(p, polygons[i]);
                int mask = targetMasks[n];
                if (actual) { actualArea++; if (mask != 0) intersection++; }
                if (mask != 0 && (mask & (mask - 1)) == 0)
                    for (int i = 0; i < Shapes.Length; i++) if (mask == (1 << i)) { exclusive[i]++; if (actual) covered[i]++; break; }
            }
            float least = 1;
            for (int i = 0; i < Shapes.Length; i++) if (exclusive[i] > 0) least = Mathf.Min(least, covered[i] / (float)exclusive[i]);
            return new ShadowMatch { coverage = intersection / (float)targetArea, precision = actualArea == 0 ? 0 : intersection / (float)actualArea,
                iou = intersection / (float)Mathf.Max(1, targetArea + actualArea - intersection), leastPart = least };
        }
        public static bool ValidPlacement(ShadowPiece candidate, ShadowPiece[] pieces, Player player, int skip)
        {
            if (candidate.scale < MinScale || candidate.scale > MaxScale || float.IsNaN(candidate.position.x) || float.IsNaN(candidate.position.y)) return false;
            Bounds b = candidate.Bounds;
            if (b.min.x < -6.8f || b.max.x > 6.8f || b.min.z < -7.4f || b.max.z > 7.9f || Rules.Touches(player.Point, b, .32f)) return false;
            foreach (Vector2 p in Shadow(candidate)) if (!Room.Contains(p)) return false;
            for (int i = 0; i < pieces.Length; i++) if (i != skip && Rules.Overlap(b, pieces[i].Bounds, .012f)) return false;
            return true;
        }
        public static Grab Capture(ShadowPuzzleState state, int index)
        {
            var p = state.pieces[index]; Vector2 delta = p.position - state.player.Point;
            return new Grab { index = index, kind = p.shape, scale = p.scale, distance = Mathf.Max(.1f, delta.magnitude), eyeHeight = state.player.Eye.y,
                yawOffset = Mathf.Atan2(-delta.x,-delta.y) - state.yaw,
                pitchOffset = Mathf.Atan2(Shapes[p.shape].bounds.center.y * p.scale - state.player.Eye.y,delta.magnitude) - state.pitch };
        }
        public static bool Place(ShadowPuzzleState state, out ShadowPiece candidate)
        {
            Grab g = state.held; float ratio = g.scale / g.distance;
            float tangent = Mathf.Tan(Mathf.Clamp(state.pitch + g.pitchOffset,-1.45f,1.45f));
            float height = Shapes[g.kind].bounds.center.y;
            float denominator = state.fixedSize ? -tangent : height * ratio - tangent;
            float distance = denominator > .001f ? (state.player.Eye.y - (state.fixedSize ? height * g.scale : 0)) / denominator : 100;
            distance = state.fixedSize ? Mathf.Clamp(distance,.75f,18) : Mathf.Clamp(distance,MinScale / ratio,MaxScale / ratio);
            float scale = state.fixedSize ? g.scale : Mathf.Clamp(ratio * distance, MinScale, MaxScale);
            Vector2 direction = new Vector2(-Mathf.Sin(state.yaw + g.yawOffset),-Mathf.Cos(state.yaw + g.yawOffset));
            candidate = new ShadowPiece(g.kind, state.player.Point + direction * distance, scale);
            return ValidPlacement(candidate,state.pieces,state.player,g.index);
        }
        public static void Move(ShadowPuzzleState state, Vector2 velocity, float dt)
        {
            int steps = Mathf.Max(1,Mathf.CeilToInt(dt * 120)); Vector2 delta = velocity * (dt / steps);
            for (int step = 0; step < steps; step++)
            {
                Vector2 p = state.player.Point;
                if (CanStand(p + new Vector2(delta.x,0),state.pieces)) p.x += delta.x;
                if (CanStand(p + new Vector2(0,delta.y),state.pieces)) p.y += delta.y;
                state.player.feet = new Vector3(p.x,0,p.y);
            }
        }
        public static bool CanStand(Vector2 p, ShadowPiece[] pieces)
        {
            if (p.x < -6.7f || p.x > 6.7f || p.y < -8.0f || p.y > 8.2f) return false;
            foreach (var piece in pieces) if (Rules.Touches(p,piece.Bounds)) return false;
            return true;
        }
        static bool RayTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0; Vector3 e = b - a, f = c - a, p = Vector3.Cross(d,f); float det = Vector3.Dot(e,p);
            if (Mathf.Abs(det) < .000001f) return false;
            Vector3 s = o - a; float u = Vector3.Dot(s,p) / det; if (u < 0 || u > 1) return false;
            Vector3 q = Vector3.Cross(s,e); float v = Vector3.Dot(d,q) / det; if (v < 0 || u + v > 1) return false;
            t = Vector3.Dot(f,q) / det; return t > .001f;
        }
        public static bool RayPiece(Vector3 origin, Vector3 direction, ShadowPiece piece, out float nearest)
        {
            nearest=(1f/0f);
            if(!Rules.RayBox(origin,direction,piece.Bounds,out _)) return false;
            var shape=Shapes[piece.shape]; Vector3 offset=new Vector3(piece.position.x,0,piece.position.y);
            for(int j=0;j<shape.triangles.Length;j+=3)
                if(RayTriangle(origin,direction,shape.vertices[shape.triangles[j]]*piece.scale+offset,
                    shape.vertices[shape.triangles[j+1]]*piece.scale+offset,shape.vertices[shape.triangles[j+2]]*piece.scale+offset,out float hit)) nearest=Mathf.Min(nearest,hit);
            return !float.IsPositiveInfinity(nearest);
        }
        public static int Aim(ShadowPuzzleState state, out float nearest)
        {
            int aimed = -1; nearest = Reach; Vector3 origin = state.player.Eye, direction = Rules.Look(state.yaw,state.pitch);
            for (int i = 0; i < state.pieces.Length; i++)
            {
                ShadowPiece piece = state.pieces[i]; if (!Rules.RayBox(origin,direction,piece.Bounds,out _)) continue;
                var shape = Shapes[piece.shape]; Vector3 offset = new Vector3(piece.position.x,0,piece.position.y);
                for (int j = 0; j < shape.triangles.Length; j += 3)
                    if (RayTriangle(origin,direction,shape.vertices[shape.triangles[j]] * piece.scale + offset,
                        shape.vertices[shape.triangles[j+1]] * piece.scale + offset,shape.vertices[shape.triangles[j+2]] * piece.scale + offset,out float hit) && hit < nearest)
                    { nearest = hit; aimed = i; }
            }
            return aimed;
        }
        public static bool AimFloor(ShadowPuzzleState state, out Vector2 point, out float distance)
        {
            Vector3 look = Rules.Look(state.yaw,state.pitch); point = default; distance = (1f/0f);
            if (look.y >= -.015f) return false;
            distance = (.028f - state.player.Eye.y) / look.y;
            Vector3 at = state.player.Eye + look * distance; point = new Vector2(at.x,at.z);
            return distance > 0 && distance < Reach && Room.Contains(point);
        }
        public static bool AimFrame(ShadowPuzzleState state)
        {
            Vector3 look = Rules.Look(state.yaw,state.pitch);
            if (!Rules.RayBox(state.player.Eye,look,Frame,out float distance) || distance > 4.5f) return false;
            Aim(state,out float obstacle); return distance < obstacle;
        }
        public static bool AimToken(ShadowPuzzleState state)
        {
            if (state.stage != ShadowStage.Ready || state.frozen == null || !AimFloor(state,out Vector2 p,out float distance)) return false;
            Aim(state,out float obstacle);
            return distance < obstacle && Contains((p - state.tokenOffset) / state.tokenScale,state.frozen);
        }
    }
}
