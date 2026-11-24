Shader "Hidden/GlassTrial/TutorialIsland"
{
    Properties
    {
        [PerRendererData] _MainTex ("Blurred background", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlassMode ("Glass design", Float) = 1
        _Refraction ("Edge refraction", Float) = 0
        _EdgeContrast ("Edge contrast", Float) = 0
        _RefractionStrength ("Refraction strength (pixels)", Range(0, 30)) = 9
        _RefractionWidth ("Refraction width", Range(0.01, 0.6)) = 0.18
        _LensDistortion ("Lens distortion (pixels)", Range(0, 80)) = 30
        _EdgeContrastStrength ("Edge contrast strength", Range(0, 2)) = 1
        _SpectrumPixels ("Spectrum separation (pixels)", Range(0, 8)) = 0
        _WhiteTintStrength ("White tint strength", Range(0, 1)) = 0.14
        _DarkTintStrength ("Dark tint strength", Range(0, 1)) = 0.26
        _Saturation ("Background saturation", Range(0, 2)) = 1.45
        _ExtraBlurPixels ("Additional blur radius", Range(0, 24)) = 9.5
        _MilkyTint ("Milky tint", Range(0, 0.5)) = 0.16
        _SuccessGlow ("Success glow", Range(0, 1)) = 0
        _NoticeTint ("Notice tint", Color) = (1,1,1,1)
        _NoticeStrength ("Notice tint strength", Range(0, 1)) = 0
        _PanelAspect ("Panel aspect", Float) = 1.47
        _CornerRadius ("Corner radius", Range(0.01, 1)) = 0.095
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _GlassMode, _Refraction, _EdgeContrast, _SpectrumPixels, _WhiteTintStrength, _PanelAspect;
            float _RefractionStrength, _RefractionWidth, _EdgeContrastStrength;
            float _LensDistortion;
            float _MilkyTint;
            float _DarkTintStrength, _Saturation, _ExtraBlurPixels;
            float _SuccessGlow;
        float4 _NoticeTint;
        float _NoticeStrength;
            float _CornerRadius;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.screen = ComputeScreenPos(o.vertex);
                o.uv = v.uv; o.color = v.color * _Color; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                fixed4 c;
                if (_GlassMode < .5)
                {
                    c = tex2D(_MainTex, uv);
                    float2 p = i.uv * 2 - 1;
                    float2 q = max(abs(p) - float2(.93,.91), 0);
                    float corner = 1-smoothstep(.067,.075,length(q));
                    c.rgb = lerp(c.rgb, float3(.06,.15,.17), .35);
                    c.a = corner * i.color.a;
                    return c;
                }
                // Rounded rectangle SDF in aspect-corrected UI-local units.
                float2 halfSize = float2(_PanelAspect, 1.0);
                float2 position = (i.uv * 2.0 - 1.0) * halfSize;
                float radius = clamp(_CornerRadius, .01, 1.0);
                float2 q = abs(position) - (halfSize - radius);
                float distanceToEdge = length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
                float edgeAA = max(fwidth(distanceToEdge), .001);
                float mask = 1.0 - smoothstep(-edgeAA, edgeAA, distanceToEdge);
                float2 outside = max(q, 0.0);
                float2 normal = (q.x > 0.0 || q.y > 0.0)
                    ? outside / max(length(outside), .0001)
                    : (q.x > q.y ? float2(1, 0) : float2(0, 1));
                normal *= sign(position);
                float edgeFalloff = 0.0;
                float cornerStrength = 1.0;
                if (_Refraction > .5)
                {
                    // Static refraction restricted to the inner rim; the center is undistorted.
                    edgeFalloff = 1.0 - smoothstep(.0, max(_RefractionWidth, .001), -distanceToEdge);
                    cornerStrength = 1.0 + .4 * (2.0 * abs(normal.x * normal.y));
                    uv -= normal * (_RefractionStrength * cornerStrength * edgeFalloff) / _ScreenParams.xy;
                    // Refract the background across the panel as a stationary lens;
                    // displacement grows away from the center, independent of spectrum.
                    float2 lensPosition = i.uv * 2.0 - 1.0;
                    float lensRadius = saturate(length(lensPosition));
                    uv -= lensPosition * (lensRadius * _LensDistortion) / _ScreenParams.xy;
                }
                // The input is already blurred by the camera. A small symmetric
                // kernel adds tutorial-only softness without changing other panels.
                float2 blur = _ExtraBlurPixels / _ScreenParams.xy;
                c = tex2D(_MainTex, uv) * .28;
                c += (tex2D(_MainTex, uv + float2(blur.x, 0)) + tex2D(_MainTex, uv - float2(blur.x, 0)) +
                      tex2D(_MainTex, uv + float2(0, blur.y)) + tex2D(_MainTex, uv - float2(0, blur.y))) * .11;
                c += (tex2D(_MainTex, uv + blur * .7071) + tex2D(_MainTex, uv - blur * .7071) +
                      tex2D(_MainTex, uv + float2(blur.x, -blur.y) * .7071) + tex2D(_MainTex, uv + float2(-blur.x, blur.y) * .7071)) * .07;
                float insideEdge = saturate((distanceToEdge + .014) / .014);
                float topLight = smoothstep(.10, 1.0, i.uv.y);
                float upperLeft = smoothstep(.40, .95, i.uv.y) * (1.0-smoothstep(.15, .9, i.uv.x));
                float topEdge = insideEdge * smoothstep(.65, .98, i.uv.y);
                float border = insideEdge * mask;
                // Preserve the existing blur; use gentle top/bottom depth and asymmetric edge light.
                float luminance = dot(c.rgb, float3(.2126, .7152, .0722));
                c.rgb = saturate(lerp(luminance.xxx, c.rgb, _Saturation));
                float3 darkTone = lerp(float3(10,14,19), float3(14,19,25), topLight) / 255.0;
                float3 whiteTone = lerp(float3(220,226,234), float3(242,245,250), topLight) / 255.0;
                c.rgb = lerp(c.rgb, darkTone, saturate(_DarkTintStrength));
                c.rgb = lerp(c.rgb, whiteTone, saturate(_WhiteTintStrength));
                float rim = 1.0 - smoothstep(.0, .018, -distanceToEdge);
                float edgeLight = rim * lerp(.252, .472, smoothstep(.65, .98, i.uv.y));
                c.rgb = lerp(c.rgb, float3(1,1,1), saturate(edgeLight));
                if (_EdgeContrast > .5)
                {
                    float fineEdge = (1.0 - smoothstep(.006, .026, -distanceToEdge)) * mask;
                    float brightSide = saturate(normal.y + max(-normal.x, 0.0) * .65);
                    float darkSide = saturate(-normal.y + max(normal.x, 0.0) * .65);
                    c.rgb *= 1.0 - saturate(fineEdge * darkSide * .45 * _EdgeContrastStrength);
                    c.rgb = lerp(c.rgb, float3(.94, 1.0, .99), saturate(fineEdge * brightSide * .48 * _EdgeContrastStrength));
                }
                float successRim = 1.0 - smoothstep(.0, .06, -distanceToEdge);
                c.rgb = lerp(c.rgb, float3(.22, 1.0, .63), saturate(_SuccessGlow * (0.12 + successRim * .62)));
                c.rgb = lerp(c.rgb, _NoticeTint.rgb, saturate(_NoticeStrength * (.05 + successRim * .58)));
                c.a = mask * i.color.a;
                return c;
            }
            ENDCG
        }
    }
}
