Shader "Parallax/Tutorial Light Beam"
{
    Properties { _Tint ("Beam tint", Color) = (1,0.91,0.76,0.035) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Tint;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 worldPos : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(cross(ddx(i.worldPos), ddy(i.worldPos)));
                float3 view = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float edgeFade = smoothstep(0.0, 0.45, abs(dot(normal, view)));
                return fixed4(_Tint.rgb, _Tint.a * i.color.a * edgeFade);
            }
            ENDCG
        }
    }
}
