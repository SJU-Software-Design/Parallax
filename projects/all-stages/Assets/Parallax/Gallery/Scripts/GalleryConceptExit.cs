using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Migg's real Rodin scan is the exhibit. The game adds a stepped approach
    // and a view-dependent library window inside its existing negative space.
    public static class GalleryConceptExit
    {
        const string CollectionName = "Concept museum · Rodin cathedral exit alcove";
        public static readonly Vector3 Home = new Vector3(0, .006f, 52.15f);
        public const float InitialScale = .6f;

        public static GalleryOrcusExit Install(GalleryGame game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var installed = game.transform.Find(CollectionName);
            if (installed != null)
            {
                var existing = installed.GetComponentInChildren<GalleryOrcusExit>(true);
                if (existing == null) throw new InvalidOperationException("Cathedral collection has no entrance component.");
                return existing;
            }

            var collection = new GameObject(CollectionName).transform;
            collection.SetParent(game.transform, false);
            var stone = Stone("Cathedral · ivory travertine steps", new Color(.91f, .86f, .74f));
            stone.SetFloat("_ExhibitLight", .36f); stone.SetFloat("_AmbientStrength", 1.20f);
            stone.SetFloat("_NormalStrength", .40f);
            var pale = Stone("Cathedral · pale alcove moulding", new Color(.88f, .84f, .75f));
            var bronze = Surface("Concept exit alcove · aged bronze", "Bronze", new Color(.43f, .28f, .12f));
            var backing = Surface("Concept exit alcove · dark exhibition backing", "Paper", new Color(.12f, .19f, .17f));
            backing.SetFloat("_ExhibitLight", .16f);
            backing.SetColor("_EmissionColor", new Color(.015f, .026f, .022f));

            BuildAlcove(collection, stone, pale, bronze, backing);
            var prop = CreateSculpture(game, collection, stone, bronze);
            var exit = prop.GetComponent<GalleryOrcusExit>();
            prop.Remember();
            // This waiting barrier is not part of the carried exhibit's
            // authored placement volume. Cache those parts before adding it.
            var wait = new GameObject("Cathedral opening · wait for moving hands");
            wait.transform.SetParent(prop.transform, false);
            var entryBarrier = wait.AddComponent<BoxCollider>();
            entryBarrier.center = new Vector3(exit.EntryCenterX,
                (exit.EntryBottom + exit.EntryTop) * .5f, exit.EntryPlane);
            entryBarrier.size = new Vector3(GalleryCathedralGeometry.ComfortableOpenedOpening.size.x,
                exit.EntryTop - exit.EntryBottom, .04f);
            entryBarrier.enabled = false;
            exit.cathedralActivation.entryBarrier = entryBarrier;
            var props = game.props == null ? new List<GalleryProp>() : new List<GalleryProp>(game.props);
            if (!props.Contains(prop)) props.Add(prop);
            game.props = props.ToArray();
            game.orcusExit = exit;

            // Normal scene generation installs before GalleryGame.Awake. The
            // runtime branch also supports an already initialized play fixture.
            if (Application.isPlaying && game.player != null)
            {
                prop.InitializePhysics();
                prop.placement.SetPlayer(game.controller);
                prop.placement.ResetPlaced();
                exit.Entered += game.BeginOrcusTransition;
                game.lighting.Rebuild();
            }
            Label(collection, "성당 · 두 손 사이의 문\n오귀스트 로댕",
                new Vector3(0, 5.77f, 55.39f), .115f, new Color(.87f, .82f, .69f));
            Label(collection, "성당\n두 손 사이에는 다른 방이 있습니다.\n가까이에서 잡고, 먼 바닥을 향해\n조각을 키워 내려놓아 보세요.\n손이 열리면 계단을 올라 들어갑니다.",
                new Vector3(3.7f, 1.35f, 51.3f), .070f, new Color(.24f, .21f, .17f));
            Label(collection, "La Cathédrale · Auguste Rodin\n박물관 전시물 3D 스캔: Migg · CC BY 4.0",
                new Vector3(0, .77f, 55.41f), .058f, new Color(.75f, .73f, .66f));
            return exit;
        }

        static GalleryProp CreateSculpture(GalleryGame game, Transform collection, Material stepStone, Material bronze)
        {
            const string resource = "MuseumScans/Cathedral/";
            var mesh = Resources.Load<Mesh>(resource + "CathedralMesh");
            var aperture = Resources.Load<Mesh>(resource + "CathedralPortalMesh");
            var albedo = Resources.Load<Texture2D>(resource + "CathedralAlbedo");
            if (mesh == null || aperture == null || albedo == null)
                throw new InvalidOperationException("Import the real Cathedral scan before preparing the museum.");

            var root = new GameObject("04 · 성당 · 두 손 사이의 문");
            root.transform.SetParent(collection, false);
            root.transform.position = game.transform.TransformPoint(Home);
            root.transform.rotation = game.transform.rotation;
            root.transform.localScale = Vector3.one * InitialScale;
            var prop = root.AddComponent<GalleryProp>();
            prop.title = "04 · 성당 · 두 손 사이의 문"; prop.zone = 3; prop.minScale = .45f;
            var ivory = Surface("Cathedral · original scanned ivory", "Paper", new Color(1.05f, 1.03f, .98f));
            ivory.mainTexture = albedo; ivory.SetFloat("_UseSurfaceMaps", 0);
            ivory.SetFloat("_Roughness", .84f); ivory.SetFloat("_ReflectionStrength", .15f);
            ivory.SetFloat("_ExhibitLight", .20f); ivory.SetFloat("_AmbientStrength", 1.12f);
            var hands = Skinned(root.transform, "Rodin · original scanned hands", mesh, ivory);
            hands.shadowCastingMode = ShadowCastingMode.TwoSided; hands.receiveShadows = true;

            var boxes = GalleryCathedralGeometry.CollisionBoxes;
            var parts = new BoxCollider[boxes.Length];
            var closed = new Vector3[boxes.Length]; var opened = new Vector3[boxes.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                var go = new GameObject("Scanned hands · physical section " + i.ToString("D2"));
                go.transform.SetParent(root.transform, false);
                var box = go.AddComponent<BoxCollider>(); box.center = boxes[i].closedCenter; box.size = boxes[i].size;
                parts[i] = box; closed[i] = boxes[i].closedCenter;
                opened[i] = Vector3.LerpUnclamped(boxes[i].closedCenter, boxes[i].openedCenter,
                    GalleryCathedralGeometry.ComfortableOpeningMultiplier);
            }
            BuildApproach(root.transform, stepStone, bronze);

            var portalMaterial = new Material(Shader.Find("Parallax/MuseumLibraryPortal")) { name = "Cathedral · warm library window" };
            GalleryWorld.Materials.Add(portalMaterial);
            var membrane = Skinned(root.transform, "Two hands · live library opening", aperture, portalMaterial);
            membrane.shadowCastingMode = ShadowCastingMode.Off; membrane.receiveShadows = false;
            var exit = root.AddComponent<GalleryOrcusExit>();
            exit.game = game; exit.prop = prop; exit.portal = membrane; exit.windEnabled = false;
            // The real hand colliders determine what can physically pass.
            // This transition window must not demand pixel-perfect centring
            // after the controller has already negotiated the actual opening.
            exit.customOpening = true;
            exit.openingCenterX = GalleryCathedralGeometry.ComfortableOpenedOpening.center.x;
            exit.openingHalfWidth = GalleryCathedralGeometry.ComfortableOpenedOpening.extents.x;
            exit.openingBottom = GalleryCathedralGeometry.EntryFeetY;
            exit.openingTop = GalleryCathedralGeometry.EntryFeetY + 1.6f / GalleryCathedralGeometry.RequiredScale + .04f;
            exit.openingPlane = GalleryCathedralGeometry.PortalPlaneZ;
            exit.requiredScaleOverride = GalleryCathedralGeometry.RequiredScale;
            exit.entryFootOffset = .006f; exit.approachStart = -2.65f;
            var portal = root.AddComponent<GalleryCathedralPortal>();
            portal.Configure(game, prop, membrane, root.transform, GalleryCathedralGeometry.ClosedOpening);
            portal.handRenderers = new Renderer[] { hands };
            var activation = root.AddComponent<GalleryCathedralActivation>();
            activation.entrance = exit; activation.portal = portal;
            activation.openedMorphWeight = 100 * GalleryCathedralGeometry.ComfortableOpeningMultiplier;
            activation.morphTargets = new[] { hands, membrane };
            activation.movingParts = parts; activation.closedCenters = closed; activation.openedCenters = opened;
            activation.closedOpening = GalleryCathedralGeometry.ClosedOpening;
            activation.openedOpening = GalleryCathedralGeometry.ComfortableOpenedOpening;
            exit.cathedralActivation = activation;
            prop.Remember();
            return prop;
        }

        static SkinnedMeshRenderer Skinned(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.sharedMaterial = material;
            var bounds = mesh.bounds; bounds.Expand(.20f);
            // Imported bounds include the native 100% morph. Preserve that
            // margin while covering the wider runtime extrapolation as well.
            float extraTravel = GalleryCathedralGeometry.NativePalmTravel *
                Mathf.Max(0, GalleryCathedralGeometry.ComfortableOpeningMultiplier - 1);
            bounds.Expand(new Vector3(extraTravel * 2, 0, 0)); renderer.localBounds = bounds;
            renderer.updateWhenOffscreen = true;
            return renderer;
        }

        static void BuildApproach(Transform parent, Material material, Material bronze)
        {
            const int count = 16;
            const float front = -2.33f, run = .09f, width = .44f;
            for (int i = 0; i < count; i++)
            {
                float height = GalleryCathedralGeometry.EntryFeetY * (i + 1) / count;
                Step(parent, "Travertine tread " + (i + 1).ToString("D2"),
                    new Vector3(0, height * .5f, front + run * (i + .5f)), new Vector3(width, height, run + .006f), material);
                var inlay = GalleryConceptArchitecture.Block(parent, "Fine bronze tread inlay " + (i + 1).ToString("D2"),
                    new Vector3(0, height + .001f, front + run * i + .015f), new Vector3(width - .045f, .002f, .008f), bronze, .001f);
                inlay.shadowCastingMode = ShadowCastingMode.Off;
            }
            // Reach the top in front of the crossed wrists, then walk across
            // a level landing through the actual clear volume between the palms.
            Step(parent, "Cathedral threshold landing", new Vector3(0, GalleryCathedralGeometry.EntryFeetY * .5f, -.355f),
                new Vector3(width, GalleryCathedralGeometry.EntryFeetY, 1.07f), material);
        }

        static void Step(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            var renderer = GalleryConceptArchitecture.Block(parent, name, at, size, material, .007f);
            renderer.gameObject.AddComponent<BoxCollider>().size = size;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        }

        static Material Surface(string name, string category, Color tint)
        {
            var shader = Shader.Find("Parallax/MuseumSurface");
            if (shader == null) throw new InvalidOperationException("MuseumSurface shader is required for the exit alcove.");
            var material = new Material(shader) { name = name };
            GalleryMuseumMaterials.Apply(material, category);
            material.color = tint;
            GalleryWorld.Materials.Add(material);
            return material;
        }

        static Material Stone(string name, Color tint)
        {
            var material = Surface(name, "Stone", tint);
            // Keep the real stone normal/roughness while avoiding the floor
            // slab's printed tile joints across this temporary carved face.
            material.SetTexture("_SurfaceAlbedo", Texture2D.whiteTexture);
            material.SetFloat("_WorldTiling", 1.25f);
            material.SetFloat("_NormalStrength", .24f);
            material.SetFloat("_Roughness", .84f);
            material.SetFloat("_ReflectionStrength", .18f);
            material.SetFloat("_ExhibitLight", .045f);
            return material;
        }

        static void BuildAlcove(Transform parent, Material stone, Material pale, Material bronze, Material backing)
        {
            var alcove = new GameObject("Stone exhibition alcove · floor remains at zero").transform;
            alcove.SetParent(parent, false);
            alcove.localPosition = new Vector3(0, 0, 55.7f);
            var palette = new GalleryConceptArchitecture.Palette { stone = stone, plaster = pale, bronze = bronze, assets = GalleryWorld.Materials };
            GalleryConceptArchitecture.ArchPortal(alcove, Vector3.zero, Quaternion.identity, 5.8f, 2.9f, .24f, palette);
            GalleryConceptArchitecture.Block(alcove, "Recessed dark alcove backing", new Vector3(0, 2.90f, .08f),
                new Vector3(5.78f, 5.78f, .07f), backing, .025f);
            GalleryConceptArchitecture.Block(alcove, "Alcove upper stone cornice", new Vector3(0, 6.06f, -.025f),
                new Vector3(6.78f, .16f, .38f), pale, .025f);
            GalleryConceptArchitecture.Block(alcove, "Alcove fine bronze cornice", new Vector3(0, 5.94f, -.15f),
                new Vector3(6.5f, .028f, .055f), bronze, .009f);
            foreach (var renderer in alcove.GetComponentsInChildren<Renderer>())
            { renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; }
            // Decorative frame only: the existing museum end wall provides its
            // physical boundary. No raised central plinth obstructs the steps.
        }

        static void Label(Transform parent, string caption, Vector3 at, float size, Color color)
        {
            var go = new GameObject("Cathedral collection label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var label = go.AddComponent<GalleryLabel>();
            label.caption = caption; label.size = size; label.color = color;
        }
    }
}
