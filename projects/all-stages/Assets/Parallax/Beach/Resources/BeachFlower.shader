Shader "Parallax/BeachFlower"
{
    Properties {
        _MainTex("Flower atlas",2D)="white"{}
        _Cutoff("Alpha cutoff",Range(0,1))=.5
    }
    SubShader {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alphatest:_Cutoff addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        struct Input {float2 uv_MainTex;};
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            fixed4 color=tex2D(_MainTex,i.uv_MainTex);
            o.Albedo=color.rgb;o.Alpha=color.a;o.Smoothness=.08;
        }
        ENDCG
    }
    Fallback "Transparent/Cutout/Diffuse"
}
