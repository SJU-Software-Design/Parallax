using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    // The complete roofed span starts beneath the open sea. The freely scaled
    // coastal boulder supplies the real load; its shadow is not a puzzle target.
    public static class BeachCounterweightFinaleBuilder
    {
        static Material wood, darkWood, iron, brass, cloth, rope, paint, orange;
        const float DeckY=3.06f, SeaBed=-13.15f;
        static readonly Vector3 CradleTop=new Vector3(6.5f,.35f,86.5f);
        static readonly Vector3 BridgeTop=new Vector3(.4f,DeckY,109.7f);
        public static BeachCounterweightFinale Build(Transform parent,BeachGame game)
        {
            var existing=parent.GetComponentInChildren<BeachCounterweightFinale>(true);
            if(existing!=null)return existing;
            wood=Mat("Harbor finale · weathered timber",new Color(.52f,.34f,.19f));
            darkWood=Mat("Counterweight · end grain",new Color(.26f,.15f,.075f));
            iron=Mat("Counterweight · aged iron",new Color(.13f,.16f,.16f),.75f,.24f);
            brass=Mat("Counterweight · worn brass",new Color(.64f,.43f,.17f),.68f,.28f);
            cloth=Mat("Counterweight · warm sailcloth",new Color(.87f,.78f,.61f));
            rope=Mat("Counterweight · hemp rope",new Color(.60f,.45f,.26f));
            paint=Mat("Counterweight · turquoise mark",new Color(.12f,.72f,.69f));
            orange=Mat("Counterweight · orange receiving rim",new Color(.84f,.42f,.18f),.16f,.24f);
            var root=Group("04 · 바다에서 인양하는 긴 천막 다리",parent,Vector3.zero);
            var f=root.gameObject.AddComponent<BeachCounterweightFinale>();f.game=game;
            f.travel=9.2f;f.liftSeconds=7;f.minimumLoadMass=280;
            f.waterSurface=-.04f;f.roofClearance=4.2f;
            BeachWorld.Water("깊은 항구 · 물 접촉 시 복귀",new Vector2(-40,83),new Vector2(40,140),Vector3.zero,bedDepth:-13);

            // Natural sand from the existing shore ends at Z83. Only the entry
            // ramp and near landing remain; there is no fixed boardwalk in the gap.
            Span(root,"해안 부두 경사로",new Vector3(0,0,82.8f),new Vector3(.4f,DeckY,93),3.6f,true);
            Deck(root,"가까운 고정 선착장",new Vector3(.4f,DeckY,95.75f),3.6f,6.1f,Vector3.forward);
            Deck(root,"배 쪽 고정 선착장",new Vector3(.4f,DeckY,123.7f),3.6f,6.2f,Vector3.forward);
            foreach(float z in new[]{94,97.8f,121.4f,125.7f})foreach(float x in new[]{-1.18f,1.98f})
                Pile(root,"선착장 해저 말뚝",new Vector3(x,0,z),DeckY+.12f,SeaBed,.14f);
            CoveredSpan(root,"해안 작업 쉼터",new Vector3(-2,0,73.8f),new Vector3(-2,0,82.9f),14,4.8f,false,false);
            CoveredSpan(root,"경사로 높은 외팔 천막",new Vector3(-.3f,0,82.7f),new Vector3(.1f,DeckY,93.2f),2.8f,4.4f,false,true);
            CoveredSpan(root,"가까운 선착장 높은 천막",new Vector3(.4f,DeckY,92.8f),new Vector3(.4f,DeckY,98.95f),5.2f,4.4f,false,false);
            CoveredSpan(root,"배 쪽 선착장 높은 천막",new Vector3(.4f,DeckY,120.45f),new Vector3(.4f,DeckY,123.3f),5.2f,4.4f,false,false);
            foreach(float x in new[]{-1.25f,2.05f})
            {
                Rail(root,"가까운 부두 난간",new Vector3(x,DeckY,93),new Vector3(x,DeckY,98.55f));
                Rail(root,"먼 부두 난간",new Vector3(x,DeckY,120.85f),new Vector3(x,DeckY,126.5f));
            }

            f.cradle=Group("깊이 내려가는 추 받침",root,CradleTop);
            f.cradleFloor=Deck(f.cradle,"추 받침 · 넓은 바닥",Vector3.zero,8,6,Vector3.forward).GetComponent<BoxCollider>();
            foreach(float x in new[]{-3.96f,3.96f})Box("추 받침 낮은 테두리",f.cradle,new Vector3(x,.045f,0),new Vector3(.08f,.09f,6.05f),orange,true);
            foreach(float z in new[]{-2.96f,2.96f})Box("추 받침 낮은 테두리",f.cradle,new Vector3(0,.045f,z),new Vector3(7.95f,.09f,.08f),orange,true);
            foreach(float x in new[]{-3.9f,3.9f})foreach(float z in new[]{-2.85f,2.85f})
                Box("추 받침 모서리 철물",f.cradle,new Vector3(x,-.10f,z),new Vector3(.18f,.35f,.2f),iron,true);
            Box("추 받침 중앙 철물",f.cradle,new Vector3(0,.01f,0),new Vector3(.65f,.02f,.65f),brass,false);

            f.bridge=Group("통째로 올라오는 긴 천막 다리",root,BridgeTop);
            f.bridgeFloor=Deck(f.bridge,"승선교 실제 보행 바닥",Vector3.zero,3.6f,21.8f,Vector3.forward).GetComponent<BoxCollider>();
            foreach(float x in new[]{-1.65f,1.65f})Rail(f.bridge,"인양 다리 난간",new Vector3(x,0,-10.86f),new Vector3(x,0,10.86f));
            foreach(float x in new[]{-1.75f,1.75f})Box("인양 다리 청록 가장자리",f.bridge,new Vector3(x,.015f,0),new Vector3(.10f,.035f,21.75f),paint,false);
            f.bridgeCanopyRenderers=CoveredSpan(f.bridge,"함께 인양되는 높은 천막",new Vector3(0,0,-10.9f),new Vector3(0,0,10.9f),5.2f,4.2f,true,false);
            f.bridgeNearPoint=new Vector3(.4f,DeckY+.025f,98.8f);
            f.bridgeFarPoint=new Vector3(.4f,DeckY+.025f,120.6f);

            var ropeStarts=new List<Transform>();var ropeEnds=new List<Transform>();var guides=new List<Transform>();var lines=new List<LineRenderer>();
            for(int i=0;i<2;i++)
            {
                float cradleZ=i==0?-2.5f:2.5f,bridgeZ=i==0?-10.1f:10.1f;
                var start=Group("추 쪽 인양 고리 "+i,f.cradle,new Vector3(3.75f,.16f,cradleZ));
                Torus("추 받침 단조 고리",start,Vector3.zero,.15f,.035f,iron,Quaternion.Euler(90,0,0));
                var end=Group("다리 지붕 인양 고리 "+i,f.bridge,new Vector3(3.55f,4.3f,bridgeZ));
                CylinderBetween("지붕 인양 횡보",f.bridge,new Vector3(-1.55f,4.3f,bridgeZ),end.localPosition,.09f,wood,true);
                Vector3 fixedA=new Vector3(CradleTop.x+3.75f,10.5f,CradleTop.z+cradleZ);
                Vector3 fixedB=new Vector3(BridgeTop.x+3.55f,10.5f,BridgeTop.z+bridgeZ);
                guides.Add(Group("고정 추 도르래 "+i,root,fixedA));guides.Add(Group("고정 다리 도르래 "+i,root,fixedB));
                Vector3 normal=Vector3.Cross(Vector3.up,(fixedB-fixedA).normalized).normalized;
                Pulley(root,fixedA,normal);Pulley(root,fixedB,normal);
                foreach(var p in new[]{fixedA+Vector3.right*1.4f,fixedB+Vector3.right*1.2f})
                {
                    Pile(root,"도르래 해저 고정 말뚝",p,10.9f,SeaBed,.18f);
                    CylinderBetween("도르래 상부 연결보",root,new Vector3(p.x,10.85f,p.z),p.x>9?fixedA:fixedB,.13f,wood,true);
                    Torus("도르래 고정 띠",root,new Vector3(p.x,1,p.z),.20f,.035f,iron,Quaternion.Euler(90,0,0));
                }
                CylinderBetween("인양 연결 대들보",root,fixedA+Vector3.up*.4f,fixedB+Vector3.up*.4f,.12f,wood,true);
                var line=Group("작동하는 인양 로프 "+i,root,Vector3.zero).gameObject.AddComponent<LineRenderer>();
                line.sharedMaterial=rope;line.useWorldSpace=true;line.positionCount=4;line.startWidth=line.endWidth=.045f;line.numCapVertices=3;line.numCornerVertices=3;line.generateLightingData=true;
                line.SetPositions(new[]{start.position,fixedA,fixedB,end.position});ropeStarts.Add(start);ropeEnds.Add(end);lines.Add(line);
            }
            f.ropeStartPoints=ropeStarts.ToArray();f.ropeEndPoints=ropeEnds.ToArray();f.ropeGuides=guides.ToArray();f.ropes=lines.ToArray();
            f.cradleRaisedPosition=f.cradle.position;f.bridgeRaisedPosition=f.bridge.position;
            f.rock=BuildRock(root,new Vector3(-4,.72f,78),1.6f);
            int stoneIndex=0;
            foreach(var natural in new[]{new Vector4(-7.8f,.2475f,75.5f,.55f),new Vector4(-5.6f,.1575f,80.8f,.35f),new Vector4(-8.8f,.18f,80.3f,.4f)})
            {
                var stone=BuildRock(root,new Vector3(natural.x,natural.y,natural.z),natural.w);
                stone.gameObject.name="해안의 자연 암석 "+stoneIndex++;UnityEngine.Object.DestroyImmediate(stone);
            }
            f.observationPoint=new Vector3(-8,.025f,81.5f);f.reviewLookDegrees=new Vector2(40,-1);
            f.pickupPoint=new Vector3(-6.2f,.025f,78);f.clearPoint=new Vector3(-2,.025f,80.3f);f.adjustmentPoint=new Vector3(3.5f,.025f,80.3f);
            f.routePoints=new[]{new Vector3(0,.025f,75),new Vector3(0,.025f,80.5f),new Vector3(0,.025f,82.7f),new Vector3(.4f,DeckY+.025f,93),new Vector3(.4f,DeckY+.025f,97.7f),f.bridgeNearPoint,new Vector3(.4f,DeckY+.025f,104),new Vector3(.4f,DeckY+.025f,113),f.bridgeFarPoint,new Vector3(.4f,DeckY+.025f,123.3f),new Vector3(.4f,DeckY+.025f,126.8f)};
            var lamps=new List<Renderer>();
            foreach(float x in new[]{-1.55f,1.55f})
            {
                CylinderBetween("인양 표시등 지지대",f.bridge,new Vector3(x,0,-10.5f),new Vector3(x,.6f,-10.5f),.035f,iron,true);
                lamps.Add(Sphere("인양 상태 표시등",f.bridge,new Vector3(x,.66f,-10.5f),Vector3.one*.13f,brass,false).GetComponent<Renderer>());
            }
            f.lamps=lamps.ToArray();
            Sign(root,"돌의 무게로 길을 인양합니다",new Vector3(1.7f,1.1f,82.4f),.09f,new Color(.92f,.77f,.48f));
            f.PrepareInitialGeometry();
            return f;
        }
        static void Pile(Transform parent,string name,Vector3 at,float top,float bottom,float radius)
        {
            Box(name,parent,new Vector3(at.x,(top+bottom)*.5f,at.z),new Vector3(radius*2,top-bottom,radius*2),wood,true);
            Box(name+" 해저 받침",parent,new Vector3(at.x,bottom+.15f,at.z),new Vector3(radius*3,.3f,radius*3),iron,true);
        }
        static Renderer[] CoveredSpan(Transform parent,string name,Vector3 a,Vector3 b,float width,float height,bool moving,bool westOnly)
        {
            var roofs=new List<Renderer>();int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/4));
            for(int n=0;n<count;n++)
            {
                Vector3 from=Vector3.Lerp(a,b,(float)n/count),to=Vector3.Lerp(a,b,(float)(n+1)/count),along=(to-from).normalized;
                from-=along*.18f;to+=along*.18f;
                var v=new Vector3[15];var uv=new Vector2[15];var tris=new List<int>();
                Vector3 across=Vector3.Cross(Vector3.up,along).normalized;
                for(int z=0;z<3;z++)for(int x=0;x<5;x++)
                {
                    float t=z*.5f,u=x*.25f,roofHeight=height+.50f*(1-Mathf.Abs(u*2-1));
                    v[z*5+x]=Vector3.Lerp(from,to,t)+across*((u-.5f)*width)+BeachLighting.ToSun*(roofHeight/BeachLighting.ToSun.y);uv[z*5+x]=new Vector2(u,t);
                    if(z<2&&x<4){int k=z*5+x;tris.AddRange(new[]{k,k+5,k+1,k+1,k+5,k+6});}
                }
                int frontCount=v.Length;var vertices=new Vector3[frontCount*2];var bothUv=new Vector2[frontCount*2];Array.Copy(v,vertices,frontCount);Array.Copy(v,0,vertices,frontCount,frontCount);Array.Copy(uv,bothUv,frontCount);Array.Copy(uv,0,bothUv,frontCount,frontCount);
                int indexCount=tris.Count;for(int t=0;t<indexCount;t+=3)tris.AddRange(new[]{tris[t]+frontCount,tris[t+2]+frontCount,tris[t+1]+frontCount});
                var mesh=new Mesh{name=name+" · 실제 높은 천막 · "+n};mesh.vertices=vertices;mesh.uv=bothUv;mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var roof=Group(name+" · 천막 "+n,parent,Vector3.zero).gameObject;roof.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=roof.AddComponent<MeshRenderer>();renderer.sharedMaterial=cloth;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;roof.AddComponent<MeshCollider>().sharedMesh=mesh;roofs.Add(renderer);
                foreach(float side in new[]{-.5f,.5f})
                {
                    foreach(var anchor in n==count-1?new[]{from,to}:new[]{from})
                    {
                    Vector3 corner=anchor+across*(side*width)+BeachLighting.ToSun*(height/BeachLighting.ToSun.y);
                    if(moving)
                    {
                        Vector3 foot=new Vector3(side<0?-1.55f:1.55f,-.25f,Mathf.Clamp(anchor.z,-10.65f,10.65f));
                        Vector3 top=new Vector3(foot.x,anchor.y+height,foot.z);
                        CylinderBetween(name+" · 다리에 박힌 기둥",parent,foot,top,.075f,wood,true);
                        CylinderBetween(name+" · 지붕 연결보",parent,top,corner,.07f,wood,true);
                    }
                    else if(!westOnly||side<0)
                    {
                        Vector3 post=corner;if(westOnly)post.x=-2.35f;
                        float bottom=post.z<82.99f?-.45f:SeaBed;
                        Pile(parent,name+" · 땅에 박힌 기둥",post,corner.y+.06f,bottom,.085f);
                        if(westOnly)
                        {
                            Vector3 opposite=anchor+across*(width*.5f)+BeachLighting.ToSun*(height/BeachLighting.ToSun.y);
                            CylinderBetween(name+" · 외팔 지붕 받침보",parent,new Vector3(post.x,corner.y,post.z),opposite,.075f,wood,true);
                        }
                    }
                    }
                    Vector3 edgeStart=from+across*(side*width)+BeachLighting.ToSun*(height/BeachLighting.ToSun.y);
                    Vector3 end=to+across*(side*width)+BeachLighting.ToSun*(height/BeachLighting.ToSun.y);
                    CylinderBetween(name+" · 지붕 가장자리",parent,edgeStart,end,.045f,wood,true);
                }
                CylinderBetween(name+" · 지붕 마룻대",parent,v[2],v[12],.045f,darkWood,true);
            }
            return roofs.ToArray();
        }
        static BeachProp BuildRock(Transform parent,Vector3 position,float scale)
        {
            var root=Group("04 · 항구의 돌 · 자유 원근 조작",parent,position);
            var source=Resources.Load<GameObject>("BottleBalance/Dike/SwordStone");
            if(source==null)throw new InvalidOperationException("Acquired CC0 Poly Haven rock_09 prefab is missing.");
            var model=UnityEngine.Object.Instantiate(source,root,false).transform;model.name="Poly Haven · rock_09 · coastal boulder";
            foreach(var c in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            var vertices=new List<Vector3>();Bounds bounds=new Bounds();bool first=true;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
                if(filter.sharedMesh!=null)foreach(var v in filter.sharedMesh.vertices)
                {
                    Vector3 p=root.InverseTransformPoint(filter.transform.TransformPoint(v));vertices.Add(p);
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            if(first)throw new InvalidOperationException("The acquired rock mesh is empty.");
            Vector3 normalization=new Vector3(1/bounds.size.x,.95f/bounds.size.y,.9f/bounds.size.z);
            Vector3 shift=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
            model.localScale=normalization;model.localPosition=Vector3.Scale(shift,normalization)-Vector3.up*.45f;
            for(int i=0;i<vertices.Count;i++)vertices[i]=Vector3.Scale(vertices[i]+shift,normalization)-Vector3.up*.45f;
            // Sliced boxes follow the real scanned rock silhouette, including its lower pivot.
            const int slices=10;float sliceHeight=.95f/slices;
            for(int n=0;n<slices;n++)
            {
                float lo=-.45f+n*sliceHeight,hi=lo+sliceHeight;Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
                foreach(var v in vertices)if(v.y>=lo-.035f&&v.y<=hi+.035f){min=Vector2.Min(min,new Vector2(v.x,v.z));max=Vector2.Max(max,new Vector2(v.x,v.z));}
                if(float.IsInfinity(min.x))continue;
                var collider=root.gameObject.AddComponent<BoxCollider>();
                collider.center=new Vector3((min.x+max.x)*.5f,(lo+hi)*.5f,(min.y+max.y)*.5f);
                collider.size=new Vector3(Mathf.Max(.035f,max.x-min.x),sliceHeight+.006f,Mathf.Max(.035f,max.y-min.y));
            }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            var prop=root.gameObject.AddComponent<BeachProp>();prop.title="해안 바위 · 자유 원근 조작";prop.zone=3;prop.minScale=.001f;prop.maxScale=float.PositiveInfinity;
            prop.buoyant=false;prop.upright=true;root.localScale=Vector3.one*scale;prop.Remember();return prop;
        }
        static Material Mat(string name,Color color,float metallic=0,float smooth=.16f)
        {
            var m=new Material(Shader.Find("Parallax/BeachSurface")){name=name,color=color};
            m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);m.EnableKeyword("_EMISSION");BeachWorld.Materials.Add(m);return m;
        }
        static Transform Group(string name,Transform parent,Vector3 position)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool solid)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static GameObject Sphere(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool solid)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static GameObject CylinderBetween(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material material,bool solid)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=(a+b)*.5f;
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);go.transform.localScale=new Vector3(radius*2,(b-a).magnitude*.5f,radius*2);go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static GameObject Deck(Transform parent,string name,Vector3 center,float width,float length,Vector3 direction)
        {
            var section=Group(name,parent,center);section.localRotation=Quaternion.LookRotation(direction,Vector3.up);
            var floor=Box(name+" · 연속 보행 충돌",section,new Vector3(0,-.14f,0),new Vector3(width,.28f,length),wood,true);
            floor.GetComponent<Renderer>().enabled=false;
            int count=Mathf.CeilToInt(length/.33f);
            for(int n=0;n<count;n++)
            {
                float z=-length*.5f+(n+.5f)*length/count;
                Box(name+" · 목재 바닥판",section,new Vector3(0,-.022f,z),new Vector3(width,.044f,length/count-.009f),wood,false);
                foreach(float x in new[]{-width*.40f,width*.40f})Rivet(section,new Vector3(x,.002f,z),.017f);
            }
            foreach(float x in new[]{-width*.35f,width*.35f})Box(name+" · 하부 받침보",section,new Vector3(x,-.29f,0),new Vector3(.16f,.20f,length),darkWood,true);
            return floor;
        }
        static void Span(Transform parent,string name,Vector3 a,Vector3 b,float width,bool rail)
        {
            Vector3 d=b-a;var t=Group(name,parent,(a+b)*.5f);t.localRotation=Quaternion.LookRotation(d,Vector3.up);
            Deck(t,name,Vector3.zero,width,d.magnitude,Vector3.forward);
            if(rail)foreach(float x in new[]{-width*.47f,width*.47f})Rail(t,name+" 난간",new Vector3(x,0,-d.magnitude*.5f),new Vector3(x,0,d.magnitude*.5f));
        }
        static void Rail(Transform parent,string name,Vector3 a,Vector3 b)
        {
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/2.5f));
            for(int n=0;n<=count;n++)
            {
                Vector3 p=Vector3.Lerp(a,b,(float)n/count);Box(name+" 기둥",parent,p+Vector3.up*.53f,new Vector3(.085f,1.06f,.085f),darkWood,true);
                Box(name+" 철제 띠",parent,p+Vector3.up*.32f,new Vector3(.10f,.10f,.10f),iron,true);
            }
            CylinderBetween(name+" 윗줄",parent,a+Vector3.up*.98f,b+Vector3.up*.98f,.022f,rope,true);
            CylinderBetween(name+" 아랫줄",parent,a+Vector3.up*.50f,b+Vector3.up*.50f,.018f,rope,true);
        }
        static void Rivet(Transform parent,Vector3 at,float radius)
        {Sphere("단조 철제 못",parent,at,new Vector3(radius*2,radius*.40f,radius*2),iron,false);}
        static void Pulley(Transform parent,Vector3 center,Vector3 normal)
        {
            var q=Quaternion.LookRotation(normal,Vector3.up);Torus("도르래 바퀴",parent,center,.30f,.065f,brass,q);
            CylinderBetween("도르래 축",parent,center-normal*.22f,center+normal*.22f,.09f,iron,true);
            foreach(int n in new[]{0,1,2,3}){float angle=n*Mathf.PI*.5f;Vector3 p=q*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*.25f;CylinderBetween("도르래 바퀴 살",parent,center,center+p,.035f,iron,false);}
        }
        static void Torus(string name,Transform parent,Vector3 at,float radius,float tube,Material mat,Quaternion rotation)
        {
            const int rings=24,sides=8;var verts=new Vector3[(rings+1)*(sides+1)];var tris=new List<int>();
            for(int n=0;n<=rings;n++)for(int j=0;j<=sides;j++)
            {
                float a=2*Mathf.PI*n/rings,b=2*Mathf.PI*j/sides,r=radius+tube*Mathf.Cos(b);
                verts[n*(sides+1)+j]=new Vector3(r*Mathf.Cos(a),r*Mathf.Sin(a),tube*Mathf.Sin(b));
                if(n<rings&&j<sides){int k=n*(sides+1)+j;tris.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});}
            }
            var m=new Mesh{name=name};m.vertices=verts;m.SetTriangles(tris,0);m.RecalculateNormals();m.RecalculateBounds();
            var t=Group(name,parent,at);t.localRotation=rotation;t.gameObject.AddComponent<MeshFilter>().sharedMesh=m;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }
        static void Sign(Transform parent,string text,Vector3 at,float size,Color color)
        {
            var t=Group("항구 안내 · "+text,parent,at);
            // Text faces the observation deck rather than appearing edge-on from the side.
            t.rotation=Quaternion.LookRotation(t.position-new Vector3(-8,1.575f,81.5f),Vector3.up);
            var label=t.gameObject.AddComponent<BeachLabel>();label.caption=text;label.size=size;label.color=color;
        }
    }
}
