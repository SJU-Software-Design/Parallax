cbuffer Globals : register(b0) {
    float4 _MainTex_ST : packoffset(c2);
    float4 _Color : packoffset(c3);
    float4 _Selection : packoffset(c4);
    float _Grid : packoffset(c5.x);
    float _Diagnostic : packoffset(c5.y);
    int _GalleryCasterCount : packoffset(c5.z);
    column_major float4x4 _GalleryCasterInverse[128] : packoffset(c6);
    float4 _GalleryWindows[4] : packoffset(c518);
    float3 _GalleryToSun : packoffset(c522);
    float _GalleryRoof : packoffset(c522.w);
};
Texture2D _MainTex : register(t0);
SamplerState sampler_MainTex : register(s0);
struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
// CPU counterpart: GalleryCurvedCaster. Profile points are packed eight per
// unused matrix slot. Slot 127 holds world-to-local plus a metadata fourth row.
bool curveRoot(float t,float3 o,float3 d,float low,float high) {
    float y=o.y+d.y*t;
    return t>.0001 && y>=low-.00001 && y<=high+.00001;
}
bool curveSegment(float3 o,float3 d,float2 p,float2 q) {
    float h=q.y-p.y,low=min(p.y,q.y),high=max(p.y,q.y);
    if(abs(h)<.000001) {
        if(abs(d.y)<.0000001)return false;
        float t=(p.y-o.y)/d.y;if(t<=.0001)return false;
        float x=o.x+d.x*t,z=o.z+d.z*t,r2=x*x+z*z;
        return r2>=min(p.x*p.x,q.x*q.x)&&r2<=max(p.x*p.x,q.x*q.x);
    }
    float slope=(q.x-p.x)/h,r=p.x+slope*(o.y-p.y),rd=slope*d.y;
    float a=d.x*d.x+d.z*d.z-rd*rd;
    float b=2*(o.x*d.x+o.z*d.z-r*rd);
    float c=o.x*o.x+o.z*o.z-r*r;
    if(abs(a)<.0000001)return abs(b)>.0000001&&curveRoot(-c/b,o,d,low,high);
    float disc=b*b-4*a*c;if(disc<0)return false;
    float root=sqrt(disc),stable=-.5*(b+(b<0?-root:root));
    if(abs(stable)<.0000000001)return curveRoot(-b/(2*a),o,d,low,high);
    return curveRoot(stable/a,o,d,low,high)||curveRoot(c/stable,o,d,low,high);
}
float2 curvePoint(int start,uint sampleIndex) {
    float4 row=_GalleryCasterInverse[start+sampleIndex/8][(sampleIndex%8)/2];
    return (sampleIndex%2)==0?row.xy:row.zw;
}
bool curvedShadow(float3 origin,float3 direction) {
    float4 header=_GalleryCasterInverse[127][3];
    if(header.w!=2)return false;
    float3 o=mul(_GalleryCasterInverse[127],float4(origin,1)).xyz;
    float3 d=mul((float3x3)_GalleryCasterInverse[127],direction);
    float enter=0,leave=1e20;
    [unroll] for(int axis=0;axis<3;axis++) {
        float extent=axis==1?1.06:.8;
        if(abs(d[axis])<.0000001) {if(abs(o[axis])>extent)return false;}
        else {
            float a=(-extent-o[axis])/d[axis],b=(extent-o[axis])/d[axis];
            enter=max(enter,min(a,b));leave=min(leave,max(a,b));
        }
    }
    if(leave<enter)return false;
    o+=d*max(0,enter-.01);
    int count=(int)header.x,start=(int)header.y;
    float2 previous=curvePoint(start,0);
    [loop] for(uint sampleIndex=1;sampleIndex<(uint)count;sampleIndex++) {
        float2 next=curvePoint(start,sampleIndex);
        if(curveSegment(o,d,previous,next))return true;
        previous=next;
    }
    return false;
}

            float sunlit(float3 surfacePosition, float3 normal) {
                float3 aperture = surfacePosition + _GalleryToSun * ((_GalleryRoof-surfacePosition.y)/_GalleryToSun.y);
                bool inWindow = false;
                [unroll] for(int w=0;w<4;w++) {
                    float4 r = _GalleryWindows[w];
                    inWindow = inWindow || (aperture.x>=r.x && aperture.x<=r.y && aperture.z>=r.z && aperture.z<=r.w);
                }
                if (!inWindow) return 0;
                float3 origin = surfacePosition + normal * .012;
                [loop] for(int i=0;i<_GalleryCasterCount;i++) {
                    float3 o=mul(_GalleryCasterInverse[i],float4(origin,1)).xyz;
                    float3 d=mul((float3x3)_GalleryCasterInverse[i],_GalleryToSun);
                    d=float3(abs(d.x)<.000001?(d.x<0?-.000001:.000001):d.x,
                             abs(d.y)<.000001?(d.y<0?-.000001:.000001):d.y,
                             abs(d.z)<.000001?(d.z<0?-.000001:.000001):d.z);
                    float3 a=(-.5-o)/d, b=(.5-o)/d;
                    float3 low=min(a,b), high=max(a,b);
                    float enter=max(max(low.x,low.y),low.z), leave=min(min(high.x,high.y),high.z);
                    if(leave>=max(enter,.001)) return 0;
                }
                return curvedShadow(origin,_GalleryToSun)?0:1;
            }
            float4 frag(v2f i): SV_Target {
                float3 n=normalize(i.normal); float lit=sunlit(i.world,n);
                if(_Diagnostic>.5) return float4(lit,lit,lit,1);
                float3 albedo=_MainTex.Sample(sampler_MainTex,i.uv).rgb*_Color.rgb;
                float face=.78+.22*max(0,dot(n,normalize(float3(.35,1,-.5))));
                float3 shade=float3(.21,.30,.34)*face;
                float3 direct=float3(1.05,.90,.66)*(.80+.20*max(0,dot(n,_GalleryToSun)));
                float3 col=albedo*lerp(shade,direct,lit);
                if(_Grid>.5 && n.y>.9) {
                    float2 tile=abs(frac(i.world.xz/2)-.5);
                    float joint=step(.493,max(tile.x,tile.y)); col*=1-joint*.14;
                }
                col+=_Selection.rgb;
                return float4(col,1);
            }
