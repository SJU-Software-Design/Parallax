using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Construction details for the new museum. The caller owns the physical
    // walls, roof apertures and sun/shade rules; these parts sit on those walls.
    public static class GalleryConceptArchitecture
    {
        public sealed class Palette
        {
            public Material plaster, stone, bronze, gold, wood, paper, ink;
            public IList<Material> assets;
        }

        static Transform Group(string name,Transform parent,Vector3 at,Quaternion turn)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);
            t.localPosition=at;t.localRotation=turn;return t;
        }
        static Renderer Geometry(string name,Transform parent,Mesh mesh,Material material,Vector3 at)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();
            r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        static void Tri(List<int> t,int a,int b,int c){t.Add(a);t.Add(b);t.Add(c);}
        static Mesh Finish(string name,List<Vector3> v,List<int> t,List<Vector2> uv=null)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);
            if(uv!=null)mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh BeveledBox(Vector3 size,float radius)
        {
            const int steps=4;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            Vector3 half=size*.5f;radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.8f);
            Vector3 core=half-Vector3.one*radius;
            for(int axis=0;axis<3;axis++)for(int side=-1;side<=1;side+=2)
            {
                int start=v.Count,u=(axis+1)%3,w=(axis+2)%3;
                for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
                {
                    Vector3 p=Vector3.zero;p[axis]=half[axis]*side;
                    p[u]=Mathf.Lerp(-half[u],half[u],(float)x/steps);p[w]=Mathf.Lerp(-half[w],half[w],(float)y/steps);
                    Vector3 q=new Vector3(Mathf.Clamp(p.x,-core.x,core.x),Mathf.Clamp(p.y,-core.y,core.y),Mathf.Clamp(p.z,-core.z,core.z));
                    v.Add(q+(p-q).normalized*radius);uv.Add(new Vector2((float)x/steps,(float)y/steps));
                }
                for(int y=0;y<steps;y++)for(int x=0;x<steps;x++)
                {
                    int a=start+y*(steps+1)+x,b=a+1,c=a+steps+1,d=c+1;
                    if(side>0){Tri(t,a,b,d);Tri(t,a,d,c);}else{Tri(t,a,d,b);Tri(t,a,c,d);}
                }
            }
            return Finish("Chamfered architectural block",v,t,uv);
        }
        public static Renderer Block(Transform parent,string name,Vector3 at,Vector3 size,Material material,float bevel=.025f)
        {return Geometry(name,parent,BeveledBox(size,bevel),material,at);}

        static Mesh FrameMesh(Vector2 canvas,Vector2[] profile)
        {
            var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            foreach(var p in profile)
            {
                float x=canvas.x*.5f+p.x,y=canvas.y*.5f+p.x;
                foreach(var corner in new[]{new Vector2(-x,-y),new Vector2(x,-y),new Vector2(x,y),new Vector2(-x,y)})
                {v.Add(new Vector3(corner.x,corner.y,p.y));uv.Add(corner*.25f);}
            }
            for(int band=0;band<profile.Length-1;band++)for(int edge=0;edge<4;edge++)
            {
                int a=band*4+edge,b=band*4+(edge+1)%4,c=a+4,d=b+4;
                Tri(t,a,d,b);Tri(t,a,c,d);
            }
            return Finish("Mitered layered picture frame",v,t,uv);
        }
        public static void PictureFrame(Transform parent,Vector2 canvas,Palette p,float scale=1)
        {
            Vector2[] shape={new Vector2(0,-.035f),new Vector2(.018f,-.070f),new Vector2(.043f,-.080f),
                new Vector2(.065f,-.045f),new Vector2(.090f,-.040f),new Vector2(.120f,-.100f),
                new Vector2(.155f,-.115f),new Vector2(.190f,-.080f),new Vector2(.205f,-.035f),new Vector2(.205f,.025f)};
            for(int i=0;i<shape.Length;i++)shape[i]*=scale;
            Geometry("Layered gilded frame",parent,FrameMesh(canvas,shape),p.gold!=null?p.gold:p.bronze,Vector3.zero);
            Vector2[] inset={new Vector2(-.025f,-.012f),new Vector2(0,-.035f)};
            Geometry("Dark linen rabbet",parent,FrameMesh(canvas,inset),p.ink,Vector3.zero);
        }
        static void Label(Transform parent,string words,Vector3 at,float size,Color color)
        {
            var go=new GameObject("Wall label · "+words.Replace('\n',' '));go.transform.SetParent(parent,false);go.transform.localPosition=at;
            var label=go.AddComponent<GalleryLabel>();label.caption=words;label.size=size;label.color=color;
        }
        public static Transform FramedArtwork(Transform parent,string key,string title,string credit,Vector3 at,Quaternion turn,
            float maximumWidth,float maximumHeight,Palette p)
        {
            Texture2D texture=Resources.Load<Texture2D>("MuseumArt/"+key);
            if(texture==null)throw new InvalidOperationException("Concept museum painting missing: "+key);
            float ratio=(float)texture.width/texture.height,h=Mathf.Min(maximumHeight,maximumWidth/ratio),w=h*ratio;
            var root=Group("Collection · "+title,parent,at,turn);
            Block(root,"Canvas stretcher",Vector3.zero,new Vector3(w+.035f,h+.035f,.075f),p.paper,.012f);
            var material=new Material(Shader.Find("Parallax/MuseumSurface")){name="Concept artwork · "+key,color=Color.white};
            GalleryMuseumMaterials.Apply(material,"Artwork");material.mainTexture=texture;
            p.assets?.Add(material);
            var vertices=new List<Vector3>{new Vector3(-w/2,-h/2,-.043f),new Vector3(w/2,-h/2,-.043f),new Vector3(w/2,h/2,-.043f),new Vector3(-w/2,h/2,-.043f)};
            Geometry("Original artwork reproduction",root,Finish("Canvas · "+key,vertices,new List<int>{0,2,1,0,3,2},new List<Vector2>{Vector2.zero,Vector2.right,Vector2.one,Vector2.up}),material,Vector3.zero);
            PictureFrame(root,new Vector2(w+.035f,h+.035f),p);
            float plateWidth=Mathf.Min(2.0f,Mathf.Max(1.3f,w*.65f));
            Block(root,"Museum catalogue plaque",new Vector3(w/2-plateWidth/2,-h/2-.43f,.015f),new Vector3(plateWidth,.29f,.035f),p.paper,.008f);
            Label(root,title,new Vector3(w/2-plateWidth/2,-h/2-.38f,-.012f),.065f,new Color(.15f,.13f,.10f));
            Label(root,credit,new Vector3(w/2-plateWidth/2,-h/2-.48f,-.012f),.045f,new Color(.25f,.23f,.19f));
            return root;
        }

        public static Transform ClassicalColumn(Transform parent,Vector3 baseAt,float height,float radius,Palette p)
        {
            var root=Group("Fluted limestone column",parent,baseAt,Quaternion.identity);
            Block(root,"Square column plinth",new Vector3(0,.09f,0),new Vector3(radius*3,.18f,radius*3),p.stone,.025f);
            float[] levels={.18f,.22f,.27f,.32f,.37f,.42f,.47f,height-.53f,height-.45f,height-.35f,height-.27f,height-.20f};
            float[] widths={1.40f,1.40f,1.13f,1.24f,1.15f,1.05f,1, .88f,.94f,1.13f,1.32f,1.32f};
            const int sides=64;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int j=0;j<levels.Length;j++)for(int i=0;i<=sides;i++)
            {
                float a=i*Mathf.PI*2/sides,flute=j>=6&&j<=7?1-.055f*(.5f+.5f*Mathf.Cos(a*16)):1;
                v.Add(new Vector3(Mathf.Cos(a)*radius*widths[j]*flute,levels[j],Mathf.Sin(a)*radius*widths[j]*flute));uv.Add(new Vector2((float)i/sides,levels[j]));
                if(j>0&&i<sides){int b=j*(sides+1)+i;Tri(t,b,b+1,b-sides);Tri(t,b,b-sides,b-sides-1);}
            }
            Geometry("Carved shaft and torus capital",root,Finish("Fluted column profile",v,t,uv),p.plaster,Vector3.zero);
            Block(root,"Cushioned abacus",new Vector3(0,height-.10f,0),new Vector3(radius*3.1f,.20f,radius*3.1f),p.stone,.035f);return root;
        }
        public static void ArchPortal(Transform parent,Vector3 baseAt,Quaternion turn,float openingWidth,float springHeight,float depth,Palette p)
        {
            var root=Group("Classical stone portal",parent,baseAt,turn);float radius=openingWidth*.5f,thickness=.38f;
            for(int side=-1;side<=1;side+=2)
            {
                Block(root,"Portal limestone pier",new Vector3(side*(radius+thickness/2),springHeight/2,0),new Vector3(thickness,springHeight,depth),p.plaster,.025f);
                Block(root,"Portal stepped foot",new Vector3(side*(radius+thickness/2),.14f,0),new Vector3(thickness+.13f,.28f,depth+.17f),p.stone,.028f);
                Block(root,"Portal spring cornice",new Vector3(side*(radius+thickness/2),springHeight-.025f,0),new Vector3(thickness+.18f,.16f,depth+.15f),p.stone,.025f);
            }
            const int segments=48;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            Vector2[] profile={new Vector2(radius,depth/2),new Vector2(radius,-depth/2+.035f),new Vector2(radius+.035f,-depth/2-.015f),
                new Vector2(radius+.10f,-depth/2-.055f),new Vector2(radius+thickness-.035f,-depth/2-.055f),
                new Vector2(radius+thickness,-depth/2),new Vector2(radius+thickness,depth/2)};
            for(int i=0;i<=segments;i++)
            {
                float a=Mathf.PI*i/segments;
                foreach(var point in profile){v.Add(new Vector3(Mathf.Cos(a)*point.x,springHeight+Mathf.Sin(a)*point.x,point.y));uv.Add(new Vector2(a,point.x));}
            }
            int stride=profile.Length;
            for(int i=0;i<segments;i++)for(int j=0;j<stride-1;j++)
            {int a=i*stride+j,b=a+stride;Tri(t,a,b,b+1);Tri(t,a,b+1,a+1);}
            Geometry("Moulded arch crown",root,Finish("Portal arched moulding",v,t,uv),p.stone,Vector3.zero);
            // A restrained keystone gives the arch a built, load-bearing appearance.
            Block(root,"Projecting keystone",new Vector3(0,springHeight+radius+.16f,-depth*.12f),new Vector3(.28f,.45f,depth+.18f),p.plaster,.025f);
        }

        public static void WallFinish(Transform parent,Vector3 baseAt,Quaternion turn,float width,float height,Palette p)
        {
            var root=Group("Layered museum wall finish",parent,baseAt,turn);
            Block(root,"Limestone skirting",new Vector3(0,.16f,0),new Vector3(width,.32f,.12f),p.stone,.012f);
            Block(root,"Skirting crown",new Vector3(0,.35f,-.035f),new Vector3(width,.07f,.15f),p.plaster,.015f);
            Block(root,"Wainscot chair rail",new Vector3(0,1.15f,-.015f),new Vector3(width,.105f,.12f),p.plaster,.02f);
            for(int i=0;i<4;i++)Block(root,"Cornice profile "+i,new Vector3(0,height-.30f+i*.085f,-i*.028f),new Vector3(width,.105f,.10f+i*.045f),p.plaster,.014f);
            int panels=Mathf.Max(1,Mathf.RoundToInt(width/2.6f));float pitch=width/panels;
            for(int i=0;i<panels;i++)
            {
                var panel=Group("Recessed wall panel",root,new Vector3(-width/2+pitch*(i+.5f),.72f,-.035f),Quaternion.identity);
                Geometry("Wainscot fine moulding",panel,FrameMesh(new Vector2(Mathf.Max(.25f,pitch-.32f),.48f),new[]{new Vector2(0,-.012f),new Vector2(.035f,-.027f),new Vector2(.065f,0)}),p.plaster,Vector3.zero);
            }
        }
        public static void Bench(Transform parent,Vector3 at,Quaternion turn,float length,Palette p)
        {
            var root=Group("Museum walnut bench",parent,at,turn);
            for(int i=0;i<3;i++)Block(root,"Rounded walnut seat board",new Vector3(0,.52f,(i-1)*.22f),new Vector3(length,.12f,.205f),p.wood,.025f);
            for(int side=-1;side<=1;side+=2)
            {
                Block(root,"Bench bronze foot",new Vector3(side*(length/2-.30f),.06f,0),new Vector3(.30f,.12f,.72f),p.bronze,.025f);
                Block(root,"Bench curved support",new Vector3(side*(length/2-.30f),.29f,0),new Vector3(.13f,.40f,.45f),p.bronze,.035f);
            }
        }
        static void Rod(Transform root,string name,Vector3 a,Vector3 b,float width,Material material)
        {
            var go=Geometry(name,root,BeveledBox(new Vector3(width,(b-a).magnitude,width),width*.2f),material,(a+b)/2).transform;
            go.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        public static void Skylight(Transform parent,Vector3 at,Quaternion turn,Vector2 opening,float rise,Palette p)
        {
            var root=Group("Pitched museum skylight lantern",parent,at,turn);float w=opening.x/2,l=opening.y/2;
            for(int side=-1;side<=1;side+=2)
            {
                Block(root,"Skylight stone curb",new Vector3(side*(w+.13f),.07f,0),new Vector3(.26f,.30f,opening.y+.52f),p.plaster,.018f);
                Rod(root,"Skylight eave",new Vector3(side*w,.12f,-l),new Vector3(side*w,.12f,l),.075f,p.bronze);
            }
            Rod(root,"Skylight ridge",new Vector3(0,rise,-l),new Vector3(0,rise,l),.085f,p.bronze);
            int rafters=Mathf.Max(2,Mathf.CeilToInt(opening.y/1.35f));
            for(int i=0;i<=rafters;i++)for(int side=-1;side<=1;side+=2)
            {float z=Mathf.Lerp(-l,l,(float)i/rafters);Rod(root,"Skylight sloped glazing bar",new Vector3(side*w,.12f,z),new Vector3(0,rise,z),.045f,p.bronze);}
            for(int side=-1;side<=1;side+=2)
                Rod(root,"Skylight mid-height rail",new Vector3(side*w*.5f,(rise+.12f)/2,-l),new Vector3(side*w*.5f,(rise+.12f)/2,l),.035f,p.bronze);
        }

        static Material Material(Palette p,string category,string name,Color color)
        {
            var material=new Material(Shader.Find("Parallax/MuseumSurface")){name=name,color=color};
            GalleryMuseumMaterials.Apply(material,category);p.assets?.Add(material);return material;
        }
        public static Palette MuseumPalette(IList<Material> registry)
        {
            var p=new Palette{assets=registry};
            p.plaster=Material(p,"Plaster","Concept museum · ivory plaster",new Color(.94f,.91f,.84f));
            p.stone=Material(p,"Stone","Concept museum · warm limestone",new Color(.86f,.83f,.75f));
            p.bronze=Material(p,"Bronze","Concept museum · patinated bronze",new Color(.56f,.36f,.14f));
            p.gold=Material(p,"Bronze","Concept museum · frame gold",new Color(1,.85f,.57f));
            p.gold.SetFloat("_Metallic",.98f);p.gold.SetFloat("_Roughness",.28f);p.gold.SetFloat("_ReflectionStrength",1);p.gold.SetFloat("_AmbientStrength",1);
            p.wood=Material(p,"Wood","Concept museum · walnut",new Color(.38f,.23f,.12f));
            p.wood.color=new Color(.42f,.28f,.17f);
            p.bronze.color=new Color(.72f,.48f,.18f);p.bronze.SetFloat("_AmbientStrength",1.25f);
            p.paper=Material(p,"Paper","Concept museum · archival board",new Color(.96f,.94f,.87f));
            p.ink=Material(p,"Ink","Concept museum · charcoal linen",new Color(.065f,.060f,.050f));return p;
        }
        static void Track(Transform parent,Vector3 at,float length,Palette p)
        {
            Block(parent,"Suspended gallery lighting rail",at,new Vector3(length,.045f,.065f),p.ink,.008f);
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 anchor=at+Vector3.right*side*(length*.37f);
                Rod(parent,"Lighting rail ceiling suspension",anchor,anchor+Vector3.up*.45f,.018f,p.ink);
            }
            int count=Mathf.Max(2,Mathf.RoundToInt(length/2.5f));
            for(int i=0;i<count;i++)
            {
                float x=at.x-length/2+(i+.5f)*length/count;
                var fixture=Group("Adjustable museum spotlight",parent,new Vector3(x,at.y-.14f,at.z),Quaternion.Euler(25,0,0));
                Block(fixture,"Spotlight yoke",Vector3.up*.075f,new Vector3(.14f,.10f,.09f),p.bronze,.015f);
                Block(fixture,"Beveled spotlight housing",Vector3.zero,new Vector3(.16f,.16f,.24f),p.ink,.035f);
                Block(fixture,"Warm lamp diffuser",new Vector3(0,0,-.125f),new Vector3(.115f,.115f,.018f),p.paper,.025f);
            }
        }

        // Four new exhibition spaces: room boundaries and their physical openings
        // come from GalleryConceptWorld. All placements here use the same plan.
        public static void Install(GalleryGame game,Palette palette=null)
        {
            if(game.transform.Find("Concept museum architectural collection")!=null)return;
            var root=Group("Concept museum architectural collection",game.transform,Vector3.zero,Quaternion.identity);
            Palette p=palette??MuseumPalette(GalleryWorld.Materials);
            for(int side=-1;side<=1;side+=2)
            {
                WallFinish(root,new Vector3(side*7.88f,0,28),Quaternion.Euler(0,side*90,0),55.6f,GalleryConceptWorld.Roof,p);
                foreach(float z in new[]{.62f,13.34f,14.65f,28.35f,29.65f,43.35f,44.65f,55.38f})
                    ClassicalColumn(root,new Vector3(side*7.48f,0,z),6.15f,.31f,p);
            }
            foreach(float z in new[]{14f,29f,44f})
            {
                ArchPortal(root,new Vector3(0,0,z-.24f),Quaternion.identity,3.6f,3.9f,.30f,p);
                for(int side=-1;side<=1;side+=2)
                    WallFinish(root,new Vector3(side*4.95f,0,z-.17f),Quaternion.identity,5.90f,GalleryConceptWorld.Roof,p);
            }
            WallFinish(root,new Vector3(0,0,.16f),Quaternion.Euler(0,180,0),15.6f,GalleryConceptWorld.Roof,p);
            WallFinish(root,new Vector3(0,0,55.84f),Quaternion.identity,15.6f,GalleryConceptWorld.Roof,p);
            foreach(var aperture in new[]{new Vector2(3,6.2f),new Vector2(18,22.2f),new Vector2(47,50)})
                Skylight(root,new Vector3(0,GalleryConceptWorld.Roof,(aperture.x+aperture.y)/2),Quaternion.identity,
                    new Vector2(aperture.x==32?GalleryConceptWorld.PortraitSkylightHalfWidth*2:15.65f,aperture.y-aperture.x),1.15f,p);

            // Tall, quiet wall-mounted works replace the previous billboard-like
            // blocks. A clear central axis leads through the first stone portal.
            FramedArtwork(root,"StarryNight","별이 빛나는 밤","빈센트 반 고흐 · 1889",new Vector3(-4.95f,2.85f,13.74f),Quaternion.identity,3.5f,3.0f,p);
            FramedArtwork(root,"WaterLilyBridge","수련 연못 위의 다리","클로드 모네 · 1899",new Vector3(4.95f,2.85f,13.74f),Quaternion.identity,2.5f,3.0f,p);
            string[] keys={"Scream","GirlWithPearlEarring","Ambassadors"};
            string[] titles={"절규","진주 귀걸이를 한 소녀","대사들"};
            string[] credits={"에드바르 뭉크 · 1893","요하네스 페르메이르 · 약 1665","한스 홀바인 · 1533"};
            for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
            {
                int index=(i+(side>0?1:0))%3;
                FramedArtwork(root,keys[index],titles[index],credits[index],new Vector3(side*7.78f,2.8f,3.5f+i*3.9f),Quaternion.Euler(0,side*90,0),2.6f,2.8f,p);
            }
            Bench(root,new Vector3(-4.9f,0,7.0f),Quaternion.Euler(0,90,0),2.5f,p);
            Bench(root,new Vector3(4.9f,0,7.0f),Quaternion.Euler(0,90,0),2.5f,p);
            Track(root,new Vector3(0,6.1f,12.25f),13,p);
            for(int side=-1;side<=1;side+=2)
            {
                var rail=Group("Side gallery picture lighting",root,new Vector3(side*6.65f,6.1f,7),Quaternion.Euler(0,90,0));
                Track(rail,Vector3.zero,10,p);
            }
            // The left wall painting frames the open Nike projection area.
            FramedArtwork(root,"Ambassadors","대사들","한스 홀바인 · 1533",new Vector3(-7.78f,2.85f,23.1f),Quaternion.Euler(0,-90,0),3.5f,3.0f,p);
            Track(root,new Vector3(0,6.1f,27.5f),12,p);
            FramedArtwork(root,"Scream","절규","에드바르 뭉크 · 1893",new Vector3(-7.78f,2.75f,48.4f),Quaternion.Euler(0,-90,0),2.2f,2.9f,p);
            GalleryPortraitRoom.Install(game,p);
        }
    }
}
