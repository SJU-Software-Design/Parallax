using System;
using UnityEngine;

namespace ShadeLink.Beach
{
    // One state per real prop: first drops remain audible, solver bounces and
    // floating water-line oscillation cannot repeatedly trigger the same sound.
    public sealed class BeachPropAudio : MonoBehaviour
    {
        BeachProp prop;
        BeachAudio audio;
        bool released,waterLatched;
        float nextImpact;
        public void Initialize(BeachProp owner,BeachAudio bank){prop=owner;audio=bank;ResetState();}
        public void ResetState(){released=false;waterLatched=false;nextImpact=0;}
        public Action CaptureState()
        {
            bool wasReleased=released,wasWet=waterLatched;float next=nextImpact;
            return ()=>{released=wasReleased;waterLatched=wasWet;nextImpact=next;};
        }
        public void BeginHold()=>ResetState();
        public void Released(){released=true;waterLatched=false;nextImpact=0;}
        public void Collision(Collision collision)
        {
            if(collision==null||collision.contactCount==0)return;
            float normalSpeed=0;Vector3 point=collision.GetContact(0).point;
            for(int i=0;i<collision.contactCount;i++)
                normalSpeed=Mathf.Max(normalSpeed,Mathf.Abs(Vector3.Dot(collision.relativeVelocity,collision.GetContact(i).normal)));
            Contact(collision.collider,point,normalSpeed);
        }
        public bool Contact(Collider surface,Vector3 point,float normalSpeed)
        {
            if(audio==null||prop==null||!audio.Running||!released||prop.body==null||prop.body.isKinematic||normalSpeed<.4f||Time.time<nextImpact)return false;
            if(surface!=null&&surface.GetComponentInParent<BeachProp>()==prop)return false;
            if(audio.IsSubmergedPoint(point))return false;
            if(audio.Ending!=null&&audio.Ending.IsRock(prop))
            {
                bool heard=audio.Ending.TryRockContact(prop,surface,point,normalSpeed);
                if(heard)
                {
                    var clip=audio.Ending.ClipFor(audio.Ending.RockCueFor(prop,false));
                    nextImpact=Time.time+Mathf.Max(1.4f,clip==null?1.4f:clip.length+.15f);
                }
                return heard;
            }
            string cue=audio.ImpactCueFor(prop,audio.IsWood(surface));
            bool played=audio.Play(cue,point,Mathf.Clamp(.5f+normalSpeed*.12f,.6f,1));
            if(played)nextImpact=Time.time+audio.RetriggerDelay(cue);return played;
        }
        public bool SampleWater()
        {
            if(audio==null||prop==null||!released||!audio.Running||prop.body==null||prop.body.isKinematic)return false;
            var boat=prop.GetComponent<BeachBoat>();Bounds bounds=boat!=null?boat.HullBounds:prop.WorldBounds;
            if(!audio.WaterContact(bounds,out var point))return false;
            if(waterLatched)return false;
            waterLatched=true;
            if(audio.Ending!=null&&audio.Ending.IsRock(prop))return audio.Ending.TryRockWater(prop,point);
            string cue=audio.ImpactCueFor(prop,false,true);
            bool played=audio.Play(cue,point);if(played)nextImpact=Time.time+audio.RetriggerDelay(cue);return played;
        }
        void FixedUpdate(){SampleWater();}
    }
}
