Shader "Parallax/Tutorial Concrete"
{
    Properties
    {
        _Color ("Concrete tint", Color) = (0.58, 0.56, 0.52, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        fixed4 _Color;
        struct Input { float3 worldPos; };

        float hash(float3 p)
        {
            p = frac(p * .3183099 + .17) * 17;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float noise(float3 p)
        {
            float3 i = floor(p);
            float3 t = frac(p);
            t = t * t * (3 - 2 * t);
            float a = lerp(hash(i), hash(i + float3(1,0,0)), t.x);
            float b = lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), t.x);
            float c = lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), t.x);
            float d = lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), t.x);
            return lerp(lerp(a,b,t.y), lerp(c,d,t.y), t.z);
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float3 p = input.worldPos;
            float broad = noise(p * 1.25);
            float grit = noise(p * 26);
            float fine = noise(p * 100);
            float detail = 1 - smoothstep(.015, .045, length(fwidth(p)));
            float pits = smoothstep(.76, .92, grit);
            float stain = noise(p * float3(3.8,.45,3.8));
            output.Albedo = _Color.rgb * (.83 + .18 * broad + .09 * stain
                + detail * (.17 * (fine - .5) - .15 * pits));
            output.Smoothness = .035 + .045 * broad;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
