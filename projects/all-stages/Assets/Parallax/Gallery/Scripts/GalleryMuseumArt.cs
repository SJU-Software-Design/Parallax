using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Exhibition dressing is deliberately separate from the puzzle's collision and
    // analytic sunlight geometry. A display light must never make unsafe sunlight safe.
    public static class GalleryMuseumArt
    {
        static Transform museum;
        static Material plaster, marble, gold, walnut, black, cream, teal, red;
        static readonly string[] Keys={"StarryNight","WaterLilyBridge","Scream","Ambassadors","GirlWithPearlEarring"};
        static readonly string[] Captions={"별이 빛나는 밤\n빈센트 반 고흐 · 1889","수련 연못 위의 다리\n클로드 모네 · 1899","절규\n에드바르 뭉크 · 1893","대사들\n한스 홀바인 · 1533","진주 귀걸이를 한 소녀\n요하네스 페르메이르 · 약 1665"};
        public static Material Material(string name,Color color,float stone=0,float sheen=0)
        {
            var shader=Shader.Find("Parallax/MuseumSurface");
            if(shader==null)throw new InvalidOperationException("MuseumSurface shader is missing");
            var m=new Material(shader){name=name,color=color};
            m.SetFloat("_Stone",stone);m.SetFloat("_Sheen",sheen);GalleryWorld.Materials.Add(m);return m;
        }
        static GameObject Box(string name,Vector3 p,Vector3 size,Material material,Transform parent=null)
        { return GalleryWorld.Box(name,p,size,material,parent==null?museum:parent,false,false); }
        static GameObject Primitive(string name,PrimitiveType shape,Vector3 p,Vector3 scale,Material material,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent==null?museum:parent,false);
            go.transform.localPosition=p;go.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return go;
        }
        static GameObject MeshObject(string name,Mesh mesh,Material material,Transform parent=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent==null?museum:parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return go;
        }
        public static void Text(string caption,Vector3 position,Quaternion rotation,float size,Color color,Transform parent)
        {
            var go=new GameObject("Museum label · "+caption.Replace('\n',' '));go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localRotation=rotation;
            var label=go.AddComponent<GalleryLabel>();label.caption=caption;label.size=size;label.color=color;
        }
        static Material Print(string key)
        {
            var texture=Resources.Load<Texture2D>("MuseumArt/"+key);
            if(texture==null)throw new InvalidOperationException("Actual museum artwork missing: "+key);
            var m=Material("Museum painting · "+key,Color.white);m.mainTexture=texture;m.SetFloat("_ExhibitLight",.65f);return m;
        }
        static void Plane(string name,Vector3 position,Vector2 size,Material material,Transform parent)
        {
            var mesh=new Mesh{name="Museum canvas · "+name};
            mesh.vertices=new[]{new Vector3(-size.x/2,-size.y/2,0),new Vector3(size.x/2,-size.y/2,0),new Vector3(size.x/2,size.y/2,0),new Vector3(-size.x/2,size.y/2,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=MeshObject(name,mesh,material,parent);go.transform.localPosition=position;
        }
        static void Frame(Transform parent,Vector2 size,float border,Material frame)
        {
            Box("Frame · left",new Vector3(-size.x/2-border/2,0,0),new Vector3(border,size.y+border*2,.16f),frame,parent);
            Box("Frame · right",new Vector3(size.x/2+border/2,0,0),new Vector3(border,size.y+border*2,.16f),frame,parent);
            Box("Frame · top",new Vector3(0,size.y/2+border/2,0),new Vector3(size.x,border,.16f),frame,parent);
            Box("Frame · bottom",new Vector3(0,-size.y/2-border/2,0),new Vector3(size.x,border,.16f),frame,parent);
            Box("Inset gold · top",new Vector3(0,size.y/2-.025f,-.095f),new Vector3(size.x,.025f,.02f),gold,parent);
            Box("Inset gold · bottom",new Vector3(0,-size.y/2+.025f,-.095f),new Vector3(size.x,.025f,.02f),gold,parent);
        }
        static void Art(string key,string caption,Vector3 position,Quaternion rotation,float maximumWidth,float maximumHeight,Transform parent)
        {
            var root=new GameObject("Collected artwork · "+key).transform;root.SetParent(parent,false);root.localPosition=position;root.localRotation=rotation;
            var material=Print(key);float ratio=(float)material.mainTexture.width/material.mainTexture.height;
            float h=Mathf.Min(maximumHeight,maximumWidth/ratio),w=h*ratio;
            Box("Canvas backing",Vector3.zero,new Vector3(w+.09f,h+.09f,.07f),cream,root);
            Plane(key,new Vector3(0,0,-.046f),new Vector2(w,h),material,root);Frame(root,new Vector2(w+.09f,h+.09f),.09f,gold);
            Box("Artwork information plaque",new Vector3(0,-h/2-.32f,-.015f),new Vector3(Mathf.Min(w,1.8f),.40f,.035f),cream,root);
            Text(caption,new Vector3(0,-h/2-.32f,-.045f),Quaternion.identity,.15f,new Color(.19f,.19f,.17f),root);
            // Warm track-light hardware, not an extra gameplay sunlight source.
            Box("Picture light arm",new Vector3(0,h/2+.36f,-.24f),new Vector3(.035f,.05f,.48f),black,root);
            var lamp=Primitive("Picture light",PrimitiveType.Cylinder,new Vector3(0,h/2+.34f,-.47f),new Vector3(.17f,.11f,.17f),gold,root);lamp.transform.localRotation=Quaternion.Euler(65,0,0);
        }
        static void MovableArt(GalleryProp p,int index)
        {
            foreach(var r in p.renderers)if(!r.name.Contains("seal"))r.enabled=false;
            var m=Print(Keys[index]);float ratio=(float)m.mainTexture.width/m.mainTexture.height;
            float h=Mathf.Min(2.40f,2.00f/ratio),w=h*ratio;
            foreach(float side in new[]{-1f,1f})
            {
                var holder=new GameObject("Museum reproduction side").transform;holder.SetParent(p.transform,false);holder.localRotation=side<0?Quaternion.identity:Quaternion.Euler(0,180,0);
                Box("Ivory mount",new Vector3(0,0,-.067f),new Vector3(2.18f,2.58f,.025f),cream,holder);
                Plane(Keys[index],new Vector3(0,0,-.084f),new Vector2(w,h),m,holder);
                Frame(holder,new Vector2(2.2f,2.60f),.09f,gold);
            }
            p.Remember();
        }
        static void Column(Vector3 position,float height)
        {
            Box("Column foot",position+Vector3.up*.13f,new Vector3(.82f,.26f,.82f),marble);
            Primitive("Fluted stone column",PrimitiveType.Cylinder,position+Vector3.up*(height/2+.26f),new Vector3(.48f,height/2,.48f),plaster);
            Box("Column capital",position+Vector3.up*(height+.28f),new Vector3(.88f,.22f,.88f),cream);
        }
        static Mesh Arch(float inner,float outer,float depth,int segments)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<=segments;i++)
            {
                float a=Mathf.PI*i/segments;
                foreach(float z in new[]{-depth/2,depth/2})foreach(float r in new[]{inner,outer})vertices.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,z));
            }
            for(int i=0;i<segments;i++)
            {
                int a=i*4,b=a+4;
                foreach(var face in new[]{new[]{a,b,b+1,a+1},new[]{a+2,a+3,b+3,b+2},new[]{a,a+2,b+2,b},new[]{a+1,b+1,b+3,a+3}})
                { triangles.Add(face[0]);triangles.Add(face[1]);triangles.Add(face[2]);triangles.Add(face[0]);triangles.Add(face[2]);triangles.Add(face[3]); }
            }
            var mesh=new Mesh{name="Carved museum arch"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void Architrave(float z,float baseHeight)
        {
            for(int s=-1;s<=1;s+=2)
            {
                Box("Doorway limestone pier",new Vector3(s*2.63f,baseHeight/2,z),new Vector3(.38f,baseHeight,.58f),plaster);
                Box("Doorway pier foot",new Vector3(s*2.63f,.12f,z),new Vector3(.58f,.24f,.7f),marble);
            }
            var arch=MeshObject("Limestone arch surround",Arch(2.43f,2.82f,.56f,32),plaster);arch.transform.localPosition=new Vector3(0,baseHeight,z);
        }
        static Mesh Drapery()
        {
            const int rings=22, sides=36;var v=new List<Vector3>();var t=new List<int>();
            for(int y=0;y<=rings;y++)for(int a=0;a<=sides;a++)
            {
                float h=(float)y/rings,angle=a*Mathf.PI*2/sides;
                float width=Mathf.Lerp(.52f,.36f,h)+.15f*Mathf.Sin(h*Mathf.PI*2-.4f);
                if(h>.77f)width+=.33f*Mathf.Sin((h-.77f)/.23f*Mathf.PI);
                float fold=1+.095f*Mathf.Sin(angle*9+h*4)+.025f*Mathf.Sin(angle*17-h*8);
                v.Add(new Vector3(Mathf.Cos(angle)*width*fold+.18f*h,h*2.7f,Mathf.Sin(angle)*width*.63f*fold+.15f*h));
            }
            for(int y=0;y<rings;y++)for(int a=0;a<sides;a++){int p=y*(sides+1)+a;t.AddRange(new[]{p,p+sides+1,p+1,p+1,p+sides+1,p+sides+2});}
            var mesh=new Mesh{name="Winged study · draped figure"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Wing(int side)
        {
            const int segments=24;var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=segments;i++)
            {
                float u=(float)i/segments,x=side*(.30f+u*2.05f),y=2.25f+1.08f*Mathf.Sin(u*Mathf.PI*.62f);
                float span=(.14f+.50f*Mathf.Sin(u*Mathf.PI))*(1-.15f*u);
                v.Add(new Vector3(x,y+.20f,-.02f+u*.28f));v.Add(new Vector3(x,y-span,-.12f+u*.28f));
            }
            for(int i=0;i<segments;i++){int p=i*2;t.AddRange(new[]{p,p+2,p+1,p+1,p+2,p+3});}
            var mesh=new Mesh{name="Winged study · wing "+side};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void WingedStudy()
        {
            var statue=new GameObject("Winged study · game sculpture after Nike").transform;statue.SetParent(museum,false);statue.localPosition=new Vector3(-9,1.05f,4.9f);statue.localRotation=Quaternion.Euler(0,-18,0);
            Box("Sculpture marble plinth",new Vector3(-9,.50f,4.9f),new Vector3(3.1f,1,2.2f),marble);
            MeshObject("Draped headless figure",Drapery(),marble,statue);
            for(int side=-1;side<=1;side+=2)
            {
                MeshObject("Raised wing",Wing(side),marble,statue);
                for(int i=0;i<10;i++)
                {
                    float u=(i+1)/11f;
                    var feather=Primitive("Stone feather",PrimitiveType.Capsule,new Vector3(side*(.50f+u*1.73f),2.37f+.81f*Mathf.Sin(u*Mathf.PI*.62f),-.025f+u*.28f),new Vector3(.12f,.28f-.1f*u,.09f),cream,statue);
                    feather.transform.localRotation=Quaternion.Euler(0,0,-side*(50+20*u));
                }
            }
            Box("Sculpture label",new Vector3(-9,.73f,3.78f),new Vector3(2.6f,.35f,.035f),cream);
            Text("날개의 습작\n사모트라케의 니케에서 착안한 게임 조각",new Vector3(-9,.73f,3.75f),Quaternion.identity,.15f,new Color(.18f,.18f,.16f),museum);
        }
        public static void Install(GalleryGame game)
        {
            if(game.transform.Find("Museum exhibition collection")!=null)return;
            museum=new GameObject("Museum exhibition collection").transform;museum.SetParent(game.transform,false);
            plaster=Material("Museum ivory limestone",new Color(.83f,.79f,.69f),1,.02f);
            marble=Material("Museum honed travertine",new Color(.83f,.79f,.70f),1,.13f);
            gold=Material("Museum antique bronze",new Color(.66f,.45f,.20f),.4f,.18f);
            walnut=Material("Museum walnut seating",new Color(.24f,.13f,.075f),.5f,.05f);
            black=Material("Museum graphite hardware",new Color(.11f,.13f,.13f));
            cream=Material("Museum archival mount",new Color(.94f,.91f,.83f));
            teal=Material("Museum exhibition teal",new Color(.18f,.29f,.29f));
            red=Material("Museum vermilion upholstery",new Color(.55f,.055f,.035f),.2f);
            int painting=0;foreach(var p in game.props)if(p.title.Contains("그림"))MovableArt(p,painting++%Keys.Length);
            foreach(var r in game.GetComponentsInChildren<Renderer>())if(r.name=="Permanent gallery frame"||r.name=="Permanent print")r.enabled=false;
            foreach(var bench in game.GetComponentsInChildren<Transform>())if(bench.name=="Visitor bench seat")bench.GetComponent<Renderer>().sharedMaterial=walnut;
            for(int side=-1;side<=1;side+=2)
            {
                foreach(float z in new[]{.6f,13.4f,26.4f,39.4f,53.4f})Column(new Vector3(side*13.55f,0,z),6.7f);
                Box("Wall base course",new Vector3(side*13.94f,.20f,27),new Vector3(.13f,.4f,54),marble);
                Box("Wall picture rail",new Vector3(side*13.93f,6.95f,27),new Vector3(.18f,.18f,54),cream);
            }
            foreach(var window in GalleryLighting.Windows)
            {
                float length=window.w-window.z,mid=(window.z+window.w)/2;
                for(int side=-1;side<=1;side+=2)Box("Skylight bronze curb",new Vector3(side*13.7f,10.2f,mid),new Vector3(.12f,.28f,length),gold);
                for(float x=-12;x<=12;x+=4)Box("Skylight slender glazing bar",new Vector3(x,10.22f,mid),new Vector3(.045f,.08f,length),gold);
                for(int edge=0;edge<2;edge++)Box("Skylight end frame",new Vector3(0,10.2f,edge==0?window.z:window.w),new Vector3(27.4f,.2f,.10f),gold);
            }
            // The first playable camera faces +Z. These two real exhibits are
            // visible immediately, while the five-metre central approach stays open.
            for(int side=-1;side<=1;side+=2)
                GalleryWorld.Box("Entrance exhibition wing",new Vector3(side*3.85f,2.2f,8.10f),new Vector3(2.55f,4.4f,.24f),teal,museum,true,true);
            Art("StarryNight",Captions[0],new Vector3(-3.85f,2.75f,7.95f),Quaternion.identity,2.15f,2.5f,museum);
            Art("WaterLilyBridge",Captions[1],new Vector3(3.85f,2.75f,7.95f),Quaternion.identity,2.15f,2.5f,museum);
            Architrave(13.72f,2.18f);Architrave(26.72f,2.18f);Architrave(39.72f,3.2f);
            // Full-size collection on the side walls; authentic paintings retain their aspect ratio.
            for(int zone=0;zone<4;zone++)
            {
                float z=new[]{5f,18.8f,31.7f,44.8f}[zone];
                for(int j=0;j<3;j++)
                {
                    int index=(zone+j)%Keys.Length;
                    Art(Keys[index],Captions[index],new Vector3(13.88f,2.9f,z+(j-1)*3.4f),Quaternion.Euler(0,90,0),2.6f,2.8f,museum);
                }
            }
            Art("StarryNight",Captions[0],new Vector3(-3.15f,2.9f,.22f),Quaternion.Euler(0,180,0),2.5f,2.5f,museum);
            Art("WaterLilyBridge",Captions[1],new Vector3(3.15f,2.9f,.22f),Quaternion.Euler(0,180,0),2.2f,2.5f,museum);
            Art("Scream",Captions[2],new Vector3(-13.88f,2.8f,10),Quaternion.Euler(0,-90,0),2.3f,3.0f,museum);
            Art("Ambassadors",Captions[3],new Vector3(-13.88f,2.8f,18.5f),Quaternion.Euler(0,-90,0),3.6f,3.4f,museum);
            Art("GirlWithPearlEarring",Captions[4],new Vector3(-13.88f,2.7f,44.8f),Quaternion.Euler(0,-90,0),2.2f,2.6f,museum);
            Text("PARALLAX\n시선과 빛의 미술관",new Vector3(0,6.65f,.23f),Quaternion.Euler(0,180,0),.6f,new Color(.26f,.25f,.20f),museum);
            Text("01  명화 전시실",new Vector3(0,5.45f,13.6f),Quaternion.identity,.4f,new Color(.85f,.80f,.66f),museum);
            Text("02  빛을 꺾는 거울",new Vector3(0,5.45f,26.6f),Quaternion.identity,.4f,new Color(.85f,.80f,.66f),museum);
            Text("03  시점의 전시실",new Vector3(-8,5.4f,39.55f),Quaternion.identity,.42f,new Color(.90f,.86f,.73f),museum);
            WingedStudy();
            BuildPerspective(game);
        }
        static GalleryProp ExhibitProp(string title,Vector3 position,float scale)
        {
            var go=new GameObject(title);go.transform.SetParent(museum,false);go.transform.position=position;
            var p=go.AddComponent<GalleryProp>();p.title=title;p.zone=2;p.minScale=.35f;p.maxScale=4;go.transform.localScale=Vector3.one*scale;return p;
        }
        static void Part(GalleryProp prop,string name,Vector3 position,Vector3 size,Material material,bool shade=false)
        { GalleryWorld.Box(name,position,size,material,prop.transform,shade,true); }
        static void BuildPerspective(GalleryGame game)
        {
            var propList=new List<GalleryProp>(game.props);
            var eyes=ExhibitProp("03 · 시점의 초상 · 눈 액자",new Vector3(-10.2f,0,37.3f),.62f);
            Part(eyes,"Paired picture stand base",new Vector3(0,.08f,0),new Vector3(3.5f,.16f,.9f),black);
            for(int side=-1;side<=1;side+=2)
            {
                Part(eyes,"Portrait stand",new Vector3(side*1.45f,1.25f,0),new Vector3(.09f,2.5f,.12f),gold);
                Part(eyes,"Eye framed canvas",new Vector3(side*.94f,2.5f,0),new Vector3(1.45f,1.02f,.13f),cream);
                var frameRoot=new GameObject("Eye antique frame").transform;frameRoot.SetParent(eyes.transform,false);frameRoot.localPosition=new Vector3(side*.94f,2.5f,-.075f);Frame(frameRoot,new Vector2(1.45f,1.02f),.075f,gold);
                Primitive("Eye white",PrimitiveType.Sphere,new Vector3(side*.94f,2.5f,-.10f),new Vector3(1.05f,.42f,.08f),cream,eyes.transform);
                Primitive("Eye iris",PrimitiveType.Sphere,new Vector3(side*.94f,2.5f,-.155f),new Vector3(.32f,.35f,.055f),teal,eyes.transform);
                Primitive("Eye pupil",PrimitiveType.Sphere,new Vector3(side*.94f,2.5f,-.189f),new Vector3(.14f,.20f,.025f),black,eyes.transform);
                var brow=Primitive("Portrait eyebrow",PrimitiveType.Capsule,new Vector3(side*.94f,2.85f,-.10f),new Vector3(.10f,.48f,.06f),walnut,eyes.transform);brow.transform.localRotation=Quaternion.Euler(0,0,90+side*5);
            }
            eyes.Remember();propList.Add(eyes);
            var nose=ExhibitProp("03 · 시점의 초상 · 코 조각",new Vector3(-7.0f,0,35.6f),.62f);
            Part(nose,"Nose exhibit base",new Vector3(0,.08f,0),new Vector3(.90f,.16f,.90f),marble);
            Part(nose,"Nose exhibit stand",new Vector3(0,.7f,.10f),new Vector3(.10f,1.35f,.12f),gold);
            Part(nose,"Nose collision silhouette",new Vector3(0,1.55f,0),new Vector3(.52f,1.05f,.60f),marble,true);
            nose.transform.Find("Nose collision silhouette").GetComponent<Renderer>().enabled=false;
            Primitive("Sculpted nose bridge",PrimitiveType.Capsule,new Vector3(0,1.73f,.04f),new Vector3(.27f,.47f,.26f),plaster,nose.transform);
            Primitive("Sculpted nose tip",PrimitiveType.Sphere,new Vector3(0,1.38f,-.18f),new Vector3(.5f,.4f,.55f),marble,nose.transform);
            for(int side=-1;side<=1;side+=2)Primitive("Nostril",PrimitiveType.Sphere,new Vector3(side*.16f,1.28f,-.35f),new Vector3(.12f,.075f,.04f),walnut,nose.transform);
            nose.Remember();propList.Add(nose);
            GalleryWorld.Box("Lips sofa exhibition platform",new Vector3(-9,.25f,33.8f),new Vector3(5.5f,.5f,2.3f),marble,museum,false,true);
            var lips=ExhibitProp("03 · 시점의 초상 · 입술 소파",new Vector3(-10,.50f,33.3f),.62f);
            Part(lips,"Sofa body",new Vector3(0,.40f,0),new Vector3(2.35f,.75f,.9f),red);
            lips.transform.Find("Sofa body").GetComponent<Renderer>().enabled=false;
            Primitive("Lower velvet lip",PrimitiveType.Sphere,new Vector3(0,.38f,-.03f),new Vector3(2.4f,.58f,.95f),red,lips.transform);
            for(int side=-1;side<=1;side+=2)Primitive("Upper velvet lip",PrimitiveType.Sphere,new Vector3(side*.52f,.79f,.16f),new Vector3(1.4f,.65f,.68f),red,lips.transform);
            Part(lips,"Sofa foot",new Vector3(-.85f,.06f,0),new Vector3(.13f,.12f,.5f),gold);Part(lips,"Sofa foot",new Vector3(.85f,.06f,0),new Vector3(.13f,.12f,.5f),gold);
            lips.Remember();propList.Add(lips);game.props=propList.ToArray();
            var viewing=new GameObject("Perspective exhibit viewing point").transform;viewing.SetParent(museum,false);viewing.position=new Vector3(-9,0,29.2f);
            var disc=Primitive("Brass observation point",PrimitiveType.Cylinder,new Vector3(-9,.012f,29.2f),new Vector3(1.1f,.008f,1.1f),gold);
            Text("관람 위치",new Vector3(-9,.025f,28.25f),Quaternion.Euler(90,0,0),.23f,new Color(.55f,.40f,.16f),museum);
            Text("시점의 초상\n눈 · 코 · 입을 키우고 옮겨\n관람 위치에서 얼굴을 완성해 보세요",new Vector3(-12.5f,2.3f,28.9f),Quaternion.Euler(0,-90,0),.24f,new Color(.85f,.81f,.70f),museum);
            Text("메이 웨스트의 방에서 착안한 체험 전시",new Vector3(-9,4.65f,39.55f),Quaternion.identity,.22f,new Color(.86f,.81f,.66f),museum);
            var feedback=Box("Perspective completion frame",new Vector3(-9,4.1f,39.45f),new Vector3(5,.08f,.08f),gold).GetComponent<Renderer>();
            var exhibit=museum.gameObject.AddComponent<GalleryMuseumPerspective>();exhibit.game=game;exhibit.viewingPoint=viewing;
            exhibit.pieces=new[]{eyes,nose,lips};exhibit.solutionPositions=new[]{new Vector3(-9,0,38.2f),new Vector3(-9,0,36.2f),new Vector3(-9,.50f,33.8f)};
            exhibit.solutionScales=new[]{1f,1f,1f};exhibit.markerLocalPositions=new[]{new Vector3(0,2.5f,0),new Vector3(0,1.55f,0),new Vector3(0,.65f,0)};exhibit.feedback=feedback;
        }
    }
}
