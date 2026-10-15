using System;
using System.Collections.Generic;
using ShadeLink.Starship;
using UnityEngine;

public static partial class StarshipHold
{
    const int QueryBudget = 256;
    const float StepDistance = .025f;
    static readonly float[] NearFactors = { .85f, .65f, .45f, 0f };
    static readonly Dictionary<StarshipGame,StarshipProp> QueuedDrops = new Dictionary<StarshipGame,StarshipProp>();
    public static void InteractInput(StarshipGame game)
    {
        if(game.held==null)game.Interact();
        else QueuedDrops[game]=game.held;
    }
    public static void FlushInteraction(StarshipGame game)
    {
        StarshipProp expected;
        if(!QueuedDrops.TryGetValue(game,out expected))return;
        QueuedDrops.Remove(game);
        if(game.active && !game.won && game.held==expected)game.Drop();
    }

    public static string Status(StarshipGame game) { return game.held == null ? "None" : Get(game).status; }
    public static int QueryCount(StarshipGame game) { return RecoveryQueryCount(game)+(game.held==null?0:game.SharedPlacementQueries); }
    public static int RecoveryQueryCount(StarshipGame game) { return game.held == null ? 0 : Get(game).queries; }
    public static void Update(StarshipGame game)
    {
        if (game.held == null) { Sessions.Remove(game); return; }
        Session s = Get(game); s.queries=0; s.exhausted=false; s.finishing=false; s.obstacle=null;
        Resolve(game);
        if(s.feasible&&game.TryApplySharedPlacement())
        {
            s.routeStage=-1;s.status="Tracking";s.history.Clear();Record(s,Current(s));
            s.setBlocked(false);s.setObstacle(null);game.invalid=false;
            game.held.Highlight(true,false);game.lighting.Synchronize();return;
        }
        if(s.routeStage>=0 && (Vector3.Distance(s.eye,s.routeEye)>.15f || Vector3.Angle(s.forward,s.routeAim)>10f ||
            Quaternion.Angle(s.requestedRotation,s.routeRotation)>10f || Mathf.Abs(s.desired-s.routeRequest)>.15f))
        {s.routeStage=-1;s.routeCandidate=0;}
        Pose accepted = Current(s);
        if(s.routeStage!=3) Record(s,accepted);
        bool blocked=false;
        string reason;
        if (!s.feasible) { s.status="NoFeasiblePose"; s.obstacle="현재 구역에서 가능한 배치가 없습니다."; blocked=true; }
        else if (!CheckPose(s,accepted,out reason))
        {
            if(RecoverOverlap(s,ref accepted)){s.status="Recovering";s.history.Clear();Record(s,accepted);}
            else{s.status="NoFeasiblePose";s.obstacle=reason;blocked=true;}
        }
        else
        {
            for (int iteration=0; iteration<1024; iteration++)
            {
                Pose target=Make(s,s.forward,s.desired,s.requestedRotation);
                Pose destination=target;
                if(s.routeStage==0) destination=Make(s,s.routeDirection,s.routeDepth,accepted.rotation);
                if(s.routeStage==1) destination=Make(s,s.forward,s.routeDepth,s.requestedRotation);
                if(s.routeStage==3 && s.history.Count>1) destination=Rebase(s,s.history[s.history.Count-2]);
                float motion=Motion(s,accepted,destination);
                if(motion < .0001f)
                {
                    if(s.routeStage==0){s.routeStage=1;continue;}
                    if(s.routeStage==1){s.routeStage=2;continue;}
                    if(s.routeStage==3)
                    {
                        if(s.history.Count>1) s.history.RemoveAt(s.history.Count-1);
                        Vector3 ignored;
                        bool escape=Clear(s,accepted,Make(s,accepted.direction,s.routeDepth,accepted.rotation),out ignored);
                        Apply(s,accepted);
                        if(s.exhausted) break;
                        if(escape){s.routeStage=0;s.routeDirection=accepted.direction;}
                        else if(s.history.Count<=1){s.routeStage=-1;s.status="ContactBlocked";blocked=true;break;}
                        continue;
                    }
                    s.routeStage=-1; s.status="Tracking"; break;
                }
                Pose next=Interpolate(s,accepted,destination,Mathf.Min(1f,StepDistance/motion));
                Vector3 normal;
                // Leave a guaranteed portion of each frame for escape/recovery.
                if(s.routeStage<0 && s.queries>=QueryBudget-64){s.exhausted=true;break;}
                if(Clear(s,accepted,next,out normal)) {accepted=next;if(s.routeStage!=3)Record(s,accepted);s.status=s.routeStage<0?"Tracking":"Recovering";continue;}
                if(s.exhausted) break;
                Pose from=accepted;
                float lo=0,hi=1;
                for(int j=0;j<6;j++)
                {
                    Pose candidate=Interpolate(s,from,next,(lo+hi)*.5f);
                    Vector3 ignored;
                    if(Clear(s,from,candidate,out ignored)){accepted=candidate;lo=(lo+hi)*.5f;}
                    else hi=(lo+hi)*.5f;
                    if(s.exhausted) break;
                }
                Apply(s,accepted);
                if(s.routeStage!=3) Record(s,accepted);
                if(s.exhausted) break;
                // A genuine retreat collision is not budget continuation.
                // Replan against current intent next frame instead of remaining
                // permanently tied to an obsolete history waypoint.
                if(s.routeStage==3){s.routeStage=-1;s.status="ContactBlocked";blocked=true;break;}
                if(s.routeStage==0 && s.history.Count>1)
                {
                    // Shrinking can itself be blocked from the contact pose.
                    // Retrace accepted, swept motion; never teleport to a pivot.
                    s.routeStage=3;s.backSteps=0;s.status="Recovering";continue;
                }
                bool changingDirection=Vector3.Angle(accepted.direction,s.forward)>1f || Quaternion.Angle(accepted.rotation,s.requestedRotation)>1f;
                if(s.routeStage<0 && changingDirection && TrySlide(s,ref accepted,target,normal)) {Record(s,accepted);s.status="ContactSliding";continue;}
                if(changingDirection && (s.routeStage<0 || s.routeStage==1 || s.routeStage==2))
                {
                    if(s.routeStage<0 || s.routeStage==2){s.routeCandidate=0;s.routeBase=Mathf.Min(accepted.depth,s.desired);}
                    if(StartNearerRoute(s,accepted)){s.status="Recovering";continue;}
                }
                s.status="ContactBlocked"; blocked=true; break;
            }
        }
        Apply(s,accepted);
        if(s.exhausted){s.status="BudgetLimited";blocked=false;}
        s.setBlocked(blocked); s.setObstacle(blocked?s.obstacle:null);
        // Same bounds/player/shape rules as shipped validation, with every
        // endpoint physics query counted and space reserved for this final check.
        s.finishing=true;game.invalid=!CheckPose(s,accepted,out game.invalidReason)||!s.feasible;s.finishing=false;
        if(!s.feasible)game.invalidReason="현재 크기로 조준한 위치에 놓을 공간이 없습니다.";
        Physics.SyncTransforms();
        game.held.Highlight(true,game.invalid);
        game.lighting.Synchronize();
        // holdDistance/heldRotation remain REQUESTED, never accepted feedback.
    }
    static Pose Rebase(Session s,Pose pose)
    {
        Vector3 anchor=pose.position+pose.rotation*(s.game.GrabLocalPoint*pose.scale)-s.eye;
        pose.depth=anchor.magnitude;pose.direction=anchor.normalized;return pose;
    }
    static void Record(Session s,Pose pose)
    {
        if(s.history.Count>0 && Motion(s,s.history[s.history.Count-1],pose)<.001f) return;
        if(s.history.Count>=1024) s.history.RemoveAt(0);
        s.history.Add(pose);
    }
    static bool StartNearerRoute(Session s,Pose accepted)
    {
        while(s.routeCandidate<NearFactors.Length)
        {
            float depth=Mathf.Max(s.minDepth,s.routeBase*NearFactors[s.routeCandidate++]);
            if(depth >= accepted.depth-.01f) continue;
            s.routeDepth=depth;s.routeDirection=accepted.direction;
            s.routeEye=s.eye;s.routeAim=s.forward;s.routeRotation=s.requestedRotation;s.routeRequest=s.desired;
            Vector3 ignored;
            bool escape=Clear(s,accepted,Make(s,accepted.direction,depth,accepted.rotation),out ignored);
            Apply(s,accepted);
            s.routeStage=escape?0:3;
            if(!escape && s.history.Count<=1){s.routeStage=-1;continue;}
            return true;
        }
        s.routeStage=-1;return false;
    }
    static bool TrySlide(Session s,ref Pose accepted,Pose target,Vector3 normal)
    {
        if(normal.sqrMagnitude<.5f) return false;
        Vector3 displacement=target.position-accepted.position;
        Vector3 tangent=displacement-normal*Vector3.Dot(displacement,normal);
        if(tangent.magnitude<.001f) return false;
        Pose slide=accepted;slide.position+=tangent.normalized*Mathf.Min(StepDistance,tangent.magnitude);
        Vector3 anchor=slide.position+slide.rotation*(s.game.GrabLocalPoint*slide.scale)-s.eye;
        slide.depth=anchor.magnitude;slide.direction=anchor.normalized;
        // A lateral contact step may temporarily decouple actual scale/depth;
        // the next tracking step restores the unchanged perspective request.
        if(Vector3.Angle(slide.direction,s.forward)>=Vector3.Angle(accepted.direction,s.forward)-.001f) return false;
        Vector3 ignored;
        if(!Clear(s,accepted,slide,out ignored)) {Apply(s,accepted);return false;}
        accepted=slide;return true;
    }
    static Pose Current(Session s)
    {
        Vector3 anchor=s.prop.transform.TransformPoint(s.game.GrabLocalPoint)-s.eye;
        return new Pose{position=s.prop.transform.position,rotation=s.prop.transform.rotation,scale=s.prop.transform.localScale.x,
            depth=anchor.magnitude,direction=anchor.normalized};
    }
    static Pose Make(Session s,Vector3 direction,float depth,Quaternion rotation)
    {
        float scale=s.ratio*depth;
        return new Pose{direction=direction,depth=depth,scale=scale,rotation=rotation,
            position=s.eye+direction*depth-rotation*(s.game.GrabLocalPoint*scale)};
    }
    static Pose Interpolate(Session s,Pose from,Pose to,float t)
    {
        Vector3 direction=Vector3.Slerp(from.direction,to.direction,t).normalized;
        float depth=Mathf.Lerp(from.depth,to.depth,t),scale=Mathf.Lerp(from.scale,to.scale,t);
        Quaternion rotation=Quaternion.Slerp(from.rotation,to.rotation,t);
        return new Pose{direction=direction,depth=depth,scale=scale,rotation=rotation,
            position=s.eye+direction*depth-rotation*(s.game.GrabLocalPoint*scale)};
    }
    static float Motion(Session s,Pose a,Pose b)
    {
        const float Rad=.0174532924f;
        return Mathf.Max(a.depth,b.depth)*Vector3.Angle(a.direction,b.direction)*Rad+Mathf.Abs(a.depth-b.depth)
            +s.radius*(Mathf.Abs(a.scale-b.scale)+Mathf.Max(a.scale,b.scale)*Quaternion.Angle(a.rotation,b.rotation)*Rad);
    }
    static void Apply(Session s,Pose pose)
    {s.prop.transform.localScale=Vector3.one*pose.scale;s.prop.transform.SetPositionAndRotation(pose.position,pose.rotation);}
    static bool Spend(Session s)
    {if(s.queries>=(s.finishing?QueryBudget:QueryBudget-s.shapes.Length)){s.exhausted=true;return false;}s.queries++;return true;}

