using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // The right exterior wall is built around real apertures. Local X runs
    // along the wall, and local +Z points outdoors (+world X).
    public static class GalleryConceptWindows
    {
        const float Height=6.6f, Length=56, Thickness=.32f;
        const float Radius=1.2f, Bottom=1.35f, Spring=4.05f;
        const int ArcSteps=96;

        sealed class Surface
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<int> triangles=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                uv.Add(new Vector2(a.x,a.y));uv.Add(new Vector2(b.x,b.y));uv.Add(new Vector2(c.x,c.y));
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                foreach(var p in new[]{a,b,c,d})uv.Add(new Vector2(p.x,p.y));
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);
                mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
        static Transform Group(Transform parent,string name,Vector3 at)
        {
            var result=new GameObject(name).transform;result.SetParent(parent,false);result.localPosition=at;return result;
        }
        static MeshRenderer Geometry(Transform parent,string name,Mesh mesh,Material material,bool solid=false,bool casts=true)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;renderer.shadowCastingMode=casts?ShadowCastingMode.On:ShadowCastingMode.Off;
            renderer.receiveShadows=casts;if(solid)go.AddComponent<MeshCollider>().sharedMesh=mesh;return renderer;
        }
        static Renderer Box(Transform parent,string name,Vector3 at,Vector3 size,Material material,bool solid=false,float bevel=.012f)
        {
            var renderer=GalleryConceptArchitecture.Block(parent,name,at,size,material,bevel);
            renderer.gameObject.isStatic=true;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            if(solid)renderer.gameObject.AddComponent<BoxCollider>().size=size;return renderer;
        }
        static Mesh Header()
        {
            var s=new Surface();float half=Thickness/2;
            for(int i=0;i<ArcSteps;i++)
            {
                float a=Mathf.PI-Mathf.PI*i/ArcSteps,b=Mathf.PI-Mathf.PI*(i+1)/ArcSteps;
                float x=Mathf.Cos(a)*Radius,y=Spring+Mathf.Sin(a)*Radius;
                float nextX=Mathf.Cos(b)*Radius,nextY=Spring+Mathf.Sin(b)*Radius;
                s.Quad(new Vector3(x,y,half),new Vector3(nextX,nextY,half),new Vector3(nextX,Height,half),new Vector3(x,Height,half));
                s.Quad(new Vector3(x,y,-half),new Vector3(x,Height,-half),new Vector3(nextX,Height,-half),new Vector3(nextX,nextY,-half));
                s.Quad(new Vector3(x,Height,-half),new Vector3(x,Height,half),new Vector3(nextX,Height,half),new Vector3(nextX,Height,-half));
                s.Quad(new Vector3(x,y,-half),new Vector3(nextX,nextY,-half),new Vector3(nextX,nextY,half),new Vector3(x,y,half));
            }
            s.Quad(new Vector3(-Radius,Spring,-half),new Vector3(-Radius,Spring,half),new Vector3(-Radius,Height,half),new Vector3(-Radius,Height,-half));
            s.Quad(new Vector3(Radius,Spring,-half),new Vector3(Radius,Height,-half),new Vector3(Radius,Height,half),new Vector3(Radius,Spring,half));
            return s.Mesh("Closed arched window header, including curved soffit and end caps");
        }
        static List<int> Triangulate(Vector2[] points)
        {
            var remaining=new List<int>();var result=new List<int>();float area=0;
            for(int i=0;i<points.Length;i++){remaining.Add(i);Vector2 a=points[i],b=points[(i+1)%points.Length];area+=a.x*b.y-b.x*a.y;}
            if(area<0)remaining.Reverse();
            int budget=points.Length*points.Length;
            while(remaining.Count>2&&budget-->0)
            {
                bool clipped=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int ia=remaining[(i+remaining.Count-1)%remaining.Count],ib=remaining[i],ic=remaining[(i+1)%remaining.Count];
                    Vector2 a=points[ia],b=points[ib],c=points[ic];
                    if(Cross(b-a,c-b)<=.000001f)continue;
                    bool inside=false;
                    foreach(int j in remaining)
                    {
                        if(j==ia||j==ib||j==ic)continue;Vector2 p=points[j];
                        if(Cross(b-a,p-a)>=-.000001f&&Cross(c-b,p-b)>=-.000001f&&Cross(a-c,p-c)>=-.000001f){inside=true;break;}
                    }
                    if(inside)continue;result.AddRange(new[]{ia,ib,ic});remaining.RemoveAt(i);clipped=true;break;
                }
                if(!clipped)break;
            }
            return result;
        }
        static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
        static Mesh Moulding(float radius,float bottom,Vector2[] profile,string name)
        {
            var path=new List<Vector2>{new Vector2(-radius,bottom)};
            var outward=new List<Vector2>{Vector2.left};
            for(int i=0;i<=ArcSteps;i++)
            {
                float angle=Mathf.PI-Mathf.PI*i/ArcSteps;var n=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                path.Add(new Vector2(n.x*radius,Spring+n.y*radius));outward.Add(n);
            }
            path.Add(new Vector2(radius,bottom));outward.Add(Vector2.right);
            var s=new Surface();int count=profile.Length;
            for(int i=0;i<path.Count-1;i++)for(int j=0;j<count;j++)
            {
                int next=(j+1)%count;
                Vector2 a=path[i]+outward[i]*profile[j].x,b=path[i+1]+outward[i+1]*profile[j].x;
                Vector2 c=path[i+1]+outward[i+1]*profile[next].x,d=path[i]+outward[i]*profile[next].x;
                s.Quad(new Vector3(a.x,a.y,profile[j].y),new Vector3(d.x,d.y,profile[next].y),
                    new Vector3(c.x,c.y,profile[next].y),new Vector3(b.x,b.y,profile[j].y));
            }
            // Ear clipping closes the concave moulding section at both sill ends.
            var caps=Triangulate(profile);
            foreach(int end in new[]{0,path.Count-1})for(int i=0;i<caps.Count;i+=3)
            {
                var points=new Vector3[3];
                for(int j=0;j<3;j++)
                {
                    Vector2 q=profile[caps[i+j]],p=path[end]+outward[end]*q.x;points[j]=new Vector3(p.x,p.y,q.y);
                }
                if(end==0)s.Triangle(points[0],points[2],points[1]);else s.Triangle(points[0],points[1],points[2]);
            }
            return s.Mesh(name);
        }
        static Mesh Prism(Vector2[] polygon,float near,float far,string name)
        {
            var s=new Surface();var faces=Triangulate(polygon);
            for(int i=0;i<faces.Count;i+=3)
            {
                Vector2 a=polygon[faces[i]],b=polygon[faces[i+1]],c=polygon[faces[i+2]];
                s.Triangle(new Vector3(a.x,a.y,near),new Vector3(c.x,c.y,near),new Vector3(b.x,b.y,near));
                s.Triangle(new Vector3(a.x,a.y,far),new Vector3(b.x,b.y,far),new Vector3(c.x,c.y,far));
            }
            float signed=0;for(int i=0;i<polygon.Length;i++)signed+=Cross(polygon[i],polygon[(i+1)%polygon.Length]);
            for(int i=0;i<polygon.Length;i++)
            {
                Vector2 a=polygon[i],b=polygon[(i+1)%polygon.Length];
                if(signed<0){Vector2 swap=a;a=b;b=swap;}
                s.Quad(new Vector3(a.x,a.y,near),new Vector3(b.x,b.y,near),new Vector3(b.x,b.y,far),new Vector3(a.x,a.y,far));
            }
            return s.Mesh(name);
        }
        static Material Glass(GalleryConceptArchitecture.Palette p)
        {
            var glass=new Material(Shader.Find("Standard")){name="Concept windows · pale blue architectural glass",color=new Color(.68f,.82f,.90f,.12f)};
            glass.SetFloat("_Mode",3);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite",0);glass.SetFloat("_Glossiness",.83f);glass.SetFloat("_Metallic",0);
            glass.DisableKeyword("_ALPHATEST_ON");glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");glass.EnableKeyword("_ALPHABLEND_ON");
            glass.SetOverrideTag("RenderType","Transparent");glass.renderQueue=(int)RenderQueue.Transparent;
            (p.assets??GalleryWorld.Materials).Add(glass);return glass;
        }
        static void Window(Transform parent,float center,GalleryConceptArchitecture.Palette p,Material glass)
        {
            var window=Group(parent,"Classical arched exterior window",new Vector3(center,0,0));
            Box(window,"Solid wall below window sill",new Vector3(0,Bottom/2,0),new Vector3(Radius*2,Bottom,Thickness),p.wall,true);
            Geometry(window,"True curved window header",Header(),p.wall,true);
            Vector2[] stoneProfile={new Vector2(0,.16f),new Vector2(0,-.17f),new Vector2(.025f,-.195f),new Vector2(.065f,-.235f),
                new Vector2(.105f,-.235f),new Vector2(.13f,-.19f),new Vector2(.18f,-.20f),new Vector2(.235f,-.28f),
                new Vector2(.29f,-.28f),new Vector2(.35f,-.21f),new Vector2(.35f,.16f)};
            Geometry(window,"Continuous carved limestone architrave",Moulding(Radius,Bottom,stoneProfile,"Swept cyma and ovolo window surround"),p.stone);
            Box(window,"Projecting rounded limestone sill",new Vector3(0,1.30f,-.10f),new Vector3(3.18f,.10f,.68f),p.stone,false,.025f);
            Geometry(window,"Central dressed stone keystone",Prism(new[]{new Vector2(-.115f,5.27f),new Vector2(.115f,5.27f),
                new Vector2(.185f,5.62f),new Vector2(-.185f,5.62f)},-.305f,-.19f,"Tapered arch keystone"),p.plaster);

            const float inner=1.07f,glassBottom=1.48f;
            Vector2[] bronzeProfile={new Vector2(0,.025f),new Vector2(.13f,.025f),new Vector2(.13f,.10f),
                new Vector2(.11f,.125f),new Vector2(.02f,.125f),new Vector2(0,.10f)};
            Geometry(window,"Bevelled bronze arched glazing frame",Moulding(inner,Bottom,bronzeProfile,"Continuous bronze window frame"),p.bronze);
            Box(window,"Bronze bottom glazing rail",new Vector3(0,Bottom+.065f,.075f),new Vector3(2.4f,.13f,.10f),p.bronze);
            // Two 12cm mullions leave three clear panes of approximately 63cm.
            foreach(float x in new[]{-.38f,.38f})
            {
                float top=Spring+Mathf.Sqrt(inner*inner-x*x);
                Box(window,"Bronze vertical glazing mullion",new Vector3(x,(glassBottom+top)/2,.075f),new Vector3(.12f,top-glassBottom,.10f),p.bronze,false,.009f);
            }
            foreach(float y in new[]{2.15f,2.91f,3.67f,4.43f})
            {
                float half=y>Spring?Mathf.Sqrt(inner*inner-(y-Spring)*(y-Spring)):inner;
                Box(window,"Bronze horizontal glazing transom",new Vector3(0,y,.075f),new Vector3(half*2,.12f,.10f),p.bronze,false,.009f);
            }
            var outline=new List<Vector2>{new Vector2(-inner,glassBottom),new Vector2(inner,glassBottom),new Vector2(inner,Spring)};
            for(int i=1;i<=ArcSteps;i++)
            {
                float angle=Mathf.PI*i/ArcSteps;outline.Add(new Vector2(Mathf.Cos(angle)*inner,Spring+Mathf.Sin(angle)*inner));
            }
            Geometry(window,"Transparent blue glass · sunlight passes through",Prism(outline.ToArray(),.071f,.079f,"Closed arched glazing pane"),glass,false,false);
            var contact=Group(window,"Window glass physical contact",new Vector3(0,(Bottom+Spring+Radius)/2,.075f));
            contact.gameObject.AddComponent<BoxCollider>().size=new Vector3(2*Radius,Spring+Radius-Bottom,.10f);
        }
        public static void BuildRightWall(Transform root,GalleryConceptArchitecture.Palette p)
        {
            if(root.Find("Concept museum right window wall")!=null)return;
            var wall=Group(root,"Concept museum right window wall",new Vector3(8.16f,0,28));wall.localRotation=Quaternion.Euler(0,90,0);
            var glass=Glass(p);float cursor=-Length/2;
            // Local horizontal coordinates correspond to world z50.5 and z21.5.
            foreach(float center in new[]{-22.5f,6.5f})
            {
                float edge=center-Radius;
                if(edge>cursor)Box(wall,"Solid exterior wall between windows",new Vector3((cursor+edge)/2,Height/2,0),new Vector3(edge-cursor,Height,Thickness),p.wall,true);
                Window(wall,center,p,glass);cursor=center+Radius;
            }
            if(cursor<Length/2)Box(wall,"Solid exterior wall after window",new Vector3((cursor+Length/2)/2,Height/2,0),new Vector3(Length/2-cursor,Height,Thickness),p.wall,true);
        }
    }
}
