using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryMirror : MonoBehaviour
    {
        public GalleryGame game;
        public Vector2 halfSize = new Vector2(1.6f, 1.6f);
        public Quaternion alignedRotation;
        public float angle, homeAngle = -18;
        public Renderer face;
        Camera reflectionCamera;
        RenderTexture reflectionTexture;
        Material mirrorMaterial, rayMaterial;
        MaterialPropertyBlock highlightBlock;
        LineRenderer incoming, outgoing;
        public Vector3 ReflectedDirection => Vector3.Reflect(-GalleryLighting.ToSun, transform.forward).normalized;
        public void Restore() { angle = homeAngle; ApplyAngle(); }
        public void Turn(float degrees) { angle = Mathf.Clamp(angle + degrees, -32, 32); ApplyAngle(); }
        void ApplyAngle()
        {
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.up) * alignedRotation;
            Physics.SyncTransforms();
            if (game != null && game.lighting != null) game.lighting.Synchronize();
        }
        public void Highlight(bool selected)
        {
            if (face == null) return;
            if (highlightBlock == null) highlightBlock = new MaterialPropertyBlock();
            highlightBlock.SetColor("_Selection", selected ? new Color(.02f, .15f, .13f) : Color.black);
            face.SetPropertyBlock(highlightBlock);
        }
        void Start()
        {
            mirrorMaterial = new Material(face.sharedMaterial); face.sharedMaterial = mirrorMaterial;
            reflectionTexture = new RenderTexture(640, 640, 24) { name = "Gallery mirror view" };
            mirrorMaterial.SetTexture("_MirrorTex", reflectionTexture);
            reflectionCamera = new GameObject("Gallery planar reflection camera").AddComponent<Camera>();
            reflectionCamera.enabled = false; reflectionCamera.gameObject.hideFlags = HideFlags.HideAndDontSave;
            rayMaterial = new Material(Shader.Find("ShadeLink/Flat")) { color = new Color(1, .72f, .28f) };
            incoming = RayLine("Incoming sunlight"); outgoing = RayLine("Reflected sunlight");
        }
        LineRenderer RayLine(string title)
        {
            var line = new GameObject(title).AddComponent<LineRenderer>();line.transform.SetParent(transform, false);
            line.sharedMaterial = rayMaterial; line.positionCount = 2; line.useWorldSpace = true;
            line.startWidth = line.endWidth = .026f; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
        void LateUpdate()
        {
            if (game == null || game.view == null || incoming == null) return;
            bool lit = game.lighting.MirrorReceivesSun(transform.position);
            incoming.enabled = outgoing.enabled = lit;
            Vector3 p = transform.position, d = ReflectedDirection;
            if (lit)
            {
                incoming.SetPosition(0, p + GalleryLighting.ToSun * ((GalleryLighting.Roof - p.y) / GalleryLighting.ToSun.y));
                incoming.SetPosition(1, p);
                float length = 22;
                foreach (var hit in Physics.RaycastAll(p + d * .08f, d, length, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<GalleryMirror>() == this || hit.collider.GetComponent<GalleryLightGlass>() != null) continue;
                    length = Mathf.Min(length, hit.distance + .08f);
                }
                outgoing.SetPosition(0, p); outgoing.SetPosition(1, p + d * length);
            }
            if (Vector3.Distance(game.view.transform.position, p) > 19) return;
            RenderReflection();
        }
        void RenderReflection()
        {
            var source = game.view;
            Vector3 normal = transform.forward, point = transform.position;
            float planeDistance = -Vector3.Dot(normal, point);
            var reflection = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++) reflection[row, col] -= 2 * normal[row] * normal[col];
                reflection[row, 3] = -2 * planeDistance * normal[row];
            }
            reflectionCamera.CopyFrom(source); reflectionCamera.enabled = false;
            reflectionCamera.targetTexture = reflectionTexture;
            reflectionCamera.cullingMask = source.cullingMask & ~(1 << 31);
            reflectionCamera.transform.SetPositionAndRotation(reflection.MultiplyPoint(source.transform.position),
                Quaternion.LookRotation(Vector3.Reflect(source.transform.forward, normal), Vector3.Reflect(source.transform.up, normal)));
            reflectionCamera.worldToCameraMatrix = source.worldToCameraMatrix * reflection;
            float side = Vector3.Dot(source.transform.position - point, normal) >= 0 ? 1 : -1;
            Vector3 clipPoint = reflectionCamera.worldToCameraMatrix.MultiplyPoint(point + normal * .025f * side);
            Vector3 clipNormal = reflectionCamera.worldToCameraMatrix.MultiplyVector(normal * side).normalized;
            reflectionCamera.projectionMatrix = source.CalculateObliqueMatrix(new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal)));
            bool oldCulling = GL.invertCulling;
            try { GL.invertCulling = !oldCulling; reflectionCamera.Render(); }
            finally { GL.invertCulling = oldCulling; }
            mirrorMaterial.SetMatrix("_MirrorVP", GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, true) * reflectionCamera.worldToCameraMatrix);
        }
        void OnDestroy()
        {
            if (reflectionCamera != null) Destroy(reflectionCamera.gameObject);
            if (reflectionTexture != null) { reflectionTexture.Release(); Destroy(reflectionTexture); }
            if (mirrorMaterial != null) Destroy(mirrorMaterial);
            if (rayMaterial != null) Destroy(rayMaterial);
        }
    }
}
