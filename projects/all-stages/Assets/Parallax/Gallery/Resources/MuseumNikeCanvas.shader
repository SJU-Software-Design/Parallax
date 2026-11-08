Shader "Parallax/MuseumNikeCanvas"
{
    Properties {
        _TargetMask ("Original scan projected outline", 2D) = "black" {}
        _LiveMask ("Held scan wall preview", 2D) = "black" {}
        _ArtTex ("Same canonical scan painted view", 2D) = "white" {}
        _Reveal ("Brush progress", Range(0,1)) = 0
        _Finished ("Finished painting", Float) = 0
        _Preview ("Preview held sculpture shadow", Float) = 0
    }
    SubShader {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _TargetMask, _LiveMask, _ArtTex, _ProjectedDepth;
        float4 _TargetMask_TexelSize;
        float _Reveal, _Finished, _Preview;
        float3 _Source, _PlaneCenter, _PlaneNormal, _PlaneRight, _PlaneUp;
        float4 _CanvasSize;
        struct vertex { float4 position:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
        struct surface { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
        struct projected { float4 position:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; float2 uv:TEXCOORD2; };
        float noise(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        surface canvasVertex(vertex v) {
            surface o; o.position=UnityObjectToClipPos(v.position); o.uv=v.uv; return o;
        }
        float2 projectionUv(float3 world) {
            float3 ray=world-_Source;
            float denominator=dot(ray,_PlaneNormal);
            float t=dot(_PlaneCenter-_Source,_PlaneNormal)/(abs(denominator)<.000001?-.000001:denominator);
            float3 onPlane=_Source+ray*t-_PlaneCenter;
            return float2(dot(onPlane,_PlaneRight)/_CanvasSize.x,dot(onPlane,_PlaneUp)/_CanvasSize.y)+.5;
        }
        projected projectScan(vertex v) {
            projected o;
            float3 world=mul(unity_ObjectToWorld,v.position).xyz;
            float3 ray=world-_Source;
            float denominator=dot(ray,_PlaneNormal);
            float t=dot(_PlaneCenter-_Source,_PlaneNormal)/(abs(denominator)<.000001?-.000001:denominator);
            o.uv=projectionUv(world);
            // Source-plane depth in W preserves perspective-correct scan
            // normals/world positions even with this off-axis canvas frustum.
            float w=max(.001,dot(world-_Source,-_PlaneNormal));
            o.position=float4((o.uv*2-1)*w,0,w);
            // Draw directly into the texture's UV coordinates on each backend.
            #if UNITY_UV_STARTS_AT_TOP
                o.position.y=-o.position.y;
            #endif
            o.position.z=.5*w;
            if(t<=1 || abs(denominator)<.000001)o.position=float4(-10000,-10000,0,1);
            o.normal=UnityObjectToWorldNormal(v.normal); o.world=world; return o;
        }
        float brushes(float2 uv) {
            // The overall front moves diagonally from top-left to bottom-right.
            // Nearby parallel brush lanes have staggered heads, curved edges,
            // variable widths and individual bristles, never horizontal rows.
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
        float4 canvasFragment(surface i):SV_Target {
            float mask=tex2D(_TargetMask,i.uv).r;
            float maximum=mask,minimum=mask;
            // Keep the original detailed contour readable from the gallery:
            // a fixed texture-width edge shrinks below a screen pixel at 4096.
            float2 pixel=max(_TargetMask_TexelSize.xy*1.2,fwidth(i.uv)*.9);
            [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++) {
                float neighbour=tex2D(_TargetMask,i.uv+float2(x,y)*pixel).r;
                maximum=max(maximum,neighbour); minimum=min(minimum,neighbour);
            }
            float outline=saturate(maximum-minimum)*.94*(1-smoothstep(0,.16,_Reveal));
            float preview=tex2D(_LiveMask,i.uv).r*_Preview*.58;
            float3 color=float3(.055,.047,.035);
            float alpha=preview;
            color=lerp(color,float3(.075,.028,.008),outline);
            alpha=max(alpha,outline);
            float amount=_Finished>.5?1:brushes(i.uv);
            float grain=(noise(floor(i.uv*2200))-.5)*.012;
            float3 linen=float3(.26,.205,.13)+grain;
            float3 subject=tex2D(_ArtTex,i.uv).rgb;
            float3 painting=lerp(linen,subject,mask);
            color=lerp(color,painting,amount);
            alpha=max(alpha,amount);
            return float4(color,alpha);
        }
        float4 maskFragment(projected i):SV_Target { return 1; }
        float4 depthFragment(projected i):SV_Target { return saturate(dot(i.world-_Source,-_PlaneNormal)/25); }
        float4 paintedFragment(projected i,float face:VFACE):SV_Target {
            float2 uv=projectionUv(i.world);
            float depth=dot(i.world-_Source,-_PlaneNormal)/25;
            clip(tex2D(_ProjectedDepth,uv).r+.00001-depth);
            float3 n=normalize(i.normal)*(face<0?-1:1);
            float3 light=normalize(_Source-i.world);
            float form=saturate(.82*saturate(dot(n,light))+.18*saturate(dot(n,normalize(float3(-.5,.7,-.7)))));
            form=pow(form,1.15);
            // Every fold/feather is still original scan geometry, with a quiet
            // pigment texture added to its lit canonical view.
            float grain=(noise(floor(uv*2800))-.5)*.012;
            float stroke=.004*sin(uv.x*720+uv.y*53)*sin(uv.y*960);
            float3 pigment=lerp(float3(.025,.013,.006),float3(.40,.275,.14),form)+grain+stroke;
            // Fine contrast follows the real feather/drapery normal changes.
            float crease=saturate((length(ddx(n))+length(ddy(n)))*2.2);
            pigment*=1-crease*.20;
            return float4(max(pigment,0),1);
        }
        ENDCG
        // Pass 0 overlays the original MuseumSurface PBR linen backing.
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex canvasVertex
            #pragma fragment canvasFragment
            ENDCG
        }
        // Pass 1 projects the unmodified full original mesh into a mask.
        Pass {
            Tags { "LightMode"="NikeProjection" }
            ZTest Always
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex projectScan
            #pragma fragment maskFragment
            ENDCG
        }
        // Pass 2 renders the same canonical mesh as the painted subject.
        Pass {
            Tags { "LightMode"="NikeProjection" }
            ZTest Always
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex projectScan
            #pragma fragment paintedFragment
            ENDCG
        }
        // Pass 3 resolves source-ray depth in linear RFloat colour with Min.
        // No camera Z buffer, platform clip-depth convention or reversed-Z
        // clear value participates in canonical painted-surface ordering.
        Pass {
            Tags { "LightMode"="NikeProjection" }
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One
            BlendOp Min
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex projectScan
            #pragma fragment depthFragment
            ENDCG
        }
    }
    Fallback Off
}
