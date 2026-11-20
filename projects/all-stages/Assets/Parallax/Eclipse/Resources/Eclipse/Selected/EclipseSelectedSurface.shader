Shader "Parallax/EclipseSelectedSurface"
{
    Properties
    {
        _Color("Tint", Color)=(1,1,1,1)
        _MainTex("Original base colour",2D)="white"{}
        _BumpMap("Original normal",2D)="bump"{}
        _ORM("Original occlusion roughness metallic",2D)="white"{}
        _EmissionMap("Original emissive",2D)="black"{}
        _EmissionGain("Emission gain",Range(0,2))=.16
        _LightGain("Presentation light",Range(0,1))=1
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex,_BumpMap,_ORM,_EmissionMap;
        fixed4 _Color; half _EmissionGain,_LightGain;
        struct Input {float2 uv_MainTex;};
        void surf(Input i, inout SurfaceOutputStandard o)
        {
            fixed4 c=tex2D(_MainTex,i.uv_MainTex)*_Color;
            half3 orm=tex2D(_ORM,i.uv_MainTex).rgb;
            o.Albedo=c.rgb; o.Alpha=1;
            o.Normal=UnpackNormal(tex2D(_BumpMap,i.uv_MainTex));
            o.Occlusion=orm.r; o.Smoothness=clamp(1-orm.g,.04,.64); o.Metallic=orm.b;
            o.Emission=tex2D(_EmissionMap,i.uv_MainTex).rgb*_EmissionGain*_LightGain;
        }
        ENDCG
    }
    Fallback "Standard"
}