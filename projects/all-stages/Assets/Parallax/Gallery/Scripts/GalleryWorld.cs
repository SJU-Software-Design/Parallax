using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    public static class GalleryWorld
    {
        public const float Width = 28, Length = 54;
        public static readonly Vector3[] Checkpoints = { new Vector3(0,0,4.2f), new Vector3(0,0,16.5f), new Vector3(0,0,29), new Vector3(0,0,43.1f) };
        public static readonly string[] Titles = { "01  빛을 가리는 그림", "02  빛을 꺾는 거울", "03  발판 위의 그림자", "04  높은 출구로 가는 길" };
        public static readonly string[] Hints = {
            "입구 벽의 그림을 떼어 키워 보세요.\n햇빛 앞에 세우고, 그림 뒤로 돌아 그늘을 따라 건넙니다.",
            "거울을 클릭하고 Q/E로 회전해 센서를 초록색으로 만드세요.\n차광판을 들고 옮겨 반사광 아래에 그늘을 만듭니다.\n센서는 계속 빛나야 문이 열립니다. 유리는 빛을 통과시킵니다.",
            "낮은 전시대를 키워 2m 높이의 통로로 올라갑니다.\n큰 그림은 발판과 통로의 윗면까지 가려야 합니다.",
            "전시대 세 개를 계단처럼 키워 배치하세요.\n첫 그림의 그늘로 세 번째 발판 앞쪽까지 올라간 뒤,\n높은 벽의 그림을 가져와 남은 길과 출구를 가리세요."
        };
        static Transform root;
        static Material ivory, stone, navy, bronze, teal, amber, dark, ink, art;
        public static readonly List<Material> Materials = new List<Material>();
        static Material Mat(string name, Color c, bool flat = false, bool tiles = false)
        {
            var m = new Material(Shader.Find(flat ? "ShadeLink/Flat" : "Parallax/GallerySurface")) { name = name, color = c };
            if (tiles) m.SetFloat("_Grid",1); Materials.Add(m); return m;
        }
        public static GameObject Box(string name, Vector3 at, Vector3 size, Material material, Transform parent = null, bool caster = true, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent == null ? root : parent, false); go.transform.localPosition = at; go.transform.localScale = size;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            var collider = go.GetComponent<BoxCollider>();
            if (caster) go.AddComponent<GalleryCaster>().shape = collider;
            if (!solid) { if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider); }
            return go;
        }
        static void Label(string text, Vector3 p, float size, Color color, Quaternion rotation)
        {
            var go = new GameObject("Sign " + text.Replace('\n',' ')); go.transform.SetParent(root,false); go.transform.SetPositionAndRotation(p,rotation);
            var label = go.AddComponent<GalleryLabel>(); label.caption=text; label.size=size; label.color=color;
        }
        static GalleryProp Prop(string title, int zone, Vector3 position, Quaternion rotation, bool platform, float maximum)
        {
            var go = new GameObject(title); go.transform.SetParent(root,false); go.transform.SetPositionAndRotation(position,rotation);
            var p=go.AddComponent<GalleryProp>(); p.title=title; p.zone=zone; p.platform=platform; p.maxScale=maximum; return p;
        }
        static GalleryProp Painting(string title,int zone,Vector3 at,Quaternion rotation)
        {
            var p=Prop(title,zone,at,rotation,false,3.475f);p.mountedAtHome=true;
            Box("Canvas",Vector3.zero,new Vector3(2.16f,2.56f,.12f),art,p.transform);
            Box("Frame L",new Vector3(-1.14f,0,0),new Vector3(.12f,2.8f,.20f),bronze,p.transform);
            Box("Frame R",new Vector3(1.14f,0,0),new Vector3(.12f,2.8f,.20f),bronze,p.transform);
            Box("Frame top",new Vector3(0,1.34f,0),new Vector3(2.16f,.12f,.20f),bronze,p.transform);
            Box("Frame base",new Vector3(0,-1.34f,0),new Vector3(2.16f,.12f,.20f),bronze,p.transform);
            Box("Movable turquoise seal",new Vector3(.84f,-1.05f,.071f),new Vector3(.22f,.22f,.018f),teal,p.transform,false,false);
            p.Remember(); return p;
        }
        static GalleryProp Plinth(string title,int zone,Vector3 at,Vector3 size,float maximum)
        {
            var p=Prop(title,zone,at,Quaternion.identity,true,maximum);
            Box("Solid exhibit pedestal",Vector3.zero,size,stone,p.transform);
            Box("Movable turquoise seal",new Vector3(0,0,-size.z/2-.006f),new Vector3(size.x*.65f,.055f,.01f),teal,p.transform,false,false);
            p.Remember(); return p;
        }
        static GalleryProp MirrorRoom(GalleryGame game)
        {
            // A roofed chamber isolates the reflected beam from direct sunlight.
            Box("Mirror chamber roof right",new Vector3(5.5f,6.1f,21.3f),new Vector3(16.6f,.2f,10.2f),ivory);
            Box("Mirror chamber roof left",new Vector3(-10.5f,6.1f,21.3f),new Vector3(6.6f,.2f,10.2f),ivory);
            Box("Mirror chamber roof behind aperture",new Vector3(-5,6.1f,24.1f),new Vector3(4.4f,.2f,4.6f),ivory);
            var mirrorRoot=new GameObject("02 · 각도를 바꾸는 거울");mirrorRoot.transform.SetParent(root,false);
            mirrorRoot.transform.position=new Vector3(-5,4.3f,19);
            var mirror=mirrorRoot.AddComponent<GalleryMirror>();game.mirror=mirror;mirror.game=game;
            Vector3 desired=new Vector3(6,-4.3f,7).normalized;
            mirror.alignedRotation=Quaternion.LookRotation((desired+GalleryLighting.ToSun).normalized,Vector3.up);
            mirror.Restore();
            var backing=Box("Mirror silver plate",Vector3.zero,new Vector3(3.2f,3.2f,.06f),dark,mirrorRoot.transform);
            backing.GetComponent<Renderer>().enabled=false;
            var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="Reflective mirror face";face.transform.SetParent(mirrorRoot.transform,false);
            face.transform.localPosition=new Vector3(0,0,.034f);face.transform.localScale=new Vector3(3.2f,3.2f,1);face.layer=31;
            Object.DestroyImmediate(face.GetComponent<Collider>());
            var mirrorMaterial=new Material(Shader.Find("Parallax/GalleryMirror")){name="Museum silver mirror"};Materials.Add(mirrorMaterial);
            mirror.face=face.GetComponent<Renderer>();mirror.face.sharedMaterial=mirrorMaterial;
            for(int side=-1;side<=1;side+=2)
            {
                Box("Mirror bronze edge",new Vector3(side*1.67f,0,0),new Vector3(.14f,3.48f,.16f),bronze,mirrorRoot.transform);
                Box("Mirror bronze edge",new Vector3(0,side*1.67f,0),new Vector3(3.2f,.14f,.16f),bronze,mirrorRoot.transform);
            }
            Box("Mirror pedestal",new Vector3(-5,.22f,19),new Vector3(2,.44f,1.5f),stone);
            Box("Mirror support",new Vector3(-5,2.28f,19),new Vector3(.16f,3.68f,.16f),bronze);
            Label("MIRROR  /  Q · E",new Vector3(-5,1.05f,17.95f),.32f,new Color(.4f,1,.87f),Quaternion.identity);
            var glass=new Material(Shader.Find("Parallax/GalleryGlass")){name="Light transmitting safety glass"};Materials.Add(glass);
            for(int side=-1;side<=1;side+=2)
            {
                var wall=Box("Transparent beam corridor",new Vector3(side*.94f,1.75f,23.4f),new Vector3(.08f,3.5f,6.8f),glass,caster:false);
                wall.AddComponent<GalleryLightGlass>();
                var end=Box("Transparent gate surround",new Vector3(side*1.64f,1.75f,26.8f),new Vector3(1.4f,3.5f,.09f),glass,caster:false);
                end.AddComponent<GalleryLightGlass>();
                foreach(float y in new[]{.03f,3.52f})Box("Glass corridor edge",new Vector3(side*.94f,y,23.4f),new Vector3(.04f,.035f,6.8f),teal,caster:false,solid:false);
            }
            var gateObject=new GameObject("02 · 반사광으로 여는 문");gateObject.transform.SetParent(root,false);
            var gate=gateObject.AddComponent<GalleryLightGate>();game.lightGate=gate;
            var leaf=Box("Light controlled gate",new Vector3(0,1.75f,26.8f),new Vector3(1.8f,3.5f,.12f),navy,gateObject.transform);
            gate.panel=leaf.GetComponent<BoxCollider>();gate.closedPosition=leaf.transform.position;
            var sensor=Box("Reflected light receiver",new Vector3(1.4f,.45f,26.1f),new Vector3(.16f,.16f,.06f),amber,gateObject.transform,caster:false,solid:false);
            gate.sensor=sensor.transform;gate.sensorFace=sensor.GetComponent<Renderer>();
            Box("Receiver bronze post",new Vector3(1.4f,.18f,26.1f),new Vector3(.08f,.36f,.08f),bronze,caster:false);
            Label("LIGHT → OPEN",new Vector3(1.5f,1.05f,26.65f),.25f,new Color(.4f,1,.87f),Quaternion.identity);
            Label("02  /  BEND THE LIGHT",new Vector3(0,4.3f,26.65f),.38f,new Color(.93f,.84f,.63f),Quaternion.identity);
            var shield=Prop("02 · 반사광 차광판",1,new Vector3(4.8f,1.4f,18),Quaternion.identity,false,2.05f);
            Box("Shield panel",Vector3.zero,new Vector3(2.4f,2.6f,.14f),ink,shield.transform);
            Box("Shield base",new Vector3(0,-1.32f,0),new Vector3(1.45f,.16f,.85f),stone,shield.transform);
            Box("Movable turquoise seal",new Vector3(.9f,-.85f,-.08f),new Vector3(.22f,.22f,.018f),teal,shield.transform,false,false);
            shield.Remember();return shield;
        }
        public static GalleryGame Build(GameObject originalGallery = null)
        {
            Materials.Clear();
            root = new GameObject("PARALLAX / Gallery — 28 × 54 m").transform;
            ivory=Mat("Gallery plaster",new Color(.91f,.87f,.78f));
            stone=Mat("Travertine exhibits",new Color(.81f,.75f,.62f));
            navy=Mat("Deep blue partitions",new Color(.28f,.43f,.48f));
            bronze=Mat("Bronze frames",new Color(.62f,.38f,.17f));
            dark=Mat("Charcoal",new Color(.18f,.23f,.25f));
            art=Mat("Canvas print",new Color(.85f,.50f,.29f));
            teal=Mat("Interaction enamel",new Color(.21f,.91f,.77f),true);
            amber=Mat("Sunlight guide",new Color(1,.77f,.38f),true);
            ink=Mat("Exhibit graphics",new Color(.21f,.39f,.54f));
            var floor=Mat("Limestone tile",new Color(.88f,.83f,.72f),false,true);
            var game = root.gameObject.AddComponent<GalleryGame>();
            game.lighting=root.gameObject.AddComponent<GalleryLighting>();
            Box("Walkable floor 1512 square metres",new Vector3(0,-.2f,27),new Vector3(28,.4f,54),floor, caster:false);
            Box("Entrance wall",new Vector3(0,4,-.18f),new Vector3(28.4f,8,.36f),ivory);
            Box("Exit facade",new Vector3(0,5,54.18f),new Vector3(28.4f,10,.36f),ivory);
            for(int s=-1;s<=1;s+=2)
                Box("Side wall",new Vector3(s*14.18f,5,27),new Vector3(.36f,10,54),ivory);
            for(int s=-1;s<=1;s+=2)
                Box("First exhibition corridor",new Vector3(s*5,2.25f,10.35f),new Vector3(.22f,4.5f,7.3f),ivory);
            // Physical skylight openings coincide with the analytic aperture masks.
            float cursor=0;
            foreach(var aperture in GalleryLighting.Windows)
            {
                if(aperture.z>cursor) Box("Solid roof",new Vector3(0,10.15f,(cursor+aperture.z)/2),new Vector3(28,.3f,aperture.z-cursor),ivory,caster:false);
                for(int s=-1;s<=1;s+=2)
                    Box("Skylight edge",new Vector3(s*13.8f,10.02f,(aperture.z+aperture.w)/2),new Vector3(.08f,.07f,aperture.w-aperture.z),amber,caster:false,solid:false);
                cursor=aperture.w;
            }
            if(cursor<54)Box("Solid roof",new Vector3(0,10.15f,(cursor+54)/2),new Vector3(28,.3f,54-cursor),ivory,caster:false);
            foreach(float z in new[]{14f,27f,40f})
            {
                for(int s=-1;s<=1;s+=2)Box("Partition",new Vector3(s*8.2f,2.75f,z),new Vector3(11.6f,5.5f,.38f),navy);
                float lower=z==40?4.65f:3.5f;
                Box("Door lintel",new Vector3(0,(lower+5.5f)/2,z),new Vector3(4.8f,5.5f-lower,.38f),navy);
            }
            Box("Raised threshold — climb required",new Vector3(0,1,40),new Vector3(4.8f,2,.38f),stone);
            Box("Upper gallery walkway",new Vector3(0,1.86f,38.4f),new Vector3(4,.28f,4.8f),stone);
            for(int i=0;i<3;i++)Box("Return stairs",new Vector3(0,(1.5f-i*.5f)/2,41.2f+i*.55f),new Vector3(3,1.5f-i*.5f,.55f),stone);
            Box("High exit balcony",new Vector3(0,4.05f,52.55f),new Vector3(7.5f,.3f,2.9f),stone);
            for(int s=-1;s<=1;s+=2)
            {
                Box("Balcony support",new Vector3(s*3.45f,1.95f,53),new Vector3(.28f,3.9f,1.7f),navy);
                Box("Exit jamb",new Vector3(s*1.55f,5.45f,53.82f),new Vector3(.09f,2.5f,.08f),teal,caster:false,solid:false);
            }
            Box("Exit door",new Vector3(0,5.45f,53.99f),new Vector3(3,2.5f,.045f),dark,caster:false);
            Label("EXIT  /  4.2 m",new Vector3(0,6.9f,53.85f),.72f,new Color(.36f,1,.84f),Quaternion.identity);
            var props=new List<GalleryProp>();
            props.Add(Painting("01 · 이동하는 그림",0,new Vector3(0,2.3f,.27f),Quaternion.identity));
            var sculpture=Prop("02 · 열린 조각",1,new Vector3(3,1.5f,17.8f),Quaternion.Euler(0,90,0),false,2.8f);
            Box("Sculpture left upright",new Vector3(-.72f,-.08f,0),new Vector3(.40f,2.84f,.50f),bronze,sculpture.transform);
            Box("Sculpture right upright",new Vector3(.72f,-.08f,0),new Vector3(.40f,2.84f,.50f),bronze,sculpture.transform);
            Box("Sculpture lintel",new Vector3(0,1.24f,0),new Vector3(2.5f,.52f,.55f),bronze,sculpture.transform);
            Box("Sculpture foot",new Vector3(0,-1.36f,0),new Vector3(2.5f,.28f,.7f),stone,sculpture.transform);
            sculpture.Remember(); props.Add(sculpture);
            props.Add(Painting("03 · 높은 그늘",2,new Vector3(-6,2.2f,27.27f),Quaternion.identity));
            props.Add(Plinth("03 · 낮은 전시대",2,new Vector3(3,.225f,30),new Vector3(1.25f,.45f,1.25f),3.2f));
            props.Add(Painting("04 · 첫 번째 차광 그림",3,new Vector3(-6,2.3f,40.27f),Quaternion.identity));
            props.Add(Plinth("04 · 발판 A",3,new Vector3(-7,.45f,43),new Vector3(1.2f,.9f,1.2f),3.6f));
            props.Add(Plinth("04 · 발판 B",3,new Vector3(-9,.45f,44.4f),new Vector3(1.2f,.9f,.9f),3.6f));
            props.Add(Plinth("04 · 발판 C",3,new Vector3(8,.45f,43),new Vector3(2.4f,.9f,1.2f),3.6f));
            // A high display reached from the intermediate steps creates an order of operations.
            Box("High display wall",new Vector3(9.35f,4.3f,49.1f),new Vector3(.3f,8.6f,5.5f),navy);
            props.Add(Painting("04 · 마지막 차광 그림",3,new Vector3(9.07f,6.2f,49.1f),Quaternion.Euler(0,90,0)));
            props.Add(MirrorRoom(game));
            game.props=props.ToArray();
            for(int i=0;i<4;i++)
            {
                float z=new[]{3.8f,16.5f,29f,42.7f}[i];
                Box("Checkpoint brass inlay",new Vector3(0,.006f,z),new Vector3(3,.012f,.035f),teal,caster:false,solid:false);
                Label(Titles[i],new Vector3(i==0?0:-7,3.8f,new[]{.23f,13.77f,26.77f,39.77f}[i]),.45f,new Color(.87f,.89f,.82f),Quaternion.Euler(0,i==0?180:0,0));
                Label("0"+(i+1),new Vector3(-2,.013f,z),.52f,new Color(.22f,.87f,.75f),Quaternion.Euler(90,0,0));
            }
            // Side exhibits stay in roofed areas, preserving a readable puzzle route.
            for(int i=0;i<4;i++)
            {
                float z=new[]{4.3f,17f,29.4f,42.5f}[i];
                Box("Visitor bench seat",new Vector3(10,.58f,z),new Vector3(3.6f,.22f,1),stone);
                for(int s=-1;s<=1;s+=2)Box("Visitor bench leg",new Vector3(10+s*1.2f,.235f,z),new Vector3(.25f,.47f,.8f),bronze);
                for(int j=0;j<3;j++)
                {
                    var frame=Box("Permanent gallery frame",new Vector3(13.95f,2.6f,z-1.5f+j*1.6f),new Vector3(.05f,1.45f,1.12f),bronze,caster:false,solid:false);
                    Box("Permanent print",new Vector3(13.916f,2.6f,z-1.5f+j*1.6f),new Vector3(.01f,1.25f,.92f),j%2==0?art:ink,caster:false,solid:false);
                }
                Label("SUNLIGHT  ↑",new Vector3(5,.016f,z+2),.5f,new Color(1,.76f,.4f),Quaternion.Euler(90,0,0));
            }
            // Original models are prepared as reusable art/architecture by GalleryBuild.
            if(originalGallery!=null)
            {
                var imported=Object.Instantiate(originalGallery,root); imported.name="ALLrounder18 — original gallery asset";
                imported.transform.localPosition=new Vector3(0,0,0);
            }
            var playerObject=new GameObject("Gallery player"); playerObject.transform.SetParent(root,false);
            playerObject.transform.position=Checkpoints[0];
            var controller=playerObject.AddComponent<CharacterController>();FirstPersonMotor.Configure(controller,null);
            game.controller=controller;
            var cameraObject=new GameObject("Gallery view");cameraObject.transform.SetParent(playerObject.transform,false);cameraObject.transform.localPosition=Vector3.up*1.55f;
            game.view=cameraObject.AddComponent<Camera>();FirstPersonMotor.Configure(controller,game.view);PlayerControls.ConfigureCamera(game.view);game.view.farClipPlane=100;
            game.view.clearFlags=CameraClearFlags.SolidColor;game.view.backgroundColor=new Color(.64f,.79f,.86f);
            cameraObject.AddComponent<AudioListener>();
            RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white;
            return game;
        }
    }
}
