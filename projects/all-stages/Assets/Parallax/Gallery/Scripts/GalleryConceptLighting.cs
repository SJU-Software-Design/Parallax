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
        [SerializeField,Range(0,1)] float portraitMood;
        public float PortraitMood=>portraitMood;
        RenderTexture[] capturedRooms;
        readonly Vector4[] portraitRadiance=new Vector4[4];
        // Exhibition fills are isolated from the gameplay sun and from all
        // horizontal walking surfaces by MuseumSurface. They light walls/art.
        static readonly Vector4[] PortraitPositions={
            new Vector4(-6.7f,5.5f,35,6),new Vector4(6.7f,5.5f,37,6),
            new Vector4(0,6.15f,39,8),new Vector4(0,5.7f,32.8f,14)
        };
        static readonly Vector3[] PortraitTargets={
            new Vector3(-7.9f,2.7f,35),new Vector3(7.9f,2.7f,37),
            new Vector3(0,2.8f,43.45f),new Vector3(0,2.2f,37.8f)
        };
        static readonly Vector4[] Positions={
            new Vector4(-4.8f,6.2f,11.5f,12),new Vector4(4.8f,6.2f,11.5f,12),
            new Vector4(-5.9f,6.5f,5.5f,11),new Vector4(5.9f,6.5f,5.5f,11),
            new Vector4(-4.8f,5.4f,20.1f,13),new Vector4(5.5f,5.2f,20,12),
            new Vector4(-3.3f,5.6f,47.5f,13),new Vector4(3.6f,4.8f,49.8f,12)
        };
        static readonly Vector3[] Targets={
            new Vector3(-4.9f,2.8f,13.8f),new Vector3(4.9f,2.8f,13.8f),
            new Vector3(-7.8f,2.7f,5.5f),new Vector3(7.8f,2.7f,5.5f),
            new Vector3(0,2.0f,21.65f),new Vector3(2.5f,2.0f,24),
            new Vector3(0,3,52),new Vector3(0,2.6f,53)
        };
        public static void KeyPose(int room,out Vector3 position,out Vector3 target,out float range,out float fov)
        {
            int index=room==0?0:room==1?4:room==2?3:6;
            Vector4 source=room==2?PortraitPositions[index]:Positions[index];
            position=source;target=room==2?PortraitTargets[index]:Targets[index];range=source.w;
            fov=room==0?92:room==2?96:116;
        }
        public static void Configure(GalleryGame game)
        {
            var owner=game.gameObject.AddComponent<GalleryConceptLighting>();owner.game=game;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.52f,.48f,.42f);
            RenderSettings.ambientEquatorColor=new Color(.36f,.32f,.27f);
            RenderSettings.ambientGroundColor=new Color(.20f,.17f,.14f);
            RenderSettings.reflectionIntensity=.75f;RenderSettings.defaultReflectionResolution=128;
            RenderSettings.fog=false;
            var sky=new Material(Shader.Find("Skybox/Procedural")){name="Concept museum · daylight sky"};
            sky.SetColor("_SkyTint",new Color(.63f,.71f,.81f));sky.SetColor("_GroundColor",new Color(.48f,.43f,.34f));
            sky.SetFloat("_AtmosphereThickness",.75f);sky.SetFloat("_Exposure",1.05f);
            RenderSettings.skybox=sky;GalleryWorld.Materials.Add(sky);
            var sun=new GameObject("Museum daylight direction").AddComponent<Light>();sun.transform.SetParent(game.transform,false);
            sun.type=LightType.Directional;sun.transform.rotation=Quaternion.LookRotation(-GalleryLighting.ToSun);
            sun.color=new Color(1,.93f,.84f);sun.intensity=1.1f;sun.shadows=LightShadows.Soft;
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
                probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;probe.resolution=256;probe.hdr=true;
                probe.size=new Vector3(16,GalleryConceptWorld.Roof,ends[room+1]-ends[room]);probe.center=Vector3.zero;
                probe.boxProjection=true;probe.intensity=1;probe.blendDistance=.6f;probe.importance=8;
                probe.nearClipPlane=.15f;probe.farClipPlane=60;
            }
            owner.galleryProbe=owner.roomProbes[1];
            game.view.gameObject.AddComponent<GalleryConceptPostFX>();
            owner.InstallCoves();
            if(game.nikeLight!=null)
            {
                game.nikeLight.intensity=3.2f;
                game.nikeLight.lightColor=new Color(1,.65f,.32f);
            }
            game.gameObject.AddComponent<GalleryExhibitShadows>().game=game;
            owner.Publish();
        }
        public void SetPortraitMood(float red01)
        {
            portraitMood=Mathf.Clamp01(red01);
            Shader.SetGlobalFloat("_MuseumPortraitMood",portraitMood);
            for(int i=0;i<portraitRadiance.Length;i++)
            {
                Color red=i<2?new Color(1,.15f,.07f):new Color(1,.055f,.025f);
                Color color=Color.Lerp(new Color(1,.65f,.32f),red,portraitMood);
                float gain=Mathf.Lerp(1,i<2?11f:i==2?15f:18.5f,portraitMood);
                portraitRadiance[i]=new Vector4(color.r,color.g,color.b,(i<2?5f:7f)*gain);
            }
            Shader.SetGlobalVectorArray("_MuseumPortraitLightColorIntensity",portraitRadiance);
        }
        void InstallCoves()
        {
            var material=new Material(Shader.Find("Parallax/MuseumSurface"))
                {name="Museum · concealed warm cove diffuser",color=new Color(.72f,.64f,.53f)};
            material.SetColor("_EmissionColor",new Color(.45f,.34f,.23f));
            material.SetFloat("_ExhibitLight",0);material.SetFloat("_ReflectionStrength",0);
            GalleryWorld.Materials.Add(material);
            float[] ends={0,14,29,44,56};
            for(int room=0;room<4;room++)foreach(int side in new[]{-1,1})
            {
                GalleryConceptArchitecture.Block(game.transform,"Museum concealed side cove · "+room+" · "+side,
                    new Vector3(side*7.92f,6.47f,(ends[room]+ends[room+1])/2),
                    new Vector3(.025f,.018f,ends[room+1]-ends[room]-.60f),material,.004f);
                GalleryConceptArchitecture.Block(game.transform,"Museum concealed end cove · "+room+" · "+side,
                    new Vector3(0,6.47f,side<0?ends[room]+.12f:ends[room+1]-.12f),
                    new Vector3(15.48f,.018f,.025f),material,.004f);
            }
        }
        void Awake()
        {
            QualitySettings.realtimeReflectionProbes=true;
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=75;QualitySettings.shadowCascades=4;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;
            Publish();
        }
        IEnumerator Start()
        {
            QualitySettings.antiAliasing=4;QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
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
                    var cube=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
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
        void AlignPortraitFixtures()
        {
            var room=game!=null?game.GetComponentInChildren<GalleryPortraitRoom>(true):null;
            if(room==null||room.pictureDiffusers==null||room.pictureDiffusers.Length<2)return;
            for(int i=0;i<2;i++)
            {
                var painting=i==0?room.originalArtwork:room.installationArtwork;
                var diffuser=room.pictureDiffusers[i];
                if(painting==null||diffuser==null)continue;
                Vector3 position=diffuser.bounds.center;
                PortraitPositions[i]=new Vector4(position.x,position.y,position.z,6);
                PortraitTargets[i]=painting.position;
            }
        }
        void Publish()
        {
            AlignPortraitFixtures();
            var colors=new Vector4[8];var directions=new Vector4[8];
            for(int i=0;i<8;i++)
            {
                Vector3 direction=(Targets[i]-(Vector3)Positions[i]).normalized;
                directions[i]=new Vector4(direction.x,direction.y,direction.z,Mathf.Cos((i<4?46:58)*Mathf.Deg2Rad));
                colors[i]=new Vector4(1,.65f,.32f,i<4?5.5f:6.0f);
            }
            colors[4].w=11.5f;
            colors[5].w=4.2f;
            colors[6].w=8;
            Shader.SetGlobalInt("_MuseumLightCount",8);
            Shader.SetGlobalVectorArray("_MuseumLightPositionRange",Positions);
            Shader.SetGlobalVectorArray("_MuseumLightColorIntensity",colors);
            Shader.SetGlobalVectorArray("_MuseumLightDirectionCone",directions);
            var portraitDirections=new Vector4[PortraitPositions.Length];
            for(int i=0;i<PortraitPositions.Length;i++)
            {
                Vector3 direction=(PortraitTargets[i]-(Vector3)PortraitPositions[i]).normalized;
                portraitDirections[i]=new Vector4(direction.x,direction.y,direction.z,Mathf.Cos((i<2?70:48)*Mathf.Deg2Rad));
            }
            Shader.SetGlobalVectorArray("_MuseumPortraitLightPositionRange",PortraitPositions);
            Shader.SetGlobalVectorArray("_MuseumPortraitLightDirectionCone",portraitDirections);
            Shader.SetGlobalInt("_MuseumPortraitLightCount",PortraitPositions.Length);
            SetPortraitMood(portraitMood);
            Shader.SetGlobalFloat("_MuseumUseUnityShadows",1);
            Shader.SetGlobalFloat("_MuseumUseRoomEnvironment",1);Shader.SetGlobalFloat("_MuseumProbeReady",0);
            Shader.SetGlobalFloat("_MuseumSunIntensity",3.4f);
            Shader.SetGlobalVector("_MuseumAmbientSky",new Vector4(.16f,.14f,.10f,1));
            Shader.SetGlobalVector("_MuseumAmbientGround",new Vector4(.22f,.16f,.11f,1));
            Shader.SetGlobalVector("_MuseumCoveRadiance",new Vector4(.35f,.24f,.15f,1));
        }
        void OnDisable()
        {
            Shader.SetGlobalInt("_MuseumLightCount",0);Shader.SetGlobalFloat("_MuseumUseUnityShadows",0);Shader.SetGlobalFloat("_MuseumSunIntensity",0);
            Shader.SetGlobalInt("_MuseumPortraitLightCount",0);Shader.SetGlobalFloat("_MuseumPortraitMood",0);
            Shader.SetGlobalVector("_MuseumCoveRadiance",Vector4.zero);
            Shader.SetGlobalFloat("_MuseumUseRoomEnvironment",0);Shader.SetGlobalFloat("_MuseumProbeReady",0);
            if(capturedRooms!=null)foreach(var cube in capturedRooms)if(cube!=null){cube.Release();Destroy(cube);}
            capturedRooms=null;
        }
    }
}
