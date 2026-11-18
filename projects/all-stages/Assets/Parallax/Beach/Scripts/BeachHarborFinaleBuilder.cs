using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ShadeLink.Beach
{
 // Visual shells are replaceable independently of the open compound cave body.
 public static class BeachHarborFinaleBuilder
 {
  public static readonly Vector3 MapOrigin=new Vector3(0,0,102);
  const float MapScale=.055f;
  static Material wood,canvas,rope,bronze,rock,sand,blue,leaf,coral;
  static Material Mat(string name,Color color)
  {
   var m=new Material(Shader.Find("Parallax/BeachSurface")){name="Harbor finale · "+name,color=color};m.SetFloat("_Glossiness",.13f);m.EnableKeyword("_EMISSION");BeachWorld.Materials.Add(m);return m;
  }
  public static BeachHarborFinale Build(Transform parent,BeachGame game)
  {
   var old=parent.GetComponentInChildren<BeachHarborFinale>(true);if(old!=null)return old;
   wood=Mat("weathered timber",new Color(.47f,.30f,.16f));canvas=Mat("tan canvas",new Color(.87f,.73f,.49f));rope=Mat("hemp rope",new Color(.66f,.51f,.31f));bronze=Mat("brass rim",new Color(.62f,.42f,.15f));
   rock=Mat("development grey rock",new Color(.43f,.46f,.43f));sand=Mat("island sand",new Color(.67f,.58f,.40f));blue=Mat("model lagoon",new Color(.08f,.43f,.53f));leaf=Mat("development palm leaves",new Color(.16f,.32f,.14f));coral=Mat("model handle",new Color(.78f,.36f,.16f));
   var root=Group("04 · Miniature harbor and perspective island",parent,Vector3.zero);var f=root.gameObject.AddComponent<BeachHarborFinale>();f.game=game;
   // Physical shallow seabed, not a solid water plane. A visitor standing here
   // remains below the true water-hazard surface and cannot walk across.
   // The common water builder supplies the real -2m seabed; the island base rests on it.
   BeachWorld.Water("항구의 잔잔한 수면",new Vector2(-14,83),new Vector2(14,125),Vector3.zero);
   Box("Departure bank · landing lip",root,new Vector3(-3,.12f,82.6f),new Vector3(8.8f,.44f,2.0f),wood,true);
   Box("Departure bank · low timber step",root,new Vector3(-3,.07f,81.05f),new Vector3(8.8f,.14f,.60f),wood,true);
   Box("Departure bank · middle timber step",root,new Vector3(-3,.21f,81.45f),new Vector3(8.8f,.14f,.70f),wood,true);
   Box("Ship harbor · high end dock",root,new Vector3(.2f,2.91f,122.25f),new Vector3(8.4f,.30f,2.2f),wood,true);
   PermanentShelter(root,new Vector3(-2,0,78.5f),new Vector2(10,10),3.6f);
   f.workbench=BuildTable(root);f.modelRoot=Group("Mini harbor · only pier controls",root,new Vector3(-4,1.15f,79.2f));f.modelRoot.localScale=Vector3.one*MapScale;
   f.islandStand=Group("Foreground island · illusion display stand",root,new Vector3(0,0,81));
   Box("Island display · wood pedestal",f.islandStand,new Vector3(0,.55f,0),new Vector3(.60f,1.10f,.60f),wood,true);
   Box("Island display · brass rim",f.islandStand,new Vector3(0,1.135f,0),new Vector3(.68f,.07f,.68f),bronze,true);
   f.island=BuildIsland(root,new Vector3(0,1.267f,81),.22f);
   f.cavePoints=new[]{new Vector3(0,.08f,-1.85f),new Vector3(0,.08f,-.65f),new Vector3(0,.08f,.50f),new Vector3(.65f,.08f,.50f),new Vector3(1.55f,.08f,.50f)};
   f.pierData=new[]{BuildPier(root,f.modelRoot,"A",new Vector3(-3,.34f,82.8f),new[]{Vector3.zero,new Vector3(0,0,10.375f)},-27),
    BuildPier(root,f.modelRoot,"B",new Vector3(.4f,3.06f,122.1f),new[]{Vector3.zero,new Vector3(8,0,0),new Vector3(8,-2.72f,-18.35f),new Vector3(3.575f,-2.72f,-18.35f)},-20)};
   var handles=new List<Collider>();var lamps=new List<Renderer>();
   for(int i=0;i<2;i++)
   {
    var h=Group("Mini harbor handle "+(i==0?"A":"B"),f.modelRoot,f.pierData[i].anchor-MapOrigin+new Vector3(i==0?-3:3,1.35f,0));
    var top=Box("Pier control · broad brass grip",h,Vector3.zero,new Vector3(3.7f,1.7f,3.7f),i==0?coral:bronze,true);handles.Add(top.GetComponent<BoxCollider>());lamps.Add(top.GetComponent<Renderer>());
    Label(h,i==0?"A":"B",new Vector3(0,1.35f,0),1.4f,Quaternion.Euler(75,0,0));
   }
   f.handleColliders=handles.ToArray();f.connectionLamps=lamps.ToArray();
   var cloths=new List<BeachHarborFinale.CanvasData>();
   for(int p=0;p<2;p++)
   {
    int segmentCount=f.pierData[p].points.Length-1;
    for(int order=0;order<segmentCount;order++)
    {
     int segment=p==0?order:segmentCount-1-order;var points=f.pierData[p].points;int count=Mathf.CeilToInt(Vector3.Distance(points[segment],points[segment+1])/3.2f);
     for(int n=0;n<count;n++)
     {
      int part=p==0?n:count-1-n;var obj=Box("Canvas · actual unfolding section "+cloths.Count,root,Vector3.zero,Vector3.one,canvas,false);
      var renderer=obj.GetComponent<Renderer>();renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.enabled=false;
      cloths.Add(new BeachHarborFinale.CanvasData{root=obj.transform,renderer=renderer,pier=p,segment=segment,from=(float)part/count,to=(float)(part+1)/count,reverse=p==1});
     }
    }
   }
   f.canvases=cloths.ToArray();
   Label(root,"항구 모형으로 부두를 움직여 보세요",new Vector3(-4,1.75f,77.55f),.18f,Quaternion.identity);
   Label(root,"섬 모형\n집어 멀리 놓으면 커져요",new Vector3(1.3f,1.25f,81),.07f,Quaternion.identity);
   return f;
  }
  static Transform BuildTable(Transform parent)
  {
   var table=Group("Harbor workbench · M01",parent,new Vector3(-4,0,79.2f));var source=Resources.Load<GameObject>("HarborIsland/Table");
   if(source!=null)
   {
    var model=UnityEngine.Object.Instantiate(source,table,false).transform;model.name="Visuals · selected M01 table";Bounds bounds=new Bounds();bool first=true;
    foreach(var filter in model.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null)
     for(int i=0;i<8;i++)
     {
      Bounds b=filter.sharedMesh.bounds;Vector3 v=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
      Vector3 p=model.InverseTransformPoint(filter.transform.TransformPoint(v));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
     }
    if(!first){Vector3 s=new Vector3(2.8f/bounds.size.x,1.05f/bounds.size.y,2.8f/bounds.size.z);model.localScale=s;model.localPosition=-Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),s);}
    foreach(var c in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
   }
   else
   {
    Box("Visuals · development table top",table,new Vector3(0,1.0f,0),new Vector3(2.8f,.12f,2.8f),wood,false);
    foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-1.1f,1.1f})Box("Visuals · table leg",table,new Vector3(x,.48f,z),new Vector3(.15f,.96f,.15f),wood,false);
   }
   var physical=Box("Workbench · physical counter",table,new Vector3(0,.98f,0),new Vector3(2.8f,.16f,2.8f),wood,true);physical.GetComponent<Renderer>().enabled=false;
   Box("Mini harbor · lagoon tray",table,new Vector3(0,1.09f,0),new Vector3(2.72f,.06f,2.72f),blue,true);
   foreach(float x in new[]{-1.40f,1.40f})Box("Workbench · brass tray edge",table,new Vector3(x,1.15f,0),new Vector3(.06f,.14f,2.85f),bronze,true);
   foreach(float z in new[]{-1.40f,1.40f})Box("Workbench · brass tray edge",table,new Vector3(0,1.15f,z),new Vector3(2.85f,.14f,.06f),bronze,true);return table;
  }
  static BeachProp BuildIsland(Transform parent,Vector3 position,float scale)
  {
   var root=Group("04 · 가까운 섬 모형 · 자유 원근 조형물",parent,position);var physical=Group("Physics · open L cave compound",root,Vector3.zero);
   Box("Island · continuous cave floor",physical,new Vector3(.15f,-.18f,-.15f),new Vector3(2.8f,.52f,3.4f),sand,true);
   Box("Island · west cave wall",physical,new Vector3(-.78f,.57f,-.25f),new Vector3(.50f,1.14f,3.0f),rock,true);
   Box("Island · east front wall",physical,new Vector3(.78f,.57f,-.90f),new Vector3(.50f,1.14f,1.2f),rock,true);
   Box("Island · north cave wall",physical,new Vector3(.35f,.57f,1.02f),new Vector3(2.8f,1.14f,.42f),rock,true);
   Box("Island · exit lower jamb",physical,new Vector3(1.04f,.57f,-.01f),new Vector3(.65f,1.14f,.33f),rock,true);
   Box("Island · rock ceiling",physical,new Vector3(.10f,1.12f,-.22f),new Vector3(2.65f,.56f,3.15f),rock,true);
   // The actual interior floor and ceiling are rendered. Their real meshes,
   // not an invisible safety proxy, supply the walkable surface and cave shade.
   foreach(var r in physical.GetComponentsInChildren<Renderer>())
    r.enabled=r.name=="Island · continuous cave floor"||r.name=="Island · rock ceiling";
   var visual=Group("Visuals · replaceable development rock shell",root,Vector3.zero);
   for(int n=0;n<3;n++){Rock(visual,new Vector3(-1.02f,.57f,-1.15f+n*.95f),new Vector3(.87f,1.40f,1.22f),n);Rock(visual,new Vector3(-.4f+n*.9f,.7f,1.20f),new Vector3(1.2f,1.5f,.80f),n+5);}
   Rock(visual,new Vector3(.96f,.57f,-1.08f),new Vector3(.82f,1.43f,1.1f),11);Rock(visual,new Vector3(1.13f,.55f,-.04f),new Vector3(.9f,1.2f,.30f),12);
   Rock(visual,new Vector3(.05f,1.26f,-.75f),new Vector3(2.65f,.82f,1.90f),16);Rock(visual,new Vector3(.18f,1.28f,.55f),new Vector3(2.7f,.85f,1.7f),17);Rock(visual,new Vector3(.10f,-.25f,-.15f),new Vector3(2.9f,.60f,3.5f),18);
   if(!FitSelected("HarborIsland/Palm01",visual,"Visuals · selected P01 palm",new Vector3(-.38f,2.08f,.23f),new Vector3(1.25f,1.35f,1.25f)))
   {
   var palm=Group("Visuals · development palm",visual,new Vector3(-.38f,1.48f,.23f));
   for(int n=0;n<5;n++)Box("Palm trunk segment",palm,new Vector3(n*.014f,.08f+n*.16f,0),new Vector3(.10f,.18f,.10f),wood,false);
   for(int n=0;n<7;n++){var frond=Group("Palm frond",palm,new Vector3(.065f,.87f,0));frond.localRotation=Quaternion.Euler(17,n*51,0);Box("Palm leaf",frond,new Vector3(0,0,.26f),new Vector3(.16f,.025f,.60f),leaf,false);}
   }
   var prop=root.gameObject.AddComponent<BeachProp>();prop.title="섬 모형 · 집어서 실제 지형으로 키우기";prop.zone=3;prop.minScale=.001f;prop.maxScale=float.PositiveInfinity;prop.platform=true;prop.buoyant=false;prop.upright=true;root.localScale=Vector3.one*scale;prop.Remember();return prop;
  }
  static BeachHarborFinale.PierData BuildPier(Transform parent,Transform map,string name,Vector3 anchor,Vector3[] points,float yaw)
  {
   var root=Group("Real pier "+name,parent,anchor);var segments=new List<Transform>();
   for(int n=0;n+1<points.Length;n++){float length=Vector3.Distance(points[n],points[n+1]);var part=Group("Pier "+name+" · span "+n,root,points[n]);part.localRotation=Quaternion.LookRotation(points[n+1]-points[n]);DeckGeometry(part,length);segments.Add(part);}
   var copy=UnityEngine.Object.Instantiate(root.gameObject,map,false).transform;copy.name="Miniature pier "+name;foreach(var c in copy.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
   copy.localPosition=anchor-MapOrigin;copy.localRotation=Quaternion.identity;var models=new Transform[segments.Count];for(int n=0;n<models.Length;n++)models[n]=copy.GetChild(n);
   root.rotation=Quaternion.Euler(0,yaw,0);copy.localRotation=root.rotation;
   return new BeachHarborFinale.PierData{root=root,model=copy,anchor=anchor,points=(Vector3[])points.Clone(),homePoints=(Vector3[])points.Clone(),homeYaw=yaw,yaw=yaw,segments=segments.ToArray(),modelSegments=models};
  }
  static void DeckGeometry(Transform parent,float length)
  {
   var floor=Box("Pier · continuous walking surface",parent,new Vector3(0,-.09f,length*.5f),new Vector3(2.35f,.18f,length),wood,true);floor.GetComponent<Renderer>().enabled=false;
   int boards=Mathf.CeilToInt(length/.43f);for(int n=0;n<boards;n++)Box("Pier · timber plank",parent,new Vector3(0,.006f,(n+.5f)*length/boards),new Vector3(2.42f,.045f,length/boards-.018f),wood,false);
   int posts=Mathf.CeilToInt(length/2.8f);
   for(int n=0;n<=posts;n++)
   {
    float z=n*length/posts;
    foreach(float x in new[]{-1.13f,1.13f}){Box("Pier · slim timber post",parent,new Vector3(x,1.52f,z),new Vector3(.105f,3.24f,.105f),wood,false);if(n<posts)Line("Pier · hemp handrail",parent,new Vector3(x,1,z),new Vector3(x,1,z+length/posts),.035f,rope);}
    Line("Pier · awning crossbar",parent,new Vector3(-2.4f,3.32f,z),new Vector3(2.4f,3.32f,z),.05f,wood);
   }
  }
  static void PermanentShelter(Transform parent,Vector3 floor,Vector2 size,float height)
  {
   Vector3 offset=BeachLighting.ToSun*(height/BeachLighting.ToSun.y);offset.y=0;Box("Workbench · actual permanent canvas shade",parent,floor+Vector3.up*height+offset,new Vector3(size.x,.07f,size.y),canvas,false);
   foreach(float x in new[]{-size.x*.48f,size.x*.48f})foreach(float z in new[]{-size.y*.48f,size.y*.48f})Box("Workbench shade · support",parent,floor+offset+new Vector3(x,height*.5f,z),new Vector3(.14f,height,.14f),wood,true);
  }
  static void Rock(Transform parent,Vector3 at,Vector3 size,int seed)
  {
   string resource="HarborIsland/Rock"+((seed%7)+1).ToString("00");
   if(FitSelected(resource,parent,"Visuals · selected C01 rock "+seed,at,size)||
      resource!="HarborIsland/Rock01"&&FitSelected("HarborIsland/Rock01",parent,"Visuals · selected C01 rock "+seed,at,size))return;
   var go=Group("Development rock · rounded shell "+seed,parent,at).gameObject;go.transform.localScale=size;go.AddComponent<MeshFilter>().sharedMesh=RockMesh(seed);var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=rock;r.shadowCastingMode=ShadowCastingMode.TwoSided;
  }
  static bool FitSelected(string key,Transform parent,string name,Vector3 at,Vector3 size)
  {
   var source=Resources.Load<GameObject>(key);if(source==null)return false;
   var holder=Group(name,parent,at);var model=UnityEngine.Object.Instantiate(source,holder,false).transform;Bounds bounds=new Bounds();bool first=true;
   foreach(var f in model.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null)for(int n=0;n<8;n++)
   {
    var b=f.sharedMesh.bounds;var v=b.center+Vector3.Scale(b.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1));
    var p=holder.InverseTransformPoint(f.transform.TransformPoint(v));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
   }
   if(!first){var scale=new Vector3(size.x/Mathf.Max(.001f,bounds.size.x),size.y/Mathf.Max(.001f,bounds.size.y),size.z/Mathf.Max(.001f,bounds.size.z));model.localScale=Vector3.Scale(model.localScale,scale);model.localPosition=-Vector3.Scale(bounds.center,scale);}
   foreach(var c in holder.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);return true;
  }
  static Mesh RockMesh(int seed)
  {
   const int rings=10,sides=16;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
   for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++){float p=Mathf.PI*y/rings,a=2*Mathf.PI*x/sides,noise=1+.035f*Mathf.Sin(x*2.31f+y*3.17f+seed);v.Add(new Vector3(Mathf.Sin(p)*Mathf.Cos(a),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(a))*.5f*noise);uv.Add(new Vector2((float)x/sides,(float)y/rings));}
   for(int y=0;y<rings;y++)for(int x=0;x<sides;x++){int a=y*(sides+1)+x,b=a+sides+1;t.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
   var mesh=new Mesh{name="Harbor development rounded rock "+seed};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
  static Transform Group(string name,Transform parent,Vector3 at){var go=new GameObject(name).transform;go.SetParent(parent,false);go.localPosition=at;return go;}
  static GameObject Box(string name,Transform parent,Vector3 at,Vector3 size,Material material,bool solid)
  {
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
  }
  static void Line(string name,Transform parent,Vector3 a,Vector3 b,float width,Material material){Vector3 d=b-a;var o=Box(name,parent,(a+b)*.5f,new Vector3(width,width,d.magnitude),material,false);o.transform.localRotation=Quaternion.LookRotation(d);}
  static void Label(Transform parent,string caption,Vector3 at,float size,Quaternion rotation){var n=Group("Harbor label · "+caption,parent,at);n.localRotation=rotation;var l=n.gameObject.AddComponent<BeachLabel>();l.caption=caption;l.size=size;l.color=new Color(.94f,.84f,.60f);}
 }
}