    struct Contact { public Collider own, other; public float depth; public Vector3 direction; }
    static bool Contacts(Session s,Pose pose,List<Contact> result,out Vector3 escape,out float total)
    {
        Apply(s,pose);Physics.SyncTransforms();escape=Vector3.zero;total=0;float deepest=0;
        foreach(Shape shape in s.shapes)
        {
            if(!Spend(s)) return false;
            Collider[] others=Physics.OverlapBox(pose.position+pose.rotation*(shape.center*pose.scale),
                Vector3.Max(Vector3.one*.001f,shape.half*pose.scale),pose.rotation*shape.rotation,Mask,QueryTriggerInteraction.Ignore);
            foreach(Collider other in others)
            {
                if(Own(s,other) || (other==s.game.controller && !s.intersectsPlayer(shape.collider)))continue;
                if(!Spend(s))return false;
                Vector3 direction;float depth;
                if(!Physics.ComputePenetration(shape.collider,shape.collider.transform.position,shape.collider.transform.rotation,
                    other,other.transform.position,other.transform.rotation,out direction,out depth)) continue;
                result.Add(new Contact{own=shape.collider,other=other,depth=depth,direction=direction});total+=depth;
                if(depth>deepest){deepest=depth;escape=direction*Mathf.Min(StepDistance,depth+.002f);}
            }
        }
        return true;
    }
    static bool RecoverOverlap(Session s,ref Pose accepted)
    {
        var before=new List<Contact>();Vector3 escape;float total;
        if(!Contacts(s,accepted,before,out escape,out total) || before.Count==0 || escape.sqrMagnitude<1e-10f)return false;
        // A deepest normal can worsen a different face of an angled corner.
        // Project the combined escape onto all non-increasing contact halfspaces.
        Vector3 combined=Vector3.zero;
        foreach(Contact contact in before)combined+=contact.direction*(contact.depth+.002f);
        for(int pass=0;pass<16;pass++)
            foreach(Contact contact in before)
            {float against=Vector3.Dot(combined,contact.direction);if(against<0)combined-=contact.direction*against;}
        if(combined.sqrMagnitude>1e-10f)escape=combined.normalized*escape.magnitude;
        // Recovery is a straight translation at unchanged scale/orientation.
        // Starting overlaps may decrease; no newly encountered barrier may be
        // skipped between the two contact sets (especially minimum-size mats).
        foreach(Shape shape in s.shapes)
        {
            Vector3 center=accepted.position+accepted.rotation*(shape.center*accepted.scale);
            Vector3 half=Vector3.Max(Vector3.one*.001f,shape.half*accepted.scale-Vector3.one*.001f);
            int count;
            while(true)
            {
                if(!Spend(s)){Apply(s,accepted);return false;}
                count=Physics.BoxCastNonAlloc(center,half,escape.normalized,s.hits,accepted.rotation*shape.rotation,escape.magnitude,Mask,QueryTriggerInteraction.Ignore);
                if(count<s.hits.Length)break;
                if(s.hits.Length>=4096){Apply(s,accepted);return false;}
                Array.Resize(ref s.hits,s.hits.Length*2);
            }
            for(int i=0;i<count;i++)
            {
                Collider other=s.hits[i].collider;if(other==null || Own(s,other))continue;
                bool existing=false;
                foreach(Contact previous in before)if(previous.own==shape.collider && previous.other==other){existing=true;break;}
                if(!existing){s.obstacle=other.name;Apply(s,accepted);return false;}
            }
        }
        Pose candidate=accepted;candidate.position+=escape;candidate=Rebase(s,candidate);
        Apply(s,candidate);Bounds b=s.prop.WorldBounds;
        if(!StarshipWorld.InZone(b,s.prop.zone)){Apply(s,accepted);return false;}
        var after=new List<Contact>();Vector3 ignored;float remaining;
        if(!Contacts(s,candidate,after,out ignored,out remaining)){Apply(s,accepted);return false;}
        bool improving=remaining < total-.000001f;
        foreach(Contact next in after)
        {
            float old=0;
            foreach(Contact previous in before)if(previous.own==next.own && previous.other==next.other)old=Mathf.Max(old,previous.depth);
            // No new obstacle; none of the existing penetrations may increase.
            if(next.depth>old+.0001f)improving=false;
        }
        if(!improving){Apply(s,accepted);return false;}
        accepted=candidate;return true;
    }

