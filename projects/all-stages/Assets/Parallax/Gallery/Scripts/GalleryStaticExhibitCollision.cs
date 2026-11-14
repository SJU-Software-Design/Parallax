using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Scenery uses its own surface, never a box around an entire collection.
    // The authored interaction/door/plant/bench colliders remain authoritative.
    public sealed class GalleryStaticExhibitCollision : MonoBehaviour
    {
        public const string ResourceFolder = "MuseumCollision/";
        public readonly List<MeshCollider> Added = new List<MeshCollider>();
        public readonly List<string> Missing = new List<string>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        public int Considered { get; private set; }

        public static GalleryStaticExhibitCollision Install(GalleryGame game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var existing = game.GetComponent<GalleryStaticExhibitCollision>();
            if (existing != null) return existing;
            var installed = game.gameObject.AddComponent<GalleryStaticExhibitCollision>();
            installed.AddStatueSurface(game);
            foreach (var filter in game.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!Candidate(game, filter)) continue;
                installed.Considered++;
                Mesh mesh = CollisionMesh(game, filter);
                if (mesh == null)
                {
                    installed.Missing.Add(Path(game.transform, filter.transform));
                    continue;
                }
                installed.AddSurface(filter.gameObject, mesh);
            }
            Physics.SyncTransforms();
            if (installed.Missing.Count > 0)
                Debug.LogError("MUSEUM_STATIC_COLLISION_MISSING readable meshes=" + string.Join("; ", installed.Missing));
            Debug.Log("MUSEUM_STATIC_COLLISION_INSTALLED exactSurfaces=" + installed.Added.Count +
                " missing=" + installed.Missing.Count + " authoredAndInteractiveCollidersPreserved=1");
            return installed;
        }

        public static bool Candidate(GalleryGame game, MeshFilter filter)
        {
            if (game == null || filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount < 3)
                return false;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy) return false;
            if (!filter.transform.IsChildOf(game.transform)) return false;
            // Human reach, including a jump, rather than hundreds of roof screws.
            if (renderer.bounds.min.y > game.transform.position.y + 3.6f ||
                renderer.bounds.max.y < game.transform.position.y + .065f) return false;
            Vector3 size = renderer.bounds.size;
            if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < .08f) return false;
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null) continue;
                string shader = material.shader.name.ToLowerInvariant();
                if (material.renderQueue >= 3000 || shader.Contains("portal") || shader.Contains("glass") ||
                    shader.Contains("mirror") || shader.Contains("worldtext") || shader.Contains("beam") ||
                    shader.Contains("foliage") || shader.Contains("reflection")) return false;
            }
            for (Transform at = filter.transform; at != null && at != game.transform; at = at.parent)
            {
                if (at.gameObject.layer == GalleryCathedralPortal.PreviewLayer ||
                    at.GetComponent<GalleryProp>() != null || at.GetComponent<Rigidbody>() != null ||
                    at.GetComponent<CharacterController>() != null || at.GetComponent<GalleryMirror>() != null ||
                    at.GetComponent<GalleryPortraitGate>() != null || at.GetComponent<GalleryLabel>() != null ||
                    at.GetComponent<TextMesh>() != null) return false;
                string name = at.name;
                // This 12 mm engraved gauge mark is a visual alignment cue,
                // paired with the moving needle. The plate remains physical;
                // turning the hairline mark into a separate tiny cooked solid
                // adds a snag/query target and can collapse during mesh welding.
                if (name == "Fixed central reference notch") return false;
                // These compound colliders already match the physical exhibit.
                if (name == "Museum walnut bench" || name == "Fluted limestone column" ||
                    name == "Concept museum · marble bust collection" ||
                    name == "Concept museum · real potted plants" ||
                    name == "Balance exhibition · arched walnut double door" ||
                    name.StartsWith("Nike arched walnut door", StringComparison.Ordinal) ||
                    name.StartsWith("Dike · original static body", StringComparison.Ordinal) ||
                    name.StartsWith("Static collision ·", StringComparison.Ordinal) ||
                    name == "Balance · rotating horizontal beam" || name == "Balance · left fixed weight pan" ||
                    name == "Balance · right artwork pan" || name == "Dike · live balance needle" ||
                    name == "Fixed spotlight head collision envelope" || name == "Spotlight head") return false;
            }
            // Trigger-only geometry is intentional query/effect geometry.
            foreach (var collider in filter.GetComponents<Collider>())
                if (collider.isTrigger || collider.enabled) return false;
            // A simple collider on an ancestor can cover a decorative child;
            // test the child itself, not all the collection's combined bounds.
            for (Transform at = filter.transform.parent; at != null && at != game.transform; at = at.parent)
                foreach (var box in at.GetComponents<BoxCollider>())
                    if (box.enabled && !box.isTrigger && Covers(box, renderer.bounds)) return false;
            return true;
        }

        static bool Covers(BoxCollider box, Bounds bounds)
        {
            Vector3 half = box.size * .5f + Vector3.one * .012f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 world = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = box.transform.InverseTransformPoint(world) - box.center;
                if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z) return false;
            }
            return true;
        }

        static Mesh CollisionMesh(GalleryGame game, MeshFilter filter)
        {
            var renderer = filter.GetComponent<Renderer>();
            // Insertion may split a source mesh into new, readable front/rear
            // surfaces. Use those current surfaces instead of a pre-insertion copy.
            if (filter.sharedMesh.isReadable && (renderer == null || !renderer.isPartOfStaticBatch))
                return filter.sharedMesh;
            return Resources.Load<Mesh>(ResourceFolder + ResourceKey(game, filter.transform));
        }

        void AddSurface(GameObject owner, Mesh mesh)
        {
            var shape = owner.AddComponent<MeshCollider>();
            shape.convex = false;
            shape.sharedMesh = mesh;
            Added.Add(shape);
        }

        void AddStatueSurface(GalleryGame game)
        {
            var course = game.GetComponent<GalleryBottleBalanceCourse>();
            var exhibit = course != null && course.Art != null ? course.Art.dikeExhibit : null;
            if (exhibit == null || exhibit.StatueRoot == null || !exhibit.Ready) return;
            Transform statue = exhibit.StatueRoot;
            var filter = statue.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
            { Missing.Add("Dike original body · readable collision source"); return; }
            var source = filter.sharedMesh;
            Vector3[] vertices = source.vertices;
            Bounds swordClearance = new Bounds();
            bool clearance = false;
            if (course.Art.verifiedRewardSword != null)
                foreach (var renderer in course.Art.verifiedRewardSword.GetComponentsInChildren<Renderer>(true))
                { if (!clearance) { swordClearance = renderer.bounds; clearance = true; } else swordClearance.Encapsulate(renderer.bounds); }
            if (clearance)
            {
                // Leave the sword's entire straight drop to its tray open, as
                // well as the initially overlapping stone fingers at the grip.
                swordClearance.Expand(new Vector3(.20f, .24f, .20f));
                Vector3 min = swordClearance.min; min.y = game.transform.position.y + .25f;
                swordClearance.SetMinMax(min, swordClearance.max);
            }
            var triangles = new List<int>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] indices = source.GetTriangles(sub);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = statue.TransformPoint(vertices[indices[i]]),
                        b = statue.TransformPoint(vertices[indices[i + 1]]), c = statue.TransformPoint(vertices[indices[i + 2]]);
                    var face = new Bounds(a, Vector3.zero); face.Encapsulate(b); face.Encapsulate(c);
                    if (clearance && face.Intersects(swordClearance)) continue;
                    triangles.Add(indices[i]); triangles.Add(indices[i + 1]); triangles.Add(indices[i + 2]);
                }
            }
            var mesh = new Mesh { name = "Dike clothing and body · exact surface with sword drop clearance", indexFormat = source.indexFormat };
            mesh.vertices = vertices; mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            ownedMeshes.Add(mesh);
            var contact = new GameObject("Static collision · Dike body excluding sword clearance");
            contact.transform.SetParent(statue, false);
            AddSurface(contact, mesh);
        }

        public static string ResourceKey(GalleryGame game, Transform owner)
        {
            string identity = Path(game.transform, owner);
            ulong hash = 14695981039346656037UL;
            unchecked { foreach (char value in identity) { hash ^= value; hash *= 1099511628211UL; } }
            return "surface-" + hash.ToString("x16");
        }

        public static string Path(Transform root, Transform owner)
        {
            var parts = new List<string>();
            for (Transform at = owner; at != null && at != root; at = at.parent)
                parts.Add(at.name + "[" + at.GetSiblingIndex() + "]");
            parts.Reverse(); return string.Join("/", parts);
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
