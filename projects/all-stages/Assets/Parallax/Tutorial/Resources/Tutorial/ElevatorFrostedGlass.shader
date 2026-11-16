Shader "Parallax/Elevator Frosted Glass"
{
    Properties
    {
        _Color ("Frost tint", Color) = (.61,.74,.76,.42)
        _Glossiness ("Smoothness", Range(0,1)) = .13
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Cull Off
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0
        fixed4 _Color;
        half _Glossiness;
        struct Input { float3 worldPos; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float grain = frac(sin(dot(floor(IN.worldPos.xy * 750), float2(12.9898,78.233))) * 43758.5453);
            o.Albedo = _Color.rgb * (.975 + grain * .025);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = saturate(_Color.a + (grain - .5) * .015);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