    static bool CheckPose(Session s,Pose pose,out string reason)
    {
        Apply(s,pose);
        Bounds b=s.prop.WorldBounds;
        if(!StarshipWorld.InZone(b,s.prop.zone))
        {reason="현재 방의 배치 범위를 벗어납니다.";s.obstacle=reason;return false;}
        foreach(Shape shape in s.shapes)
        {
            int count;
            Vector3 center=pose.position+pose.rotation*(shape.center*pose.scale);
            Vector3 half=Vector3.Max(Vector3.one*.001f,shape.half*pose.scale-Vector3.one*.001f);
            if(!Spend(s)){reason="계산 계속 중";return false;}
            // OverlapBoxNonAlloc was stripped from this shipped managed API.
            // Use its complete preserved query rather than fabricate an icall.
            s.overlaps=Physics.OverlapBox(center,half,pose.rotation*shape.rotation,Mask,QueryTriggerInteraction.Ignore);
            count=s.overlaps.Length;
            for(int i=0;i<count;i++)
            {
                Collider other=s.overlaps[i];
                if(Own(s,other)) continue;
                if(other==s.game.controller && !s.intersectsPlayer(shape.collider)) continue;
                reason=other==s.game.controller?"플레이어와 겹칩니다.":"다른 사물이나 구조물과 겹칩니다.";
                s.obstacle=other.name;return false;
            }
        }
        reason=null;return true;
    }
    static bool Clear(Session s,Pose from,Pose to,out Vector3 normal)
    {
        normal=Vector3.zero;
        foreach(Shape shape in s.shapes)
        {
            Vector3 a=from.position+from.rotation*(shape.center*from.scale);
            Vector3 delta=to.position+to.rotation*(shape.center*to.scale)-a;
            Quaternion orientation=from.rotation*shape.rotation;
            Quaternion difference=Quaternion.Inverse(orientation)*(to.rotation*shape.rotation);
            Vector3 toHalf=shape.half*to.scale;
            Vector3 projected=Abs(difference*Vector3.right)*toHalf.x+Abs(difference*Vector3.up)*toHalf.y+Abs(difference*Vector3.forward)*toHalf.z;
            float angle=Quaternion.Angle(from.rotation,to.rotation)*.0174532924f;
            float arc=Vector3.Angle(from.direction,to.direction)*.0174532924f;
            float padding=(s.radius*Mathf.Max(from.scale,to.scale)*angle*angle+Mathf.Max(from.depth,to.depth)*arc*arc)*.125f
                +(s.radius*Mathf.Abs(from.scale-to.scale)*angle+Mathf.Abs(from.depth-to.depth)*arc)*.25f;
            // Conservative translation + rotation/scale envelope, not only a
            // min-scale box. Endpoints alone must not jump through thin walls.
            Vector3 half=Vector3.Max(Vector3.one*.001f,Vector3.Max(shape.half*from.scale,projected)+Vector3.one*(padding-.0015f));
            int count;
            if(delta.magnitude>.000001f)
            {
                while(true)
                {
                    if(!Spend(s)) return false;
                    count=Physics.BoxCastNonAlloc(a,half,delta.normalized,s.hits,orientation,delta.magnitude,Mask,QueryTriggerInteraction.Ignore);
                    if(count<s.hits.Length) break;
                    if(s.hits.Length>=4096){s.obstacle="충돌 질의 복잡도 제한";return false;}
                    Array.Resize(ref s.hits,s.hits.Length*2);
                }
                for(int i=0;i<count;i++)
                {
                    RaycastHit hit=s.hits[i];
                    if(hit.collider==null || Own(s,hit.collider) || hit.collider==s.game.controller) continue;
                    if(angle<.000001f && SeparatedMorph(shape,from,to,hit.collider,hit.normal,padding))continue;
                    // An initial touching hit must not forbid moving away.
                    if(hit.distance<.0001f && Vector3.Dot(delta,hit.normal)>.00001f && to.scale<=from.scale && angle<.0001f) continue;
                    s.obstacle=hit.collider.name;normal=hit.normal;Apply(s,from);return false;
                }
            }
            else if(angle>.000001f || to.scale>from.scale+.000001f)
            {
                if(!Spend(s)) return false;
                s.overlaps=Physics.OverlapBox(a,half,orientation,Mask,QueryTriggerInteraction.Ignore);
                count=s.overlaps.Length;
                for(int i=0;i<count;i++)
                {if(!Own(s,s.overlaps[i]) && s.overlaps[i]!=s.game.controller){s.obstacle=s.overlaps[i].name;Apply(s,from);return false;}}
            }
        }
        string reason;
        bool clear=CheckPose(s,to,out reason);
        if(!clear) Apply(s,from);
        return clear;
    }
    static bool SeparatedMorph(Shape shape,Pose from,Pose to,Collider other,Vector3 hitNormal,float deviation)
    {
        // An enlarged envelope can initially overlap a bank while the actual
        // object remains on the water side or slides upward along its side.
        // A common plane must also cover center-arc deviation; fixed object
        // orientation alone does not make the camera-relative trajectory linear.
        BoxCollider box=other as BoxCollider;
        SphereCollider sphere=other as SphereCollider;
        if(box==null && sphere==null)return false;
        Vector3 center=box!=null?box.transform.TransformPoint(box.center):sphere.bounds.center;
        Vector3 half=box!=null?Vector3.Scale(box.size*.5f,Abs(box.transform.lossyScale)):Vector3.zero;
        Vector3 a=from.position+from.rotation*(shape.center*from.scale);
        Vector3 b=to.position+to.rotation*(shape.center*to.scale);
        Quaternion ownRotation=from.rotation*shape.rotation;
        for(int axis=0;axis<4;axis++)
        {
            Vector3 n=axis==3?hitNormal:(box!=null?box.transform.rotation:ownRotation)*(axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward);
            if(n.sqrMagnitude<.5f)continue;
            if(Vector3.Dot(a-center,n)<0)n=-n;
            float ownRadius=Vector3.Dot(Abs(Quaternion.Inverse(ownRotation)*n),shape.half);
            float otherRadius;
            if(box!=null)otherRadius=Vector3.Dot(Abs(Quaternion.Inverse(box.transform.rotation)*n),half);
            else {Vector3 extent=sphere.bounds.extents;otherRadius=Mathf.Max(extent.x,Mathf.Max(extent.y,extent.z));}
            float gapA=Vector3.Dot(a-center,n)-ownRadius*from.scale-otherRadius;
            float gapB=Vector3.Dot(b-center,n)-ownRadius*to.scale-otherRadius;
            if(gapA>=deviation-.0009f && gapB>=deviation-.0009f)return true;
        }
        return false;
    }
}
