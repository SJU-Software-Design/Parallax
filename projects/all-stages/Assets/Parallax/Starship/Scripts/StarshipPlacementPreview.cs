using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Starship
{
    // Render only inactive original meshes into a sun-space depth map. The map
    // decorates receiving surfaces; it never participates in lighting/safety.
    public sealed class StarshipPlacementPreview:MonoBehaviour
    {
        public StarshipGame game;
        Camera depthCamera;RenderTexture target;Shader replacement;
        readonly List<Renderer> candidates=new List<Renderer>();
        readonly Dictionary<GameObject,int> layers=new Dictionary<GameObject,int>();
        const int PreviewLayer=30;
        void Awake()
        {
            replacement=Shader.Find("Parallax/PlacementDepth");
            target=new RenderTexture(1024,1024,24,RenderTextureFormat.RFloat){name="Inactive prop preview",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};target.Create();
            depthCamera=new GameObject("Placement preview camera").AddComponent<Camera>();depthCamera.enabled=false;depthCamera.gameObject.hideFlags=HideFlags.HideAndDontSave;
            depthCamera.orthographic=true;depthCamera.aspect=1;depthCamera.nearClipPlane=.1f;depthCamera.farClipPlane=150;depthCamera.clearFlags=CameraClearFlags.SolidColor;depthCamera.backgroundColor=Color.white;depthCamera.cullingMask=1<<PreviewLayer;depthCamera.targetTexture=target;depthCamera.allowHDR=false;depthCamera.allowMSAA=false;depthCamera.useOcclusionCulling=false;
        }
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            candidates.Clear();if(game==null)return;Bounds bounds=new Bounds();bool first=true;
            foreach(var prop in game.props)
            {
                if(prop.placement==null||prop.placement.Ready)continue;
                foreach(var renderer in prop.placement.Visuals)
                {
                    if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy||!prop.placement.OriginalCastsShadow(renderer))continue;
                    candidates.Add(renderer);if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
                }
            }
            Shader.SetGlobalFloat("_PlacementPreviewActive",first?0:1);if(first)return;
            depthCamera.transform.SetPositionAndRotation(bounds.center+StarshipLighting.ToSun*65,Quaternion.LookRotation(-StarshipLighting.ToSun));
            depthCamera.orthographicSize=Mathf.Max(4,bounds.extents.magnitude+1);Shader.SetGlobalFloat("_PlacementPreviewFar",depthCamera.farClipPlane);
            layers.Clear();
            try
            {
                foreach(var renderer in candidates)if(!layers.ContainsKey(renderer.gameObject)){layers.Add(renderer.gameObject,renderer.gameObject.layer);renderer.gameObject.layer=PreviewLayer;}
                depthCamera.RenderWithShader(replacement,"");
            }
            finally {foreach(var pair in layers)pair.Key.layer=pair.Value;}
            Shader.SetGlobalTexture("_PlacementPreviewMap",target);Shader.SetGlobalMatrix("_PlacementPreviewVP",GL.GetGPUProjectionMatrix(depthCamera.projectionMatrix,true)*depthCamera.worldToCameraMatrix);Shader.SetGlobalMatrix("_PlacementPreviewView",depthCamera.worldToCameraMatrix);
        }
        void OnDestroy(){Shader.SetGlobalFloat("_PlacementPreviewActive",0);if(depthCamera!=null)Destroy(depthCamera.gameObject);if(target!=null){target.Release();Destroy(target);}}
    }
}
