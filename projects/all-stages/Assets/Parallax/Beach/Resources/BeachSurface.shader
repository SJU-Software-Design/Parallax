Shader "Parallax/BeachSurface"
{
    Properties {
        _Color("Color",Color)=(1,1,1,1)
        _MainTex("Albedo",2D)="white"{}
        _Metallic("Metallic",Range(0,1))=0
        _Glossiness("Smoothness",Range(0,1))=.2
        _EmissionColor("Emission",Color)=(0,0,0,1)
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows finalcolor:Preview
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color,_EmissionColor;half _Metallic,_Glossiness;
        struct Input {float2 uv_MainTex;float3 worldPos;};
        void surf(Input i,inout SurfaceOutputStandard o){fixed4 c=tex2D(_MainTex,i.uv_MainTex)*_Color;o.Albedo=c.rgb;o.Alpha=c.a;o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Emission=_EmissionColor.rgb;}
        #include "BeachPlacementPreview.cginc"
        void Preview(Input i,SurfaceOutputStandard o,inout fixed4 color){BeachPreviewColor(i.worldPos,color);}
        ENDCG
    }
    Fallback "Diffuse"
}
