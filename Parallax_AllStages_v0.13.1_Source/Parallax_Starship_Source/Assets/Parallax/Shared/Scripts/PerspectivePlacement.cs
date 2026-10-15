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
        public struct Result
        {
            public float Depth,RoomMinimum,RoomMaximum;
            public bool HasSurface,Feasible;
            public int Samples;
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
        Quaternion cachedRotation;
        float cachedFov,cachedAspect;
        int cachedWidth,cachedHeight;
        bool cached;

        public PerspectivePlacement(Transform prop,BoxCollider[] parts,Vector3 grabPoint,float scale,float distance)
        {
            held=prop;grab=grabPoint;ratio=scale/distance;shapes=new Shape[parts.Length];corners=new Vector3[parts.Length*8];
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];Vector3 size=Vector3.Scale(part.size*.5f,part.transform.lossyScale)/scale;
                shapes[i]=new Shape {center=prop.InverseTransformPoint(part.transform.TransformPoint(part.center)),
                    half=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z)),
                    rotation=Quaternion.Inverse(prop.rotation)*part.transform.rotation};
                for(int k=0;k<8;k++)corners[i*8+k]=shapes[i].center+shapes[i].rotation*Vector3.Scale(shapes[i].half,
                    new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));
            }
        }

        public Result Resolve(Camera camera,Quaternion rotation,Collider player,int mask,float minimumScale,float maximumScale,
            Vector3 minimum,Vector3 maximum,float previousDepth,float maximumIntent=float.PositiveInfinity)
        {
            Queries=0;
            float minDepth=minimumScale/ratio,maxDepth=maximumScale/ratio,lo=minDepth,hi=maxDepth;
            Transform view=camera.transform;Quaternion cameraRotation=view.rotation;
            Vector3 eye=view.position,forward=cameraRotation*Vector3.forward;
            foreach(var point in corners)
            {
                Vector3 velocity=forward+rotation*((point-grab)*ratio);
                InteractionRaycast.ClipDepth(eye.x,velocity.x,minimum.x,maximum.x,ref lo,ref hi);
                InteractionRaycast.ClipDepth(eye.y,velocity.y,minimum.y,maximum.y,ref lo,ref hi);
                InteractionRaycast.ClipDepth(eye.z,velocity.z,minimum.z,maximum.z,ref lo,ref hi);
            }
            CacheSamples(camera,Quaternion.Inverse(cameraRotation)*rotation);
            bool surface=false;float limit=float.PositiveInfinity;
            foreach(var sample in samples)
            {
                bool found=InteractionRaycast.Nearest(eye,cameraRotation*sample.direction,player,held,mask,ref hits,out var hit,out int queries);
                Queries+=queries;
                if(found){surface=true;limit=Mathf.Min(limit,Mathf.Max(0,hit.distance-ContactSkin)/sample.backDistance);}
            }
            float target=Mathf.Min(surface?limit:previousDepth,maximumIntent);
            bool feasible=lo<=hi&&Mathf.Min(limit,maximumIntent)>=lo;
            target=feasible?Mathf.Clamp(target,lo,hi):Mathf.Clamp(target,minDepth,maxDepth);
            // Finite raster samples can straddle a thin obstacle's edge. Refine
            // the endpoint with the full collision volume before handing it on.
            if(feasible&&!ClearAt(eye,forward,rotation,target,player,mask)&&ClearAt(eye,forward,rotation,lo,player,mask))
            {
                float lower=lo,upper=target;
                for(int i=0;i<18;i++)
                {
                    float middle=(lower+upper)*.5f;
                    if(ClearAt(eye,forward,rotation,middle,player,mask))lower=middle;else upper=middle;
                }
                target=lower;
            }
            return new Result {Depth=target,RoomMinimum=lo,RoomMaximum=hi,Feasible=feasible,HasSurface=surface,Samples=samples.Count};
        }

        void CacheSamples(Camera camera,Quaternion rotation)
        {
            int width=Mathf.Max(1,camera.pixelWidth),height=Mathf.Max(1,camera.pixelHeight);
            if(cached&&Quaternion.Angle(cachedRotation,rotation)<.01f&&Mathf.Abs(cachedFov-camera.fieldOfView)<.001f&&
                Mathf.Abs(cachedAspect-camera.aspect)<.001f&&cachedWidth==width&&cachedHeight==height)return;
            cached=true;cachedRotation=rotation;cachedFov=camera.fieldOfView;cachedAspect=camera.aspect;cachedWidth=width;cachedHeight=height;
            samples.Clear();projected.Clear();
            float halfHeight=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f),halfWidth=halfHeight*camera.aspect;
            Vector2 lower=Vector2.one*float.PositiveInfinity,upper=Vector2.one*float.NegativeInfinity;
            foreach(var shape in shapes)
            {
                var projection=new Shape {center=Vector3.forward+rotation*((shape.center-grab)*ratio),
                    half=shape.half*ratio,rotation=rotation*shape.rotation};
                Quaternion inverse=Quaternion.Inverse(projection.rotation);
                projected.Add(new ProjectedShape {shape=projection,inverse=inverse,origin=inverse*(-projection.center)});
            }
            foreach(var point in corners)
            {
                Vector3 v=Vector3.forward+rotation*((point-grab)*ratio);
                if(v.z<=.00001f){lower=new Vector2(-halfWidth,-halfHeight);upper=-lower;continue;}
                Vector2 screen=new Vector2(v.x/v.z,v.y/v.z);lower=Vector2.Min(lower,screen);upper=Vector2.Max(upper,screen);
            }
            lower=Vector2.Max(lower,new Vector2(-halfWidth,-halfHeight));upper=Vector2.Min(upper,new Vector2(halfWidth,halfHeight));
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

        bool ClearAt(Vector3 eye,Vector3 forward,Quaternion rotation,float depth,Collider player,int mask)
        {
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
                    if(overlaps[i]!=player&&!overlaps[i].transform.IsChildOf(held))return false;
            }
            return true;
        }
    }
}
