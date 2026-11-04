Shader "Parallax/GalleryOrcusPortal"
{
    Properties
    {
        _Color ("Stone or aperture tint", Color) = (.65,.58,.44,1)
        _MainTex ("Base color", 2D) = "white" {}
        _Selection ("Interaction accent", Color) = (0,0,0,0)
        _StoneMode ("Carved stone", Float) = 0
        _Pulse ("Touch ripple", Range(0,1)) = 0
        _Near ("Near the mouth", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _Selection;
            float _StoneMode, _Pulse, _Near;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; float3 local:TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                o.normal=UnityObjectToWorldNormal(v.normal); o.local=v.vertex.xyz; return o;
            }
            float noise(float3 p) { return frac(sin(dot(p,float3(12.9898,78.233,37.719)))*43758.5453); }
            fixed4 frag(v2f i):SV_Target
            {
                if(_StoneMode>.5)
                {
                    float3 n=normalize(i.normal);
                    float key=.52+.48*saturate(dot(n,normalize(float3(-.35,.8,-.6))));
                    float grain=noise(floor(i.local*135))*.07+noise(floor(i.local*19))*.035;
                    float strata=sin(i.local.y*43+i.local.x*4)*.014;
                    return float4(_Color.rgb*(key+grain+strata)+_Selection.rgb,1);
                }
                float2 p=(i.uv-.5)*2;
                float radius=length(p), angle=atan2(p.y,p.x), time=_Time.y;
                float wave=sin(radius*30-time*2.2+sin(angle*3+time)*(.5+_Pulse*1.4));
                float ribbon=pow(saturate(.5+.5*sin(angle*4-radius*14+time*.8)),14);
                float rim=pow(saturate(radius),6);
                float3 dark=float3(.006,.011,.014);
                float3 mist=float3(.07,.10,.105)*(ribbon*.20+rim*.45+wave*.04)*(.5+_Near*.5+_Pulse*.5);
                return float4(dark+mist+_Selection.rgb*.35,1);
            }
            ENDCG
        }
    }
}
