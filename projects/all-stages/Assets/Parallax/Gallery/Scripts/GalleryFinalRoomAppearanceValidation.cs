using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Read-only scene-state/raycast probe. No rendering, ReadPixels, screenshot,
    // camera manipulation or desktop input. Called explicitly by the native suite.
    public static class GalleryFinalRoomAppearanceValidation
    {
        static Renderer HitRenderer(RaycastHit hit)
        {
            var direct = hit.collider.GetComponent<Renderer>();
            if (direct != null) return direct;
            for (Transform owner = hit.collider.transform.parent; owner != null; owner = owner.parent)
                foreach (var renderer in owner.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Bounds bounds = renderer.bounds; bounds.Expand(.03f);
                    if (renderer.enabled && bounds.Contains(hit.point)) return renderer;
                }
            return null;
        }
        static string HierarchyPath(Transform node)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (; node != null; node = node.parent) parts.Add(node.name);
            parts.Reverse(); return string.Join("/", parts);
        }
        static string WallDiagnostic(RaycastHit hit, Renderer selected)
        {
            string detail = "collider=" + HierarchyPath(hit.collider.transform) + " hit=" + hit.point.ToString("F3") +
                " renderer=" + (selected == null ? "<none>" : HierarchyPath(selected.transform) + " batched=" + selected.isPartOfStaticBatch + " bounds=" + selected.bounds);
            Transform owner = hit.collider.transform.parent;
            if (owner != null)
                detail += " siblings=" + string.Join(" | ", owner.GetComponentsInChildren<MeshRenderer>(true).Take(6).Select(renderer =>
                    renderer.name + " enabled=" + renderer.enabled + " batched=" + renderer.isPartOfStaticBatch + " bounds=" + renderer.bounds));
            return detail;
        }
        public static IEnumerator Run(GalleryGame game, Action<bool, string, string> check)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            check(game != null && game.conceptLayout, "Final-room appearance probe owns the real concept museum", "");
            if (game == null) yield break;
            yield return null; yield return null;
            var finish = game.GetComponent<GalleryFinalWindowAppearance>();
            check(finish != null && string.IsNullOrEmpty(finish.InstallError) && finish.Before != null && finish.WindowRenderer != null && finish.WindowGroup != null,
                "Runtime scene installation updates the existing serialized last window", finish == null ? "Missing installer" : (finish.InstallError ?? finish.CandidateDiagnostics));
            if (finish == null || finish.Before == null || finish.WindowRenderer == null) yield break;
            var before = finish.Before; var after = GalleryFinalWindowAppearance.Capture(finish.WindowRenderer);
            var paneMesh = finish.WindowRenderer.GetComponent<MeshFilter>();
            float expectedZ = 50.5f + (game.bottleBalance != null ? GalleryBottleBalanceLayout.AddedLength : 0);
            check(finish.WindowRenderer.enabled && finish.WindowRenderer.gameObject.activeInHierarchy && paneMesh != null && paneMesh.sharedMesh != null &&
                paneMesh.sharedMesh.vertexCount > 0 && Mathf.Abs(game.transform.InverseTransformPoint(finish.WindowRenderer.bounds.center).z - expectedZ) < .05f &&
                finish.OriginalWindowColliderCount > 0,
                "Installer selects the live rear glazing mesh and the complete final-window contact group", finish.CandidateDiagnostics);
            float transformZ = game.transform.InverseTransformPoint(finish.WindowRenderer.transform.position).z;
            float groupZ = game.transform.InverseTransformPoint(finish.WindowGroup.position).z;
            float boundsZ = game.transform.InverseTransformPoint(finish.WindowRenderer.bounds.center).z;
            check(!finish.WindowRenderer.isPartOfStaticBatch && !before.isPartOfStaticBatch && !after.isPartOfStaticBatch &&
                Mathf.Abs(transformZ - expectedZ) < .05f && Mathf.Abs(groupZ - expectedZ) < .05f && Mathf.Abs(boundsZ - expectedZ) < .05f,
                "Runtime-inserted final glazing is unbatched and its transform, group and renderer bounds agree at the final location",
                "expectedZ=" + expectedZ + " transformZ=" + transformZ + " groupZ=" + groupZ + " boundsZ=" + boundsZ + " " + finish.CandidateDiagnostics);
            check(before.shader == "Standard" && before.color.b > before.color.r + .1f && before.color.a > .1f && before.glossiness > .8f,
                "Before state identifies the authored blue reflective window rather than an opaque wall", JsonUtility.ToJson(before));
            check(before.shaderSupported && after.shaderSupported && GraphicsSettings.currentRenderPipeline == null,
                "Original and adjusted window shaders are supported by the active built-in renderer", SystemInfo.graphicsDeviceType.ToString());
            check(finish.WindowRenderer.sharedMaterial != finish.OriginalMaterial && finish.OtherWindowCount >= 1 && finish.OtherWindowsUnchanged(),
                "Only the final window uses a local clone while earlier shared blue glazing stays unchanged", "otherWindows=" + finish.OtherWindowCount);
            check(after.color == GalleryFinalWindowAppearance.NeutralTint && after.glossiness == GalleryFinalWindowAppearance.NeutralGlossiness &&
                after.specular == 0 && after.reflections == 0,
                "After state has local neutral frosted glazing without the strong blue specular coating", JsonUtility.ToJson(after));
            check(after.shader == before.shader && after.queue >= 3000 && after.queue == before.queue && after.zWrite == 0 &&
                after.mode == before.mode && after.srcBlend == before.srcBlend && after.dstBlend == before.dstBlend,
                "Adjustment preserves the existing transparent blend and depth settings", "mode=" + after.mode + " src=" + after.srcBlend + " dst=" + after.dstBlend);
            check((after.center - before.center).sqrMagnitude < .000001f && (after.size - before.size).sqrMagnitude < .000001f &&
                finish.WindowGroup.GetComponentsInChildren<Collider>(true).Length == finish.OriginalWindowColliderCount,
                "Window silhouette, glazing contacts and daylight aperture geometry remain unchanged", "");
            check(after.color.a == 1 && after.dstBlend == (float)BlendMode.OneMinusSrcAlpha &&
                (after.srcBlend == (float)BlendMode.One || after.srcBlend == (float)BlendMode.SrcAlpha),
                "Full-coverage frosted glazing gives the blue exterior sky zero destination blend contribution",
                "alpha=" + after.color.a + " destinationFactor=1-alpha=" + (1 - after.color.a));
            check(finish.EnvironmentUnchanged(game), "Local frosted window preserves the original skybox, clear flags and ambient illumination",
                "before=" + JsonUtility.ToJson(finish.EnvironmentBefore) + " after=" + JsonUtility.ToJson(GalleryFinalWindowAppearance.CaptureEnvironment(game)));
            check(after.shadowCastingMode == before.shadowCastingMode && after.shadowCastingMode == (int)ShadowCastingMode.Off &&
                finish.WindowGroup.GetComponentsInChildren<GalleryCaster>(true).Length == finish.OriginalWindowCasterCount,
                "Local frosted finish preserves noncasting glazing and the gameplay sunlight-caster set", "");
            Physics.SyncTransforms();
            float z = game.transform.InverseTransformPoint(finish.WindowRenderer.bounds.center).z;
            Vector3 wallOrigin = game.transform.TransformPoint(new Vector3(7.55f, 2.5f, z - 3.5f));
            bool wallHit = Physics.Raycast(wallOrigin, game.transform.right, out var wall, 1.3f, ~(1 << GalleryCathedralPortal.PreviewLayer), QueryTriggerInteraction.Ignore);
            var wallRenderer = wallHit ? HitRenderer(wall) : null; var wallMaterial = wallRenderer == null ? null : wallRenderer.sharedMaterial;
            check(wallHit && wallMaterial != null && wallMaterial.shader != null && wallMaterial.shader.name == "Parallax/MuseumSurface" &&
                wallMaterial.renderQueue < 2500 && wallMaterial.color.a >= .99f && Mathf.Abs(wallMaterial.color.r - wallMaterial.color.b) < .08f,
                "Final right-wall ray hits the existing warm opaque wall, separate from blue glazing",
                wallHit ? WallDiagnostic(wall, wallRenderer) + "; material=" + (wallMaterial == null ? "<none>" : wallMaterial.name) : "No right-wall contact");
            Vector3 paneOrigin = game.transform.TransformPoint(new Vector3(7.55f, 2.5f, z));
            bool paneHit = Physics.Raycast(paneOrigin, game.transform.right, out var pane, 1.3f, ~(1 << GalleryCathedralPortal.PreviewLayer), QueryTriggerInteraction.Ignore);
            check(paneHit && pane.collider.name == "Window glass physical contact", "Final window keeps its original physical glazing contact",
                paneHit ? pane.collider.name : "No window contact");
            var portal = game.GetComponentInChildren<GalleryCathedralPortal>(true);
            check(portal != null && portal.Ready && portal.portalSurface != null && portal.PreviewCamera != null,
                "Hands retain their separate live coastal aperture", portal == null ? "No portal" : "renderCount=" + portal.RenderCount);
            if (portal != null && portal.portalSurface != null && portal.PreviewCamera != null)
            {
                int preview = 1 << GalleryCathedralPortal.PreviewLayer;
                check((game.view.cullingMask & preview) == 0 && portal.PreviewCamera.cullingMask == preview,
                    "Coastal preview layer is excluded from the museum view and isolated in its own camera", "mainMask=" + game.view.cullingMask);
                check(!portal.portalSurface.bounds.Intersects(finish.WindowRenderer.bounds) &&
                    portal.portalSurface.sharedMaterial.shader.name == "Parallax/MuseumLibraryPortal" && portal.portalSurface.sharedMaterial != finish.WindowRenderer.sharedMaterial,
                    "Coastal texture is confined to the hand aperture and has no window-material overlap", "portalBounds=" + portal.portalSurface.bounds);
            }
            var previewRoot = GameObject.Find("Beach beyond the hands · render-only coastal stage");
            check(previewRoot != null && previewRoot.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == GalleryCathedralPortal.PreviewLayer) &&
                previewRoot.GetComponentsInChildren<Collider>(true).Length == 0,
                "Render-only coast has no colliders and all geometry remains on the excluded preview layer", "");
            Debug.Log("GALLERY_FINAL_WINDOW_PROBE pixelsVerified=false captures=0 before=" + JsonUtility.ToJson(before) + " after=" + JsonUtility.ToJson(after));
        }
    }
}