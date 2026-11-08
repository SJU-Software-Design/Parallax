Shader "Parallax/MuseumNikeBrush"
{
    Properties
    {
        _MainTex ("Team Nike painting", 2D) = "white" {}
        _Reveal ("Brush progress", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Reveal;
            struct Vertex { float4 position:POSITION; float2 uv:TEXCOORD0; };
            struct Surface { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            Surface vert(Vertex v)
            {
                Surface o;
                o.position = UnityObjectToClipPos(v.position);
                o.uv = v.uv;
                return o;
            }

            float noise(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float brushes(float2 uv)
            {
                // Preserve the original top-left to bottom-right brush lanes.
                float diagonal=(uv.x+1-uv.y)*.5;
                float across=(uv.x+uv.y)*.5;
                float lane=floor(across*34);
                float coverage=0;
                [unroll] for(int neighbour=-1;neighbour<=1;neighbour++) {
                    float stroke=lane+neighbour;
                    float seed=noise(float2(stroke,3.71));
                    float centre=(stroke+.5)/34+.002*sin(diagonal*21+stroke*1.9);
                    float width=(.64+seed*.13)/34;
                    float bristle=(noise(float2(floor(diagonal*1200),stroke))-.5)*.004;
                    float feather=1-smoothstep(width-.003,width+.004,abs(across-centre+bristle));
                    float dab=noise(float2(stroke,floor(diagonal*11)));
                    float head=_Reveal*1.08-.04+(seed-.5)*.045+(dab-.5)*.012;
                    float lengthMask=1-smoothstep(head-.022,head+.022,diagonal);
                    coverage=max(coverage,feather*lengthMask);
                }
                return lerp(coverage,1,smoothstep(.975,1,_Reveal));
            }

            float4 frag(Surface i):SV_Target
            {
                float4 artwork = tex2D(_MainTex, i.uv);
                // Exact endpoints keep resets clear and inserted door halves
                // fully painted, including their split UV ranges.
                artwork.a *= _Reveal <= 0 ? 0 : _Reveal >= 1 ? 1 : brushes(i.uv);
                return artwork;
            }
            ENDCG
        }
    }
    Fallback Off
}
