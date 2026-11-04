Shader "Hidden/Parallax/MuseumNikeDepth"
{
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            Cull Off
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _NikeLightPositionRange, _NikeLightDirectionCone;
            float4 _NikeShadowParams;
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 position:SV_POSITION; float forwardMetres:TEXCOORD0; };
            v2f vert(appdata v) {
                v2f o;
                o.position=UnityObjectToClipPos(v.vertex);
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.forwardMetres=dot(world-_NikeLightPositionRange.xyz,normalize(_NikeLightDirectionCone.xyz));
                return o;
            }
            // Write linear forward depth to an RFloat colour RT, cleared to 1.
            // The actual depth buffer still chooses the nearest mesh surface.
            // Comparing these values needs no platform depth/reversed-Z remap.
            float4 frag(v2f i):SV_Target {
                float depth=saturate(i.forwardMetres/max(_NikeShadowParams.x,.01));
                return float4(depth,depth,depth,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
