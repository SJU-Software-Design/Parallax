Shader "Parallax/BeachSand"
{
    Properties {
        _Color("Sand color",Color)=(.65,.596,.487,1)
        _BumpMap("Ripples",2D)="bump"{}
        _BumpScale("Ripple strength",Range(0,2))=1
        _SpecGlossMap("Specular (RGB), Smoothness (A)",2D)="black"{}
        _Glossiness("Smoothness scale",Range(0,1))=1
        _Tiling("Repeats per metre",Float)=.35
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        LOD 200
        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows finalcolor:Preview
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        sampler2D _BumpMap,_SpecGlossMap;
        fixed4 _Color;half _BumpScale,_Glossiness;float _Tiling;
        struct Input {float3 worldPos;float3 worldNormal;INTERNAL_DATA};
        void surf(Input i,inout SurfaceOutputStandardSpecular o)
        {
            half3 face=normalize(WorldNormalVector(i,half3(0,0,1)));
            half3 u=abs(face.x)>.5?half3(0,0,1):half3(1,0,0);
            half3 v=abs(face.y)>.5?half3(0,0,1):half3(0,1,0);
            float2 uv=float2(dot(i.worldPos,u),dot(i.worldPos,v))*_Tiling;
            half3 bump=UnpackScaleNormal(tex2D(_BumpMap,uv),_BumpScale);
            half3 worldBump=u*bump.x+v*bump.y+face*bump.z;
            o.Normal=half3(dot(worldBump,WorldNormalVector(i,half3(1,0,0))),
                dot(worldBump,WorldNormalVector(i,half3(0,1,0))),
                dot(worldBump,WorldNormalVector(i,half3(0,0,1))));
            fixed4 spec=tex2D(_SpecGlossMap,uv);
            o.Albedo=_Color.rgb;o.Specular=spec.rgb;o.Smoothness=spec.a*_Glossiness;o.Alpha=_Color.a;
        }
        #include "BeachPlacementPreview.cginc"
        void Preview(Input i,SurfaceOutputStandardSpecular o,inout fixed4 color){BeachPreviewColor(i.worldPos,color);}
        ENDCG
    }
    Fallback "Standard (Specular setup)"
}
