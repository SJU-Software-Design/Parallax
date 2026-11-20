Shader "Parallax/EclipseExitSource"
{
    Properties {_MainTex("Original vietp6105 Background image",2D)="white"{} _Darkness("Eclipse state",Range(0,1))=0}
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry"}
        Cull Off ZWrite On ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Darkness;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            half4 frag(v2f i):SV_Target
            {
                // UVs are baked from sign-space X/Y after the i opening cut.
                // Both large faces therefore use the actual green source image;
                // a back-face border UV can no longer turn the board white.
                half3 c=tex2D(_MainTex,i.uv).rgb;
                return half4(c*lerp(.62,1,_Darkness),1);
            }
            ENDCG
        }
    }
}
