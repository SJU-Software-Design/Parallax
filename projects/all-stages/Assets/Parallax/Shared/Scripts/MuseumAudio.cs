using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink
{
    // Installed by GalleryGame only. Other stages continue using their own audio.
    // A voice owns its clip, pitch and position until its tail has finished.
    public sealed class MuseumAudio : MonoBehaviour
    {
        sealed class Definition
        {
            public string file; public float gain; public bool local;
            public Definition(string f,float g,bool l=false){file=f;gain=g;local=l;}
        }
        sealed class Voice
        {
            public AudioSource source; public AudioReverbFilter reverb;
            public Component owner; public string cue; public double until,pauseAt; public bool paused;
        }
        static readonly Dictionary<string,Definition> Definitions=new Dictionary<string,Definition>
        {
            {"movement.footstep",new Definition("Footstep81",.22f,true)},
            {"movement.land",new Definition("Land72",.34f,true)},
            {"prop.grab",new Definition("PropGrab128",.20f)},
            {"prop.release",new Definition("PropRelease156",.30f)},
            {"impact.wood.light",new Definition("WoodLight5",.30f)},
            {"impact.wood.medium",new Definition("WoodMedium8",.34f)},
            {"impact.wood.heavy",new Definition("WoodHeavy132",.40f)},
            {"impact.metal.light",new Definition("MetalLight11",.24f)},
            {"impact.metal.medium",new Definition("MetalMedium14",.27f)},
            {"impact.metal.heavy",new Definition("MetalHeavy157",.29f)},
            {"impact.stone.heavy",new Definition("StoneHeavy139",.45f)},
            {"nike.shadow_fit",new Definition("NikeShadow53",.32f)},
            {"nike.paint",new Definition("NikePaint146",.34f)},
            {"nike.unlock",new Definition("NikeUnlock57",.32f)},
            {"nike.door_open",new Definition("NikeDoor161",.32f)},
            {"face.smile",new Definition("FaceSmile151",.38f,true)},
            {"hands.open",new Definition("HandsOpen58",.40f)},
            {"hands.settle",new Definition("HandsSettle166",.32f)},
            {"ui.confirm",new Definition("UIConfirm42",.22f,true)},
            {"ui.cancel",new Definition("UICancel142",.22f,true)},
            {"ui.error",new Definition("UIError145",.20f,true)}
        };
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<Rigidbody,string> materials=new Dictionary<Rigidbody,string>();
        readonly List<Voice> voices=new List<Voice>();
        AudioSource musicA,musicB,currentMusic,incomingMusic;
        AudioClip musicClip;
        Transform listener;
        bool running,portraitSilent,musicStarted,crossfading;
        float crossfadeTime,musicFade,portraitGain=1;
        int footstepIndex;
        const float MusicGain=.105f,MusicCrossfadeSeconds=1.5f,PortraitFadeOutSeconds=2;
        public static MuseumAudio Instance {get;private set;}
        public static bool Available=>Instance!=null&&Instance.isActiveAndEnabled;
        public bool PortraitSilenced=>portraitSilent;
        public bool PortraitFadeOutComplete=>portraitSilent&&portraitGain<=0;
        public float PortraitFadeOutProgress=>portraitSilent?1-portraitGain:0;
        public bool GameplayRunning=>running;
        public bool MusicAudible=>running&&!PortraitFadeOutComplete&&currentMusic!=null&&currentMusic.isPlaying;
        public float MusicGainLevel=>musicA==null||musicB==null?0:Mathf.Sqrt(musicA.volume*musicA.volume+musicB.volume*musicB.volume);
        public float MusicTime=>currentMusic!=null?currentMusic.time:0;
        public string LastRequestedCue {get;private set;}
        public int RequestCount {get;private set;}
        public int ActiveVoiceCount {get {int count=0;foreach(var v in voices)if(v.cue!=null&&(v.paused||AudioSettings.dspTime<v.until))count++;return count;}}

        public static MuseumAudio Install(GameObject root,Transform playerListener)
        {
            var audio=root.GetComponent<MuseumAudio>();if(audio==null)audio=root.AddComponent<MuseumAudio>();
            Instance=audio;audio.listener=playerListener;audio.LoadBank();return audio;
        }
        void Awake(){Instance=this;}
        void LoadBank()
        {
            foreach(var pair in Definitions)if(!clips.ContainsKey(pair.Key))
                clips[pair.Key]=Resources.Load<AudioClip>("MuseumAudio/Selected/"+pair.Value.file);
            if(musicClip==null)musicClip=Resources.Load<AudioClip>("MuseumAudio/Selected/MuseumBgmC01");
            if(musicA==null){musicA=MusicSource("Museum BGM A");musicB=MusicSource("Museum BGM B");currentMusic=musicA;incomingMusic=musicB;}
        }
        AudioSource MusicSource(string title)
        {
            var go=new GameObject(title);go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();
            source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.dopplerLevel=0;
            source.priority=80;source.bypassReverbZones=true;source.volume=0;source.clip=musicClip;return source;
        }
        public bool HasClip(string cue){return clips.TryGetValue(cue,out var value)&&value!=null;}
        public AudioClip Clip(string cue){return clips.TryGetValue(cue,out var value)?value:null;}
        public static float Duration(string cue){return Available&&Instance.Clip(cue)!=null?Instance.Clip(cue).length:0;}
        public static bool IsMuseumBody(Rigidbody body)=>Available&&body!=null&&body.gameObject.scene==Instance.gameObject.scene&&body.transform.IsChildOf(Instance.transform);
        public void RegisterProp(Rigidbody body,string title)
        {
            if(body==null)return;
            string text=(title+" "+body.name).ToLowerInvariant();
            if(text.Contains("그림")||text.Contains("액자")||text.Contains("painting")||text.Contains("canvas")||text.Contains("wood")||text.Contains("판자")){materials[body]="wood";return;}
            if(text.Contains("청동")||text.Contains("bronze")||text.Contains("metal")||text.Contains("금속")){materials[body]="metal";return;}
            materials[body]="stone";
        }
        public static bool TryPlayMovement(PlayerMovementAudio.Cue cue,float impact,Vector3 position)
        {
            if(!Available)return false;
            if(cue==PlayerMovementAudio.Cue.Jump||cue==PlayerMovementAudio.Cue.None)return true; // No unselected takeoff cue.
            float variation=1;
            if(cue==PlayerMovementAudio.Cue.Footstep){int i=Instance.footstepIndex++;variation=1+(i%3-1)*.018f;}
            return TryPlay(cue==PlayerMovementAudio.Cue.Land?"movement.land":"movement.footstep",Instance,position,
                cue==PlayerMovementAudio.Cue.Land?Mathf.Lerp(.72f,1.15f,impact):1,variation);
        }
        public static bool TryPlayPropGrab(Rigidbody body)=>IsMuseumBody(body)&&TryPlay("prop.grab",body,body.position);
        public static bool TryPlayPropRelease(Rigidbody body)=>IsMuseumBody(body)&&TryPlay("prop.release",body,body.position);
        public static bool TryPlayPropImpact(Rigidbody body,float speed)
        {
            if(!IsMuseumBody(body))return false;
            if(!Instance.materials.TryGetValue(body,out string material)){Instance.RegisterProp(body,body.name);material=Instance.materials[body];}
            float mass=Mathf.Max(.25f,body.mass);string weight=mass<6?"light":mass<24?"medium":"heavy";
            if(material=="stone"&&weight!="heavy")return true; // Small stone contact was not selected.
            float pitch=Mathf.Clamp(1.04f-Mathf.Log(mass/8,2)*.035f,.74f,1.12f);
            return TryPlay("impact."+material+"."+weight,body,body.position,Mathf.Clamp(.45f+speed*.12f,.4f,1.15f),pitch);
        }
        public static bool TryPlay(string cue,Component owner,Vector3 position,float volume=1,float pitch=1)
        {
            if(!Available||(owner!=null&&owner.gameObject.scene!=Instance.gameObject.scene))return false;
            var audio=Instance;audio.LastRequestedCue=cue;audio.RequestCount++;
            if(!Definitions.TryGetValue(cue,out var definition)||!audio.clips.TryGetValue(cue,out var clip)||clip==null)return true;
            bool ui=cue.StartsWith("ui.",StringComparison.Ordinal);
            if(!Application.isPlaying||(!audio.running&&!ui))return true;
            var voice=audio.FreeVoice();if(voice==null)return true;
            voice.owner=owner;voice.cue=cue;voice.paused=false;voice.source.transform.position=position;
            voice.source.clip=clip;voice.source.pitch=Mathf.Clamp(pitch,.5f,1.5f);
            voice.source.volume=Mathf.Clamp01(definition.gain*volume);
            voice.source.spatialBlend=definition.local?0:.55f;
            bool indoor=position.z>=14&&!ui;voice.reverb.enabled=indoor;
            voice.source.priority=ui?100:cue.StartsWith("movement.",StringComparison.Ordinal)?160:110;
            voice.until=AudioSettings.dspTime+clip.length/voice.source.pitch+(indoor?.7:0);
            voice.source.Play();return true;
        }
        Voice FreeVoice()
        {
            double now=AudioSettings.dspTime;
            foreach(var v in voices)if(!v.paused&&(v.cue==null||now>=v.until)){v.cue=null;return v;}
            // Never steal a playing voice: dropping an excessive contact is better than cutting a tail.
            if(voices.Count>=48)return null;
            var go=new GameObject("Museum one-shot voice "+voices.Count);go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.dopplerLevel=0;
            source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=2;source.maxDistance=38;source.bypassReverbZones=true;
            var reverb=go.AddComponent<AudioReverbFilter>();reverb.reverbPreset=AudioReverbPreset.User;
            reverb.dryLevel=0;reverb.room=-2200;reverb.roomHF=-1700;reverb.decayTime=.62f;
            reverb.decayHFRatio=.65f;reverb.reflectionsLevel=-2200;reverb.reverbLevel=-2300;reverb.enabled=false;
            var voice=new Voice{source=source,reverb=reverb};voices.Add(voice);return voice;
        }
        public static void StopOwner(Component owner)
        {
            if(!Available||owner==null)return;
            foreach(var voice in Instance.voices)if(voice.owner==owner)
            {voice.source.Stop();voice.cue=null;voice.owner=null;voice.paused=false;}
        }
        public void SetGameplayRunning(bool value)
        {
            if(running==value)return;running=value;
            double now=AudioSettings.dspTime;
            foreach(var v in voices)
            {
                if(v.cue!=null&&v.cue.StartsWith("ui.",StringComparison.Ordinal))continue;
                if(!value&&v.cue!=null&&v.source.isPlaying){v.source.Pause();v.paused=true;v.pauseAt=now;}
                else if(value&&v.paused){v.until+=now-v.pauseAt;v.paused=false;v.source.UnPause();}
            }
            SynchronizeMusic();
        }
        public void SetPortraitSilence(bool silent)
        {
            if(portraitSilent==silent)return;portraitSilent=silent;
            if(silent)portraitGain=musicStarted?1:0;
            else
            {
                portraitGain=1;musicFade=0;
                if(musicA!=null)musicA.volume=0;
                if(musicB!=null)musicB.volume=0;
            }
            SynchronizeMusic();
        }
        public void SetCheckpoint(int checkpoint){if(checkpoint>=3)SetPortraitSilence(false);}
        void SynchronizeMusic()
        {
            if(musicA==null||musicClip==null||!Application.isPlaying)return;
            if(!running||PortraitFadeOutComplete){musicA.Pause();musicB.Pause();return;}
            if(!musicStarted)
            {currentMusic=musicA;incomingMusic=musicB;currentMusic.time=0;currentMusic.volume=0;currentMusic.Play();musicStarted=true;musicFade=0;}
            else {currentMusic.UnPause();if(crossfading)incomingMusic.UnPause();}
        }
        void Update()
        {
            // The portrait envelope and loop crossfade share the gameplay clock:
            // a menu pause freezes both, while SFX keep their independent voices.
            if(!running||PortraitFadeOutComplete||!musicStarted||musicClip==null)return;
            if(portraitSilent)
            {
                portraitGain=Mathf.MoveTowards(portraitGain,0,Time.unscaledDeltaTime/PortraitFadeOutSeconds);
                if(PortraitFadeOutComplete)
                {
                    musicA.volume=musicB.volume=0;
                    // Pause only after the fade reaches zero; retain both loop
                    // cursors and the in-progress crossfade for the next room.
                    SynchronizeMusic();return;
                }
            }
            else musicFade=Mathf.MoveTowards(musicFade,1,Time.unscaledDeltaTime/.5f);
            if(!crossfading&&!currentMusic.isPlaying){currentMusic.time=0;currentMusic.Play();musicFade=0;}
            float seconds=Mathf.Min(MusicCrossfadeSeconds,musicClip.length*.2f);
            if(!crossfading&&currentMusic.time>=musicClip.length-seconds)
            {incomingMusic.time=0;incomingMusic.volume=0;incomingMusic.Play();crossfading=true;crossfadeTime=0;}
            if(crossfading)
            {
                crossfadeTime+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(crossfadeTime/seconds);
                currentMusic.volume=MusicGain*musicFade*portraitGain*Mathf.Cos(t*Mathf.PI*.5f);
                incomingMusic.volume=MusicGain*musicFade*portraitGain*Mathf.Sin(t*Mathf.PI*.5f);
                if(t>=1){currentMusic.Stop();var previous=currentMusic;currentMusic=incomingMusic;incomingMusic=previous;crossfading=false;}
            }
            else currentMusic.volume=MusicGain*musicFade*portraitGain;
        }
        public void ResetPlayback()
        {
            foreach(var voice in voices){voice.source.Stop();voice.cue=null;voice.owner=null;voice.paused=false;}
            if(musicA!=null)musicA.Stop();if(musicB!=null)musicB.Stop();
            portraitSilent=false;portraitGain=1;musicStarted=crossfading=false;musicFade=0;footstepIndex=0;
            SynchronizeMusic();
        }
        void OnDisable(){running=false;ResetPlayback();if(musicA!=null)musicA.Stop();if(musicB!=null)musicB.Stop();}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
