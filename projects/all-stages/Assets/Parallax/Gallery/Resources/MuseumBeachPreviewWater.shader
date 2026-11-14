Shader "Parallax/MuseumBeachPreviewWater"
{
    Properties
    {
        _MainNormal("Original water texture",2D)="gray"{}
        _SecondNormal("Original second water texture",2D)="gray"{}
        _DeepColor("Original deep tint",Color)=(.035,.31,.39,1)
        _ShallowColor("Original shallow tint",Color)=(.12,.57,.59,1)
        _FoamColor("Original foam",Color)=(.84,.94,.86,1)
        _NormalTiling("Normal repeats per 50 metres",Float)=10
        _NormalStrength("Original normal strength",Float)=.12
        _WaveHeight("Original displacement",Float)=.025
        _Flow("Original flow XZ / speed",Vector)=(0,0,0,0)
        _Area("Original area min / max",Vector)=(0,0,0,0)
        _WaterKind("Ocean / puddle / channel",Float)=0
        _SimpleWater("Source uses transmitted-water shading",Float)=0
        _HasNormalTextures("Source water maps present",Float)=0
        _SourceOrigin("Source origin",Vector)=(0,0,0,1)
        _SourceX("Source X",Vector)=(1,0,0,0)
        _SourceY("Source Y",Vector)=(0,1,0,0)
        _SourceZ("Source Z",Vector)=(0,0,1,0)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainNormal,_SecondNormal;
            float4 _DeepColor,_ShallowColor,_FoamColor,_Flow,_Area,_SourceOrigin,_SourceX,_SourceY,_SourceZ;
            float _NormalTiling,_NormalStrength,_WaveHeight,_WaterKind,_SimpleWater,_HasNormalTextures;
            struct appdata {float4 vertex:POSITION;};
            struct v2f {float4 position:SV_POSITION;float3 source:TEXCOORD0;float3 world:TEXCOORD1;};
            float3 SourceDirection(float3 p){return _SourceX.xyz*p.x+_SourceY.xyz*p.y+_SourceZ.xyz*p.z;}
            v2f vert(appdata v)
            {
                v2f o;o.source=_SourceOrigin.xyz+SourceDirection(v.vertex.xyz);
                v.vertex.y+=sin(o.source.x*.43+o.source.z*.37-_Time.y*.33)*_WaveHeight*.35;
                o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;
            }
            float Hash(float2 p){p=frac(p*float2(123.34,345.45));p+=dot(p,p+34.345);return frac(p.x*p.y);}
            float Noise(float2 p)
            {
                float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
            }
            float4 frag(v2f i):SV_Target
            {
                if(_WaterKind<.5&&all(i.source.xz>=_Area.xy)&&all(i.source.xz<=_Area.zw))clip(-1);
                float2 flow=dot(_Flow.xy,_Flow.xy)>.00001?normalize(_Flow.xy):float2(0,1);
                float2 p=i.source.xz-flow*_Time.y*_Flow.z*.55-float2(.022,.013)*_Time.y;
                float broad=Noise(p*.095+float2(13.7,5.2)),medium=Noise(p*.31+float2(-8.1,17.6));
                float ripple=sin(p.x*2.35+p.y*1.24+medium*3.8)*.65+sin(p.y*4.15-p.x*1.4+medium*2.3-_Time.y*.11)*.35;
                float2 slope=float2(cos(p.x*2.35+p.y*1.24)*.035,cos(p.y*4.15-p.x*1.4)*.028);
                if(_HasNormalTextures>.5)
                {
                    float2 uv=p*(_NormalTiling/50);
                    float3 a=tex2D(_MainNormal,uv).rgb,b=tex2D(_SecondNormal,uv).rgb;
                    slope=(a.rg+b.rg-1)*2*_NormalStrength;
                }
                float3 normal=normalize(float3(-slope.x,1,-slope.y));
                float3 view=normalize(SourceDirection(mul((float3x3)unity_WorldToObject,_WorldSpaceCameraPos-i.world)));
                float fresnel=pow(1-saturate(dot(normal,view)),3.5);
                float2 span=_Area.zw-_Area.xy;
                float areaValid=step(.05,min(span.x,span.y))*step(.5,_WaterKind);
                float edge=max(0,min(min(i.source.x-_Area.x,_Area.z-i.source.x),min(i.source.z-_Area.y,_Area.w-i.source.z)));
                float shallow=areaValid*(1-smoothstep(.08,1.3,edge));
                // Simple Water's RGB tints are mostly transparent: preserve its teal absorption
                // appearance here with local scattering, without a GrabPass or museum depth texture.
                float3 deep=lerp(_DeepColor.rgb,float3(.045,.24,.27),_SimpleWater);
                float3 pale=lerp(_ShallowColor.rgb,float3(.14,.49,.50),_SimpleWater);
                float3 color=lerp(deep,pale,saturate(.24+broad*.24+medium*.13+shallow*.32));
                color+=ripple*.009;
                color=lerp(color,float3(.60,.81,.88),.035+fresnel*.38);
                float3 halfway=normalize(normalize(float3(.24,1,-.42))+view);
                color+=pow(saturate(dot(normal,halfway)),96)*float3(1,.90,.66)*.20;
                float fleck=smoothstep(.87,.98,Noise(p*1.7))*smoothstep(.57,.83,Noise(p*.72+29.4));
                float foam=fleck*.07+(1-smoothstep(.025,.34,edge))*areaValid*smoothstep(.46,.8,Noise(p*2.7))*.26;
                color=lerp(color,_FoamColor.rgb,foam);
                float distance=length(_WorldSpaceCameraPos-i.world);
                color=lerp(color,float3(.60,.81,.90),1-exp(-distance*distance*.00001024));
                return float4(color,1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
