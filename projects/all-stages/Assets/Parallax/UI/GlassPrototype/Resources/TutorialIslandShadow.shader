Shader "Hidden/GlassTrial/TutorialIslandShadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _PanelSize ("Panel size", Vector) = (268,52,0,0)
        _Padding ("Shadow padding", Float) = 13
        _FadeDistance ("Shadow fade distance", Float) = 11
        _CornerRadiusPixels ("Corner radius pixels; negative = pill", Float) = -1
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
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            fixed4 _Color;
            float4 _PanelSize;
            float _Padding, _FadeDistance, _CornerRadiusPixels;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color * _Color; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 panelSize = max(_PanelSize.xy, float2(1, 1));
                float padding = max(_Padding, 1);
                float2 position = (i.uv - .5) * (panelSize + padding * 2);
                float radius = _CornerRadiusPixels < 0 ? min(panelSize.x, panelSize.y) * .5 : min(_CornerRadiusPixels, min(panelSize.x, panelSize.y) * .5);
                float2 q = abs(position) - (panelSize * .5 - radius);
                float distanceToPill = length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
                float fade = min(max(_FadeDistance, 1), padding - .5);
                float alpha = 1 - smoothstep(0, fade, max(distanceToPill, 0));
                return fixed4(i.color.rgb, i.color.a * alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}