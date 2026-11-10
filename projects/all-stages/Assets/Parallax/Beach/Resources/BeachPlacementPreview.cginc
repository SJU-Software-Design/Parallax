sampler2D _PlacementPreviewMap;
float4x4 _PlacementPreviewVP,_PlacementPreviewView;
float _PlacementPreviewActive,_PlacementPreviewFar;

void BeachPreviewColor(float3 worldPos,inout fixed4 color)
{
    #ifndef UNITY_PASS_FORWARDADD
    if(_PlacementPreviewActive>.5)
    {
        float4 clip=mul(_PlacementPreviewVP,float4(worldPos,1));float2 uv=clip.xy/clip.w*.5+.5;
        #if UNITY_UV_STARTS_AT_TOP
        uv.y=1-uv.y;
        #endif
        float depth=-mul(_PlacementPreviewView,float4(worldPos,1)).z/_PlacementPreviewFar;
        float nearest=tex2D(_PlacementPreviewMap,uv).r;
        if(all(uv>0)&&all(uv<1)&&depth>nearest+.00045&&nearest<.999)
        {float stripe=step(.57,frac((worldPos.x+worldPos.z)*3));color.rgb=lerp(color.rgb,float3(.05,.60,1),.2+stripe*.57);}
    }
    #endif
}
