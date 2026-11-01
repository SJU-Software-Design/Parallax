using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink
{
    public sealed class ShadeVisuals : MonoBehaviour
    {
        public Camera view;
        readonly List<GameObject> owned = new List<GameObject>();
        readonly Transform[] objects = new Transform[2];
        readonly Mesh[] shadowMeshes = new Mesh[3];
        readonly LineRenderer[] boundaries = new LineRenderer[3];
        readonly Material[] objectMaterials = new Material[2];
        Material chalk, concrete, dark, brass, turquoise, white, shade, lineMaterial, worldTextMaterial;
        ReflectionProbe reflectionProbe;
        Transform beacon, door;
        Font font;
        public static readonly Color Teal = new Color(.38f, .87f, .79f);
        public static readonly Color Amber = new Color(1, .74f, .36f);
        // Browser rules use a camera looking down -Z with +X on the right.
        // Unity's camera basis is left-handed: mirror world X at this boundary.
        public static Vector3 ToUnity(Vector3 p) => new Vector3(-p.x, p.y, p.z);

        Material Lit(string name, Color color, float metal = 0, float smooth = .18f)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Metallic", metal); material.SetFloat("_Glossiness", smooth); return material;
        }
        Material Flat(string name, Color color)
        { return new Material(Shader.Find("ShadeLink/Flat")) { name = name, color = color }; }
        GameObject Box(string name, Vector3 center, Vector3 size, Material material, Transform parent = null, bool shadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent == null ? transform : parent, false);
            go.transform.localPosition = ToUnity(center); go.transform.localScale = size;
            Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = shadows; owned.Add(go); return go;
        }
        GameObject Root(string name)
        { var go = new GameObject(name); go.transform.SetParent(transform, false); return go; }
        void Text(string text, Vector3 at, float size, Color color, Quaternion rotation, Transform parent = null)
        {
            var go = new GameObject(text); go.transform.SetParent(parent == null ? transform : parent, false);
            go.transform.localPosition = ToUnity(at); go.transform.localRotation = rotation;
            var mesh = go.AddComponent<TextMesh>(); mesh.text = text; mesh.font = font; mesh.fontSize = 96;
            mesh.characterSize = size * 10 / 96; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
            mesh.color = color; go.GetComponent<MeshRenderer>().sharedMaterial = worldTextMaterial;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        Mesh PlaneMesh(string name, List<Vector2> polygon, float y, Material material)
        {
            var go = Root(name); var mesh = new Mesh { name = name };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            UpdatePolygon(mesh, polygon, y); return mesh;
        }
        static void UpdatePolygon(Mesh mesh, List<Vector2> polygon, float y)
        {
            mesh.Clear(); if (polygon.Count < 3) return;
            var vertices = new Vector3[polygon.Count]; var triangles = new int[(polygon.Count - 2) * 3];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = new Vector3(-polygon[i].x, y, polygon[i].y);
            for (int i = 0; i < polygon.Count - 2; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        LineRenderer Outline(string name, List<Vector2> polygon, Color color, float width = .024f)
        {
            var go = Root(name); var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial; line.loop = true; line.useWorldSpace = true;
            line.widthMultiplier = width; line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            SetLine(line, polygon); return line;
        }
        static void SetLine(LineRenderer line, List<Vector2> polygon)
        {
            line.positionCount = polygon.Count;
            for (int i = 0; i < polygon.Count; i++) line.SetPosition(i, new Vector3(-polygon[i].x, .038f, polygon[i].y));
        }
        public void Build(Font koreanFont, bool groundExit = false)
        {
            font = koreanFont;
            worldTextMaterial = new Material(Shader.Find("ShadeLink/WorldText"));
            worldTextMaterial.mainTexture = font.material.mainTexture;
            Font.textureRebuilt += RefreshFont;
            chalk = Lit("Warm limestone", new Color(.79f, .76f, .66f), 0, .12f);
            concrete = Lit("Sandstone", new Color(.53f, .52f, .47f), 0, .18f);
            dark = Lit("Graphite", new Color(.065f, .105f, .12f), .28f, .4f);
            brass = Lit("Brushed copper", new Color(.72f, .41f, .18f), .55f, .36f);
            turquoise = Lit("Glazed teal", new Color(.18f, .40f, .40f), .25f, .38f);
            white = Flat("Ivory lettering", new Color(.96f, .91f, .77f));
            shade = Flat("Safety shadow", new Color(.115f, .235f, .245f));
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            QualitySettings.antiAliasing = 4; QualitySettings.shadowDistance = 75;
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowCascades = 4; QualitySettings.vSyncCount = 1;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.55f, .65f, .69f);
            RenderSettings.ambientEquatorColor = new Color(.35f, .40f, .42f);
            RenderSettings.ambientGroundColor = new Color(.19f, .20f, .18f);
            // Procedural scenes have no baked ambient probe. Supply diffuse sky
            // lighting explicitly so shaded PBR surfaces retain readable detail.
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.45f, .53f, .57f));
            RenderSettings.ambientProbe = ambient;
            RenderSettings.reflectionIntensity = .5f;
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.70f, .72f, .69f);
            RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 22; RenderSettings.fogEndDistance = 75;
            var sunObject = Root("Fixed sunlight"); var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1, .88f, .66f); sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .8f; sun.shadowBias = .035f; sun.shadowNormalBias = .2f;
            sun.transform.rotation = Quaternion.LookRotation(ToUnity(Rules.Sun).normalized); RenderSettings.sun = sun;

            // Floors use analytical silhouettes to keep every visible safety edge exact.
            Box("Sunlit floor", new Vector3(0, -.18f, 0), new Vector3(14, .36f, 32), chalk, shadows: false);
            var joint = Flat("Floor joints", new Color(.51f, .51f, .45f));
            for (int z = -16; z <= 16; z += 2) Box("Floor seam", new Vector3(0, .002f, z), new Vector3(14, .002f, .012f), joint, shadows: false);
            for (int x = -7; x <= 7; x += 2) Box("Floor seam", new Vector3(x, .002f, 0), new Vector3(.012f, .002f, 32), joint, shadows: false);
            for (int i = 0; i < Rules.FixedShades.Length; i++)
            {
                var rect = Rules.FixedShades[i]; PlaneMesh("Architectural shade " + i, rect.Polygon(), .009f, shade);
                Outline("Safe shelter rim " + i, rect.Polygon(), new Color(.30f, .60f, .56f, .65f), .018f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Side wall", new Vector3(side * 7.18f, 3.8f, 0), new Vector3(.36f, 7.6f, 32.7f), chalk);
                Box("Wall base", new Vector3(side * 6.98f, .21f, 0), new Vector3(.025f, .42f, 32), dark);
                Box("Copper datum", new Vector3(side * 6.96f, 1.1f, 0), new Vector3(.018f, .014f, 32), brass);
                for (int z = -15; z <= 15; z += 5)
                {
                    Box("Vertical rib", new Vector3(side * 6.97f, 4, z), new Vector3(.04f, 7.2f, .07f), concrete);
                    Box("Clerestory recess", new Vector3(side * 6.965f, 5.7f, z + 1.4f), new Vector3(.016f, 1.3f, 2.15f), dark);
                }
            }
            Box("Entrance wall", new Vector3(0, 3.8f, 16.2f), new Vector3(14.4f, 7.6f, .4f), concrete);
            Box("Exit wall", new Vector3(0, 3.8f, -16.2f), new Vector3(14.4f, 7.6f, .4f), chalk);
            Box("Entrance portal", new Vector3(-3.6f, 1.6f, 15.98f), new Vector3(2.4f, 3.2f, .015f), dark);
            Box("Entry canopy", new Vector3(-4.15f, 7.82f, 14.1f), new Vector3(5.7f, .18f, 3.8f), dark);
            Box("Rest canopy", new Vector3(-.05f, 7.82f, -.15f), new Vector3(10.9f, .16f, 3.3f), dark);
            Box("Exit canopy", new Vector3(4.1f, 7.82f, -13.7f), new Vector3(5.8f, .16f, 4.6f), dark);
            Box("Rest divider", Rules.LowWall.center, Rules.LowWall.size, concrete);
            Box("Divider top inlay", new Vector3(0, .8005f, .55f), new Vector3(1.5f, .001f, 1.8f), dark);
            if (!groundExit)
            {
                Box("Raised exit", Rules.ExitLedge.center, Rules.ExitLedge.size, dark);
                Box("Exit top", new Vector3(4.65f, 2.7005f, -15.4f), new Vector3(2.5f, .001f, 1.2f), turquoise, shadows: false);
                for (int i = 1; i <= 3; i++) Box("Height datum", new Vector3(4.65f, i * .9f - .06f, -14.794f), new Vector3(2.35f, .018f, .008f), brass);
            }
            door = Root("Exit portal").transform;
            if (groundExit) door.localPosition = Vector3.down * Rules.ExitHeight;
            Box("Door aperture", new Vector3(4.65f, 4, -15.974f), new Vector3(1.75f, 2.6f, .025f), dark, door);
            var portal = Flat("Exit glow", Teal * .85f);
            Box("Door light left", new Vector3(3.77f, 4, -15.94f), new Vector3(.035f, 2.65f, .035f), portal, door, false);
            Box("Door light right", new Vector3(5.53f, 4, -15.94f), new Vector3(.035f, 2.65f, .035f), portal, door, false);
            Box("Door light lintel", new Vector3(4.65f, 5.33f, -15.94f), new Vector3(1.8f, .035f, .035f), portal, door, false);
            Text("출구", new Vector3(4.65f, groundExit ? 3.05f : 4.48f, -15.91f), .70f, Teal, Quaternion.Euler(0, 180, 0));
            if (!groundExit) Text("2.7 m", new Vector3(4.65f, 1.44f, -14.78f), .62f, new Color(.85f, .83f, .68f), Quaternion.Euler(0, 180, 0));
            Text("그늘 잇기", new Vector3(-2.8f, 4.5f, -15.965f), 1.1f, new Color(.30f, .35f, .33f), Quaternion.Euler(0, 180, 0));
            Text(groundExit ? "빛을 피하고, 맞는 크기를 찾다" : "빛을 피하고, 높이를 넘다", new Vector3(-2.8f, 3.75f, -15.965f), .36f, new Color(.38f, .41f, .37f), Quaternion.Euler(0, 180, 0));
            Text("01   시작 그늘", new Vector3(-3.7f, .012f, 13.15f), .49f, Teal, Quaternion.Euler(90, 180, 0));
            Text("02   쉼터", new Vector3(2.8f, .012f, .65f), .53f, Teal, Quaternion.Euler(90, 180, 0));
            Text("03   도착 그늘", new Vector3(4.2f, .012f, -11.8f), .49f, Teal, Quaternion.Euler(90, 180, 0));
            beacon = Root("Checkpoint beacon").transform;
            Outline("Checkpoint pad", Rules.Checkpoint.Polygon(), Teal, .045f);
            Text("저장", new Vector3(6.475f, .015f, -14.33f), .29f, Teal, Quaternion.Euler(90, 180, 0), beacon);
            Text("그늘 연결\n저장 지점", new Vector3(6.4f, 1.4f, -15.95f), .23f, Teal, Quaternion.Euler(0, 180, 0));

            for (int i = 0; i < objects.Length; i++)
            {
                objects[i] = Root(i == 0 ? "01 Perspective pillar" : "02 Perspective screen").transform;
                Vector3 size = Rules.BaseSizes[i];
                objectMaterials[i] = new Material(i == 0 ? brass : turquoise);
                Box("Solid body", new Vector3(0, size.y / 2, 0), size, objectMaterials[i], objects[i]);
                float inset = .065f;
                // Insets sit on the bounding faces so the geometric shadow remains exact.
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Face inset", new Vector3(0, size.y / 2, side * (size.z / 2 + .0005f)),
                        new Vector3(size.x - inset * 2, size.y - inset * 2, .001f), i == 0 ? chalk : dark, objects[i], false);
                    Box("Identity stripe", new Vector3(0, size.y * .23f, side * (size.z / 2 + .0015f)),
                        new Vector3(size.x * .6f, .019f, .001f), white, objects[i], false);
                    Text(i == 0 ? "01" : "02", new Vector3(0, size.y * .58f, side * (size.z / 2 + .004f)),
                        i == 0 ? .67f : .77f, i == 0 ? new Color(.25f, .28f, .25f) : Teal,
                        Quaternion.Euler(0, side == 1 ? 180 : 0, 0), objects[i]);
                }
                Box("Top inset", new Vector3(0, size.y + .0005f, 0), new Vector3(size.x - .08f, .001f, size.z - .08f), dark, objects[i], false);
            }
            var initial = Rules.Shadows(Rules.InitialBlocks());
            for (int i = 0; i < initial.Length; i++)
            {
                shadowMeshes[i] = PlaneMesh("Exact shadow " + i, initial[i], .014f + i * .001f, shade);
                boundaries[i] = Outline("Placement boundary " + i, initial[i], Teal);
            }
            var cameraObject = Root("Player camera"); view = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>(); view.fieldOfView = 68; view.nearClipPlane = .045f; view.farClipPlane = 120;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.66f, .74f, .76f);
            view.allowHDR = true; view.allowMSAA = true;
            cameraObject.AddComponent<ShadePost>();
            reflectionProbe = Root("Courtyard reflection").AddComponent<ReflectionProbe>();
            reflectionProbe.transform.position = new Vector3(0, 3, 0);
            reflectionProbe.mode = ReflectionProbeMode.Realtime;
            reflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            reflectionProbe.resolution = 128; reflectionProbe.size = new Vector3(15, 9, 33);
            reflectionProbe.boxProjection = true; reflectionProbe.intensity = .65f;
            reflectionProbe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            reflectionProbe.backgroundColor = view.backgroundColor;
        }
        void Start() { if (reflectionProbe != null) reflectionProbe.RenderProbe(); }
        void RefreshFont(Font changed) { if (changed == font && worldTextMaterial != null) worldTextMaterial.mainTexture = font.material.mainTexture; }
        void OnDestroy() { Font.textureRebuilt -= RefreshFont; }
        public void Sync(GameState state, List<Vector2>[] shadows, int selected, bool invalid)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                Block block = state.blocks[i]; objects[i].localPosition = new Vector3(-block.x, Rules.FloorGap, block.z);
                objects[i].localScale = Vector3.one * block.scale;
                bool highlight = state.held != null ? state.held.index == i : selected == i;
                objectMaterials[i].EnableKeyword("_EMISSION");
                objectMaterials[i].SetColor("_EmissionColor", highlight ? (invalid ? new Color(.18f, .035f, .01f) : new Color(.04f, .13f, .12f)) : Color.black);
            }
            for (int i = 0; i < shadows.Length; i++)
            {
                UpdatePolygon(shadowMeshes[i], shadows[i], .014f + i * .001f);
                SetLine(boundaries[i], shadows[i]);
                boundaries[i].enabled = state.held != null && state.held.index == i;
                boundaries[i].startColor = boundaries[i].endColor = invalid ? Amber : Teal;
            }
            view.transform.SetPositionAndRotation(ToUnity(state.player.Eye), Quaternion.LookRotation(ToUnity(Rules.Look(state.yaw, state.pitch))));
            view.GetComponent<ShadePost>().exposure = state.exposure;
        }
    }

    [RequireComponent(typeof(Camera))]
    public sealed class ShadePost : MonoBehaviour
    {
        Material material;
        public float exposure;
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null) material = new Material(Shader.Find("ShadeLink/Finish"));
            material.SetFloat("_Exposure", exposure); Graphics.Blit(source, destination, material);
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
