Shader "ShadeLink/Finish"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} _Exposure ("Danger", Float) = 0 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Exposure;
            float4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                // Filmic highlight roll-off, edge darkening and heat feedback.
                c = saturate((c * (2.51 * c + .03)) / (c * (2.43 * c + .59) + .14));
                float2 p = i.uv * 2 - 1;
                float edge = saturate(dot(p, p) * .4);
                c *= 1 - edge * .17;
                c = lerp(c, float3(.72, .19, .065), edge * _Exposure * .34);
                return float4(c, 1);
            }
            ENDCG
        }
    }
}
