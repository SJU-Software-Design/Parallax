using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Library
{
    /// <summary>Four linked reading rooms. Furniture, physical silhouettes and shadow casters share geometry.</summary>
    public static class LibraryWorld
    {
        public const float Width = 28, Length = 54;
        public static readonly Bounds PlayBounds = new Bounds(new Vector3(0, 4.87f, 27), new Vector3(27.6f, 9.76f, 53.6f));
        public static readonly Vector3[] Checkpoints = {
            new Vector3(0,0,4), new Vector3(-7,0,16.2f), new Vector3(6,0,29), new Vector3(-5,0,42)
        };
        public static readonly string[] Titles = {
            "01  서가 뒤의 긴 그늘", "02  책 한 권의 다리", "03  책상 아래로 걷기", "04  같은 서가, 두 번의 길"
        };
        public static readonly string[] Hints = {
            "빈 책꽂이를 가까이에서 잡고, 더 먼 곳에 배치해 키우세요.\n왼쪽 문 앞 햇빛을 책꽂이의 넓은 뒷면으로 가립니다.\n책꽂이 옆으로 돌아 뒤쪽의 그늘을 따라 이동하세요.",
            "책 두 권을 서가 밖의 넓은 바닥에 먼저 꺼내세요.\n먼쪽 책부터 크게 세우고, 가까운 책을 나중에 배치합니다.\n차양 앞뒤에 그림자를 연결하고 책 옆을 돌아갈 공간도 남겨 두세요.",
            "책상 앞쪽의 여유 있는 곳에서 정면을 보고 잡아 보세요.\n책상을 키우면 다리 사이로 사람이 지나갈 수 있습니다.\n상판의 그늘을 햇빛 구간에 겹쳐 왼쪽 문까지 통과하세요.",
            "빈 서가 정면에서 잡아 키운 뒤 중앙 쉼터까지 그늘을 만드세요.\n쉼터에 도착하면 사용한 서가를 다시 집어 오른쪽으로 옮깁니다.\n서가 옆으로 돌아갈 공간을 남기고 출구까지 그늘을 연결하세요."
        };
        // Sun direction shifts a ground point 7.5 m toward the entrance at the 10 m roof.
        // Ground light bands: 9–12.3, 20–26, 34–38, 44–52.5 m.
        public static readonly Vector4[] Skylights = {
            new Vector4(-14.1f,14.1f,1.5f,4.8f), new Vector4(-14.1f,14.1f,12.5f,18.5f),
            new Vector4(-14.1f,14.1f,26.5f,30.5f), new Vector4(-14.1f,14.1f,36.5f,45f)
        };
        public static Vector2 ZoneDepth(int zone)
        {
            return new Vector2(new[] {.18f,14.3f,27.3f,40.3f}[Mathf.Clamp(zone,0,3)],
                new[] {13.7f,26.7f,39.7f,53.8f}[Mathf.Clamp(zone,0,3)]);
        }
        public static int CheckpointFor(Vector3 feet, int current)
        {
            int next=feet.z>41.2f?3:feet.z>28.2f?2:feet.z>15.2f?1:0;
            return Mathf.Max(current,next);
        }
        public static bool IsExit(Vector3 feet, int checkpoint)
        {
            return checkpoint==3 && feet.z>53.05f && Mathf.Abs(feet.x-6)<1.35f && feet.y>-.1f && feet.y<.2f;
        }

        static Transform root;
        static Material plaster, wood, darkWood, stone, teal, gold, ink, redBook, greenBook, blueBook;
        public static readonly List<Material> Materials = new List<Material>();

        static Material Mat(string name, Color color, bool flat=false, bool grid=false)
        {
            var material = new Material(Shader.Find(flat?"ShadeLink/Flat":"Parallax/LibrarySurface")) { name=name, color=color };
            if(grid)material.SetFloat("_Grid",1);
            Materials.Add(material); return material;
        }
        public static GameObject Box(string name, Vector3 position, Vector3 size, Material material,
            Transform parent=null, bool caster=true, bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
            go.transform.SetParent(parent==null?root:parent,false);
            go.transform.localPosition=position; go.transform.localScale=size;
            var renderer=go.GetComponent<Renderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            var shape=go.GetComponent<BoxCollider>();
            if(caster)go.AddComponent<LibraryCaster>().shape=shape;
            if(!solid){if(Application.isPlaying)Object.Destroy(shape);else Object.DestroyImmediate(shape);}
            return go;
        }
        static void Label(string text,Vector3 position,float size,Color color,Quaternion rotation)
        {
            var go=new GameObject("Library sign · "+text.Replace('\n',' '));go.transform.SetParent(root,false);
            go.transform.SetPositionAndRotation(position,rotation);
            var label=go.AddComponent<LibraryLabel>();label.caption=text;label.size=size;label.color=color;
        }
        static LibraryProp Prop(string id,string title,string kind,int zone,Vector3 position,bool platform,float maximum)
        {
            var go=new GameObject(title);go.transform.SetParent(root,false);go.transform.localPosition=position;
            var prop=go.AddComponent<LibraryProp>();prop.title=title;prop.objectId=id;prop.kind=kind;
            prop.zone=zone;prop.platform=platform;prop.minScale=.35f;prop.maxScale=maximum;
            return prop;
        }
        static void Finish(LibraryProp prop)
        {
            // Build-time source-mesh integration uses kind/objectId and this identity anchor.
            // A book is always its own root; it never inherits its shelf's scale or Rigidbody.
            var anchor=new GameObject("Kit visual anchor · "+prop.kind);
            anchor.transform.SetParent(prop.transform,false);
            prop.Remember();
        }
        static LibraryProp Shelf(string id,string title,int zone,Vector3 at)
        {
            var prop=Prop(id,title,"Bookshelf",zone,at,false,2.8f);
            Box("Shelf rear panel",new Vector3(0,0,.31f),new Vector3(2.4f,3,.08f),wood,prop.transform);
            for(int side=-1;side<=1;side+=2)
                Box("Shelf side",new Vector3(side*1.14f,0,0),new Vector3(.12f,3,.7f),darkWood,prop.transform);
            foreach(float y in new[]{-1.44f,-.48f,.48f,1.44f})
                Box("Shelf board",new Vector3(0,y,0),new Vector3(2.16f,.12f,.7f),wood,prop.transform);
            Box("Movable library seal",new Vector3(0,-1.44f,-.356f),new Vector3(.46f,.07f,.008f),teal,prop.transform,false,false);
            Finish(prop);return prop;
        }
        static LibraryProp Book(string id,string title,int zone,Vector3 at,int palette=0)
        {
            var prop=Prop(id,title,"Book",zone,at,true,8.5f);
            var cover=palette%3==0?redBook:palette%3==1?greenBook:blueBook;
            Box("Book solid pages and binding",Vector3.zero,new Vector3(.48f,.8f,.12f),cover,prop.transform);
            Box("Book spine gold",new Vector3(0,.23f,-.062f),new Vector3(.31f,.025f,.004f),gold,prop.transform,false,false);
            Box("Movable library seal",new Vector3(0,-.25f,-.063f),new Vector3(.11f,.11f,.006f),teal,prop.transform,false,false);
            Finish(prop);return prop;
        }
        static void LoadedShelf(List<LibraryProp> props,string id,string title,int zone,Vector3 at,Quaternion rotation)
        {
            var shelf=Shelf(id,title,zone,at);shelf.transform.rotation=rotation;shelf.Remember();props.Add(shelf);
            for(int level=0;level<3;level++)for(int slot=0;slot<4;slot++)
            {
                int n=level*4+slot;
                Vector3 local=new Vector3(-.78f+slot*.52f,-.965f+level*.96f,-.22f);
                var book=Book(id+"-book-"+(n+1).ToString("00"),title+" · 책 "+(n+1).ToString("00"),zone,at+rotation*local,n);
                book.transform.rotation=rotation;book.Remember();props.Add(book);
            }
        }
        static LibraryProp Table(string id,string title,int zone,Vector3 at)
        {
            var prop=Prop(id,title,"Table",zone,at,true,3.5f);
            Box("Table canopy top",new Vector3(0,.38f,0),new Vector3(2.2f,.14f,1.7f),wood,prop.transform);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Box("Table leg",new Vector3(x*.92f,-.07f,z*.68f),new Vector3(.16f,.76f,.16f),darkWood,prop.transform);
            Box("Movable library seal",new Vector3(0,.382f,-.856f),new Vector3(.4f,.06f,.01f),teal,prop.transform,false,false);
            Finish(prop);return prop;
        }
        static LibraryProp Chair(string id,string title,int zone,Vector3 at)
        {
            var prop=Prop(id,title,"Chair",zone,at,true,4.5f);
            Box("Chair seat",new Vector3(0,-.04f,0),new Vector3(.76f,.12f,.76f),wood,prop.transform);
            Box("Chair back",new Vector3(0,.285f,.32f),new Vector3(.76f,.53f,.12f),wood,prop.transform);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Box("Chair leg",new Vector3(x*.29f,-.32f,z*.29f),new Vector3(.12f,.46f,.12f),darkWood,prop.transform);
            Box("Movable library seal",new Vector3(0,.3f,.254f),new Vector3(.16f,.16f,.008f),teal,prop.transform,false,false);
            Finish(prop);return prop;
        }
        static void Partition(float z,float doorX)
        {
            const float halfDoor=2.1f;
            float left=doorX-halfDoor,right=doorX+halfDoor;
            Box("Reading-room partition left",new Vector3((-14+left)/2,3.4f,z),new Vector3(left+14,6.8f,.38f),plaster);
            Box("Reading-room partition right",new Vector3((right+14)/2,3.4f,z),new Vector3(14-right,6.8f,.38f),plaster);
            Box("Timber lintel",new Vector3(doorX,5.15f,z),new Vector3(4.2f,3.3f,.38f),darkWood);
            for(int s=-1;s<=1;s+=2)
                Box("Door brass edge",new Vector3(doorX+s*2.02f,1.75f,z-.205f),new Vector3(.04f,3.5f,.025f),gold,caster:false,solid:false);
        }
        static void Canopy(string name,float x,float z,float width,float depth,float height=3.2f)
        {
            Box(name+" roof",new Vector3(x,height,z),new Vector3(width,.2f,depth),wood);
            for(int side=-1;side<=1;side+=2)
                Box(name+" column",new Vector3(x+side*(width/2-.1f),(height-.1f)/2,z),new Vector3(.2f,height-.1f,.2f),darkWood);
        }
        static void ArchitecturalTimber()
        {
            // These pilasters are embedded in the existing side walls. Only a 5 mm
            // finish faces the room; the playable width and puzzle route stay unchanged.
            for(int room=0;room<4;room++)for(int side=-1;side<=1;side+=2)
            {
                float z=new[]{4.2f,16.8f,29.2f,42.1f}[room];
                Box("Oak wall pilaster "+(room+1)+" "+side,new Vector3(side*14.035f,4,z),
                    new Vector3(.08f,8,.62f),wood);
                Box("Oak wall dado "+(room+1)+" "+side,new Vector3(side*14.037f,.78f,new[]{6.8f,20.5f,33.5f,47f}[room]),
                    new Vector3(.08f,1.56f,new[]{13.2f,12.2f,12.2f,13.2f}[room]),darkWood,caster:false);
            }
            // Crossbeams are above the 9.75 m manipulation ceiling, wholly beneath
            // opaque roof spans. They cannot close an aperture or add a puzzle shadow.
            foreach(float z in new[]{6.4f,11.1f,19.8f,25.1f,31.7f,35.2f,46.5f,52.4f})
                Box("Oak roof crossbeam",new Vector3(0,9.94f,z),new Vector3(28.1f,.24f,.3f),wood,caster:false);
        }
        public static LibraryGame Build()
        {
            Materials.Clear();root=new GameObject("PARALLAX / Library — 28 × 54 m").transform;
            plaster=Mat("Warm library plaster",new Color(.68f,.61f,.43f));
            wood=Mat("Oak library furniture",new Color(.48f,.26f,.11f));
            darkWood=Mat("Dark oak beams",new Color(.25f,.13f,.065f));
            stone=Mat("Library limestone",new Color(.77f,.72f,.59f),false,true);
            teal=Mat("Movable turquoise library mark",new Color(.2f,.96f,.78f),true);
            gold=Mat("Warm brass",new Color(.95f,.69f,.24f),true);
            ink=Mat("Exit charcoal",new Color(.08f,.14f,.14f));
            redBook=Mat("Oxblood leather",new Color(.60f,.15f,.09f));
            greenBook=Mat("Sage leather",new Color(.24f,.46f,.22f));
            blueBook=Mat("Indigo leather",new Color(.17f,.28f,.52f));
            var game=root.gameObject.AddComponent<LibraryGame>();game.lighting=root.gameObject.AddComponent<LibraryLighting>();
            Box("Library floor — 1512 square metres",new Vector3(0,-.2f,27),new Vector3(28,.4f,54),stone,caster:false);
            Box("Entrance facade",new Vector3(0,5,-.18f),new Vector3(28.4f,10,.36f),plaster);
            Box("Exit facade",new Vector3(0,5,54.18f),new Vector3(28.4f,10,.36f),plaster);
            for(int s=-1;s<=1;s+=2)
                Box("Library side wall",new Vector3(s*14.18f,5,27),new Vector3(.36f,10,54),plaster);
            float cursor=0;
            foreach(var aperture in Skylights)
            {
                if(aperture.z>cursor)Box("Opaque library roof",new Vector3(0,10.15f,(cursor+aperture.z)/2),new Vector3(28,.3f,aperture.z-cursor),darkWood,caster:false);
                for(int side=-1;side<=1;side+=2)
                    Box("Skylight brass rim",new Vector3(side*13.8f,10.02f,(aperture.z+aperture.w)/2),new Vector3(.08f,.05f,aperture.w-aperture.z),gold,caster:false,solid:false);
                cursor=aperture.w;
            }
            if(cursor<54)Box("Opaque library roof",new Vector3(0,10.15f,(cursor+54)/2),new Vector3(28,.3f,54-cursor),darkWood,caster:false);
            Partition(14,-7);Partition(27,6);Partition(40,-5);
            // Preserve the small resting island while leaving clearance to move enlarged books.
            Canopy("Book bridge reading shelter",6,19.35f,4.5f,1.7f,5.2f);
            // Raising this canopy and moving it toward the sun preserves its ground shadow,
            // while leaving room to retrieve the six-metre shelf beneath it.
            Canopy("Final crosswise reading shelter",0,43,18,2.4f,7.2f);
            ArchitecturalTimber();

            var props=new List<LibraryProp>();
            props.Add(Shelf("shelf-a","01 · 빈 서가 / 넓은 그늘",0,new Vector3(-7,1.5f,4.7f)));
            props.Add(Chair("chair-a","01 · 열람 의자",0,new Vector3(7,.55f,4.7f)));
            props.Add(Table("table-reading","01 · 열람 탁자",0,new Vector3(10,.45f,4.5f)));
            props.Add(Book("reading-book-a","01 · 열람용 책 1",0,new Vector3(9.55f,1.32f,4.5f),1));
            props.Add(Book("reading-book-b","01 · 열람용 책 2",0,new Vector3(10.2f,1.32f,4.5f),2));
            LoadedShelf(props,"reading-shelf-west","01 · 입구의 열람 서가",0,new Vector3(-11.6f,1.5f,2.2f),Quaternion.Euler(0,180,0));

            props.Add(Shelf("shelf-loaded","02 · 책이 꽂힌 서가",1,new Vector3(9,1.5f,17.2f)));
            for(int level=0;level<3;level++)for(int slot=0;slot<4;slot++)
            {
                int n=level*4+slot;
                string id=n==4?"hero-book-a":n==5?"hero-book-b":"shelf-book-"+(n+1).ToString("00");
                props.Add(Book(id,"02 · 서가의 책 "+(n+1).ToString("00"),1,
                    new Vector3(8.22f+slot*.52f,.535f+level*.96f,16.98f),n));
            }
            props.Add(Table("table-a","03 · 차양이 되는 책상",2,new Vector3(-8,.45f,29.7f)));
            props.Add(Chair("chair-b","03 · 이동식 열람 의자",2,new Vector3(-11,.55f,30)));
            props.Add(Book("reading-book-c","03 · 참고 도서 1",2,new Vector3(8,.4f,30),0));
            props.Add(Book("reading-book-d","03 · 참고 도서 2",2,new Vector3(9,.4f,30),1));
            LoadedShelf(props,"reading-shelf-east","03 · 참고 도서 서가",2,new Vector3(10.5f,1.5f,29.7f),Quaternion.identity);
            props.Add(Shelf("shelf-b","04 · 다시 사용하는 서가",3,new Vector3(-8,1.5f,42.4f)));
            game.props=props.ToArray();

            for(int i=0;i<4;i++)
            {
                var spawn=Checkpoints[i];
                Box("Checkpoint inlay "+(i+1),spawn+new Vector3(0,.006f,0),new Vector3(2.7f,.012f,.045f),teal,caster:false,solid:false);
                Label("0"+(i+1),spawn+new Vector3(-1.8f,.018f,0),.5f,new Color(.2f,.88f,.73f),Quaternion.Euler(90,0,0));
                float back=new[]{.22f,14.22f,27.22f,40.22f}[i];
                Label(Titles[i],new Vector3(i==0?0:i==1?0:i==2?-4:4,3.75f,back),.44f,new Color(.96f,.86f,.64f),Quaternion.Euler(0,180,0));
                Label("빛의 방향  ↑",new Vector3(new[]{-3f,6f,-5f,-5f}[i],.018f,new[]{7f,19.5f,32f,43f}[i]),.38f,new Color(.88f,.57f,.17f),Quaternion.Euler(90,0,0));
            }
            Label("책 한 권씩 선택 · 위의 책을 먼저 꺼내면 서가도 이동 가능",new Vector3(9,3.65f,17.57f),.17f,new Color(.98f,.86f,.58f),Quaternion.identity);
            Label("중간 쉼터 · 다음 책을 옮길 자리",new Vector3(6,.02f,23.0f),.23f,new Color(.19f,.70f,.58f),Quaternion.Euler(90,0,0));
            Label("중앙 쉼터 · 앞의 서가를 다시 옮겨 보세요",new Vector3(0,.02f,48),.30f,new Color(.19f,.70f,.58f),Quaternion.Euler(90,0,0));
            Box("Library exit door",new Vector3(6,1.35f,53.98f),new Vector3(3,2.7f,.045f),ink,caster:false);
            for(int s=-1;s<=1;s+=2)Box("Exit green jamb",new Vector3(6+s*1.55f,1.35f,53.9f),new Vector3(.06f,2.7f,.05f),teal,caster:false,solid:false);
            Label("EXIT / ARCHIVE",new Vector3(6,3.2f,53.88f),.48f,new Color(.4f,1,.82f),Quaternion.identity);

            var playerObject=new GameObject("Library player");playerObject.transform.SetParent(root,false);playerObject.transform.position=Checkpoints[0];
            var controller=playerObject.AddComponent<CharacterController>();FirstPersonMotor.Configure(controller,null);
            game.controller=controller;
            var cameraObject=new GameObject("Library view");cameraObject.transform.SetParent(playerObject.transform,false);cameraObject.transform.localPosition=Vector3.up*1.55f;
            game.view=cameraObject.AddComponent<Camera>();FirstPersonMotor.Configure(controller,game.view);PlayerControls.ConfigureCamera(game.view);game.view.farClipPlane=100;
            game.view.clearFlags=CameraClearFlags.SolidColor;game.view.backgroundColor=new Color(.58f,.71f,.79f);cameraObject.AddComponent<AudioListener>();
            RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white;
            return game;
        }
    }
}
