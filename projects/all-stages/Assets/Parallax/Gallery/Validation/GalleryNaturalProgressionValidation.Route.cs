using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed partial class GalleryNaturalProgressionValidation
    {
        // Only the bottle's starting geometry and the extracted book's final
        // pan pose are fixtures. The checkpoint, extraction, balance, sword
        // fall, pickup, insertion and doorway passage remain production state.
        IEnumerator CompleteDikeRoute()
        {
            var course=game.bottleBalance;
            var reward=course==null?null:course.Reward;
            Require(course!=null&&reward!=null&&reward.Available,"Dike route requires its real course and independently verified sword asset");
            Require(game.checkpoint==1&&course.InRoom&&!course.Solved&&game.shadowCourse!=null&&game.shadowCourse.NikeCleared,
                "Dike must begin after the real Nike passage at checkpoint1, before solving: cp="+game.checkpoint+" feet="+game.player.feet);
            TraceRoute("Dike start: actual Nike doorway passed; checkpoint="+game.checkpoint+" solved="+course.Solved);

            RoutePoseFixture(course.Bottle,new Vector3(0,3.2f,34),Quaternion.identity,1.95f);
            yield return Frames(8);
            var glass=course.Art.bottleVisual.Find("Open hand-blown vessel")?.GetComponent<Renderer>();
            Require(glass!=null,"Dike route cannot locate the real open glass vessel renderer");
            yield return Walk(new Vector3(0,0,30.2f));
            Look(glass.bounds.center);game.UpdateAim();game.Interact();
            Require(game.held==course.Bottle,"Public bottle pickup failed: aimed="+(game.aimed==null?"none":game.aimed.title)+" feet="+game.player.feet);
            TraceRoute("Dike geometry fixture: bottle at (0,3.2,34), scale1.95; public camera pickup succeeded");
            game.RotateHeld(-90,0);yield return Frames(6);
            for(int i=0;i<80&&GalleryBottleBalanceVessel.MouthDirection(course.Bottle).y>-.96f;i++)
            {game.RotateHeld(0,2);Step();if(i%8==7)yield return null;}
            for(int lift=0;lift<8&&(course.ExtractionExitPosition.y<.70f||course.ExtractionBlocked);lift++)
            {Look(game.view.transform.position+game.view.transform.forward*8+Vector3.up*.55f);yield return Frames(8);}
            for(int i=0;i<1200&&!course.Extracted;i++)
            {Step();if(i%8==7)yield return null;}
            Require(course.Extracted&&course.Sculpture.body!=null&&!course.Sculpture.body.isKinematic&&course.Sculpture.body.useGravity,
                "Public held tilt did not yield real law-book containment exit and gravity: "+course.Status+" blocker="+course.ExtractionBlocker);
            TraceRoute("Dike public bottle tilt physically extracted the dynamic law book; checkpoint="+game.checkpoint);
            if(game.held!=null)game.Cancel(true);
            RoutePoseFixture(course.Bottle,new Vector3(-7,1,32.5f),Quaternion.identity,1);

            var pan=course.Art.rightPan.GetComponent<BoxCollider>();
            Require(pan!=null,"The real Dike right-pan collider is missing");
            RouteBookOnPan(course.Sculpture,pan,GalleryBottleBalanceCourse.TargetSculptureScale*1.04f);
            course.Sculpture.Release();
            Require(!course.Sculpture.body.isKinematic&&course.Sculpture.body.useGravity&&!course.Solved,
                "The declared book-pan pose must release through ordinary gravity before balance completes");
            TraceRoute("Dike geometry fixture: extracted book at target scale*1.04 above actual right pan; normal Release invoked");
            for(int i=0;i<1200&&!course.Solved;i++)
            {Step();if(i%8==7)yield return null;}
            Require(course.Solved&&course.StableSeconds>=GalleryBottleBalanceCourse.StableSecondsRequired,
                "Real released book did not earn the two-second balance: "+course.Status+" stable="+course.StableSeconds+" releases="+reward.ReleaseCount);
            TraceRoute("Dike balance completed through real pan settling/dwell; stable="+course.StableSeconds+" releases="+reward.ReleaseCount);
            for(int i=0;i<1300&&!reward.SwordAvailable;i++)
            {Step();if(i%8==7)yield return null;}
            Require(reward.SwordAvailable&&reward.FirstFallCompletedPhysically&&reward.MaximumDropDistance>.20f&&reward.RecoveryCount==0&&reward.ReleaseCount==1,
                "Sword must complete its own gravity fall, without recovery or pose injection: state="+reward.Stage+" drop="+reward.MaximumDropDistance+" recoveries="+reward.RecoveryCount);
            Require(!reward.Unlocked&&course.Door.Opening<.001f&&course.Door.Barrier.enabled,
                "Balance or first sword fall opened the Dike door before explicit socket use");
            TraceRoute("Dike sword completed physical first fall: drop="+reward.MaximumDropDistance+" checkpoint="+game.checkpoint);

            RouteAimSword(reward);game.UpdateAim();game.Interact();
            Require(reward.Carrying&&!reward.Unlocked,"Public camera-ray pickup did not hold the fallen sword");
            TraceRoute("Dike real sword publicly picked up from a disclosed clear floor approach");
            Vector3 socket=reward.LockTransform.position;
            // This disclosed floor approach selects the receiver; the sword's
            // carried pose and socket ray remain validated by production code.
            game.PlacePlayer(new Vector3(socket.x,0,socket.z-2.2f));Look(socket+Vector3.up*.08f);
            yield return Frames(8);reward.RefreshAim(game.view);
            Require(reward.TargetedLock&&!reward.CarryPoseBlocked,
                "Actual central socket is not targetable with the carried sword: feet="+game.player.feet+" socket="+socket+" carryBlocked="+reward.CarryPoseBlocked);
            game.UpdateAim();game.Interact();
            Require(reward.Busy&&!reward.Unlocked,"Public socket click did not begin gradual sword insertion");
            TraceRoute("Dike explicit socket click began insertion at the real central P02 receiver");
            for(int i=0;i<600&&(course.Door.Opening<.999f||!reward.Unlocked);i++)
            {Step();if(i%8==7)yield return null;}
            Require(reward.Unlocked&&course.Door.Opening>=.999f&&!course.Door.Barrier.enabled,
                "Sword insertion did not unlock and fully open the physical Dike door: state="+reward.Stage+" opening="+course.Door.Opening+" barrier="+course.Door.Barrier.enabled);
            Require(game.checkpoint==1,"Dike's insertion fixture must not promote the checkpoint before walking out: cp="+game.checkpoint);
            TraceRoute("Dike sword seated and exit door fully opened; checkpoint remains1 before actual exit walking");
            yield return Walk(new Vector3(2.6f,0,GalleryDikeGalleryLayout.ReceiverStation.z-2.2f));
            yield return Walk(new Vector3(2.6f,0,GalleryBottleBalanceLayout.EndZ-1.6f));
            yield return Walk(new Vector3(0,0,GalleryBottleBalanceLayout.EndZ-1.0f));
            yield return Walk(new Vector3(0,0,GalleryBottleBalanceLayout.EndZ+.7f));
            yield return Frames(12);
            Require(game.player.feet.z>GalleryBottleBalanceLayout.EndZ+.4f&&game.checkpoint==2,
                "Actual controller passage through Dike's opened exit did not naturally reach checkpoint2: cp="+game.checkpoint+" feet="+game.player.feet+" NikeCleared="+game.shadowCourse.NikeCleared);
            TraceRoute("Dike actual exit doorway traversal reached checkpoint2 naturally; feet="+game.player.feet);
        }

        void RoutePoseFixture(GalleryProp prop,Vector3 at,Quaternion rotation,float scale)
        {
            PropReleasePhysics.Attach(prop.body).CancelPendingRelease();HeldCollisionState.Attach(prop.body).Restore();
            if(!prop.body.isKinematic){prop.body.linearVelocity=Vector3.zero;prop.body.angularVelocity=Vector3.zero;}
            prop.body.isKinematic=true;prop.body.useGravity=false;
            prop.transform.SetPositionAndRotation(at,rotation);prop.transform.localScale=Vector3.one*scale;
            prop.body.position=at;prop.body.rotation=rotation;prop.RefreshMass();Physics.SyncTransforms();prop.placement.ResetPlaced();
            if(prop==game.bottleBalance.Bottle&&!game.bottleBalance.Extracted)game.bottleBalance.Contents.Reset();
        }
        void RouteBookOnPan(GalleryProp book,BoxCollider pan,float scale)
        {
            Bounds b=pan.bounds;Vector3 top=new Vector3(b.center.x,b.max.y,b.center.z);
            RoutePoseFixture(book,top+Vector3.up,Quaternion.identity,scale);Bounds actual=book.WorldBounds;
            book.transform.position+=top-new Vector3(actual.center.x,actual.min.y,actual.center.z)+Vector3.up*.008f;
            book.body.position=book.transform.position;Physics.SyncTransforms();book.placement.ResetPlaced();
        }
        void RouteAimSword(GalleryBottleBalanceKeyReward reward)
        {
            var shape=reward.SwordPhysicalCollider;
            Require(shape!=null,"The fallen sword's actual pickup collider is missing");
            Vector3 at=shape.bounds.center;
            Vector3[] offsets={new Vector3(0,0,-2.25f),new Vector3(2.3f,0,-.5f),new Vector3(1.85f,0,-2.1f)};
            Vector3[] targets={at,shape.transform.TransformPoint(shape.center+Vector3.up*shape.size.y*.35f),shape.transform.TransformPoint(shape.center-Vector3.up*shape.size.y*.30f)};
            var diagnostics=new List<string>();
            foreach(Vector3 offset in offsets)
            {
                Vector3 candidate=new Vector3(at.x+offset.x,0,at.z+offset.z);
                if(Mathf.Abs(candidate.x)>GalleryBottleBalanceLayout.HalfWidth-.4f)continue;
                bool floorFound=false;float best=float.NegativeInfinity;Vector3 feet=candidate;
                foreach(var hit in Physics.RaycastAll(new Vector3(candidate.x,1.2f,candidate.z),Vector3.down,1.6f,~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.collider==null||hit.normal.y<.95f||hit.point.y<-.08f||hit.point.y>.12f)continue;
                    string name=hit.collider.name;
                    if(name.IndexOf("floor",StringComparison.OrdinalIgnoreCase)<0&&name.IndexOf("바닥",StringComparison.Ordinal)<0)continue;
                    if(hit.point.y<=best)continue;
                    best=hit.point.y;feet=new Vector3(candidate.x,hit.point.y+.03f,candidate.z);floorFound=true;
                }
                if(!floorFound||!game.CanStand(feet)){diagnostics.Add("floor blocked at "+candidate);continue;}
                game.PlacePlayer(feet);
                foreach(Vector3 target in targets)
                {
                    Look(target);reward.RefreshAim(game.view);
                    if(!reward.TargetedKey)continue;
                    TraceRoute("Dike disclosed reachable-floor sword aim fixture: feet="+game.player.feet+" target="+target);
                    return;
                }
                diagnostics.Add("sword ray occluded at "+feet);
            }
            Require(false,"No actual unoccluded sword pickup ray from the three real floor approaches: "+string.Join(" | ",diagnostics));
        }
    }
}
