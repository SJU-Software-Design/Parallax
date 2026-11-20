Shader "Parallax/EclipseShelterCanopy"
{
    Properties
    {
        _Color("Canopy colour",Color)=(.24,.27,.30,1)
        _CameraOpacity("Camera-only visibility",Range(0,1))=1
    }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent"}
        Cull Back
        CGPROGRAM
        #pragma surface surf Standard alpha:fade fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        half _CameraOpacity;
        struct Input {float3 worldPos;};
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            o.Albedo=_Color.rgb;o.Smoothness=.12;o.Metallic=0;
            o.Alpha=_Color.a*_CameraOpacity;
        }
        ENDCG
        // Gameplay shading is independent of camera transparency: the same
        // solid high roof keeps casting its real directional shadow.
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual Cull Off
            Blend Off
            CGPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            struct v2f {V2F_SHADOW_CASTER;};
            v2f shadowVert(appdata_base v)
            {
                v2f o;TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);return o;
            }
            float4 shadowFrag(v2f i):SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback Off
}
