Shader "Parallax/GallerySurface"
{
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Base color", 2D) = "white" {}
        _Selection ("Interaction accent", Color) = (0,0,0,0)
        _Grid ("Floor tile joints", Float) = 0
        _Diagnostic ("Sun visibility diagnostic", Float) = 0
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            Cull Off
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST, _Color, _Selection;
            float _Grid, _Diagnostic;
            int _GalleryCasterCount;
            float4x4 _GalleryCasterInverse[128];
            float _GalleryCasterPreview[128];
            float _GalleryCasterMirror[128];
            float _GalleryMirrorActive;
            float4x4 _GalleryMirrorInverse;
            float3 _GalleryMirrorPosition, _GalleryMirrorNormal, _GalleryReflectedDirection;
            float2 _GalleryMirrorHalf;
            float4 _GalleryWindows[4];
            float3 _GalleryToSun;
            float _GalleryRoof;
            struct appdata { float4 vertex: POSITION; float3 normal: NORMAL; float2 uv: TEXCOORD0; };
            struct v2f { float4 pos: SV_POSITION; float3 world: TEXCOORD0; float3 normal: TEXCOORD1; float2 uv: TEXCOORD2; };
            v2f vert(appdata v) {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal); o.uv = TRANSFORM_TEX(v.uv,_MainTex); return o;
            }
            bool windowLit(float3 location) {
                float3 aperture = location + _GalleryToSun * ((_GalleryRoof-location.y)/_GalleryToSun.y);
                bool inWindow = false;
                [unroll] for(int w=0;w<4;w++) {
                    float4 r = _GalleryWindows[w];
                    inWindow = inWindow || (aperture.x>=r.x && aperture.x<=r.y && aperture.z>=r.z && aperture.z<=r.w);
                }
                return inWindow;
            }
            bool blocked(float3 origin,float3 direction,float maximum,bool skipMirror,bool previewOnly) {
                [loop] for(int i=0;i<_GalleryCasterCount;i++) {
                    if((_GalleryCasterPreview[i]>.5)!=previewOnly)continue;
                    if(skipMirror && _GalleryCasterMirror[i]>.5)continue;
                    float3 o=mul(_GalleryCasterInverse[i],float4(origin,1)).xyz;
                    float3 d=mul((float3x3)_GalleryCasterInverse[i],direction);
                    d=float3(abs(d.x)<.000001?(d.x<0?-.000001:.000001):d.x,
                             abs(d.y)<.000001?(d.y<0?-.000001:.000001):d.y,
                             abs(d.z)<.000001?(d.z<0?-.000001:.000001):d.z);
                    float3 a=(-.5-o)/d, b=(.5-o)/d;
                    float3 low=min(a,b), high=max(a,b);
                    float enter=max(max(low.x,low.y),low.z), leave=min(min(high.x,high.y),high.z);
                    if(leave>=max(enter,.001) && enter<maximum) return true;
                }
                return false;
            }
            float directLit(float3 surfacePosition,float3 normal) {
                if(!windowLit(surfacePosition))return 0;
                return blocked(surfacePosition+normal*.012,_GalleryToSun,1e20,false,false)?0:1;
            }
            float reflectedLit(float3 surfacePosition,float3 normal) {
                if(_GalleryMirrorActive<.5)return 0;
                float denominator=dot(_GalleryReflectedDirection,_GalleryMirrorNormal);
                if(dot(_GalleryMirrorNormal,_GalleryToSun)<=.015 || denominator<=.015)return 0;
                float distance=dot(surfacePosition-_GalleryMirrorPosition,_GalleryMirrorNormal)/denominator;
                if(distance<=.03 || distance>32)return 0;
                float3 reflection=surfacePosition-_GalleryReflectedDirection*distance;
                float3 local=mul(_GalleryMirrorInverse,float4(reflection,1)).xyz;
                if(abs(local.x)>_GalleryMirrorHalf.x || abs(local.y)>_GalleryMirrorHalf.y || !windowLit(reflection))return 0;
                if(blocked(reflection+_GalleryMirrorNormal*.012,_GalleryToSun,1e20,true,false))return 0;
                return blocked(surfacePosition+normal*.012,-_GalleryReflectedDirection,distance-.025,true,false)?0:1;
            }
            float4 frag(v2f i): SV_Target {
                float3 n=normalize(i.normal);float sunlight=directLit(i.world,n),reflection=reflectedLit(i.world,n);float lit=max(sunlight,reflection);
                if(_Diagnostic>.5) return float4(lit,lit,lit,1);
                float3 albedo=tex2D(_MainTex,i.uv).rgb*_Color.rgb;
                float face=.78+.22*max(0,dot(n,normalize(float3(.35,1,-.5))));
                float3 shade=float3(.21,.30,.34)*face;
                float3 direct=float3(1.05,.90,.66)*(.80+.20*max(0,dot(n,_GalleryToSun)));
                if(reflection>.5 && sunlight<.5)direct=float3(1.03,.78,.46)*(.84+.16*max(0,dot(n,-_GalleryReflectedDirection)));
                float3 col=albedo*lerp(shade,direct,lit);
                if(_Grid>.5 && n.y>.9) {
                    float2 tile=abs(frac(i.world.xz/2)-.5);
                    float joint=step(.493,max(tile.x,tile.y)); col*=1-joint*.14;
                }
                bool predicted=sunlight>.5&&blocked(i.world+n*.012,_GalleryToSun,1e20,false,true);
                if(reflection>.5){float d=dot(i.world-_GalleryMirrorPosition,_GalleryMirrorNormal)/dot(_GalleryReflectedDirection,_GalleryMirrorNormal);predicted=predicted||blocked(i.world+n*.012,-_GalleryReflectedDirection,d-.025,true,true);}
                if(predicted){float stripe=step(.64,frac((i.world.x+i.world.y+i.world.z)*3));col=lerp(col,float3(.04,.65,1),.13+stripe*.60);}
                col+=_Selection.rgb;
                return float4(col,1);
            }
            ENDCG
        }
    }
}
