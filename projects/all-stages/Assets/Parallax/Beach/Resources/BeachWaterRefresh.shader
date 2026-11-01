Shader "Parallax/BeachWaterRefresh"
{
    Properties
    {
        _Flow("Flow direction XZ / speed",Vector)=(0,1,.3,0)
        _Area("Area min XZ / max XZ",Vector)=(0,0,0,0)
        _WaterKind("Water kind: ocean 0, puddle 1, channel 2",Float)=0
        _DeepColor("Deep teal",Color)=(.035,.31,.39,1)
        _ShallowColor("Shallow turquoise",Color)=(.12,.57,.59,1)
        _FoamColor("Soft foam",Color)=(.84,.94,.86,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct input { float4 vertex:POSITION; };
            struct output
            {
                float4 pos:SV_POSITION;
                float3 world:TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };
            float4 _Flow,_Area;
            float _WaterKind;
            fixed4 _DeepColor,_ShallowColor,_FoamColor;

            float Hash(float2 p)
            {
                p=frac(p*float2(123.34,345.45));
                p+=dot(p,p+34.345);
                return frac(p.x*p.y);
            }
            float Noise(float2 p)
            {
                float2 cell=floor(p),blend=frac(p);
                blend=blend*blend*(3-2*blend);
                float a=Hash(cell),b=Hash(cell+float2(1,0));
                float c=Hash(cell+float2(0,1)),d=Hash(cell+float2(1,1));
                return lerp(lerp(a,b,blend.x),lerp(c,d,blend.x),blend.y);
            }
            output vert(input v)
            {
                output o;o.pos=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            fixed4 frag(output i):SV_Target
            {
                float puddle=step(.5,_WaterKind)*(1-step(1.5,_WaterKind));
                float channel=step(1.5,_WaterKind);
                float2 flow=dot(_Flow.xy,_Flow.xy)>.00001?normalize(_Flow.xy):float2(0,1);
                float2 side=float2(-flow.y,flow.x);
                float speed=max(0,_Flow.z);
                // Every detail advects downstream. A small independent wind
                // ripple keeps puddles alive without making them look like rapids.
                float2 p=i.world.xz-flow*_Time.y*speed*.55;
                float2 wind=i.world.xz-float2(.065,.032)*_Time.y;
                float broad=Noise(p*.095+float2(13.7,5.2));
                float medium=Noise(p*.31+float2(-8.1,17.6));
                float2 warp=float2(Noise(p*.17+3.4),Noise(p*.19+41.8))-.5;
                float2 warped=p+warp*1.8;
                float phaseA=dot(warped,flow)*2.35+broad*4.2;
                float2 crossDirection=normalize(flow*.36+side*.93);
                float phaseB=dot(warped,crossDirection)*4.15+medium*3.8-_Time.y*.11;
                float phaseC=dot(wind,float2(.77,-.64))*7.3+medium*2.1;
                float ripple=sin(phaseA)*.56+sin(phaseB)*.29+sin(phaseC)*.15;
                float footprint=max(length(ddx(i.world.xz)),length(ddy(i.world.xz)));
                float detail=1-smoothstep(.18,.85,footprint);

                // Bounds give a shallow-water impression, not a simulated depth
                // measurement. Zero bounds deliberately disable edge treatment.
                float2 span=_Area.zw-_Area.xy;
                float areaValid=step(.05,min(span.x,span.y))*step(.5,_WaterKind);
                float2 edgePair=min(i.world.xz-_Area.xy,_Area.zw-i.world.xz);
                float edge=max(0,min(edgePair.x,edgePair.y));
                float shallow=areaValid*(1-smoothstep(.08,lerp(1.6,.75,puddle),edge));
                float variation=broad*.57+medium*.30+ripple*.065+.06;
                float turquoise=saturate(.22+variation*.42+shallow*.34+puddle*.10);
                float3 color=lerp(_DeepColor.rgb,_ShallowColor.rgb,turquoise);
                color+=ripple*.014*lerp(1,.45,puddle)*detail;
                color=lerp(color,float3(.37,.65,.57),shallow*puddle*.16);

                float waveStrength=lerp(.044,.018,puddle);
                float2 slope=flow*cos(phaseA)*waveStrength+
                    crossDirection*cos(phaseB)*waveStrength*.48;
                slope+=float2(.77,-.64)*cos(phaseC)*.009*detail;
                float3 normal=normalize(float3(-slope.x,1,-slope.y));
                float3 view=normalize(_WorldSpaceCameraPos.xyz-i.world);
                float fresnel=pow(1-saturate(dot(normal,view)),3.5);
                float3 sky=float3(.62,.81,.84);
                color=lerp(color,sky,.035+fresnel*.28);
                float3 sun=normalize(_WorldSpaceLightPos0.xyz+float3(.001,.001,0));
                float3 halfway=normalize(sun+view+float3(0,.0001,0));
                float glint=pow(saturate(dot(normal,halfway)),80);
                color+=glint*float3(1,.90,.66)*.19;

                // Broken, irregular flecks replace the old continuous stripes.
                // Long wisps make a channel's direction readable as they travel.
                float along=dot(p,flow),across=dot(p,side);
                float flowFleck=Noise(float2(across*2.1,along*.43)+warp*2.4);
                float breakup=Noise(p*.72+float2(29.4,-11.2));
                float currentFoam=smoothstep(.71,.91,flowFleck)*
                    smoothstep(.36,.73,breakup)*channel*.26*detail;
                float edgeWidth=.23+medium*.21;
                float edgeBand=1-smoothstep(.025,edgeWidth,edge);
                float edgeBreak=smoothstep(.42,.76,Noise(p*2.7+warp*3.1));
                float shoreFoam=edgeBand*edgeBreak*areaValid*lerp(.42,.26,puddle)*detail;
                float oceanFleck=smoothstep(.89,.99,flowFleck)*
                    smoothstep(.60,.85,breakup)*(1-channel)*(1-puddle)*.065*detail;
                color=lerp(color,_FoamColor.rgb,saturate(currentFoam+shoreFoam+oceanFleck));
                fixed4 result=fixed4(color,1);
                UNITY_APPLY_FOG(i.fogCoord,result);
                return result;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
