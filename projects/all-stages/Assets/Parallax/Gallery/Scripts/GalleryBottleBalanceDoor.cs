using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // A fixed arched walnut door, matching the museum's existing exhibition
    // doors. Only its two leaves swing; the doorway never rises like a gate.
    public sealed class GalleryBottleBalanceDoor
    {
        const float HingeX = 1.74f;
        const float LeafWidth = 1.73f;
        const float Spring = 3.90f;
        const float OpenDegrees = 100f;
        public const float OpeningSeconds = 3.0f;
        public Transform Root { get; private set; }
        public Transform LeftLeaf { get; private set; }
        public Transform RightLeaf { get; private set; }
        public BoxCollider Barrier { get; private set; }
        public float Opening { get; private set; }
        public float LeafAngle { get; private set; }

        public static GalleryBottleBalanceDoor Create(Transform parent, Material brass, Material darkBrass)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var view = new GalleryBottleBalanceDoor();
            view.Root = Group(parent, "Balance exhibition · arched walnut double door", new Vector3(0, 0, GalleryBottleBalanceLayout.EndZ-.22f));
            Material walnut = ExistingWalnut(parent);
            Material raised = new Material(walnut) { name = "Balance door · raised walnut panels" };
            raised.color *= new Color(1.10f, 1.07f, 1.03f, 1);
            GalleryWorld.Materials.Add(raised);

            view.LeftLeaf = Leaf(view.Root, -1, walnut, raised, brass, darkBrass);
            view.RightLeaf = Leaf(view.Root, 1, walnut, raised, brass, darkBrass);
            view.Barrier = view.Root.gameObject.AddComponent<BoxCollider>();
            view.Barrier.center = new Vector3(0, 2.8f, -.15f);
            view.Barrier.size = new Vector3(3.5f, 5.6f, .18f);
            view.SetOpening(0);
            return view;
        }

        public void SetOpening(float progress)
        {
            Opening = Mathf.Clamp01(progress);
            LeafAngle = OpenDegrees * Mathf.SmoothStep(0, 1, Opening);
            // Both leaves open away from the visitor into the next gallery.
            LeftLeaf.localRotation = Quaternion.Euler(0, -LeafAngle, 0);
            RightLeaf.localRotation = Quaternion.Euler(0, LeafAngle, 0);
            // At 55 degrees the visible central gap is already wider than the
            // visitor capsule. The leaf colliders remain physical throughout.
            Barrier.enabled = LeafAngle < 55f;
            Physics.SyncTransforms();
        }

        static Material ExistingWalnut(Transform parent)
        {
            foreach (var renderer in parent.root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.name.IndexOf("walnut", StringComparison.OrdinalIgnoreCase) >= 0)
                        return material;
            var shader = Shader.Find("Parallax/MuseumSurface");
            var walnut = new Material(shader != null ? shader : Shader.Find("Standard"))
                { name = "Balance door · dark museum walnut", color = new Color(.25f, .13f, .065f) };
            if (shader != null)
            {
                GalleryMuseumMaterials.Apply(walnut, "wood");
                walnut.SetFloat("_Roughness", .52f);
                walnut.SetFloat("_ReflectionStrength", .12f);
            }
            GalleryWorld.Materials.Add(walnut);
            return walnut;
        }

        static Transform Group(Transform parent, string name, Vector3 at)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false); group.localPosition = at;
            return group;
        }

        static Renderer Block(Transform parent, string name, Vector3 at, Vector3 size, Material material, bool solid = false)
        {
            var renderer = GalleryConceptArchitecture.Block(parent, name, at, size, material, .008f);
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            if (solid) renderer.gameObject.AddComponent<BoxCollider>().size = size;
            return renderer;
        }

        static Transform Leaf(Transform parent, int side, Material walnut, Material raised, Material brass, Material dark)
        {
            var hinge = Group(parent, "Walnut door · " + (side < 0 ? "left hinge" : "right hinge"), new Vector3(side * HingeX, 0, -.15f));
            float center = -side * LeafWidth * .5f;
            Block(hinge, "Solid walnut door leaf", new Vector3(center, 1.95f, 0), new Vector3(LeafWidth, 3.89f, .12f), walnut, true);
            var crownObject = Group(hinge, "Arched walnut crown", Vector3.zero).gameObject;
            Mesh crown = Crown(side);
            crownObject.AddComponent<MeshFilter>().sharedMesh = crown;
            var crownRenderer = crownObject.AddComponent<MeshRenderer>(); crownRenderer.sharedMaterial = walnut;
            crownRenderer.shadowCastingMode = ShadowCastingMode.On; crownRenderer.receiveShadows = true;
            crownObject.AddComponent<MeshCollider>().sharedMesh = crown;

            // Model both faces: the opened door is still an exhibition-quality
            // object when seen from the portrait room.
            foreach (float face in new[] { -1f, 1f })
            {
                float z = face * .074f;
                foreach (float x in new[] { -side * .15f, -side * (LeafWidth - .15f) })
                    Block(hinge, "Raised walnut vertical stile", new Vector3(x, 1.95f, z), new Vector3(.18f, 3.72f, .05f), raised);
                foreach (float y in new[] { .19f, 1.18f, 3.68f })
                    Block(hinge, "Raised walnut horizontal rail", new Vector3(center, y, z), new Vector3(LeafWidth - .20f, .16f, .05f), raised);
                FramedPanel(hinge, center, .67f, LeafWidth - .44f, .65f, face, walnut, raised, brass);
                FramedPanel(hinge, center, 2.43f, LeafWidth - .44f, 2.12f, face, walnut, raised, brass);
                Block(hinge, "Slim brass meeting stile", new Vector3(-side * (LeafWidth - .018f), 1.96f, face * .109f),
                    new Vector3(.025f, 3.75f, .014f), brass);
                var handleAt = new Vector3(-side * (LeafWidth - .29f), 1.76f, face * .13f);
                Block(hinge, "Brass handle backplate", handleAt, new Vector3(.105f, .39f, .035f), dark);
                Rod(hinge, "Polished vertical door pull", handleAt + new Vector3(0, -.12f, face * .055f),
                    handleAt + new Vector3(0, .12f, face * .055f), .022f, brass);
                foreach (float y in new[] { -.12f, .12f })
                    Rod(hinge, "Door pull short mounting pin", handleAt + Vector3.up * y,
                        handleAt + Vector3.up * y + Vector3.forward * face * .055f, .017f, brass);
                CurvedMoulding(hinge, side, face, brass);
            }
            foreach (float y in new[] { .52f, 1.97f, 3.48f })
                Rod(hinge, "Brass door hinge barrel", new Vector3(0, y - .12f, 0), new Vector3(0, y + .12f, 0), .038f, dark);
            return hinge;
        }

        static void FramedPanel(Transform parent, float x, float y, float width, float height, float face,
            Material walnut, Material raised, Material brass)
        {
            Block(parent, "Inset walnut door panel", new Vector3(x, y, face * .086f), new Vector3(width, height, .032f), walnut);
            float z = face * .111f;
            foreach (float dx in new[] { -width * .5f, width * .5f })
            {
                Block(parent, "Raised panel side moulding", new Vector3(x + dx, y, z), new Vector3(.055f, height + .055f, .034f), raised);
                Block(parent, "Fine brass panel side inlay", new Vector3(x + dx, y, z + face * .02f), new Vector3(.012f, height, .009f), brass);
            }
            foreach (float dy in new[] { -height * .5f, height * .5f })
            {
                Block(parent, "Raised panel horizontal moulding", new Vector3(x, y + dy, z), new Vector3(width + .055f, .055f, .034f), raised);
                Block(parent, "Fine brass panel horizontal inlay", new Vector3(x, y + dy, z + face * .02f), new Vector3(width, .012f, .009f), brass);
            }
        }

        static void Rod(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name;
            go.transform.SetParent(parent, false); Vector3 delta = to - from;
            go.transform.localPosition = (from + to) * .5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            go.transform.localScale = new Vector3(radius * 2, delta.magnitude * .5f, radius * 2);
            var collider = go.GetComponent<Collider>(); collider.enabled = false;
            if (Application.isPlaying) UnityEngine.Object.Destroy(collider); else UnityEngine.Object.DestroyImmediate(collider);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        }

        static void CurvedMoulding(Transform hinge, int side, float face, Material brass)
        {
            const int steps = 32;
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float distance = Mathf.Lerp(.035f, LeafWidth - .025f, i / (float)steps);
                float y = Spring + Mathf.Sqrt(Mathf.Max(0, HingeX * HingeX - Mathf.Pow(HingeX - distance, 2))) - .055f;
                Vector3 next = new Vector3(-side * distance, y, face * .085f);
                if (i > 0) Rod(hinge, "Arched brass door crown moulding", previous, next, .014f, brass);
                previous = next;
            }
        }

        static Mesh Crown(int side)
        {
            const int steps = 32;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float distance = Mathf.Lerp(.012f, LeafWidth, i / (float)steps);
                float x = -side * distance;
                float top = Spring + Mathf.Sqrt(Mathf.Max(0, HingeX * HingeX - Mathf.Pow(HingeX - distance, 2)));
                foreach (float z in new[] { -.06f, .06f }) foreach (float y in new[] { Spring - .018f, top })
                { vertices.Add(new Vector3(x, y, z)); uv.Add(new Vector2(x, y)); }
                if (i == 0) continue;
                int a = (i - 1) * 4, b = i * 4;
                foreach (var q in new[] { new[] { a, b, b + 1, a + 1 }, new[] { a + 2, a + 3, b + 3, b + 2 },
                    new[] { a + 1, b + 1, b + 3, a + 3 }, new[] { a, a + 2, b + 2, b } })
                    triangles.AddRange(new[] { q[0], q[1], q[2], q[0], q[2], q[3] });
            }
            int end = steps * 4;
            triangles.AddRange(new[] { 0, 1, 3, 0, 3, 2, end, end + 2, end + 3, end, end + 3, end + 1 });
            if (side > 0) for (int i = 0; i < triangles.Count; i += 3)
            { int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap; }
            var mesh = new Mesh { name = "Balance exhibition · quarter-arched walnut door crown" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
