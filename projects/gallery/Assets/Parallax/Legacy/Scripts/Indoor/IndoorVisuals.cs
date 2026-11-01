using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink
{
    public sealed class IndoorVisuals : MonoBehaviour
    {
        public Camera view;
        IndoorGame game;
        Material textMat, lineMat, dark, wall, trim, glow;
        readonly Transform[] bodies = new Transform[2];
        readonly Material[] bodyMats = new Material[2];
        readonly Mesh[] shadowMeshes = new Mesh[4];
        readonly LineRenderer[] shadowLines = new LineRenderer[4];
        readonly LineRenderer[] hints = new LineRenderer[2];
        Transform exitDoor;
        public static readonly Color SafeColor = new Color(.22f, .85f, .75f), LightColor = new Color(1, .73f, .32f);
        Material Flat(string name, Color color) => new Material(Shader.Find("ShadeLink/Flat")) { name = name, color = color };
        Material Lit(string name, Color color, float metal = 0)
        {
            var m = new Material(Shader.Find("Standard")) { name = name, color = color };
            m.SetFloat("_Metallic", metal); m.SetFloat("_Glossiness", .32f); return m;
        }
        GameObject Root(string name, Transform parent = null)
        { var go = new GameObject(name); go.transform.SetParent(parent == null ? transform : parent, false); return go; }
        GameObject Box(string name, Vector3 position, Vector3 size, Material mat, Transform parent = null, bool shadow = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent == null ? transform : parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            Destroy(go.GetComponent<Collider>()); var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = shadow; return go;
        }
        void Text(string value, Vector3 at, float size, Color color, Quaternion rotation, Transform parent = null)
        {
            var go = Root(value, parent); go.transform.localPosition = at; go.transform.localRotation = rotation;
            var tm = go.AddComponent<TextMesh>(); tm.text = value; tm.font = game.font; tm.fontSize = 100;
            tm.characterSize = size / 10; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = textMat; r.shadowCastingMode = ShadowCastingMode.Off;
        }
        Mesh Polygon(string name, List<Vector2> poly, float height, Material material)
        {
            var go = Root(name); var mesh = new Mesh { name = name }; go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            UpdatePolygon(mesh, poly, height); return mesh;
        }
        static void UpdatePolygon(Mesh mesh, List<Vector2> points, float height)
        {
            mesh.Clear(); if (points.Count < 3) return;
            var vertices = new Vector3[points.Count]; var triangles = new int[(points.Count - 2) * 3];
            for (int i = 0; i < points.Count; i++) vertices[i] = new Vector3(points[i].x, height, points[i].y);
            for (int i = 0; i < points.Count - 2; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1; }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        LineRenderer Outline(string name, List<Vector2> points, Color color, float width = .025f)
        {
            var line = Root(name).AddComponent<LineRenderer>(); line.sharedMaterial = lineMat; line.loop = true; line.useWorldSpace = true;
            line.widthMultiplier = width; line.startColor = line.endColor = color; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            SetLine(line, points); return line;
        }
        static void SetLine(LineRenderer line, List<Vector2> points)
        { line.positionCount = points.Count; for (int i = 0; i < points.Count; i++) line.SetPosition(i, new Vector3(points[i].x, .045f, points[i].y)); }
        public void Build(IndoorGame owner)
        {
            game = owner;
            textMat = new Material(Shader.Find("ShadeLink/WorldText")); textMat.mainTexture = game.font.material.mainTexture;
            Font.textureRebuilt += RefreshFont;
            lineMat = new Material(Shader.Find("Sprites/Default"));
            dark = Lit("Gunmetal", new Color(.10f, .16f, .20f), .3f);
            wall = Lit("Blue concrete", new Color(.30f, .38f, .42f));
            trim = Lit("Brushed aluminium", new Color(.49f, .56f, .57f), .4f);
            glow = Flat("Safe accent", SafeColor);
            var shade = Flat("Shadow / safe floor", new Color(.045f, .15f, .17f));
            var floor = Flat("Shelter floor", new Color(.06f, .105f, .13f));
            var lightFloor = Flat("Shuttered light footprint", new Color(.80f, .65f, .38f));
            var amber = Flat("Lamp aperture", LightColor);
            var seam = Flat("Floor joints", new Color(.085f, .15f, .17f));
            QualitySettings.antiAliasing = 4; QualitySettings.vSyncCount = 1;
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = ShadowResolution.High; QualitySettings.shadowDistance = 45;
            RenderSettings.fog = false; RenderSettings.skybox = null; RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.33f, .42f, .50f);
            var ambient = new SphericalHarmonicsL2(); ambient.AddAmbientLight(new Color(.34f, .42f, .5f)); RenderSettings.ambientProbe = ambient;
            Box("Level floor", new Vector3(0, -.15f, 18), new Vector3(9.6f, .3f, 36.5f), floor, shadow: false);
            for (int z = 0; z <= 36; z++) Box("Tile joint", new Vector3(0, .001f, z), new Vector3(9.2f, .002f, .012f), seam, shadow: false);
            for (int x = -4; x <= 4; x++) Box("Tile joint", new Vector3(x, .001f, 18), new Vector3(.012f, .002f, 36), seam, shadow: false);
            Box("Ceiling", new Vector3(0, 8.9f, 18), new Vector3(9.6f, .35f, 36.5f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Continuous side wall", new Vector3(side * 4.8f, 4.4f, 18), new Vector3(.4f, 8.8f, 36.5f), wall);
                Box("Wall base", new Vector3(side * 4.58f, .23f, 18), new Vector3(.035f, .46f, 36), dark);
                for (int z = 1; z < 36; z += 3)
                {
                    Box("Wall rib", new Vector3(side * 4.57f, 4.4f, z), new Vector3(.055f, 8.7f, .08f), trim);
                    Box("Wall recess", new Vector3(side * 4.57f, 3.2f, z + 1.3f), new Vector3(.03f, 2.8f, 1.5f), dark);
                }
                foreach (float z in new[] { 2.5f, 16f, 31.5f })
                    Box("Shelter indicator", new Vector3(side * 4.55f, .7f, z), new Vector3(.035f, .035f, 3.3f), glow, shadow: false);
            }
            Box("Entrance wall", new Vector3(0, 4.4f, -.2f), new Vector3(9.6f, 8.8f, .4f), wall);
            Box("Exit wall", new Vector3(0, 4.4f, 36.2f), new Vector3(9.6f, 8.8f, .4f), wall);
            for (int i = 0; i < 2; i++)
            {
                var beam = IndoorRules.Beams[i]; Polygon("Illuminated passage " + i, beam.Polygon(), .005f, lightFloor);
                foreach (float z in new[] { beam.minZ, beam.maxZ })
                {
                    Box("Light boundary", new Vector3(0, .009f, z), new Vector3(9.2f, .004f, .045f), amber, shadow: false);
                    for (int x = -4; x <= 4; x++) Box("Caution dash", new Vector3(x, .009f, z - .12f), new Vector3(.5f, .004f, .09f), amber, shadow: false);
                }
                var fixture = Root("Profile light " + (i + 1)).transform; fixture.position = IndoorRules.Lamps[i];
                fixture.rotation = Quaternion.LookRotation(new Vector3(0, 0, (beam.minZ + beam.maxZ) / 2) - fixture.position);
                Box("Lamp housing", new Vector3(0, 0, -.13f), new Vector3(1.25f, .72f, .45f), dark, fixture);
                Box("Rectangular aperture", new Vector3(0, 0, .103f), new Vector3(.88f, .4f, .014f), amber, fixture, false);
                for (int side = -1; side <= 1; side += 2)
                    Box("Shutter", new Vector3(side * .57f, 0, .32f), new Vector3(.045f, .64f, .52f), dark, fixture);
                Box("Ceiling mount", new Vector3(0, 8.05f, IndoorRules.Lamps[i].z), new Vector3(.12f, 1.7f, .12f), trim);
                var light = Root("Focused working light " + i).AddComponent<Light>(); light.transform.SetPositionAndRotation(fixture.position, fixture.rotation);
                light.type = LightType.Spot; light.spotAngle = 74; light.innerSpotAngle = 55; light.range = 30;
                light.intensity = 3.5f; light.color = new Color(1, .85f, .61f); light.shadows = LightShadows.Soft; light.shadowBias = .035f;
                Text("BEAM 0" + (i + 1), new Vector3(-3.25f, 1.9f, beam.minZ + .05f), .55f, LightColor, Quaternion.identity);
                hints[i] = Outline("Placement hint " + i, new FloorRect(i == 0 ? -1.5f : -2.9f, i == 0 ? 1.5f : 2.9f, i == 0 ? 13.7f : 27.5f, i == 0 ? 15.8f : 29.8f).Polygon(), SafeColor, .04f);
                Text(i == 0 ? "01  시작 쉼터" : "02  중간 쉼터 · 자동 저장", new Vector3(0, .018f, i == 0 ? 3.6f : 17), .53f, SafeColor, Quaternion.Euler(90, 0, 0));
            }
            Text("03  도착 쉼터", new Vector3(0, .018f, 30.7f), .62f, SafeColor, Quaternion.Euler(90, 0, 0));
            Text("PARALLAX", new Vector3(0, 5.2f, 35.96f), 1.05f, new Color(.56f, .70f, .72f), Quaternion.identity);
            Text("빛 사이를 건너다", new Vector3(0, 4.3f, 35.96f), .55f, SafeColor, Quaternion.identity);
            Box("Door recess", new Vector3(0, 1.6f, 35.95f), new Vector3(2.7f, 3.2f, .07f), dark);
            exitDoor = Box("Sliding exit", new Vector3(0, 1.5f, 35.85f), new Vector3(2.35f, 3, .09f), trim).transform;
            for (int side = -1; side <= 1; side += 2)
                Box("Exit jamb", new Vector3(side * 1.35f, 1.65f, 35.8f), new Vector3(.045f, 3.3f, .06f), glow, shadow: false);
            Text("EXIT  /  출구", new Vector3(0, 3.55f, 35.78f), .51f, SafeColor, Quaternion.identity);
            Text("LIGHT CORRIDOR", new Vector3(0, 3.8f, .05f), .9f, SafeColor, Quaternion.Euler(0, 180, 0));
            Text("원근으로 그늘길을 만드세요", new Vector3(0, 2.9f, .05f), .42f, Color.white, Quaternion.Euler(0, 180, 0));
            for (int i = 0; i < 2; i++)
            {
                bodies[i] = Root("Perspective object " + i).transform; Vector3 size = Rules.BaseSizes[i];
                bodyMats[i] = Lit(i == 0 ? "Amber column" : "Teal screen", i == 0 ? new Color(.73f, .39f, .13f) : new Color(.12f, .43f, .45f), .2f);
                Box("Solid body", Vector3.up * (size.y / 2), size, bodyMats[i], bodies[i]);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Panel inset", new Vector3(0, size.y / 2, side * (size.z / 2 + .001f)), new Vector3(size.x * .78f, size.y * .76f, .002f), dark, bodies[i], false);
                    Text("0" + (i + 1), new Vector3(0, size.y * .6f, side * (size.z / 2 + .005f)), .55f, i == 0 ? LightColor : SafeColor, Quaternion.Euler(0, side == -1 ? 0 : 180, 0), bodies[i]);
                    Box("Handle marker", new Vector3(0, size.y * .24f, side * (size.z / 2 + .006f)), new Vector3(size.x * .45f, .02f, .004f), i == 0 ? amber : glow, bodies[i], false);
                }
            }
            for (int i = 0; i < shadowMeshes.Length; i++)
            { shadowMeshes[i] = Polygon("Exact point-light shadow " + i, game.shadows[i], .018f + i * .001f, shade); shadowLines[i] = Outline("Shadow preview " + i, game.shadows[i], SafeColor); }
            view = Root("Indoor player camera").AddComponent<Camera>(); view.fieldOfView = 72; view.nearClipPlane = .045f; view.farClipPlane = 70;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.025f, .04f, .055f); view.allowHDR = true;
            view.gameObject.AddComponent<AudioListener>(); view.gameObject.AddComponent<ShadePost>();
        }
        void RefreshFont(Font changed) { if (changed == game.font) textMat.mainTexture = changed.material.mainTexture; }
        void OnDestroy() { Font.textureRebuilt -= RefreshFont; }
        public void Sync()
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                Block b = game.blocks[i]; bodies[i].localPosition = new Vector3(b.x, Rules.FloorGap, b.z); bodies[i].localScale = Vector3.one * b.scale;
                bodyMats[i].EnableKeyword("_EMISSION"); bodyMats[i].SetColor("_EmissionColor", (game.held != null ? game.held.index == i : game.aimed == i) ? (game.invalid ? new Color(.25f, .03f, 0) : new Color(.06f, .19f, .16f)) : Color.black);
                hints[i].enabled = game.hints;
            }
            for (int i = 0; i < shadowMeshes.Length; i++)
            {
                UpdatePolygon(shadowMeshes[i], game.shadows[i], .018f + i * .001f); SetLine(shadowLines[i], game.shadows[i]);
                shadowLines[i].enabled = game.held != null && i % game.blocks.Length == game.held.index;
                shadowLines[i].startColor = shadowLines[i].endColor = game.invalid ? LightColor : SafeColor;
            }
            exitDoor.localPosition = new Vector3(game.checkpoint == 2 ? 2.45f : 0, 1.5f, 35.85f);
            view.transform.SetPositionAndRotation(game.player.Eye, Quaternion.LookRotation(IndoorRules.Look(game.yaw, game.pitch)));
            view.GetComponent<ShadePost>().exposure = game.exposure;
        }
    }
}
