using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Starship
{
    public static class StarshipWorld
    {
        public const int PhysicalMask=~(1<<StarshipLighting.ShadowLayer);
        public const float WaterBedY=-2f,PropMinimumY=-2.03f;
        public static readonly Vector3[] Centers={Vector3.zero,new Vector3(0,0,-42),new Vector3(42,0,0),new Vector3(0,0,42),new Vector3(-42,0,0)};
        public static readonly float[] Yaws={180,90,0,270,180};
        public static Vector3 Point(int room,Vector3 local)=>Centers[room]+Quaternion.Euler(0,Yaws[room],0)*local;
        public static Vector3 Direction(int room,Vector3 local)=>Quaternion.Euler(0,Yaws[room],0)*local;
        public static readonly Vector3[] Checkpoints={Point(0,new Vector3(0,0,-11)),Point(1,new Vector3(0,0,-11)),Point(2,new Vector3(0,0,-11)),Point(3,new Vector3(0,0,-11)),Point(4,new Vector3(0,0,-11))};
        public static readonly Vector3 CastleRest=Point(3,new Vector3(5,2.55f,10.5f));
        public static Bounds ZoneBounds(int room)=>new Bounds(Centers[room]+Vector3.up*4,new Vector3(27.6f,12,27.6f));
        public static bool InZone(Bounds bounds,int room)
        {
            Bounds zone=ZoneBounds(room);
            return bounds.min.x>=zone.min.x&&bounds.max.x<=zone.max.x&&bounds.min.z>=zone.min.z&&bounds.max.z<=zone.max.z&&bounds.min.y>=PropMinimumY&&bounds.max.y<=zone.max.y;
        }
        public static int RoomAt(Vector3 p)
        {
            for(int i=4;i>=0;i--){Vector3 d=p-Centers[i];if(Mathf.Abs(d.x)<13.5f&&Mathf.Abs(d.z)<13.5f)return i;}
            return -1;
        }
        public static readonly string[] Titles={"01 / 화물 실험실","02 / 태양광 정렬실","03 / 장비 보관실","04 / 냉각·상층 정비실","05 / 남겨진 표지"};
        public static readonly string[] Hints={
            "큰 화물은 그림자와 발판이 됩니다. 높은 출입구까지 이어지는 두 역할을 함께 생각하세요.",
            "두 수신기는 동시에 빛을 받아야 합니다. 유리 너머의 빛과 사람의 이동 경로는 다릅니다.",
            "보관 랙 위의 장비를 먼저 꺼내세요. 사용한 랙과 탁자를 다음 쉼터에서 다시 회수할 수 있습니다.",
            "수로를 잇는 발판, 빛을 막는 차광판, 물살로 보내는 캡슐이 각각 필요합니다. 상층에서도 같은 장비를 재사용하세요.",
            "이 방에는 표지 하나만 남아 있습니다. 지금까지 사용했던 조작을 떠올려 보세요."
        };
        public static readonly string[] DetailedHints={
            "차광판을 먼 바닥에 크게 놓고 안착을 기다리세요. 낮은 장비와 큰 화물 상자를 계단처럼 배치하면 2.6m 출입구에 올라갈 수 있습니다.",
            "반사판을 확대하고 Q/E·Z/X로 두 수신기를 동시에 비추세요. 내려놓은 반사판이 안착한 뒤 두 빛을 0.8초 맞추면 문이 열립니다. 유리 왼쪽 통로에 차광판을 다시 놓고 돌아가세요.",
            "랙 위 모듈을 먼저 꺼낸 뒤 랙을 확대해 중간 쉼터로 그늘을 연결하세요. 탁자는 다리 사이로 지나갈 만큼 키우고, 모듈로 짧은 빈 그늘·높이 차를 메우세요.",
            "첫 수로는 판자를 확대해 걸치고 그 위를 차광하세요. 두 번째 수로에는 캡슐을 놓아 6.5m 이상 떠내려 보내면 옆길이 열립니다. 판자를 회수해 높은 턱을 잇고 쉼터에서 다시 배치하세요.",
            "EXIT를 잡고 먼 바닥을 바라보세요. 표지가 커지면 작은 내부 통로도 함께 커집니다. 내려놓고 안착시킨 뒤 그 안으로 들어가세요."
        };
        public static readonly List<Material> Materials=new List<Material>();
        static Transform root;
        static Func<string,GameObject> assets;
        static Material hull,floor,white,ink,cyan,amber,glass,coolant,black;
        static StarshipGame game;
        static readonly List<StarshipProp> props=new List<StarshipProp>();
        public static Material Mat(string name,Color c,bool flat=false)
        {
            var m=new Material(Shader.Find(flat?"ShadeLink/Flat":"Parallax/StarshipSurface")){name=name,color=c};
            if(m.HasProperty("_Glossiness")){m.SetFloat("_Glossiness",.30f);m.SetFloat("_Metallic",.30f);m.EnableKeyword("_EMISSION");}
            Materials.Add(m);return m;
        }
        public static GameObject Box(string name,Vector3 at,Vector3 size,Material mat,Transform parent=null,bool solid=true,bool shadow=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent==null?root:parent,false);
            go.transform.localPosition=at;go.transform.localScale=size;
            var r=go.GetComponent<Renderer>();r.sharedMaterial=mat;r.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;
            if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static GameObject RBox(int room,string name,Vector3 at,Vector3 size,Material mat,bool solid=true,bool shadow=true)
        {
            var go=Box(name,Point(room,at),size,mat,solid:solid,shadow:shadow);go.transform.rotation=Quaternion.Euler(0,Yaws[room],0);return go;
        }
        static Bounds VisualBounds(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>();Bounds b=rs[0].bounds;for(int i=1;i<rs.Length;i++)b.Encapsulate(rs[i].bounds);return b;
        }
        static GameObject Model(string key,Transform parent,Vector3 at,Vector3 size,bool shadow=true)
        {
            var go=UnityEngine.Object.Instantiate(assets(key));go.name="Kenney / "+key;go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
            Bounds b=VisualBounds(go);
            go.transform.localScale=new Vector3(size.x/Mathf.Max(.001f,b.size.x),size.y/Mathf.Max(.001f,b.size.y),size.z/Mathf.Max(.001f,b.size.z));
            b=VisualBounds(go);go.transform.localPosition=at-new Vector3(b.center.x-parent.position.x,b.min.y-parent.position.y,b.center.z-parent.position.z);
            foreach(var r in go.GetComponentsInChildren<Renderer>())r.shadowCastingMode=shadow?ShadowCastingMode.TwoSided:ShadowCastingMode.Off;
            return go;
        }
        static GameObject RModel(int room,string key,Vector3 at,Vector3 size,bool shadow=true)
        {
            var anchor=new GameObject("Module "+key).transform;anchor.SetParent(root,false);anchor.position=Point(room,at);
            Model(key,anchor,Vector3.zero,size,shadow);anchor.rotation=Quaternion.Euler(0,Yaws[room],0);return anchor.gameObject;
        }
        static void Label(string caption,Vector3 at,Quaternion rotation,float size=.28f,Transform parent=null,Color? color=null)
        {
            var go=new GameObject("Facility lettering");go.transform.SetParent(parent==null?root:parent,false);go.transform.localPosition=at;go.transform.localRotation=rotation;
            var l=go.AddComponent<StarshipLabel>();l.caption=caption;l.size=size;l.color=color??new Color(.56f,.90f,1);
        }
        static void RLabel(int room,string caption,Vector3 at,float size=.28f)
        {Label(caption,Point(room,at),Quaternion.Euler(0,Yaws[room],0),size);}
        static void Floor(int room,float near,float far,float left=-14,float right=14)
        {
            RBox(room,"Pressure deck",new Vector3((left+right)/2,-.28f,(near+far)/2),new Vector3(right-left,.56f,far-near),floor);
            for(float x=left+1.75f;x<right-.1f;x+=3.5f)for(float z=near+1.75f;z<far-.1f;z+=3.5f)
                if(x+1.7f<right&&z+1.7f<far)RModel(room,"floor-panel",new Vector3(x,-.04f,z),new Vector3(3.43f,.045f,3.43f),false);
        }
        static void Cover(int room,Vector3 feet,float width=5.5f,float depth=4.5f,float roofHeight=3.2f)
        {
            // The roof is offset toward the fixed world-space sun to shade the saved feet.
            Vector3 sunLocal=Quaternion.Inverse(Quaternion.Euler(0,Yaws[room],0))*StarshipLighting.ToSun;
            Vector3 roof=feet+new Vector3(sunLocal.x/sunLocal.y*roofHeight,roofHeight,sunLocal.z/sunLocal.y*roofHeight);
            RBox(room,"Saved refuge roof",roof,new Vector3(width,.14f,depth),white);
            RBox(room,"Saved refuge marker",feet+Vector3.up*.012f,new Vector3(2,.024f,1.7f),cyan,false,false);
        }
        static void Walls(int room)
        {
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<10;i++)
                {
                    Vector3 p=new Vector3(side*14.1f,0,-12.6f+i*2.8f);
                    var wall=RModel(room,i%3==1?"wall-window":"wall",p,new Vector3(2.8f,2.8f,.3f));
                    wall.transform.rotation=Quaternion.Euler(0,Yaws[room]+90,0);
                    var upper=RModel(room,"wall-window-frame",p+Vector3.up*2.8f,new Vector3(2.8f,2.8f,.3f));
                    upper.transform.rotation=Quaternion.Euler(0,Yaws[room]+90,0);
                }
                RBox(room,"Transparent pressure boundary",new Vector3(side*14.2f,7,0),new Vector3(.3f,16,28.7f),glass,true,false);
                foreach(int end in new[]{-1,1})
                {
                    RBox(room,"Bulkhead side",new Vector3(side*8.4f,2.8f,end*14.1f),new Vector3(11.8f,5.6f,.35f),hull);
                    RBox(room,"Bulkhead pressure boundary",new Vector3(side*8.4f,9,end*14.1f),new Vector3(11.8f,10,.35f),glass,true,false);
                }
            }
            foreach(int end in new[]{-1,1})
            {
                if(room==4&&end==1)
                {
                    RBox(room,"Empty rear bulkhead",new Vector3(0,2.8f,14.1f),new Vector3(5.2f,5.6f,.4f),hull);
                    RBox(room,"Rear pressure boundary",new Vector3(0,10,14.1f),new Vector3(5.2f,12,.3f),glass,true,false);
                    continue;
                }
                float y=room==0&&end==1?2.6f:room==3&&end==1?4.55f:0;
                RModel(room,"door-double",new Vector3(0,y,end*14.1f),new Vector3(5.05f,4.2f,.38f));
                RBox(room,"Port overhead boundary",new Vector3(0,y+10,end*14.1f),new Vector3(5,12,.3f),glass,true,false);
                if(y>0)RBox(room,"Raised port base",new Vector3(0,y*.5f,end*14.1f),new Vector3(5,y,.4f),hull);
            }
            RBox(room,"Hull underfloor",new Vector3(0,-3.2f,0),new Vector3(29.8f,2.2f,29.8f),hull);
            // Transparent collector glazing lets the same external star illuminate every room.
            RBox(room,"Solar collector glazing",new Vector3(0,10.5f,0),new Vector3(28,.1f,28),glass,true,false);
            foreach(float x in new[]{-14f,14f})RBox(room,"Collector rim",new Vector3(x,5.7f,0),new Vector3(.22f,.22f,28.6f),amber);
            if(room!=4)RLabel(room,Titles[room].Substring(0,2)+"  /  SOLAR LAB",new Vector3(0,3.5f,13.88f),.22f);
        }
        static void Collision(StarshipProp p,string name,Vector3 center,Vector3 size)
        {var go=new GameObject(name);go.transform.SetParent(p.transform,false);var c=go.AddComponent<BoxCollider>();c.center=center;c.size=size;}
        static StarshipProp Prop(string type,string title,int room,Vector3 at,float initial=1,float max=4)
        {
            var go=new GameObject(title);go.transform.SetParent(root,false);go.transform.position=Point(room,at);
            var p=go.AddComponent<StarshipProp>();p.title=title;p.zone=room;p.minScale=.30f;p.maxScale=max;
            p.buoyant=type=="plank"||type=="capsule";p.upright=type=="canopy"||type=="rack"||type=="exit"||type=="capsule";
            if(type=="cargo"||type=="module"||type=="capsule")
            {
                Vector3 size=type=="cargo"?new Vector3(1.15f,.75f,1.15f):type=="module"?new Vector3(.55f,.36f,.62f):new Vector3(.68f,.68f,.68f);
                Model(type=="cargo"?"container-wide":type=="module"?"container-flat":"container",p.transform,Vector3.zero,size);
                Collision(p,"Cargo body",new Vector3(0,size.y*.5f,0),size);
            }
            else if(type=="plank")
            {
                Model("floor-panel-straight",p.transform,Vector3.zero,new Vector3(1.15f,.16f,2.35f));
                Collision(p,"Maintenance panel",new Vector3(0,.08f,0),new Vector3(1.15f,.16f,2.35f));p.platform=true;
            }
            else if(type=="canopy")
            {
                Model("structure-panel",p.transform,new Vector3(0,2.36f,0),new Vector3(2.6f,.16f,2.2f));
                Box("Shade mast",new Vector3(0,1.2f,0),new Vector3(.1f,2.4f,.1f),hull,p.transform);
                Box("Shade base",new Vector3(0,.04f,0),new Vector3(.58f,.08f,.58f),amber,p.transform);
                Collision(p,"Collector shield",new Vector3(0,2.44f,0),new Vector3(2.6f,.16f,2.2f));
                // Primitive mast and base already supply their exact colliders.
            }
            else if(type=="rack")
            {
                // Open compound frame: modules remain separate interactable rigid bodies.
                Box("Rack back",new Vector3(0,1.05f,.38f),new Vector3(2.7f,2.1f,.14f),hull,p.transform);
                foreach(float x in new[]{-1.27f,1.27f})Box("Rack upright",new Vector3(x,1.08f,0),new Vector3(.16f,2.16f,.86f),white,p.transform);
                foreach(float y in new[]{.10f,1.10f,2.16f})Box("Rack shelf",new Vector3(0,y,0),new Vector3(2.7f,.12f,.86f),white,p.transform);
                Model("computer-system",p.transform,new Vector3(0,2.22f,.05f),new Vector3(.65f,.3f,.4f),false);
            }
            else if(type=="table")
            {
                Model("table-large",p.transform,new Vector3(0,.82f,0),new Vector3(2.6f,.16f,1.75f));
                Collision(p,"Table top",new Vector3(0,.90f,0),new Vector3(2.6f,.16f,1.75f));
                foreach(float x in new[]{-1.1f,1.1f})Box("Table support",new Vector3(x,.42f,0),new Vector3(.20f,.84f,1.5f),white,p.transform);
            }
            else if(type=="exit")
            {
                // Actual hollow doorway. Enlarging the sign enlarges its interior opening.
                Box("EXIT left",new Vector3(-.36f,.32f,0),new Vector3(.48f,.64f,.34f),white,p.transform);
                Box("EXIT right",new Vector3(.36f,.32f,0),new Vector3(.48f,.64f,.34f),white,p.transform);
                Box("EXIT header",new Vector3(0,.51f,0),new Vector3(.24f,.26f,.34f),white,p.transform);
                Box("EXIT threshold",new Vector3(0,.007f,0),new Vector3(.24f,.014f,.34f),white,p.transform);
                Box("Small dark interior",new Vector3(0,.195f,.19f),new Vector3(.23f,.36f,.015f),black,p.transform,false,false);
                Label("EXIT",new Vector3(0,.52f,-.177f),Quaternion.identity,.42f,p.transform,new Color(.06f,.85f,.57f));
            }
            go.transform.rotation=Quaternion.Euler(0,Yaws[room],0);go.transform.localScale=Vector3.one*initial;p.Remember();props.Add(p);return p;
        }
        static StarshipLightGate Gate(int room,float z,bool water)
        {
            var obj=new GameObject(water?"Transport relay":"Optical relay");obj.transform.SetParent(root,false);
            var g=obj.AddComponent<StarshipLightGate>();g.game=game;g.waterGate=water;
            var panel=RBox(room,"Locked bulkhead",new Vector3(0,water?6.55f:2,z),new Vector3(4.8f,4,.25f),ink);g.panel=panel.GetComponent<BoxCollider>();g.closedPosition=panel.transform.position;
            g.sensors=new Transform[water?0:2];g.sensorFaces=new Renderer[g.sensors.Length];
            for(int i=0;i<g.sensors.Length;i++)
            {
                var s=RBox(room,"Optical receiver "+i,new Vector3(i==0?-1.45f:1.45f,.9f,z-.35f),new Vector3(.28f,.28f,.08f),amber,false,false);
                g.sensors[i]=s.transform;g.sensorFaces[i]=s.GetComponent<Renderer>();
            }
            return g;
        }
        static void Water(int room,string name,float near,float far,float left,float right,bool transport)
        {
            Vector3 a=Point(room,new Vector3(left,0,near)),b=Point(room,new Vector3(right,0,far));
            var go=RBox(room,name,new Vector3((left+right)*.5f,-.065f,(near+far)*.5f),new Vector3(right-left,.06f,far-near),coolant,false,false);
            var w=go.AddComponent<StarshipWater>();w.min=new Vector2(Mathf.Min(a.x,b.x),Mathf.Min(a.z,b.z));w.max=new Vector2(Mathf.Max(a.x,b.x),Mathf.Max(a.z,b.z));
            w.surface=-.04f;w.flow=Direction(room,new Vector3(0,0,transport?1.35f:.18f));w.transportChannel=transport;w.transportOrigin=Point(room,new Vector3(0,0,near));
            var m=new Material(coolant){name=name+" stream"};m.SetVector("_Flow",new Vector4(w.flow.x,w.flow.z,w.flow.magnitude,0));Materials.Add(m);go.GetComponent<Renderer>().sharedMaterial=m;
            RBox(room,"Coolant channel bed",new Vector3((left+right)*.5f,-2.15f,(near+far)*.5f),new Vector3(right-left,.3f,far-near),ink,true,false);
            if(transport)for(float z=near+.7f;z<far-.5f;z+=1.8f)
            {
                foreach(float x in new[]{-8f,-3f,3f})
                {
                    RBox(room,"Current direction",new Vector3(x,-.012f,z),new Vector3(.05f,.016f,.7f),cyan,false,false);
                    foreach(int side in new[]{-1,1})
                    {
                        var arrow=RBox(room,"Current arrowhead",new Vector3(x+side*.12f,-.012f,z+.25f),new Vector3(.045f,.016f,.34f),cyan,false,false);
                        arrow.transform.rotation=Quaternion.Euler(0,Yaws[room]+side*45,0);
                    }
                }
            }
        }
        static void Corridor(Vector3 a,Vector3 b)
        {
            Vector3 d=b-a;float length=d.magnitude;if(length<.01f)return;
            Quaternion rotation=Quaternion.LookRotation(d);
            var c=new GameObject("Pressurised transfer corridor").transform;c.SetParent(root,false);c.position=(a+b)*.5f;c.rotation=rotation;
            Box("Corridor deck",new Vector3(0,-.3f,0),new Vector3(5,.6f,length+2),floor,c);
            // The same hall must clear the 4.55m maintenance exit as well as
            // ground-level ports; its overhang reaches into the room.
            Box("Corridor cover",new Vector3(0,7.5f,0),new Vector3(8,.2f,length+8),white,c);
            foreach(int side in new[]{-1,1})Box("Corridor wall",new Vector3(side*2.6f,3.7f,0),new Vector3(.2f,7.4f,Mathf.Max(.3f,length-5.4f)),hull,c);
            Box("Wayfinding strip",new Vector3(.9f,.015f,0),new Vector3(.045f,.025f,length),cyan,c,false,false);
        }
        static void Connections()
        {
            Corridor(new Vector3(0,0,-14),new Vector3(0,0,-23));
            Corridor(new Vector3(0,0,-23),new Vector3(-20,0,-23));
            Corridor(new Vector3(-20,0,-23),new Vector3(-20,0,-42));
            Corridor(new Vector3(-20,0,-42),new Vector3(-14,0,-42));
            Corridor(new Vector3(14,0,-42),new Vector3(42,0,-42));
            Corridor(new Vector3(42,0,-42),new Vector3(42,0,-14));
            Corridor(new Vector3(42,0,14),new Vector3(42,0,42));
            Corridor(new Vector3(42,0,42),new Vector3(14,0,42));
            Corridor(new Vector3(-14,0,42),new Vector3(-42,0,42));
            Corridor(new Vector3(-42,0,42),new Vector3(-42,0,14));
        }
        static void OpticalRoom()
        {
            var p=new GameObject("02 / Solar reflector").AddComponent<StarshipProp>();p.transform.SetParent(root,false);p.transform.position=Point(1,new Vector3(-3,.025f,-6.8f));
            p.title="소형 태양광 반사판";p.zone=1;p.minScale=.3f;p.maxScale=3.8f;p.upright=true;
            Box("Reflector base",new Vector3(0,.06f,0),new Vector3(1.25f,.12f,.90f),ink,p.transform);
            Box("Reflector mast",new Vector3(0,.65f,0),new Vector3(.12f,1.3f,.12f),white,p.transform);
            var head=new GameObject("Optical surface").transform;head.SetParent(p.transform,false);head.localPosition=new Vector3(0,1.45f,0);
            Vector3 target=Point(1,new Vector3(0,.9f,13.35f));
            head.rotation=Quaternion.LookRotation(((target-head.position).normalized+StarshipLighting.ToSun).normalized);
            var mirror=head.gameObject.AddComponent<StarshipMirror>();mirror.game=game;mirror.prop=p;mirror.halfSize=new Vector2(.7f,.9f);
            Box("Reflector housing",Vector3.zero,new Vector3(1.5f,1.9f,.1f),amber,head);
            var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="Reflective face";face.transform.SetParent(head,false);face.transform.localPosition=new Vector3(0,0,.057f);face.transform.localScale=new Vector3(1.4f,1.8f,1);UnityEngine.Object.DestroyImmediate(face.GetComponent<Collider>());face.layer=31;
            var m=new Material(Shader.Find("Parallax/GalleryMirror")){name="Starship reflector"};Materials.Add(m);mirror.face=face.GetComponent<Renderer>();mirror.face.sharedMaterial=m;mirror.face.shadowCastingMode=ShadowCastingMode.Off;
            p.transform.localScale=Vector3.one*.55f;p.Remember();props.Add(p);game.mirror=mirror;game.lighting.mirror=mirror;
            game.lightGate=Gate(1,13.65f,false);
            RBox(1,"Optical glass left",new Vector3(-11.7f,2.5f,6),new Vector3(4.6f,5,.14f),glass,true,false);
            RBox(1,"Optical glass right",new Vector3(4.6f,2.5f,6),new Vector3(18.8f,5,.14f),glass,true,false);
            Prop("canopy","02 / 광선 차광판",1,new Vector3(-6,.025f,-7),.8f,3.4f);
            Prop("module","02 / 차광 보조 모듈",1,new Vector3(4,.025f,-8),1,5);
            Cover(1,new Vector3(-7,0,-2.8f),2.8f,2.8f);
            Cover(1,new Vector3(-7,0,9.5f),4,4);
        }
        static void Exterior()
        {
            foreach(int room in new[]{0,1,2,3})
            {
                foreach(int side in new[]{-1,1})
                {
                    var engine=RModel(room,"pipe-ring-colored",new Vector3(side*15.8f,-1.7f,-8),new Vector3(2.4f,2.4f,2.4f),false);
                    engine.transform.rotation=Quaternion.Euler(0,Yaws[room]+90,90);
                    RBox(room,"Exterior heat fin",new Vector3(side*16.2f,-.6f,0),new Vector3(2.4f,.2f,12),ink,true,false);
                }
            }
            var planet=GameObject.CreatePrimitive(PrimitiveType.Sphere);planet.name="Distant blue planet";planet.transform.SetParent(root,false);planet.transform.position=new Vector3(170,80,170);planet.transform.localScale=Vector3.one*80;
            UnityEngine.Object.DestroyImmediate(planet.GetComponent<Collider>());
            var pm=new Material(Shader.Find("Parallax/StarshipPlanet")){name="Distant planet"};Materials.Add(pm);planet.GetComponent<Renderer>().sharedMaterial=pm;planet.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        public static StarshipGame Build(Func<string,GameObject> source)
        {
            assets=source;Materials.Clear();props.Clear();root=new GameObject("PARALLAX / HELIOS EXPERIMENTAL STARSHIP").transform;
            hull=Mat("Pearl pressure hull",new Color(.68f,.74f,.82f));floor=Mat("Graphite deck",new Color(.13f,.19f,.26f));white=Mat("Ceramic equipment",new Color(.86f,.91f,.97f));ink=Mat("Dark machinery",new Color(.045f,.08f,.12f));cyan=Mat("Cyan navigation",new Color(.04f,.65f,.82f),true);amber=Mat("Collector amber",new Color(1,.54f,.10f));black=Mat("Interior black",Color.black,true);
            glass=new Material(Shader.Find("Parallax/StarshipGlass")){name="Pressure glazing",color=new Color(.04f,.16f,.26f,.035f)};Materials.Add(glass);
            coolant=new Material(Shader.Find("Parallax/BeachWater")){name="Radiant coolant"};coolant.SetColor("_Color",new Color(.02f,.42f,.5f,.85f));Materials.Add(coolant);
            game=root.gameObject.AddComponent<StarshipGame>();game.lighting=root.gameObject.AddComponent<StarshipLighting>();
            game.ending=root.gameObject.AddComponent<StarshipEnding>();game.ending.game=game;
            var sunObj=new GameObject("External star / fixed solar direction");sunObj.transform.SetParent(root,false);sunObj.transform.rotation=Quaternion.LookRotation(-StarshipLighting.ToSun);
            var sun=sunObj.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.94f,.82f);sun.intensity=1.5f;sun.shadows=LightShadows.Soft;sun.shadowBias=.012f;sun.shadowNormalBias=.015f;RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.20f,.27f,.38f);RenderSettings.ambientEquatorColor=new Color(.16f,.21f,.28f);RenderSettings.ambientGroundColor=new Color(.07f,.10f,.15f);RenderSettings.fog=false;
            var sky=new Material(Shader.Find("Parallax/StarshipSky")){name="Starfield and external sun"};Materials.Add(sky);RenderSettings.skybox=sky;
            QualitySettings.shadowDistance=110;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowCascades=4;QualitySettings.shadows=ShadowQuality.All;
            for(int i=0;i<5;i++)
            {
                Walls(i);
                if(i!=3)Floor(i,-14,14);
                if(i!=4)Cover(i,new Vector3(0,0,-11),6,5);
            }
            Prop("cargo","01 / 확대용 화물 상자",0,new Vector3(-3,.025f,-8),.8f,3.3f);
            Prop("module","01 / 낮은 장비 모듈",0,new Vector3(3,.025f,-8),1,4);
            Prop("canopy","01 / 접이식 차광판",0,new Vector3(-5,.025f,-9),.75f,3.4f);
            RBox(0,"High experiment port",new Vector3(0,2.44f,11.7f),new Vector3(5.1f,.32f,4.2f),white);
            Cover(0,new Vector3(0,0,0),3.4f,3.6f);
            Cover(0,new Vector3(0,2.6f,11.3f),5.3f,5.1f);
            OpticalRoom();
            Prop("rack","03 / 분리형 모듈 보관 랙",2,new Vector3(-5,.02f,-7),1,3.2f);
            Prop("module","03 / 랙 왼쪽 모듈",2,new Vector3(-5.65f,1.18f,-7),1,5);
            Prop("module","03 / 랙 오른쪽 모듈",2,new Vector3(-4.35f,1.18f,-7),1,5);
            Prop("table","03 / 정비 탁자",2,new Vector3(5,.025f,-7),1,3.8f);
            Cover(2,new Vector3(-1.5f,0,0),3.3f,3.3f);
            Cover(2,new Vector3(4,0,8),3.4f,3.4f);
            RBox(2,"Storage upper landing",new Vector3(0,1.49f,12),new Vector3(5,.22f,3.8f),white);
            // Elevated rear doorway forces the modules/table to become steps as well as shade.
            RBox(2,"Storage threshold",new Vector3(0,.8f,14),new Vector3(5,1.6f,.3f),hull);
            Floor(3,-14,-9);Floor(3,-5,-2);Floor(3,6,14);Floor(3,-2,6,9,14);
            Water(3,"Bridge coolant trench",-9,-5,-14,14,false);
            Water(3,"Transport coolant channel",-2,6,-14,8.9f,true);
            Water(3,"Outer coolant gutter",-2,6,12.6f,14,false);
            Prop("plank","04 / 재사용 정비 발판",3,new Vector3(-3,.025f,-11),1,3.4f);
            Prop("canopy","04 / 상층 차광판",3,new Vector3(4,.025f,-11),.8f,3.8f);
            Prop("capsule","04 / 부력 운반 캡슐",3,new Vector3(6,.025f,-11),1.05f,3.5f);
            game.waterGate=Gate(3,13.7f,true);
            Cover(3,new Vector3(0,0,-3.5f),3.4f,2.9f);
            Cover(3,new Vector3(10.6f,0,7),3.4f,3.4f);
            var sideLock=RBox(3,"Transport-locked side walkway",new Vector3(10.75f,1.7f,-2.2f),new Vector3(3.5f,3.4f,.25f),ink);
            sideLock.AddComponent<StarshipWaterGateLink>().gate=game.waterGate;
            RBox(3,"First high maintenance ledge",new Vector3(7,1.45f,8.8f),new Vector3(1.7f,.3f,1.8f),white);
            RBox(3,"Middle maintenance refuge",new Vector3(5,2.4f,10.5f),new Vector3(2.6f,.3f,4.8f),white);
            RBox(3,"Upper maintenance ledge",new Vector3(-1,3.4f,10.5f),new Vector3(2.6f,.3f,3.4f),white);
            RBox(3,"Upper exit deck",new Vector3(0,4.40f,12.8f),new Vector3(4.9f,.3f,2.4f),white);
            Cover(3,new Vector3(5,2.55f,10.5f),2.9f,4.9f,3.5f);
            // EXIT room deliberately contains only the manipulable sign.
            RBox(4,"Quiet room ceiling",new Vector3(0,5.7f,0),new Vector3(29,.2f,29),ink);
            var exit=Prop("exit","EXIT",4,new Vector3(0,.02f,0),1,8.5f);game.ending.exitSign=exit;
            var quietLight=new GameObject("Quiet room light").AddComponent<Light>();quietLight.transform.SetParent(root,false);quietLight.transform.position=Point(4,new Vector3(0,4,-5));quietLight.type=LightType.Point;quietLight.range=20;quietLight.intensity=1.2f;quietLight.color=new Color(.55f,.73f,1);
            Connections();Exterior();game.ending.BuildCorridor(root,ink,black,cyan,white);
            var player=new GameObject("플레이어");player.transform.SetParent(root,false);player.transform.position=Checkpoints[0]+Vector3.up*.025f;
            var controller=player.AddComponent<CharacterController>();controller.height=1.75f;controller.radius=.24f;controller.center=new Vector3(0,.875f,0);controller.stepOffset=.24f;controller.skinWidth=.015f;controller.minMoveDistance=0;game.controller=controller;
            var cam=new GameObject("Starship view");cam.transform.SetParent(root,false);game.view=cam.AddComponent<Camera>();game.view.fieldOfView=70;game.view.nearClipPlane=.05f;game.view.farClipPlane=1100;cam.AddComponent<AudioListener>();
            game.props=props.ToArray();return game;
        }
    }
}
