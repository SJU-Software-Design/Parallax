using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // A textile surround for the new perspective gallery. The final doorway's
    // complete 3.6m-wide arched opening remains physically and visually clear.
    public static class GalleryConceptStage
    {
        static Material Material(string name,string category,Color color)
        {
            var material=new Material(Shader.Find("Parallax/MuseumSurface")){name=name,color=color};
            GalleryMuseumMaterials.Apply(material,category);GalleryWorld.Materials.Add(material);return material;
        }
        static Renderer MeshObject(string name,Transform parent,Mesh mesh,Material material,Vector3 at)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        static Mesh Finish(string name,List<Vector3> vertices,List<int> triangles,List<Vector2> uv)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void Quad(List<int> t,int a,int b,int c,int d)
        {t.AddRange(new[]{a,b,c,a,c,d});}
        static Mesh CutBackdrop()
        {
            const int rows=40;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int row=0;row<=rows;row++)
            {
                float y=.05f+4.8f*row/rows;
                float opening=y<=3.9f?1.84f:Mathf.Sqrt(Mathf.Max(0,1.8f*1.8f-(y-3.9f)*(y-3.9f)))+.04f;
                foreach(float x in new[]{-3.25f,-opening,opening,3.25f}){v.Add(new Vector3(x,y,0));uv.Add(new Vector2((x+3.25f)/6.5f,(y-.05f)/4.8f));}
            }
            for(int row=0;row<rows;row++)
            {
                int a=row*4;Quad(t,a,a+4,a+5,a+1);Quad(t,a+2,a+6,a+7,a+3);
            }
            return Finish("Teal backdrop with a true arched opening",v,t,uv);
        }
        static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
        {float s=1-t;return s*s*s*a+3*s*s*t*b+3*s*t*t*c+t*t*t*d;}
        static Mesh Drapery(int side)
        {
            const int rows=36,columns=32;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int row=0;row<=rows;row++)
            {
                float vertical=(float)row/rows;
                var center=Bezier(new Vector3(side*3.32f,.06f,-.02f),new Vector3(side*4.0f,1.7f,-.10f),new Vector3(side*3.25f,4.8f,-.04f),new Vector3(side*3.32f,6.45f,0),vertical);
                float waist=Mathf.Exp(-Mathf.Pow((vertical-.33f)*6,2)),width=1.38f-.49f*waist;
                for(int column=0;column<=columns;column++)
                {
                    float u=(float)column/columns;
                    float fold=(Mathf.Cos(u*Mathf.PI*12)+.22f*Mathf.Cos(u*Mathf.PI*24))*(.085f+.025f*vertical);
                    v.Add(center+new Vector3((u-.5f)*width,Mathf.Sin(u*Mathf.PI)*.035f*(1-vertical),fold));uv.Add(new Vector2(u,vertical));
                    if(row>0&&column<columns){int a=row*(columns+1)+column;Quad(t,a,a+1,a-columns,a-columns-1);}
                }
            }
            return Finish("Gathered velvet drape · "+side,v,t,uv);
        }
        static Mesh Valance()
        {
            const int columns=64,rows=4;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int row=0;row<=rows;row++)for(int column=0;column<=columns;column++)
            {
                float u=(float)column/columns,s=(float)row/rows;
                float depth=.34f+.06f*Mathf.Cos(u*Mathf.PI*6),z=.07f*Mathf.Cos(u*Mathf.PI*30);
                v.Add(new Vector3((u-.5f)*7.8f,6.55f-s*depth,z));uv.Add(new Vector2(u,s));
                if(row>0&&column<columns){int a=row*(columns+1)+column;Quad(t,a,a-columns-1,a-columns,a+1);}
            }
            return Finish("Folded velvet upper valance",v,t,uv);
        }
        static Mesh Ring()
        {
            const int around=24,tube=6;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int a=0;a<=around;a++)for(int b=0;b<=tube;b++)
            {
                float theta=a*Mathf.PI*2/around,phi=b*Mathf.PI*2/tube,r=.065f+Mathf.Cos(phi)*.010f;
                v.Add(new Vector3(Mathf.Sin(phi)*.010f,Mathf.Cos(theta)*r,Mathf.Sin(theta)*r));uv.Add(new Vector2((float)a/around,(float)b/tube));
                if(a>0&&b<tube){int p=a*(tube+1)+b;Quad(t,p,p+1,p-tube,p-tube-1);}
            }
            return Finish("Bronze curtain suspension ring",v,t,uv);
        }
        public static void Install(GalleryGame game)
        {
            if(game.transform.Find("Perspective gallery textile stage")!=null)return;
            var root=new GameObject("Perspective gallery textile stage").transform;root.SetParent(game.transform,false);
            var bronze=Material("Concept stage · aged bronze","Bronze",new Color(.52f,.34f,.12f));
            var velvet=Material("Concept stage · oxblood velvet","Paper",new Color(.19f,.035f,.025f));
            var teal=Material("Concept stage · deep teal linen","Paper",new Color(.045f,.11f,.115f));
            var fabric=Material("Concept stage · dark tan woven rug","Paper",new Color(.24f,.15f,.085f));
            foreach(var material in new[]{velvet,teal,fabric})if(material.HasProperty("_Roughness"))material.SetFloat("_Roughness",.96f);
            MeshObject("Teal stage wall textile",root,CutBackdrop(),teal,new Vector3(0,0,43.72f));
            for(int side=-1;side<=1;side+=2)
            {
                MeshObject("Decorative burgundy side curtain · "+(side<0?"left":"right"),root,Drapery(side),velvet,new Vector3(0,0,43.54f));
                GalleryConceptArchitecture.Block(root,"Curtain bronze tie",new Vector3(side*3.54f,2.13f,43.40f),new Vector3(.82f,.065f,.075f),bronze,.025f);
            }
            MeshObject("Velvet valance above the clear arch",root,Valance(),velvet,new Vector3(0,0,43.49f));
            GalleryConceptArchitecture.Block(root,"Bronze curtain rod",new Vector3(0,6.65f,43.55f),new Vector3(8.1f,.065f,.065f),bronze,.02f);
            var ring=Ring();for(int i=0;i<18;i++)MeshObject("Curtain bronze ring",root,ring,bronze,new Vector3(Mathf.Lerp(-3.65f,3.65f,i/17f),6.63f,43.55f));
            GalleryConceptArchitecture.Block(root,"Woven exhibition rug",new Vector3(0,.016f,38),new Vector3(4.8f,.016f,3.5f),fabric,.006f);
            for(int line=0;line<3;line++)
            {
                float inset=.08f+line*.065f,w=4.8f-inset*2,l=3.5f-inset*2,y=.033f+line*.001f;
                for(int side=-1;side<=1;side+=2)
                {
                    GalleryConceptArchitecture.Block(root,"Rug fine bronze long border",new Vector3(side*w/2,y,38),new Vector3(.012f,.012f,l),bronze,.003f);
                    GalleryConceptArchitecture.Block(root,"Rug fine bronze end border",new Vector3(0,y,38+side*l/2),new Vector3(w,.012f,.012f),bronze,.003f);
                }
            }
            var palette=new GalleryConceptArchitecture.Palette{assets=GalleryWorld.Materials,bronze=bronze,gold=GalleryWorld.Materials.Find(m=>m.name=="Concept museum · frame gold")};
            palette.paper=Material("Concept stage · catalogue paper","Paper",new Color(.96f,.94f,.88f));
            palette.ink=Material("Concept stage · dark frame linen","Ink",new Color(.07f,.06f,.05f));
            GalleryConceptArchitecture.FramedArtwork(root,"Ambassadors","대사들","한스 홀바인 · 1533",new Vector3(7.78f,2.8f,36),Quaternion.Euler(0,90,0),3.6f,3.05f,palette);
        }
    }
}
