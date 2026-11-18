Shader "Parallax/BeachCanopyDrips"
{
    Properties { _Color("Water tint", Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; };
            struct v2f { float4 position:SV_POSITION; fixed4 color:COLOR; UNITY_FOG_COORDS(1) };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; o.position=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color;
                UNITY_TRANSFER_FOG(o,o.position); return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 color=i.color;
                UNITY_APPLY_FOG(i.fogCoord,color);
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
