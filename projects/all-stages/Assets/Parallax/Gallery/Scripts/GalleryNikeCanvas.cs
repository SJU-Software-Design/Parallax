using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    public sealed partial class GalleryNikeCanvas : MonoBehaviour
    {
        public static readonly Vector3 SolutionPosition = new Vector3(-5.10f, .006f, 24.75f);
        public static readonly Vector3 DemoPosition = new Vector3(0, 0, 15.70f);
        public const float SolutionScale = .90f;
        public float RequiredMatch => portablePainting != null ? 1f : .70f;
        public static readonly Vector3 Center = new Vector3(-4.48f, 2.70f, 28.68f);
        static readonly Vector2 Size = new Vector2(3.45f, 4.95f);
        const int SampleWidth = 28, SampleHeight = 40;

        public Transform emitter, sculpture;
        public GalleryProp statue;
        public MeshCollider scanProbe;
        public Renderer guide, painting;
        public GameObject shadeCanvas, shadeMarks;
        [SerializeField] byte[] target;

        public bool Complete => portablePainting != null ? fusionPhase == FusionPhase.Complete : painting != null && painting.gameObject.activeSelf;


        public static GalleryNikeCanvas Install(Transform parent, GalleryProp statue, Transform emitter,
            Material gold, Material walnut, Material linen, Material mint)
        {
            var root = Group(parent, "Nike · shadow alignment canvas", Center);
            var canvas = root.gameObject.AddComponent<GalleryNikeCanvas>();
            canvas.statue = statue;
            canvas.emitter = emitter;
            canvas.sculpture = statue.transform.Find("Nike · original scanned geometry");
            var probe = Group(parent, "Nike · off-map silhouette query", new Vector3(0, -100, 0));
            probe.gameObject.layer = 2;
            canvas.scanProbe = probe.gameObject.AddComponent<MeshCollider>();
            canvas.scanProbe.sharedMesh = Resources.Load<Mesh>("MuseumScans/NikeCollision");
            if (canvas.sculpture == null || canvas.scanProbe.sharedMesh == null)
                throw new InvalidOperationException("Nike canvas requires the visible scan and its raycast mesh.");

            Frame(root, Size, gold, walnut);
            var guideMaterial = new Material(Shader.Find("Parallax/MuseumSurface")) { name = "Nike canvas · silhouette", color = Color.white };
            guideMaterial.SetFloat("_ExhibitLight", .18f);
            guideMaterial.SetFloat("_ReflectionStrength", .03f);
            GalleryWorld.Materials.Add(guideMaterial);
            var paintingTexture = Resources.Load<Texture2D>("MuseumArt/NikeShadowStudy");
            if (paintingTexture == null) throw new InvalidOperationException("The completed Nike painting is missing.");
            var paintedMaterial = new Material(Shader.Find("Parallax/MuseumNikeBrush"))
                { name = "Nike canvas · completed oil painting", mainTexture = paintingTexture };
            paintedMaterial.SetFloat("_Reveal", 0);
            GalleryWorld.Materials.Add(paintedMaterial);
            canvas.guide = Face(root, "Projected shadow guide", Size, guideMaterial);
            canvas.painting = Face(root, "Completed Nike painting", Size, paintedMaterial);
            // The brush layer sits in front of the original guide without
            // changing the team's painting texture or its UV coordinates.
            canvas.painting.transform.localPosition = Vector3.back * .008f;

            Vector3 receiver = new Vector3(0, 0, 29.0f);
            Vector3 source = emitter.position;
            Vector3 flatSource = new Vector3(source.x, 0, source.z);
            Quaternion routeRotation = Quaternion.LookRotation(receiver - flatSource);
            canvas.InstallFusion(parent, gold, walnut, linen, paintedMaterial);

            var marks = Group(parent, "Mint footsteps to the arched doorway", Vector3.zero);
            canvas.shadeMarks = marks.gameObject;
            Vector3 sideways = routeRotation * Vector3.right;
            for (int i = 0; i < 9; i++)
            {
                float z = Mathf.Lerp(25.4f, 28.65f, i / 8f);
                Vector3 p = Vector3.Lerp(flatSource, receiver, Mathf.InverseLerp(flatSource.z, receiver.z, z));
                p += sideways * (i % 2 == 0 ? -.12f : .12f);
                var footprint = Group(marks, "Footstep", p + Vector3.up * .012f);
                footprint.localRotation = routeRotation * Quaternion.Euler(0, i % 2 == 0 ? -11 : 11, 0);
                Sole(footprint, mint, new Vector3(0, 0, .055f), new Vector3(.105f, .005f, .155f));
                Sole(footprint, mint, new Vector3(0, 0, -.065f), new Vector3(.076f, .005f, .070f));
            }
            canvas.SetComplete(false);
            return canvas;
        }

        public void SetComplete(bool complete)
        {
            SetArtworkReveal(complete ? 1 : 0);
            guide.gameObject.SetActive(!complete);
            painting.gameObject.SetActive(complete);
            if (shadeCanvas != null) shadeCanvas.SetActive(complete);
            shadeMarks.SetActive(complete);
        }

        public Texture2D BakeGuide()
        {
            Physics.SyncTransforms();
            Matrix4x4 meshToProp = statue.transform.worldToLocalMatrix * sculpture.localToWorldMatrix;
            Matrix4x4 targetWorldToMesh = (Matrix4x4.TRS(SolutionPosition, statue.homeRotation,
                Vector3.one * SolutionScale) * meshToProp).inverse;
            Matrix4x4 targetWorldToProbe = scanProbe.transform.localToWorldMatrix * targetWorldToMesh;
            Vector3 origin = targetWorldToProbe.MultiplyPoint3x4(emitter.position);
            Matrix4x4 canvasToProbe = targetWorldToProbe * transform.localToWorldMatrix;
            const int width = 320, height = 460, padX = 55, padY = 41;
            var mask = new bool[width * height];
            var left = new int[height];
            var right = new int[height];
            for (int y = 0; y < height; y++)
            {
                left[y] = width; right[y] = -1;
                for (int x = 0; x < width; x++)
                {
                    if (!Hits(origin, canvasToProbe, (x + .5f) / width, (y + .5f) / height)) continue;
                    mask[y * width + x] = true;
                    left[y] = Mathf.Min(left[y], x); right[y] = x;
                }
            }

            var allowed = new bool[mask.Length];
            for (int y = 0; y < height; y++)
            {
                int first = width, last = -1;
                for (int row = Mathf.Max(0, y - padY); row <= Mathf.Min(height - 1, y + padY); row++)
                {
                    if (right[row] < 0) continue;
                    float round = Mathf.Sqrt(1 - Mathf.Pow((row - y) / (float)padY, 2));
                    int margin = Mathf.RoundToInt(padX * round);
                    first = Mathf.Min(first, left[row] - margin);
                    last = Mathf.Max(last, right[row] + margin);
                }
                if (last < 0) continue;
                for (int x = Mathf.Max(5, first); x <= Mathf.Min(width - 6, last); x++) allowed[y * width + x] = true;
            }

            target = new byte[SampleWidth * SampleHeight];
            int filled = 0;
            for (int y = 0; y < SampleHeight; y++)
            for (int x = 0; x < SampleWidth; x++)
            {
                int pixel = (int)((y + .5f) * height / SampleHeight) * width + (int)((x + .5f) * width / SampleWidth);
                if (allowed[pixel]) { target[y * SampleWidth + x] = 1; filled++; }
            }
            if (filled < 120) throw new InvalidOperationException("The target Nike silhouette is too small.");

            var pixels = new Color32[mask.Length];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                int grain = ((x * 131 + y * 97 + (x ^ y) * 53) & 7) - 3;
                // Only the displayed guide becomes an outline. The target mask
                // and its scoring margin above keep their original filled shape.
                float nearestEdge = float.PositiveInfinity;
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height || mask[ny * width + nx] == mask[i]) continue;
                    nearestEdge = Mathf.Min(nearestEdge, Mathf.Sqrt(dx * dx + dy * dy));
                }
                // A soft two-pixel contour leaves both sides as ivory linen;
                // the dark interior must come from the actual cast shadow.
                float ink = Mathf.Clamp01(1.75f - nearestEdge);
                var linen = new Color32((byte)(214 + grain), (byte)(207 + grain), (byte)(194 + grain), 255);
                var outline = new Color32((byte)(110 + grain), (byte)(90 + grain), (byte)(70 + grain), 255);
                pixels[i] = Color32.Lerp(linen, outline, ink);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            { name = "Nike shadow alignment contour guide", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        public float Match()
        {
            if (target == null || target.Length == 0) return 0;
            Matrix4x4 worldToProbe = scanProbe.transform.localToWorldMatrix * sculpture.worldToLocalMatrix;
            Vector3 origin = worldToProbe.MultiplyPoint3x4(emitter.position);
            Matrix4x4 canvasToProbe = worldToProbe * transform.localToWorldMatrix;
            int overlap = 0, castCount = 0, union = 0;
            // Sample outside the frame as well so a cropped wing cannot count as a fit.
            int border = portablePainting != null ? 5 : 0;
            for (int y = -border; y < SampleHeight + border; y++)
            for (int x = -border; x < SampleWidth + border; x++)
            {
                bool expected = y >= 0 && y < SampleHeight && x >= 0 && x < SampleWidth && target[y * SampleWidth + x] != 0;
                bool cast = Hits(origin, canvasToProbe, (x + .5f) / SampleWidth, (y + .5f) / SampleHeight);
                if (cast) castCount++;
                if (cast || expected) union++;
                if (cast && expected) overlap++;
            }
            int total = portablePainting != null ? castCount : union;
            return (float)overlap / Mathf.Max(1, total);
        }

        bool Hits(Vector3 origin, Matrix4x4 canvasToProbe, float u, float v)
        {
            Vector3 local = new Vector3((u - .5f) * Size.x, (v - .5f) * Size.y, 0);
            Vector3 delta = canvasToProbe.MultiplyPoint3x4(local) - origin;
            return scanProbe.Raycast(new Ray(origin, delta.normalized), out _, delta.magnitude);
        }

        static Transform Group(Transform parent, string name, Vector3 position)
        {
            var transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            transform.localPosition = position;
            return transform;
        }

        static Renderer Face(Transform root, string name, Vector2 size, Material material)
        {
            var go = Group(root, name, Vector3.zero).gameObject;
            var mesh = new Mesh { name = "Nike canvas face" };
            mesh.vertices = new[] { new Vector3(-size.x / 2, -size.y / 2, 0), new Vector3(size.x / 2, -size.y / 2, 0),
                new Vector3(size.x / 2, size.y / 2, 0), new Vector3(-size.x / 2, size.y / 2, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        static void Frame(Transform root, Vector2 size, Material gold, Material walnut)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                GalleryConceptArchitecture.Block(root, "Walnut outer frame", new Vector3(side * (size.x / 2 + .072f), 0, .025f),
                    new Vector3(.15f, size.y + .30f, .18f), walnut, .024f);
                GalleryConceptArchitecture.Block(root, "Walnut outer frame", new Vector3(0, side * (size.y / 2 + .072f), .025f),
                    new Vector3(size.x, .15f, .18f), walnut, .024f);
                GalleryConceptArchitecture.Block(root, "Gilded inner bead", new Vector3(side * (size.x / 2 + .024f), 0, -.075f),
                    new Vector3(.054f, size.y + .12f, .043f), gold, .012f);
                GalleryConceptArchitecture.Block(root, "Gilded inner bead", new Vector3(0, side * (size.y / 2 + .024f), -.075f),
                    new Vector3(size.x, .054f, .043f), gold, .012f);
            }
        }

        static void Sole(Transform parent, Material mint, Vector3 at, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Painted sole";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mint;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
