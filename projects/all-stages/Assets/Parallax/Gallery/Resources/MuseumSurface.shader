Shader "Parallax/MuseumSurface"
{
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Base color", 2D) = "white" {}
        _Selection ("Interaction accent", Color) = (0,0,0,0)
        _Grid ("Floor tile joints", Float) = 0
        _Stone ("Stone grain", Float) = 0
        _Sheen ("Stone sheen", Float) = 0
        _ExhibitLight ("Display light fill", Float) = 0
        _SurfaceAlbedo ("World surface albedo", 2D) = "white" {}
        _SurfaceNormal ("World RGB OpenGL normal", 2D) = "bump" {}
        _SurfaceRoughness ("World roughness", 2D) = "white" {}
        _SurfaceOcclusion ("World occlusion", 2D) = "white" {}
        _UseSurfaceMaps ("World surface maps", Float) = 0
        _WorldTiling ("Texture repeats per metre", Float) = .6667
        _NormalStrength ("Normal relief", Range(0,2)) = .65
        _Roughness ("Roughness", Range(.08,1)) = .65
        _Metallic ("Metallic", Range(0,1)) = 0
        _ReflectionStrength ("Environment reflection", Range(0,1)) = .3
        _AmbientStrength ("Ambient fill", Range(0,2)) = 1
        _ContactAO ("Contact occlusion", Range(0,1)) = .55
        _ContactDistance ("Contact reach metres", Range(.05,1.5)) = .65
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _PortalGlowPosition ("Sculpture window light position", Vector) = (0,0,0,0)
        _PortalGlowColor ("Sculpture window light", Color) = (0,0,0,0)
        _PortalGlowRadius ("Sculpture window light reach", Float) = 1
        _Diagnostic ("Sun visibility diagnostic", Float) = 0
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            Tags { "LightMode"="ForwardBase" }
            Cull Off
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "UnityCG.cginc"
            #include "UnityStandardUtils.cginc"
            #include "UnityImageBasedLighting.cginc"
            #include "AutoLight.cginc"
            sampler2D _MainTex, _SurfaceAlbedo, _SurfaceNormal, _SurfaceRoughness, _SurfaceOcclusion;
            float4 _MainTex_ST, _Color, _Selection;
            float _Grid, _Diagnostic, _Stone, _Sheen, _ExhibitLight;
            float _UseSurfaceMaps, _WorldTiling, _NormalStrength, _Roughness, _Metallic;
            float _ReflectionStrength, _AmbientStrength, _ContactAO, _ContactDistance;
            float4 _EmissionColor;
            float4 _PortalGlowPosition, _PortalGlowColor;
            float _PortalGlowRadius;
            float4 _MuseumAmbientSky, _MuseumAmbientGround;
            float _MuseumUseUnityShadows,_MuseumSunIntensity;
            float _MuseumUseRoomEnvironment,_MuseumProbeReady;
            UNITY_DECLARE_TEXCUBE(_MuseumRoomEnvironment0);
            UNITY_DECLARE_TEXCUBE(_MuseumRoomEnvironment1);
            UNITY_DECLARE_TEXCUBE(_MuseumRoomEnvironment2);
            UNITY_DECLARE_TEXCUBE(_MuseumRoomEnvironment3);
            float4 _MuseumRoomEnvironmentHDR[4],_MuseumRoomProbePosition[4];
            float4 _MuseumRoomProbeBoxMin[4],_MuseumRoomProbeBoxMax[4];
            int _MuseumLightCount;
            float4 _MuseumLightPositionRange[8], _MuseumLightColorIntensity[8], _MuseumLightDirectionCone[8];
            int _GalleryCasterCount;
            float4x4 _GalleryCasterInverse[128];
            float _GalleryCasterPreview[128];
            float _GalleryCasterMirror[128];
            float _GalleryMirrorActive;
            float4x4 _GalleryMirrorInverse;
            float3 _GalleryMirrorPosition, _GalleryMirrorNormal, _GalleryReflectedDirection;
            float2 _GalleryMirrorHalf;
            float4 _GalleryWindows[4];
            float3 _GalleryToSun;
            float _GalleryRoof;
            struct appdata { float4 vertex: POSITION; float3 normal: NORMAL; float2 uv: TEXCOORD0; };
            struct v2f { float4 pos: SV_POSITION; float3 world: TEXCOORD0; float3 normal: TEXCOORD1; float2 uv: TEXCOORD2; SHADOW_COORDS(3) };
            v2f vert(appdata v) {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal); o.uv = TRANSFORM_TEX(v.uv,_MainTex);
                TRANSFER_SHADOW(o);return o;
            }
            bool windowLit(float3 location) {
                float3 aperture = location + _GalleryToSun * ((_GalleryRoof-location.y)/_GalleryToSun.y);
                bool inWindow = false;
                [unroll] for(int w=0;w<4;w++) {
                    float4 r = _GalleryWindows[w];
                    inWindow = inWindow || (aperture.x>=r.x && aperture.x<=r.y && aperture.z>=r.z && aperture.z<=r.w);
                }
                return inWindow;
            }
            bool blocked(float3 origin,float3 direction,float maximum,bool skipMirror,bool previewOnly) {
                [loop] for(int i=0;i<_GalleryCasterCount;i++) {
                    if((_GalleryCasterPreview[i]>.5)!=previewOnly)continue;
                    if(skipMirror && _GalleryCasterMirror[i]>.5)continue;
                    float3 o=mul(_GalleryCasterInverse[i],float4(origin,1)).xyz;
                    float3 d=mul((float3x3)_GalleryCasterInverse[i],direction);
                    d=float3(abs(d.x)<.000001?(d.x<0?-.000001:.000001):d.x,
                             abs(d.y)<.000001?(d.y<0?-.000001:.000001):d.y,
                             abs(d.z)<.000001?(d.z<0?-.000001:.000001):d.z);
                    float3 a=(-.5-o)/d, b=(.5-o)/d;
                    float3 low=min(a,b), high=max(a,b);
                    float enter=max(max(low.x,low.y),low.z), leave=min(min(high.x,high.y),high.z);
                    if(leave>=max(enter,.001) && enter<maximum) return true;
                }
                return false;
            }
            float directLit(float3 surfacePosition,float3 normal) {
                if(!windowLit(surfacePosition))return 0;
                return blocked(surfacePosition+normal*.012,_GalleryToSun,1e20,false,false)?0:1;
            }
            float reflectedLit(float3 surfacePosition,float3 normal) {
                if(_GalleryMirrorActive<.5)return 0;
                float denominator=dot(_GalleryReflectedDirection,_GalleryMirrorNormal);
                if(dot(_GalleryMirrorNormal,_GalleryToSun)<=.015 || denominator<=.015)return 0;
                float distance=dot(surfacePosition-_GalleryMirrorPosition,_GalleryMirrorNormal)/denominator;
                if(distance<=.03 || distance>32)return 0;
                float3 reflection=surfacePosition-_GalleryReflectedDirection*distance;
                float3 local=mul(_GalleryMirrorInverse,float4(reflection,1)).xyz;
                if(abs(local.x)>_GalleryMirrorHalf.x || abs(local.y)>_GalleryMirrorHalf.y || !windowLit(reflection))return 0;
                if(blocked(reflection+_GalleryMirrorNormal*.012,_GalleryToSun,1e20,true,false))return 0;
                return blocked(surfacePosition+normal*.012,-_GalleryReflectedDirection,distance-.025,true,false)?0:1;
            }
            float3 surfaceWeights(float3 normal) {
                float3 weights=pow(abs(normal),8);
                return weights/max(dot(weights,float3(1,1,1)),.0001);
            }
            float4 worldSample(sampler2D source,float3 position,float3 normal,float3 weights) {
                float3 signs=step(0,normal)*2-1;
                return tex2D(source,float2(-position.z*signs.x,position.y)*_WorldTiling)*weights.x+
                    tex2D(source,float2(position.x,-position.z*signs.y)*_WorldTiling)*weights.y+
                    tex2D(source,float2(position.x*signs.z,position.y)*_WorldTiling)*weights.z;
            }
            float3 surfaceNormal(float3 position,float3 geometric,float3 weights) {
                float3 signs=step(0,geometric)*2-1;
                float3 x=tex2D(_SurfaceNormal,float2(-position.z*signs.x,position.y)*_WorldTiling).xyz*2-1;
                float3 y=tex2D(_SurfaceNormal,float2(position.x,-position.z*signs.y)*_WorldTiling).xyz*2-1;
                float3 z=tex2D(_SurfaceNormal,float2(position.x*signs.z,position.y)*_WorldTiling).xyz*2-1;
                float3 nx=float3(geometric.x*abs(x.z),geometric.y+x.y*_NormalStrength,geometric.z-x.x*signs.x*_NormalStrength);
                float3 ny=float3(geometric.x+y.x*_NormalStrength,geometric.y*abs(y.z),geometric.z-y.y*signs.y*_NormalStrength);
                float3 nz=float3(geometric.x+z.x*signs.z*_NormalStrength,geometric.y+z.y*_NormalStrength,geometric.z*abs(z.z));
                return normalize(nx*weights.x+ny*weights.y+nz*weights.z);
            }
            float contactOcclusion(float3 position) {
                if(_ContactAO<.001)return 1;
                float occlusion=0;
                [loop] for(int caster=0;caster<_GalleryCasterCount;caster++) {
                    if(_GalleryCasterPreview[caster]>.5)continue;
                    float4x4 inverse=_GalleryCasterInverse[caster];
                    float3 localPosition=mul(inverse,float4(position,1)).xyz;
                    float3 outside=max(abs(localPosition)-.5,0);
                    float3 scale=float3(rcp(max(length(inverse[0].xyz),.001)),rcp(max(length(inverse[1].xyz),.001)),rcp(max(length(inverse[2].xyz),.001)));
                    float distance=length(outside*scale);
                    // The surface of this same collision shape is not ambient occlusion.
                    if(distance<.018||distance>=_ContactDistance)continue;
                    float proximity=1-distance/_ContactDistance;
                    occlusion=max(occlusion,proximity*proximity);
                }
                return 1-occlusion*_ContactAO*.52;
            }
            float3 fresnel(float cosine,float3 f0) {
                return f0+(1-f0)*pow(1-saturate(cosine),5);
            }
            float3 brdf(float3 n,float3 view,float3 light,float3 albedo,float roughness,float metallic) {
                float nl=saturate(dot(n,light)),nv=max(.035,saturate(dot(n,view)));
                float3 halfVector=normalize(view+light);
                float nh=saturate(dot(n,halfVector)),vh=saturate(dot(view,halfVector));
                float alpha=roughness*roughness,alpha2=alpha*alpha;
                float denominator=nh*nh*(alpha2-1)+1;
                float distribution=alpha2/(3.14159265*denominator*denominator+.0001);
                float k=(roughness+1)*(roughness+1)*.125;
                float geometry=(nv/(nv*(1-k)+k))*(nl/(nl*(1-k)+k));
                float3 f0=lerp(float3(.04,.04,.04),albedo,metallic);
                float3 f=fresnel(vh,f0);
                float3 diffuse=(1-f)*(1-metallic)*albedo/3.14159265;
                float3 specular=distribution*geometry*f/max(4*nv*nl,.001);
                return (diffuse+specular)*nl;
            }
            float3 museumEnvironment(float3 position,float3 view,float3 n,float roughness,float3 f0,float3 sky,float3 ground) {
                Unity_GlossyEnvironmentData glossy=UnityGlossyEnvironmentSetup(1-roughness,view,n,f0);
                float3 originalDirection=glossy.reflUVW;
                float3 environment;
                if(_MuseumUseRoomEnvironment>.5 && _MuseumProbeReady>.5) {
                    int room=position.z<14?0:(position.z<29?1:(position.z<44?2:3));
                    glossy.reflUVW=BoxProjectedCubemapDirection(originalDirection,position,
                        _MuseumRoomProbePosition[room],_MuseumRoomProbeBoxMin[room],_MuseumRoomProbeBoxMax[room]);
                    // Keep each texture/sampler pair explicit on all Built-in
                    // backends; the CPU publishes the completed probe textures.
                    if(room==0)environment=Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(_MuseumRoomEnvironment0),_MuseumRoomEnvironmentHDR[0],glossy);
                    else if(room==1)environment=Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(_MuseumRoomEnvironment1),_MuseumRoomEnvironmentHDR[1],glossy);
                    else if(room==2)environment=Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(_MuseumRoomEnvironment2),_MuseumRoomEnvironmentHDR[2],glossy);
                    else environment=Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(_MuseumRoomEnvironment3),_MuseumRoomEnvironmentHDR[3],glossy);
                } else {
                    glossy.reflUVW=BoxProjectedCubemapDirection(originalDirection,position,
                        unity_SpecCube0_ProbePosition,unity_SpecCube0_BoxMin,unity_SpecCube0_BoxMax);
                    environment=Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(unity_SpecCube0),unity_SpecCube0_HDR,glossy);
                }
                // A probe can be temporarily unbound while its six faces are
                // rendering. Preserve a real coloured specular response during
                // that interval instead of turning conducting surfaces black.
                // This also makes the first probe capture contain visible metal.
                if(_MuseumUseRoomEnvironment>.5 && dot(environment,float3(.2126,.7152,.0722))<.002) {
                    float3 broadDirection=lerp(originalDirection,n,roughness*roughness);
                    environment=lerp(ground,sky,saturate(broadDirection.y*.5+.5));
                }
                return max(environment,0);
            }
            float4 frag(v2f i,float facing:VFACE): SV_Target {
                // The visible sun mask uses exactly the geometric normal and
                // ray bias used by the CPU gameplay rules. PBR normal maps never
                // alter whether a foot position is safe.
                float3 geometric=normalize(i.normal);
                float sunlight=directLit(i.world,geometric),reflection=reflectedLit(i.world,geometric);float lit=max(sunlight,reflection);
                if(_Diagnostic>.5) return float4(lit,lit,lit,1);
                if(facing<0) {
                    geometric=-geometric;
                    sunlight=directLit(i.world,geometric);reflection=reflectedLit(i.world,geometric);lit=max(sunlight,reflection);
                }
                // Native mesh shadows supply the detailed scan/column/lantern
                // silhouette. This visual multiplier never changes Diagnostic
                // or the CPU's analytic sunlight and reflected-light decision.
                float meshShadow=_MuseumUseUnityShadows>.5?SHADOW_ATTENUATION(i):1;
                float visibleLit=sunlight>.5?sunlight*meshShadow:reflection;
                float3 albedo=tex2D(_MainTex,i.uv).rgb*_Color.rgb;
                float3 n=geometric;
                float roughness=_Roughness;
                float ao=1;
                if(_UseSurfaceMaps>.5) {
                    float3 weights=surfaceWeights(geometric);
                    albedo*=worldSample(_SurfaceAlbedo,i.world,geometric,weights).rgb;
                    n=surfaceNormal(i.world,geometric,weights);
                    roughness=.12+worldSample(_SurfaceRoughness,i.world,geometric,weights).r*_Roughness;
                    ao=worldSample(_SurfaceOcclusion,i.world,geometric,weights).r;
                }
                roughness=clamp(roughness,.16,.96);
                float3 view=normalize(_WorldSpaceCameraPos.xyz-i.world);
                float3 sky=dot(_MuseumAmbientSky.rgb,float3(1,1,1))>.001?_MuseumAmbientSky.rgb:float3(.41,.45,.49);
                float3 ground=dot(_MuseumAmbientGround.rgb,float3(1,1,1))>.001?_MuseumAmbientGround.rgb:float3(.25,.21,.16);
                float3 ambient=lerp(ground,sky,saturate(n.y*.5+.5));
                ao*=contactOcclusion(i.world);
                // Conductors receive indirect light through their coloured
                // specular response rather than an extra diffuse ambient term.
                float3 col=albedo*(1-_Metallic)*ambient*_AmbientStrength*ao;
                float3 toLight=reflection>.5&&sunlight<.5?-_GalleryReflectedDirection:_GalleryToSun;
                float3 sunColor=reflection>.5&&sunlight<.5?float3(1,.76,.46):float3(1,.88,.70);
                float sunIntensity=_MuseumSunIntensity>.001?_MuseumSunIntensity:2.8;
                col+=brdf(n,view,normalize(toLight),albedo,roughness,_Metallic)*sunColor*sunIntensity*visibleLit;
                [loop] for(int source=0;source<min(_MuseumLightCount,8);source++) {
                    float4 lamp=_MuseumLightPositionRange[source];
                    float3 delta=lamp.xyz-i.world;float distance=length(delta);
                    float3 direction=delta/max(distance,.001);
                    float attenuation=pow(saturate(1-distance/max(lamp.w,.01)),2);
                    float4 cone=_MuseumLightDirectionCone[source];
                    if(cone.w>=0)attenuation*=smoothstep(cone.w,min(.999,cone.w+.14),dot(-direction,normalize(cone.xyz)));
                    float4 radiance=_MuseumLightColorIntensity[source];
                    col+=brdf(n,view,direction,albedo,roughness,_Metallic)*radiance.rgb*radiance.w*attenuation;
                }
                float3 f0=lerp(float3(.04,.04,.04),albedo,_Metallic);
                float3 environmentColor=museumEnvironment(i.world,view,n,roughness,f0,sky,ground);
                // Match Unity's integrated GGX environment response. The
                // perceptual roughness remap/mip selection occurs in the helper.
                float realRoughness=roughness*roughness;
                #ifdef UNITY_COLORSPACE_GAMMA
                    float surfaceReduction=1-.28*realRoughness*roughness;
                #else
                    float surfaceReduction=rcp(1+realRoughness*realRoughness);
                #endif
                float grazing=saturate(1-roughness+max(f0.r,max(f0.g,f0.b)));
                col+=surfaceReduction*environmentColor*FresnelLerp(f0,grazing.xxx,saturate(dot(n,view)))*_ReflectionStrength*ao;
                col=lerp(col,max(col,albedo*.78),saturate(_ExhibitLight)*(1-visibleLit));
                if(_Grid>.5 && n.y>.9) {
                    float2 tile=abs(frac(i.world.xz/2)-.5);
                    float joint=step(.493,max(tile.x,tile.y)); col*=1-joint*.14;
                }
                bool predicted=sunlight>.5&&blocked(i.world+geometric*.012,_GalleryToSun,1e20,false,true);
                if(reflection>.5){float d=dot(i.world-_GalleryMirrorPosition,_GalleryMirrorNormal)/dot(_GalleryReflectedDirection,_GalleryMirrorNormal);predicted=predicted||blocked(i.world+geometric*.012,-_GalleryReflectedDirection,d-.025,true,true);}
                if(predicted){float stripe=step(.64,frac((i.world.x+i.world.y+i.world.z)*3));col=lerp(col,float3(.04,.65,1),.13+stripe*.60);}
                float3 windowDelta=_PortalGlowPosition.xyz-i.world;
                float windowDistance=length(windowDelta);
                float windowFalloff=pow(saturate(1-windowDistance/max(.01,_PortalGlowRadius)),2);
                float windowFacing=.3+.7*saturate(dot(n,windowDelta*rsqrt(max(.00001,dot(windowDelta,windowDelta)))));
                col+=albedo*_PortalGlowColor.rgb*windowFalloff*windowFacing;
                col+=_Selection.rgb+_EmissionColor.rgb;
                return float4(col,1);
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
}
