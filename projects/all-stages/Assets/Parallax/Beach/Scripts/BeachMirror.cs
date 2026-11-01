using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachMirror : MonoBehaviour
    {
        public BeachGame game;
        public BeachProp prop;
        public Light beam;
        public bool Illuminated {get;private set;}
        public float WorldWidth=>halfSize.x*2*transform.lossyScale.x;
        public float ConeAngle=>2*Mathf.Atan(WorldWidth*.5f/8)*Mathf.Rad2Deg;
        public Vector3 LightOrigin=>transform.position+ReflectedDirection*.16f;
        public void UpdateLight()
        {
            Illuminated=game!=null&&game.lighting!=null&&prop.placement!=null&&prop.placement.Ready&&game.lighting.MirrorReceivesSun(transform.position);
            if(beam==null)return;
            beam.enabled=Illuminated;beam.transform.SetPositionAndRotation(LightOrigin,Quaternion.LookRotation(ReflectedDirection));beam.spotAngle=ConeAngle;
        }
        public Vector2 halfSize = new Vector2(1.6f, 1.6f);
        public Quaternion alignedRotation;
        public float angle, homeAngle = -18;
        public Renderer face;
        Camera reflectionCamera;
        RenderTexture reflectionTexture;
        Material mirrorMaterial, rayMaterial;
        LineRenderer incoming, outgoing;
        public Vector3 ReflectedDirection => Vector3.Reflect(-BeachLighting.ToSun, transform.forward).normalized;
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
            var block = new MaterialPropertyBlock();
            block.SetColor("_Selection", selected ? new Color(.02f, .15f, .13f) : Color.black);
            face.SetPropertyBlock(block);
        }
        void Start()
        {
            beam=new GameObject("Reflected sunlight").AddComponent<Light>();beam.type=LightType.Spot;beam.range=32;beam.intensity=4.5f;beam.color=new Color(1,.9f,.67f);beam.shadows=LightShadows.Soft;beam.shadowBias=.015f;beam.shadowNormalBias=.03f;
            mirrorMaterial = new Material(prop.placement.OriginalMaterial(face,0));prop.placement.ReplaceOriginalMaterial(face,0,mirrorMaterial);
            reflectionTexture = new RenderTexture(640, 640, 24) { name = "Beach mirror view" };
            mirrorMaterial.SetTexture("_MirrorTex", reflectionTexture);
            reflectionCamera = new GameObject("Beach planar reflection camera").AddComponent<Camera>();
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
            rayMaterial.color=prop.placement.Ready?new Color(1,.72f,.28f):new Color(.08f,.65f,1);
            Vector3 p = transform.position, d = ReflectedDirection;
            if (lit)
            {
                incoming.SetPosition(0, p + BeachLighting.ToSun * 16);
                incoming.SetPosition(1, p);
                float length = 22;
                foreach (var hit in Physics.RaycastAll(p + d * .08f, d, length, BeachWorld.PhysicalMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<BeachMirror>() == this || hit.collider.GetComponentInParent<BeachProp>() == prop) continue;
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
            if(beam!=null)Destroy(beam.gameObject);
        }
    }
}
