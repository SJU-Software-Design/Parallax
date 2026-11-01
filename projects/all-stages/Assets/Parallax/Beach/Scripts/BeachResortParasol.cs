using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    // Fixed cantilever umbrellas: the visible fabric also supplies the real
    // collision and sun-occlusion meshes. There is no flat hidden shade slab.
    public static class BeachResortParasol
    {
        static readonly Vector2[] Rim = {
            new Vector2(-.64f,-1), new Vector2(.64f,-1),
            new Vector2(1,-.64f), new Vector2(1,.64f),
            new Vector2(.64f,1), new Vector2(-.64f,1),
            new Vector2(-1,.64f), new Vector2(-1,-.64f)
        };

        sealed class Shape
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int>[] triangles = { new List<int>(), new List<int>() };
            public int Vertex(Vector3 point)
            {
                int index = vertices.Count;
                vertices.Add(point); uv.Add(new Vector2(point.x,point.z));
                return index;
            }
            public void Triangle(int a,int b,int c,int material=0,bool reverse=false)
            { triangles[material].Add(a); triangles[material].Add(reverse?c:b); triangles[material].Add(reverse?b:c); }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material=0)
            {
                int start = Vertex(a); Vertex(b); Vertex(c); Vertex(d);
                Triangle(start,start+1,start+2,material); Triangle(start,start+2,start+3,material);
            }
            public Mesh Mesh(string name,int materialCount=1)
            {
                var mesh = new Mesh { name=name };
                mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.subMeshCount=materialCount;
                for(int i=0;i<materialCount;i++)mesh.SetTriangles(triangles[i],i);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
            public void Rod(Vector3 a,Vector3 b,float radius)
            {
                Vector3 axis=(b-a).normalized;
                Vector3 across=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized*radius;
                Vector3 other=Vector3.Cross(axis,across).normalized*radius;
                const int sides=8;
                for(int i=0;i<sides;i++)
                {
                    float t=i*Mathf.PI*2/sides,n=(i+1)*Mathf.PI*2/sides;
                    Vector3 p=across*Mathf.Cos(t)+other*Mathf.Sin(t),q=across*Mathf.Cos(n)+other*Mathf.Sin(n);
                    Quad(a+p,a+q,b+q,b+p);
                }
            }
        }

        static Material Palette(string name,Color color,float metallic=0,float gloss=.16f)
        {
            var existing=BeachWorld.Materials.Find(m=>m!=null&&m.name==name);
            if(existing!=null)return existing;
            var material=new Material(Shader.Find("Parallax/BeachSurface")){name=name,color=color};
            material.SetFloat("_Metallic",metallic);material.SetFloat("_Glossiness",gloss);
            BeachWorld.Materials.Add(material);return material;
        }
        static GameObject Part(string name,Transform parent,Mesh mesh,Material[] materials,bool solid=false)
        {
            var item=new GameObject(name);item.transform.SetParent(parent,false);
            item.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=item.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
            renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            if(solid)item.AddComponent<MeshCollider>().sharedMesh=mesh;
            return item;
        }
        static Vector3 Point(int side,float along,float radius,Vector2 size,float rise,float y=0)
        {
            Vector2 rim=Vector2.Lerp(Rim[side],Rim[(side+1)%Rim.Length],along);
            return new Vector3(rim.x*size.x*.5f*radius,
                y-rise*radius-.035f*Mathf.Sin(along*Mathf.PI)*radius-.025f*Mathf.Sin(radius*Mathf.PI),
                rim.y*size.y*.5f*radius);
        }
        static Mesh Fabric(string name,Vector2 size,float rise)
        {
            var shape=new Shape();float[] rings={.24f,.62f,1};
            // Separate lower faces give the cloth a lit underside and a small
            // physical thickness without needing a platform-specific shader.
            foreach(bool underside in new[]{false,true})
            for(int side=0;side<Rim.Length;side++)
            {
                float y=underside?-.018f:0;
                int center=shape.Vertex(new Vector3(0,y,0)),previous=-1;
                foreach(float radius in rings)
                {
                    int start=shape.vertices.Count;
                    for(int k=0;k<=4;k++)shape.Vertex(Point(side,k/4f,radius,size,rise,y));
                    for(int k=0;k<4;k++)
                    {
                        if(previous<0)shape.Triangle(center,start+k+1,start+k,side%2,underside);
                        else
                        {
                            shape.Triangle(previous+k,start+k+1,start+k,side%2,underside);
                            shape.Triangle(previous+k,previous+k+1,start+k+1,side%2,underside);
                        }
                    }
                    previous=start;
                }
            }
            return shape.Mesh(name,2);
        }

        public static void Build(string name,Transform parent,Vector3 peak,Vector2 size,float groundY,Vector3? mastFoot=null)
        {
            var host=new GameObject("Beach resort parasol · "+name);host.transform.SetParent(parent,false);host.transform.position=peak;
            var cloth=Palette("Resort sand canvas",new Color(.91f,.83f,.68f));
            var alternate=Palette("Resort sand canvas alternate",new Color(.87f,.78f,.62f));
            var hem=Palette("Resort canvas binding",new Color(.74f,.64f,.47f));
            var frame=Palette("Resort graphite frame",new Color(.14f,.19f,.20f),.55f,.34f);
            var ribs=Palette("Resort bronze ribs",new Color(.35f,.31f,.24f),.35f,.24f);
            var foot=Palette("Resort weighted foot",new Color(.27f,.31f,.31f),.12f,.18f);
            float rise=Mathf.Clamp(Mathf.Min(size.x,size.y)*.17f,.50f,.72f);
            Part(name+" canvas",host.transform,Fabric("Resort canopy · "+name,size,rise),new[]{cloth,alternate},true);

            // A short soft skirt, stitched perimeter and exposed underside ribs
            // make the silhouette read as cloth rather than a rectangular roof.
            var skirt=new Shape();var frameShape=new Shape();var piping=new Shape();
            for(int side=0;side<Rim.Length;side++)
            {
                for(int k=0;k<4;k++)
                {
                    Vector3 a=Point(side,k/4f,1,size,rise),b=Point(side,(k+1)/4f,1,size,rise);
                    float dropA=.09f+.025f*Mathf.Sin(k/4f*Mathf.PI),dropB=.09f+.025f*Mathf.Sin((k+1)/4f*Mathf.PI);
                    skirt.Quad(a,b,b-Vector3.up*dropB,a-Vector3.up*dropA);
                    skirt.Quad(a-Vector3.up*dropA,b-Vector3.up*dropB,b,a);
                    piping.Rod(a+Vector3.up*.006f,b+Vector3.up*.006f,.012f);
                }
                Vector3 previous=new Vector3(0,-.075f,0);
                foreach(float radius in new[]{.24f,.62f,1f})
                {
                    Vector3 next=Point(side,0,radius,size,rise,-.052f);
                    frameShape.Rod(previous,next,.026f);previous=next;
                }
            }
            Part(name+" fabric valance",host.transform,skirt.Mesh("Resort valance · "+name),new[]{hem});
            Part(name+" perimeter stitching",host.transform,piping.Mesh("Resort piping · "+name),new[]{cloth});
            Part(name+" underside ribs",host.transform,frameShape.Mesh("Resort ribs · "+name),new[]{ribs});
            // Raised vent cap, with a narrow dark reveal underneath.
            var vent=Part(name+" raised wind vent",host.transform,Fabric("Resort vent · "+name,size*.20f,.11f),new[]{cloth,alternate});
            vent.transform.localPosition=Vector3.up*.13f;

            Vector3 pole=mastFoot.HasValue?mastFoot.Value-peak:new Vector3(-size.x*.5f-.68f,groundY-peak.y,0);
            float poleX=pole.x,poleZ=pole.z,bottom=pole.y,sideSign=Mathf.Sign(poleX);
            // The outer mast leaves the resting floor and plank landings open.
            BeachWorld.Box(name+" weighted base",new Vector3(poleX,bottom+.065f,poleZ),new Vector3(.88f,.13f,.78f),foot,host.transform);
            foreach(float offset in new[]{-.21f,.21f})
                BeachWorld.Box(name+" base seam",new Vector3(poleX+offset,bottom+.133f,poleZ),new Vector3(.012f,.006f,.76f),frame,host.transform,false,false);
            BeachWorld.Box(name+" offset mast",new Vector3(poleX,(bottom+.10f)*.5f,poleZ),new Vector3(.13f,.10f-bottom,.15f),frame,host.transform);
            // Slim upper cantilever and diagonal brace follow the reference's
            // side-supported silhouette. Their visible rods have exact colliders.
            Rod(name+" overhead arm",host.transform,new Vector3(poleX,.10f,poleZ),new Vector3(0,.07f,0),.055f,frame);
            Rod(name+" diagonal brace",host.transform,new Vector3(poleX,-.80f,poleZ),new Vector3(sideSign*size.x*.25f,.085f,poleZ*.5f),.043f,frame);
            BeachWorld.Box(name+" adjustment collar",new Vector3(poleX,-.78f,poleZ),new Vector3(.21f,.20f,.22f),ribs,host.transform);
            BeachWorld.Box(name+" winding handle",new Vector3(poleX+sideSign*.15f,-.82f,poleZ-.06f),new Vector3(.22f,.045f,.045f),frame,host.transform,false);
        }

        static void Rod(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material material)
        {
            var shape=new Shape();shape.Rod(a,b,radius);
            Part(name,parent,shape.Mesh("Resort support · "+name),new[]{material},true);
        }
    }
}
