using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class ShadowPuzzleTests
    {
        static readonly List<string> checks=new List<string>();
        static void Require(bool value,string why) { if(!value) throw new Exception("SHADOW_TEST_FAILED: "+why); }
        static void Check(string label,Action action) { action(); checks.Add(label); Debug.Log("SHADOW_PASS "+label); }
        public static void Run()
        {
            checks.Clear();
            Check("All seven actual solids have legal, non-overlapping initial and solved positions",()=>
            {
                var p=new Player(new Vector2(0,6.5f));
                foreach(var pieces in new[]{ShadowPuzzleRules.InitialPieces(),ShadowPuzzleRules.SolvedPieces()})
                    for(int i=0;i<pieces.Length;i++) Require(ShadowPuzzleRules.ValidPlacement(pieces[i],pieces,p,i),"Illegal pose "+i+" at "+pieces[i].position);
            });
            Check("Complete projected silhouette passes; scattered pieces and each missing part fail",()=>
            {
                Require(ShadowPuzzleRules.Match(ShadowPuzzleRules.Target).complete,"Target rejected");
                Require(!ShadowPuzzleRules.Match(ShadowPuzzleRules.Shadows(ShadowPuzzleRules.InitialPieces())).complete,"Initial layout solved");
                for(int missing=0;missing<7;missing++)
                {
                    var partial=new List<List<Vector2>>();
                    for(int i=0;i<7;i++) if(i!=missing) partial.Add(ShadowPuzzleRules.Target[i]);
                    Require(!ShadowPuzzleRules.Match(partial.ToArray()).complete,"Missing part accepted: "+missing);
                }
            });
            Check("Oversize covering shapes and a translated dog cannot bypass silhouette matching",()=>
            {
                Require(!ShadowPuzzleRules.Match(new[]{new FloorRect(-4,4,-4,4).Polygon()}).complete,"Blanket coverage accepted");
                var pieces=ShadowPuzzleRules.SolvedPieces();
                for(int i=0;i<pieces.Length;i++) pieces[i].position+=Vector2.right*.8f;
                Require(!ShadowPuzzleRules.Match(ShadowPuzzleRules.Shadows(pieces)).complete,"Wrong location accepted");
                pieces=ShadowPuzzleRules.SolvedPieces(); pieces[1].scale=1.65f;
                Require(!ShadowPuzzleRules.Match(ShadowPuzzleRules.Shadows(pieces)).complete,"Wrong head scale accepted");
            });
            Check("Shadow remains ungrabbable until a released complete silhouette is stable",()=>
            {
                var s=new ShadowPuzzleState(); Require(!s.PickShadow()&&!s.Restore(),"Initial state bypass");
                s.pieces=ShadowPuzzleRules.SolvedPieces(); s.held=new Grab { index=0 };
                s.Evaluate(3); Require(s.stage==ShadowStage.Arranging&&!s.PickShadow(),"Held solution unlocked");
                s.held=null; s.Evaluate(.30f); Require(!s.PickShadow(),"Unstable solution unlocked");
                s.pieces[0].position+=Vector2.left; s.Evaluate(.2f); Require(s.stable==0,"Stability did not reset");
                s.pieces=ShadowPuzzleRules.SolvedPieces(); s.Evaluate(.7f);
                Require(s.stage==ShadowStage.Ready&&s.PickShadow(),"Completed silhouette not collectible");
            });
            Check("Earned geometry persists on carry/drop; only inserting a carried dog opens the door",()=>
            {
                var s=new ShadowPuzzleState { pieces=ShadowPuzzleRules.SolvedPieces() }; s.Evaluate(.7f);
                Require(!s.Restore()&&!s.DoorUnlocked,"Ready shadow opened door remotely");
                Vector2 v=s.frozen[0][0]; Require(s.PickShadow()&&!s.PickShadow(),"Duplicate pickup");
                s.pieces[0].position+=Vector2.one; Require(s.frozen[0][0]==v,"Frozen geometry changed");
                Require(s.DropShadow(new Vector2(100,100))&&s.tokenOffset.x==4&&!s.DoorUnlocked,"Safe drop");
                Require(s.PickShadow()&&s.Restore()&&s.DoorUnlocked&&!s.PickShadow(),"Insertion transition");
                var reset=new ShadowPuzzleState(); Require(!reset.DoorUnlocked&&reset.frozen==null&&!reset.PickShadow(),"Reset state leak");
            });
            Check("Grab preserves pose; fixed-size mouse movement and perspective scaling both work",()=>
            {
                var s=new ShadowPuzzleState(); s.held=ShadowPuzzleRules.Capture(s,0);
                Require(ShadowPuzzleRules.Place(s,out var original),"Initial grab");
                Require((original.position-s.pieces[0].position).magnitude<.001f&&Mathf.Abs(original.scale-1)<.001f,"Pickup jump");
                var g=s.held; float distance=g.distance*.8f;
                s.pitch=Mathf.Atan2(ShadowPuzzleRules.Shapes[0].bounds.center.y*.8f-s.player.Eye.y,distance)-g.pitchOffset;
                Require(ShadowPuzzleRules.Place(s,out var smaller)&&Mathf.Abs(smaller.scale-.8f)<.001f,"Perspective shrink");
                s.fixedSize=true; Vector2 target=ShadowPuzzleRules.Shapes[0].solution,delta=target-s.player.Point;
                s.yaw=Mathf.Atan2(-delta.x,-delta.y)-g.yawOffset;
                s.pitch=Mathf.Atan2(ShadowPuzzleRules.Shapes[0].bounds.center.y-s.player.Eye.y,delta.magnitude)-g.pitchOffset;
                Require(ShadowPuzzleRules.Place(s,out var moved)&&Mathf.Abs(moved.scale-1)<.001f&&(moved.position-target).magnitude<.001f,"Fixed size placement");
            });
            Check("Placement rejects walls, player and other solids; movement cannot tunnel through walls",()=>
            {
                var s=new ShadowPuzzleState();
                Require(!ShadowPuzzleRules.ValidPlacement(new ShadowPiece(0,new Vector2(20,0)),s.pieces,s.player,0),"Wall bypass");
                Require(!ShadowPuzzleRules.ValidPlacement(new ShadowPiece(0,s.player.Point),s.pieces,s.player,0),"Player overlap");
                Require(!ShadowPuzzleRules.ValidPlacement(new ShadowPiece(0,s.pieces[1].position),s.pieces,s.player,0),"Solid overlap");
                ShadowPuzzleRules.Move(s,new Vector2(3,0),10); Require(s.player.feet.x<=6.7f,"Movement tunnel");
            });
            Check("Floor target cannot be picked remotely or through a solid; frame insertion has a reach limit",()=>
            {
                var s=new ShadowPuzzleState(); Require(!ShadowPuzzleRules.AimToken(s),"No token pickup");
                Vector3 d=ShadowPuzzleRules.Frame.center-s.player.Eye;
                s.yaw=Mathf.Atan2(-d.x,-d.z); s.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);
                Require(!ShadowPuzzleRules.AimFrame(s),"Remote frame reachable");
                s.pieces=ShadowPuzzleRules.SolvedPieces(); s.Evaluate(.7f);
                s.pieces[0]=new ShadowPiece(0,new Vector2(0,3),1.7f);
                s.yaw=0; s.pitch=Mathf.Atan2(.028f-s.player.Eye.y,6.5f);
                Require(!ShadowPuzzleRules.AimToken(s),"Picked shadow through an occluding solid");
                s.player=new Player(new Vector2(0,-5.7f)); d=ShadowPuzzleRules.Frame.center-s.player.Eye;
                s.yaw=Mathf.Atan2(-d.x,-d.z); s.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);
                Require(ShadowPuzzleRules.AimFrame(s),"Nearby frame inaccessible");
            });
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/shadow-rules-results.txt","Unity "+Application.unityVersion+"\nPassed: "+checks.Count+"\n"+string.Join("\n",checks));
        }
    }
}
