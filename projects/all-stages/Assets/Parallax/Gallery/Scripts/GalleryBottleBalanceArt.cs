using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Procedural, original exhibition hardware. The controller owns puzzle rules,
    // interaction and the contained object's physics; this class only owns art.
    public static class GalleryBottleBalanceArt
    {
        public sealed class View
        {
            public Transform root, bottleStation, scaleStation;
            public Transform bottlePivot, bottleVisual, contentAnchor, catchPad, beam, leftPan, rightPan, indicator, fixedSculpture;
            public Transform[] cradleArms;
            public GameObject bottlePickShape;
            public Renderer indicatorRenderer;
            public Material brass, darkBrass, stone, velvet, glass, indicatorMaterial;
            // Dike exhibit: these are populated only from a verified independent
            // imported sword; a beautiful statue alone must not unlock the exit.
            public Transform rewardHandAnchor, rewardCatchPad, swordSocketAnchor, verifiedRewardSword;
            public Renderer[] statueRenderers = Array.Empty<Renderer>();
            public bool SwordReleaseVerified;
            public string SwordReleaseAvailabilityReason = "정의의 여신상과 검의 독립 구조를 확인 중입니다.";
            public GalleryDikeExhibit dikeExhibit;
            internal readonly List<Transform> armLower = new List<Transform>();
            internal readonly List<Transform> armUpper = new List<Transform>();
            internal readonly List<Transform> armSleeve = new List<Transform>();
            internal readonly List<Transform> armCollars = new List<Transform>();
            internal readonly List<Transform> bearings = new List<Transform>();
            internal readonly List<Transform> axles = new List<Transform>();
            internal readonly List<Transform> feet = new List<Transform>();
            internal Transform bridge, balanceNeedle;
            public float BottleSize { get; private set; } = 1;
            public float BottlePitch { get; private set; }
            public float panDrop = 1.35f;
            public float panHalfSpan = 1.65f;
            public Vector3 MouthWorld => bottleVisual.TransformPoint(new Vector3(1.05f, 0, 0));
            public Vector3 MouthDirection => bottleVisual.right;
            public float MouthRadius => .19f * BottleSize;
            public Vector3 CatchSurfaceWorld => catchPad.TransformPoint(Vector3.up * .10f);

            // Positive pitch lowers the mouth (local +X); the pivot axis is local Z.
            public void SetBottlePose(float size, float pitchDegrees)
            {
                BottleSize = Mathf.Clamp(size, .65f, 2.8f);
                BottlePitch = Mathf.Clamp(pitchDegrees, -55, 55);
                float height = 1.45f + .64f * (BottleSize - 1f);
                float spread = .62f * BottleSize + .13f;
                bottlePivot.localPosition = new Vector3(0, height, 0);
                bottlePivot.localRotation = Quaternion.Euler(0, 0, -BottlePitch);
                bottleVisual.localScale = Vector3.one * BottleSize;
                contentAnchor.localPosition = new Vector3(-.10f * BottleSize, -.32f * BottleSize, 0);
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1 : 1;
                    float z = spread * side;
                    float low = .34f, split = .98f;
                    Segment(armLower[i], new Vector3(0, low, z), new Vector3(0, split, z), .15f);
                    Segment(armUpper[i], new Vector3(0, split - .12f, z), new Vector3(0, height, z), .105f);
                    armSleeve[i].localPosition = new Vector3(0, split - .04f, z);
                    armCollars[i].localPosition = new Vector3(0, height - .17f, z);
                    bearings[i].localPosition = new Vector3(0, height, z);
                    feet[i].localPosition = new Vector3(0, .39f, z);
                    axles[i].localPosition = new Vector3(0, 0, (.62f * BottleSize + .07f) * side);
                    axles[i].localScale = new Vector3(.12f, .12f, .16f);
                }
                bridge.localScale = new Vector3(.19f, .16f, spread * 2 + .14f);
            }

            // Pans remain horizontal even while the beam rotates.
            public void SetBalance(float degrees, float progress, bool solved)
            {
                beam.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(degrees, -18, 18));
                Vector3 a = scaleStation.InverseTransformPoint(beam.TransformPoint(new Vector3(-panHalfSpan, 0, 0)));
                Vector3 b = scaleStation.InverseTransformPoint(beam.TransformPoint(new Vector3(panHalfSpan, 0, 0)));
                leftPan.localPosition = a - Vector3.up * panDrop;
                rightPan.localPosition = b - Vector3.up * panDrop;
                leftPan.localRotation = rightPan.localRotation = Quaternion.identity;
                if (balanceNeedle != null) balanceNeedle.localRotation = Quaternion.Euler(0, 0, -degrees);
                Color c = solved ? new Color(.26f, 1f, .32f) :
                    Color.Lerp(new Color(.95f, .34f, .055f), new Color(.56f, .92f, .27f), Mathf.Clamp01(progress));
                indicatorMaterial.color = c;
                if (indicatorMaterial.HasProperty("_EmissionColor")) indicatorMaterial.SetColor("_EmissionColor", c * .75f);
                dikeExhibit?.UpdateBalance(degrees, progress, solved);
            }
        }

        static Transform Group(string name, Transform parent, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false); t.localPosition = at; return t;
        }

        static Material Surface(string name, Color color, string preset, float roughness)
        {
            var shader = Shader.Find("Parallax/MuseumSurface");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name, color = color };
            if (shader.name == "Parallax/MuseumSurface") GalleryMuseumMaterials.Apply(m, preset);
            if (m.HasProperty("_Roughness")) m.SetFloat("_Roughness", roughness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 1 - roughness);
            if (preset == "bronze" || preset == "brass")
            {
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", .64f);
                if (m.HasProperty("_ReflectionStrength")) m.SetFloat("_ReflectionStrength", .25f);
                if (m.HasProperty("_ExhibitLight")) m.SetFloat("_ExhibitLight", .22f);
            }
            GalleryWorld.Materials.Add(m); return m;
        }

        static Renderer Shape(string name, Transform parent, Mesh mesh, Material material, Vector3 at)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }

        static Transform Cube(string name, Transform parent, Vector3 at, Vector3 size, Material material, bool solid = false)
        {
            var r = GalleryConceptArchitecture.Block(parent, name, at, size, material, Mathf.Min(.035f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .18f));
            if (solid) r.gameObject.AddComponent<BoxCollider>().size = size;
            return r.transform;
        }

        static Transform Primitive(string name, Transform parent, PrimitiveType type, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            var collider = go.GetComponent<Collider>();
            if (Application.isPlaying) UnityEngine.Object.Destroy(collider); else UnityEngine.Object.DestroyImmediate(collider);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go.transform;
        }

        static void Segment(Transform t, Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a; t.localPosition = (a + b) * .5f;
            t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            t.localScale = new Vector3(radius * 2, d.magnitude * .5f, radius * 2);
        }

        static Transform Rod(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var t = Primitive(name, parent, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, mat);
            Segment(t, a, b, radius); return t;
        }

        static Mesh Finish(string name, List<Vector3> vertices, List<int> triangles, List<Vector2> uv = null, List<Vector3> normals = null)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            if (uv != null) mesh.SetUVs(0, uv);
            if (normals != null) mesh.SetNormals(normals); else mesh.RecalculateNormals();
            mesh.RecalculateBounds(); return mesh;
        }

        static Mesh LatheX(string name, IList<Vector2> profile, int sides = 64)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int j = 0; j < profile.Count; j++)
            {
                Vector2 tangent = profile[Mathf.Min(j + 1, profile.Count - 1)] - profile[Mathf.Max(0, j - 1)];
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides, c = Mathf.Cos(a), s = Mathf.Sin(a);
                    v.Add(new Vector3(profile[j].x, profile[j].y * c, profile[j].y * s));
                    n.Add(new Vector3(-tangent.y, tangent.x * c, tangent.x * s).normalized);
                    uv.Add(new Vector2((float)i / sides, (float)j / (profile.Count - 1)));
                }
            }
            for (int j = 0; j < profile.Count - 1; j++) for (int i = 0; i < sides; i++)
            {
                int a = j * (sides + 1) + i, b = a + sides + 1;
                t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
            return Finish(name, v, t, uv, n);
        }

        public static Mesh CreateBottleMesh()
        {
            var outer = new List<Vector2>();
            // Cross-section: the body is round, while the neck has an open .19m inner radius.
            for (int i = 0; i <= 34; i++)
            {
                float a = Mathf.Lerp(Mathf.PI, .58f, i / 34f);
                outer.Add(new Vector2(.62f * Mathf.Cos(a), .62f * Mathf.Sin(a)));
            }
            outer.AddRange(new[] { new Vector2(.60f, .285f), new Vector2(.69f, .225f), new Vector2(.80f, .215f),
                new Vector2(.98f, .215f), new Vector2(1.005f, .236f), new Vector2(1.05f, .236f) });
            var p = new List<Vector2>(outer);
            p.Add(new Vector2(1.05f, .19f));
            p.Add(new Vector2(1.005f, .19f));
            for (int i = outer.Count - 5; i >= 0; i--)
                p.Add(new Vector2(outer[i].x + .027f, Mathf.Max(0, outer[i].y - .035f)));
            return LatheX("Museum hand-blown glass vessel · outer wall, inner wall and open lip", p, 80);
        }

        static Mesh Torus(string name, float radius, float tube, int around = 72, int radial = 10)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float a = i * Mathf.PI * 2 / around;
                Vector3 c = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
                for (int j = 0; j <= radial; j++)
                {
                    float b = j * Mathf.PI * 2 / radial;
                    Vector3 normal = Vector3.right * Mathf.Sin(b) + c * Mathf.Cos(b);
                    v.Add(c * radius + normal * tube); n.Add(normal);
                    uv.Add(new Vector2((float)i / around, (float)j / radial));
                }
            }
            for (int i = 0; i < around; i++) for (int j = 0; j < radial; j++)
            {
                int a = i * (radial + 1) + j, b = a + radial + 1;
                t.AddRange(new[] { a, b + 1, a + 1, a, b, b + 1 });
            }
            return Finish(name, v, t, uv, n);
        }

        public static Mesh CreateKnotMesh(float diameter = .56f)
        {
            const int sections = 180, sides = 14;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Func<float, Vector3> curve = a => new Vector3((2 + Mathf.Cos(3 * a)) * Mathf.Cos(2 * a),
                (2 + Mathf.Cos(3 * a)) * Mathf.Sin(2 * a), Mathf.Sin(3 * a)) * .18f;
            for (int i = 0; i <= sections; i++)
            {
                float a = i * Mathf.PI * 2 / sections; Vector3 c = curve(a);
                Vector3 tangent = (curve(a + .003f) - curve(a - .003f)).normalized;
                Vector3 normal = Vector3.Cross(tangent, Vector3.forward).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2 / sides; Vector3 r = normal * Mathf.Cos(b) + binormal * Mathf.Sin(b);
                    v.Add(c + r * .105f); n.Add(r); uv.Add(new Vector2((float)i / sections, (float)j / sides));
                }
            }
            for (int i = 0; i < sections; i++) for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                t.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }
            Bounds bounds = new Bounds(v[0], Vector3.zero); foreach (var p in v) bounds.Encapsulate(p);
            float scale = diameter / Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            for (int i = 0; i < v.Count; i++) v[i] = (v[i] - bounds.center) * scale;
            return Finish("Original smooth trefoil bronze study", v, t, uv, n);
        }

        static Mesh FlameMesh()
        {
            const int steps = 90, sides = 12;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps, a = u * Mathf.PI * 2;
                Vector3 c = new Vector3(.22f * Mathf.Sin(a), .46f - .46f * Mathf.Cos(a), .11f * Mathf.Sin(a * 2));
                Vector3 tangent = new Vector3(.22f * Mathf.Cos(a), .46f * Mathf.Sin(a), .22f * Mathf.Cos(a * 2)).normalized;
                Vector3 normal = Vector3.Cross(tangent, Vector3.forward).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2 / sides;
                    v.Add(c + normal * Mathf.Cos(b) * (.10f + .035f * Mathf.Sin(a)) + binormal * Mathf.Sin(b) * .07f);
                }
            }
            for (int i = 0; i < steps; i++) for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                t.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }
            return Finish("Twisting bronze flame · fixed reference weight", v, t);
        }

        static void Label(Transform parent, string words, Vector3 at, float size, Color color)
        {
            var t = Group("Exhibition plaque · " + words.Replace('\n', ' '), parent, at);
            var label = t.gameObject.AddComponent<GalleryLabel>(); label.caption = words; label.size = size; label.color = color;
        }

        static void Pan(Transform pan, Material brass, Material dark)
        {
            // Mesh is a shallow polished dish; collider top is y=0 for placement.
            var profile = new[] { new Vector2(0, 0), new Vector2(.012f, .12f), new Vector2(.025f, .36f),
                new Vector2(.065f, .58f), new Vector2(.10f, .69f), new Vector2(.12f, .70f),
                new Vector2(.13f, .68f), new Vector2(.095f, .61f), new Vector2(.045f, .35f), new Vector2(.025f, 0) };
            var bowl = Shape("Hammered brass weighing dish", pan, LatheX("Turned shallow balance pan", profile), brass, new Vector3(0, -.13f, 0));
            bowl.transform.localRotation = Quaternion.Euler(0, 0, 90);
            var solid = pan.gameObject.AddComponent<BoxCollider>(); solid.center = new Vector3(0, -.05f, 0); solid.size = new Vector3(1.18f, .10f, 1.05f);
            // Three independent gold suspension chains, with small visible chain eyes.
            foreach (float angle in new[] { 30f, 150f, 270f })
            {
                float rad = angle * Mathf.Deg2Rad;
                Vector3 rim = new Vector3(Mathf.Cos(rad) * .59f, .015f, Mathf.Sin(rad) * .59f);
                Vector3 apex = Vector3.up * 1.35f;
                Rod("Fine brass suspension", pan, rim, apex, .012f, brass);
                for (int k = 1; k < 8; k++)
                {
                    Vector3 at = Vector3.Lerp(rim, apex, k / 8f);
                    var eye = Shape("Suspension chain link", pan, Torus("Small brass chain eye", .024f, .006f, 16, 6), brass, at).transform;
                    eye.localRotation = Quaternion.Euler(90, k % 2 * 90, 0);
                }
            }
            Primitive("Balance pan top eye", pan, PrimitiveType.Sphere, Vector3.up * 1.35f, Vector3.one * .10f, dark);
        }

        static bool CollectedSculpture(Transform parent, string resource, Material material, float maximumDimension)
        {
            var source = Resources.Load<GameObject>(resource);
            if (source == null) return false;
            var instance = UnityEngine.Object.Instantiate(source, parent, false);
            instance.name = "Collected CC0 bronze sculpture · " + source.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            bool found = false; Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = instance.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                }
            }
            if (!found)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(instance); else UnityEngine.Object.DestroyImmediate(instance);
                return false;
            }
            float factor = maximumDimension / Mathf.Max(.0001f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            instance.transform.localScale = Vector3.one * factor;
            instance.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * factor;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                renderer.sharedMaterials = mats; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            return true;
        }

        public static View Create(Transform parent, Vector3 bottleAt, Vector3 scaleAt)
        {
            var view = new View();
            view.root = Group("Added exhibition · The weight in the bottle", parent, Vector3.zero);
            view.brass = Surface("BottleBalance · warm polished brass", new Color(.76f, .52f, .22f), "brass", .38f);
            view.darkBrass = Surface("BottleBalance · aged bronze recess", new Color(.32f, .20f, .08f), "bronze", .48f);
            view.stone = Surface("BottleBalance · honed cream travertine", new Color(.85f, .79f, .66f), "stone", .48f);
            view.velvet = Surface("BottleBalance · burgundy velvet", new Color(.28f, .022f, .028f), "paper", .96f);
            view.indicatorMaterial = Surface("BottleBalance · balance indicator", new Color(1, .4f, .04f), "paper", .25f);
            var glassShader = Shader.Find("Parallax/BottleBalanceGlass");
            if (glassShader == null) glassShader = Shader.Find("Parallax/GalleryGlass");
            view.glass = new Material(glassShader) { name = "BottleBalance · clear thick museum glass", color = new Color(.94f, .90f, .82f, .015f) };
            if (view.glass.HasProperty("_RimStrength")) view.glass.SetFloat("_RimStrength", .18f);
            GalleryWorld.Materials.Add(view.glass);

            view.bottleStation = Group("Bottle · fixed exhibition pedestal", view.root, bottleAt);
            Cube("Beveled limestone foot", view.bottleStation, new Vector3(0, .16f, 0), new Vector3(3.8f, .32f, 3.8f), view.stone, true);
            Cube("Dark bronze reveal below plinth", view.bottleStation, new Vector3(0, .025f, 0), new Vector3(3.65f, .05f, 3.65f), view.darkBrass);
            Cube("Thin brass edging", view.bottleStation, new Vector3(0, .325f, 0), new Vector3(3.73f, .025f, 3.73f), view.brass);
            view.bridge = Cube("U support · expanding crosspiece", view.bottleStation, new Vector3(0, .39f, 0), new Vector3(.19f, .16f, 1.5f), view.brass);
            view.bottlePivot = Group("Bottle · transverse body pivot", view.bottleStation, Vector3.up * 1.45f);
            view.bottleVisual = Group("Bottle · scalable glass and mounting band", view.bottlePivot, Vector3.zero);
            Shape("Open hand-blown vessel", view.bottleVisual, CreateBottleMesh(), view.glass, Vector3.zero);
            Shape("Thin pivot mounting band", view.bottleVisual, Torus("Belly mounting band", .627f, .016f), view.brass, Vector3.zero);
            view.contentAnchor = Group("Bottle · fixed-size contained artwork anchor", view.bottlePivot, new Vector3(-.10f, -.32f, 0));

            var pick = Group("Bottle · special interaction pick volume", view.bottleVisual, Vector3.zero);
            view.bottlePickShape = pick.gameObject;
            pick.gameObject.AddComponent<SphereCollider>().radius = .64f;
            var neck = pick.gameObject.AddComponent<BoxCollider>(); neck.center = new Vector3(.78f, 0, 0); neck.size = new Vector3(.58f, .48f, .48f);

            for (int side = -1; side <= 1; side += 2)
            {
                var arm = Group(side < 0 ? "Near telescopic brass support" : "Far telescopic brass support", view.bottleStation, Vector3.zero);
                view.armLower.Add(Primitive("Broad brass lower sleeve", arm, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, view.brass));
                view.armUpper.Add(Primitive("Extending inner brass pillar", arm, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, view.brass));
                view.armSleeve.Add(Primitive("Dark telescoping joint", arm, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.32f, .055f, .32f), view.darkBrass));
                view.armCollars.Add(Primitive("Upper bearing collar", arm, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.23f, .04f, .23f), view.brass));
                view.feet.Add(Cube("Support foot", arm, Vector3.zero, new Vector3(.42f, .12f, .42f), view.brass));
                var bearing = Primitive("Round transverse pivot bearing", arm, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.37f, .075f, .37f), view.darkBrass);
                bearing.localRotation = Quaternion.Euler(90, 0, 0); view.bearings.Add(bearing);
                var face = Primitive("Polished bearing face", bearing, PrimitiveType.Cylinder, new Vector3(0, side * .95f, 0), new Vector3(.76f, .08f, .76f), view.brass);
                var pin = Primitive("Body-mounted short pivot stub", view.bottlePivot, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, view.brass);
                pin.localRotation = Quaternion.Euler(90, 0, 0); view.axles.Add(pin);
            }
            view.cradleArms = new[] { view.armLower[0].parent, view.armLower[1].parent };
            view.catchPad = Group("Artwork · independent landing cushion", view.bottleStation, new Vector3(3.25f, .62f, 0));
            Cube("Artwork landing pad stone", view.catchPad, new Vector3(0, -.08f, 0), new Vector3(.82f, .26f, 1.04f), view.stone, true);
            Cube("Soft burgundy landing cushion", view.catchPad, new Vector3(0, .05f, 0), new Vector3(.76f, .10f, .96f), view.velvet, true);
            Cube("Bottle exhibition title plaque", view.bottleStation, new Vector3(0, .205f, -1.913f), new Vector3(1.72f, .20f, .025f), view.darkBrass);
            Label(view.bottleStation, "병 속의 법전", new Vector3(0, .205f, -1.933f), .12f, new Color(.96f, .83f, .51f));

            view.scaleStation = Group("Balance · brass exhibition scale", view.root, scaleAt);
            Cube("Balance travertine plinth", view.scaleStation, new Vector3(0, .12f, 0), new Vector3(4.6f, .24f, 1.80f), view.stone, true);
            Cube("Balance dark bronze base reveal", view.scaleStation, new Vector3(0, .265f, 0), new Vector3(4.45f, .05f, 1.68f), view.darkBrass);
            var columnProfile = new[] { new Vector2(0, 0), new Vector2(.02f, .52f), new Vector2(.12f, .53f),
                new Vector2(.18f, .43f), new Vector2(.32f, .36f), new Vector2(.50f, .24f), new Vector2(.82f, .14f),
                new Vector2(1.55f, .11f), new Vector2(2.04f, .14f), new Vector2(2.23f, .22f), new Vector2(2.35f, .21f),
                new Vector2(2.44f, .11f), new Vector2(2.55f, .13f), new Vector2(2.66f, .05f), new Vector2(2.74f, 0) };
            var col = Shape("Turned antique balance column", view.scaleStation, LatheX("Classical tapered brass column", columnProfile), view.brass, new Vector3(0, .29f, 0)).transform;
            col.localRotation = Quaternion.Euler(0, 0, 90);
            col.localScale = new Vector3((2.35f-.29f)/2.74f,1,1);
            view.beam = Group("Balance · rotating horizontal beam", view.scaleStation, Vector3.up * 2.35f);
            Cube("Forged brass balance beam", view.beam, Vector3.zero, new Vector3(4.10f, .11f, .15f), view.brass);
            Cube("Dark engraved beam inset", view.beam, new Vector3(0, 0, -.083f), new Vector3(3.67f, .035f, .014f), view.darkBrass);
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive("Beam end finial", view.beam, PrimitiveType.Sphere, new Vector3(side * 2.08f, 0, 0), Vector3.one * .20f, view.brass);
                Primitive("Suspension pivot eye", view.beam, PrimitiveType.Sphere, new Vector3(side * 1.65f, 0, 0), Vector3.one * .16f, view.darkBrass);
            }
            var dial = Primitive("Balance central round dial", view.scaleStation, PrimitiveType.Cylinder, new Vector3(0, 2.35f, -.13f), new Vector3(.64f, .065f, .64f), view.brass);
            dial.localRotation = Quaternion.Euler(90, 0, 0);
            view.indicator = Cube("Balance status light", view.scaleStation, new Vector3(0, 2.35f, -.215f), new Vector3(.46f, .13f, .045f), view.indicatorMaterial);
            view.indicatorRenderer = view.indicator.GetComponent<Renderer>();
            view.balanceNeedle = Cube("Balance dial needle", view.scaleStation, new Vector3(0, 2.35f, -.25f), new Vector3(.014f, .19f, .018f), view.darkBrass);
            view.leftPan = Group("Balance · left fixed weight pan", view.scaleStation, new Vector3(-1.65f, 1f, 0));
            view.rightPan = Group("Balance · right artwork pan", view.scaleStation, new Vector3(1.65f, 1f, 0));
            Pan(view.leftPan, view.brass, view.darkBrass); Pan(view.rightPan, view.brass, view.darkBrass);
            view.fixedSculpture = Group("Fixed bronze reference sculpture", view.leftPan, new Vector3(0, .045f, 0));
            var fallbackSculpture = Shape("Fixed flame-shaped bronze study", view.fixedSculpture, FlameMesh(), view.brass, Vector3.zero);
            if (CollectedSculpture(view.fixedSculpture, "BottleBalance/Models/BronzeTwist", view.brass, 1.05f)) fallbackSculpture.gameObject.SetActive(false);
            Cube("Balance title plaque", view.scaleStation, new Vector3(0, .145f, -.916f), new Vector3(1.85f, .18f, .025f), view.darkBrass);
            Label(view.scaleStation, "균형의 전시실", new Vector3(0, .145f, -.937f), .12f, new Color(.96f, .83f, .51f));
            view.SetBottlePose(1, 0); view.SetBalance(12, 0, false);
            return view;
        }
    }
}
