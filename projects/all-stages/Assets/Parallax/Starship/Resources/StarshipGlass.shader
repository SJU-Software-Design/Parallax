Shader "Parallax/StarshipGlass"
{
    Properties {_Color("Tint",Color)=(.12,.42,.64,.12)}
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct v2f{float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 view:TEXCOORD1;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.view=_WorldSpaceCameraPos-mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
            fixed4 frag(v2f i):SV_Target{float f=pow(1-abs(dot(normalize(i.normal),normalize(i.view))),3);return fixed4(_Color.rgb+f*.08,_Color.a+f*.09);}
            ENDCG
        }
    }
}
