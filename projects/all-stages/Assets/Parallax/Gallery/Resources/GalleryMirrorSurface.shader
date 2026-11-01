Shader "Parallax/GalleryMirror"
{
    Properties { _Color ("Silver tint", Color) = (.82,.93,1,1) _MirrorTex ("Reflection", 2D) = "gray" {} _Selection ("Selection", Color) = (0,0,0,0) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MirrorTex;
            float4 _Color, _Selection;
            float4x4 _MirrorVP;
            struct v2f { float4 pos : SV_POSITION; float4 projection : TEXCOORD0; float2 uv : TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.projection=mul(_MirrorVP,mul(unity_ObjectToWorld,v.vertex));o.uv=v.texcoord.xy;return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float2 uv=i.projection.xy/max(.001,i.projection.w)*.5+.5;
                float3 reflection=tex2D(_MirrorTex,uv).rgb;
                float edge=step(.015,min(min(i.uv.x,i.uv.y),min(1-i.uv.x,1-i.uv.y)));
                return float4(lerp(float3(.65,.81,.86),reflection*_Color.rgb*.88+.06,edge)+_Selection.rgb,1);
            }
            ENDCG
        }
    }
}
