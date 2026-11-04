using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    /// <summary>Game interpretation of the Bomarzo stone mask, with a genuinely hollow, compound-collider mouth.</summary>
    public static class GalleryOrcusBuilder
    {
        static Material Stone(string name, Color color, bool stone = true)
        {
            var material = new Material(Shader.Find("Parallax/GalleryOrcusPortal")) { name = name, color = color };
            material.SetFloat("_StoneMode", stone ? 1 : 0);
            GalleryWorld.Materials.Add(material); return material;
        }

        public static GalleryProp Create(GalleryGame game, Transform parent, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("04 · 오르쿠스 — 공간을 삼키는 입");
            root.transform.SetParent(parent, false); root.transform.SetPositionAndRotation(position, rotation);
            var prop = root.AddComponent<GalleryProp>(); prop.title = "04 · 오르쿠스 — 입 안의 바람";
            prop.zone = 3; prop.platform = false; prop.minScale = .45f;
            var stone = Stone("Orcus · warm carved limestone", new Color(.65f, .58f, .44f));
            var pale = Stone("Orcus · worn raised features", new Color(.74f, .67f, .52f));
            var recess = Stone("Orcus · carved recesses", new Color(.095f, .086f, .073f));
            var membrane = Stone("Orcus · deep rippling aperture", new Color(.025f, .04f, .045f), false);
            MakeMesh("Orcus carved hollow face", FaceMesh(), stone, root.transform);
            // Four physical blocks surround the aperture. No hidden full-face collision box spans the mouth.
            Block("Orcus left cheek", new Vector3(-.86f, .74f, 0), new Vector3(.58f, 1.18f, 1.03f), root.transform);
            Block("Orcus right cheek", new Vector3(.86f, .74f, 0), new Vector3(.58f, 1.18f, 1.03f), root.transform);
            Block("Orcus forehead", new Vector3(0, 1.68f, 0), new Vector3(2.28f, .76f, 1.03f), root.transform);
            Block("Orcus lower lip", new Vector3(0, .09f, 0), new Vector3(2.28f, .18f, 1.03f), root.transform);
            // Each rise stays <= .15 m at the intended 2.5x enlargement, below the motor's .2 m step.
            // The approach is part of the movable compound prop, so its geometry scales with the mouth.
            Step("Orcus threshold · first low stone", new Vector3(0, .03f, -.92f), new Vector3(.92f, .06f, .28f), stone, root.transform);
            Step("Orcus threshold · second low stone", new Vector3(0, .06f, -.73f), new Vector3(.92f, .12f, .20f), stone, root.transform);
            Step("Orcus threshold · lip approach", new Vector3(0, .09f, -.565f), new Vector3(.92f, .18f, .20f), stone, root.transform);

            for (int side = -1; side <= 1; side += 2)
            {
                Ball("Carved eye socket", new Vector3(side * .48f, 1.61f, -.57f), new Vector3(.43f, .44f, .22f), recess, root.transform);
                Ball("Bulging stone eye", new Vector3(side * .48f, 1.61f, -.66f), new Vector3(.34f, .36f, .22f), pale, root.transform);
                Ball("Deep round pupil", new Vector3(side * .48f, 1.61f, -.777f), new Vector3(.12f, .14f, .036f), recess, root.transform);
                Ball("Heavy carved brow", new Vector3(side * .48f, 1.84f, -.56f), new Vector3(.63f, .18f, .29f), stone, root.transform);
                Ball("Cheekbone", new Vector3(side * .86f, 1.24f, -.52f), new Vector3(.42f, .42f, .25f), pale, root.transform);
                Ball("Upper square tooth", new Vector3(side * .22f, 1.37f, -.58f), new Vector3(.21f, .20f, .22f), pale, root.transform, true);
                for (int row = 0; row < 4; row++)
                    MakeMesh("Orcus curling stone hair", Curl(new Vector3(side * (1.02f + (row % 2) * .04f), .35f + row * .43f, -.40f), .19f, side), stone, root.transform);
            }
            for (int i = -2; i <= 2; i++)
                MakeMesh("Orcus crown curls", Curl(new Vector3(i * .31f, 1.95f + .05f * Mathf.Cos(i), -.30f), .17f, i < 0 ? -1 : 1), pale, root.transform);
            Ball("Orcus broad nose", new Vector3(0, 1.48f, -.67f), new Vector3(.38f, .39f, .44f), pale, root.transform);
            Ball("Orcus nose bridge", new Vector3(0, 1.72f, -.54f), new Vector3(.21f, .31f, .27f), stone, root.transform);
            for (int side = -1; side <= 1; side += 2)
                Ball("Carved nostril", new Vector3(side * .105f, 1.385f, -.88f), new Vector3(.105f, .065f, .036f), recess, root.transform);
            var surface = MakeMesh("Orcus mouth · rippling dark boundary", Aperture(), membrane, root.transform);
            var exit = root.AddComponent<GalleryOrcusExit>(); exit.game = game; exit.prop = prop; exit.portal = surface;
            prop.Remember(); return prop;
        }

        static void Block(string name, Vector3 at, Vector3 size, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = at;
            var box = go.AddComponent<BoxCollider>(); box.size = size;
            go.AddComponent<GalleryCaster>().shape = box;
        }

        static void Step(string name, Vector3 at, Vector3 size, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = at; go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            go.AddComponent<GalleryCaster>().shape = go.GetComponent<BoxCollider>();
        }

        static Renderer Ball(string name, Vector3 at, Vector3 scale, Material material, Transform parent, bool square = false)
        {
            var go = GameObject.CreatePrimitive(square ? PrimitiveType.Cube : PrimitiveType.Sphere);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = at; go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return renderer;
        }

        static Renderer MakeMesh(string name, Mesh mesh, Material material, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return renderer;
        }

        static Mesh FaceMesh()
        {
            const int segments = 72, bands = 8;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int depth = 0; depth < 2; depth++) for (int band = 0; band <= bands; band++) for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments, t = (float)band / bands;
                var inner = new Vector2(Mathf.Cos(angle) * .62f, .78f + Mathf.Sin(angle) * .60f);
                var outer = new Vector2(Mathf.Cos(angle) * 1.14f, 1.025f + Mathf.Sin(angle) * 1.035f);
                Vector2 p = Vector2.Lerp(inner, outer, t);
                float bevel = Mathf.Sin(t * Mathf.PI) * .14f;
                float z = depth == 0 ? -.51f - bevel + .025f * Mathf.Sin(angle * 5) * t : .50f;
                vertices.Add(new Vector3(p.x, p.y, z)); uv.Add(new Vector2((p.x + 1.2f) / 2.4f, p.y / 2.1f));
            }
            int row = segments + 1, layer = row * (bands + 1);
            for (int depth = 0; depth < 2; depth++) for (int band = 0; band < bands; band++) for (int i = 0; i < segments; i++)
            {
                int a = depth * layer + band * row + i, b = a + row;
                if (depth == 0) { Quad(triangles, a, a + 1, b + 1, b); }
                else { Quad(triangles, a, b, b + 1, a + 1); }
            }
            for (int i = 0; i < segments; i++)
            {
                Quad(triangles, i, layer + i, layer + i + 1, i + 1);
                int a = bands * row + i; Quad(triangles, a, a + 1, layer + a + 1, layer + a);
            }
            return Mesh("Orcus · hollow carved mask", vertices, uv, triangles);
        }

        static Mesh Aperture()
        {
            const int segments = 72;
            var vertices = new List<Vector3> { new Vector3(0, .78f, -.49f) };
            var uv = new List<Vector2> { new Vector2(.5f, .5f) }; var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * .619f, .78f + Mathf.Sin(a) * .599f, -.49f));
                uv.Add(new Vector2(.5f + Mathf.Cos(a) * .5f, .5f + Mathf.Sin(a) * .5f));
                if (i > 0) { triangles.Add(0); triangles.Add(i + 1); triangles.Add(i); }
            }
            return Mesh("Orcus · elliptical membrane", vertices, uv, triangles);
        }

        static Mesh Curl(Vector3 center, float radius, int side)
        {
            const int steps = 28, sides = 7;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps, a = t * Mathf.PI * 2.8f * side, r = radius * (1 - t * .76f);
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, -.05f * t);
                Vector3 tangent = new Vector3(-Mathf.Sin(a) * side, Mathf.Cos(a) * side, -.05f).normalized;
                Vector3 right = Vector3.Cross(tangent, Vector3.forward).normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float angle = j * Mathf.PI * 2 / sides;
                    vertices.Add(p + (right * Mathf.Cos(angle) + Vector3.forward * Mathf.Sin(angle)) * .065f);
                    uv.Add(new Vector2(t, (float)j / sides));
                    if (i > 0 && j < sides)
                    { int b = i * (sides + 1) + j; Quad(triangles, b, b + 1, b - sides, b - sides - 1); }
                }
            }
            return Mesh("Orcus · carved spiral", vertices, uv, triangles);
        }

        static void Quad(List<int> t, int a, int b, int c, int d)
        { t.Add(a); t.Add(b); t.Add(c); t.Add(a); t.Add(c); t.Add(d); }
        static Mesh Mesh(string name, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
#if UNITY_EDITOR
            // Scene regeneration must persist procedural meshes, just as it persists the exhibit materials.
            if (!Application.isPlaying)
            {
                const string directory = "Assets/Parallax/Gallery/Generated/Orcus/";
                System.IO.Directory.CreateDirectory(directory);
                string path = directory + name.Replace('·', '-').Replace(' ', '_') + ".asset";
                // Curls use one shared topology with distinct centres baked into their vertices.
                // Include the first vertex in the name so each decorative curl keeps its own geometry.
                if (name.Contains("spiral"))
                {
                    var p = vertices[0];
                    path = directory + "curl_" + p.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "_" +
                        p.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ".asset";
                }
                var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) UnityEditor.AssetDatabase.CreateAsset(mesh, path);
                else
                {
                    UnityEditor.EditorUtility.CopySerialized(mesh, existing);
                    Object.DestroyImmediate(mesh); mesh = existing;
                }
            }
#endif
            return mesh;
        }
    }
}
