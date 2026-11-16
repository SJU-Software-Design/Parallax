using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Gallery
{
    // Explicit camera/exhibit pose fixtures for visual QA. Actual carrying, stairs and scene traversal are checked by GalleryMuseumMechanicsValidation.
    public sealed class GalleryCathedralValidation : MonoBehaviour
    {
        public GalleryGame game;
        readonly List<string> errors = new List<string>();
        readonly List<Shot> shots = new List<Shot>();
        readonly Report report = new Report();
        string output;
        double deadline;

        [Serializable] sealed class Shot
        {
            public string file;
            public Vector3 cameraPosition, lookTarget, destinationEye;
            public int renderCount;
            public float projectionOffsetX;
        }
        [Serializable] sealed class Report
        {
            public bool passed, realScan, livePreview, beachGeometry, beachPalette, nonFlat, warmColors, seaColors, parallax, noWind, backgroundOccluded, runtimeCleanup, runtimeReenabled;
            public string version, scene, unity, gpu, failure;
            public string scope = "Explicit small/home and enlarged/scaled exhibit and camera pose fixtures. Actual 1600x900 player-camera rendering includes the existing post-processing stack. The render-only destination must contain real beach geometry with animated-water, sand and original flower-atlas material groups, plus its isolated coastal sky. RenderTexture statistics require both blue/teal sea/sky and warm sand/scenery pixels. Left/right eye movement verifies a live non-flat off-axis beach view. A temporary magenta witness behind the aperture is visible when the portal surface is disabled and hidden when enabled; it is removed before final screenshots. This is visual/integration QA, not a claim of player-driven puzzle solving or traversal.";
            public float meanRed, meanGreen, meanBlue, luminanceDeviation, parallaxMeanDifference, parallaxChangedFraction, backgroundControlMagenta, backgroundWithPortalMagenta;
            public float bluePixelFraction, warmPixelFraction;
            public int previewRenderers, renderCount, previewWaterGroups, previewSandGroups, previewFlowerGroups, previewTriangles;
            public Shot[] shots;
            public string[] errors;
        }

        void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + trace);
        }
        IEnumerator Start()
        {
            Application.logMessageReceived += Log;
            deadline = Time.realtimeSinceStartupAsDouble + 30;
            var run = Sequence();
            while (true)
            {
                bool more; object next = null;
                try { Guard(); more = run.MoveNext(); if (more) next = run.Current; }
                catch (Exception exception) { report.failure = exception.ToString(); break; }
                if (!more) break;
                yield return next;
            }
            report.version = GalleryGame.Version; report.scene = SceneManager.GetActiveScene().name;
            report.unity = Application.unityVersion; report.gpu = SystemInfo.graphicsDeviceName;
            report.shots = shots.ToArray(); report.errors = errors.ToArray();
            report.passed = report.failure == null && errors.Count == 0 && report.realScan && report.livePreview && report.nonFlat &&
                report.beachGeometry && report.beachPalette && report.parallax && report.noWind && report.backgroundOccluded && report.runtimeCleanup && report.runtimeReenabled;
            if (output != null) File.WriteAllText(Path.Combine(output, "cathedral-results.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= Log;
            if (report.passed) Debug.Log("CATHEDRAL_VISUAL_OK " + shots.Count + " actual camera views; real coastal geometry, sea/sand palette, off-axis parallax, background occlusion, silent exit and runtime cleanup passed");
            else Debug.LogError("CATHEDRAL_VISUAL_FAIL " + report.failure + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Sequence()
        {
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-validationDir");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) :
                Path.Combine(Application.persistentDataPath, "GalleryCathedralValidation");
            Directory.CreateDirectory(output);
            Require(game != null && game.automated && game.view != null, "Actual automated museum camera is required.");
            game.conceptCapture = true; game.Resume(); game.active = false; Time.timeScale = 1;
            var portal = game.GetComponentInChildren<GalleryCathedralPortal>(true);
            Require(portal != null && game.orcusExit != null && portal.prop == game.orcusExit.prop, "The Cathedral portal must belong to the actual exit exhibit.");
            var prop = portal.prop; var entrance = game.orcusExit;
            prop.Restore();
            if (entrance.cathedralActivation != null) entrance.cathedralActivation.ResetToRest();
            entrance.ResetEntrance();
            for (int frame = 0; frame < 180 && (!portal.Ready || Shader.GetGlobalFloat("_MuseumProbeReady") < .5f); frame++) yield return null;
            Require(portal.Ready, "The live beach camera did not initialize.");
            var scanned = prop.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            report.realScan = Array.Exists(scanned, r => r != portal.portalSurface && r.sharedMesh != null && r.sharedMesh.vertexCount > 100000);
            Require(report.realScan, "The exit does not display the high-detail external Rodin scan.");
            report.previewRenderers = portal.PreviewRendererCount;
            var previewSource = Resources.Load<GameObject>(GalleryCathedralPortal.PreviewResource);
            report.livePreview = report.previewRenderers >= 4 && portal.PreviewCamera != null && previewSource != null &&
                previewSource.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 && previewSource.GetComponentsInChildren<Collider>(true).Length == 0 &&
                previewSource.GetComponentsInChildren<AudioListener>(true).Length == 0 && previewSource.GetComponentsInChildren<Camera>(true).Length == 0 &&
                previewSource.GetComponentsInChildren<Light>(true).Length == 0;
            Require(report.livePreview, "A render-only actual-beach preview with distinct material groups is required.");
            CheckBeachPreview(previewSource, portal.PreviewCamera);
            CheckMaterials(prop);
            report.noWind = !entrance.windEnabled && prop.GetComponentsInChildren<AudioSource>(true).Length == 0 &&
                portal.PreviewCamera.GetComponent<AudioListener>() == null;
            Require(report.noWind, "The Cathedral exit must not create wind audio or an additional listener.");

            var smallHero = SetCamera("01-cathedral-small-hero.png", Legacy(new Vector3(0, 1.6f, 48.45f)), prop.transform.TransformPoint(new Vector3(0, .85f, 0)));
            yield return null; yield return new WaitForEndOfFrame(); Capture(smallHero, portal);
            var smallClose = SetCamera("02-cathedral-small-close.png", Legacy(new Vector3(-.35f, 1.35f, 49.65f)), prop.transform.TransformPoint(new Vector3(0, .9f, 0)));
            yield return null; yield return new WaitForEndOfFrame(); Capture(smallClose, portal);

            // This is deliberately a labelled visual fixture, not simulated pickup or placement input.
            prop.InitializePhysics(); prop.body.isKinematic = true; prop.body.useGravity = false;
            prop.transform.SetPositionAndRotation(Legacy(new Vector3(0, .006f, 53)), Quaternion.identity);
            prop.transform.localScale = Vector3.one * 3;
            prop.body.position = prop.transform.position; prop.body.rotation = prop.transform.rotation;
            Physics.SyncTransforms(); prop.placement.ResetPlaced();
            float openingBudget = entrance.cathedralActivation == null ? 0 : entrance.cathedralActivation.duration + 2;
            for (float elapsed = 0; elapsed < openingBudget && entrance.cathedralActivation != null && !entrance.cathedralActivation.Opened;
                elapsed += Mathf.Min(.1f, Time.unscaledDeltaTime))
            {
                prop.placement.Tick(1f / 90); yield return null;
            }
            Require(entrance.cathedralActivation != null && entrance.cathedralActivation.Opened, "The settled enlarged palms did not open.");
            Vector3 center = portal.apertureFrame.TransformPoint(portal.apertureBounds.center);
            var enlarged = SetCamera("03-cathedral-enlarged-hero.png", Legacy(new Vector3(0, 1.75f, 45.4f)), Legacy(new Vector3(0, 2.85f, 53)));
            yield return null; yield return new WaitForEndOfFrame(); Capture(enlarged, portal);
            var front = SetCamera("04-cathedral-window-front.png", center + new Vector3(0, -.10f, -3.8f), center);
            yield return null; yield return new WaitForEndOfFrame(); Capture(front, portal);
            Color32[] frontalPixels = Read(portal.PortalTexture, "portal-front.png");
            Summarize(frontalPixels);
            Require(report.nonFlat, "The portal view is blank or nearly one flat color.");
            Require(report.beachPalette, "The live coastal view must show both blue/teal sea or sky and warm sand or scenery pixels.");

            var left = SetCamera("05-cathedral-window-left.png", center + new Vector3(-.65f, -.10f, -3.1f), center);
            yield return null; yield return new WaitForEndOfFrame(); Capture(left, portal);
            Vector3 leftEye = portal.LastDestinationEye; Matrix4x4 leftProjection = portal.LastProjection;
            Color32[] leftPixels = Read(portal.PortalTexture, "portal-left.png");
            var right = SetCamera("06-cathedral-window-right.png", center + new Vector3(.65f, -.10f, -3.1f), center);
            yield return null; yield return new WaitForEndOfFrame(); Capture(right, portal);
            Vector3 rightEye = portal.LastDestinationEye; Matrix4x4 rightProjection = portal.LastProjection;
            Color32[] rightPixels = Read(portal.PortalTexture, "portal-right.png");
            Compare(leftPixels, rightPixels);
            report.parallax = rightEye.x - leftEye.x > .05f && Mathf.Abs(rightProjection.m02 - leftProjection.m02) > .01f &&
                report.parallaxMeanDifference > .002f && report.parallaxChangedFraction > .03f;
            Require(report.parallax, "Left/right camera movement did not produce a different off-axis beach view.");

            SetCamera(null, center + new Vector3(0, 0, -3.2f), center);
            yield return null; yield return new WaitForEndOfFrame();
            GameObject witness = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material witnessMaterial = new Material(Shader.Find("Unlit/Color")) { color = Color.magenta, name = "QA only · gallery-background witness" };
            try
            {
                witness.name = "QA only · removed before gallery screenshots";
                Destroy(witness.GetComponent<Collider>());
                witness.transform.SetPositionAndRotation(center + portal.apertureFrame.forward * .36f, portal.apertureFrame.rotation);
                Vector3 worldSize = portal.apertureFrame.TransformVector(portal.apertureBounds.size);
                witness.transform.localScale = new Vector3(Mathf.Abs(worldSize.x) * .85f, Mathf.Abs(worldSize.y) * .85f, .025f);
                witness.GetComponent<Renderer>().sharedMaterial = witnessMaterial;
                portal.portalSurface.enabled = false;
                yield return null; yield return new WaitForEndOfFrame();
                Color32[] control = RenderCamera(null);
                report.backgroundControlMagenta = MagentaAt(control, game.view.WorldToViewportPoint(center));
                portal.portalSurface.enabled = true;
                yield return null; yield return new WaitForEndOfFrame();
                Color32[] occluded = RenderCamera(null);
                report.backgroundWithPortalMagenta = MagentaAt(occluded, game.view.WorldToViewportPoint(center));
                report.backgroundOccluded = report.backgroundControlMagenta > .5f && report.backgroundWithPortalMagenta < .15f;
            }
            finally
            {
                portal.portalSurface.enabled = true;
                Destroy(witness); Destroy(witnessMaterial);
            }
            Require(report.backgroundOccluded, "The hand aperture failed the positive/negative gallery-background occlusion check.");
            report.renderCount = portal.RenderCount;
            int originalMask = portal.SourceCullingMaskBeforePortal;
            var oldTexture = portal.PortalTexture; var oldCamera = portal.PreviewCamera;
            portal.enabled = false;
            yield return null; yield return null;
            report.runtimeCleanup = portal.PortalTexture == null && portal.PreviewCamera == null && !portal.Ready &&
                game.view.cullingMask == originalMask && oldCamera == null && oldTexture == null;
            Require(report.runtimeCleanup, "Disabling the portal leaked its texture/camera or failed to restore the source camera mask.");
            portal.enabled = true;
            yield return null; yield return null;
            report.runtimeReenabled = portal.Ready && portal.PortalTexture != null && portal.PreviewCamera != null &&
                (game.view.cullingMask & (1 << GalleryCathedralPortal.PreviewLayer)) == 0;
            Require(report.runtimeReenabled, "A cleaned-up portal failed to recreate its live view after re-enabling.");
            portal.enabled = false;
            yield return null; yield return null;
        }

        Vector3 Legacy(Vector3 point) => game.bottleBalance != null ? GalleryBottleBalanceCourse.ShiftLegacy(point) : point;
        Shot SetCamera(string file, Vector3 at, Vector3 target)
        {
            game.view.transform.SetPositionAndRotation(at, Quaternion.LookRotation(target - at));
            game.view.aspect = 1600f / 900; game.lighting.Synchronize();
            return new Shot { file = file, cameraPosition = at, lookTarget = target };
        }
        void Capture(Shot shot, GalleryCathedralPortal portal)
        {
            RenderCamera(shot.file);
            shot.destinationEye = portal.LastDestinationEye; shot.renderCount = portal.RenderCount;
            shot.projectionOffsetX = portal.LastProjection.m02; shots.Add(shot);
        }
        Color32[] RenderCamera(string file)
        {
            Camera camera = game.view; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D image = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                if (file != null) File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                target.Release(); Destroy(target); if (image != null) Destroy(image);
            }
        }
        Color32[] Read(RenderTexture texture, string file)
        {
            Require(texture != null && texture.IsCreated(), "The live portal has no created RenderTexture.");
            var old = RenderTexture.active; Texture2D image = null;
            try
            {
                RenderTexture.active = texture;
                image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
                if (file != null) File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally { RenderTexture.active = old; if (image != null) Destroy(image); }
        }
        void Summarize(Color32[] colors)
        {
            double r = 0, g = 0, b = 0, sum = 0, squared = 0;
            int blue = 0, warm = 0;
            foreach (var c in colors)
            {
                r += c.r; g += c.g; b += c.b;
                double value = (c.r * .2126 + c.g * .7152 + c.b * .0722) / 255;
                sum += value; squared += value * value;
                if (c.g > c.r * 1.06f && c.b > c.r * 1.05f && c.g + c.b > 50) blue++;
                if (c.r > c.b * 1.10f && c.g > c.b * 1.04f && c.r > 45 && c.g > 35) warm++;
            }
            report.meanRed = (float)(r / colors.Length / 255); report.meanGreen = (float)(g / colors.Length / 255); report.meanBlue = (float)(b / colors.Length / 255);
            double mean = sum / colors.Length;
            report.luminanceDeviation = (float)Math.Sqrt(Math.Max(0, squared / colors.Length - mean * mean));
            report.nonFlat = report.luminanceDeviation > .018f && mean > .015 && mean < .97;
            report.bluePixelFraction = (float)blue / colors.Length;
            report.warmPixelFraction = (float)warm / colors.Length;
            report.seaColors = report.bluePixelFraction > .08f;
            report.warmColors = report.warmPixelFraction > .01f;
            report.beachPalette = report.seaColors && report.warmColors;
        }
        void CheckBeachPreview(GameObject source, Camera camera)
        {
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                Require(filter.sharedMesh != null, "The coastal preview has a missing original mesh group.");
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                    report.previewTriangles += (int)(filter.sharedMesh.GetIndexCount(i) / 3);
            }
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true)) foreach (var material in renderer.sharedMaterials)
            {
                Require(material != null && material.shader != null && material.shader.isSupported &&
                    material.shader.name != "Hidden/InternalErrorShader", "Invalid coastal preview material: " + renderer.name);
                string shader = material.shader.name;
                Require(shader == "Parallax/MuseumBeachPreviewSurface" || shader == "Parallax/MuseumBeachPreviewWater",
                    "A destination material could still depend on scene-global lighting or placement state: " + shader);
                if (shader == "Parallax/MuseumBeachPreviewWater")
                {
                    Color deep = material.GetColor("_DeepColor"), shallow = material.GetColor("_ShallowColor");
                    Require(deep.maxColorComponent > .01f && shallow.maxColorComponent > .01f &&
                        material.GetFloat("_WaveHeight") > 0, "The original beach water tints or animated displacement are missing.");
                    report.previewWaterGroups++;
                }
                if (shader == "Parallax/MuseumBeachPreviewSurface" && material.GetFloat("_Projection") > 1.5f)
                {
                    Color sand = material.color;
                    Require(sand.r > sand.b * 1.04f && material.GetTexture("_BumpMap") != null,
                        "The source beach's warm rippled-sand material is missing.");
                    report.previewSandGroups++;
                }
                if (shader == "Parallax/MuseumBeachPreviewSurface" && material.GetFloat("_AlphaClip") > .5f)
                {
                    Texture atlas = material.mainTexture;
                    Require(atlas != null && atlas.width > 1 && atlas.height > 1 && material.GetFloat("_Cutoff") > 0,
                        "The source flower atlas or alpha-cutout setting is missing.");
                    report.previewFlowerGroups++;
                }
            }
            Material sky = Resources.Load<Material>(GalleryCathedralPortal.PreviewSkyResource);
            var cameraSky = camera.GetComponent<Skybox>();
            report.beachGeometry = report.previewTriangles > 10000 && report.previewWaterGroups > 0 &&
                report.previewSandGroups > 0 && report.previewFlowerGroups > 0 && sky != null && sky.shader != null &&
                sky.shader.name == "Parallax/MuseumBeachPreviewSky" && sky.shader.isSupported &&
                camera.clearFlags == CameraClearFlags.Skybox && cameraSky != null && cameraSky.material == sky;
            Require(report.beachGeometry, "The real beach's water, rippled sand, flower geometry and independent coastal sky are required.");
        }
        void Compare(Color32[] a, Color32[] b)
        {
            Require(a.Length == b.Length, "Left and right portal sample dimensions changed unexpectedly.");
            double difference = 0; int changed = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int delta = Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
                difference += delta / (255.0 * 3); if (delta > 18) changed++;
            }
            report.parallaxMeanDifference = (float)(difference / a.Length); report.parallaxChangedFraction = (float)changed / a.Length;
        }
        static float MagentaAt(Color32[] pixels, Vector3 viewport)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(viewport.x * 1600), 8, 1591), y = Mathf.Clamp(Mathf.RoundToInt(viewport.y * 900), 8, 891);
            int magenta = 0, samples = 0;
            for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
            {
                Color32 c = pixels[(y + dy) * 1600 + x + dx];
                if (c.r > 125 && c.b > 90 && c.g < c.r * .58f && c.g < c.b * .72f) magenta++;
                samples++;
            }
            return (float)magenta / samples;
        }
        static void CheckMaterials(GalleryProp prop)
        {
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true)) foreach (var material in renderer.sharedMaterials)
                Require(material != null && material.shader != null && material.shader.isSupported && material.shader.name != "Hidden/InternalErrorShader", "Invalid Cathedral material on " + renderer.name);
        }
        void Guard() { if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Cathedral visual QA exceeded its 30-second wall-clock budget."); }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void OnDestroy() { Application.logMessageReceived -= Log; }
    }
}
