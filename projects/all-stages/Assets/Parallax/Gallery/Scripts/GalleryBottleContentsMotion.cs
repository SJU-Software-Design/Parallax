using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Contained motion is integrated independently of the perspective controller.
    // Contacts use the vessel's real hollow compound shell, even while the shared
    // held-object layer deliberately excludes ordinary PhysX solver contacts.
    // The released sculpture then hands over to the existing dynamic rigidbody.
    public sealed class GalleryBottleContentsMotion
    {
        // The imported normalized mesh reaches .31524m from its origin.
        public const float Radius=.32f;
        public Vector3 Velocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }
        public int ImpactCount { get; private set; }
        public float LastImpactSpeed { get; private set; }
        public Vector3 LastImpactPoint { get; private set; }
        public bool BeyondMouth { get; private set; }
        public event Action<float,Vector3> GlassImpact;
        readonly GalleryProp bottle, piece;
        readonly Transform frame;
        readonly SphereCollider probe;
        readonly Collider[] nearby=new Collider[128];
        Vector3 origin, frameVelocity, previousFrameVelocity;
        Quaternion rotation;
        float scale, sinceImpact=1;
        bool initialized, poseChangedSinceTick;

        public GalleryBottleContentsMotion(GalleryProp bottle,GalleryProp piece,Transform parent)
        {
            this.bottle=bottle;this.piece=piece;
            frame=bottle.GetComponent<GalleryBottleBalanceVessel>().glassFrame;
            var go=new GameObject("Contained sculpture · contact query sphere");
            go.transform.SetParent(parent,false);go.layer=2;
            probe=go.AddComponent<SphereCollider>();probe.radius=Radius;probe.isTrigger=true;
        }
        public void Reset()
        {
            Velocity=AngularVelocity=frameVelocity=previousFrameVelocity=Vector3.zero;
            ImpactCount=0;LastImpactSpeed=0;sinceImpact=1;BeyondMouth=false;
            origin=frame.position;rotation=frame.rotation;scale=bottle.transform.lossyScale.x;
            initialized=true;
            piece.transform.SetPositionAndRotation(GalleryBottleBalanceVessel.ContentWorldPosition(bottle),Quaternion.identity);
            SetContainedPose();
        }
        public void Synchronize(float dt)
        {
            if(!initialized){Reset();return;}
            Vector3 nextOrigin=frame.position;Quaternion nextRotation=frame.rotation;
            float nextScale=bottle.transform.lossyScale.x;
            if((nextOrigin-origin).sqrMagnitude<.00000001f&&Quaternion.Angle(nextRotation,rotation)<.001f&&Mathf.Abs(nextScale-scale)<.00001f)return;
            dt=Mathf.Clamp(dt,1f/180,.05f);
            Vector3 oldOffset=piece.transform.position-origin;
            Vector3 newOffset=(nextRotation*Quaternion.Inverse(rotation))*oldOffset;
            // Rebase translation/rotation, never scale the independent piece.
            Vector3 previous=piece.transform.position;
            Vector3 rebased=nextOrigin+newOffset;
            piece.transform.position=SweepExternal(previous,rebased);
            frameVelocity=Vector3.ClampMagnitude((nextOrigin-origin+newOffset-oldOffset)/dt,5);
            Velocity=Vector3.ClampMagnitude(Velocity-(frameVelocity-previousFrameVelocity)*.48f,3.5f);
            previousFrameVelocity=frameVelocity;
            poseChangedSinceTick=true;
            bool resized=Mathf.Abs(nextScale-scale)>.003f;
            origin=nextOrigin;rotation=nextRotation;scale=nextScale;
            if(resized)KeepInsideAfterResize();
            SetContainedPose();
        }
        void KeepInsideAfterResize()
        {
            Vector3 p=Quaternion.Inverse(rotation)*(piece.transform.position-origin);
            // A perspective resize can cross the whole shell in one frame.
            // Re-seat only such discontinuities; ordinary shaking uses contacts.
            float inner=GalleryBottleBalanceVessel.InnerRadiusAt(bottle,p.x);
            if(p.x<-.62f*scale-Radius||p.x>1.05f*scale+Radius||
                inner<Radius+.005f||new Vector2(p.y,p.z).magnitude>inner-.006f)
            {piece.transform.position=SweepExternal(piece.transform.position,GalleryBottleBalanceVessel.ContentWorldPosition(bottle));Velocity*=.25f;}
        }
        Vector3 SweepExternal(Vector3 from,Vector3 to)
        {
            Vector3 motion=to-from;
            if(motion.sqrMagnitude<.00000001f)return to;
            float permitted=motion.magnitude;
            foreach(var hit in Physics.SphereCastAll(from,Radius,motion.normalized,permitted,~0,QueryTriggerInteraction.Ignore))
                if(!Owned(hit.collider)&&hit.collider!=probe&&hit.distance>.00001f)
                    permitted=Mathf.Min(permitted,Mathf.Max(0,hit.distance-.003f));
            return from+motion.normalized*permitted;
        }
        public void Tick(float dt,bool allowExit)
        {
            if(dt<=0)return;
            Synchronize(dt);BeyondMouth=false;
            if(!poseChangedSinceTick)
            {
                Velocity=Vector3.ClampMagnitude(Velocity+previousFrameVelocity*.48f,3.5f);
                previousFrameVelocity=frameVelocity=Vector3.zero;
            }
            int steps=Mathf.Clamp(Mathf.CeilToInt(dt/(1f/120)),1,8);
            float h=Mathf.Min(dt,.066f)/steps;
            for(int step=0;step<steps;step++)
            {
                sinceImpact+=h;
                Velocity+=Physics.gravity*h;
                Velocity*=Mathf.Exp(-.65f*h);
                Vector3 from=piece.transform.position;
                Vector3 at=from+Velocity*h;
                // The proxy follows a swept, small substep, preventing fast hand
                // motion from carrying the contents through a thin external wall.
                Vector3 motion=at-from;
                if(motion.sqrMagnitude>.00000001f)
                {
                    RaycastHit nearest=default;float distance=float.PositiveInfinity;
                    foreach(var hit in Physics.SphereCastAll(from,Radius,motion.normalized,motion.magnitude,~0,QueryTriggerInteraction.Ignore))
                    {
                        if(Owned(hit.collider)||hit.collider==probe||hit.distance<=.00001f||hit.distance>=distance)continue;
                        nearest=hit;distance=hit.distance;
                    }
                    if(nearest.collider!=null)
                    {at=from+motion.normalized*Mathf.Max(0,distance-.003f);Bounce(nearest.normal,at-nearest.normal*Radius,false);}
                }
                for(int pass=0;pass<3;pass++)
                {
                    bool changed=false;
                    foreach(var wall in bottle.parts)
                    {
                        if(wall==null||!wall.enabled)continue;
                        if(!Physics.ComputePenetration(probe,at,Quaternion.identity,wall,wall.transform.position,wall.transform.rotation,out Vector3 normal,out float depth))continue;
                        at+=normal*(depth+.001f);changed=true;
                        Bounce(normal,at-normal*Radius,true);
                    }
                    int count=Physics.OverlapSphereNonAlloc(at,Radius,nearby,~0,QueryTriggerInteraction.Ignore);
                    for(int i=0;i<count;i++)
                    {
                        var other=nearby[i];if(Owned(other)||other==probe)continue;
                        if(!Physics.ComputePenetration(probe,at,Quaternion.identity,other,other.transform.position,other.transform.rotation,out Vector3 normal,out float depth))continue;
                        at+=normal*(depth+.002f);changed=true;Bounce(normal,at-normal*Radius,false);
                    }
                    if(!changed)break;
                }
                Vector3 local=Quaternion.Inverse(rotation)*(at-origin);
                float mouth=1.05f*scale;
                if(!allowExit&&local.x>mouth-Radius)
                {local.x=mouth-Radius;at=origin+rotation*local;Bounce(-(rotation*Vector3.right),at,false);}
                // Wait for every rotated release-box corner to clear the lip
                // before restoring ordinary rigidbody contacts.
                BeyondMouth=allowExit&&local.x>mouth+ReleaseSupport(rotation*Vector3.right)+.025f;
                piece.transform.position=at;
                AngularVelocity*=Mathf.Exp(-2.2f*h);
                if(AngularVelocity.sqrMagnitude>.00001f)
                    piece.transform.rotation=Quaternion.AngleAxis(AngularVelocity.magnitude*h*Mathf.Rad2Deg,AngularVelocity.normalized)*piece.transform.rotation;
                if(BeyondMouth)break;
            }
            SetContainedPose();
            poseChangedSinceTick=false;
        }
        bool Owned(Collider shape)
        {var owner=shape.GetComponentInParent<GalleryProp>();return owner==piece||owner==bottle;}
        float ReleaseSupport(Vector3 direction)
        {
            float support=Radius;
            foreach(var shape in piece.parts)
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=shape.center+Vector3.Scale(shape.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 offset=shape.transform.TransformPoint(corner)-piece.transform.position;
                    support=Mathf.Max(support,-Vector3.Dot(offset,direction));
                }
            return support;
        }
        void Bounce(Vector3 normal,Vector3 point,bool glass)
        {
            float incoming=-Vector3.Dot(Velocity,normal);
            if(incoming<=0)return;
            Vector3 tangent=Velocity+normal*incoming;
            // Low glass friction removes only the normal impulse's fraction.
            // Multiplying all tangent velocity on every solver pass would pin
            // the piece to the shoulder even with a downhill open neck.
            tangent=Vector3.MoveTowards(tangent,Vector3.zero,incoming*.055f);
            Velocity=tangent+normal*incoming*.24f;
            AngularVelocity=Vector3.ClampMagnitude(AngularVelocity+Vector3.Cross(normal,tangent)*2.8f,9);
            if(!glass||incoming<.20f||sinceImpact<.075f)return;
            sinceImpact=0;ImpactCount++;LastImpactSpeed=incoming;LastImpactPoint=point;
            GlassImpact?.Invoke(incoming,point);
        }
        void SetContainedPose()
        {
            piece.transform.localScale=Vector3.one;
            foreach(var part in piece.parts)part.enabled=false;
            piece.body.isKinematic=true;piece.body.useGravity=false;
            piece.body.position=piece.transform.position;piece.body.rotation=piece.transform.rotation;
        }
        public Vector3 ReleaseVelocity=>Vector3.ClampMagnitude(Velocity+frameVelocity,4);
    }
}
