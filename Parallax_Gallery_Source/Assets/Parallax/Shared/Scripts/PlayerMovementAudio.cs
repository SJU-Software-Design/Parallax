using UnityEngine;

namespace ShadeLink
{
    public sealed class PlayerMovementAudio : MonoBehaviour
    {
        public enum Cue { None, Footstep, Jump, Land }
        float stepTime,lastVertical;
        bool wasGrounded=true;
        AudioSource source;
        static AudioClip contact;

        public static PlayerMovementAudio Attach(CharacterController controller)
        {
            var value=controller.GetComponent<PlayerMovementAudio>();
            return value!=null?value:controller.gameObject.AddComponent<PlayerMovementAudio>();
        }
        public void Reset(){stepTime=lastVertical=0;wasGrounded=true;if(source!=null)source.Stop();}

        public Cue Tick(Vector3 velocity,bool grounded,bool movementInput,float dt)
        {
            if(dt<=0)return Cue.None;
            Cue cue=Cue.None;
            if(wasGrounded&&!grounded&&velocity.y>0)cue=Cue.Jump;
            else if(!wasGrounded&&grounded&&lastVertical<-.5f)cue=Cue.Land;
            bool walking=grounded&&movementInput&&new Vector2(velocity.x,velocity.z).sqrMagnitude>=.5f;
            stepTime=walking?stepTime+dt:0;
            if(cue==Cue.None&&stepTime>=.43f){stepTime%=.43f;cue=Cue.Footstep;}
            if(cue!=Cue.None)Play(cue,Mathf.Clamp01(-lastVertical/20));
            wasGrounded=grounded;lastVertical=velocity.y;
            return cue;
        }
        void Play(Cue cue,float impact)
        {
            if(source==null)
            {
                source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;
                source.spatialBlend=0;source.dopplerLevel=0;source.priority=150;
            }
            source.pitch=cue==Cue.Jump?1.4f:cue==Cue.Land?.8f:1;
            source.volume=cue==Cue.Land?.07f+.14f*impact:cue==Cue.Jump?.045f:.075f;
            if(!Application.isPlaying)return;
            if(contact==null)
            {
                const int rate=22050,count=1764;
                var samples=new float[count];uint noise=8675309;
                for(int i=0;i<count;i++)
                {
                    noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;
                    float t=(float)i/rate,p=(float)i/(count-1);
                    float envelope=Mathf.Sin(Mathf.PI*p)*Mathf.Exp(-p*6);
                    samples[i]=envelope*(Mathf.Sin(2*Mathf.PI*145*t)*.8f+((noise&65535)/32767.5f-1)*.2f);
                }
                contact=AudioClip.Create("Parallax soft movement",count,1,rate,false);
                contact.hideFlags=HideFlags.HideAndDontSave;contact.SetData(samples,0);
            }
            source.PlayOneShot(contact);
        }
    }
}
