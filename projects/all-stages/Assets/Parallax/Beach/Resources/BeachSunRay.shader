Shader "Parallax/BeachSunRay"
{
    Properties { _Color("Ray color",Color)=(1,.72,.28,1) }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1) };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;
                UNITY_TRANSFER_FOG(o,o.position);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 color=_Color;
                float edge=1-smoothstep(0,1,abs(i.uv.y*2-1));color.a*=edge*edge*.65;
                UNITY_APPLY_FOG_COLOR(i.fogCoord,color,fixed4(0,0,0,0));return color;
            }
            ENDCG
        }
    }
}
