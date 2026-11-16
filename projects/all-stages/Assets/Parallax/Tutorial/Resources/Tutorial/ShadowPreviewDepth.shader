Shader "Parallax/Tutorial Shadow Preview Depth"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _TutorialShadowPreviewFar;
            struct v2f { float4 position : SV_POSITION; float depth : TEXCOORD0; };
            v2f vert(appdata_base vertex)
            {
                v2f output;
                output.position = UnityObjectToClipPos(vertex.vertex);
                output.depth = -UnityObjectToViewPos(vertex.vertex).z / _TutorialShadowPreviewFar;
                return output;
            }
            float4 frag(v2f input) : SV_Target { return input.depth.xxxx; }
            ENDCG
        }
    }
}
