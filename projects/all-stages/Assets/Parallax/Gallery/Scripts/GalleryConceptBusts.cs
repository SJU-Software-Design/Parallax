using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // The downloaded FBX keeps its original topology, hierarchy and UVs.
    // Only its display scale, placement and museum lighting material change.
    public static class GalleryConceptBusts
    {
        const string CollectionName = "Concept museum · marble bust collection";
        const string ResourceRoot = "MuseumScans/MarbleBust/";
        const float PlinthHeight = 1.05f, PlinthWidth = .65f, BustHeight = .85f;

        public static void Install(GalleryGame game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (game.transform.Find(CollectionName) != null) return;

            var source = Resources.Load<GameObject>(ResourceRoot + "marble_bust_01_2k");
            var albedo = Resources.Load<Texture2D>(ResourceRoot + "textures/marble_bust_01_diff_2k");
            if (source == null || albedo == null)
                throw new InvalidOperationException("The original Marble Bust 01 FBX and UV albedo are required.");

            var shader = Shader.Find("Parallax/MuseumSurface");
            if (shader == null) throw new InvalidOperationException("MuseumSurface shader is missing.");
            var art = new Material(shader) { name = "Concept marble bust · original UV albedo" };
            GalleryMuseumMaterials.Apply(art, "Artwork");
            art.color = Color.white;
            art.SetTexture("_MainTex", albedo);
            art.SetFloat("_UseSurfaceMaps", 0);
            art.SetFloat("_ExhibitLight", .12f);
            art.SetFloat("_Roughness", .72f);
            art.SetFloat("_Metallic", 0);
            GalleryWorld.Materials.Add(art);

            Material stone = null;
            foreach (var material in GalleryWorld.Materials)
                if (material != null && material.name == "Concept museum · warm limestone")
                { stone = material; break; }
            if (stone == null)
            {
                stone = new Material(shader) { name = "Concept museum · warm limestone" };
                GalleryMuseumMaterials.Apply(stone, "Stone");
                GalleryWorld.Materials.Add(stone);
            }

            var collection = new GameObject(CollectionName).transform;
            collection.SetParent(game.transform, false);
            Display(collection, source, art, stone, new Vector3(5.5f, 0, 26.8f), "동쪽 전시");
            Display(collection, source, art, stone, new Vector3(-4.8f, 0, 26.5f), "서쪽 전시");
        }

        static void Display(Transform parent, GameObject source, Material art, Material stone, Vector3 at, string title)
        {
            var exhibit = new GameObject("대리석 흉상 · " + title).transform;
            exhibit.SetParent(parent, false);
            exhibit.localPosition = at;

            var plinth = GalleryConceptArchitecture.Block(exhibit, "Marble bust limestone plinth",
                Vector3.up * (PlinthHeight / 2), new Vector3(PlinthWidth, PlinthHeight, PlinthWidth), stone, .018f);
            var shape = plinth.gameObject.AddComponent<BoxCollider>();
            shape.size = new Vector3(PlinthWidth, PlinthHeight, PlinthWidth);
            plinth.gameObject.AddComponent<GalleryCaster>().shape = shape;

            var pivot = new GameObject("Original sculpture display pivot").transform;
            pivot.SetParent(exhibit, false);
            pivot.localPosition = Vector3.up * PlinthHeight;
            var normalized = new GameObject("Original FBX · normalized display scale").transform;
            normalized.SetParent(pivot, false);
            var model = UnityEngine.Object.Instantiate(source, normalized, false);
            model.name = "Marble Bust 01 · Rico Cilliers · CC0";
            // Preserve the imported FBX root transforms: they can carry its
            // authored unit and axis conversion, in addition to child geometry.
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("The marble bust FBX has no renderers.");
            Bounds combined = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                combined.Encapsulate(renderer.bounds);
                var slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < slots.Length; i++) slots[i] = art;
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            float height = combined.size.y;
            if (height <= .00001f || float.IsNaN(height) || float.IsInfinity(height))
                throw new InvalidOperationException("The marble bust FBX has invalid display bounds.");
            Vector3 bottom = normalized.InverseTransformPoint(new Vector3(combined.center.x, combined.min.y, combined.center.z));
            // First produce a unit-height model with its bottom centered at
            // zero, then apply the requested .85m exhibition height.
            normalized.localScale = Vector3.one / height;
            normalized.localPosition = -bottom / height;
            normalized.localScale *= BustHeight;
            normalized.localPosition *= BustHeight;
            // Imported model forward is +Z. Turn toward the incoming visitor
            // along the room's central aisle without rotating the label/plinth.
            pivot.localRotation = Quaternion.LookRotation(new Vector3(-at.x, 0, 17.5f - at.z), Vector3.up);

            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var collider = filter.GetComponent<MeshCollider>();
                if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }

            var labelObject = new GameObject("대리석 흉상 · 작품 및 제작자 표기");
            labelObject.transform.SetParent(exhibit, false);
            labelObject.transform.localPosition = new Vector3(0, .77f, -PlinthWidth / 2 - .012f);
            var label = labelObject.AddComponent<GalleryLabel>();
            label.caption = "대리석 흉상\n리코 실리어스 · Poly Haven\nCC0 · 외부 조각 모델";
            label.size = .043f;
            label.color = new Color(.19f, .17f, .14f);
        }
    }
}
