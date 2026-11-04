Shader "Parallax/MuseumLibraryPortal"
{
    Properties
    {
        _PortalTexture ("The library beyond the hands", 2D) = "black" {}
        _PortalReady ("Live view ready", Float) = 0
        _TouchRipple ("Gentle touch ripple", Range(0,1)) = 0
        _PortalNear ("Approach", Range(0,1)) = 0
        _Selection ("Interaction accent", Color) = (0,0,0,0)
        _FlipY ("Texture vertical correction", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Cull Off
            ZWrite On
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _PortalTexture;
            float4x4 _PortalViewProjection;
            float4 _Selection;
            float _PortalReady, _TouchRipple, _PortalNear, _FlipY;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION; float4 portal:TEXCOORD0; float2 uv:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o; o.position=UnityObjectToClipPos(v.vertex);
                o.portal=mul(_PortalViewProjection,mul(unity_ObjectToWorld,v.vertex));
                o.uv=v.uv;return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.portal.xy/max(.00001,i.portal.w)*.5+.5;
                uv.y=lerp(uv.y,1-uv.y,step(.5,_FlipY));
                float2 p=i.uv-.5;
                float radius=length(p);
                float ripple=sin(radius*34-_Time.y*1.8)*(.00015+_TouchRipple*.0014+_PortalNear*.00025);
                uv+=p*ripple;
                float3 library=tex2D(_PortalTexture,saturate(uv)).rgb;
                float3 shade=float3(.018,.009,.004);
                float3 color=lerp(shade,library,saturate(_PortalReady));
                // A faint warm catch on the membrane; the silhouette comes from the actual hand-gap polygon.
                float shimmer=pow(saturate(.5+.5*sin(i.uv.y*27+i.uv.x*11-_Time.y*.45)),12);
                color+=float3(.023,.011,.002)*shimmer*(_TouchRipple*.4+_PortalNear*.08);
                return float4(color+_Selection.rgb*.12,1);
            }
            ENDCG
        }
    }
}
