using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink
{
    // Rasterize the projected compound collider, including its interior faces and
    // holes. Each occupied screen sample carries the back surface on that ray.
    // A world hit limits only that ray, never an imaginary infinite support plane.
    public sealed class PerspectivePlacement
    {
        const int MaximumRasterSamples=2048;
        const float PixelSpacing=4,ContactSkin=.002f;
        struct Shape { public Vector3 center,half;public Quaternion rotation; }
        struct ProjectedShape { public Shape shape;public Quaternion inverse;public Vector3 origin; }
        struct Sample { public Vector3 direction;public float backDistance; }
        public enum LimitKind { Surface, Boundary, Size, Intent, Fallback, Minimum, Volume, OriginInside }
        public struct Result
        {
            public float Depth,RoomMinimum,RoomMaximum;
            public bool HasSurface,Feasible;
            public int Samples;
            public LimitKind Limiter;
            public Collider Source;
            public Vector3 Point;
            public bool HasPoint;
            public string Boundary;
            public float SurfaceDepth,BoundaryDepth,SizeDepth,IntentDepth;
        }
        public int Queries { get; private set; }
        readonly Shape[] shapes;
        readonly Vector3[] corners;
        readonly Transform held;
        readonly Vector3 grab;
        readonly float ratio;
        readonly List<Sample> samples=new List<Sample>();
        readonly List<ProjectedShape> projected=new List<ProjectedShape>();
        RaycastHit[] hits=new RaycastHit[64];
        Collider[] overlaps=new Collider[32];
        Collider queryBlocker;
        Quaternion cachedRotation;
        Vector3 cachedDirection;
        float cachedFov,cachedAspect;
        int cachedWidth,cachedHeight;
        bool cached;

        public PerspectivePlacement(Transform prop,BoxCollider[] parts,Vector3 grabPoint,float scale,float distance)
        {
            Vector3 shapeRatio=prop.localScale/scale;
            held=prop;grab=Vector3.Scale(grabPoint,shapeRatio);ratio=scale/distance;shapes=new Shape[parts.Length];corners=new Vector3[parts.Length*8];
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];Vector3 size=Vector3.Scale(part.size*.5f,part.transform.lossyScale)/scale;
                shapes[i]=new Shape {center=Vector3.Scale(prop.InverseTransformPoint(part.transform.TransformPoint(part.center)),shapeRatio),
                    half=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z)),
                    rotation=Quaternion.Inverse(prop.rotation)*part.transform.rotation};
                for(int k=0;k<8;k++)corners[i*8+k]=shapes[i].center+shapes[i].rotation*Vector3.Scale(shapes[i].half,
                    new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));
            }
        }

        public Result Resolve(Camera camera,Quaternion rotation,Collider player,int mask,float minimumScale,float maximumScale,
            Vector3 minimum,Vector3 maximum,float maximumIntent=float.PositiveInfinity,Vector3 direction=default)
        {
            Queries=0;
            // Query current scripted obstacle transforms even with auto-sync disabled.
            Physics.SyncTransforms();
            mask&=~(1<<HeldCollisionState.Layer);
            float minDepth=minimumScale/ratio,maxDepth=maximumScale/ratio,lo=minDepth,hi=float.PositiveInfinity;
            string boundary=null;Vector3 boundaryPoint=default;
            Transform view=camera.transform;Quaternion cameraRotation=view.rotation;
            Vector3 eye=view.position,forward=direction.sqrMagnitude<.000001f?view.forward:direction.normalized;
            // Raycasts omit a collider containing their origin. An eye inside a
            // solid must not turn that solid into an unobstructed background hit.
            if(OriginBlocked(eye,player,mask))
                return new Result {Depth=minDepth,RoomMinimum=lo,RoomMaximum=maxDepth,Feasible=false,HasSurface=true,Limiter=LimitKind.OriginInside,Source=queryBlocker,SurfaceDepth=float.PositiveInfinity,BoundaryDepth=float.PositiveInfinity,SizeDepth=maxDepth,IntentDepth=maximumIntent};
            foreach(var point in corners)
            {
                Vector3 velocity=forward+rotation*((point-grab)*ratio);
                float previous=hi;
                InteractionRaycast.ClipDepth(eye.x,velocity.x,minimum.x,maximum.x,ref lo,ref hi);
                if(hi<previous){boundary=velocity.x>0?"X 최대":"X 최소";boundaryPoint=eye+velocity*hi;}previous=hi;
                InteractionRaycast.ClipDepth(eye.y,velocity.y,minimum.y,maximum.y,ref lo,ref hi);
                if(hi<previous){boundary=velocity.y>0?"Y 최대":"Y 최소";boundaryPoint=eye+velocity*hi;}previous=hi;
                InteractionRaycast.ClipDepth(eye.z,velocity.z,minimum.z,maximum.z,ref lo,ref hi);
                if(hi<previous){boundary=velocity.z>0?"Z 최대":"Z 최소";boundaryPoint=eye+velocity*hi;}
            }
            float boundaryDepth=hi;hi=Mathf.Min(hi,maxDepth);
            CacheSamples(camera,Quaternion.Inverse(cameraRotation)*rotation,Quaternion.Inverse(cameraRotation)*forward);
            bool surface=false;float limit=float.PositiveInfinity;Collider source=null;Vector3 hitPoint=default;
            foreach(var sample in samples)
            {
                bool found=InteractionRaycast.Nearest(eye,cameraRotation*sample.direction,player,held,mask,ref hits,out var hit,out int queries);
                Queries+=queries;
                if(found)
                {
                    surface=true;float candidate=Mathf.Max(0,hit.distance-ContactSkin)/sample.backDistance;
                    if(candidate<limit){limit=candidate;source=hit.collider;hitPoint=hit.point;}
                }
            }
            if(surface)limit*=.99f;
            float target=surface?limit:400f;
            LimitKind kind=surface?LimitKind.Surface:LimitKind.Fallback;
            bool hasPoint=surface;Vector3 diagnosticPoint=hitPoint;
            if(maximumIntent<target){kind=LimitKind.Intent;source=null;hasPoint=false;}
            target=Mathf.Min(target,maximumIntent);
            if(boundaryDepth<target){kind=LimitKind.Boundary;source=null;hasPoint=true;diagnosticPoint=boundaryPoint;}
            if(maxDepth<Mathf.Min(target,boundaryDepth)){kind=LimitKind.Size;source=null;hasPoint=false;}
            if(lo>Mathf.Min(target,hi))kind=LimitKind.Minimum;
            bool feasible=lo<=hi&&Mathf.Min(limit,maximumIntent)>=lo;
            target=feasible?Mathf.Clamp(target,lo,hi):Mathf.Clamp(target,minDepth,maxDepth);
            // Finite raster samples can straddle a thin obstacle's edge. Refine
            // the endpoint with the full collision volume before handing it on.
            if(feasible&&!ClearAt(eye,forward,rotation,target,player,mask))
            {
                Collider upperBlocker=queryBlocker;
                if(ClearAt(eye,forward,rotation,lo,player,mask))
                {
                    float lower=lo,upper=target;
                    for(int i=0;i<18;i++)
                    {
                        float middle=(lower+upper)*.5f;
                        if(ClearAt(eye,forward,rotation,middle,player,mask))lower=middle;
                        else {upper=middle;upperBlocker=queryBlocker;}
                    }
                    target=lower;
                }
                kind=LimitKind.Volume;source=upperBlocker;hasPoint=false;
            }
            return new Result {Depth=target,RoomMinimum=lo,RoomMaximum=hi,Feasible=feasible,HasSurface=surface,Samples=samples.Count,
                Limiter=kind,Source=source,Point=diagnosticPoint,HasPoint=hasPoint,Boundary=boundary,
                SurfaceDepth=limit,BoundaryDepth=boundaryDepth,SizeDepth=maxDepth,IntentDepth=maximumIntent};
        }

        void CacheSamples(Camera camera,Quaternion rotation,Vector3 direction)
        {
            int width=Mathf.Max(1,camera.pixelWidth),height=Mathf.Max(1,camera.pixelHeight);
            if(cached&&Vector3.Angle(cachedDirection,direction)<.01f&&Quaternion.Angle(cachedRotation,rotation)<.01f&&Mathf.Abs(cachedFov-camera.fieldOfView)<.001f&&
                Mathf.Abs(cachedAspect-camera.aspect)<.001f&&cachedWidth==width&&cachedHeight==height)return;
            cached=true;cachedDirection=direction;cachedRotation=rotation;cachedFov=camera.fieldOfView;cachedAspect=camera.aspect;cachedWidth=width;cachedHeight=height;
            samples.Clear();projected.Clear();
            float halfHeight=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f),halfWidth=halfHeight*camera.aspect;
            Vector2 lower=Vector2.one*float.PositiveInfinity,upper=Vector2.one*float.NegativeInfinity;
            foreach(var shape in shapes)
            {
                var projection=new Shape {center=direction+rotation*((shape.center-grab)*ratio),
                    half=shape.half*ratio,rotation=rotation*shape.rotation};
                Quaternion inverse=Quaternion.Inverse(projection.rotation);
                projected.Add(new ProjectedShape {shape=projection,inverse=inverse,origin=inverse*(-projection.center)});
            }
            foreach(var point in corners)
            {
                Vector3 v=direction+rotation*((point-grab)*ratio);
                if(v.z<=.00001f){lower=new Vector2(-halfWidth,-halfHeight);upper=-lower;continue;}
                Vector2 screen=new Vector2(v.x/v.z,v.y/v.z);lower=Vector2.Min(lower,screen);upper=Vector2.Max(upper,screen);
            }
            // The carried silhouette can extend beyond the screen. Those parts
            // must still constrain placement against walls and door frames.
            if(lower.x>upper.x||lower.y>upper.y)return;
            int columns=Mathf.Max(2,Mathf.CeilToInt((upper.x-lower.x)/(2*halfWidth)*width/PixelSpacing));
            int rows=Mathf.Max(2,Mathf.CeilToInt((upper.y-lower.y)/(2*halfHeight)*height/PixelSpacing));
            float reduction=Mathf.Max(1,Mathf.Sqrt((columns+1f)*(rows+1f)/MaximumRasterSamples));
            columns=Mathf.Max(2,Mathf.FloorToInt(columns/reduction));rows=Mathf.Max(2,Mathf.FloorToInt(rows/reduction));
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
                AddSample(new Vector3(Mathf.Lerp(lower.x,upper.x,(float)x/columns),Mathf.Lerp(lower.y,upper.y,(float)y/rows),1));
            // Keep the aim and thin compound parts represented even when they
            // fall between raster cells. Empty holes still produce no sample.
            AddSample(Vector3.forward);
            foreach(var projection in projected)
            {
                var shape=projection.shape;
                AddSample(shape.center);
                for(int k=0;k<8;k++)AddSample(shape.center+shape.rotation*Vector3.Scale(shape.half,
                    new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));
            }
        }

        void AddSample(Vector3 direction)
        {
            if(direction.z<=.00001f)return;
            direction.Normalize();float back=0;
            foreach(var projection in projected)
            {
                var shape=projection.shape;
                Vector3 origin=projection.origin,ray=projection.inverse*direction;
                float lo=0,hi=float.PositiveInfinity;
                InteractionRaycast.ClipDepth(origin.x,ray.x,-shape.half.x,shape.half.x,ref lo,ref hi);
                InteractionRaycast.ClipDepth(origin.y,ray.y,-shape.half.y,shape.half.y,ref lo,ref hi);
                InteractionRaycast.ClipDepth(origin.z,ray.z,-shape.half.z,shape.half.z,ref lo,ref hi);
                if(lo<=hi&&hi>0)back=Mathf.Max(back,hi);
            }
            if(back>0)samples.Add(new Sample {direction=direction,backDistance=back});
        }

        bool OriginBlocked(Vector3 eye,Collider player,int mask)
        {
            queryBlocker=null;int count;
            while(true)
            {
                count=Physics.OverlapSphereNonAlloc(eye,.0001f,overlaps,mask,QueryTriggerInteraction.Ignore);Queries++;
                if(count<overlaps.Length)break;
                if(overlaps.Length>=4096){overlaps=Physics.OverlapSphere(eye,.0001f,mask,QueryTriggerInteraction.Ignore);count=overlaps.Length;Queries++;break;}
                System.Array.Resize(ref overlaps,overlaps.Length*2);
            }
            for(int i=0;i<count;i++)
                if(overlaps[i]!=player&&!overlaps[i].transform.IsChildOf(held)){queryBlocker=overlaps[i];return true;}
            return false;
        }

        bool ClearAt(Vector3 eye,Vector3 forward,Quaternion rotation,float depth,Collider player,int mask)
        {
            queryBlocker=null;
            float scale=ratio*depth;
            foreach(var shape in shapes)
            {
                Vector3 center=eye+forward*depth+rotation*((shape.center-grab)*scale);
                Vector3 half=Vector3.Max(Vector3.one*.0001f,shape.half*scale-Vector3.one*.001f);
                int count;
                while(true)
                {
                    count=Physics.OverlapBoxNonAlloc(center,half,overlaps,rotation*shape.rotation,mask,QueryTriggerInteraction.Ignore);Queries++;
                    if(count<overlaps.Length)break;
                    if(overlaps.Length>=4096){overlaps=Physics.OverlapBox(center,half,rotation*shape.rotation,mask,QueryTriggerInteraction.Ignore);count=overlaps.Length;Queries++;break;}
                    System.Array.Resize(ref overlaps,overlaps.Length*2);
                }
                for(int i=0;i<count;i++)
                    if(overlaps[i]!=player&&!overlaps[i].transform.IsChildOf(held)){queryBlocker=overlaps[i];return false;}
            }
            return true;
        }
    }
}
