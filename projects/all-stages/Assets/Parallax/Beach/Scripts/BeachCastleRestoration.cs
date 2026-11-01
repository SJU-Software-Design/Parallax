using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Beach
{
    // Two missing front wall sections, each restored once. The original castle
    // is the route; no external climbing loop or duplicated replacement exists.
    public sealed class BeachCastleRestoration : MonoBehaviour
    {
        public static readonly Vector3 LowerStart=new Vector3(0,1.1f,89.0f),Midpoint=new Vector3(0,3.44f,91.7f);
        public static readonly Vector3 UpperStart=Midpoint,Summit=new Vector3(0,6.7f,95.4f);
        public static readonly Vector3 LowerPose=new Vector3(0,2.27f,90.5f),UpperPose=new Vector3(0,5.07f,93.55f);
        public BeachGame game;
        public BeachProp LowerFragment,UpperFragment,Parasol;
        public Transform CastleRoot,FixedParasol;
        public BoxCollider SummitFloor,CrownCutFace;
        public Renderer[] JointRenderers,CanopyRenderers;
        public Renderer[] ParasolRenderers=>Parasol.renderers;
        public Vector3[] ApproachRoute;
        public bool FirstRestored,SecondRestored,AtMidpoint,ReachedEntrance;
        public bool LowerConnected,UpperConnected;
        bool walkedLower,walkedUpper;
        readonly RaycastHit[] supportHits=new RaycastHit[128];
        public string Status
        {
            get
            {
                if(SecondRestored)return "성벽 복구 완료 · 정상 도착";
                if(game.player!=null&&IsAtSummit(game.player.feet))
                {
                    if(!FirstRestored||!walkedUpper)return "정상 도착 · 두 성벽 조각으로 올라온 길을 먼저 완성하세요";
                    if(!game.safe)return "정상 도착 · 발밑까지 파라솔 그늘을 이어 주세요";
                }
                return FirstRestored?"평평한 성벽 위에 파라솔을 놓고, 두 번째 조각으로 정상까지":"정면의 빠진 성벽을 작은 계단 조각으로 복구하세요";
            }
        }
        public bool IsFixed(BeachProp prop)=>(prop==LowerFragment&&FirstRestored)||(prop==UpperFragment&&SecondRestored);
        public void PiecePickedUp(BeachProp prop)
        {
            if(prop==LowerFragment&&!FirstRestored)walkedLower=false;
            if(prop==UpperFragment&&!SecondRestored)walkedUpper=false;
        }
        public void Restore()
        {
            FirstRestored=SecondRestored=AtMidpoint=ReachedEntrance=walkedLower=walkedUpper=false;
            LowerConnected=UpperConnected=false;
        }
        public void RecoverFragment()
        {
            if(!ReachedEntrance&&!FirstRestored)return;
            if(!FirstRestored){LowerFragment.Restore();walkedLower=false;return;}
            // Completed lower masonry stays in the castle. Only an unfinished
            // upper attempt is returned to its own reachable display plinth.
            walkedUpper=false;if(!UpperConnected)UpperFragment.Restore();
            float parasolFoot=Parasol.transform.TransformPoint(Vector3.down*2.15f).y;
            if(Parasol.placement==null||!Parasol.placement.Ready||parasolFoot<3.3f)Parasol.Restore();
        }
        bool GroundAt(Vector3 feet,out RaycastHit ground)
        {
            // Match the motor's support shape: a player standing along a stair
            // edge is supported even when a thin ray through their centre misses.
            var controller=game.controller;float radius=Mathf.Max(.02f,controller.radius-.025f);
            var bottom=feet+controller.center-Vector3.up*(controller.height*.5f-controller.radius);
            int count=Physics.SphereCastNonAlloc(bottom+Vector3.up*.05f,radius,Vector3.down,supportHits,.15f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity;ground=default;
            for(int i=0;i<count;i++)
            {
                var hit=supportHits[i];
                if(hit.collider==controller||!PlacementSupport.IsUsableGround(hit.collider)||hit.normal.y<Mathf.Cos(controller.slopeLimit*Mathf.Deg2Rad)||hit.point.y>feet.y+.1f||hit.distance>=nearest)continue;
                nearest=hit.distance;ground=hit;
            }
            return nearest<float.PositiveInfinity;
        }
        bool Released(BeachProp prop)=>prop!=null&&prop.gameObject.activeInHierarchy&&game.held!=prop&&prop.placement!=null&&prop.placement.Ready&&prop.body!=null&&!prop.body.isKinematic;
        public bool IsAtSummit(Vector3 feet)
        {
            if(!GroundAt(feet,out var ground))return false;
            if(ground.collider==SummitFloor)return true;
            bool crown=ground.collider==CrownCutFace||(CastleRoot!=null&&(ground.collider.transform==CastleRoot||ground.collider.transform.IsChildOf(CastleRoot)));
            var fromTower=new Vector2(ground.point.x,ground.point.z-96);
            return crown&&ground.point.y>=5.8f&&fromTower.sqrMagnitude<=2.4f*2.4f;
        }
        bool Connected(BeachProp prop,float z,float run,float bottom,float rise)
        {
            if(prop==null||game.held==prop||prop.placement==null||!prop.placement.Ready)return false;
            for(int i=0;i<9;i++)
            {
                float t=(i+.5f)/9;
                var p=new Vector3(0,bottom+rise*t,z+run*t);bool found=false;
                foreach(var hit in Physics.RaycastAll(p+Vector3.up*.25f,Vector3.down,.50f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore))
                    if(hit.collider.GetComponentInParent<BeachProp>()==prop&&hit.normal.y>.8f&&Mathf.Abs(hit.point.y-p.y)<.22f){found=true;break;}
                if(!found)return false;
            }
            return true;
        }
        static void Fix(BeachProp prop)
        {prop.body.isKinematic=true;prop.body.useGravity=false;prop.body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;}
        public void Tick(float dt)
        {
            if(game.checkpoint!=3)return;
            LowerConnected=Connected(LowerFragment,89.5f,2,1.1f,2.34f);
            UpperConnected=Connected(UpperFragment,92.1f,2.9f,3.44f,3.26f);
            var feet=game.player.feet;
            if(game.player.grounded&&game.safe&&Vector3.Distance(feet,LowerStart)<1.2f)ReachedEntrance=true;
            if(game.player.grounded&&GroundAt(feet,out var support))
            {
                var supportedProp=support.collider.GetComponentInParent<BeachProp>();
                // Ideal-pose rays remain diagnostics. Progress follows real
                // use of released stairs, including offset/shorter placements
                // and brief sun exposure already governed by the heat meter.
                if(Released(LowerFragment)&&supportedProp==LowerFragment&&Mathf.Abs(feet.x)<2.8f&&feet.z>90&&feet.z<92.4f)walkedLower=true;
                bool upperCourse=Mathf.Abs(feet.x)<3.2f&&feet.z>91.45f&&feet.z<95.25f&&support.point.y>=3.25f;
                if(!FirstRestored&&walkedLower&&Released(LowerFragment)&&upperCourse)
                {
                    FirstRestored=ReachedEntrance=true;Fix(LowerFragment);
                    AtMidpoint=game.castleRest=game.FootSafe(Midpoint+Vector3.up*.025f);
                    Debug.Log("BEACH_CASTLE_FIRST_TRAVERSED feet="+feet+" idealPose="+LowerConnected);
                    game.Tell("첫 성벽 복구 · 이 조각은 연결한 자리에 고정됩니다. 평평한 곳에 파라솔을 놓아 위쪽 그늘을 만드세요.",8);
                }
                if(FirstRestored&&Released(UpperFragment)&&supportedProp==UpperFragment&&Mathf.Abs(feet.x)<2.8f&&feet.z>93&&feet.z<95.4f)walkedUpper=true;
                if(FirstRestored&&!AtMidpoint&&upperCourse&&game.FootSafe(Midpoint+Vector3.up*.025f))AtMidpoint=game.castleRest=true;
                if(!SecondRestored&&FirstRestored&&walkedUpper&&Released(UpperFragment)&&game.safe&&IsAtSummit(feet))
                {
                    SecondRestored=true;Fix(UpperFragment);
                    Debug.Log("BEACH_CASTLE_SUMMIT_REACHED feet="+feet+" idealPose="+UpperConnected);
                    game.Tell("두 성벽을 복구했습니다 · 정상 도착!",6);
                }
            }
            if(JointRenderers!=null)
            {
                var block=new MaterialPropertyBlock();
                foreach(var r in JointRenderers)if(r!=null)
                {
                    var held=game.held;float near=held==LowerFragment||held==UpperFragment?Mathf.Clamp01(1-Vector3.Distance(held.transform.position,r.bounds.center)/1.7f):0;
                    block.SetColor("_EmissionColor",new Color(.02f,.16f,.14f)*near);r.SetPropertyBlock(block);
                }
            }
        }
        static Material Palette(string name,Color color)
        {var m=new Material(Shader.Find("Parallax/BeachSurface")){name=name,color=color};m.SetFloat("_Glossiness",.12f);BeachWorld.Materials.Add(m);return m;}
        public static BeachCastleRestoration Build(Transform root,BeachGame game,Transform castle,BeachProp parasol,Transform fixedParasol)
        {
            var host=new GameObject("04 · 정면 성벽 복구");host.transform.SetParent(root,false);
            var route=host.AddComponent<BeachCastleRestoration>();route.game=game;route.CastleRoot=castle;route.Parasol=parasol;route.FixedParasol=fixedParasol;
            parasol.transform.SetParent(host.transform,true);fixedParasol.SetParent(host.transform,true);
            var sand=Palette("Front castle golden sand",new Color(.83f,.69f,.40f));
            var cap=Palette("Front castle flat stone",new Color(.93f,.80f,.50f));
            GameObject Box(string name,Vector3 at,Vector3 size,Material mat,bool solid=true)=>BeachWorld.Box(name,at,size,mat,host.transform,solid);
            // A shallow front apron belongs to the castle base. It never reaches
            // either missing upper wall section by itself.
            Box("모래성 정면 받침",new Vector3(0,.55f,88.25f),new Vector3(6,1.1f,2.5f),sand);
            var approach=new List<Vector3>{new Vector3(0,0,85.65f)};
            for(int i=0;i<7;i++)
            {
                float y=1.1f*(i+1)/7,z=86.05f+i*.15f;
                Box("정면 기단의 낮은 디딤단",new Vector3(0,y*.5f,z),new Vector3(3,y,.17f),sand);
            }
            approach.Add(new Vector3(0,1.1f,88.1f));approach.Add(LowerStart);route.ApproachRoute=approach.ToArray();
            // Broad, level masonry gives the umbrella a real horizontal base.
            // Its front edge starts after stair A, keeping the ascent headroom open.
            Box("파라솔을 놓는 평평한 성벽",new Vector3(0,2.26f,93.25f),new Vector3(6,2.36f,3.5f),sand);
            Box("평평한 성벽 마감",new Vector3(0,3.443f,93.25f),new Vector3(6,.006f,3.5f),cap,false);
            foreach(float side in new[]{-1f,1f})
                Box("성벽의 평평한 작업 여유",new Vector3(side*2.2f,2.26f,91.1f),new Vector3(1.6f,2.36f,.8f),sand);
            route.SummitFloor=Box("정상 탑 받침",new Vector3(0,6.32f,95.7f),new Vector3(1.8f,.76f,1.4f),sand).GetComponent<BoxCollider>();
            // The imported tower is a closed shell. Seal the new cut face so
            // its rear battlements do not appear to float through a culled back wall.
            route.CrownCutFace=Box("중앙 탑의 절단 단면",new Vector3(0,4.67f,95.23f),new Vector3(3.3f,2.54f,.06f),sand).GetComponent<BoxCollider>();
            // Narrow cut faces cap only the exposed original front-wall edges.
            foreach(float side in new[]{-1f,1f})
                Box("앞 성벽의 잘린 단면",new Vector3(side*1.42f,2.25f,91.5f),new Vector3(.04f,2.34f,1.26f),cap);
            var lower=BeachCastleStairBuilder.Build(host.transform,game,"낮은 성벽 계단 조각",1.17f,1,1.1f);
            lower.transform.position=new Vector3(-1.8f,2.65f,89.2f);lower.Remember();route.LowerFragment=lower;
            var upper=BeachCastleStairBuilder.Build(host.transform,game,"높은 성벽 계단 조각",1.63f,1.45f,.8f);
            upper.transform.position=new Vector3(-1.8f,4.99f,92.3f);upper.Remember();route.UpperFragment=upper;
            void Plinth(string name,BeachProp piece,float floor,float rise,Vector2 size)
            {
                float top=piece.transform.position.y-rise*.5f*piece.transform.localScale.x;
                Box(name,new Vector3(piece.transform.position.x,(floor+top)*.5f,piece.transform.position.z),new Vector3(size.x,top-floor,size.y),sand);
            }
            Plinth("첫 성벽 조각 받침",lower,1.1f,1.17f,new Vector2(.85f,1));
            Plinth("두 번째 성벽 조각 받침",upper,3.44f,1.63f,new Vector2(.85f,1.1f));
            var marks=new List<Renderer>();
            marks.AddRange(BeachCastleWallBuilder.CreateShellCrest(host.transform,new Vector3(-1.46f,2.65f,90.83f),Quaternion.Euler(0,180,0),1.4f).GetComponentsInChildren<Renderer>());
            marks.AddRange(BeachCastleWallBuilder.CreateShellCrest(host.transform,new Vector3(-1.46f,3.448f,93.1f),Quaternion.Euler(-90,0,0),1.4f).GetComponentsInChildren<Renderer>());
            route.JointRenderers=marks.ToArray();route.CanopyRenderers=fixedParasol.GetComponentsInChildren<Renderer>();
            return route;
        }
    }
}
