Shader "Parallax/MuseumBeachPreviewSky"
{
    Properties
    {
        _SourceOrigin("Source origin",Vector)=(0,0,0,1)
        _SourceX("Source X",Vector)=(1,0,0,0)
        _SourceY("Source Y",Vector)=(0,1,0,0)
        _SourceZ("Source Z",Vector)=(0,0,1,0)
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _SourceX,_SourceY,_SourceZ;
            struct appdata {float4 vertex:POSITION;};
            struct v2f {float4 position:SV_POSITION;float3 direction:TEXCOORD0;};
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 local=normalize(i.direction);
                float3 ray=normalize(_SourceX.xyz*local.x+_SourceY.xyz*local.y+_SourceZ.xyz*local.z);
                float up=saturate(ray.y);
                float3 sky=lerp(float3(.60,.81,.90),float3(.22,.52,.79),pow(up,.48));
                sky=lerp(float3(.51,.67,.68),sky,smoothstep(-.17,.03,ray.y));
                float sun=saturate(dot(ray,normalize(float3(.24,1,-.42))));
                sky+=float3(1,.76,.41)*(pow(sun,4200)*1.8+pow(sun,32)*.035);
                return float4(sky,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
