Shader "Parallax/StarshipSurface"
{
    Properties {
        _Color("Color",Color)=(1,1,1,1)
        _MainTex("Albedo",2D)="white"{}
        _Metallic("Metallic",Range(0,1))=0
        _Glossiness("Smoothness",Range(0,1))=.2
        _EmissionColor("Emission",Color)=(0,0,0,1)
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows finalcolor:Preview
        #pragma target 3.0
        sampler2D _MainTex,_PlacementPreviewMap;
        fixed4 _Color,_EmissionColor;half _Metallic,_Glossiness;
        float4x4 _PlacementPreviewVP,_PlacementPreviewView;
        float _PlacementPreviewActive,_PlacementPreviewFar;
        struct Input {float2 uv_MainTex;float3 worldPos;};
        void surf(Input i,inout SurfaceOutputStandard o){fixed4 c=tex2D(_MainTex,i.uv_MainTex)*_Color;o.Albedo=c.rgb;o.Alpha=c.a;o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Emission=_EmissionColor.rgb;}
        void Preview(Input i,SurfaceOutputStandard o,inout fixed4 color)
        {
            #ifndef UNITY_PASS_FORWARDADD
            if(_PlacementPreviewActive>.5)
            {
                float4 clip=mul(_PlacementPreviewVP,float4(i.worldPos,1));float2 uv=clip.xy/clip.w*.5+.5;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y=1-uv.y;
                #endif
                float depth=-mul(_PlacementPreviewView,float4(i.worldPos,1)).z/_PlacementPreviewFar;
                float nearest=tex2D(_PlacementPreviewMap,uv).r;
                if(all(uv>0)&&all(uv<1)&&depth>nearest+.00045&&nearest<.999)
                {float stripe=step(.57,frac((i.worldPos.x+i.worldPos.z)*3));color.rgb=lerp(color.rgb,float3(.05,.60,1),.2+stripe*.57);}
            }
            #endif
        }
        ENDCG
    }
    Fallback "Diffuse"
}
