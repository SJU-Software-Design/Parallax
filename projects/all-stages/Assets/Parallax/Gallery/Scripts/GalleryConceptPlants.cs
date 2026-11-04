using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Original CC0 Poly Haven FBX; every instance retains its authored hierarchy.
    public static class GalleryConceptPlants
    {
        const string CollectionName = "Concept museum · real potted plants";
        const string ResourceRoot = "MuseumScans/PottedPlant/";
        const float DisplayHeight = 1.8f;

        public static void Install(GalleryGame game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (game.transform.Find(CollectionName) != null) return;
            var source = Resources.Load<GameObject>(ResourceRoot + "potted_plant_02_2k");
            var leafMap = Resources.Load<Texture2D>(ResourceRoot + "textures/potted_plant_02_leaves_diff_1k");
            var potMap = Resources.Load<Texture2D>(ResourceRoot + "textures/potted_plant_02_pot_diff_2k");
            var shader = Shader.Find("Standard");
            var foliageShader=Shader.Find("Parallax/MuseumFoliage");
            if (source == null || leafMap == null || potMap == null || shader == null||foliageShader==null)
                throw new InvalidOperationException("Original Potted Plant 02 FBX, alpha texture and Standard shader are required.");

            var leaves = new Material(foliageShader) { name = "Concept plant · original RGBA cutout leaves", color = Color.white };
            leaves.mainTexture = leafMap;
            leaves.SetFloat("_Metallic", 0);
            leaves.SetFloat("_Glossiness", .18f);
            leaves.SetFloat("_Mode", 1);
            leaves.SetFloat("_Cutoff", .4f);
            leaves.SetInt("_SrcBlend", (int)BlendMode.One);
            leaves.SetInt("_DstBlend", (int)BlendMode.Zero);
            leaves.SetInt("_ZWrite", 1);
            // Standard variants that expose culling can draw both sides;
            // native shadow casting is two-sided on every leaf renderer.
            if (leaves.HasProperty("_Cull")) leaves.SetInt("_Cull", (int)CullMode.Off);
            leaves.SetOverrideTag("RenderType", "TransparentCutout");
            leaves.EnableKeyword("_ALPHATEST_ON");
            leaves.DisableKeyword("_ALPHABLEND_ON");
            leaves.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            leaves.renderQueue = (int)RenderQueue.AlphaTest;
            leaves.doubleSidedGI = true;

            var pot = new Material(shader) { name = "Concept plant · original terracotta and soil UV", color = Color.white };
            pot.mainTexture = potMap;
            pot.SetFloat("_Mode", 0);
            pot.SetFloat("_Metallic", 0);
            pot.SetFloat("_Glossiness", .16f);
            pot.SetInt("_ZWrite", 1);
            pot.SetOverrideTag("RenderType", "Opaque");
            GalleryWorld.Materials.Add(leaves);
            GalleryWorld.Materials.Add(pot);

            var collection = new GameObject(CollectionName).transform;
            collection.SetParent(game.transform, false);
            Display(collection, source, leaves, pot, new Vector3(-6.4f, 0, 1.5f), 20);
            Display(collection, source, leaves, pot, new Vector3(6.4f, 0, 1.5f), 110);
            Display(collection, source, leaves, pot, new Vector3(6.6f, 0, 27.1f), 55);
            Display(collection, source, leaves, pot, new Vector3(6.4f, 0, 53.2f), -25);
        }

        static bool IsLeaf(Material source)
        { return source != null && source.name.IndexOf("leaves", StringComparison.OrdinalIgnoreCase) >= 0; }

        static void Display(Transform collection, GameObject source, Material leaves, Material pot, Vector3 at, float yaw)
        {
            var display = new GameObject("Potted Plant 02 · Rico Cilliers · CC0").transform;
            display.SetParent(collection, false);
            display.localPosition = at;
            var normalized = new GameObject("Original plant · display normalization").transform;
            normalized.SetParent(display, false);
            var instance = UnityEngine.Object.Instantiate(source, normalized, false);
            instance.name = "Original potted_plant_02 hierarchy";
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Potted Plant 02 has no renderers.");
            Bounds combined = renderers[0].bounds;
            bool[] potOnly = new bool[renderers.Length];
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                combined.Encapsulate(renderer.bounds);
                var originals = renderer.sharedMaterials;
                var assigned = new Material[Mathf.Max(1, originals.Length)];
                bool anyLeaf = false;
                for (int slot = 0; slot < assigned.Length; slot++)
                {
                    bool leaf = slot < originals.Length && IsLeaf(originals[slot]);
                    anyLeaf |= leaf;
                    assigned[slot] = leaf ? leaves : pot;
                }
                potOnly[r] = !anyLeaf;
                renderer.sharedMaterials = assigned;
                renderer.shadowCastingMode = anyLeaf ? ShadowCastingMode.TwoSided : ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            float height = combined.size.y;
            if (height <= .00001f || float.IsNaN(height) || float.IsInfinity(height))
                throw new InvalidOperationException("Potted Plant 02 has invalid bounds.");
            Vector3 bottom = normalized.InverseTransformPoint(new Vector3(combined.center.x, combined.min.y, combined.center.z));
            float scale = DisplayHeight / height;
            normalized.localScale = Vector3.one * scale;
            normalized.localPosition = -bottom * scale;

            // Keep foliage freely walkable: only the small terracotta base is
            // physical. Prefer original opaque-renderer bounds when separate.
            bool foundPot = false;
            Bounds potBounds = new Bounds();
            for (int r = 0; r < renderers.Length; r++)
            {
                if (!potOnly[r]) continue;
                if (!foundPot) { potBounds = renderers[r].bounds; foundPot = true; }
                else potBounds.Encapsulate(renderers[r].bounds);
            }
            var collision = new GameObject("Terracotta pot contact only").transform;
            collision.SetParent(display, false);
            var shape = collision.gameObject.AddComponent<BoxCollider>();
            if (foundPot && potBounds.size.y > .02f && potBounds.size.y < DisplayHeight * .52f)
            {
                collision.localPosition = display.InverseTransformPoint(potBounds.center);
                shape.size = new Vector3(potBounds.size.x * .92f, potBounds.size.y, potBounds.size.z * .92f);
            }
            else
            {
                collision.localPosition = Vector3.up * .24f;
                shape.size = new Vector3(.50f, .48f, .50f);
            }
            display.localRotation = Quaternion.Euler(0, yaw, 0);
        }
    }
}
