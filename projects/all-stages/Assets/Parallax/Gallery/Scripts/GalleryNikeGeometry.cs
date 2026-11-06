using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Exact source-mesh queries are separate from the compound boxes used to
    // carry the exhibit. No triangle is simplified or replaced for light rays.
    public sealed class GalleryNikeGeometry : MonoBehaviour
    {
        public MeshFilter scanFilter;
        public Renderer scanRenderer;
        public MeshFilter ScanFilter => scanFilter;
        public Renderer ScanRenderer => scanRenderer;
        sealed class CachedMesh {public Acceleration tree;public int users;}
        static readonly Dictionary<Mesh,CachedMesh> cache=new Dictionary<Mesh,CachedMesh>();
        Mesh ownedMesh;
        CachedMesh owned;
        readonly int[] stack=new int[128];

        public void Prepare()
        {
            Mesh mesh=scanFilter==null?null:scanFilter.sharedMesh;
            if(owned!=null&&ReferenceEquals(ownedMesh,mesh))return;
            Release();if(mesh==null)return;
            if(!cache.TryGetValue(mesh,out var entry))
            {
                entry=new CachedMesh{tree=new Acceleration(mesh)};cache.Add(mesh,entry);
            }
            entry.users++;ownedMesh=mesh;owned=entry;
        }

        void Release()
        {
            if(owned==null)return;
            if(--owned.users==0)cache.Remove(ownedMesh);
            owned=null;ownedMesh=null;
        }
        void OnDestroy(){Release();}

        public bool Raycast(Ray ray,float maximum,out float distance)
        {
            distance=maximum;
            if(scanFilter==null||scanFilter.sharedMesh==null||!scanFilter.gameObject.activeInHierarchy||maximum<=0||float.IsNaN(maximum))return false;
            float length=ray.direction.magnitude;if(length<.000001f||float.IsNaN(length)||float.IsInfinity(length))return false;
            Prepare();var tree=owned.tree;if(tree.nodes.Count==0)return false;
            Matrix4x4 inverse=scanFilter.transform.worldToLocalMatrix;
            Vector3 origin=inverse.MultiplyPoint3x4(ray.origin);
            // Normalize in WORLD space only. Keeping this local vector's length
            // preserves world metres in slab and triangle t, including scale.
            Vector3 direction=inverse.MultiplyVector(ray.direction/length);
            bool hit=false;int pending=0;stack[pending++]=0;
            while(pending>0)
            {
                Node node=tree.nodes[stack[--pending]];
                if(!Intersects(node.bounds,origin,direction,distance))continue;
                if(node.count>0)
                {
                    for(int i=node.start;i<node.start+node.count;i++)
                    {
                        int tri=tree.order[i]*3;
                        if(Triangle(origin,direction,tree.vertices[tree.indices[tri]],tree.vertices[tree.indices[tri+1]],
                            tree.vertices[tree.indices[tri+2]],distance,out float at))
                        {distance=at;hit=true;}
                    }
                }
                else {stack[pending++]=node.left;stack[pending++]=node.right;}
            }
            return hit;
        }

        static bool Intersects(Bounds box,Vector3 origin,Vector3 direction,float maximum)
        {
            double enter=0,leave=maximum;Vector3 lo=box.min,hi=box.max;
            for(int axis=0;axis<3;axis++)
            {
                double d=direction[axis],o=origin[axis];
                if(Math.Abs(d)<1e-20){if(o<lo[axis]||o>hi[axis])return false;continue;}
                double a=(lo[axis]-o)/d,b=(hi[axis]-o)/d;if(a>b){double temp=a;a=b;b=temp;}
                enter=Math.Max(enter,a);leave=Math.Min(leave,b);if(leave<enter)return false;
            }
            return true;
        }

        static bool Triangle(Vector3 origin,Vector3 direction,Vector3 a,Vector3 b,Vector3 c,float maximum,out float distance)
        {
            distance=maximum;
            double ex=(double)b.x-a.x,ey=(double)b.y-a.y,ez=(double)b.z-a.z;
            double fx=(double)c.x-a.x,fy=(double)c.y-a.y,fz=(double)c.z-a.z;
            double px=direction.y*fz-direction.z*fy,py=direction.z*fx-direction.x*fz,pz=direction.x*fy-direction.y*fx;
            double determinant=ex*px+ey*py+ez*pz;if(Math.Abs(determinant)<1e-18)return false;
            double inverse=1/determinant,tx=(double)origin.x-a.x,ty=(double)origin.y-a.y,tz=(double)origin.z-a.z;
            double u=(tx*px+ty*py+tz*pz)*inverse;if(u< -1e-9||u>1+1e-9)return false;
            double qx=ty*ez-tz*ey,qy=tz*ex-tx*ez,qz=tx*ey-ty*ex;
            double v=(direction.x*qx+direction.y*qy+direction.z*qz)*inverse;if(v< -1e-9||u+v>1+1e-9)return false;
            double t=(fx*qx+fy*qy+fz*qz)*inverse;if(t<.00001||t>maximum)return false;
            distance=(float)t;return true;
        }

        struct Node {public Bounds bounds;public int left,right,start,count;}
        sealed class Acceleration
        {
            public readonly Vector3[] vertices;
            public readonly int[] indices,order;
            public readonly List<Node> nodes;
            readonly Bounds[] triangles;
            readonly Vector3[] centres;
            public Acceleration(Mesh mesh)
            {
                vertices=mesh.vertices;indices=mesh.triangles;int count=indices.Length/3;
                order=new int[count];triangles=new Bounds[count];centres=new Vector3[count];
                nodes=new List<Node>(Mathf.Max(1,count/4));
                for(int i=0;i<count;i++)
                {
                    order[i]=i;var box=new Bounds(vertices[indices[i*3]],Vector3.zero);
                    box.Encapsulate(vertices[indices[i*3+1]]);box.Encapsulate(vertices[indices[i*3+2]]);
                    triangles[i]=box;centres[i]=box.center;
                }
                if(count>0)Build(0,count,0);
                // Only the original vertices/indices, order and compact nodes
                // remain in the cached tree after the one-time construction.
                triangles=null;centres=null;
            }
            int Build(int start,int count,int depth)
            {
                Bounds box=triangles[order[start]],centroid=new Bounds(centres[order[start]],Vector3.zero);
                for(int i=start+1;i<start+count;i++){box.Encapsulate(triangles[order[i]]);centroid.Encapsulate(centres[order[i]]);}
                // Padding protects zero-thickness bounds and float transforms;
                // triangles themselves still decide every hit exactly.
                box.Expand(.000002f);
                int index=nodes.Count;nodes.Add(new Node{bounds=box,start=start,count=count});
                if(count<=12||depth>=48)return index;
                Vector3 size=centroid.size;int axis=size.y>size.x?1:0;if(size.z>size[axis])axis=2;
                float middle=centroid.center[axis];int left=start,right=start+count-1;
                while(left<=right)
                {
                    while(left<=right&&centres[order[left]][axis]<middle)left++;
                    while(left<=right&&centres[order[right]][axis]>=middle)right--;
                    if(left<right){int temp=order[left];order[left]=order[right];order[right]=temp;left++;right--;}
                }
                int split=left;if(split==start||split==start+count)split=start+count/2;
                int first=Build(start,split-start,depth+1),second=Build(split,start+count-split,depth+1);
                nodes[index]=new Node{bounds=box,left=first,right=second};return index;
            }
        }
    }
}
