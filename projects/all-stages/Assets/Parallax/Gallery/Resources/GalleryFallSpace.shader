Shader "Parallax/GalleryFallSpace"
{
    Properties
    {
        _HazeColor ("Arrival haze and camera clear color", Color) = (.24,.21,.17,1)
        _HazeStart ("Haze begins", Float) = 12
        _HazeEnd ("Fully matches camera clear color", Float) = 30
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _HazeColor;
            float _HazeStart, _HazeEnd;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; };
            v2f vert(appdata input)
            {
                v2f output;
                output.position=UnityObjectToClipPos(input.vertex);
                output.world=mul(unity_ObjectToWorld,input.vertex).xyz;
                output.uv=input.uv;
                return output;
            }
            fixed4 frag(v2f input):SV_Target
            {
                float angle=input.uv.x*6.2831853;
                float height=input.uv.y;
                // Periodic angular functions meet exactly at the closed seam.
                // Static shallow bands pass upwards as the camera accelerates.
                float grain=sin(height*9.7+sin(angle*13)*.35)*.007;
                float band=pow(saturate(.5+.5*cos(height*3.14159265)),22);
                float carving=.5+.5*sin(angle*12);
                float3 stone=float3(.085,.072,.057)+grain;
                stone+=float3(.095,.074,.042)*band*(.55+.45*carving);
                float haze=smoothstep(_HazeStart,_HazeEnd,distance(_WorldSpaceCameraPos,input.world));
                return float4(lerp(stone,_HazeColor.rgb,haze),1);
            }
            ENDCG
        }
    }
    Fallback Off
}