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
        public static readonly Vector3 CastleRest=new Vector3(5,3.42f,96),Exit=new Vector3(0,6.7f,95.4f);
        public static readonly string[] Titles={"01  작아진 거울, 넓어지는 빛","02  판자 위에 그늘을 놓다","03  물살에게 사물을 맡기다","04  모래성 꼭대기의 출구"};
        public static readonly string[] Hints={
            "작은 거울을 잡아 먼 모래 위에 크게 놓으세요.\nQ/E와 Z/X로 반사 방향을 맞춰 두 센서를 함께 비춥니다.\n문이 열리면 파라솔이나 돗자리로 반사광을 가려 건너세요.",
            "물줄기는 점프로 건너기엔 너무 넓습니다.\n판자를 키워 양쪽 둑에 걸친 다음, 파라솔을 키워\n판자 윗면에도 그늘이 이어지게 배치하세요.",
            "화살표 방향으로 물살이 흐릅니다. 공이나 판자를\n입구 쪽 물 위에 놓아 반대편까지 보내세요.\n수문이 열리면 옆 보행로로 이동해 사물을 회수할 수 있습니다.",
            "의자와 판자를 발판으로 재사용해 성벽을 오르세요.\n높이 차는 한 번의 점프 범위 안에 맞춰야 합니다.\n파라솔·돗자리의 그늘은 높은 발판까지 덮어야 합니다.\n오른쪽 성벽의 파라솔 아래는 중간 체크포인트입니다."};
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
        static void Label(string caption,Vector3 at,float size=.30f)
        {
            var go=new GameObject("Beach sign");go.transform.SetParent(root,false);go.transform.position=at;
            var l=go.AddComponent<BeachLabel>();l.caption=caption;l.size=size;l.color=new Color(.07f,.22f,.3f);
        }
        static void Shelter(Vector3 at)
        {
            Box("쉼터 차양",at+new Vector3(.68f,3.1f,-1.25f),new Vector3(6.2f,.18f,5.2f),foam);
            foreach(float x in new[]{-2.25f,3.6f})Box("쉼터 기둥",at+new Vector3(x,1.5f,-3.3f),new Vector3(.15f,3,.15f),timber);
            Box("체크포인트 표식",at+new Vector3(0,.012f,0),new Vector3(2,.025f,1.4f),blue,solid:false,shadow:false);
        }
        static BeachWater Water(string name,Vector2 min,Vector2 max,Vector3 flow,bool transport=false,float y=-.04f)
        {
            var go=Box(name,new Vector3((min.x+max.x)/2,y-.03f,(min.y+max.y)/2),new Vector3(max.x-min.x,.06f,max.y-min.y),water,solid:false,shadow:false);
            var material=new Material(water){name=name+" surface"};material.SetVector("_Flow",flow.sqrMagnitude>.01f?new Vector4(flow.x,flow.z,flow.magnitude,0):new Vector4(0,1,.15f,0));Materials.Add(material);go.GetComponent<Renderer>().sharedMaterial=material;
            var w=go.AddComponent<BeachWater>();w.min=min;w.max=max;w.surface=y;w.flow=flow;w.transportChannel=transport;
            // Channels need a real floor so non-buoyant props remain reachable.
            // Puddles already sit on the sand; neither floor makes water safe to walk in.
            if(y<0)Box(name+" · 水底",new Vector3((min.x+max.x)/2,WaterBedY-.15f,(min.y+max.y)/2),
                new Vector3(max.x-min.x,.3f,max.y-min.y),sand,shadow:false);
            if(transport)for(int z=(int)min.y+1;z<max.y;z+=2)
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
        static BeachLightGate Gate(BeachGame game,float z,bool flowing)
        {
            foreach(int side in new[]{-1,1})Box("길을 막는 해변 수문 벽",new Vector3(side*8,2,z),new Vector3(12,4,.35f),timber);
            var obj=new GameObject(flowing?"물살 운반 수문":"반사광 센서 문");obj.transform.SetParent(root,false);var g=obj.AddComponent<BeachLightGate>();g.game=game;g.waterGate=flowing;
            var panel=Box("수문",new Vector3(0,2,z),new Vector3(4,4,.3f),blue,obj.transform);g.panel=panel.GetComponent<BoxCollider>();g.closedPosition=panel.transform.position;
            g.sensors=new Transform[flowing?0:2];g.sensorFaces=new Renderer[g.sensors.Length];
            for(int i=0;i<g.sensors.Length;i++){var sensor=Box("빛 센서 "+(i+1),new Vector3(i==0?-1.02f:1.02f,.5f,z-.32f),new Vector3(.25f,.25f,.09f),gold,solid:false,shadow:false);g.sensors[i]=sensor.transform;g.sensorFaces[i]=sensor.GetComponent<Renderer>();}
            return g;
        }
        public static BeachGame Build(Func<string,GameObject> source)
        {
            assets=source;Materials.Clear();root=new GameObject("PARALLAX / 마지막 해변").transform;
            sand=Mat("Sand",new Color(.68f,.56f,.39f));timber=Mat("Weathered timber",new Color(.54f,.32f,.16f));foam=Mat("Warm canvas",new Color(.95f,.93f,.82f));coral=Mat("Coral marker",new Color(.92f,.28f,.19f));blue=Mat("Lagoon blue",new Color(.04f,.46f,.67f));navy=Mat("Deep navy",new Color(.08f,.17f,.22f));gold=Mat("Signal amber",new Color(1,.67f,.1f));
            water=new Material(Shader.Find("Parallax/BeachWater")){name="Moving sea water"};Materials.Add(water);
            var game=root.gameObject.AddComponent<BeachGame>();game.lighting=root.gameObject.AddComponent<BeachLighting>();
            var sunObj=new GameObject("해변 햇빛");sunObj.transform.SetParent(root);sunObj.transform.rotation=Quaternion.LookRotation(-BeachLighting.ToSun);var sun=sunObj.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.94f,.78f);sun.intensity=1.35f;sun.shadows=LightShadows.Soft;sun.shadowBias=.012f;sun.shadowNormalBias=.015f;RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.49f,.68f,.78f);RenderSettings.ambientEquatorColor=new Color(.37f,.44f,.46f);RenderSettings.ambientGroundColor=new Color(.27f,.25f,.20f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.60f,.81f,.9f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0045f;
            var sky=new Material(Shader.Find("Skybox/Procedural")){name="Beach sky"};sky.SetColor("_SkyTint",new Color(.44f,.69f,.95f));sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",.7f);RenderSettings.skybox=sky;Materials.Add(sky);
            QualitySettings.shadowDistance=100;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowCascades=4;QualitySettings.shadows=ShadowQuality.All;
            Box("수평선",new Vector3(0,-.22f,40),new Vector3(900,.1f,900),water,solid:false,shadow:false);
            Ground(0,33);Ground(40,55);Ground(67,108);Ground(55,67,7.2f,10.4f);
            // Low walls frame the playable shoreline; the continuous water cuts cannot be bypassed.
            foreach(int side in new[]{-1,1})
            {
                Box("해변 경계",new Vector3(side*14.1f,1.5f,54),new Vector3(.3f,3,108),sand);
                for(int z=2;z<108;z+=8){var fence=Model("fence",root,new Vector3(side*13.95f,1.4f,z),2.4f);fence.transform.rotation=Quaternion.Euler(0,90,0);}
            }
            Box("입구 둑",new Vector3(0,1,0),new Vector3(28,2,.3f),sand);Box("끝 둑",new Vector3(0,1,108),new Vector3(28,2,.3f),sand);
            Water("얕은 물웅덩이",new Vector2(3,9),new Vector2(9,12),Vector3.zero,false,.055f);
            Water("작은 물웅덩이",new Vector2(-11,17),new Vector2(-6,19),Vector3.zero,false,.055f);
            Water("판자 횡단 수로",new Vector2(-14,33),new Vector2(14,40),new Vector3(.18f,0,0));
            Water("운반 물살",new Vector2(-14,55),new Vector2(7.1f,67),new Vector3(0,0,1.35f),true);
            Water("보행로 바깥 물살",new Vector2(10.5f,55),new Vector2(14,67),new Vector3(0,0,1.35f));
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
            props.Add(Prop("umbrella","01 · 파라솔",0,new Vector3(-8,.02f,8.5f),.8f,3.8f));props.Add(Prop("mat","01 · 차광 돗자리",0,new Vector3(-5,.04f,5.5f),1,5));
            Box("거울 통로 차양",new Vector3(.8f,4.6f,20),new Vector3(6.8f,.2f,7.5f),foam);
            game.lightGate=Gate(game,23.7f,false);Label("01  거울을 키워 두 센서에 빛을 연결하세요",new Vector3(0,3.2f,23.4f),.20f);
            props.Add(Prop("plank","02 · 짧은 판자",1,new Vector3(-3,.04f,29),1,4.2f));props.Add(Prop("umbrella","02 · 햇빛 가리개",1,new Vector3(4,.02f,29),.8f,4.5f));props.Add(Prop("chair","02 · 접이식 의자",1,new Vector3(-6,.02f,29),1.3f,3.8f));props.Add(Prop("ball","02 · 비치볼",1,new Vector3(6,.03f,30),1,4));
            Shelter(new Vector3(0,0,43.8f));Label("02  통로와 그늘",new Vector3(-7,2.5f,32),.16f);
            props.Add(Prop("ball","03 · 물살 운반 비치볼",2,new Vector3(2,.04f,53),1.1f,4));props.Add(Prop("plank","03 · 떠다니는 판자",2,new Vector3(-3,.04f,52.6f),.8f,4));props.Add(Prop("umbrella","03 · 보행로 파라솔",2,new Vector3(6,.03f,51),1,4.4f));props.Add(Prop("mat","03 · 접이식 차광막",2,new Vector3(-5,.05f,52),1,5));
            // The dry side walk is usable only after a floated object reaches the receiving cove.
            var sideBlock=Box("운반 전 보행로 잠금",new Vector3(8.8f,1.6f,55.6f),new Vector3(3.1f,3.2f,.25f),blue);
            game.waterGate=Gate(game,71.8f,true);var link=sideBlock.AddComponent<BeachWaterGateLink>();link.gate=game.waterGate;
            Box("물살 도착 표식",new Vector3(-2,.04f,66),new Vector3(4,.08f,.25f),gold,solid:false,shadow:false);Label("03  물살로 운반",new Vector3(-8,2.6f,54),.16f);
            var castle=Model("sandcastle",root,new Vector3(0,0,96),9,true);
            Box("정상 탑 받침",new Vector3(0,6.32f,95.4f),new Vector3(1.8f,.76f,1.8f),sand);
            Box("정상 출구 표식",Exit+Vector3.up*.008f,new Vector3(1.55f,.016f,1.55f),coral,solid:false,shadow:false);
            Box("성벽 중간 쉼터",CastleRest-Vector3.up*.12f,new Vector3(2.4f,.24f,2.4f),timber);
            Box("성벽 쉼터 기둥",new Vector3(5,1.6f,96),new Vector3(.3f,3.2f,.3f),timber);
            // A visible, spaced route makes every height change achievable. The first
            // 1.32 m ledge needs an object step; later jumps still need player-made shade.
            var ledges=new[]{new Vector3(8.2f,1.32f,96),new Vector3(6.65f,2.37f,96),new Vector3(3.45f,4.35f,96),new Vector3(2.15f,5.28f,94.9f),new Vector3(1.3f,6.10f,94.2f)};
            for(int i=0;i<ledges.Length;i++)
            {var p=ledges[i];Box("모래성 등반 턱 "+(i+1),p-Vector3.up*.14f,new Vector3(1.45f,.28f,1.45f),sand);Box("등반 턱 지지대",new Vector3(p.x,p.y/2-.14f,p.z),new Vector3(.52f,p.y-.28f,.52f),sand);}
            Model("umbrella",root,new Vector3(5.65f,3.42f,94.8f),1.35f,true);
            props.Add(Prop("chair","04 · 낮은 등반 의자",3,new Vector3(-3,.03f,79),1.3f,3.6f));props.Add(Prop("chair","04 · 높은 등반 의자",3,new Vector3(3,.03f,80),1.2f,4));props.Add(Prop("plank","04 · 등반 판자",3,new Vector3(-6,.03f,81),1,5));props.Add(Prop("plank","04 · 연결 판자",3,new Vector3(6,.03f,83),1,5));props.Add(Prop("umbrella","04 · 정상 차광 파라솔",3,new Vector3(4,.03f,77),1,5.5f));props.Add(Prop("umbrella","04 · 성벽 파라솔",3,new Vector3(-4,.03f,84),.8f,4.5f));props.Add(Prop("mat","04 · 성벽 돗자리",3,new Vector3(6,.04f,78),1,6));props.Add(Prop("ball","04 · 큰 비치볼",3,new Vector3(-6,.04f,78),1.8f,5));
            Label("04  오른쪽 성벽을 따라 정상으로",new Vector3(7,2.5f,87),.16f);Label("EXIT",Exit+new Vector3(0,1.1f,.35f),.28f);
            // Unreachable shoreline dressing establishes the resort silhouette.
            for(int z=7;z<105;z+=13){Model("surfboard",root,new Vector3(-17,0,z),1.4f);Model("driftwood",root,new Vector3(17,-.1f,z+3),1.7f);}
            var player=new GameObject("플레이어");player.transform.SetParent(root,false);player.transform.position=Checkpoints[0]+Vector3.up*.025f;
            var controller=player.AddComponent<CharacterController>();controller.height=1.75f;controller.radius=.24f;controller.center=new Vector3(0,.875f,0);controller.stepOffset=.24f;controller.skinWidth=.015f;controller.minMoveDistance=0;game.controller=controller;
            var cam=new GameObject("Beach view");cam.transform.SetParent(root,false);game.view=cam.AddComponent<Camera>();game.view.fieldOfView=70;game.view.nearClipPlane=.05f;game.view.farClipPlane=260;game.view.backgroundColor=new Color(.59f,.80f,.92f);cam.AddComponent<AudioListener>();
            game.props=props.ToArray();game.lighting.mirror=mirror;return game;
        }
    }
}
