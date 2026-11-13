using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Opt-in, runtime-only insertion. The authored Gallery scene, its meshes and
    // lightmap assets remain untouched. Existing rear rooms keep their local
    // puzzle coordinates, and the original Nike doorway remains the entrance.
    public static class GalleryBottleBalanceLayout
    {
        public const float InsertAt = 29f;
        public const float Length = 17f;
        public const float AddedLength = Length;
        public const float HalfWidth = 11f;
        public const float EndZ = InsertAt + Length;
        public const float Roof = 6.6f;
        const string CollectionName = "Bottle balance · inserted exhibition architecture";
        public const string ReadableResourceFolder = "BottleBalance/ReadableArchitecture/";
        static readonly Vector3 Offset = Vector3.forward * AddedLength;
        static readonly string[] RearCollections =
        {
            "The portrait from one point",
            "Portrait gallery · opening curtain gate",
            "Portrait room · face references and safe wall fixtures",
            "Perspective gallery textile stage",
            "Concept museum · Rodin cathedral exit alcove"
        };

        public static void Extend(GalleryGame game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (!game.conceptLayout || game.transform.Find(CollectionName) != null) return;
            Physics.SyncTransforms();
            var originalChildren = Children(game.transform);
            var room = new GameObject(CollectionName).transform;
            room.SetParent(game.transform, false);
            var moved = new HashSet<Transform>();
            foreach (string name in RearCollections)
            {
                Transform collection = game.transform.Find(name);
                if (collection == null) continue;
                collection.position += Offset;
                moved.Add(collection);
            }
            foreach (Transform child in originalChildren)
            {
                if (moved.Contains(child) || child == game.controller.transform ||
                    child.GetComponent<GalleryMuseumShadowCourse>() != null ||
                    child.GetComponent<GalleryNikeLight>() != null ||
                    child.GetComponent<GalleryProp>() != null && child.GetComponent<GalleryProp>().zone <= 1)
                    continue;
                ShiftBranch(child);
            }
            if (game.props != null)
                foreach (GalleryProp prop in game.props)
                {
                    if (prop == null || prop.homePosition.z < InsertAt - .001f) continue;
                    prop.homePosition += Offset;
                    if (prop.body != null)
                    {
                        prop.body.position = prop.transform.position;
                        prop.body.rotation = prop.transform.rotation;
                    }
                }
            BuildInsertedRoom(game, room);
            Physics.SyncTransforms();
            Debug.Log("MUSEUM_BOTTLE_BALANCE_LAYOUT_OK insertion=" + InsertAt + " length=" + AddedLength +
                " originalSceneAndMeshesPreserved=1");
        }

        static Transform[] Children(Transform parent)
        {
            var result = new Transform[parent.childCount];
            for (int i = 0; i < result.Length; i++) result[i] = parent.GetChild(i);
            return result;
        }

        static bool KeepNikeBoundary(Transform node)
        {
            if (Mathf.Abs(node.position.z - InsertAt) > .41f) return false;
            string name = node.name;
            return name == "Museum room partition" || name.StartsWith("Curved doorway", StringComparison.Ordinal) ||
                name == "Arch sun occlusion proxy" || name == "Classical stone portal" ||
                name == "Layered museum wall finish";
        }

        static void ShiftBranch(Transform node)
        {
            if (KeepNikeBoundary(node)) return;
            var light = node.GetComponent<Light>();
            if (light != null && light.type == LightType.Directional) return;
            if (SubtreeBounds(node, out Bounds aggregate))
            {
                if (aggregate.min.z > InsertAt + .001f)
                {
                    node.position += Offset;
                    return;
                }
                if (aggregate.max.z < InsertAt - .001f) return;
            }
            else if (node.position.z > InsertAt + .001f)
            {
                node.position += Offset;
                return;
            }
            // Snapshot first: splitting a long architectural mesh creates a
            // shifted child, which must never be shifted for a second time.
            Transform[] children = Children(node);
            ShiftOwnGeometry(node);
            foreach (Transform child in children) ShiftBranch(child);
        }

        static bool SubtreeBounds(Transform node, out Bounds bounds)
        {
            bool found = false;
            bounds = new Bounds(node.position, Vector3.zero);
            foreach (Renderer renderer in node.GetComponentsInChildren<Renderer>(true))
                Include(ref bounds, ref found, renderer.bounds);
            foreach (Collider collider in node.GetComponentsInChildren<Collider>(true))
                if (collider.enabled) Include(ref bounds, ref found, collider.bounds);
            foreach (GalleryLabel label in node.GetComponentsInChildren<GalleryLabel>(true))
                Include(ref bounds, ref found, new Bounds(label.transform.position, Vector3.zero));
            foreach (ReflectionProbe probe in node.GetComponentsInChildren<ReflectionProbe>(true))
                Include(ref bounds, ref found, new Bounds(probe.transform.position, Vector3.zero));
            foreach (Light source in node.GetComponentsInChildren<Light>(true))
                if (source.type != LightType.Directional)
                    Include(ref bounds, ref found, new Bounds(source.transform.position, Vector3.zero));
            return found;
        }

        static void Include(ref Bounds bounds, ref bool found, Bounds addition)
        {
            if (!found) { bounds = addition; found = true; }
            else bounds.Encapsulate(addition);
        }

        static void ShiftOwnGeometry(Transform node)
        {
            var filter = node.GetComponent<MeshFilter>();
            var renderer = node.GetComponent<MeshRenderer>();
            Mesh original = filter != null ? filter.sharedMesh : null;
            Mesh front = null, rear = null;
            if (original != null && renderer != null && renderer.bounds.max.z >= InsertAt - .001f)
            {
                bool originalVisible = renderer.enabled;
                Mesh readable = ReadableSource(node, original);
                front = Clip(readable, node, false);
                rear = Clip(readable, node, true);
                filter.sharedMesh = front;
                if (front.vertexCount == 0) renderer.enabled = false;
                if (rear.vertexCount > 0)
                {
                    var extra = new GameObject(node.name + " · rear segment");
                    extra.transform.SetParent(node, false);
                    extra.AddComponent<MeshFilter>().sharedMesh = rear;
                    var rearRenderer = extra.AddComponent<MeshRenderer>();
                    rearRenderer.sharedMaterials = renderer.sharedMaterials;
                    rearRenderer.enabled = originalVisible;
                    rearRenderer.shadowCastingMode = renderer.shadowCastingMode;
                    rearRenderer.receiveShadows = renderer.receiveShadows;
                    rearRenderer.lightmapIndex = renderer.lightmapIndex;
                    rearRenderer.lightmapScaleOffset = renderer.lightmapScaleOffset;
                    rearRenderer.realtimeLightmapIndex = renderer.realtimeLightmapIndex;
                    rearRenderer.realtimeLightmapScaleOffset = renderer.realtimeLightmapScaleOffset;
                    rearRenderer.lightProbeUsage = renderer.lightProbeUsage;
                    rearRenderer.reflectionProbeUsage = renderer.reflectionProbeUsage;
                }
            }
            foreach (MeshCollider collider in node.GetComponents<MeshCollider>())
            {
                if (!collider.enabled || collider.bounds.max.z < InsertAt - .001f) continue;
                Mesh collisionSource = collider.sharedMesh;
                if (collisionSource == null) continue;
                Mesh readable = collisionSource == original && front != null ? null : ReadableSource(node, collisionSource);
                Mesh collisionFront = collisionSource == original && front != null ? front : Clip(readable, node, false);
                Mesh collisionRear = collisionSource == original && rear != null ? rear : Clip(readable, node, true);
                collider.sharedMesh = collisionFront.vertexCount > 0 ? collisionFront : null;
                if (collisionRear.vertexCount > 0)
                {
                    var extra = new GameObject(node.name + " · rear physical mesh");
                    extra.transform.SetParent(node, false);
                    var added = extra.AddComponent<MeshCollider>();
                    added.sharedMesh = collisionRear;
                    added.sharedMaterial = collider.sharedMaterial;
                    added.isTrigger = collider.isTrigger;
                    added.convex = collider.convex;
                }
            }
            foreach (BoxCollider collider in node.GetComponents<BoxCollider>())
                SplitBox(collider);
        }

        static Mesh ReadableSource(Transform owner, Mesh source)
        {
            string key = ReadableResourceKey(owner, source);
            Mesh readable = Resources.Load<Mesh>(ReadableResourceFolder + key);
            // Prefer the saved-scene snapshot even when a player build exposes
            // a readable, but optimized/combined, replacement mesh.
            if (readable != null && readable.isReadable) return readable;
            if (source.isReadable) return source;
            if (readable == null || !readable.isReadable)
                throw new InvalidOperationException("Readable architecture copy is missing for " + owner.name +
                    ". Run GalleryBottleBalanceReadableArchitecture.PrepareSavedScene before building. Resource=" + key +
                    " Identity=" + ReadableResourceIdentity(owner) + " PlayerMesh=" + source.name + " Vertices=" + source.vertexCount);
            return readable;
        }

        // Mesh names/counts/bounds may change during player mesh optimization or
        // static batching. Serialized hierarchy poses remain stable instead.
        public static string ReadableResourceKey(Transform owner, Mesh source)
        {
            string identity = ReadableResourceIdentity(owner);
            ulong hash = 14695981039346656037UL;
            unchecked { foreach (char letter in identity) { hash ^= letter; hash *= 1099511628211UL; } }
            return "architecture-" + hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        public static string ReadableResourceIdentity(Transform owner)
        {
            var names = new List<string>();
            for (Transform cursor = owner; cursor != null; cursor = cursor.parent)
            {
                Vector3 at = cursor.localPosition;
                names.Add(cursor.name + "@" + Mathf.RoundToInt(at.x * 1000) + "," +
                    Mathf.RoundToInt(at.y * 1000) + "," + Mathf.RoundToInt(at.z * 1000));
            }
            names.Reverse();
            return string.Join("/", names);
        }

        static void SplitBox(BoxCollider collider)
        {
            if (!collider.enabled || collider.bounds.max.z < InsertAt - .001f) return;
            Transform transform = collider.transform;
            Vector3 size = collider.size, center = collider.center;
            int axis = 0;
            float alignment = 0;
            for (int i = 0; i < 3; i++)
            {
                Vector3 direction = Vector3.zero; direction[i] = 1;
                float candidate = Mathf.Abs(Vector3.Dot(transform.TransformDirection(direction).normalized, Vector3.forward));
                if (candidate > alignment) { alignment = candidate; axis = i; }
            }
            if (alignment < .999f)
                throw new InvalidOperationException("Unexpected rotated box spans the room insertion: " + transform.name);
            Vector3 axisDirection = Vector3.zero; axisDirection[axis] = 1;
            float signedWorldScale = Vector3.Dot(transform.TransformVector(axisDirection), Vector3.forward);
            float minimum = center[axis] - size[axis] / 2, maximum = center[axis] + size[axis] / 2;
            float cut = transform.InverseTransformPoint(new Vector3(transform.position.x, transform.position.y, InsertAt))[axis];
            float frontMinimum = signedWorldScale > 0 ? minimum : Mathf.Max(minimum, cut);
            float frontMaximum = signedWorldScale > 0 ? Mathf.Min(maximum, cut) : maximum;
            float rearMinimum = signedWorldScale > 0 ? Mathf.Max(minimum, cut) : minimum;
            float rearMaximum = signedWorldScale > 0 ? maximum : Mathf.Min(maximum, cut);
            if (frontMaximum > frontMinimum + .00001f)
            {
                Vector3 frontSize = size, frontCenter = center;
                frontSize[axis] = frontMaximum - frontMinimum;
                frontCenter[axis] = (frontMinimum + frontMaximum) / 2;
                collider.size = frontSize; collider.center = frontCenter;
            }
            else collider.enabled = false;
            if (rearMaximum <= rearMinimum + .00001f) return;
            var extra = new GameObject(transform.name + " · rear box contact");
            extra.transform.SetParent(transform, false);
            var rear = extra.AddComponent<BoxCollider>();
            Vector3 rearSize = size, rearCenter = center;
            rearSize[axis] = rearMaximum - rearMinimum;
            rearCenter[axis] = (rearMinimum + rearMaximum) / 2;
            rear.center = rearCenter + transform.InverseTransformVector(Offset);
            rear.size = rearSize; rear.isTrigger = collider.isTrigger; rear.sharedMaterial = collider.sharedMaterial;
            var caster = transform.GetComponent<GalleryCaster>();
            if (caster != null && caster.shape == collider)
                extra.AddComponent<GalleryCaster>().shape = rear;
        }

        struct Vertex
        {
            public Vector3 point, normal;
            public Vector2 uv, uv2;
            public Vector4 tangent;
            public Color color;
            public static Vertex Lerp(Vertex a, Vertex b, float amount)
            {
                return new Vertex
                {
                    point = Vector3.LerpUnclamped(a.point, b.point, amount),
                    normal = Vector3.LerpUnclamped(a.normal, b.normal, amount).normalized,
                    uv = Vector2.LerpUnclamped(a.uv, b.uv, amount),
                    uv2 = Vector2.LerpUnclamped(a.uv2, b.uv2, amount),
                    tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, amount),
                    color = Color.LerpUnclamped(a.color, b.color, amount)
                };
            }
        }

        static Mesh Clip(Mesh source, Transform transform, bool rear)
        {
            Vector3[] sourcePositions = source.vertices, sourceNormals = source.normals;
            Vector2[] sourceUv = source.uv, sourceUv2 = source.uv2;
            Vector4[] sourceTangents = source.tangents;
            Color[] sourceColors = source.colors;
            var vertices = new Vertex[sourcePositions.Length];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vertex
                {
                    point = transform.TransformPoint(sourcePositions[i]),
                    normal = sourceNormals.Length == vertices.Length ? sourceNormals[i] : Vector3.up,
                    uv = sourceUv.Length == vertices.Length ? sourceUv[i] : Vector2.zero,
                    uv2 = sourceUv2.Length == vertices.Length ? sourceUv2[i] : Vector2.zero,
                    tangent = sourceTangents.Length == vertices.Length ? sourceTangents[i] : new Vector4(1, 0, 0, 1),
                    color = sourceColors.Length == vertices.Length ? sourceColors[i] : Color.white
                };
            var positions = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var uv2 = new List<Vector2>();
            var tangents = new List<Vector4>(); var colors = new List<Color>();
            var submeshes = new List<int>[source.subMeshCount];
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                submeshes[submesh] = new List<int>();
                int[] triangles = source.GetTriangles(submesh);
                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    var polygon = new List<Vertex>(4)
                    { vertices[triangles[triangle]], vertices[triangles[triangle + 1]], vertices[triangles[triangle + 2]] };
                    var clipped = new List<Vertex>(4);
                    for (int edge = 0; edge < polygon.Count; edge++)
                    {
                        Vertex a = polygon[edge], b = polygon[(edge + 1) % polygon.Count];
                        bool insideA = rear ? a.point.z >= InsertAt : a.point.z <= InsertAt;
                        bool insideB = rear ? b.point.z >= InsertAt : b.point.z <= InsertAt;
                        if (insideA) clipped.Add(a);
                        if (insideA != insideB)
                        {
                            float amount = (InsertAt - a.point.z) / (b.point.z - a.point.z);
                            clipped.Add(Vertex.Lerp(a, b, amount));
                        }
                    }
                    if (clipped.Count < 3) continue;
                    int first = positions.Count;
                    foreach (Vertex vertex in clipped)
                    {
                        positions.Add(transform.InverseTransformPoint(vertex.point + (rear ? Offset : Vector3.zero)));
                        normals.Add(vertex.normal); uv.Add(vertex.uv); uv2.Add(vertex.uv2);
                        tangents.Add(vertex.tangent); colors.Add(vertex.color);
                    }
                    for (int corner = 1; corner < clipped.Count - 1; corner++)
                        submeshes[submesh].AddRange(new[] { first, first + corner, first + corner + 1 });
                }
            }
            var mesh = new Mesh { name = source.name + (rear ? " · inserted rear copy" : " · insertion front copy") };
            if (positions.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2);
            mesh.SetTangents(tangents); mesh.SetColors(colors); mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        static GalleryConceptArchitecture.Palette Palette(GalleryGame game)
        {
            var palette = GalleryConceptArchitecture.MuseumPalette(GalleryWorld.Materials);
            var materials = new Dictionary<string, Material>();
            foreach (Renderer renderer in game.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && !materials.ContainsKey(material.name)) materials.Add(material.name, material);
            palette.plaster = Existing(materials, "Concept museum · ivory plaster", palette.plaster);
            palette.wall = Existing(materials, "Concept museum · warm grey gallery walls", palette.wall);
            palette.ceiling = Existing(materials, "Concept museum · softly lit ceiling", palette.ceiling);
            palette.stone = Existing(materials, "Concept museum · warm limestone", palette.stone);
            palette.floor = Existing(materials, "Concept museum · polished limestone floor", palette.floor);
            palette.bronze = Existing(materials, "Concept museum · patinated bronze", palette.bronze);
            palette.gold = Existing(materials, "Concept museum · frame gold", palette.gold);
            palette.wood = Existing(materials, "Concept museum · walnut", palette.wood);
            palette.paper = Existing(materials, "Concept museum · archival board", palette.paper);
            palette.ink = Existing(materials, "Concept museum · charcoal linen", palette.ink);
            return palette;
        }

        static Material Existing(Dictionary<string, Material> materials, string name, Material fallback)
        { return materials.TryGetValue(name, out Material found) ? found : fallback; }

        static Renderer PhysicalBlock(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            Renderer renderer = GalleryConceptArchitecture.Block(parent, name, at, size, material, .012f);
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            renderer.gameObject.AddComponent<BoxCollider>().size = size;
            // The inserted indoor room is already beneath an opaque ceiling;
            // its trim does not consume the 128 analytic gameplay-sun slots.
            return renderer;
        }

        static void BuildInsertedRoom(GalleryGame game, Transform room)
        {
            GalleryConceptArchitecture.Palette palette = Palette(game);
            float middle = InsertAt + Length / 2, exit = EndZ;
            PhysicalBlock(room, "Inserted gallery · continuous limestone floor", new Vector3(0, -.15f, middle),
                new Vector3(HalfWidth * 2, .30f, Length), palette.floor);
            PhysicalBlock(room, "Inserted gallery · opaque ceiling", new Vector3(0, 6.72f, middle),
                new Vector3(HalfWidth * 2, .24f, Length), palette.ceiling);
            for (int side = -1; side <= 1; side += 2)
            {
                PhysicalBlock(room, "Inserted gallery · outer side wall", new Vector3(side * (HalfWidth + .16f), Roof / 2, middle),
                    new Vector3(.32f, Roof, Length), palette.wall);
                GalleryConceptArchitecture.WallFinish(room, new Vector3(side * (HalfWidth - .12f), 0, middle),
                    Quaternion.Euler(0, side * 90, 0), Length - .4f, Roof, palette);
                foreach (float z in new[] { InsertAt + .65f, middle, exit - .65f })
                    GalleryConceptArchitecture.ClassicalColumn(room, new Vector3(side * (HalfWidth - .52f), 0, z), 6.15f, .31f, palette);
                // Existing Nike walls end at X +/-8. Close only the new wings
                // here; keep the authored Nike partition and door unchanged.
                float entranceWing = HalfWidth - 8f;
                PhysicalBlock(room, "Balance gallery · widened entrance return", new Vector3(side * (8f + entranceWing / 2), Roof / 2, InsertAt),
                    new Vector3(entranceWing, Roof, .32f), palette.wall);
                float finishCenter = (HalfWidth + 1.9f) / 2, finishWidth = HalfWidth - 2.1f;
                GalleryConceptArchitecture.WallFinish(room, new Vector3(side * finishCenter, 0, InsertAt + .17f),
                    Quaternion.Euler(0, 180, 0), finishWidth, Roof, palette);
                PhysicalBlock(room, "Balance gallery · exit partition", new Vector3(side * (HalfWidth + 1.8f) / 2, Roof / 2, exit),
                    new Vector3(HalfWidth - 1.8f, Roof, .32f), palette.wall);
                GalleryConceptArchitecture.WallFinish(room, new Vector3(side * finishCenter, 0, exit - .17f),
                    Quaternion.identity, finishWidth, Roof, palette);
            }
            // Clone only the existing boundary's curved physical header, not
            // its surrounding scene hierarchy or its serialized puzzle data.
            foreach (Transform child in Children(game.transform))
                if (child.name.StartsWith("Curved doorway", StringComparison.Ordinal) && Mathf.Abs(child.position.z - InsertAt) < .05f)
                {
                    var header = UnityEngine.Object.Instantiate(child.gameObject, room);
                    header.name = "Balance gallery · curved exit physical header";
                    header.transform.position = child.position + Offset;
                    break;
                }
            GalleryConceptArchitecture.ArchPortal(room, new Vector3(0, 0, exit - .24f), Quaternion.identity,
                3.6f, 3.9f, .30f, palette);
            // The four approved original works, matching wall fixtures and
            // actual room reflection are confined to this inserted collection.
            GalleryBottleBalanceEnvironment.Install(game, room, palette);
        }

    }
}
