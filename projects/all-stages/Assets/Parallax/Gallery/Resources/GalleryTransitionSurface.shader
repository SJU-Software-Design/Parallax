Shader "Parallax/GalleryTransitionSurface"
{
    Properties { _Color ("Color",Color)=(.15,.12,.09,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct v2f { float4 position:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; };
            v2f vert(appdata_base input)
            {
                v2f output;output.position=UnityObjectToClipPos(input.vertex);
                output.normal=UnityObjectToWorldNormal(input.normal);
                output.world=mul(unity_ObjectToWorld,input.vertex).xyz;return output;
            }
            fixed4 frag(v2f input):SV_Target
            {
                float light=.48+.52*max(0,dot(normalize(input.normal),normalize(float3(-.3,1,-.4))));
                float warmth=lerp(.42,1.25,saturate((-input.world.y-28)/22));
                return fixed4(_Color.rgb*light*warmth,1);
            }
            ENDCG
        }
    }
}
