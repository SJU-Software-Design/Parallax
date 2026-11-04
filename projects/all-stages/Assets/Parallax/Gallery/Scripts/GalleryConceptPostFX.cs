using UnityEngine;

namespace ShadeLink.Gallery
{
    // Main-camera image treatment only. Diagnostic and shadow-mask cameras do
    // not acquire this component, so their 0/1 gameplay comparisons stay exact.
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class GalleryConceptPostFX : MonoBehaviour
    {
        public bool EffectEnabled=true;
        public bool DebugOcclusion;
        [Range(0,1)] public float OcclusionStrength=.55f;
        [Range(.08f,1.5f)] public float OcclusionRadius=.55f;
        [Range(.001f,.2f)] public float OcclusionBias=.035f;
        [Range(.4f,2)] public float Exposure=1;
        [Range(0,1)] public float ToneMappingStrength=.60f;
        [Range(.5f,1.5f)] public float Saturation=1.015f;
        [Range(0,.2f)] public float BloomStrength=.035f;
        [Range(.5f,3)] public float BloomThreshold=1.05f;

        Camera targetCamera;
        Material treatment;
        bool originalHDR,configured;
        DepthTextureMode originalDepth;

        void OnEnable()
        {
            targetCamera=GetComponent<Camera>();
            originalHDR=targetCamera.allowHDR;originalDepth=targetCamera.depthTextureMode;
            targetCamera.allowHDR=true;
            targetCamera.depthTextureMode|=DepthTextureMode.DepthNormals;
            configured=true;
        }

        bool Prepare()
        {
            if(targetCamera==null)targetCamera=GetComponent<Camera>();
            if(treatment==null)
            {
                var shader=Shader.Find("Parallax/GalleryConceptPostFX");
                if(shader==null||!shader.isSupported)return false;
                treatment=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            }
            return true;
        }

        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!EffectEnabled||!Prepare())
            {Graphics.Blit(source,destination);return;}
            RenderTexture half=null;
            try
            {
                // Exactly one temporary half-resolution buffer for both effects.
                var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                    ?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32;
                half=RenderTexture.GetTemporary(Mathf.Max(1,source.width/2),Mathf.Max(1,source.height/2),0,format,RenderTextureReadWrite.Linear);
                half.filterMode=FilterMode.Bilinear;half.wrapMode=TextureWrapMode.Clamp;
                float tangent=Mathf.Tan(targetCamera.fieldOfView*Mathf.Deg2Rad*.5f);
                treatment.SetVector("_CameraProjection",new Vector4(tangent*targetCamera.aspect,tangent,targetCamera.farClipPlane,0));
                treatment.SetVector("_Occlusion",new Vector4(OcclusionRadius,OcclusionStrength,OcclusionBias,0));
                treatment.SetVector("_Tone",new Vector4(Exposure,ToneMappingStrength,Saturation,BloomStrength));
                treatment.SetFloat("_BloomThreshold",BloomThreshold);
                treatment.SetFloat("_DebugOcclusion",DebugOcclusion?1:0);
                Graphics.Blit(source,half,treatment,0);
                treatment.SetTexture("_AmbientBloom",half);
                Graphics.Blit(source,destination,treatment,1);
            }
            finally
            {
                if(treatment!=null)treatment.SetTexture("_AmbientBloom",null);
                if(half!=null)RenderTexture.ReleaseTemporary(half);
            }
        }

        void OnDisable()
        {
            if(configured&&targetCamera!=null)
            {
                targetCamera.allowHDR=originalHDR;
                targetCamera.depthTextureMode=originalDepth;
            }
            configured=false;ReleaseMaterial();
        }
        void OnDestroy(){ReleaseMaterial();}
        void ReleaseMaterial()
        {
            if(treatment==null)return;
            if(Application.isPlaying)Destroy(treatment);else DestroyImmediate(treatment);
            treatment=null;
        }
    }
}
