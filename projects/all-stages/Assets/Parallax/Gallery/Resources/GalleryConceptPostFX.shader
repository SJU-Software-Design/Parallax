Shader "Parallax/GalleryConceptPostFX"
{
    Properties { _MainTex ("Scene",2D)="white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex,_CameraDepthNormalsTexture,_AmbientBloom;
        float4 _MainTex_TexelSize,_AmbientBloom_TexelSize;
        float4 _CameraProjection,_Occlusion,_Tone;
        float _BloomThreshold,_DebugOcclusion;
        struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0; };
        struct v2f { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 depthUV:TEXCOORD1; };
        v2f vert(appdata input)
        {
            v2f output;output.vertex=UnityObjectToClipPos(input.vertex);
            output.uv=input.uv;output.depthUV=input.uv;
            #if UNITY_UV_STARTS_AT_TOP
            if(_MainTex_TexelSize.y<0)output.depthUV.y=1-output.depthUV.y;
            #endif
            return output;
        }
        void surface(float2 uv,out float depth,out float3 normal)
        {
            DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture,saturate(uv)),depth,normal);
        }
        float3 viewPosition(float2 uv,float depth)
        {
            float eyeDepth=depth*_CameraProjection.z;
            return float3((uv*2-1)*_CameraProjection.xy,-1)*eyeDepth;
        }
        float3 bright(float2 uv)
        {
            float3 color=tex2D(_MainTex,saturate(uv)).rgb;
            float luminance=dot(color,float3(.2126,.7152,.0722));
            return color*max(luminance-_BloomThreshold,0)/max(luminance,.001);
        }
        float3 filmCurve(float3 color)
        {
            return saturate((color*(2.51*color+.03))/(color*(2.43*color+.59)+.14));
        }
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment ambientAndBloom
            float4 ambientAndBloom(v2f input):SV_Target
            {
                float depth;float3 normal;surface(input.depthUV,depth,normal);
                float occlusion=0;
                if(depth>.0001&&depth<.9998&&_Occlusion.y>.001)
                {
                    float3 center=viewPosition(input.depthUV,depth);
                    float2 radiusUV=_Occlusion.x/(max(-center.z,.15)*2*_CameraProjection.xy);
                    // A fixed two-ring pattern is stable under mouse movement;
                    // there is no frame noise, temporal ghosting or depth of field.
                    const float2 directions[10]={
                        float2(.24,.08),float2(-.17,.27),float2(-.30,-.16),float2(.12,-.38),
                        float2(.61,.42),float2(-.72,.36),float2(-.65,-.57),float2(.68,-.59),
                        float2(.05,.97),float2(.96,-.03)};
                    [unroll] for(int sampleIndex=0;sampleIndex<10;sampleIndex++)
                    {
                        float2 sampleUV=input.depthUV+directions[sampleIndex]*radiusUV;
                        float sampleDepth;float3 unused;surface(sampleUV,sampleDepth,unused);
                        float3 difference=viewPosition(sampleUV,sampleDepth)-center;
                        float distance=length(difference);
                        float horizon=saturate((dot(difference/max(distance,.001),normal)-_Occlusion.z)*2.1);
                        float range=1-smoothstep(_Occlusion.x*.22,_Occlusion.x,distance);
                        if(sampleDepth>.0001&&sampleDepth<.9998)occlusion+=horizon*range;
                    }
                }
                float ao=1-saturate(occlusion*.18)*_Occlusion.y;
                float2 texel=abs(_MainTex_TexelSize.xy)*2;
                float3 bloom=bright(input.uv)*.36;
                bloom+=(bright(input.uv+float2(texel.x,0))+bright(input.uv-float2(texel.x,0))+
                    bright(input.uv+float2(0,texel.y))+bright(input.uv-float2(0,texel.y)))*.16;
                return float4(bloom,ao);
            }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment composite
            float4 composite(v2f input):SV_Target
            {
                float3 color=tex2D(_MainTex,input.uv).rgb;
                float centerDepth;float3 centerNormal;surface(input.depthUV,centerDepth,centerNormal);
                float4 treatment=tex2D(_AmbientBloom,input.uv)*2;
                float total=2;
                const float2 neighbors[4]={float2(1,0),float2(-1,0),float2(0,1),float2(0,-1)};
                [unroll] for(int sampleIndex=0;sampleIndex<4;sampleIndex++)
                {
                    float2 offset=neighbors[sampleIndex]*abs(_AmbientBloom_TexelSize.xy);
                    float2 depthOffset=offset;
                    #if UNITY_UV_STARTS_AT_TOP
                    if(_MainTex_TexelSize.y<0)depthOffset.y=-depthOffset.y;
                    #endif
                    float depth;float3 normal;surface(input.depthUV+depthOffset,depth,normal);
                    float weight=exp(-abs(depth-centerDepth)*_CameraProjection.z*3)*pow(saturate(dot(normal,centerNormal)),4);
                    treatment+=tex2D(_AmbientBloom,saturate(input.uv+offset))*weight;
                    total+=weight;
                }
                treatment/=total;
                if(_DebugOcclusion>.5)return float4(treatment.aaa,1);
                #if defined(UNITY_COLORSPACE_GAMMA)
                color=GammaToLinearSpace(color);
                #endif
                color=color*lerp(1,treatment.a,.88)+treatment.rgb*_Tone.w;
                color*=max(.01,_Tone.x);
                color=lerp(color,filmCurve(color),_Tone.y);
                float luminance=dot(color,float3(.2126,.7152,.0722));
                color=lerp(luminance.xxx,color,_Tone.z);
                #if defined(UNITY_COLORSPACE_GAMMA)
                color=LinearToGammaSpace(color);
                #endif
                return float4(color,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
