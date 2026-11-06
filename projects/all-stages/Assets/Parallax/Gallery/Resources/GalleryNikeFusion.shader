Shader "Parallax/GalleryNikeFusion"
{
    Properties
    {
        _Charge ("Charge", Float) = 0
        _Pulse ("Pulse", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Charge, _Pulse;

            float4 frag(v2f_img i):SV_Target
            {
                float2 xy = i.uv * 2 - 1;
                float radius = length(xy);
                float angle = atan2(xy.y, xy.x);
                float rim = 1 - smoothstep(.75, 1, radius);
                if (_Pulse < 0)
                {
                    float spiral = pow(saturate(.5 + .5 * cos(angle * 4 + radius * 20 - _Charge * 17)), 7);
                    float gather = exp(-radius * (3 + _Charge * 5)) * _Charge * _Charge;
                    return float4(float3(1.0, .52, .14) * gather * (.27 + spiral * .36) * rim, 0);
                }

                float age = saturate(_Pulse);
                float ring = exp(-abs(radius - (.12 + age * .97)) * 36) * (1 - age);
                float secondRing = exp(-abs(radius - age * .66) * 32) * (1 - age);
                float core = exp(-radius * (3.0 + age * 9)) * pow(1 - age, 5) * 2.1;
                float rays = pow(saturate(.5 + .5 * cos(angle * 12 + radius * 6)), 11);
                float sparks = rays * exp(-abs(radius - (.11 + age * .74)) * 11) * pow(1 - age, 2) * .30;
                return float4((float3(1.0, .65, .21) * (ring * .55 + secondRing * .23 + sparks) +
                    float3(1.0, .87, .63) * core) * rim, 0);
            }
            ENDCG
        }
    }
}
