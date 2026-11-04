using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // The visible outline and the painted subject are both projections of every
    // original scan triangle. The small CPU grid only makes matching forgiving;
    // it never supplies the visible contour or replaces the scanned geometry.
    [DefaultExecutionOrder(140)]
    public sealed class GalleryNikeCanvas : MonoBehaviour
    {
        public GalleryGame game;
        public GalleryNikeLight illumination;
        public GalleryProp nike;
        public GalleryNikeGeometry geometry;
        public Transform source, targetPlane;
        public Renderer paper, overlay;
        public Texture2D artwork;
        public Vector2 Size = new Vector2(4.8f, 5.3f);
        public Vector3 CanonicalPosition = new Vector3(-4.8f, 0, 23.9f);
        public Quaternion CanonicalRotation = Quaternion.identity;
        public float CanonicalScale = 1;
        public float HoldSeconds = 1, BrushSeconds = 2.6f;
        public bool Completed { get; private set; }
        public bool Revealing { get; private set; }
        public float Alignment { get; private set; }
        public float MatchProgress => Mathf.Clamp01(matchTime / Mathf.Max(.1f, HoldSeconds));
        public float RevealProgress => Mathf.Clamp01(revealTime / Mathf.Max(.1f, BrushSeconds));
        public float Progress => RevealProgress;
        public Transform TargetCanvas => targetPlane == null ? transform : targetPlane;
        public Vector3 TargetCenter => TargetCanvas.position;
        public Vector3 TargetNormal => -TargetCanvas.forward;
        public string Status => Completed ? "니케가 그림으로 남았어요 · 열린 문까지 그림자의 길을 이어 보세요." :
            Revealing ? "그림자가 붓결을 따라 그림으로 채워지고 있어요." :
            illumination != null && !illumination.Ready && Alignment >= .84f ? "윤곽이 이어져요 · 니케를 내려놓아 고정하세요." :
            Alignment >= .84f ? "그림자가 윤곽과 이어졌어요 · 잠깐 기다려 주세요." :
            "니케를 옮기고 크기를 바꾸어 벽의 금빛 윤곽에 그림자를 겹쳐 보세요.";

        const int Columns = 48, Rows = 56, TargetResolution = 4096, LiveResolution = 2048;
        const float MatchThreshold = .84f;
        readonly bool[] reference = new bool[Columns * Rows], live = new bool[Columns * Rows];
        MeshFilter scan;
        Matrix4x4 scanToNike;
        RenderTexture targetMask, liveMask, paintedScan, projectedDepth;
        Material projector;
        MaterialPropertyBlock display;
        float matchTime, revealTime, missedTime, sampleCountdown;
        bool referenceReady, renderedReference;
        Matrix4x4 lastLiveMatrix;
        bool haveLiveMatrix;
        Vector3 referenceSource;
        Matrix4x4 referenceCanonical, referencePlane;
        Vector2 referenceSize;

        public static GalleryNikeCanvas Install(GalleryGame owner, GalleryNikeLight light, GalleryProp prop, GalleryNikeGeometry exact)
        {
            var existing = owner.GetComponentInChildren<GalleryNikeCanvas>(true);
            if (existing != null) { light.canvas = existing; owner.nikeCanvas = existing; return existing; }
            var root = new GameObject("Nike · shadow becomes a painting");
            root.transform.SetParent(owner.transform, false);
            root.transform.localPosition = new Vector3(-4.8f, 2.9f, 28.6f);
            var puzzle = root.AddComponent<GalleryNikeCanvas>();
            puzzle.game = owner; puzzle.illumination = light; puzzle.nike = prop; puzzle.geometry = exact;
            puzzle.source = light.emitter; puzzle.targetPlane = root.transform;
            light.canvas = puzzle; owner.nikeCanvas = puzzle;
            var paperMaterial = new Material(Shader.Find("Parallax/MuseumSurface")) { name = "Nike canvas · warm archival linen", color = new Color(.44f, .35f, .23f) };
            GalleryMuseumMaterials.Apply(paperMaterial, "Paper"); paperMaterial.SetFloat("_Roughness", .94f);
            var face = new GameObject("Blank linen · original museum PBR"); face.transform.SetParent(root.transform, false);
            var mesh = Quad(puzzle.Size);
            face.AddComponent<MeshFilter>().sharedMesh = mesh;
            puzzle.paper = face.AddComponent<MeshRenderer>(); puzzle.paper.sharedMaterial = paperMaterial;
            var support = face.AddComponent<BoxCollider>(); support.center = Vector3.forward * .05f;
            support.size = new Vector3(puzzle.Size.x, puzzle.Size.y, .10f);
            var image = new GameObject("Exact scan outline and brush-painted Nike"); image.transform.SetParent(root.transform, false);
            image.transform.localPosition = Vector3.back * .016f;
            image.AddComponent<MeshFilter>().sharedMesh = mesh;
            puzzle.overlay = image.AddComponent<MeshRenderer>();
            var paintingMaterial = new Material(Shader.Find("Parallax/MuseumNikeCanvas")) { name = "Nike canvas · exact silhouette and painted scan" };
            puzzle.overlay.sharedMaterial = paintingMaterial;
            puzzle.overlay.shadowCastingMode = ShadowCastingMode.Off; puzzle.overlay.receiveShadows = false;
            var gold = new Material(paperMaterial) { name = "Nike canvas · thin bronze frame", color = new Color(.68f, .46f, .21f) };
            gold.SetFloat("_Metallic", .85f); gold.SetFloat("_Roughness", .38f);
            var ink = new Material(paperMaterial) { name = "Nike canvas · dark linen rabbet", color = new Color(.08f, .07f, .05f) };
            GalleryConceptArchitecture.PictureFrame(root.transform, puzzle.Size + Vector2.one * .025f,
                new GalleryConceptArchitecture.Palette { gold = gold, bronze = gold, ink = ink }, .65f);
            GalleryWorld.Materials.Add(paperMaterial); GalleryWorld.Materials.Add(paintingMaterial);
            GalleryWorld.Materials.Add(gold); GalleryWorld.Materials.Add(ink);
            return puzzle;
        }

        static Mesh Quad(Vector2 size)
        {
            var mesh = new Mesh { name = "Nike wall canvas · full surface" };
            mesh.vertices = new[] { new Vector3(-size.x, -size.y, 0) * .5f, new Vector3(size.x, -size.y, 0) * .5f,
                new Vector3(size.x, size.y, 0) * .5f, new Vector3(-size.x, size.y, 0) * .5f };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        bool FindScan()
        {
            if (scan != null) return true;
            if (geometry == null || geometry.ScanFilter == null || nike == null || source == null) return false;
            scan = geometry.ScanFilter;
            scanToNike = nike.transform.worldToLocalMatrix * scan.transform.localToWorldMatrix;
            return scan.sharedMesh != null;
        }

        Matrix4x4 CanonicalMatrix => Matrix4x4.TRS(CanonicalPosition, CanonicalRotation, Vector3.one * CanonicalScale) * scanToNike;
        public Vector3 CanvasPoint(float u, float v) => TargetCanvas.TransformPoint(new Vector3((u - .5f) * Size.x, (v - .5f) * Size.y, 0));

        Vector2 Project(Vector3 world)
        {
            Vector3 ray = world - source.position;
            float denominator = Vector3.Dot(ray, TargetNormal);
            if (Mathf.Abs(denominator) < .00001f) return Vector2.one * -1000;
            float t = Vector3.Dot(TargetCenter - source.position, TargetNormal) / denominator;
            if (t <= 1) return Vector2.one * -1000;
            Vector3 plane = TargetCanvas.InverseTransformPoint(source.position + ray * t);
            return new Vector2(plane.x / Size.x + .5f, plane.y / Size.y + .5f);
        }

        void EnsureReference()
        {
            illumination?.SyncPose();
            if (!FindScan()) return;
            Matrix4x4 canonical = CanonicalMatrix;
            if (referenceReady && referenceCanonical == canonical && referencePlane == TargetCanvas.localToWorldMatrix &&
                referenceSource == source.position && referenceSize == Size) return;
            System.Array.Clear(reference, 0, reference.Length);
            var mesh = scan.sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles;
            var projected = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) projected[i] = Project(canonical.MultiplyPoint3x4(vertices[i]));
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector2 a = projected[triangles[i]], b = projected[triangles[i + 1]], c = projected[triangles[i + 2]];
                if (a.x < -100 || b.x < -100 || c.x < -100) continue;
                float area = Cross(b - a, c - a); if (Mathf.Abs(area) < .000000001f) continue;
                int x0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) * Columns - .5f));
                int x1 = Mathf.Min(Columns - 1, Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) * Columns - .5f));
                int y0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) * Rows - .5f));
                int y1 = Mathf.Min(Rows - 1, Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) * Rows - .5f));
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2((x + .5f) / Columns, (y + .5f) / Rows);
                    float ab = Cross(b - a, p - a), bc = Cross(c - b, p - b), ca = Cross(a - c, p - c);
                    if (area > 0 ? ab >= -.0000001f && bc >= -.0000001f && ca >= -.0000001f : ab <= .0000001f && bc <= .0000001f && ca <= .0000001f)
                        reference[y * Columns + x] = true;
                }
            }
            referenceCanonical = canonical; referencePlane = TargetCanvas.localToWorldMatrix;
            referenceSource = source.position; referenceSize = Size;
            referenceReady = true; renderedReference = false;
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool Nearby(bool[] mask, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            { int xx = x + dx, yy = y + dy; if (xx >= 0 && xx < Columns && yy >= 0 && yy < Rows && mask[yy * Columns + xx]) return true; }
            return false;
        }

        public void RefreshAlignment()
        {
            EnsureReference();
            if (!referenceReady || illumination == null) { Alignment = 0; return; }
            int expected = 0, actual = 0, recalled = 0, precise = 0;
            for (int y = 0; y < Rows; y++) for (int x = 0; x < Columns; x++)
            {
                int at = y * Columns + x;
                live[at] = illumination.IsNikeShadow(CanvasPoint((x + .5f) / Columns, (y + .5f) / Rows), TargetNormal, true);
                if (reference[at]) expected++;
                if (live[at]) actual++;
            }
            for (int y = 0; y < Rows; y++) for (int x = 0; x < Columns; x++)
            {
                int at = y * Columns + x;
                if (reference[at] && Nearby(live, x, y)) recalled++;
                if (live[at] && Nearby(reference, x, y)) precise++;
            }
            float recall = recalled / Mathf.Max(1f, expected), precision = precise / Mathf.Max(1f, actual);
            Alignment = actual < expected * .55f || actual > expected * 1.8f ? 0 : 2 * recall * precision / Mathf.Max(.0001f, recall + precision);
            sampleCountdown = .15f;
        }

        public void Tick(float dt)
        {
            dt = Mathf.Clamp(dt, 0, .1f);
            if (Completed) return;
            if (Revealing)
            {
                revealTime += dt;
                if (RevealProgress >= 1) { Completed = true; revealTime = Mathf.Max(.1f, BrushSeconds); }
                return;
            }
            sampleCountdown -= dt;
            if (sampleCountdown <= 0) RefreshAlignment();
            if (illumination != null && illumination.Ready && Alignment >= MatchThreshold)
            {
                missedTime = 0; matchTime += dt;
                if (MatchProgress >= 1) { Revealing = true; revealTime = 0; game?.Tell("그림자가 붓결을 따라 그림으로 남습니다.", 4); }
            }
            else if (illumination == null || !illumination.Ready) { matchTime = 0; missedTime = 0; }
            else { missedTime += dt; if (missedTime > .2f) matchTime = Mathf.Max(0, matchTime - dt * 2); }
        }

        public void ResetPuzzle()
        {
            Completed = Revealing = false; matchTime = revealTime = missedTime = 0; sampleCountdown = 0; Alignment = 0;
            haveLiveMatrix = false;
            UpdateMaterial();
        }

        RenderTexture CreateTexture(int resolution, int depth, bool mask, string title)
        {
            var format = mask && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
            var texture = new RenderTexture(resolution, resolution, depth, format, RenderTextureReadWrite.Linear)
            { name = title, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            texture.Create(); return texture;
        }

        void EnsureTextures()
        {
            if (projector != null) return;
            projector = new Material(Shader.Find("Parallax/MuseumNikeCanvas")) { hideFlags = HideFlags.DontSave };
            targetMask = CreateTexture(TargetResolution, 0, true, "Nike · full scan target contour 4096");
            paintedScan = CreateTexture(TargetResolution, 0, false, "Nike · canonical full scan painted view 4096");
            liveMask = CreateTexture(LiveResolution, 0, true, "Nike · exact carried sculpture wall preview");
            projectedDepth = new RenderTexture(TargetResolution, TargetResolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            { name = "Nike · canonical source-ray linear depth", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            projectedDepth.Create(); projector.SetTexture("_ProjectedDepth", projectedDepth);
        }

        void RenderProjection(RenderTexture target, Matrix4x4 matrix, int pass)
        {
            projector.SetVector("_Source", source.position);
            projector.SetVector("_PlaneCenter", TargetCenter);
            projector.SetVector("_PlaneNormal", TargetNormal);
            projector.SetVector("_PlaneRight", TargetCanvas.right); projector.SetVector("_PlaneUp", TargetCanvas.up);
            projector.SetVector("_CanvasSize", new Vector4(Size.x, Size.y, 0, 0));
            var command = new CommandBuffer { name = "Nike · project original mesh onto canvas" };
            var previous = RenderTexture.active;
            try
            {
                command.SetRenderTarget(target); command.ClearRenderTarget(false, true, pass == 3 ? Color.white : Color.clear);
                command.DrawMesh(scan.sharedMesh, matrix, projector, 0, pass);
                Graphics.ExecuteCommandBuffer(command);
            }
            finally { command.Release(); RenderTexture.active = previous; }
        }

        void UpdateMaterial()
        {
            if (overlay == null || overlay.sharedMaterial == null) return;
            if (display == null) display = new MaterialPropertyBlock();
            overlay.GetPropertyBlock(display);
            display.SetTexture("_TargetMask", targetMask == null ? (Texture)Texture2D.blackTexture : targetMask);
            display.SetTexture("_LiveMask", liveMask == null ? (Texture)Texture2D.blackTexture : liveMask);
            display.SetTexture("_ArtTex", artwork != null ? (Texture)artwork : paintedScan != null ? (Texture)paintedScan : Texture2D.whiteTexture);
            display.SetVector("_TargetMask_TexelSize", new Vector4(1f / TargetResolution, 1f / TargetResolution, TargetResolution, TargetResolution));
            display.SetFloat("_Reveal", RevealProgress); display.SetFloat("_Finished", Completed ? 1 : 0);
            display.SetFloat("_Preview", !Completed && !Revealing && illumination != null && !illumination.Ready ? 1 : 0);
            overlay.SetPropertyBlock(display);
        }

        void LateUpdate()
        {
            if (!FindScan() || source == null || overlay == null) return;
            EnsureReference(); EnsureTextures();
            if (!renderedReference)
            {
                RenderProjection(targetMask, CanonicalMatrix, 1);
                RenderProjection(projectedDepth, CanonicalMatrix, 3);
                RenderProjection(paintedScan, CanonicalMatrix, 2);
                renderedReference = true;
            }
            Matrix4x4 current = scan.transform.localToWorldMatrix;
            if (!haveLiveMatrix || current != lastLiveMatrix)
            { RenderProjection(liveMask, current, 1); lastLiveMatrix = current; haveLiveMatrix = true; }
            UpdateMaterial();
        }

        void OnDestroy()
        {
            foreach (var texture in new[] { targetMask, liveMask, paintedScan, projectedDepth })
                if (texture != null) { texture.Release(); Destroy(texture); }
            if (projector != null) Destroy(projector);
        }
    }
}
