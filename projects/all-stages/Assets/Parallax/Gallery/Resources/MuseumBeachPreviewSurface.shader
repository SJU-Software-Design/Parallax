Shader "Parallax/MuseumBeachPreviewSurface"
{
    Properties
    {
        _Color("Original beach tint",Color)=(1,1,1,1)
        _MainTex("Original beach albedo",2D)="white"{}
        _BumpMap("Original normal",2D)="bump"{}
        _BumpScale("Normal strength",Float)=1
        _OcclusionMap("Original occlusion",2D)="white"{}
        _MetallicGlossMap("Original metallic/smoothness",2D)="white"{}
        _SpecGlossMap("Original sand specular",2D)="black"{}
        _Metallic("Metallic",Float)=0
        _Glossiness("Smoothness",Float)=.2
        _EmissionColor("Original emission",Color)=(0,0,0,1)
        _Cutoff("Original leaf cutoff",Float)=.5
        _AlphaClip("Cutout",Float)=0
        _Projection("UV / triplanar / rippled sand",Float)=0
        _Tiling("Source repeats per metre",Float)=.5
        _SourceOrigin("Source origin",Vector)=(0,0,0,1)
        _SourceX("Source X",Vector)=(1,0,0,0)
        _SourceY("Source Y",Vector)=(0,1,0,0)
        _SourceZ("Source Z",Vector)=(0,0,1,0)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityStandardUtils.cginc"
            sampler2D _MainTex,_BumpMap,_OcclusionMap,_MetallicGlossMap,_SpecGlossMap;
            float4 _MainTex_ST,_Color,_EmissionColor,_SourceOrigin,_SourceX,_SourceY,_SourceZ;
            float _BumpScale,_Metallic,_Glossiness,_Cutoff,_AlphaClip,_Projection,_Tiling;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float3 source:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float3 world:TEXCOORD3;};
            float3 SourceDirection(float3 p){return _SourceX.xyz*p.x+_SourceY.xyz*p.y+_SourceZ.xyz*p.z;}
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);
                o.source=_SourceOrigin.xyz+SourceDirection(v.vertex.xyz);
                o.normal=SourceDirection(v.normal);o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;
            }
            float4 frag(v2f i,float face:VFACE):SV_Target
            {
                float3 normal=normalize(i.normal)*(face>=0?1:-1);
                float4 albedo=tex2D(_MainTex,i.uv)*_Color;
                float occlusion=tex2D(_OcclusionMap,i.uv).g;
                if(_Projection>.5)
                {
                    float3 weights=pow(abs(normal),4);weights/=max(.0001,weights.x+weights.y+weights.z);
                    float3 p=i.source*_Tiling;
                    float2 x=p.zy,y=p.xz,z=p.xy;
                    float3 nx=UnpackScaleNormal(tex2D(_BumpMap,x),_BumpScale);
                    float3 ny=UnpackScaleNormal(tex2D(_BumpMap,y),_BumpScale);
                    float3 nz=UnpackScaleNormal(tex2D(_BumpMap,z),_BumpScale);
                    float3 bump=float3(nx.z*sign(normal.x),nx.y,nx.x)*weights.x+
                        float3(ny.x,ny.z*sign(normal.y),ny.y)*weights.y+
                        float3(nz.x,nz.y,nz.z*sign(normal.z))*weights.z;
                    normal=normalize(bump);
                    if(_Projection>1.5){albedo=_Color;occlusion=1;}
                    else
                    {
                        albedo=(tex2D(_MainTex,x)*weights.x+tex2D(_MainTex,y)*weights.y+tex2D(_MainTex,z)*weights.z)*_Color;
                        occlusion=tex2D(_OcclusionMap,x).g*weights.x+tex2D(_OcclusionMap,y).g*weights.y+tex2D(_OcclusionMap,z).g*weights.z;
                    }
                }
                if(_AlphaClip>.5)clip(albedo.a-_Cutoff);
                // Independent daylight is expressed in the source beach's frame. Museum lights,
                // fog, reflection probes and placement-preview globals cannot tint this camera.
                float3 sun=normalize(float3(.24,1,-.42));
                float sky=saturate(normal.y*.5+.5);
                float3 ambient=lerp(float3(.39,.37,.30),float3(.63,.75,.80),sky)*.68;
                float3 light=ambient+float3(1,.94,.78)*saturate(dot(normal,sun))*.88;
                float3 localView=mul((float3x3)unity_WorldToObject,_WorldSpaceCameraPos-i.world);
                float3 view=normalize(SourceDirection(localView));
                float3 halfway=normalize(sun+view);
                float spec=pow(saturate(dot(normal,halfway)),lerp(12,96,saturate(_Glossiness)))*_Glossiness*.11;
                float3 color=albedo.rgb*light*lerp(.55,1,occlusion)+spec+_EmissionColor.rgb;
                float distance=length(_WorldSpaceCameraPos-i.world);
                float fog=1-exp(-distance*distance*.00001024);
                color=lerp(color,float3(.60,.81,.90),fog);
                return float4(color,1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
