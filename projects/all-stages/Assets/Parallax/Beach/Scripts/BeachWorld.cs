using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    public static class BeachWorld
    {
        public const int PhysicalMask=~(1<<BeachLighting.ShadowLayer);
        public const float WaterBedY=-2f, PropMinimumY=WaterBedY-.03f;
        public static readonly float[] ZoneNear={.18f,24.1f,48.1f,72.1f},ZoneFar={23.8f,47.8f,71.8f,107.8f};
        public static readonly Vector3[] Checkpoints={new Vector3(-6,0,4),new Vector3(0,0,27),new Vector3(0,0,51),new Vector3(0,0,75)};
        public static readonly Vector3 CastleRest=new Vector3(0,3.44f,91.7f),Exit=new Vector3(0,6.7f,95.4f);
        public static readonly string[] Titles={"01  태양광 산책로를 열다","02  두 섬을 잇는 비치 가든","03  그림자에서 꺼낸 그늘 배","04  원근으로 복구하는 모래성"};
        public static readonly string[] Hints={
            "깃발이 있는 모래성 정상이 목적지입니다.\n거울을 키워 놓고 안착시킨 뒤, 반사광을 태양 문양의\n두 수신판에 보내세요. 두 링이 차오르면 산책로가 열립니다.\n파라솔과 돗자리로 그늘을 이어 열린 길로 이동하세요.",
            "왼쪽 쉼터 섬과 오른쪽 섬을 판자로 비스듬히 잇거나,\n긴 다리를 직접 만들어 건널 수 있습니다. 파라솔로\n판자 위까지 그늘을 이어 주세요. 건너편 화단 사이에서는\n산책로와 차양을 따라 S자로 이동하세요.",
            "오른쪽에 세워진 제작 보드에 선체와 차양 그림자를\n맞추세요. 완성된 배가 작아져 보드 앞 받침에 떨어집니다.\n그 배를 직접 잡아 출항 부두 앞 물에 크게 놓고\n실제 그늘에 올라타 E로 출항하세요.",
            "모래성 정면의 빠진 성벽 두 곳을 복구하세요.\n첫 계단 조각을 키워 아래 그늘에서 올라가세요.\n중간의 평평한 성벽에 파라솔을 크게 놓아\n그늘을 만든 뒤, 두 번째 조각으로 정상까지 연결하세요."};
        public static readonly List<Material> Materials=new List<Material>();
        static Transform root;static Func<string,GameObject> assets;
        static Material sand,timber,foam,coral,blue,navy,water,gold;
        static Material Mat(string name,Color c)
        {
            var m=new Material(Shader.Find("Parallax/BeachSurface")){name=name,color=c};m.SetFloat("_Glossiness",.16f);m.EnableKeyword("_EMISSION");Materials.Add(m);return m;
        }
        public static GameObject Box(string name,Vector3 at,Vector3 size,Material material,Transform parent=null,bool solid=true,bool shadow=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent==null?root:parent,false);go.transform.localPosition=at;go.transform.localScale=size;
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;
            if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Ground(float near,float far,float left=-14,float right=14)
        {Box("마른 모래 둑",new Vector3((left+right)/2,-.45f,(near+far)/2),new Vector3(right-left,.9f,far-near),sand);}
        static GameObject Model(string key,Transform parent,Vector3 position,float scale=1,bool solid=false)
        {
            var go=UnityEngine.Object.Instantiate(assets(key));go.name="3DAssets · "+key;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one*scale;
            foreach(var r in go.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.TwoSided;
            if(solid)foreach(var f in go.GetComponentsInChildren<MeshFilter>())f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;
            return go;
        }
        static void Label(string caption,Vector3 at,float size=.30f,Color? ink=null)
        {
            var go=new GameObject("Beach sign");go.transform.SetParent(root,false);go.transform.position=at;
            var l=go.AddComponent<BeachLabel>();l.caption=caption;l.size=size;l.color=ink??new Color(.07f,.22f,.3f);
        }
        static void Shelter(Vector3 at)
        {
            if(at.z>24)
            {
                const float height=3.65f;
                // Keep the rear fabric edge clear of the preceding 4 m gate.
                // The full checkpoint marker remains inside the real shadow.
                BeachResortParasol.Build("체크포인트 "+Mathf.RoundToInt(at.z),root,
                    at+new Vector3(.24f*height,height,-.70f),new Vector2(6.2f,4.6f),at.y);
            }
            else
            {
                Box("쉼터 차양",at+new Vector3(.68f,3.1f,-1.25f),new Vector3(6.2f,.18f,5.2f),foam);
                foreach(float x in new[]{-2.25f,3.6f})Box("쉼터 기둥",at+new Vector3(x,1.5f,-3.3f),new Vector3(.15f,3,.15f),timber);
            }
            Box("체크포인트 표식",at+new Vector3(0,.012f,0),new Vector3(2,.025f,1.4f),blue,solid:false,shadow:false);
        }
        static BeachWater Water(string name,Vector2 min,Vector2 max,Vector3 flow,bool transport=false,float y=-.04f)
        {
            var go=Box(name,new Vector3((min.x+max.x)/2,y-.03f,(min.y+max.y)/2),new Vector3(max.x-min.x,.06f,max.y-min.y),water,solid:false,shadow:false);
            bool moving=flow.sqrMagnitude>.01f;
            var material=new Material(water){name=name+" surface"};material.SetVector("_Flow",moving?new Vector4(flow.x,flow.z,flow.magnitude,0):Vector4.zero);
            material.SetVector("_Area",new Vector4(min.x,min.y,max.x,max.y));material.SetFloat("_WaterKind",y>=0||!moving?1:2);Materials.Add(material);go.GetComponent<Renderer>().sharedMaterial=material;
            var w=go.AddComponent<BeachWater>();w.min=min;w.max=max;w.surface=y;w.flow=flow;w.transportChannel=transport;
            // Channels need a real floor so non-buoyant props remain reachable.
            // Puddles already sit on the sand; neither floor makes water safe to walk in.
            if(y<0)Box(name+" · 水底",new Vector3((min.x+max.x)/2,WaterBedY-.15f,(min.y+max.y)/2),
                new Vector3(max.x-min.x,.3f,max.y-min.y),sand,shadow:false);
            if(moving)for(int z=(int)min.y+1;z<max.y;z+=2)
            {
                foreach(float x in new[]{-3f,0,3f})
                {
                    Box("물살 화살표",new Vector3(x,y+.017f,z),new Vector3(.055f,.018f,.6f),foam,solid:false,shadow:false);
                    foreach(int side in new[]{-1,1}){var head=Box("물살 방향",new Vector3(x+side*.12f,y+.017f,z+.24f),new Vector3(.05f,.018f,.32f),foam,solid:false,shadow:false);head.transform.rotation=Quaternion.Euler(0,side*45,0);}
                }
            }
            return w;
        }
        static void Collision(BeachProp p,string name,Vector3 center,Vector3 size)
        {var go=new GameObject(name);go.transform.SetParent(p.transform,false);var c=go.AddComponent<BoxCollider>();c.center=center;c.size=size;}
        static BeachProp Prop(string key,string title,int zone,Vector3 at,float initial=1,float maximum=4.5f)
        {
            var obj=new GameObject(title);obj.transform.SetParent(root,false);obj.transform.position=at;var p=obj.AddComponent<BeachProp>();p.title=title;p.zone=zone;p.maxScale=maximum;p.minScale=.30f;p.buoyant=key=="plank"||key=="ball"||key=="chair"||key=="mat";p.upright=key=="umbrella"||key=="mirror";
            if(key=="plank")
            {
                Box("나무 판자",new Vector3(0,.10f,0),new Vector3(1.25f,.2f,2.35f),timber,p.transform);
                foreach(float x in new[]{-.4f,0,.4f})Box("판자 결",new Vector3(x,.204f,0),new Vector3(.025f,.008f,2.3f),navy,p.transform,false,false);
            }
            else
            {
                Model(key,p.transform,Vector3.zero);
                if(key=="umbrella")
                {Collision(p,"파라솔 기둥",new Vector3(0,1.05f,0),new Vector3(.12f,2.1f,.12f));Collision(p,"파라솔 차양",new Vector3(0,2.20f,0),new Vector3(2.12f,.32f,2.12f));Collision(p,"파라솔 받침",new Vector3(0,.05f,0),new Vector3(.45f,.1f,.45f));}
                else if(key=="chair")
                {Collision(p,"의자 좌석",new Vector3(0,.42f,0),new Vector3(.60f,.12f,.64f));Collision(p,"의자 등받이",new Vector3(0,.68f,.30f),new Vector3(.62f,.46f,.12f));foreach(float x in new[]{-.25f,.25f})Collision(p,"의자 다리",new Vector3(x,.18f,0),new Vector3(.1f,.36f,.70f));}
                else if(key=="mat")Collision(p,"돗자리",new Vector3(0,.018f,0),new Vector3(1.79f,.036f,.8f));
                else if(key=="ball")
                {Collision(p,"비치볼 배치 검사",new Vector3(0,.30f,0),new Vector3(.6f,.6f,.6f));p.isBall=true;p.sphere=obj.AddComponent<SphereCollider>();p.sphere.center=new Vector3(0,.30f,0);p.sphere.radius=.30f;p.sphere.enabled=false;}
            }
            obj.transform.localScale=Vector3.one*initial;p.Remember();return p;
        }
        static void Decoration(BeachProp prop)
        {
            prop.gameObject.name="장식 · "+prop.title;
            // Preserve the visible furniture and its real static colliders;
            // only puzzle objects enter the grab/physics registry.
            UnityEngine.Object.DestroyImmediate(prop);
        }
        static BeachLightGate Gate(BeachGame game,float z,bool flowing)
        {
            if(!flowing)return SolarPromenadeGate(game,z);
            var obj=new GameObject("03 · 그늘 배 도착 게이트");obj.transform.SetParent(root,false);
            foreach(int side in new[]{-1,1})
            {
                Box("화물 게이트 윗틀",new Vector3(side*8,3.9f,z),new Vector3(12,.16f,.20f),foam,obj.transform);
                Box("화물 게이트 밑틀",new Vector3(side*8,.10f,z),new Vector3(12,.16f,.20f),navy,obj.transform);
                for(int i=0;i<27;i++)Box("작업장 사이가 보이는 문살",new Vector3(side*(2.15f+i*.45f),2,z),new Vector3(.045f,3.8f,.14f),foam,obj.transform);
                foreach(float x in new[]{2.15f,8f,13.8f})Box("화물 게이트 기둥",new Vector3(side*x,2,z),new Vector3(.22f,4,.24f),navy,obj.transform);
            }
            var g=obj.AddComponent<BeachLightGate>();g.game=game;g.waterGate=true;
            var panel=new GameObject("물살 도착 수문");panel.transform.SetParent(obj.transform,false);panel.transform.position=new Vector3(0,2,z);
            g.panel=panel.AddComponent<BoxCollider>();g.panel.size=new Vector3(4,4,.3f);g.closedPosition=panel.transform.position;
            foreach(float y in new[]{-1.9f,1.9f})Box("수문 이동 프레임",new Vector3(0,y,0),new Vector3(4,.18f,.25f),navy,panel.transform,false);
            for(int i=0;i<9;i++)Box("열리는 수문 문살",new Vector3(-1.8f+i*.45f,0,0),new Vector3(.055f,3.8f,.16f),foam,panel.transform,false);
            Box("도착 게이트 표지",new Vector3(0,4.35f,z),new Vector3(5.7f,.65f,.15f),navy,obj.transform,false,false);
            Label("그늘 배 선착장 → 모래성 축제",new Vector3(0,4.35f,z-.1f),.22f,new Color(.98f,.95f,.83f));
            g.sensors=new Transform[0];g.sensorFaces=new Renderer[0];
            return g;
        }
        static BeachLightGate SolarPromenadeGate(BeachGame game,float z)
        {
            var obj=new GameObject("01 · 태양광 산책로 입구");obj.transform.SetParent(root,false);
            // Thin vertical bars retain sightlines without low rails that become
            // a jump/mantle ladder around the optical route lock.
            foreach(int side in new[]{-1,1})
            {
                Box("산책로 난간 아래",new Vector3(side*8,.10f,z),new Vector3(12,.12f,.16f),timber,obj.transform);
                Box("산책로 난간 위",new Vector3(side*8,3.18f,z),new Vector3(12,.13f,.18f),foam,obj.transform);
                for(int i=0;i<27;i++)Box("시야가 통하는 산책로 문살",new Vector3(side*(2.15f+i*.45f),1.6f,z),new Vector3(.045f,3.1f,.12f),foam,obj.transform);
                foreach(float x in new[]{2.15f,5.5f,9.5f,13.8f})
                {
                    Box("산책로 기둥",new Vector3(side*x,1.6f,z),new Vector3(.19f,3.2f,.22f),timber,obj.transform);
                    Box("산책로 기둥 모자",new Vector3(side*x,3.24f,z),new Vector3(.26f,.12f,.29f),coral,obj.transform);
                }
            }
            var gate=obj.AddComponent<BeachLightGate>();gate.game=game;gate.waterGate=false;gate.openOffset=Vector3.right*4.5f;
            var leaf=new GameObject("태양광 산책로 · 슬라이딩 문");leaf.transform.SetParent(obj.transform,false);leaf.transform.position=new Vector3(0,0,z);
            var collider=leaf.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.6f,0);collider.size=new Vector3(4,3.2f,.18f);
            Box("문 아래 프레임",new Vector3(0,.10f,0),new Vector3(4,.16f,.18f),navy,leaf.transform,false);
            Box("문 위 프레임",new Vector3(0,3.12f,0),new Vector3(4,.16f,.18f),navy,leaf.transform,false);
            for(int i=0;i<9;i++)Box("사이가 보이는 문살",new Vector3(-1.8f+i*.45f,1.6f,0),new Vector3(.045f,3.1f,.12f),foam,leaf.transform,false);
            gate.panel=collider;gate.closedPosition=leaf.transform.position;
            gate.sensors=new Transform[2];gate.sensorFaces=new Renderer[2];gate.receiverVisuals=new BeachSolarReceiver[2];
            for(int i=0;i<2;i++)
            {
                var receiver=BeachSolarReceiver.Create(obj.transform,new Vector3(i==0?-1.4f:1.4f,1.65f,z-.35f),i+1);
                gate.receiverVisuals[i]=receiver;gate.sensors[i]=receiver.Target;gate.sensorFaces[i]=receiver.Face;
                Box("수신기 전력 연결선",new Vector3(i==0?-1.4f:1.4f,.032f,z-.75f),new Vector3(.055f,.025f,.8f),gold,obj.transform,false,false);
            }
            Box("태양광 안내판",new Vector3(-5.1f,1.9f,z-.35f),new Vector3(3.3f,1.02f,.13f),navy,obj.transform,false,false);
            Label("태양광 산책로\n거울 → 태양 수신판 2개 → 길 열림",new Vector3(-5.1f,1.9f,z-.43f),.18f,new Color(.98f,.95f,.83f));
            return gate;
        }
        static void EntranceBoundary()
        {
            foreach(int side in new[]{-1,1})
            {
                // Narrow gaps retain the walking boundary while revealing the sea.
                // A single capsule barrier follows these bars; the visible gaps are
                // smaller than the player's diameter. Thin coastal rails cast no shade.
                var coast=new GameObject("열린 후반 해안 경계");coast.transform.SetParent(root,false);coast.transform.position=new Vector3(side*14.1f,1.6f,67);
                coast.AddComponent<BoxCollider>().size=new Vector3(.12f,3.2f,82);
                Box("후반 해안 모래턱",new Vector3(side*14.1f,.15f,67),new Vector3(.24f,.3f,82),sand);
                Box("후반 해안 난간 윗틀",new Vector3(side*14.1f,3.18f,67),new Vector3(.10f,.09f,82),foam,shadow:false);
                for(int i=0;i<=164;i++)Box("바다를 여는 가는 난간",new Vector3(side*14.1f,1.6f,26+i*.5f),new Vector3(.08f,3.1f,.035f),foam,solid:false,shadow:false);
                for(int z=26;z<=108;z+=8)Box("해안 나무 말뚝",new Vector3(side*14.1f,1.6f,z),new Vector3(.20f,3.2f,.20f),timber,shadow:false);
                Box("입구 해안 모래턱",new Vector3(side*14.2f,.15f,12),new Vector3(.65f,.3f,24),sand);
                // No waist-height footholds: repeated jumps on the former rails
                // could bypass the closed optical gate at the coast.
                Box("열린 해변 경계 윗틀",new Vector3(side*14.05f,3.18f,13),new Vector3(.12f,.10f,26),foam);
                for(int i=0;i<=58;i++)Box("바다가 보이는 가는 경계 문살",new Vector3(side*14.05f,1.6f,i*26f/58),new Vector3(.10f,3.1f,.045f),foam);
                foreach(int z in new[]{0,6,12,18,24,26})
                {
                    Box("해안 경계 말뚝",new Vector3(side*14.05f,1.6f,z),new Vector3(.20f,3.2f,.20f),timber);
                    Box("해안 경계 표식",new Vector3(side*14.05f,3.24f,z),new Vector3(.23f,.12f,.23f),coral);
                }
            }
            Box("입구 모래 둑",new Vector3(0,.25f,0),new Vector3(28,.5f,.5f),sand);
            Box("입구 해변 난간 윗틀",new Vector3(0,3.18f,0),new Vector3(28,.10f,.12f),foam);
            for(int i=0;i<=62;i++)Box("입구의 가는 경계 문살",new Vector3(-14+i*28f/62,1.6f,0),new Vector3(.045f,3.1f,.10f),foam);
            for(int x=-14;x<=14;x+=4)Box("입구 해변 말뚝",new Vector3(x,1.6f,0),new Vector3(.2f,3.2f,.2f),timber);
            Box("끝 둑",new Vector3(0,1,108),new Vector3(28,2,.3f),sand);
        }
        static void EntranceWayfinding()
        {
            var pavers=new[]{new Vector3(-6,0,5.7f),new Vector3(-4.6f,0,7.4f),new Vector3(-3.1f,0,9.2f),new Vector3(-1.7f,0,11.3f),new Vector3(-.6f,0,13.5f),new Vector3(0,0,15.8f),new Vector3(0,0,18)};
            for(int i=0;i<pavers.Length;i++)
            {
                var tile=Box("모래 위 산책로 디딤목",pavers[i]+Vector3.up*.018f,new Vector3(.9f,.036f,.48f),i%3==0?coral:timber,solid:false,shadow:false);
                tile.transform.rotation=Quaternion.Euler(0,-14+i*3,0);
            }
            Box("모래성 방향 안내판 기둥",new Vector3(-8.6f,.9f,14.5f),new Vector3(.16f,1.8f,.16f),timber);
            Box("모래성 방향 안내판",new Vector3(-8.6f,1.92f,14.5f),new Vector3(2.65f,.95f,.14f),navy);
            Label("목표 · 모래성 정상  ↗\n깃발을 향해 이동하세요",new Vector3(-8.6f,1.92f,14.41f),.19f,new Color(.98f,.95f,.83f));
            Box("거울 설치 구역 테두리",new Vector3(-1.1f,.012f,13.3f),new Vector3(3.5f,.025f,2.2f),foam,solid:false,shadow:false);
            Box("거울 설치 구역 안쪽",new Vector3(-1.1f,.028f,13.3f),new Vector3(3.3f,.008f,2.0f),sand,solid:false,shadow:false);
            Label("거울로 햇빛을 보내는 곳",new Vector3(-1.1f,.09f,14.0f),.15f);
        }
        static void GoalLandmark()
        {
            Box("모래성 목표 깃대",new Vector3(1.9f,9.65f,97),new Vector3(.14f,6.1f,.14f),foam,solid:false);
            var flag=Box("Beach goal flag",new Vector3(3.35f,11.8f,97),new Vector3(2.8f,1.75f,.055f),coral,solid:false,shadow:false);
            flag.transform.rotation=Quaternion.Euler(0,-12,-4);
            var stripe=Box("모래성 깃발 표식",new Vector3(3.35f,11.8f,96.96f),new Vector3(2.3f,.25f,.06f),foam,solid:false,shadow:false);
            stripe.transform.rotation=flag.transform.rotation;
            Label("모래성 정상",new Vector3(1.1f,8.1f,95.8f),.34f);
        }
        static void ShadeStop(string name,Vector3 refuge,Vector2 size,float roofY,Vector3? mastFoot=null)
        {
            // Project the canopy towards the sun so its real shadow covers the
            // labelled resting floor, rather than the empty sand behind it.
            float height=roofY-refuge.y;
            Vector3 roof=refuge+new Vector3(.24f*height,height,-.42f*height);
            var host=new GameObject(name);host.transform.SetParent(root,false);
            BeachResortParasol.Build(name,host.transform,roof,size,0,mastFoot);
            Box(name+" 바닥 표식",refuge+Vector3.up*.012f,new Vector3(1.1f,.022f,.62f),blue,host.transform,false,false);
        }
        static BoxCollider RouteDeck(string name,Vector3 top,Vector2 size,Material surface)
        {
            // Solid sand-filled timber bases give these short platforms visible
            // support; no invisible holes or player-only collision are introduced.
            var deck=Box(name,new Vector3(top.x,top.y*.5f,top.z),new Vector3(size.x,top.y,size.y),surface);
            int count=Mathf.Max(3,Mathf.RoundToInt(size.y/.24f));
            for(int i=0;i<count;i++)Box(name+" 판재 틈",new Vector3(top.x,top.y+.007f,top.z-size.y*.5f+(i+.5f)*size.y/count),new Vector3(size.x,.014f,.028f),navy,solid:false,shadow:false);
            foreach(float x in new[]{-1f,1f})Box(name+" 모서리",new Vector3(top.x+x*(size.x*.5f-.05f),top.y+.025f,top.z),new Vector3(.075f,.05f,size.y),foam,solid:false,shadow:false);
            return deck.GetComponent<BoxCollider>();
        }
        static BoxCollider LaterRoute()
        {
            RouteDeck("02 · 왼쪽 쉼터 섬",new Vector3(-4,.30f,35.2f),new Vector2(3,2.3f),timber);
            RouteDeck("02 · 오른쪽 연결 섬",new Vector3(4.5f,.30f,38.5f),new Vector2(3,2.1f),timber);
            // Short fingers support both ends of broad rotated planks and give
            // players room to land without an exact-size placement trick.
            RouteDeck("02 · 왼쪽 섬 접안길",new Vector3(-3.6f,.30f,33.95f),new Vector2(1.5f,1.3f),timber);
            RouteDeck("02 · 오른쪽 섬 접안길",new Vector3(4.1f,.30f,39.9f),new Vector2(1.6f,1.4f),timber);
            ShadeStop("02 · 물 위의 쉼터",new Vector3(-4,.30f,35.2f),new Vector2(3.8f,3),4.20f,new Vector3(-5.02f,.30f,35.8f));
            foreach(var p in new[]{new Vector3(-5.15f,.42f,35.9f),new Vector3(5.65f,.42f,39.1f)})
            {
                Box("두 섬의 항로 말뚝",p+Vector3.up*.55f,new Vector3(.10f,1.1f,.10f),timber);
                Box("두 섬의 항로 깃발",p+new Vector3(.26f,1.0f,0),new Vector3(.55f,.33f,.035f),coral,solid:false,shadow:false);
            }
            Box("02 · 왼쪽 모래 화단",new Vector3(-4,.50f,42.5f),new Vector3(8,1,1.5f),sand);
            Box("02 · 오른쪽 모래 화단",new Vector3(4,.55f,46.2f),new Vector3(8,1.1f,1.5f),sand);
            ShadeStop("02 · 가든 오른쪽 쉼터",new Vector3(3,0,42.6f),new Vector2(4.4f,4),4.9f);
            ShadeStop("02 · 가든 왼쪽 쉼터",new Vector3(-2.6f,0,46.5f),new Vector2(4.4f,4),4.7f);
            Label("02  두 섬을 비스듬히 연결하세요",new Vector3(-7.3f,2.2f,31.7f),.18f);
            Label("그늘을 따라 비치 가든으로  ↗",new Vector3(7.2f,1.65f,41.6f),.17f);

            // The third waterway is an actual gap: only short launch/arrival
            // piers project from the two banks. There is no continuous side path.
            RouteDeck("03 · 그늘 배 출발 선착장",new Vector3(0,.24f,54.5f),new Vector2(4.8f,3),timber);
            var arrivalPier=RouteDeck("03 · 모래성 도착 선착장",new Vector3(0,.24f,69.3f),new Vector2(5.4f,2.8f),timber);
            ShadeStop("03 · 승선 준비 그늘",new Vector3(0,.24f,54.2f),new Vector2(5.6f,4.2f),4.3f,new Vector3(4.5f,0,53.9f));
            ShadeStop("03 · 도착 쉼터",new Vector3(0,.24f,69.7f),new Vector2(6.2f,4.6f),4.4f,new Vector3(-3.5f,0,70.2f));
            foreach(float x in new[]{-2.5f,2.5f})
            {
                Box("출발 계류 말뚝",new Vector3(x,.5f,55.15f),new Vector3(.18f,1,.18f),navy);
                Box("도착 계류 말뚝",new Vector3(x,.5f,68.8f),new Vector3(.18f,1,.18f),navy);
                Box("선착장 완충재",new Vector3(x,.30f,68.4f),new Vector3(.28f,.30f,.32f),coral);
            }
            Label("제작 보드에서 꺼낸 배를 물가로 가져오세요",new Vector3(-5.7f,1.7f,53.8f),.18f);
            Label("가림막 그늘에 승선 → E 출항",new Vector3(0,1.3f,54.2f),.18f);
            Label("모래성 구역 도착 선착장",new Vector3(0,1.4f,70.5f),.19f);

            Model("umbrella",root,new Vector3(1.2f,0,80.4f),2.4f,true);
            foreach(float z in new[]{77.5f,80.5f,83.5f,85.5f})
                Box("모래성 정면 산책로",new Vector3(0,.012f,z),new Vector3(.6f,.024f,.25f),coral,solid:false,shadow:false);
            return arrivalPier;
        }
        public static BeachGame Build(Func<string,GameObject> source)
        {
            assets=source;Materials.Clear();root=new GameObject("PARALLAX / 마지막 해변").transform;
            sand=Mat("Sand",new Color(.78f,.67f,.46f));timber=Mat("Weathered timber",new Color(.54f,.32f,.16f));foam=Mat("Warm canvas",new Color(.95f,.93f,.82f));coral=Mat("Coral marker",new Color(.92f,.28f,.19f));blue=Mat("Lagoon blue",new Color(.04f,.46f,.67f));navy=Mat("Deep navy",new Color(.08f,.17f,.22f));gold=Mat("Signal amber",new Color(1,.67f,.1f));
            water=new Material(Shader.Find("Parallax/BeachWaterRefresh")){name="Moving sea water"};Materials.Add(water);
            var game=root.gameObject.AddComponent<BeachGame>();game.lighting=root.gameObject.AddComponent<BeachLighting>();
            var sunObj=new GameObject("해변 햇빛");sunObj.transform.SetParent(root);sunObj.transform.rotation=Quaternion.LookRotation(-BeachLighting.ToSun);var sun=sunObj.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.94f,.78f);sun.intensity=1.35f;sun.shadows=LightShadows.Soft;sun.shadowBias=.012f;sun.shadowNormalBias=.015f;RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.49f,.68f,.78f);RenderSettings.ambientEquatorColor=new Color(.37f,.44f,.46f);RenderSettings.ambientGroundColor=new Color(.27f,.25f,.20f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.60f,.81f,.9f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0032f;
            var sky=new Material(Shader.Find("Skybox/Procedural")){name="Beach sky"};sky.SetColor("_SkyTint",new Color(.44f,.69f,.95f));sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",.7f);RenderSettings.skybox=sky;Materials.Add(sky);
            QualitySettings.shadowDistance=100;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowCascades=4;QualitySettings.shadows=ShadowQuality.All;
            Box("수평선",new Vector3(0,-.22f,40),new Vector3(900,.1f,900),water,solid:false,shadow:false);
            Ground(0,33);Ground(40,55);Ground(69,108);
            EntranceBoundary();EntranceWayfinding();
            Water("얕은 물웅덩이",new Vector2(3,9),new Vector2(9,12),Vector3.zero,false,.055f);
            Water("작은 물웅덩이",new Vector2(-11,17),new Vector2(-6,19),Vector3.zero,false,.055f);
            Water("판자 횡단 수로",new Vector2(-14,33),new Vector2(14,40),Vector3.zero);
            Water("그늘 배 항로",new Vector2(-14,55),new Vector2(14,69),new Vector3(0,0,.9f));
            foreach(var checkpoint in Checkpoints)Shelter(checkpoint);
            var props=new List<BeachProp>();
            var mirrorRoot=new GameObject("01 · 작은 해변 거울");mirrorRoot.transform.SetParent(root,false);mirrorRoot.transform.position=new Vector3(-3,.04f,7.5f);mirrorRoot.transform.localScale=Vector3.one*.45f;
            var mirrorProp=mirrorRoot.AddComponent<BeachProp>();mirrorProp.title="작은 거울 · 잡아서 확대";mirrorProp.zone=0;mirrorProp.minScale=.3f;mirrorProp.maxScale=3.8f;mirrorProp.upright=true;
            Box("거울 받침",new Vector3(0,.08f,0),new Vector3(1.25f,.16f,.9f),navy,mirrorRoot.transform);Box("거울 기둥",new Vector3(0,.7f,0),new Vector3(.13f,1.3f,.13f),timber,mirrorRoot.transform);
            var head=new GameObject("움직이는 반사면");head.transform.SetParent(mirrorRoot.transform,false);head.transform.localPosition=new Vector3(0,1.4f,0);
            Vector3 desired=new Vector3(3,-2.0f,12).normalized;head.transform.localRotation=Quaternion.LookRotation((desired+BeachLighting.ToSun).normalized);
            var mirror=head.AddComponent<BeachMirror>();mirror.game=game;mirror.prop=mirrorProp;mirror.halfSize=new Vector2(.7f,.9f);game.mirror=mirror;
            Box("거울 몸체",Vector3.zero,new Vector3(1.5f,1.9f,.1f),timber,head.transform);
            var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.transform.SetParent(head.transform,false);face.transform.localPosition=new Vector3(0,0,.057f);face.transform.localScale=new Vector3(1.4f,1.8f,1);UnityEngine.Object.DestroyImmediate(face.GetComponent<Collider>());face.layer=31;
            var mirrorMat=new Material(Shader.Find("Parallax/GalleryMirror")){name="Beach reflection"};Materials.Add(mirrorMat);mirror.face=face.GetComponent<Renderer>();mirror.face.sharedMaterial=mirrorMat;mirror.face.shadowCastingMode=ShadowCastingMode.Off;
            mirrorRoot.transform.rotation=Quaternion.Euler(0,-18,0);mirrorProp.Remember();props.Add(mirrorProp);
            props.Add(Prop("umbrella","01 · 파라솔",0,new Vector3(-8,.02f,8.5f),.8f,3.8f));Decoration(Prop("mat","01 · 차광 돗자리",0,new Vector3(-5,.04f,5.5f),1,5));
            Box("거울 통로 차양",new Vector3(.4f,4.4f,21.6f),new Vector3(6.8f,.12f,4.2f),foam);
            for(int i=0;i<5;i++)Box("산책로 줄무늬 차양",new Vector3(-2.2f+i*1.3f,4.47f,21.6f),new Vector3(.46f,.025f,4.2f),coral,solid:false,shadow:false);
            foreach(float x in new[]{-3.0f,3.8f})Box("산책로 차양 기둥",new Vector3(x,2.15f,23.9f),new Vector3(.14f,4.3f,.14f),timber);
            game.lightGate=Gate(game,23.7f,false);
            props.Add(Prop("plank","02 · 짧은 판자",1,new Vector3(-3,.04f,29),1,4.2f));props.Add(Prop("umbrella","02 · 햇빛 가리개",1,new Vector3(4,.02f,29),.8f,4.5f));Decoration(Prop("chair","02 · 접이식 의자",1,new Vector3(-6,.02f,29),1.3f,3.8f));Decoration(Prop("ball","02 · 비치볼",1,new Vector3(6,.03f,30),1,4));
            var arrivalPier=LaterRoute();
            props.Add(Prop("umbrella","03 · 제작대 파라솔",2,new Vector3(5,.03f,51),.8f,3));
            game.waterGate=Gate(game,71.8f,true);
            game.boat=BeachBoatBuilder.Build(root,game);game.boat.ArrivalBowZ=67.85f;game.boat.ArrivalPier=arrivalPier;props.Add(game.boat.Prop);
            game.shadowHarbor=BeachShadowHarborBuilder.Build(root,game,game.boat);props.AddRange(game.shadowHarbor.Pieces);
            var castle=Model("sandcastle",root,new Vector3(0,0,96),9,true);
            GoalLandmark();
            BeachCastleCut.Apply(castle.transform);
            var upperParasol=Prop("umbrella","04 · 성벽 파라솔",3,new Vector3(2,3.44f,91.7f),.65f,3.2f);
            foreach(Transform child in upperParasol.transform)child.localPosition-=Vector3.up*2.15f;
            upperParasol.transform.position+=Vector3.up*(2.15f*.65f);upperParasol.Remember();
            var lowerParasol=Model("umbrella",root,new Vector3(1.4f,1.1f,88.65f),2.4f,true);
            game.castleRoute=BeachCastleRestoration.Build(root,game,castle.transform,upperParasol,lowerParasol.transform);
            props.Add(game.castleRoute.LowerFragment);props.Add(game.castleRoute.UpperFragment);props.Add(upperParasol);
            // Unreachable shoreline dressing establishes the resort silhouette.
            for(int z=7;z<105;z+=13){Model("surfboard",root,new Vector3(-17,0,z),1.4f);Model("driftwood",root,new Vector3(17,-.1f,z+3),1.7f);}
            BeachScenery.Build(root,assets);
            var player=new GameObject("플레이어");player.transform.SetParent(root,false);player.transform.position=Checkpoints[0]+Vector3.up*.025f;
            var controller=player.AddComponent<CharacterController>();FirstPersonMotor.Configure(controller,null);game.controller=controller;
            var cam=new GameObject("Beach view");cam.transform.SetParent(root,false);game.view=cam.AddComponent<Camera>();FirstPersonMotor.Configure(controller,game.view);PlayerControls.ConfigureCamera(game.view);game.view.farClipPlane=260;game.view.backgroundColor=new Color(.59f,.80f,.92f);cam.AddComponent<AudioListener>();
            game.props=props.ToArray();game.lighting.mirror=mirror;return game;
        }
    }
}
