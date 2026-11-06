Shader "Parallax/GalleryNikeBeam"
{
    SubShader
    {
        Tags { "Queue"="Transparent+12" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float depth:TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.depth = v.uv.y;
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float view = abs(dot(normalize(_WorldSpaceCameraPos - i.world), normalize(i.normal)));
                float edge = smoothstep(.03, .68, view);
                float falloff = smoothstep(0, .06, i.depth) * (1 - smoothstep(.15, 1, i.depth));
                return float4(1, .72, .35, .085 * edge * falloff);
            }
            ENDCG
        }
    }
}
