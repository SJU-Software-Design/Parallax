using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShadeLink.Tutorial;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadeLink.Editor
{
    public static class KeyShadowPassageVariants
    {
        const string Folder = "Assets/Parallax/Tutorial/Resources/Tutorial";
        const string SourcePath = Folder + "/ShadowKeyBranch/ShadowKey_Branch.fbx";
        const string NarrowPath = Folder + "/ShadowKeyBranch/ShadowKey_NarrowPassage_V1.asset";
        const string SettingsPath = Folder + "/KeyShadowPassageTest.asset";
        const string Request = "Temp/KeyShadowPassageV1.request";
        const string Report = "Temp/KeyShadowPassageV1.result.txt";
        const float OriginalRadius = .17f, NarrowRadius = OriginalRadius * .25f, BlendRadius = .33f;
        static readonly Vector3 Center = new Vector3(.15f, 3.71f, 0f);

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!File.Exists(Request)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(Request);
                try
                {
                    BuildNarrowMesh();
                    Select(true);
                    KeyShadowAlignmentTests.RunAll();
                }
                catch (Exception error)
                {
                    File.AppendAllText(Report, "FAIL: " + error + "\n");
                    Debug.LogException(error);
                }
            };
        }

        [MenuItem("그늘 잇기/통로 테스트/원본으로 비교")]
        static void Original() => Select(false);

        [MenuItem("그늘 잇기/통로 테스트/1안 - 구멍 폭 25%")]
        static void Narrow()
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(NarrowPath) == null) BuildNarrowMesh();
            Select(true);
        }

        [MenuItem("그늘 잇기/통로 테스트/원본으로 비교", true)]
        [MenuItem("그늘 잇기/통로 테스트/1안 - 구멍 폭 25%", true)]
        static bool CanSelect() => !EditorApplication.isPlayingOrWillChangePlaymode;

        static Mesh OriginalMesh()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            var filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("The original branch sculpture mesh is missing.");
            return filter.sharedMesh;
        }

        static void BuildNarrowMesh()
        {
            Mesh original = OriginalMesh();
            Mesh generated = new Mesh { name = "ShadowKey_Branch · narrow passage V1", indexFormat = original.indexFormat };
            Vector3[] vertices = original.vertices;
            Vector3[] normals = original.normals;
            bool[] changed = new bool[vertices.Length];
            float passageMinimumZ = float.PositiveInfinity, passageMaximumZ = float.NegativeInfinity;
            int count = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                Vector2 radial = new Vector2(v.x - Center.x, v.y - Center.y);
                float radius = radial.magnitude;
                if (Mathf.Abs(radius - OriginalRadius) <= .03f)
                {
                    passageMinimumZ = Mathf.Min(passageMinimumZ, v.z);
                    passageMaximumZ = Mathf.Max(passageMaximumZ, v.z);
                }
                if (radius >= BlendRadius || radius <= .000001f) continue;
                float blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(OriginalRadius, BlendRadius, radius));
                float narrowed = radius - (OriginalRadius - NarrowRadius) * (1f - blend);
                radial *= narrowed / radius;
                vertices[i] = new Vector3(Center.x + radial.x, Center.y + radial.y, v.z);
                changed[i] = true;
                count++;
            }
            Require(!float.IsInfinity(passageMinimumZ) && passageMaximumZ > passageMinimumZ,
                "The original passage's axial span could not be measured.");
            var combinedVertices = new List<Vector3>(vertices);
            var indices = new List<int>(original.triangles);
            // The sculpture is a carved branch surface, not a continuous bore.
            // Narrowing its existing vertices leaves side openings that a ray
            // can escape through. A thin inner wall closes those openings over
            // the passage's existing axial span, without extending its length.
            AddPassageWall(combinedVertices, indices, passageMinimumZ, passageMaximumZ);
            generated.SetVertices(combinedVertices);
            generated.SetTriangles(indices, 0);
            var uv = original.uv;
            if (uv.Length == vertices.Length)
            {
                Array.Resize(ref uv, combinedVertices.Count);
                generated.uv = uv;
            }
            generated.RecalculateNormals();
            // Preserve authored normals everywhere outside the changed passage.
            if (normals.Length == vertices.Length)
            {
                var recalculated = generated.normals;
                for (int i = 0; i < normals.Length; i++) if (!changed[i]) recalculated[i] = normals[i];
                generated.normals = recalculated;
            }
            generated.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(NarrowPath);
            if (existing == null) AssetDatabase.CreateAsset(generated, NarrowPath);
            else
            {
                var owner = AssetDatabase.LoadAssetAtPath<KeyShadowPassageSettings>(SettingsPath);
                if (owner == null || owner.narrowPassage != existing)
                    throw new InvalidOperationException("The narrow passage destination is not the registered test asset.");
                EditorUtility.CopySerialized(generated, existing);
                Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
            }
            File.WriteAllText(Report, $"V1_BUILT width=25%, changed body vertices={count}, continuous inner wall over original passage Z=[{passageMinimumZ:F5},{passageMaximumZ:F5}], imported FBX preserved.\n");
        }

        static void AddPassageWall(List<Vector3> vertices, List<int> indices, float minimumZ, float maximumZ)
        {
            const int segments = 64;
            float outerRadius = NarrowRadius + .0125f;
            Vector3 Point(float radius, float angle, float z) =>
                new Vector3(Center.x + radius * Mathf.Cos(angle), Center.y + radius * Mathf.Sin(angle), z);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
                indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
            }
            for (int i = 0; i < segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments, b = 2f * Mathf.PI * (i + 1) / segments;
                Vector3 ia = Point(NarrowRadius, a, minimumZ), ib = Point(NarrowRadius, b, minimumZ);
                Vector3 ic = Point(NarrowRadius, b, maximumZ), id = Point(NarrowRadius, a, maximumZ);
                Vector3 oa = Point(outerRadius, a, minimumZ), ob = Point(outerRadius, b, minimumZ);
                Vector3 oc = Point(outerRadius, b, maximumZ), od = Point(outerRadius, a, maximumZ);
                Quad(ia, id, ic, ib);
                Quad(oa, ob, oc, od);
                Quad(ia, ib, ob, oa);
                Quad(id, od, oc, ic);
            }
        }

        static void Select(bool narrow)
        {
            var settings = AssetDatabase.LoadAssetAtPath<KeyShadowPassageSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<KeyShadowPassageSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            Undo.RecordObject(settings, "Select key passage test");
            settings.originalPassage = OriginalMesh();
            settings.narrowPassage = AssetDatabase.LoadAssetAtPath<Mesh>(NarrowPath);
            settings.useNarrowPassage = narrow;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            Menu.SetChecked("그늘 잇기/통로 테스트/원본으로 비교", !narrow);
            Menu.SetChecked("그늘 잇기/통로 테스트/1안 - 구멍 폭 25%", narrow);
            Debug.Log("다음 재생: " + settings.Label);
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        public static void VerifyGeometry()
        {
            var narrow = AssetDatabase.LoadAssetAtPath<Mesh>(NarrowPath);
            if (narrow == null) return;
            Mesh original = OriginalMesh();
            var a = original.vertices;
            var b = narrow.vertices;
            Require(b.Length > a.Length && narrow.triangles.Length > original.triangles.Length,
                "V1 is missing its continuous internal passage wall.");
            var originalIndices = original.triangles;
            var narrowIndices = narrow.triangles;
            for (int i = 0; i < originalIndices.Length; i++)
                Require(originalIndices[i] == narrowIndices[i], "Narrowing changed triangle indices.");
            for (int i = 0; i < a.Length; i++)
            {
                Require(a[i].z == b[i].z, "V1 changed passage length instead of width only.");
                if (Vector2.Distance(new Vector2(a[i].x, a[i].y), new Vector2(Center.x, Center.y)) >= BlendRadius)
                    Require(a[i] == b[i], "V1 changed the sculpture outside the passage region.");
            }
            for (int i = a.Length; i < b.Length; i++)
                Require(Vector2.Distance(new Vector2(b[i].x, b[i].y), new Vector2(Center.x, Center.y)) < OriginalRadius &&
                    b[i].z >= original.bounds.min.z && b[i].z <= original.bounds.max.z,
                    "The internal wall extends outside the existing passage region.");
            Require(Vector3.Distance(original.bounds.center, narrow.bounds.center) < .00001f &&
                Vector3.Distance(original.bounds.size, narrow.bounds.size) < .00001f, "V1 changed the outer dimensions.");

            var source = GameObject.Find("ShadowKey - Branch Sculpture Preview");
            var lamp = GameObject.Find("Only light source · modern wall lamp on right");
            Require(source != null && lamp != null, "Open KeyShadowPreview for passage verification.");
            var sourceFilter = source.GetComponentInChildren<MeshFilter>();
            var root = new GameObject("Passage width comparison");
            var visual = new GameObject("Passage width comparison mesh");
            var output = new StringBuilder();
            try
            {
                Quaternion aligned = Quaternion.Euler(343.33970f, 101.07150f, 17.04333f);
                root.transform.SetPositionAndRotation(source.transform.position, aligned);
                root.transform.localScale = source.transform.lossyScale;
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = sourceFilter.transform.localPosition;
                visual.transform.localRotation = sourceFilter.transform.localRotation;
                visual.transform.localScale = sourceFilter.transform.localScale;
                var filter = visual.AddComponent<MeshFilter>();
                float[,] ends = new float[2, 4];
                for (int profile = 0; profile < 2; profile++)
                {
                    filter.sharedMesh = profile == 0 ? original : narrow;
                    using var align = new KeyShadowAutoAlign(root.transform, lamp.transform, visual.transform);
                    Require(align.CanCapture(aligned) && align.PassageIsClear, "The exact solution is blocked by the narrowed geometry.");
                    for (int direction = 0; direction < 4; direction++)
                    {
                        Vector3 axis = visual.transform.TransformDirection(direction < 2 ? Vector3.right : Vector3.up);
                        float sign = direction % 2 == 0 ? -1f : 1f;
                        float end = 0f;
                        for (float degrees = .25f; degrees <= 60f; degrees += .25f)
                        {
                            Quaternion pose = Quaternion.AngleAxis(sign * degrees, axis) * aligned;
                            if (!align.CanCapture(pose)) break;
                            end = degrees;
                        }
                        ends[profile, direction] = end;
                        output.AppendLine($"{(profile == 0 ? "Original" : "V1 narrow")} direction={direction} capture edge={end:F2} deg (including unchanged 2-degree assist)");
                    }
                }
                for (int i = 0; i < 4; i++)
                    Require(ends[1, i] < ends[0, i] - 1f, "Narrowing did not reduce a sampled capture range.");
                filter.sharedMesh = narrow;
                using (var align = new KeyShadowAutoAlign(root.transform, lamp.transform, visual.transform))
                {
                    Quaternion reproduced = Quaternion.Euler(-11.873f, 69.006f, -16.429f);
                    Require(align.NearestTargetAngle(reproduced, out _) > 25f && !align.CanCapture(reproduced),
                        "The 30-degree side-gap false positive from manual play is still accepted.");
                    int checkedPoses = 0;
                    foreach (float pitch in new[] { -45f, -15f, 15f, 45f })
                    foreach (float roll in new[] { -45f, 0f, 45f })
                    for (float yaw = 0; yaw < 360f; yaw += 10f)
                    {
                        Quaternion pose = Quaternion.Euler(pitch, yaw, roll);
                        if (align.NearestTargetAngle(pose, out _) <= 6f) continue;
                        Require(!align.CanCapture(pose), $"V1 still accepts a remote gap: ({pitch}, {yaw}, {roll}).");
                        checkedPoses++;
                    }
                    output.AppendLine($"REMOTE_GAPS_REJECTED: {checkedPoses} distant orientations, plus manual-play 30-degree false positive.");
                }
                File.AppendAllText(Report, "GEOMETRY_OK original body triangles, original axial span, outer vertices, outer bounds, exact passage, closed side openings.\n" + output);
                Debug.Log("KEY_PASSAGE_V1_GEOMETRY_OK");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
