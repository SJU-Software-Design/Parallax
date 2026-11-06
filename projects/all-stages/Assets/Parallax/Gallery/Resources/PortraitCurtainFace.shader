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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST,_Color,_SplitPlane;
            float _Opacity,_Red01,_Fold,_SplitSide;
            struct Vertex {float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct Surface {float4 position:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;float cut:TEXCOORD2;};
            Surface vert(Vertex input)
            {
                Surface output;
                output.cut=dot(_SplitPlane,input.position);
                float4 vertexPosition=input.position;
                vertexPosition.z+=sin(vertexPosition.x*24+vertexPosition.y*1.8)*.035*_Fold;
                output.position=UnityObjectToClipPos(vertexPosition);
                output.normal=UnityObjectToWorldNormal(input.normal);
                output.uv=TRANSFORM_TEX(input.uv,_MainTex);
                return output;
            }
            float4 frag(Surface input):SV_Target
            {
                clip(input.cut*_SplitSide+.00001);
                float4 colour=tex2D(_MainTex,input.uv)*_Color;
                float light=.67+.33*max(0,dot(normalize(input.normal),normalize(float3(-.3,.65,-1))));
                colour.rgb*=light*lerp(float3(1,1,1),float3(1,.24,.18),_Red01);
                colour.a*=_Opacity;
                return colour;
            }
            ENDCG
        }
    }
    Fallback Off
}
