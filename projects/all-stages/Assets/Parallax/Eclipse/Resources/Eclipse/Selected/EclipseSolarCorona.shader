Shader "Parallax/EclipseSolarCorona"
{
    Properties {_Visibility("Visibility",Range(0,1))=.24}
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent-10"}
        Blend SrcAlpha One Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Visibility;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            half4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*2; float r=length(p)*1.62;
                float angle=atan2(p.y,p.x);
                float rays=.88+.07*sin(angle*17)+.03*sin(angle*31+1.2)+.02*sin(angle*53);
                float d=max(0,r-1);
                float glow=(exp(-d*28)*.52+exp(-d*8)*.065)*rays;
                float edge=smoothstep(.996,1.012,r)*(1-smoothstep(1.28,1.48,r));
                return half4(1,.90,.72,saturate(glow*edge*_Visibility));
            }
            ENDCG
        }
    }
}