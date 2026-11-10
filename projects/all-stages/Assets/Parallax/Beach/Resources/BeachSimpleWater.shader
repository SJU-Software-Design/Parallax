// Built-in adaptation of IgniteCoders' WaterShader Shader Graph; see ThirdParty/SimpleWaterShaderURP/source.json.
Shader "Parallax/BeachSimpleWater"
{
    Properties
    {
        _MainNormal("Main water texture",2D)="gray"{}
        _SecondNormal("Second water texture",2D)="gray"{}
        _DeepColor("Deep color",Color)=(.11,.12,.11,.10)
        _ShallowColor("Shallow color",Color)=(.15,.16,.14,.025)
        _NormalTiling("Normal repeats per 50 metres",Float)=10
        _NormalStrength("Normal strength",Range(0,1))=.12
        _Smoothness("Smoothness",Range(0,1))=.95
        _DepthStrength("Depth strength",Float)=.35
        _WaveHeight("Displacement in metres",Float)=.025
        _Flow("Flow direction XZ / speed",Vector)=(0,0,0,0)
        _Area("Area min XZ / max XZ",Vector)=(0,0,0,0)
        _WaterKind("Water kind",Float)=0
        _Absorption("Water absorption",Vector)=(.55,.24,.14,0)
    }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent"}
        LOD 200
        GrabPass {"_BeachWaterBackground"}
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf StandardSpecular alpha:premul fullforwardshadows vertex:vert
        sampler2D _MainNormal,_SecondNormal,_BeachWaterBackground;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        fixed4 _DeepColor,_ShallowColor;
        float4 _Flow,_Area,_Absorption;
        float _NormalTiling,_NormalStrength,_Smoothness,_DepthStrength,_WaveHeight,_WaterKind;
        struct Input {float3 worldPos;float4 screenPos;float4 grabPos;};

        float2 GradientDirection(float2 p)
        {
            p=fmod(p,289);
            float x=fmod((34*p.x+1)*p.x,289)+p.y;
            x=fmod((34*x+1)*x,289);
            x=frac(x/41)*2-1;
            return normalize(float2(x-floor(x+.5),abs(x)-.5));
        }
        float GradientNoise(float2 p)
        {
            float2 cell=floor(p),f=frac(p);
            float a=dot(GradientDirection(cell),f);
            float b=dot(GradientDirection(cell+float2(0,1)),f-float2(0,1));
            float c=dot(GradientDirection(cell+float2(1,0)),f-float2(1,0));
            float d=dot(GradientDirection(cell+1),f-1);
            f=f*f*f*(f*(f*6-15)+10);
            return lerp(lerp(a,b,f.y),lerp(c,d,f.y),f.x)+.5;
        }
        float2 WaterPosition(float3 world)
        {
            float2 flow=dot(_Flow.xy,_Flow.xy)>.00001?normalize(_Flow.xy):float2(0,1);
            return world.xz-flow*_Flow.z*_Time.y;
        }
        void vert(inout appdata_full v,out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input,data);
            float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
            world.y+=(GradientNoise(WaterPosition(world)*.4)-.5)*_WaveHeight;
            v.vertex=mul(unity_WorldToObject,float4(world,1));
            data.grabPos=ComputeGrabScreenPos(UnityObjectToClipPos(v.vertex));
        }
        void surf(Input i,inout SurfaceOutputStandardSpecular o)
        {
            // Keep the ocean beneath the channel from adding a second transparent layer.
            if(_WaterKind<.5&&all(i.worldPos.xz>=_Area.xy)&&all(i.worldPos.xz<=_Area.zw))clip(-1);
            float sceneDepth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screenPos)));
            // Vertical thickness keeps the tint stable when the viewing angle changes.
            float thickness=max(0,sceneDepth-i.screenPos.w)*abs(i.worldPos.y-_WorldSpaceCameraPos.y)/max(.001,i.screenPos.w);
            float depth=saturate(thickness*_DepthStrength);
            fixed4 color=lerp(_ShallowColor,_DeepColor,depth);
            float2 uv=WaterPosition(i.worldPos)*(_NormalTiling/50);
            float3 a=tex2D(_MainNormal,uv).rgb;
            float3 b=tex2D(_SecondNormal,uv).rgb;
            o.Normal=normalize(float3((a.rg+b.rg-1)*2*_NormalStrength,1));
            float4 grab=i.grabPos;
            float2 offset=o.Normal.xy*.025*saturate(thickness*2);
            float4 refractedDepthPos=i.screenPos;
            float2 depthOffset=offset;
            #if UNITY_UV_STARTS_AT_TOP
            depthOffset.y*=-_ProjectionParams.x;
            #else
            depthOffset.y*=_ProjectionParams.x;
            #endif
            refractedDepthPos.xy+=depthOffset*refractedDepthPos.w;
            float refractedDepth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(refractedDepthPos)));
            grab.xy+=offset*grab.w*step(i.screenPos.w,refractedDepth);
            fixed3 refracted=tex2Dproj(_BeachWaterBackground,UNITY_PROJ_COORD(grab)).rgb;
            float3 transmission=exp(-_Absorption.xyz*thickness);
            refracted=refracted*transmission+float3(.045,.19,.21)*(1-transmission);
            o.Albedo=0;o.Emission=lerp(refracted,color.rgb,color.a);
            o.Alpha=1;o.Specular=.02;o.Smoothness=_Smoothness;
        }
        ENDCG
    }
    Fallback "Transparent/Diffuse"
}
