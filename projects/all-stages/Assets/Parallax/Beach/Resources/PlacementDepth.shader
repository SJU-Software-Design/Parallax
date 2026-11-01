Shader "Parallax/PlacementDepth"
{
    SubShader { Tags { "RenderType"="Opaque" } Pass {
        Cull Off ZWrite On ZTest LEqual
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        float _PlacementPreviewFar;
        struct v2f {float4 position:SV_POSITION;float depth:TEXCOORD0;};
        v2f vert(appdata_base v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.depth=-UnityObjectToViewPos(v.vertex).z/_PlacementPreviewFar;return o;}
        float4 frag(v2f i):SV_Target{return i.depth.xxxx;}
        ENDCG
    }}
}
