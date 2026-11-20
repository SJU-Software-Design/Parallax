using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    // Cosmetic, manually advanced world-space drops. No colliders or shadow
    // casting are added: wet cloth never changes the actual safe-shade query.
    public sealed class BeachCanopyDrips : MonoBehaviour
    {
        public const int Capacity = 192;
        public const float DrySeconds = 8f;
        const float DropsPerSecond = 52f;

        struct Edge
        {
            public Transform roof;
            public Renderer renderer;
            public Vector3 from, to;
        }
        struct Drop
        {
            public Vector3 position, velocity;
            public float age, lifetime, radius, length;
            public bool alive;
        }

        BeachCounterweightFinale finale;
        readonly List<Edge> edges = new List<Edge>();
        readonly Drop[] drops = new Drop[Capacity];
        readonly Vector3[] vertices = new Vector3[Capacity * 6];
        readonly Color32[] colors = new Color32[Capacity * 6];
        readonly int[] triangles = new int[Capacity * 24];
        Mesh mesh;
        MeshRenderer dropRenderer;
        Material material;
        float dryTime, budget, previousProgress;
        uint random = 0x8465ab31;
        bool configured;

        public bool Emitting { get; private set; }
        public bool HasEmerged { get; private set; }
        public float Wetness { get; private set; }
        public int VisibleDropCount { get; private set; }
        public int TotalEmitted { get; private set; }
        public int RoofEdgeCount => edges.Count;
        public Vector3 LastSpawnPosition { get; private set; }
        public bool HasVisibleGeometry => dropRenderer != null && dropRenderer.enabled && VisibleDropCount > 0;
        public Renderer DropRenderer => dropRenderer;

        // Configure once at runtime, then Tick immediately after finale.Tick.
        // This component intentionally has no automatic Update or physics.
        public void Configure(BeachCounterweightFinale value)
        {
            finale = value;
            edges.Clear();
            if (finale != null)
                foreach (var renderer in finale.BridgeCanopyRenderers)
                {
                    if (renderer == null) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    CaptureEaves(renderer, filter.sharedMesh.vertices);
                }
            EnsureGeometry();
            configured = true;
            ResetState();
        }

        void CaptureEaves(Renderer renderer, Vector3[] points)
        {
            if (points == null || points.Length < 3) return;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            foreach (var p in points) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); }
            float tolerance = Mathf.Max(.002f, (maxX - minX) * .002f);
            // The existing authored canopy has two X eaves and a higher ridge.
            // Extract endpoints from real mesh vertices, not a guessed height.
            foreach (float x in new[] { minX, maxX })
            {
                Vector3 from = Vector3.zero, to = Vector3.zero;
                float near = float.PositiveInfinity, far = float.NegativeInfinity;
                foreach (var p in points)
                    if (Mathf.Abs(p.x - x) <= tolerance)
                    {
                        if (p.z < near) { near = p.z; from = p; }
                        if (p.z > far) { far = p.z; to = p; }
                    }
                if (far - near > .01f)
                    edges.Add(new Edge { roof = renderer.transform, renderer = renderer, from = from, to = to });
            }
        }

        void EnsureGeometry()
        {
            if (mesh != null) return;
            var visual = new GameObject("Wet canopy · falling water drops");
            visual.transform.SetParent(transform, false);
            visual.layer = 2; // Ignore Raycast, in addition to having no collider.
            var filter = visual.AddComponent<MeshFilter>();
            dropRenderer = visual.AddComponent<MeshRenderer>();
            dropRenderer.shadowCastingMode = ShadowCastingMode.Off;
            dropRenderer.receiveShadows = false;
            dropRenderer.lightProbeUsage = LightProbeUsage.Off;
            dropRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            dropRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            Shader shader = Resources.Load<Shader>("BeachCanopyDrips");
            if (shader == null) shader = Shader.Find("Parallax/BeachCanopyDrips");
            if (shader == null) throw new InvalidOperationException("Canopy drip shader was not included.");
            material = new Material(shader) { name = "Wet canopy · clear water", hideFlags = HideFlags.DontSave };
            dropRenderer.sharedMaterial = material;
            mesh = new Mesh { name = "Wet canopy · pooled drop mesh", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            // Six vertices form a small elongated octahedron per water drop.
            int[] faces = { 0,2,4, 0,4,3, 0,3,5, 0,5,2, 1,4,2, 1,3,4, 1,5,3, 1,2,5 };
            for (int i = 0; i < Capacity; i++)
                for (int n = 0; n < faces.Length; n++) triangles[i * 24 + n] = i * 6 + faces[n];
            mesh.vertices = vertices; mesh.colors32 = colors; mesh.triangles = triangles;
            filter.sharedMesh = mesh;
            dropRenderer.enabled = false;
        }

        public void ResetState()
        {
            Array.Clear(drops, 0, drops.Length);
            dryTime = budget = previousProgress = 0;
            random = 0x8465ab31;
            Emitting = HasEmerged = false;
            Wetness = 0; VisibleDropCount = TotalEmitted = 0;
            LastSpawnPosition = Vector3.zero;
            if (dropRenderer != null) dropRenderer.enabled = false;
        }

        public void RestoreDryForCheckpoint()
        {
            ResetState(); HasEmerged = true; dryTime = DrySeconds; previousProgress = 1;
        }

        public void Tick(float dt)
        {
            if (!configured || finale == null || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            float progress = finale.LiftProgress;
            if (progress + .0001f < previousProgress || (!finale.LoadLatched && (HasEmerged || TotalEmitted > 0)))
                ResetState();
            previousProgress = progress;

            bool exposed = false;
            if (finale.LoadLatched)
                foreach (var edge in edges)
                    if (CanEmit(edge)) { exposed = true; break; }
            if (exposed) HasEmerged = true;
            if (finale.Ready && HasEmerged) dryTime += dt;
            Wetness = HasEmerged ? 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(dryTime / DrySeconds)) : 0;
            Emitting = exposed && Wetness > .02f;

            for (int i = 0; i < drops.Length; i++)
            {
                if (!drops[i].alive) continue;
                var d = drops[i]; d.age += dt;
                d.position += d.velocity * dt + Vector3.down * (4.6f * dt * dt);
                d.velocity += Vector3.down * (9.2f * dt);
                if (d.age >= d.lifetime || DropReachedSurface(d.position)) d.alive = false;
                drops[i] = d;
            }
            if (Emitting)
            {
                budget += dt * DropsPerSecond * Wetness;
                int count = Mathf.Min(Capacity, Mathf.FloorToInt(budget)); budget -= count;
                for (int i = 0; i < count; i++) EmitDrop();
            }
            else budget = 0;
            UploadGeometry();
        }

        bool DropReachedSurface(Vector3 position)
        {
            if (position.y <= finale.WaterSurface + .015f) return true;
            var floor = finale.BridgeFloor;
            if (floor == null || !floor.enabled) return false;
            Vector3 local = floor.transform.InverseTransformPoint(position) - floor.center;
            Vector3 half = floor.size * .5f;
            // Cosmetic contact with the actual moving deck. This avoids drips
            // passing through timber without creating physics bodies/queries.
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z && local.y <= half.y + .015f;
        }

        bool CanEmit(Edge edge)
        {
            return edge.roof != null && edge.renderer != null && edge.renderer.enabled &&
                edge.renderer.gameObject.activeInHierarchy &&
                Mathf.Min(edge.roof.TransformPoint(edge.from).y, edge.roof.TransformPoint(edge.to).y) >
                finale.WaterSurface + .10f;
        }

        float Random01()
        {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            return (random & 0xffffff) / 16777216f;
        }

        void EmitDrop()
        {
            if (edges.Count == 0) return;
            int index = -1;
            for (int i = 0; i < drops.Length; i++) if (!drops[i].alive) { index = i; break; }
            if (index < 0) return;
            int offset = Mathf.Min(edges.Count - 1, (int)(Random01() * edges.Count));
            for (int n = 0; n < edges.Count; n++)
            {
                var edge = edges[(offset + n) % edges.Count];
                if (!CanEmit(edge)) continue;
                Vector3 spawn = edge.roof.TransformPoint(Vector3.Lerp(edge.from, edge.to, Random01())) - Vector3.up * .035f;
                drops[index] = new Drop {
                    alive = true, position = spawn,
                    velocity = new Vector3((Random01() - .5f) * .12f, -.35f - Random01() * .7f, (Random01() - .5f) * .10f),
                    age = 0, lifetime = 2.5f,
                    radius = .022f + Random01() * .012f, length = .10f + Random01() * .075f
                };
                LastSpawnPosition = spawn; TotalEmitted++;
                return;
            }
        }

        void UploadGeometry()
        {
            VisibleDropCount = 0;
            for (int i = 0; i < drops.Length; i++)
            {
                int at = i * 6; var d = drops[i];
                if (!d.alive)
                {
                    for (int j = 0; j < 6; j++) { vertices[at + j] = Vector3.zero; colors[at + j] = new Color32(0,0,0,0); }
                    continue;
                }
                VisibleDropCount++;
                float length = d.length * (1 + Mathf.Min(8, Mathf.Abs(d.velocity.y)) * .05f);
                var worldToLocal = dropRenderer.transform.worldToLocalMatrix;
                vertices[at] = worldToLocal.MultiplyPoint3x4(d.position + Vector3.up * length * .55f);
                vertices[at + 1] = worldToLocal.MultiplyPoint3x4(d.position - Vector3.up * length * .55f);
                vertices[at + 2] = worldToLocal.MultiplyPoint3x4(d.position + Vector3.left * d.radius);
                vertices[at + 3] = worldToLocal.MultiplyPoint3x4(d.position + Vector3.right * d.radius);
                vertices[at + 4] = worldToLocal.MultiplyPoint3x4(d.position + Vector3.forward * d.radius);
                vertices[at + 5] = worldToLocal.MultiplyPoint3x4(d.position + Vector3.back * d.radius);
                float alpha = Mathf.Clamp01(d.age / .055f) * Mathf.Clamp01((d.lifetime - d.age) / .18f);
                for (int j = 0; j < 6; j++)
                    colors[at + j] = (Color32)new Color(j == 4 ? .91f : .55f, j == 4 ? .98f : .84f, 1, alpha * .72f);
            }
            dropRenderer.enabled = VisibleDropCount > 0;
            if (VisibleDropCount == 0) return;
            mesh.vertices = vertices; mesh.colors32 = colors; mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
