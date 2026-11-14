Shader "Parallax/PortraitCurtainFace"
{
    Properties
    {
        _MainTex ("Original face material",2D)="white" {}
        _Color ("Original colour",Color)=(1,1,1,1)
        _Opacity ("Absorption / fold visibility",Range(0,1))=1
        _Red01 ("Portrait red mood",Range(0,1))=0
        _Fold ("Curtain fold",Range(0,1))=0
        _SplitSide ("Curtain half",Float)=1
        _SplitPlane ("Original prop local X",Vector)=(1,0,0,0)
    }
    SubShader
    {
        Tags {"Queue"="Transparent+8" "RenderType"="Transparent"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST,_Color,_SplitPlane;
            float _Opacity,_Red01,_Fold,_SplitSide;
            int _MuseumPortraitLightCount;
            float4 _MuseumPortraitLightPositionRange[4],_MuseumPortraitLightDirectionCone[4],_MuseumPortraitLightColorIntensity[4];
            struct Vertex {float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct Surface {float4 position:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;float cut:TEXCOORD2;float3 world:TEXCOORD3;};
            Surface vert(Vertex input)
            {
                Surface output;
                output.cut=dot(_SplitPlane,input.position);
                float4 vertexPosition=input.position;
                vertexPosition.z+=sin(vertexPosition.x*24+vertexPosition.y*1.8)*.035*_Fold;
                output.position=UnityObjectToClipPos(vertexPosition);
                output.world=mul(unity_ObjectToWorld,vertexPosition).xyz;
                output.normal=UnityObjectToWorldNormal(input.normal);
                output.uv=TRANSFORM_TEX(input.uv,_MainTex);
                return output;
            }
            float4 frag(Surface input,float facing:VFACE):SV_Target
            {
                clip(input.cut*_SplitSide+.00001);
                float4 colour=tex2D(_MainTex,input.uv)*_Color;
                float3 normal=normalize(input.normal)*(facing<0?-1:1);
                float3 light=float3(.50,.48,.43)*lerp(1,.65,_Red01);
                [loop] for(int source=0;source<min(_MuseumPortraitLightCount,4);source++) {
                    float4 lamp=_MuseumPortraitLightPositionRange[source];
                    float3 delta=lamp.xyz-input.world;float distance=length(delta);
                    float3 direction=delta/max(distance,.001);
                    float attenuation=pow(saturate(1-distance/max(lamp.w,.01)),2);
                    float4 cone=_MuseumPortraitLightDirectionCone[source];
                    attenuation*=smoothstep(cone.w,min(.999,cone.w+.14),dot(-direction,normalize(cone.xyz)));
                    float4 radiance=_MuseumPortraitLightColorIntensity[source];
                    light+=radiance.rgb*radiance.w*attenuation*saturate(dot(normal,direction))*.31831;
                }
                colour.rgb*=light;
                colour.a*=_Opacity;
                return colour;
            }
            ENDCG
        }
    }
    Fallback Off
}
