using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Event timing belongs to this exhibition. MuseumAudio owns independent
    // voices, indoor reverb, the pause clock and the existing shared sounds.
    public sealed class GalleryDikeAudio : MonoBehaviour
    {
        public struct CueStamp
        {
            public string cue;
            public float time;
            public Vector3 point;
        }
        public IReadOnlyList<CueStamp> History => history;
        public int FirstSwordContactCount { get; private set; }
        public float FirstSwordContactTime { get; private set; }
        public bool GlowAwaitingSelection => !MuseumAudio.Available||!MuseumAudio.Instance.HasClip("dike.glow");
        public GalleryBottleBalanceCourse Course { get; private set; }
        readonly List<CueStamp> history = new List<CueStamp>();
        GalleryDikePropImpact bottleImpact, bookImpact;
        float scaleSoundAngle, scaleCooldown, glassCooldown;
        bool pendingSwordContact, insertSoundStarted, wasInserting, doorStarted;

        public static GalleryDikeAudio Install(GalleryBottleBalanceCourse course)
        {
            var audio=course.GetComponent<GalleryDikeAudio>();
            if(audio!=null)return audio;
            audio=course.gameObject.AddComponent<GalleryDikeAudio>();audio.Course=course;
            course.GlassImpact+=audio.GlassContact;
            course.BalanceCompleted+=audio.GlowStarted;
            course.Reward.SwordReleased+=audio.SwordReleased;
            course.Reward.KeyTaken+=audio.SwordTaken;
            course.Reward.KeyInserted+=audio.SwordSeated;
            audio.bottleImpact=AttachProp(audio,course.Bottle,true);
            audio.bookImpact=AttachProp(audio,course.Sculpture,false);
            if(course.Reward.SwordTransform!=null)
            {
                var impact=course.Reward.SwordTransform.gameObject.AddComponent<GalleryDikeSwordContact>();
                impact.routing=audio;
            }
            audio.ResetSounds();
            return audio;
        }
        static GalleryDikePropImpact AttachProp(GalleryDikeAudio owner,GalleryProp prop,bool bottle)
        {
            var impact=prop.gameObject.AddComponent<GalleryDikePropImpact>();
            impact.Configure(owner,prop,bottle);return impact;
        }
        public int Count(string cue)
        {
            int count=0;foreach(var stamp in history)if(stamp.cue==cue)count++;return count;
        }
        bool Running => Course!=null&&Course.game!=null&&Course.game.active&&!Course.game.won&&!Course.game.Transitioning;
        public bool Play(string cue,Vector3 point,float volume=1)
        {
            if(!Running||!MuseumAudio.Available||!MuseumAudio.Instance.HasClip(cue))return false;
            if(!MuseumAudio.TryPlay(cue,this,point,volume))return false;
            if(history.Count>=256)history.RemoveAt(0);
            history.Add(new CueStamp{cue=cue,time=Time.time,point=point});return true;
        }
        void GlassContact(float speed,Vector3 point)
        {
            if(!Running||Course.Extracted||speed<.25f||glassCooldown>0)return;
            if(Play("dike.glass.contact",point,Mathf.Clamp(.6f+speed*.15f,.6f,1.15f)))glassCooldown=.12f;
        }
        void SwordReleased(){pendingSwordContact=true;}
        void GlowStarted(){Play("dike.glow",Course.Art.scaleStation.position+Vector3.up*1.8f);}
        void SwordTaken(){pendingSwordContact=false;Play("dike.sword.pickup",Course.Reward.KeyPickupPosition);}
        void SwordSeated()
        {
            MuseumAudio.StopOwner(this,"dike.sword.insert");
            Play("dike.sword.seat",Course.Reward.LockTransform.position);
            // The selected WAV is already slowed to 1.60 seconds. Do not apply
            // a second runtime pitch reduction to the same source.
            Play("dike.socket.rumble",Course.Reward.LockTransform.position);
        }
        public void SwordContact(float speed,Vector3 point)
        {
            if(!Running||!pendingSwordContact||Course.Reward.Stage!=GalleryBottleBalanceKeyReward.RewardStage.Falling||speed<.25f)return;
            pendingSwordContact=false;FirstSwordContactCount++;FirstSwordContactTime=Time.time;
            Play("dike.sword.land",point,Mathf.Clamp(.55f+speed*.12f,.55f,1.1f));
        }
        public static string ImpactCue(bool bottle,float scale)
        {
            string size=scale<1.35f?"small":scale<2.25f?"medium":"large";
            return "dike."+(bottle?"bottle.":"book.")+size;
        }
        public void Tick(float dt)
        {
            if(!Running||dt<=0)return;
            glassCooldown=Mathf.Max(0,glassCooldown-dt);scaleCooldown=Mathf.Max(0,scaleCooldown-dt);
            // Compare net travel with the last audible angle. Repeated tiny
            // oscillations around level must not accumulate into another cue.
            if(Mathf.Abs(Course.BeamDegrees-scaleSoundAngle)>=1.8f&&scaleCooldown<=0)
            {
                Play("dike.scale.motion",Course.Art.beam.position);
                scaleSoundAngle=Course.BeamDegrees;scaleCooldown=.65f;
            }
            var reward=Course.Reward;
            if(reward.Busy&&!wasInserting)insertSoundStarted=false;
            if(reward.Busy&&!insertSoundStarted&&reward.InsertionProgress>=GalleryBottleBalanceKeyReward.InsertAlignmentFraction)
            {insertSoundStarted=true;Play("dike.sword.insert",reward.LockTransform.position);}
            if(wasInserting&&!reward.Busy&&!reward.Unlocked)MuseumAudio.StopOwner(this,"dike.sword.insert");
            wasInserting=reward.Busy;
            if(!doorStarted&&Course.Door.Opening>0)
            {doorStarted=true;Play("dike.door.open",Course.Door.Root.position);}
        }
        public bool IsPan(Collider other)=>other!=null&&
            (other.transform==Course.Art.leftPan||other.transform.IsChildOf(Course.Art.leftPan)||
             other.transform==Course.Art.rightPan||other.transform.IsChildOf(Course.Art.rightPan));
        public void ResetSounds()
        {
            MuseumAudio.StopOwner(this);history.Clear();
            scaleSoundAngle=Course!=null?Course.BeamDegrees:12;scaleCooldown=glassCooldown=0;
            pendingSwordContact=insertSoundStarted=wasInserting=doorStarted=false;
            FirstSwordContactCount=0;FirstSwordContactTime=0;
            bottleImpact?.ResetContact();bookImpact?.ResetContact();
        }
        void OnDestroy()
        {
            MuseumAudio.StopOwner(this);
            if(Course==null)return;
            Course.GlassImpact-=GlassContact;
            Course.BalanceCompleted-=GlowStarted;
            if(Course.Reward==null)return;
            Course.Reward.SwordReleased-=SwordReleased;
            Course.Reward.KeyTaken-=SwordTaken;
            Course.Reward.KeyInserted-=SwordSeated;
        }
    }

    // Separate collision callbacks prevent the generic stone/metal bank from
    // playing over the selected bottle/book floor recordings.
    public sealed class GalleryDikePropImpact : MonoBehaviour
    {
        public const float BottleImpactInterval=1.10f;
        public const float BottleRollingNormalSpeed=.55f;
        GalleryDikeAudio routing;
        GalleryProp prop;
        bool bottle, previousSuppression, bottleHasImpacted;
        float lastContact=-1000;
        PropReleasePhysics release;
        public void Configure(GalleryDikeAudio owner,GalleryProp piece,bool isBottle)
        {
            routing=owner;prop=piece;bottle=isBottle;
            release=PropReleasePhysics.Attach(prop.body);previousSuppression=release.SuppressImpactAudio;
            release.SuppressImpactAudio=true;
        }
        public void ResetContact(){lastContact=-1000;bottleHasImpacted=false;}
        void Update()
        {
            // A new deliberate pickup/drop starts a new impact episode. Keep
            // the first landing audible even if the previous roll was recent.
            if(bottle&&prop!=null&&prop.placement!=null&&prop.placement.Held)ResetContact();
        }
        void OnCollisionEnter(Collision collision)
        {
            if(prop==null||prop.body==null||prop.body.isKinematic||routing==null)return;
            if(Time.time-lastContact<(bottle?BottleImpactInterval:.18f))return;
            if(!bottle&&!routing.Course.Extracted)return;
            // Releasing a book onto the pan already plays prop.release through
            // the ordinary interaction controller; do not duplicate that cue.
            if(routing.IsPan(collision.collider)||collision.collider.GetComponentInParent<GalleryDikePropImpact>()!=null)return;
            float speed=collision.relativeVelocity.magnitude;if(speed<.30f)return;
            if(bottle&&bottleHasImpacted)
            {
                // Rolling can have substantial sideways speed without another
                // real strike. Only a fresh normal impact earns a repeat cue.
                float normalSpeed=0;
                for(int i=0;i<collision.contactCount;i++)
                    normalSpeed=Mathf.Max(normalSpeed,Mathf.Abs(Vector3.Dot(collision.relativeVelocity,collision.GetContact(i).normal)));
                if(normalSpeed<BottleRollingNormalSpeed)return;
            }
            Vector3 point=collision.contactCount>0?collision.GetContact(0).point:prop.body.position;
            if(routing.Play(GalleryDikeAudio.ImpactCue(bottle,Mathf.Abs(prop.transform.lossyScale.x)),point,
                Mathf.Clamp(.5f+speed*.12f,.5f,1.15f)))
            {lastContact=Time.time;if(bottle)bottleHasImpacted=true;}
        }
        void OnDestroy(){if(release!=null)release.SuppressImpactAudio=previousSuppression;}
    }

    public sealed class GalleryDikeSwordContact : MonoBehaviour
    {
        public GalleryDikeAudio routing;
        void OnCollisionEnter(Collision collision)
        {
            if(routing==null)return;
            Vector3 point=collision.contactCount>0?collision.GetContact(0).point:transform.position;
            routing.SwordContact(collision.relativeVelocity.magnitude,point);
        }
    }
}
