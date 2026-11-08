using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Display-light shadows are visual only; the sunlight/placement masks keep
    // their independent diagnostic path and the portrait floor stays unlit.
    [DefaultExecutionOrder(130)]
    public sealed class GalleryExhibitShadows : MonoBehaviour
    {
        public GalleryGame game;
        readonly RenderTexture[] maps=new RenderTexture[4];
        readonly Matrix4x4[] matrices=new Matrix4x4[4];
        readonly Vector4[] origins=new Vector4[4],directions=new Vector4[4],settings=new Vector4[4];
        Camera depthCamera;
        Shader depthShader;
        float refreshAt;
        int lastRoom=-1;

        void OnEnable(){if(Application.isPlaying&&game!=null)Initialize();}
        void Start(){if(depthCamera==null)Initialize();}
        void Initialize()
        {
            depthShader=Shader.Find("Hidden/Parallax/MuseumExhibitDepth");
            if(game==null||game.view==null||depthShader==null||!depthShader.isSupported||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
            {enabled=false;return;}
            var owner=new GameObject("Museum exhibition key · mesh shadow capture"){hideFlags=HideFlags.DontSave};
            owner.transform.SetParent(transform,false);
            depthCamera=owner.AddComponent<Camera>();depthCamera.enabled=false;
            depthCamera.renderingPath=RenderingPath.Forward;depthCamera.clearFlags=CameraClearFlags.SolidColor;
            depthCamera.backgroundColor=Color.white;depthCamera.nearClipPlane=.08f;depthCamera.aspect=1;
            depthCamera.allowHDR=false;depthCamera.allowMSAA=false;
            for(int room=0;room<4;room++)
            {
                int size=room==1?2048:1024;
                maps[room]=new RenderTexture(size,size,24,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear)
                {name="Museum exhibition key depth · "+room,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                maps[room].Create();RenderRoom(room);
            }
            Publish();Debug.Log("MUSEUM_EXHIBIT_SHADOWS_OK rooms=4");
        }

        void LateUpdate()
        {
            if(depthCamera==null)return;
            float z=game.view.transform.position.z;
            int room=z<14?0:z<29?1:z<44?2:3;
            bool moving=game.held!=null||room==2&&game.portraitGate!=null&&game.portraitGate.Performing||
                room==3&&game.orcusExit!=null&&game.orcusExit.cathedralActivation!=null&&
                game.orcusExit.cathedralActivation.Progress>0&&!game.orcusExit.cathedralActivation.Opened;
            foreach(var prop in game.props)
                if(prop!=null&&prop.zone==room&&prop.body!=null&&!prop.body.IsSleeping())moving=true;
            if(room!=lastRoom||moving||Time.unscaledTime>=refreshAt)
            {
                RenderRoom(room);lastRoom=room;refreshAt=Time.unscaledTime+.05f;
                Publish();
            }
        }

        void RenderRoom(int room)
        {
            GalleryConceptLighting.KeyPose(room,out var position,out var target,out float range,out float fov);
            depthCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            depthCamera.farClipPlane=range;depthCamera.fieldOfView=fov;depthCamera.targetTexture=maps[room];
            depthCamera.cullingMask=game.view.cullingMask;
            var previous=RenderTexture.active;
            try {depthCamera.RenderWithShader(depthShader,"RenderType");}
            finally {RenderTexture.active=previous;}
            matrices[room]=GL.GetGPUProjectionMatrix(depthCamera.projectionMatrix,true)*depthCamera.worldToCameraMatrix;
            Vector3 direction=depthCamera.transform.forward;
            origins[room]=new Vector4(position.x,position.y,position.z,range);
            directions[room]=new Vector4(direction.x,direction.y,direction.z,0);
            settings[room]=new Vector4(1f/maps[room].width,.009f/range,3.5f,SystemInfo.graphicsUVStartsAtTop?1:0);
            Shader.SetGlobalTexture("_MuseumExhibitShadow"+room,maps[room]);
        }

        void Publish()
        {
            Shader.SetGlobalMatrixArray("_MuseumExhibitShadowMatrix",matrices);
            Shader.SetGlobalVectorArray("_MuseumExhibitShadowOrigin",origins);
            Shader.SetGlobalVectorArray("_MuseumExhibitShadowDirection",directions);
            Shader.SetGlobalVectorArray("_MuseumExhibitShadowSettings",settings);
            Shader.SetGlobalFloat("_MuseumExhibitShadowReady",1);
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat("_MuseumExhibitShadowReady",0);
            if(depthCamera!=null){depthCamera.targetTexture=null;Destroy(depthCamera.gameObject);depthCamera=null;}
            for(int room=0;room<4;room++)if(maps[room]!=null)
            {
                Shader.SetGlobalTexture("_MuseumExhibitShadow"+room,Texture2D.whiteTexture);
                maps[room].Release();Destroy(maps[room]);maps[room]=null;
            }
        }
    }
}
