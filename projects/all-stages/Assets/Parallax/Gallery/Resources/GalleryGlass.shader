Shader "Parallax/GalleryGlass"
{
    Properties { _Color ("Tint", Color) = (.35,.8,.9,.04) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0;float3 world:TEXCOORD1; };
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
            float4 frag(v2f i):SV_Target{float rim=pow(1-abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),3);return float4(_Color.rgb,_Color.a+rim*.06);}
            ENDCG
        }
    }
}
