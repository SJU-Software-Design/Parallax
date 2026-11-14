Shader "Parallax/Tutorial Brushed Steel"
{
    Properties
    {
        _Color ("Steel tint", Color) = (.72, .74, .74, 1)
        _Metallic ("Metallic", Range(0, 1)) = .3
        _Glossiness ("Smoothness", Range(0, 1)) = .5
        _GrainScale ("Brush scale per meter", Vector) = (36, 5, 36, 0)
        _Wear ("Surface wear", Range(0, 1)) = .6
        [HideInInspector] _TutorialShadowPreviewReceiver ("Shadow preview receiver", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows finalcolor:ShadowPreview
        #pragma target 3.0

        half4 _Color;
        half _Metallic, _Glossiness, _Wear;
        float4 _GrainScale;
        half _TutorialShadowPreviewReceiver;
        sampler2D _TutorialShadowPreviewMap;
        float4x4 _TutorialShadowPreviewVP, _TutorialShadowPreviewView;
        float _TutorialShadowPreviewActive, _TutorialShadowPreviewFar;
        struct Input { float3 worldPos; float3 worldNormal; };

        float Hash(float3 cell)
        {
            cell = frac(cell * .3183099 + .17) * 17;
            return frac(cell.x * cell.y * cell.z * (cell.x + cell.y + cell.z));
        }

        float Noise(float3 position)
        {
            float3 cell = floor(position);
            float3 t = frac(position);
            t = t * t * (3 - 2 * t);
            float a = lerp(Hash(cell), Hash(cell + float3(1,0,0)), t.x);
            float b = lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), t.x);
            float c = lerp(Hash(cell + float3(0,0,1)), Hash(cell + float3(1,0,1)), t.x);
            float d = lerp(Hash(cell + float3(0,1,1)), Hash(cell + float3(1,1,1)), t.x);
            return lerp(lerp(a, b, t.y), lerp(c, d, t.y), t.z);
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float3 p = input.worldPos;
            float3 brushed = p * _GrainScale.xyz;
            // Fade features once they are too small to resolve, especially along the floor.
            float detail = 1 - smoothstep(.25, .95, length(fwidth(brushed)));
            float grain = (Noise(brushed) - .5) * detail;
            float finish = Noise(p * 3.4) - .5;
            float wear = Noise(p * float3(.55, .87, .64)) - .5;
            float scuff = smoothstep(.62, .9, Noise(brushed * .13 + 9.1));
            output.Albedo = _Color.rgb * (1 + _Wear * (.10 * wear + .10 * finish + .14 * grain - .075 * scuff));
            output.Metallic = _Metallic;
            output.Smoothness = saturate(_Glossiness + _Wear * (.085 * finish + .11 * grain - .085 * scuff));
        }

        void ShadowPreview(Input input, SurfaceOutputStandard output, inout fixed4 color)
        {
            #ifndef UNITY_PASS_FORWARDADD
            if (_TutorialShadowPreviewReceiver > .5 && _TutorialShadowPreviewActive > .5 && input.worldNormal.y > .8)
            {
                float4 clip = mul(_TutorialShadowPreviewVP, float4(input.worldPos, 1));
                float2 uv = clip.xy / clip.w * .5 + .5;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1 - uv.y;
                #endif
                float depth = -mul(_TutorialShadowPreviewView, float4(input.worldPos, 1)).z / _TutorialShadowPreviewFar;
                float nearest = tex2D(_TutorialShadowPreviewMap, uv).r;
                if (all(uv > 0) && all(uv < 1) && nearest < .999 && depth > nearest + .00045)
                {
                    float stripePosition = (input.worldPos.x + input.worldPos.z) * 2.5;
                    float antialias = min(.2, fwidth(stripePosition));
                    float stripe = smoothstep(.76 - antialias, .76 + antialias, frac(stripePosition));
                    color.rgb = lerp(color.rgb, float3(.08, .48, .68), .15 + stripe * .63);
                }
            }
            #endif
        }
        ENDCG
    }
    FallBack "Standard"
}
