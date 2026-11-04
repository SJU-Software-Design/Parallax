Shader "Parallax/MuseumLibraryPreviewSurface"
{
    Properties
    {
        _Color ("Original library surface", Color) = (1,1,1,1)
        _MainTex ("Original library texture", 2D) = "white" {}
        _Emission ("Warm practical light", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST,_Color,_Emission;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION; float3 local:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);
                o.local=v.vertex.xyz;o.normal=v.normal;o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o;
            }
            float3 lamp(float3 at,float3 n,float3 position)
            {
                float3 delta=position-at;
                float distanceSquared=max(.02,dot(delta,delta));
                float facing=.24+.76*saturate(dot(n,delta*rsqrt(distanceSquared)));
                return float3(1.12,.69,.30)*facing/(1+distanceSquared*.17);
            }
            float4 frag(v2f i,float facing:VFACE):SV_Target
            {
                float3 n=normalize(i.normal)*(facing>=0?1:-1);
                float3 light=float3(.27,.20,.135);
                light+=lamp(i.local,n,float3(0,3.35,3.6))*.88;
                light+=lamp(i.local,n,float3(0,3.35,7.2))*.88;
                light+=lamp(i.local,n,float3(0,3.35,10.6))*.70;
                float3 color=tex2D(_MainTex,i.uv).rgb*_Color.rgb*light;
                // A little falloff keeps the far shelving deep brown while book spines remain readable.
                float depth=saturate((i.local.z-7)/12);
                color=lerp(color,float3(.045,.025,.011),depth*.28);
                return float4(color+_Emission.rgb,1);
            }
            ENDCG
        }
    }
}
