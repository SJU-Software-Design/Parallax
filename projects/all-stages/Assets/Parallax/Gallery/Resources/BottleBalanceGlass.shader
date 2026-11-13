Shader "Parallax/BottleBalanceGlass"
{
    Properties
    {
        _Color ("Glass tint", Color) = (.94,.90,.82,.015)
        _Selection ("Selection tint", Color) = (0,0,0,0)
        _RimStrength ("Edge reflection", Range(0,1)) = .18
        _Gloss ("Polished highlights", Range(8,256)) = 96
    }
    SubShader
    {
        Tags { "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            float4 _Color, _Selection;
            float _RimStrength, _Gloss;
            struct v2f
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
            };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.position=UnityObjectToClipPos(v.vertex);
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal=normalize(i.normal);
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float facing=saturate(abs(dot(normal,view)));
                float rim=pow(1-facing,3.1);
                float3 reflected=reflect(-view,normal);
                half4 probe=UNITY_SAMPLE_TEXCUBE(unity_SpecCube0,reflected);
                half3 environment=DecodeHDR(probe,unity_SpecCube0_HDR);
                // Broad warm ceiling cards keep the clear vessel readable in an
                // exhibit bay even when the scene has no local reflection probe.
                float card1=pow(saturate(dot(reflected,normalize(float3(-.42,.86,-.28)))),32);
                float card2=pow(saturate(dot(reflected,normalize(float3(.56,.78,.25)))),56);
                float card3=pow(saturate(dot(reflected,normalize(float3(-.65,.23,.72)))),84);
                float3 light=normalize(_WorldSpaceLightPos0.xyz);
                float spec=pow(saturate(dot(normal,normalize(light+view))),max(8,_Gloss));
                float3 highlight=(card1*.65+card2*.8+card3*.36)*float3(1,.91,.72)+spec*_LightColor0.rgb*.7;
                float3 tint=lerp(_Color.rgb,environment,.28)+highlight*.65+_Selection.rgb*.5;
                float alpha=saturate(_Color.a+rim*_RimStrength+max(max(highlight.r,highlight.g),highlight.b)*.10);
                return fixed4(tint,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
