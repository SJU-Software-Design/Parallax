Shader "Parallax/MuseumFoliage"
{
    Properties
    {
        _MainTex ("Leaf colour and alpha",2D)="white" {}
        _Color ("Tint",Color)=(1,1,1,1)
        _Cutoff ("Alpha cutoff",Range(0,1))=.4
        _Glossiness ("Smoothness",Range(0,1))=.25
        _Metallic ("Metallic",Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "IgnoreProjector"="True" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows addshadow alphatest:_Cutoff
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness,_Metallic;
        struct Input
        {
            float2 uv_MainTex;
            float facing:VFACE;
        };
        void surf(Input input,inout SurfaceOutputStandard output)
        {
            fixed4 leaf=tex2D(_MainTex,input.uv_MainTex)*_Color;
            output.Albedo=leaf.rgb;
            output.Alpha=leaf.a;
            output.Smoothness=_Glossiness;
            output.Metallic=_Metallic;
            // Back faces remain visible and shade with their outward normal.
            output.Normal=float3(0,0,input.facing>=0?1:-1);
        }
        ENDCG
    }
    Fallback "Legacy Shaders/Transparent/Cutout/VertexLit"
}
