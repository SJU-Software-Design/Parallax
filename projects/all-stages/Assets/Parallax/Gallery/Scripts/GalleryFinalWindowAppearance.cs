using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Gallery
{
    // The final room's blue glazing is an authored window, separate from the
    // hands' coastal portal. Keep its geometry, collision and sunlight opening;
    // use a local frosted material instance so the earlier window stays intact.
    [DisallowMultipleComponent]
    public sealed class GalleryFinalWindowAppearance : MonoBehaviour
    {
        public const string PaneName = "Transparent blue glass · sunlight passes through";
        public static readonly Color NeutralTint = new Color(.94f, .94f, .91f, 1f);
        public const float NeutralGlossiness = .20f;
        [Serializable] public sealed class MaterialState
        {
            public string material, shader, keywords;
            public Color color;
            public float mode, srcBlend, dstBlend, zWrite, glossiness, specular, reflections;
            public int queue, layer, shadowCastingMode;
            public bool shaderSupported, propertyBlockEmpty, isPartOfStaticBatch;
            public Vector3 center, size;
        }
        [Serializable] public sealed class EnvironmentState
        {
            public string skybox, skyShader;
            public Color skyTint, ambientSky, ambientEquator, ambientGround;
            public int cameraClearFlags, ambientMode;
        }
        public EnvironmentState EnvironmentBefore { get; private set; }
        Material originalSkybox;
        public Renderer WindowRenderer { get; private set; }
        public Transform WindowGroup { get; private set; }
        public string CandidateDiagnostics { get; private set; }
        public Material OriginalMaterial { get; private set; }
        public MaterialState Before { get; private set; }
        public string InstallError { get; private set; }
        public int OtherWindowCount => otherWindows.Count;
        public int OriginalWindowColliderCount { get; private set; }
        public int OriginalWindowCasterCount { get; private set; }
        Material ownedMaterial;
        readonly List<Renderer> otherWindows = new List<Renderer>();
        readonly List<Material> otherMaterials = new List<Material>();
        readonly List<Color> otherColors = new List<Color>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            Install(FindFirstObjectByType<GalleryGame>());
        }
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var game = root.GetComponentInChildren<GalleryGame>(true);
                if (game != null) { Install(game); break; }
            }
        }
        public static GalleryFinalWindowAppearance Install(GalleryGame game)
        {
            if (game == null || !game.conceptLayout) return null;
            var current = game.GetComponent<GalleryFinalWindowAppearance>();
            if (current != null) return current;
            var finish = game.gameObject.AddComponent<GalleryFinalWindowAppearance>();
            finish.Apply(game); return finish;
        }
        static bool PaneNameMatches(Renderer renderer) => renderer != null &&
            (renderer.name == PaneName || renderer.name.StartsWith(PaneName + " · rear segment", StringComparison.Ordinal));
        static Transform AuthoredWindowGroup(Renderer renderer, Transform owner)
        {
            if (!PaneNameMatches(renderer)) return null;
            Transform window = null;
            for (Transform parent = renderer.transform.parent; parent != null && parent != owner; parent = parent.parent)
            {
                if (parent.name == "Classical arched exterior window") window = parent;
                if (parent.name == "Concept museum right window wall") return window;
            }
            return null;
        }
        static bool AuthoredWindowPane(Renderer renderer, Transform owner)
        {
            if (AuthoredWindowGroup(renderer, owner) == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return false;
            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0 && renderer.bounds.size.sqrMagnitude > .000001f;
        }
        static string CandidateDescription(Renderer renderer, Transform owner)
        {
            var names = new List<string>();
            for (Transform node = renderer.transform; node != null; node = node.parent)
            { names.Add(node.name); if (node == owner) break; }
            names.Reverse(); var filter = renderer.GetComponent<MeshFilter>();
            Transform group = AuthoredWindowGroup(renderer, owner);
            return "path=" + string.Join("/", names) + " enabled=" + renderer.enabled + " active=" + renderer.gameObject.activeInHierarchy +
                " vertices=" + (filter == null || filter.sharedMesh == null ? 0 : filter.sharedMesh.vertexCount) +
                " mesh=" + (filter == null || filter.sharedMesh == null ? "<none>" : filter.sharedMesh.name) +
                " batched=" + renderer.isPartOfStaticBatch + " isStatic=" + renderer.gameObject.isStatic +
                " position=" + renderer.transform.position.ToString("F3") + " localPosition=" + renderer.transform.localPosition.ToString("F3") +
                " groupPosition=" + (group == null ? "<none>" : group.position.ToString("F3")) +
                " localToWorld=" + renderer.localToWorldMatrix.ToString("F3") +
                " transformLocalToWorld=" + renderer.transform.localToWorldMatrix.ToString("F3") +
                " bounds=" + renderer.bounds + " localCenter=" + owner.InverseTransformPoint(renderer.bounds.center).ToString("F3");
        }
        void Apply(GalleryGame game)
        {
            var panes = new List<Renderer>(); var candidates = new List<string>(); float furthest = float.NegativeInfinity;
            foreach (var renderer in game.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (PaneNameMatches(renderer)) candidates.Add(CandidateDescription(renderer, game.transform));
                if (!AuthoredWindowPane(renderer, game.transform)) continue;
                panes.Add(renderer);
                float z = game.transform.InverseTransformPoint(renderer.bounds.center).z;
                if (z > furthest) { furthest = z; WindowRenderer = renderer; }
            }
            CandidateDiagnostics = string.Join(" | ", candidates);
            Debug.Log("GALLERY_FINAL_WINDOW_CANDIDATES " + CandidateDiagnostics);
            WindowGroup = AuthoredWindowGroup(WindowRenderer, game.transform);
            float finalRoomStart = GalleryConceptWorld.PortraitRoomEnd + (game.bottleBalance != null ? GalleryBottleBalanceLayout.AddedLength : 0);
            if (WindowRenderer == null || WindowGroup == null || furthest < finalRoomStart || WindowRenderer.sharedMaterial == null)
            {
                InstallError = "The final-room authored exterior window was not found. " + CandidateDiagnostics;
                Debug.LogWarning("GALLERY_FINAL_WINDOW: " + InstallError); return;
            }
            foreach (var pane in panes)
            {
                if (pane == WindowRenderer) continue;
                otherWindows.Add(pane); otherMaterials.Add(pane.sharedMaterial);
                otherColors.Add(pane.sharedMaterial != null ? pane.sharedMaterial.color : Color.clear);
            }
            OriginalMaterial = WindowRenderer.sharedMaterial;
            Before = Capture(WindowRenderer);
            originalSkybox = RenderSettings.skybox;
            EnvironmentBefore = CaptureEnvironment(game);
            OriginalWindowColliderCount = WindowGroup.GetComponentsInChildren<Collider>(true).Length;
            OriginalWindowCasterCount = WindowGroup.GetComponentsInChildren<GalleryCaster>(true).Length;
            ownedMaterial = new Material(OriginalMaterial) { name = "Last-room window · neutral frosted glazing (runtime only)" };
            // The real opening otherwise sees the blue procedural sky. Full
            // coverage removes that destination contribution locally; global
            // sky, illumination and analytic daylight apertures stay unchanged.
            ownedMaterial.color = NeutralTint;
            if (ownedMaterial.HasProperty("_Glossiness")) ownedMaterial.SetFloat("_Glossiness", NeutralGlossiness);
            if (ownedMaterial.HasProperty("_SpecularHighlights")) ownedMaterial.SetFloat("_SpecularHighlights", 0);
            if (ownedMaterial.HasProperty("_GlossyReflections")) ownedMaterial.SetFloat("_GlossyReflections", 0);
            ownedMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            ownedMaterial.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            WindowRenderer.sharedMaterial = ownedMaterial;
            Debug.Log("GALLERY_FINAL_WINDOW_BEFORE " + JsonUtility.ToJson(Before));
            Debug.Log("GALLERY_FINAL_WINDOW_AFTER " + JsonUtility.ToJson(Capture(WindowRenderer)));
        }
        public bool OtherWindowsUnchanged()
        {
            for (int i = 0; i < otherWindows.Count; i++)
                if (otherWindows[i] == null || otherWindows[i].sharedMaterial != otherMaterials[i] ||
                    otherMaterials[i] == null || otherMaterials[i].color != otherColors[i]) return false;
            return true;
        }
        public static EnvironmentState CaptureEnvironment(GalleryGame game)
        {
            Material sky = RenderSettings.skybox;
            return new EnvironmentState { skybox = sky == null ? "<none>" : sky.name,
                skyShader = sky == null || sky.shader == null ? "<none>" : sky.shader.name,
                skyTint = sky != null && sky.HasProperty("_SkyTint") ? sky.GetColor("_SkyTint") : Color.clear,
                cameraClearFlags = game == null || game.view == null ? -1 : (int)game.view.clearFlags,
                ambientMode = (int)RenderSettings.ambientMode, ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor, ambientGround = RenderSettings.ambientGroundColor };
        }
        public bool EnvironmentUnchanged(GalleryGame game)
        {
            if (EnvironmentBefore == null || originalSkybox != RenderSettings.skybox) return false;
            var after = CaptureEnvironment(game); var before = EnvironmentBefore;
            return after.skyTint == before.skyTint && after.cameraClearFlags == before.cameraClearFlags &&
                after.ambientMode == before.ambientMode && after.ambientSky == before.ambientSky &&
                after.ambientEquator == before.ambientEquator && after.ambientGround == before.ambientGround;
        }
        static float Value(Material material, string property) => material.HasProperty(property) ? material.GetFloat(property) : -1;
        public static MaterialState Capture(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null) return null;
            var material = renderer.sharedMaterial; var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
            return new MaterialState { material = material.name, shader = material.shader == null ? "<missing>" : material.shader.name,
                shaderSupported = material.shader != null && material.shader.isSupported, isPartOfStaticBatch = renderer.isPartOfStaticBatch, color = material.color, keywords = string.Join(",", material.shaderKeywords),
                mode = Value(material, "_Mode"), srcBlend = Value(material, "_SrcBlend"), dstBlend = Value(material, "_DstBlend"), zWrite = Value(material, "_ZWrite"),
                glossiness = Value(material, "_Glossiness"), specular = Value(material, "_SpecularHighlights"), reflections = Value(material, "_GlossyReflections"),
                queue = material.renderQueue, layer = renderer.gameObject.layer, shadowCastingMode = (int)renderer.shadowCastingMode, center = renderer.bounds.center, size = renderer.bounds.size, propertyBlockEmpty = properties.isEmpty };
        }
        void OnDestroy()
        {
            if (WindowRenderer != null && WindowRenderer.sharedMaterial == ownedMaterial) WindowRenderer.sharedMaterial = OriginalMaterial;
            if (ownedMaterial != null) Destroy(ownedMaterial);
        }
    }
}