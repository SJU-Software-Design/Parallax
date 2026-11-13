using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Only the optional inserted gallery owns these artworks, fixtures and
    // reflection. The authored Nike and portrait collections are not edited.
    public sealed class GalleryBottleBalanceEnvironment : MonoBehaviour
    {
        const string Collection = "Balance gallery · knot and weighing collection";
        public const string ArtResourceFolder = "MuseumBalanceArt/";
        public ReflectionProbe RoomProbe { get; private set; }
        public bool CaptureReady { get; private set; }
        public int ArtworkCount { get; private set; }
        GalleryGame game;
        RenderTexture environment;
        readonly Vector4[] positions = new Vector4[8];
        readonly Vector4[] colours = new Vector4[8];
        readonly Vector4[] directions = new Vector4[8];
        int lightCount;

        public static GalleryBottleBalanceEnvironment Install(GalleryGame game, Transform room,
            GalleryConceptArchitecture.Palette palette = null)
        {
            if (game == null || room == null) throw new ArgumentNullException("An inserted museum room is required.");
            var existing = room.GetComponent<GalleryBottleBalanceEnvironment>();
            if (existing != null) return existing;
            var owner = room.gameObject.AddComponent<GalleryBottleBalanceEnvironment>();
            owner.game = game;
            owner.Build(room, palette ?? GalleryConceptArchitecture.MuseumPalette(GalleryWorld.Materials));
            return owner;
        }

        void Awake()
        {
            Shader.SetGlobalFloat("_MuseumBalanceProbeReady", 0);
            Shader.SetGlobalInt("_MuseumBalanceLightCount", 0);
        }

        static Transform Group(Transform parent, string name, Vector3 at, Quaternion turn)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false); root.localPosition = at; root.localRotation = turn;
            return root;
        }

        void Build(Transform room, GalleryConceptArchitecture.Palette p)
        {
            Transform root = Group(room, Collection, Vector3.zero, Quaternion.identity);
            float frontArtZ = GalleryBottleBalanceLayout.InsertAt + 5;
            float rearArtZ = GalleryBottleBalanceLayout.EndZ - 6;
            Artwork(root, p, "DurerFirst", "첫 번째 매듭", "알브레히트 뒤러 · 1521년 이전 · The Met", -1, frontArtZ);
            Artwork(root, p, "DurerSixth", "여섯 번째 매듭", "알브레히트 뒤러 · 1521년 이전 · The Met", -1, rearArtZ);
            Artwork(root, p, "VermeerBalance", "저울을 든 여인", "요하네스 베르메르 · 약 1664 · NGA", 1, frontArtZ);
            Artwork(root, p, "ChristusGoldsmith", "가게 안의 금세공인", "페트뤼스 크리스튀스 · 1449 · The Met", 1, rearArtZ);

            // Slim overhead rails and fixtures reuse the museum's charcoal,
            // limestone and brass vocabulary, leaving both floor work areas clear.
            float frontRailZ = GalleryDikeGalleryLayout.FrontLightingZ;
            float rearRailZ = GalleryDikeGalleryLayout.RearLightingZ;
            foreach (float z in new[] { frontRailZ, rearRailZ })
            {
                GalleryConceptArchitecture.Block(root, "Balance gallery · suspended lighting rail", new Vector3(0, 6.08f, z),
                    new Vector3(GalleryBottleBalanceLayout.HalfWidth * 2 - 4, .055f, .085f), p.ink, .01f);
                foreach (int side in new[] { -1, 1 })
                    GalleryConceptArchitecture.Block(root, "Lighting rail · ceiling suspension", new Vector3(side * GalleryBottleBalanceLayout.HalfWidth * .625f, 6.34f, z),
                        new Vector3(.035f, .50f, .035f), p.ink, .006f);
            }
            Vector3 bottleAt=GalleryDikeGalleryLayout.BottleStation;
            Vector3 scaleAt=GalleryDikeGalleryLayout.ScaleStation;
            Spot(root, p, "Bottle exhibit · front warm spotlight", new Vector3(bottleAt.x, 5.8f, frontRailZ), bottleAt+Vector3.up*1.4f, 13, 7, 78);
            Spot(root, p, "Balance exhibit · front warm spotlight", new Vector3(scaleAt.x, 5.8f, frontRailZ), scaleAt+Vector3.up*1.5f, 13, 8, 78);
            Spot(root, p, "Bottle exhibit · rear warm spotlight", new Vector3(bottleAt.x, 5.8f, rearRailZ), bottleAt+Vector3.up*1.4f, 13, 5, 78);
            Spot(root, p, "Balance exhibit · rear warm spotlight", new Vector3(scaleAt.x, 5.8f, rearRailZ), scaleAt+Vector3.up*1.5f, 13, 5.5f, 78);
            PublishLights();

            Transform probeRoot = Group(root, "Balance gallery · actual room reflection", new Vector3(0, GalleryBottleBalanceLayout.Roof / 2,
                GalleryBottleBalanceLayout.InsertAt + GalleryBottleBalanceLayout.Length / 2), Quaternion.identity);
            RoomProbe = probeRoot.gameObject.AddComponent<ReflectionProbe>();
            RoomProbe.mode = ReflectionProbeMode.Realtime;
            RoomProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            RoomProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            RoomProbe.renderDynamicObjects = true; RoomProbe.resolution = 256; RoomProbe.hdr = true;
            RoomProbe.boxProjection = true;
            RoomProbe.size = new Vector3(GalleryBottleBalanceLayout.HalfWidth * 2, GalleryBottleBalanceLayout.Roof, GalleryBottleBalanceLayout.Length);
            RoomProbe.blendDistance = .3f; RoomProbe.importance = 20;
            RoomProbe.cullingMask = game.view.cullingMask;
            Debug.Log("MUSEUM_BALANCE_ENVIRONMENT_OK artworks=" + ArtworkCount + " lights=" + lightCount +
                " width=" + GalleryBottleBalanceLayout.HalfWidth * 2 + " depth=" + GalleryBottleBalanceLayout.Length +
                " height=" + GalleryBottleBalanceLayout.Roof);
        }

        void Artwork(Transform parent, GalleryConceptArchitecture.Palette p, string key, string title, string credit, int side, float z)
        {
            Texture2D texture = Resources.Load<Texture2D>(ArtResourceFolder + key);
            if (texture == null) throw new InvalidOperationException("Approved balance gallery artwork is missing: " + ArtResourceFolder + key);
            // Respect each full original image's aspect ratio, including paper
            // borders; do not crop knot roundels or the painted balance.
            float ratio = (float)texture.width / texture.height;
            float height = Mathf.Min(2.8f, 3.15f / ratio), width = height * ratio;
            var painting = Group(parent, "Balance artwork · " + key + " · " + title,
                new Vector3(side * (GalleryBottleBalanceLayout.HalfWidth - .22f), 2.9f, z), Quaternion.Euler(0, side * 90, 0));
            GalleryConceptArchitecture.Block(painting, "Canvas stretcher", Vector3.zero, new Vector3(width + .035f, height + .035f, .075f), p.paper, .012f);
            var material = new Material(Shader.Find("Parallax/MuseumSurface")) { name = "Balance original artwork · " + key, color = Color.white };
            GalleryMuseumMaterials.Apply(material, "Artwork"); material.mainTexture = texture;
            GalleryWorld.Materials.Add(material);
            var canvas = new GameObject("Original collection image · " + key); canvas.transform.SetParent(painting, false);
            var mesh = new Mesh { name = "Balance full artwork · " + key };
            mesh.vertices = new[] { new Vector3(-width / 2, -height / 2, -.043f), new Vector3(width / 2, -height / 2, -.043f),
                new Vector3(width / 2, height / 2, -.043f), new Vector3(-width / 2, height / 2, -.043f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            canvas.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = canvas.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            GalleryConceptArchitecture.PictureFrame(painting, new Vector2(width + .035f, height + .035f), p);
            GalleryConceptArchitecture.Block(painting, "Artwork catalogue plaque", new Vector3(0, -height / 2 - .38f, .015f),
                new Vector3(2.1f, .30f, .035f), p.paper, .008f);
            Label(painting, title, new Vector3(0, -height / 2 - .33f, -.012f), .072f);
            Label(painting, credit, new Vector3(0, -height / 2 - .44f, -.012f), .040f);

            float fixtureHeight = height / 2 + .33f;
            GalleryConceptArchitecture.Block(painting, "Picture light · brass hood", new Vector3(0, fixtureHeight, -.24f),
                new Vector3(Mathf.Min(1.45f, width * .65f), .10f, .26f), p.bronze, .025f);
            foreach (int bracketSide in new[] { -1, 1 })
                GalleryConceptArchitecture.Block(painting, "Picture light · wall bracket", new Vector3(bracketSide * .45f, fixtureHeight + .08f, -.10f),
                    new Vector3(.045f, .20f, .25f), p.bronze, .012f);
            var diffuser = new Material(p.paper) { name = "Balance picture light · warm diffuser" };
            diffuser.SetColor("_EmissionColor", new Color(.55f, .42f, .23f)); GalleryWorld.Materials.Add(diffuser);
            GalleryConceptArchitecture.Block(painting, "Picture light · warm diffuser", new Vector3(0, fixtureHeight - .055f, -.27f),
                new Vector3(Mathf.Min(1.3f, width * .59f), .018f, .17f), diffuser, .006f);
            // The actual light is slightly in front of the hood to illuminate
            // the whole work, while the hood remains the visible fixture.
            Vector3 at = painting.TransformPoint(new Vector3(0, fixtureHeight - .04f, -.42f));
            AddLight(at, painting.TransformPoint(new Vector3(0, -.10f, -.05f)), 7, 7, 106);
            ArtworkCount++;
        }

        static void Label(Transform parent, string caption, Vector3 at, float size)
        {
            Transform labelRoot = Group(parent, "Balance wall label · " + caption, at, Quaternion.identity);
            var label = labelRoot.gameObject.AddComponent<GalleryLabel>(); label.caption = caption; label.size = size;
            label.color = new Color(.21f, .18f, .12f);
        }

        void Spot(Transform parent, GalleryConceptArchitecture.Palette p, string name, Vector3 at, Vector3 target, float range, float intensity, float angle)
        {
            var fixture = Group(parent, name, at, Quaternion.LookRotation(target - at));
            GalleryConceptArchitecture.Block(fixture, "Spotlight · bronze yoke", new Vector3(0, .10f, 0), new Vector3(.26f, .14f, .12f), p.bronze, .018f);
            GalleryConceptArchitecture.Block(fixture, "Spotlight · charcoal housing", Vector3.zero, new Vector3(.21f, .21f, .32f), p.ink, .035f);
            GalleryConceptArchitecture.Block(fixture, "Spotlight · warm lens", new Vector3(0, 0, .17f), new Vector3(.15f, .15f, .025f), p.paper, .025f);
            AddLight(at, target, range, intensity, angle);
        }

        void AddLight(Vector3 at, Vector3 target, float range, float intensity, float angle)
        {
            if (lightCount >= positions.Length) throw new InvalidOperationException("Balance gallery light array exceeded its shader capacity.");
            Vector3 direction = (target - at).normalized;
            positions[lightCount] = new Vector4(at.x, at.y, at.z, range);
            colours[lightCount] = new Vector4(1, .88f, .70f, intensity);
            directions[lightCount] = new Vector4(direction.x, direction.y, direction.z, Mathf.Cos(angle * .5f * Mathf.Deg2Rad));
            lightCount++;
        }

        void PublishLights()
        {
            Shader.SetGlobalVectorArray("_MuseumBalanceLightPositionRange", positions);
            Shader.SetGlobalVectorArray("_MuseumBalanceLightColorIntensity", colours);
            Shader.SetGlobalVectorArray("_MuseumBalanceLightDirectionCone", directions);
            Shader.SetGlobalInt("_MuseumBalanceLightCount", lightCount);
        }

        IEnumerator Start()
        {
            // Let existing museum globals and all new interactive exhibits
            // finish initialization before capturing this real environment.
            yield return null; yield return null; yield return null;
            if (RoomProbe == null || game == null || game.view == null) yield break;
            var captureObject = new GameObject("Balance gallery · reflection capture");
            var capture = captureObject.AddComponent<Camera>(); capture.enabled = false;
            capture.allowHDR = true; capture.allowMSAA = false; capture.clearFlags = CameraClearFlags.Skybox;
            capture.renderingPath = RenderingPath.Forward; capture.nearClipPlane = .12f; capture.farClipPlane = 65;
            capture.cullingMask = game.view.cullingMask; capture.transform.position = RoomProbe.transform.position;
            environment = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            { name = "Balance gallery · actual HDR environment", dimension = TextureDimension.Cube, useMipMap = true, autoGenerateMips = false };
            try
            {
                environment.Create();
                if (!capture.RenderToCubemap(environment)) throw new InvalidOperationException("Cannot capture inserted balance gallery reflection.");
                environment.GenerateMips();
                RoomProbe.mode = ReflectionProbeMode.Custom; RoomProbe.customBakedTexture = environment;
                Vector3 at = RoomProbe.transform.position;
                Shader.SetGlobalTexture("_MuseumBalanceEnvironment", environment);
                Shader.SetGlobalVector("_MuseumBalanceEnvironmentHDR", new Vector4(1, 1, 0, 0));
                Shader.SetGlobalVector("_MuseumBalanceProbePosition", new Vector4(at.x, at.y, at.z, 1));
                Shader.SetGlobalVector("_MuseumBalanceProbeBoxMin", RoomProbe.bounds.min);
                Shader.SetGlobalVector("_MuseumBalanceProbeBoxMax", RoomProbe.bounds.max);
                Shader.SetGlobalFloat("_MuseumBalanceProbeReady", 1); CaptureReady = true;
                Debug.Log("MUSEUM_BALANCE_REFLECTION_OK originalArtworks=4 width=" + GalleryBottleBalanceLayout.HalfWidth * 2 +
                    " depth=" + GalleryBottleBalanceLayout.Length);
            }
            finally { Destroy(captureObject); }
        }

        void OnDestroy()
        {
            Shader.SetGlobalFloat("_MuseumBalanceProbeReady", 0);
            Shader.SetGlobalInt("_MuseumBalanceLightCount", 0);
            if (environment != null) { environment.Release(); Destroy(environment); }
        }
    }
}
