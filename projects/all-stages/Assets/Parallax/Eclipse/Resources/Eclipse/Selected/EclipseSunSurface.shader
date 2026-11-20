Shader "Parallax/EclipseSunSurface"
{
    Properties {_MainTex("Solar System Scope Sun",2D)="white"{} _Exposure("Exposure",Range(.2,4))=1.25}
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry"}
        Cull Back ZWrite On ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Exposure;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            half4 frag(v2f i):SV_Target {half3 c=tex2D(_MainTex,i.uv).rgb;return half4(c*_Exposure,1);}
            ENDCG
        }
    }
}