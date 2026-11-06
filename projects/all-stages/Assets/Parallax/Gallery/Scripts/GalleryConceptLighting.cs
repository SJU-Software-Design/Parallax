using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    public sealed class GalleryConceptLighting : MonoBehaviour
    {
        public GalleryGame game;
        public ReflectionProbe galleryProbe;
        public ReflectionProbe[] roomProbes;
        RenderTexture[] capturedRooms;
        static readonly Vector4[] Positions={
            new Vector4(-4.8f,6.2f,11.5f,12),new Vector4(4.8f,6.2f,11.5f,12),
            new Vector4(-5.9f,6.5f,5.5f,11),new Vector4(5.9f,6.5f,5.5f,11),
            new Vector4(-1,6.8f,19,13),new Vector4(5.5f,5.8f,23,11),
            new Vector4(0,6.3f,36,13),new Vector4(0,6.4f,51,12)
        };
        static readonly Vector3[] Targets={
            new Vector3(-4.9f,2.8f,13.8f),new Vector3(4.9f,2.8f,13.8f),
            new Vector3(-7.8f,2.7f,5.5f),new Vector3(7.8f,2.7f,5.5f),
            new Vector3(0,1.7f,21.65f),new Vector3(7.7f,2.4f,23),
            new Vector3(0,2.5f,38),new Vector3(0,1.5f,52)
        };
        public static void Configure(GalleryGame game)
        {
            var owner=game.gameObject.AddComponent<GalleryConceptLighting>();owner.game=game;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.60f,.65f,.71f);
            RenderSettings.ambientEquatorColor=new Color(.47f,.45f,.40f);
            RenderSettings.ambientGroundColor=new Color(.31f,.27f,.21f);
            RenderSettings.reflectionIntensity=.75f;RenderSettings.defaultReflectionResolution=128;
            RenderSettings.fog=false;
            var sky=new Material(Shader.Find("Skybox/Procedural")){name="Concept museum · daylight sky"};
            sky.SetColor("_SkyTint",new Color(.63f,.71f,.81f));sky.SetColor("_GroundColor",new Color(.48f,.43f,.34f));
            sky.SetFloat("_AtmosphereThickness",.75f);sky.SetFloat("_Exposure",1.05f);
            RenderSettings.skybox=sky;GalleryWorld.Materials.Add(sky);
            var sun=new GameObject("Museum daylight direction").AddComponent<Light>();sun.transform.SetParent(game.transform,false);
            sun.type=LightType.Directional;sun.transform.rotation=Quaternion.LookRotation(-GalleryLighting.ToSun);
            sun.color=new Color(1,.88f,.70f);sun.intensity=1.1f;sun.shadows=LightShadows.Soft;
            sun.shadowResolution=LightShadowResolution.VeryHigh;sun.shadowBias=.025f;sun.shadowNormalBias=.12f;sun.shadowStrength=1;RenderSettings.sun=sun;
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=75;QualitySettings.shadowCascades=4;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;
            QualitySettings.realtimeReflectionProbes=true;
            foreach(var renderer in game.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.renderQueue>=3000)renderer.shadowCastingMode=ShadowCastingMode.Off;
                else if(renderer.shadowCastingMode!=ShadowCastingMode.TwoSided)renderer.shadowCastingMode=ShadowCastingMode.On;
                renderer.receiveShadows=true;
            }
            game.view.clearFlags=CameraClearFlags.Skybox;game.view.allowHDR=true;game.view.allowMSAA=true;
            owner.roomProbes=new ReflectionProbe[4];float[] ends={0,14,29,44,56};
            for(int room=0;room<4;room++)
            {
                var probeObject=new GameObject("Museum room "+(room+1)+" · local architecture reflection");probeObject.transform.SetParent(game.transform,false);
                probeObject.transform.localPosition=new Vector3(0,GalleryConceptWorld.Roof/2,(ends[room]+ends[room+1])/2);
                var probe=probeObject.AddComponent<ReflectionProbe>();owner.roomProbes[room]=probe;
                probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
                probe.renderDynamicObjects=true;
                probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;probe.resolution=128;probe.hdr=true;
                probe.size=new Vector3(16,GalleryConceptWorld.Roof,ends[room+1]-ends[room]);probe.center=Vector3.zero;
                probe.boxProjection=true;probe.intensity=1;probe.blendDistance=.6f;probe.importance=8;
                probe.nearClipPlane=.15f;probe.farClipPlane=60;
            }
            owner.galleryProbe=owner.roomProbes[1];
            game.view.gameObject.AddComponent<GalleryConceptPostFX>();
            owner.Publish();
        }
        void Awake()
        {
            QualitySettings.realtimeReflectionProbes=true;
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=75;QualitySettings.shadowCascades=4;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;
            Publish();
        }
        IEnumerator Start()
        {
            if(roomProbes==null||roomProbes.Length!=4)yield break;
            yield return null;yield return null;
            // Explicit renders also work when the validation player window is
            // hidden and Unity skips its normal probe scheduling loop.
            var captureObject=new GameObject("Museum room reflection capture");
            var capture=captureObject.AddComponent<Camera>();capture.enabled=false;
            capture.allowHDR=true;capture.allowMSAA=false;capture.clearFlags=CameraClearFlags.Skybox;
            capture.renderingPath=RenderingPath.Forward;capture.nearClipPlane=.15f;capture.farClipPlane=60;
            capture.cullingMask=game.view.cullingMask;capturedRooms=new RenderTexture[4];
            var hdr=new Vector4[4];var positions=new Vector4[4];var minimum=new Vector4[4];var maximum=new Vector4[4];
            try
            {
                for(int i=0;i<4;i++)
                {
                    var probe=roomProbes[i];
                    var cube=new RenderTexture(128,128,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
                    {name="Museum room "+(i+1)+" actual HDR environment",dimension=TextureDimension.Cube,useMipMap=true,autoGenerateMips=false};
                    cube.Create();capturedRooms[i]=cube;capture.transform.position=probe.transform.position;
                    if(!capture.RenderToCubemap(cube))throw new System.InvalidOperationException("Cannot capture museum room reflection "+i);
                    cube.GenerateMips();probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=cube;
                    Shader.SetGlobalTexture("_MuseumRoomEnvironment"+i,cube);
                    hdr[i]=new Vector4(1,1,0,0);Vector3 at=probe.transform.position;
                    positions[i]=new Vector4(at.x,at.y,at.z,1);minimum[i]=probe.bounds.min;maximum[i]=probe.bounds.max;
                }
            }
            finally { Destroy(captureObject); }
            Shader.SetGlobalVectorArray("_MuseumRoomEnvironmentHDR",hdr);Shader.SetGlobalVectorArray("_MuseumRoomProbePosition",positions);
            Shader.SetGlobalVectorArray("_MuseumRoomProbeBoxMin",minimum);Shader.SetGlobalVectorArray("_MuseumRoomProbeBoxMax",maximum);
            Shader.SetGlobalFloat("_MuseumProbeReady",1);Debug.Log("MUSEUM_ROOM_REFLECTION_OK rooms=4");
        }
        void Publish()
        {
            var colors=new Vector4[8];var directions=new Vector4[8];
            for(int i=0;i<8;i++)
            {
                Vector3 direction=(Targets[i]-(Vector3)Positions[i]).normalized;
                directions[i]=new Vector4(direction.x,direction.y,direction.z,Mathf.Cos(38*Mathf.Deg2Rad));
                colors[i]=new Vector4(1,.88f,.70f,i<4?4.0f:3.0f);
            }
            colors[4].w=3.8f; // Soft fill on the sculpture's near side.
            colors[5].w=4.2f; // Fill aimed at the Nike room's right wall.
            Shader.SetGlobalInt("_MuseumLightCount",8);
            Shader.SetGlobalVectorArray("_MuseumLightPositionRange",Positions);
            Shader.SetGlobalVectorArray("_MuseumLightColorIntensity",colors);
            Shader.SetGlobalVectorArray("_MuseumLightDirectionCone",directions);
            Shader.SetGlobalFloat("_MuseumUseUnityShadows",1);
            Shader.SetGlobalFloat("_MuseumUseRoomEnvironment",1);Shader.SetGlobalFloat("_MuseumProbeReady",0);
            Shader.SetGlobalFloat("_MuseumSunIntensity",4.5f);
            Shader.SetGlobalVector("_MuseumAmbientSky",new Vector4(.27f,.31f,.36f,1));
            Shader.SetGlobalVector("_MuseumAmbientGround",new Vector4(.12f,.095f,.07f,1));
        }
        void OnDisable()
        {
            Shader.SetGlobalInt("_MuseumLightCount",0);Shader.SetGlobalFloat("_MuseumUseUnityShadows",0);Shader.SetGlobalFloat("_MuseumSunIntensity",0);
            Shader.SetGlobalFloat("_MuseumUseRoomEnvironment",0);Shader.SetGlobalFloat("_MuseumProbeReady",0);
            if(capturedRooms!=null)foreach(var cube in capturedRooms)if(cube!=null){cube.Release();Destroy(cube);}
            capturedRooms=null;
        }
    }
}
