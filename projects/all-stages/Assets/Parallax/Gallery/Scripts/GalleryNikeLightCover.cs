using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // The lamp remains fixed inside a real open-sided compound collider.
    // The wall-facing side and underside stay open. The entire exhibit uses
    // the ordinary perspective pickup, lifting, carrying and release lifecycle.
    public sealed class GalleryNikeLightCover : MonoBehaviour
    {
        const string ResourceRoot = "MuseumScans/Aurelius/";
        public static readonly Vector3 StartPosition = new Vector3(-6.16f, 0, 18.20f);
        public static readonly Vector3 RevealedPosition = new Vector3(-4.36f, 0, 18.20f);
        public static readonly Vector3 CoverSize = new Vector3(1.20f, .95f, 1.15f);
        const float Wall = .10f, BustHeight = .75f;
        [SerializeField] GalleryProp prop;
        [SerializeField] bool discovered;
        BoxCollider[] blockingParts;
        public GalleryProp Prop => prop;
        public bool Discovered => discovered;

        void Awake()
        {
            // PlacementSupport's preview switches are runtime properties, so
            // the opaque-cover rule must also be restored after scene loading.
            if (prop == null) prop = GetComponent<GalleryProp>();
            if (prop == null) return;
            prop.minScale = .45f;
            prop.maxScale = 3.4f;
            prop.InitializePhysics();
            prop.placement.PreviewWhileHeld = false;
            prop.placement.PreviewUntilSettled = false;
        }

        public static GalleryNikeLightCover Install(GalleryGame game, Transform parent)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var existing = game.GetComponentInChildren<GalleryNikeLightCover>(true);
            if (existing != null) return existing;
            var root = new GameObject("02 · 아우렐리우스 흉상과 조명 가림 받침").transform;
            root.SetParent(parent == null ? game.transform : parent, false);
            root.localPosition = StartPosition;
            var cover = root.gameObject.AddComponent<GalleryNikeLightCover>();
            cover.prop = root.gameObject.AddComponent<GalleryProp>();
            cover.prop.title = "대리석 흉상과 받침";
            cover.prop.zone = 1;
            cover.prop.minScale = .45f;
            cover.prop.maxScale = 3.4f;
            var stone = Surface("Nike cover · warm marble", "Stone", new Color(.87f, .85f, .80f));
            stone.SetFloat("_WorldTiling", .22f);
            stone.SetFloat("_Roughness", .72f);
            // A single solid bounding collider would fill the lamp's cavity.
            AddPanel(root, "Marble cover top", new Vector3(0, CoverSize.y - Wall / 2, 0),
                new Vector3(CoverSize.x, Wall, CoverSize.z), stone);
            AddPanel(root, "Marble cover · room side", new Vector3((CoverSize.x - Wall) / 2, (CoverSize.y - Wall) / 2, 0),
                new Vector3(Wall, CoverSize.y - Wall, CoverSize.z), stone);
            foreach (int side in new[] { -1, 1 })
                AddPanel(root, "Marble cover · " + (side > 0 ? "canvas face" : "entrance face"),
                    new Vector3(-Wall / 2, (CoverSize.y - Wall) / 2, side * (CoverSize.z - Wall) / 2),
                    new Vector3(CoverSize.x - Wall, CoverSize.y - Wall, Wall), stone);
            InstallBust(root);
            var captionObject = new GameObject("Aurelius · artist credit");
            captionObject.transform.SetParent(root, false);
            captionObject.transform.localPosition = new Vector3(0, .68f, -CoverSize.z / 2 - .013f);
            var caption = captionObject.AddComponent<GalleryLabel>();
            caption.caption = "마르쿠스 아우렐리우스\nAurelius (PhotoScan) · ryanfb\nCC BY 4.0 · 크기와 재질 조정";
            caption.size = .038f;
            caption.color = new Color(.22f, .19f, .15f);
            cover.prop.Remember();
            cover.blockingParts = cover.prop.parts;
            cover.prop.InitializePhysics();
            cover.prop.placement.PreviewWhileHeld = false;
            cover.prop.placement.PreviewUntilSettled = false;
            if (game.controller != null) cover.prop.placement.SetPlayer(game.controller);
            cover.prop.placement.ResetPlaced();
            return cover;
        }

        static Material Surface(string name, string category, Color color)
        {
            var shader = Shader.Find("Parallax/MuseumSurface");
            if (shader == null) throw new InvalidOperationException("MuseumSurface is required for the marble cover.");
            var material = new Material(shader) { name = name };
            GalleryMuseumMaterials.Apply(material, category);
            material.color = color;
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_ExhibitLight", .08f);
            GalleryWorld.Materials.Add(material);
            return material;
        }

        static void AddPanel(Transform root, string name, Vector3 at, Vector3 size, Material material)
        {
            var renderer = GalleryConceptArchitecture.Block(root, name, at, size, material, .008f);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            var collider = renderer.gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            renderer.gameObject.AddComponent<GalleryCaster>().shape = collider;
        }

        static void InstallBust(Transform parent)
        {
            var art = Surface("Aurelius · original scan texture", "Artwork", Color.white);
            var albedo = Resources.Load<Texture2D>(ResourceRoot + "AureliusAlbedo");
            var source = Resources.Load<GameObject>(ResourceRoot + "AureliusBust");
            var pivot = new GameObject("Aurelius scan · normalized display").transform;
            pivot.SetParent(parent, false);
            GameObject model;
            if (source != null)
            {
                model = Instantiate(source, pivot, false);
                model.name = "Aurelius (PhotoScan) · ryanfb · original model";
            }
            else
            {
                var mesh = Resources.Load<Mesh>(ResourceRoot + "AureliusMesh");
                if (mesh == null) throw new InvalidOperationException("The selected Aurelius (PhotoScan) model is missing.");
                model = new GameObject("Aurelius (PhotoScan) · ryanfb · original mesh");
                model.transform.SetParent(pivot, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>();
            }
            // Stand the source scan upright, then face the incoming visitor
            // before measuring the display bottom and carry volume.
            Vector3 entryDirection = new Vector3(-StartPosition.x, 0, 15.70f - StartPosition.z);
            model.transform.localRotation = Quaternion.LookRotation(entryDirection, Vector3.up) *
                model.transform.localRotation * Quaternion.Euler(180, 0, 0);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("The Aurelius scan has no renderers.");
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                if (albedo == null && renderer.sharedMaterial != null)
                    albedo = renderer.sharedMaterial.mainTexture as Texture2D;
            }
            if (albedo == null) throw new InvalidOperationException("The selected Aurelius scan's original albedo is missing.");
            art.SetTexture("_MainTex", albedo);
            art.SetFloat("_UseSurfaceMaps", 0);
            art.SetFloat("_Roughness", .74f);
            if (bounds.size.y <= .00001f || float.IsNaN(bounds.size.y))
                throw new InvalidOperationException("The Aurelius scan has invalid display bounds.");
            Vector3 bottom = pivot.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            float scale = BustHeight / bounds.size.y;
            pivot.localScale = Vector3.one * scale;
            pivot.localPosition = Vector3.up * CoverSize.y - bottom * scale;
            foreach (var renderer in renderers)
            {
                var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++) materials[i] = art;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            // A lightweight carry volume stays separate from the scanned render mesh.
            var carryShape = new GameObject("Aurelius scan · carry volume");
            carryShape.transform.SetParent(parent, false);
            var box = carryShape.AddComponent<BoxCollider>();
            box.center = new Vector3(0, CoverSize.y + BustHeight / 2, 0);
            box.size = new Vector3(bounds.size.x * scale, BustHeight, bounds.size.z * scale);
            carryShape.AddComponent<GalleryCaster>().shape = box;
        }

        public bool BlocksLight(Transform emitter, Vector3 target)
        {
            if (emitter == null || !gameObject.activeInHierarchy) return false;
            if (blockingParts == null || blockingParts.Length == 0)
                blockingParts = GetComponentsInChildren<BoxCollider>(true);
            foreach (var part in blockingParts)
            {
                if (part == null || !part.enabled || !part.gameObject.activeInHierarchy) continue;
                Vector3 origin = part.transform.InverseTransformPoint(emitter.position) - part.center;
                Vector3 end = part.transform.InverseTransformPoint(target) - part.center;
                if (SegmentIntersectsBox(origin, end - origin, part.size / 2)) return true;
            }
            return false;
        }

        // A slab query handles an emitter inside a moved wall, unlike Raycast,
        // which omits a collider containing the ray origin. Held layers/readiness
        // never cause an opaque cover to stop blocking the puzzle source.
        static bool SegmentIntersectsBox(Vector3 origin, Vector3 delta, Vector3 half)
        {
            float near = 0, far = 1;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(delta[axis]) < .000001f)
                {
                    if (origin[axis] < -half[axis] || origin[axis] > half[axis]) return false;
                    continue;
                }
                float first = (-half[axis] - origin[axis]) / delta[axis];
                float last = (half[axis] - origin[axis]) / delta[axis];
                if (first > last) { float swap = first; first = last; last = swap; }
                near = Mathf.Max(near, first); far = Mathf.Min(far, last);
                if (near > far) return false;
            }
            return far >= .00001f && near < .99999f;
        }

        public bool IsBlockingEmitter(Transform emitter, Vector3 target) => BlocksLight(emitter, target);
        public void Observe(Transform emitter, Vector3 target)
        {
            if (emitter != null && !BlocksLight(emitter, target)) discovered = true;
        }
        public void ResetDiscovery() { discovered = false; }
    }
}
