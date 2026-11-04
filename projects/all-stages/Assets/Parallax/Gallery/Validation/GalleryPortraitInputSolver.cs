using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Player-only pickup fixtures choose the original angular size. Every prop
    // pose thereafter comes from the normal shared camera-placement resolver,
    // movement, Drop and PhysX. This utility never writes a prop transform.
    public static class GalleryPortraitInputSolver
    {
        const float Dt=1f/90;
        const int MaximumTicks=3000;

        sealed class Run
        {
            readonly Action<Vector2> step;
            public int ticks;
            public Run(Action<Vector2> step){this.step=step;}
            public void Step(Vector2 movement)
            {
                Require(ticks<MaximumTicks,"Exceeded 3000 simulated input ticks");
                step(movement);ticks++;
            }
        }
        static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("PORTRAIT_INPUT_SOLVER: "+message);}
        static void Look(GalleryGame game,Vector3 point)
        {
            Vector3 d=point-game.view.transform.position;
            Require(d.sqrMagnitude>.000001f,"Camera target coincides with the eye");
            game.yaw=Mathf.Atan2(d.x,d.z);game.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);game.SyncView();
        }
        static List<BoxCollider> PickSurfaces(GalleryProp piece,Vector3 marker)
        {
            var surfaces=new List<BoxCollider>();
            foreach(var shape in piece.parts)
                if(shape!=null&&shape.enabled&&shape.gameObject.activeInHierarchy&&!shape.isTrigger)surfaces.Add(shape);
            surfaces.Sort((a,b)=>
                (piece.transform.InverseTransformPoint(a.bounds.center)-marker).sqrMagnitude.CompareTo(
                (piece.transform.InverseTransformPoint(b.bounds.center)-marker).sqrMagnitude));
            Require(surfaces.Count>0,"No selectable collider on "+piece.title);return surfaces;
        }
        static string Pose(GalleryProp piece)
        {return "position="+piece.transform.position.ToString("F4")+" scale="+piece.transform.localScale.ToString("F4")+" rotation="+piece.transform.eulerAngles.ToString("F2");}

        public static IEnumerator Solve(GalleryGame game,GalleryConceptPerspective portrait,Action<Vector2> step)
        {
            Require(game!=null&&portrait!=null&&step!=null,"Actual game, portrait and Tick/PhysX step are required");
            Require(game.automated&&game.active&&!game.won,"Solver requires a resumed automated game");
            Require(game.controller!=null&&game.view!=null&&game.player!=null,"Actual player/controller/view are required");
            Require(game.held==null,"Start with no held prop");
            Require(portrait.pieces!=null&&portrait.pieces.Length==3&&portrait.viewingPoint!=null,"Three original portrait pieces and observation point are required");
            Require(portrait.solutionPositions!=null&&portrait.solutionPositions.Length==3&&portrait.solutionScales!=null&&portrait.solutionScales.Length==3&&
                portrait.markerLocalPositions!=null&&portrait.markerLocalPositions.Length==3,"Portrait solution metadata is incomplete");
            var run=new Run(step);int respawns=game.respawns;
            Vector3 observer=portrait.viewingPoint.position,observerEye=observer+Vector3.up*1.55f;

            for(int index=0;index<portrait.pieces.Length;index++)
            {
                GalleryProp piece=portrait.pieces[index];Require(piece!=null,"Missing piece "+index);
                piece.InitializePhysics();
                Vector3 homePosition=piece.transform.position;float homeScale=piece.transform.localScale.x,goalScale=portrait.solutionScales[index];
                Require(homeScale>0&&goalScale>0,"Nonpositive scale on "+piece.title);
                Require(Quaternion.Angle(piece.transform.rotation,portrait.transform.rotation)<1,"Original portrait pieces must begin upright and facing the observer");
                var surfaces=PickSurfaces(piece,portrait.markerLocalPositions[index]);
                Vector3 homeClick=surfaces[0].bounds.center;

                // ObjectInteraction intentionally freezes the prop root as its
                // perspective anchor (GrabLocalPoint=zero), rather than the ray
                // hit on the selected eye/cushion. Pick the surface for visibility
                // but calculate angular size about that actual shared anchor.
                Vector3 predictedGrab=Vector3.zero,homeAnchor=piece.transform.TransformPoint(predictedGrab);
                Vector3 goalAnchor=portrait.transform.TransformPoint(portrait.solutionPositions[index]+predictedGrab*goalScale);
                float goalDepth=Vector3.Distance(goalAnchor,observerEye),reference=homeScale/goalScale*goalDepth;
                float vertical=homeAnchor.y-(observer.y+1.55f),radicand=reference*reference-vertical*vertical;
                Require(radicand>.25f,"Required pickup distance cannot be reached from the floor for "+piece.title);
                Vector3 pickupFeet=new Vector3(homeAnchor.x,observer.y,homeAnchor.z-Mathf.Sqrt(radicand));
                Require(game.CanStand(pickupFeet),"Player-only pickup fixture is obstructed for "+piece.title+" at "+pickupFeet);
                game.PlacePlayer(pickupFeet);
                for(int i=0;i<24;i++)
                {
                    Look(game,homeClick);run.Step(Vector2.zero);
                    Require(game.respawns==respawns,"Pickup fixture caused a respawn");
                    if(run.ticks%12==0)yield return null;
                }
                Require(Vector3.Distance(piece.transform.position,homePosition)<.01f,"Original prop moved before its pickup: "+piece.title);
                foreach(var surface in surfaces)
                {
                    Look(game,surface.bounds.center);game.UpdateAim();
                    if(game.aimed!=piece)continue;
                    game.Interact();if(game.held==piece)break;
                }
                Require(game.held==piece,"Actual camera ray cannot grab "+piece.title+" from fixture "+game.player.feet);
                Require(Mathf.Abs(game.referenceScale-homeScale)<.001f,"Shared pickup did not preserve the source scale");
                goalAnchor=portrait.transform.TransformPoint(portrait.solutionPositions[index]+game.GrabLocalPoint*goalScale);
                float actualRequired=homeScale/goalScale*Vector3.Distance(goalAnchor,observerEye);
                Require(Mathf.Abs(game.referenceDistance-actualRequired)/actualRequired<.06f,"Actual frozen anchor differs from the predicted pickup fixture; ref="+
                    game.referenceDistance+" expected="+actualRequired+" grab="+game.GrabLocalPoint);
                Debug.Log("PORTRAIT_INPUT_SOLVER_PICKUP "+index+" "+piece.title+" fixture="+game.player.feet.ToString("F4")+
                    " clicked="+homeClick.ToString("F4")+" refDistance="+game.referenceDistance.ToString("F5")+" refScale="+game.referenceScale.ToString("F5")+
                    " grabLocal="+game.GrabLocalPoint.ToString("F4")+" goalAnchor="+goalAnchor.ToString("F4"));

                // Walk backwards/sideways to the observation disc, always aiming
                // at the desired anchor. Step receives ordinary world movement;
                // GalleryGame handles yaw conversion and the shared held advance.
                while(true)
                {
                    Vector2 delta=new Vector2(observer.x-game.player.feet.x,observer.z-game.player.feet.z);
                    if(delta.magnitude<.055f)break;
                    Look(game,goalAnchor);
                    float brake=Mathf.Sqrt(Mathf.Clamp01(delta.magnitude/1.2f));run.Step(delta.normalized*brake);
                    Require(game.held==piece&&game.respawns==respawns,"Carry route lost its held prop or respawned: "+piece.title);
                    if(run.ticks%12==0)yield return null;
                }
                for(int i=0;i<36;i++)
                {
                    Look(game,goalAnchor);run.Step(Vector2.zero);
                    Require(game.held==piece&&game.respawns==respawns,"Observation braking lost its held prop");
                    if(run.ticks%12==0)yield return null;
                }
                game.UpdateHeldPose();
                Require(game.Drop(),"Public Drop refused "+piece.title+": "+game.invalidReason+" / "+game.PlacementObstacle+" / "+Pose(piece));
                Require(game.held==null,"Public Drop did not release "+piece.title);
                bool settled=false;
                for(int i=0;i<300;i++)
                {
                    Look(game,goalAnchor);run.Step(Vector2.zero);portrait.Evaluate();
                    Require(game.respawns==respawns,"Drop settling caused a respawn");
                    if(piece.placement.Ready&&portrait.AtViewingPoint&&portrait.AlignedPieces>=index+1){settled=true;break;}
                    if(run.ticks%12==0)yield return null;
                }
                Require(settled,"Piece did not settle into its projected portrait after 300 actual physics ticks: "+piece.title+
                    " ready="+piece.placement.Ready+" aligned="+portrait.AlignedPieces+" / "+Pose(piece));
                Debug.Log("PORTRAIT_INPUT_SOLVER_PLACED "+index+" aligned="+portrait.AlignedPieces+" ready="+piece.placement.Ready+" "+Pose(piece));
            }
            Look(game,portrait.transform.TransformPoint(new Vector3(0,1.65f,39)));
            portrait.Evaluate();
            Require(portrait.Complete&&portrait.AtViewingPoint,"Three input-placed props do not make the complete portrait at the observation point");
            Debug.Log("PORTRAIT_INPUT_SOLVER_OK ticks="+run.ticks+" simulatedSeconds="+(run.ticks*Dt).ToString("F3")+
                "; original props solved through camera Interact, CharacterController walking, shared resize, Drop and PhysX only");
        }
    }
}
