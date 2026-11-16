using System;
using System.Collections.Generic;
using UnityEngine;
namespace ShadeLink.Beach
{
 public sealed class BeachHarborFinale:MonoBehaviour
 {
  [Serializable] public sealed class PierData
  {
   public Transform root,model; public Transform[] segments,modelSegments;
   public Vector3 anchor; public Vector3[] points,homePoints; public Vector2 offset; public float yaw,homeYaw;
  }
  [Serializable] public sealed class CanvasData
  {
   public Transform root; public Renderer renderer; public int pier,segment; public float from,to; public bool reverse;
  }
  public BeachGame game; public BeachProp island; public Transform modelRoot,workbench,islandStand;
  public Collider[] handleColliders; public PierData[] pierData; public CanvasData[] canvases; public Renderer[] connectionLamps;
  public Vector3[] cavePoints; public Vector3 islandSolutionPosition=new Vector3(-3,0,101.5f); public float islandSolutionScale=4.5f;
  [SerializeField] float stableSeconds,canopyProgress; [SerializeField] bool geometryFrozen,ready;
  int selected=-1; float selectedAt; bool initialized,shadowRebuilt; RigidbodyConstraints islandConstraints;
  public BeachProp Island=>island; public bool Ready=>ready; public bool GeometryFrozen=>geometryFrozen;
  public Vector3 LastShadeFailure{get;private set;} public string LastRouteFailure{get;private set;}
  public bool Unfurling=>geometryFrozen&&!ready; public bool Manipulating=>selected>=0; public int SelectedHandle=>selected;
  public bool ConnectionsMatched{get;private set;} public float StableProgress=>Mathf.Clamp01(stableSeconds);
  public float CanopyProgress=>canopyProgress; public float[] ConnectionErrors{get;private set;}=new float[2];
  public float[] NearContactClearances{get;private set;}=new float[2];
  public Vector3 IslandSolutionPosition=>islandSolutionPosition; public float IslandSolutionScale=>islandSolutionScale;
  public Vector2[] ModelSolutionOffsets=>new[]{Vector2.zero,Vector2.zero}; public float[] ModelSolutionYaws=>new[]{0f,0f};
  public Vector3 ObservationPoint=>new Vector3(0,0,77.6f); public Vector3 PickupPoint=>new Vector3(0,0,80.1f);
  public Transform[] Piers=>new[]{pierData[0].root,pierData[1].root}; public Transform[] BridgeModels=>new[]{pierData[0].model,pierData[1].model};
  public Renderer[] CanopyRenderers=>Array.ConvertAll(canvases,c=>c.renderer);
  public Vector3[] CaveEntrances=>new[]{island.transform.TransformPoint(cavePoints[0]),island.transform.TransformPoint(cavePoints[cavePoints.Length-1])};
  public Vector3[] CaveWaypoints=>Array.ConvertAll(cavePoints,p=>island.transform.TransformPoint(p));
  public bool AssetsAvailable=>Resources.Load<GameObject>("HarborIsland/Table")!=null&&Resources.Load<GameObject>("HarborIsland/Rock01")!=null&&Resources.Load<GameObject>("HarborIsland/Palm01")!=null;
  public string AssetsMessage=>AssetsAvailable?"Selected source prefabs available":"Development geometry. Selected C01/P01 sources are not installed; M01 table is used when available.";
  public string Status=>ready?"천막 아래 길이 이어졌어요 · 큰 배 갑판에서 E 출항":geometryFrozen?"천막 전개 중 · "+Mathf.RoundToInt(canopyProgress*100)+"%":
   Manipulating?"부두 "+(selected==0?"A":"B")+" · WASD/드래그 위치 · Q/E/휠 회전 · 클릭 마침":
   game!=null&&game.held==island?"섬도 일반 사물처럼 원근으로 커져요 · 가운데 바다에 내려놓으세요":
   ConnectionsMatched?"양쪽 접속이 맞아요 · 섬이 안정될 때까지 잠깐 기다리세요":"가까운 섬을 키우고 · 항구 모형으로 두 부두를 연결하세요";
  public string Hint=>"모형 손잡이를 클릭하면 실제 부두가 함께 움직여요.\n섬은 자유 사물이에요. 가까이 잡아 먼 지면에 크게 놓으세요.\n남쪽 입구와 오른쪽 출구를 잇고 천막 아래로 승선하세요.";
  public Vector3[] RouteWaypoints
  {
   get{var p=new List<Vector3>();if(pierData==null||island==null)return p.ToArray();foreach(var q in WorldPoints(0))p.Add(q+Vector3.up*.025f);
    foreach(var q in CaveWaypoints)p.Add(q+Vector3.up*.025f);var b=WorldPoints(1);for(int i=b.Length-1;i>=0;i--)p.Add(b[i]+Vector3.up*.025f);
    p.Add(new Vector3(.4f,3.085f,123));p.Add(new Vector3(.4f,3.085f,126.8f));return p.ToArray();}
  }
  public static BeachHarborFinale Build(BeachGame g)=>BeachHarborFinaleBuilder.Build(g.transform,g);
  public void Initialize()
  {
   if(initialized)return;initialized=true;if(game==null)game=GetComponentInParent<BeachGame>();
   if(island.body==null)island.InitializePhysics();islandConstraints=island.body.constraints;ResetPuzzle();Debug.Log("HARBOR_FINALE_INITIALIZED "+AssetsMessage);
  }
  public void ResetPuzzle()
  {
   CancelInteraction();ready=geometryFrozen=ConnectionsMatched=shadowRebuilt=false;stableSeconds=canopyProgress=0;
   if(island!=null&&island.body!=null){island.body.constraints=islandConstraints;island.Restore();}
   for(int i=0;i<pierData.Length;i++){var p=pierData[i];p.offset=Vector2.zero;p.yaw=p.homeYaw;p.points=(Vector3[])p.homePoints.Clone();ApplyPier(i);}
   foreach(var c in canvases){c.renderer.enabled=false;c.root.localScale=new Vector3(4.8f,.045f,.001f);}SetLamps(false);game?.lighting?.Synchronize();
  }
  public void CancelInteraction(){selected=-1;}
  public bool HasAimTarget(Camera camera)=>FindHandle(camera)>=0;
  public string AimText(Camera camera){int i=FindHandle(camera);return i<0?null:"[클릭] 모형 부두 "+(i==0?"A":"B")+" 조절";}
  int FindHandle(Camera camera)
  {
   if(camera==null||geometryFrozen||handleColliders==null)return -1;
   if(!Physics.Raycast(camera.ViewportPointToRay(new Vector3(.5f,.5f,0)),out var h,6,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore))return -1;
   for(int i=0;i<handleColliders.Length;i++)if(h.collider==handleColliders[i])return i;return -1;
  }
  public bool TryInteract(Camera camera)
  {
   if(Manipulating){CancelInteraction();return true;}
   if(geometryFrozen&&game!=null&&game.aimed==island){game.Tell("접속 걸쇠가 섬과 부두를 고정하고 있어요. R로 다시 시작할 수 있어요.");return true;}
   int i=FindHandle(camera);if(i<0||game==null||game.held!=null||!game.active||game.won)return false;
   selected=i;selectedAt=Time.unscaledTime;game.Tell("WASD/드래그: 위치 · Q/E/휠: 회전 · 클릭: 마침",5);return true;
  }
  public void HandleInput(float dt)
  {
   if(!Manipulating||game==null||!game.active||geometryFrozen)return;
   if(Input.GetKeyDown(KeyCode.Backspace)||Input.GetKeyDown(KeyCode.Return)||Input.GetMouseButtonDown(1)||Input.GetMouseButtonDown(0)&&Time.unscaledTime-selectedAt>.15f){CancelInteraction();return;}
   var p=pierData[selected];Vector2 m=new Vector2((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0))*Mathf.Max(0,dt)*1.65f;
   if(Input.GetMouseButton(0))m+=new Vector2(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y"))*.13f;
   float turn=((Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0))*42*dt+Input.mouseScrollDelta.y*3;SetModelControl(selected,p.offset+m,p.yaw+turn);
  }
  public bool SetModelControl(int index,Vector2 offset,float yawDegrees)
  {
   if(geometryFrozen||index<0||index>=pierData.Length||float.IsNaN(offset.x)||float.IsNaN(offset.y)||float.IsNaN(yawDegrees)||float.IsInfinity(offset.x)||float.IsInfinity(offset.y)||float.IsInfinity(yawDegrees))return false;
   var p=pierData[index];p.offset=new Vector2(Mathf.Clamp(offset.x,-3,3),Mathf.Clamp(offset.y,-.45f,.45f));p.yaw=Mathf.Clamp(yawDegrees,-38,38);ApplyPier(index);return true;
  }
  void ApplyPier(int index)
  {
   var p=pierData[index];p.root.position=p.anchor+new Vector3(p.offset.x,0,p.offset.y);p.root.rotation=Quaternion.Euler(0,p.yaw,0);
   UpdateNearEndpoint(index);
   ApplySegments(p.segments,p.points,p.homePoints);p.model.localPosition=p.root.position-BeachHarborFinaleBuilder.MapOrigin;p.model.localRotation=p.root.rotation;ApplySegments(p.modelSegments,p.points,p.homePoints);Physics.SyncTransforms();
  }
  bool UpdateNearEndpoint(int index)
  {
   if(geometryFrozen)return false;
   var p=pierData[index];int last=p.points.Length-1;Vector3 target=p.homePoints[last];NearContactClearances[index]=0;
   if(island!=null&&island.placement!=null&&island.placement.Ready&&!island.placement.Held&&(game==null||game.held!=island)&&Vector3.Angle(island.transform.up,Vector3.up)<=9)
   {
    // Eligibility always uses the authored endpoint, never a previous snapped
    // endpoint, so repeated updates cannot extend the magnetic range.
    Vector3 nominal=p.root.TransformPoint(p.homePoints[last]),port=CaveEntrances[index],delta=port-nominal;
    Vector3 inward=Vector3.ProjectOnPlane(nominal-p.root.TransformPoint(p.homePoints[last-1]),Vector3.up).normalized;
    Vector3 outward=index==0?-island.transform.forward:island.transform.right;
    Vector3 wanted=Vector3.ProjectOnPlane(-outward,Vector3.up).normalized;
    if(new Vector2(delta.x,delta.z).magnitude<=1.25f&&Mathf.Abs(delta.y)<=.62f&&Vector3.Angle(inward,wanted)<=34)
    {
     // Keep the entire tilted deck end outside the island plane, including
     // its projected half-width and full thickness, until completion locks it.
     Vector3 previous=p.root.TransformPoint(p.homePoints[last-1]);float gap=.06f;
     for(int pass=0;pass<3;pass++)
     {
      Vector3 d=(port+outward*gap-previous).normalized,right=Vector3.Cross(Vector3.up,d).normalized,spanUp=Vector3.Cross(d,right);
      gap=.06f+1.22f*Mathf.Abs(Vector3.Dot(right,outward))+.18f*Mathf.Abs(Vector3.Dot(spanUp,outward));
     }
     NearContactClearances[index]=gap;target=p.root.InverseTransformPoint(port+outward*gap);
    }
   }
   if((p.points[last]-target).sqrMagnitude<.00000001f)return false;
   p.points[last]=target;return true;
  }
  static void ApplySegments(Transform[] segments,Vector3[] points,Vector3[] original)
  {
   for(int n=0;n<segments.Length;n++){Vector3 d=points[n+1]-points[n];float nominal=Vector3.Distance(original[n],original[n+1]);segments[n].localPosition=points[n];segments[n].localRotation=Quaternion.LookRotation(d,Vector3.up);segments[n].localScale=new Vector3(1,1,d.magnitude/nominal);}
  }
  public Vector3[] WorldPoints(int index){var p=pierData[index];return Array.ConvertAll(p.points,q=>p.root.TransformPoint(q));}
  public bool CheckConnections()
  {
   if(island==null||island.placement==null||!island.placement.Ready||game.held==island||Vector3.Angle(island.transform.up,Vector3.up)>9)return false;
   var ports=CaveEntrances;var outward=new[]{-island.transform.forward,island.transform.right};bool valid=true;
   for(int i=0;i<2;i++){var p=WorldPoints(i);Vector3 end=p[p.Length-1],d=ports[i]-end;ConnectionErrors[i]=new Vector2(d.x,d.z).magnitude;
    Vector3 inward=Vector3.ProjectOnPlane(end-p[p.Length-2],Vector3.up).normalized,want=Vector3.ProjectOnPlane(-outward[i],Vector3.up).normalized;
    valid&=StartBankSupported(i)&&ConnectionErrors[i]<=1.25f&&Mathf.Abs(d.y)<=.62f&&Vector3.Angle(inward,want)<=34;}
   return valid;
  }
  bool StartBankSupported(int index)
  {
   var points=WorldPoints(index);Vector3 direction=Vector3.ProjectOnPlane(points[1]-points[0],Vector3.up).normalized;
   Vector3 right=Vector3.Cross(Vector3.up,direction),behind=points[0]-direction*.30f;
   foreach(float side in new[]{-.60f,0,.60f})
   {
    bool supported=false;
    foreach(var hit in Physics.RaycastAll(behind+right*side+Vector3.up*.15f,Vector3.down,.38f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore))
     if(!hit.transform.IsChildOf(pierData[index].root)&&hit.collider.GetComponentInParent<BeachProp>()==null&&Vector3.Dot(hit.normal,Vector3.up)>.65f){supported=true;break;}
    if(!supported)return false;
   }
   return true;
  }
  public void Tick(float dt)
  {
   if(!initialized)Initialize();if(game==null||!game.active||game.won)return;dt=Mathf.Clamp(dt,0,.05f);
   if(!geometryFrozen)
   {
    // Keep static pier tips outside the resting island before the one-second
    // connection test; the island itself remains governed by normal physics.
    for(int i=0;i<pierData.Length;i++)if(UpdateNearEndpoint(i))ApplyPier(i);
    ConnectionsMatched=CheckConnections();SetLamps(ConnectionsMatched);stableSeconds=ConnectionsMatched?stableSeconds+dt:0;if(stableSeconds>=1)FreezeConnections();return;
   }
   canopyProgress=Mathf.Min(1,canopyProgress+dt/2.8f);
   for(int i=0;i<canvases.Length;i++){float phase=Mathf.Clamp01(canopyProgress*1.25f-(float)i/canvases.Length*.25f);PositionCanvas(canvases[i],Mathf.SmoothStep(0,1,phase));}
   game.lighting.Synchronize();
   if(canopyProgress>=1&&!ready&&RouteShadeContinuous()){ready=true;game.Tell("천막 아래 항로가 이어졌어요. 큰 배에 올라 E로 마지막 항해를 시작하세요.",7);Debug.Log("HARBOR_FINALE_READY actualCanvas=1 actualShadow=1");}
  }
  void FreezeConnections()
  {
   geometryFrozen=true;CancelInteraction();var ports=CaveEntrances;
   // Lock the completed island before closing the small pre-lock contact gap.
   island.body.isKinematic=true;island.body.useGravity=false;island.body.constraints=RigidbodyConstraints.FreezeAll;
   for(int i=0;i<2;i++){var p=pierData[i];p.points[p.points.Length-1]=p.root.InverseTransformPoint(ports[i]);ApplyPier(i);}
   foreach(var c in canvases){c.renderer.enabled=true;PositionCanvas(c,0);}game.lighting.Rebuild();shadowRebuilt=true;game.Tell("걸쇠가 맞물렸어요. 로프를 따라 천막이 펼쳐집니다.",4);
  }
  void PositionCanvas(CanvasData c,float fraction)
  {
   var p=WorldPoints(c.pier);
   Vector3 a=Vector3.Lerp(p[c.segment],p[c.segment+1],c.from),b=Vector3.Lerp(p[c.segment],p[c.segment+1],c.to);
   bool mouth=c.segment==p.Length-2&&c.to>.999f;
   if(c.reverse){var q=a;a=b;b=q;}
   Vector3 direction=(b-a).normalized;
   // Pier A unfolds into the south mouth at its final end. Reversed pier B
   // starts at the east mouth, so it needs a trailing (start-side) overlap.
   float before=mouth&&c.pier==1?1.65f:0;
   float after=mouth&&c.pier==0?1.85f:.65f;
   float distance=Vector3.Distance(a,b);a-=direction*before;
   float length=Mathf.Max(.001f,(distance+before+after)*fraction);
   float height=3.25f;
   if(mouth)height=Mathf.Min(height,Mathf.Max(2.1f,island.transform.lossyScale.y*.76f-.20f));
   float width=mouth?3.0f:4.8f;
   Vector3 offset=BeachLighting.ToSun*(height/BeachLighting.ToSun.y);offset.y=0;
   c.root.SetPositionAndRotation(a+direction*(length*.5f)+Vector3.up*height+offset,Quaternion.LookRotation(direction,Vector3.up));
   c.root.localScale=new Vector3(width,.055f,length);
  }
  public bool RouteShadeContinuous()
  {
   if(!geometryFrozen||!shadowRebuilt||game?.lighting==null)return false;
   foreach(var p in SampleRoute(RouteWaypoints,.4f))foreach(float side in new[]{-.22f,0,.22f})
   {
    Vector3 q=p+Vector3.right*side;
    if(BeachWater.InHazard(q)){LastShadeFailure=q;LastRouteFailure="Water hazard";return false;}
    if(!Physics.Raycast(q+Vector3.up*.20f,Vector3.down,out var hit,.55f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)||
       hit.collider==game.controller||Vector3.Dot(hit.normal,Vector3.up)<.65f)
    {LastShadeFailure=q;LastRouteFailure="Missing walkable physical floor";return false;}
    if(game.lighting.IsSunlit(q,Vector3.up)){LastShadeFailure=q;LastRouteFailure="Direct or reflected sunlight";return false;}
   }
   LastRouteFailure=null;return true;
  }
  public static IEnumerable<Vector3> SampleRoute(Vector3[] route,float spacing)
  {
   for(int n=0;n+1<route.Length;n++){int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(route[n],route[n+1])/spacing));for(int i=0;i<count;i++)yield return Vector3.Lerp(route[n],route[n+1],(float)i/count);}if(route.Length>0)yield return route[route.Length-1];
  }
  void SetLamps(bool matched)
  {
   if(connectionLamps==null)return;var block=new MaterialPropertyBlock();block.SetColor("_EmissionColor",matched?new Color(.03f,.28f,.15f):Color.black);foreach(var lamp in connectionLamps)if(lamp!=null)lamp.SetPropertyBlock(block);
  }
  void OnDisable(){CancelInteraction();}
 }
}
