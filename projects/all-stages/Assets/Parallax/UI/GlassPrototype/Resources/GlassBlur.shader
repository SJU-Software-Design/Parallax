Shader "Hidden/GlassTrial/Blur"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Direction ("Direction", Vector) = (1,0,0,0) }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _Direction;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * _Direction.xy;
                return tex2D(_MainTex, i.uv) * .227027
                     + (tex2D(_MainTex, i.uv + d * 1.384615) + tex2D(_MainTex, i.uv - d * 1.384615)) * .316216
                     + (tex2D(_MainTex, i.uv + d * 3.230769) + tex2D(_MainTex, i.uv - d * 3.230769)) * .070270;
            }
            ENDCG
        }
    }
    Fallback Off
}