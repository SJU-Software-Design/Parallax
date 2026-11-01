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
