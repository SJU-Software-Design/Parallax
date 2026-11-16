Shader "Parallax/Elevator Floor Text"
{
    Properties
    {
        _MainTex ("Font atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Lighting Off
        Cull Back
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            struct VIn { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct VOut { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            VOut vert(VIn v)
            {
                VOut o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(VOut i) : SV_Target
            {
                return fixed4(i.color.rgb, i.color.a * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
}
