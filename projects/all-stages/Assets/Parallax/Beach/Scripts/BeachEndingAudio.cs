using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Beach
{
    // The selected harbor layer follows real hoist/ship state. It never changes
    // mass, placement, progression, boat motion or the player's controls.
    public sealed class BeachEndingAudio : MonoBehaviour
    {
        public const float FlightGain=.065f, LargeRockGain=.22f, DepartureMusicGain=.14f;
        public const float PuzzleFadeSeconds=2f, DepartureFadeSeconds=1.5f;
        // HF33 is a 16-second comparison: music 5..14, wind 8..16.
        // Align that profile with the last 16 seconds of the real 24-second flight.
        public const float MusicFadeStart=13f,MusicFadeEnd=22f,WorldFadeStart=16f,WorldFadeEnd=24f;
        public static readonly string[] RequiredFiles={
            "rock_small","rock_medium","rock_large","load_latch","pulley","water_rise",
            "bridge_stop","canopy_drips","splash_small","splash_medium","splash_large",
            "departure","takeoff","flight","departure_bgm"};
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,AudioSource> sources=new Dictionary<string,AudioSource>();
        readonly Dictionary<string,int> counts=new Dictionary<string,int>();
        readonly Dictionary<string,float> gains=new Dictionary<string,float>();
        readonly HashSet<AudioSource> paused=new HashSet<AudioSource>();
        BeachGame game;
        BeachAudio bank;
        bool initialized,loadSeen,emergedSeen,readySeen,launchSeen,takeoffSeen,flightSeen,waterLatched;
        float musicWait,musicAge;
        bool musicStarted;
        public bool Running {get;private set;}
        public string MusicPhase {get;private set;}="Puzzle";
        public float PuzzleMusicEnvelope {get;private set;}=1;
        public float EndingEnvelope {get;private set;}=1;
        public float DepartureMusicEnvelope {get;private set;}
        public float ShoreEnvelope {get;private set;}=1;
        public BeachCanopyDrips Drips {get;private set;}
        public AudioSource MusicSource=>SourceFor("departure_bgm");
        public AudioSource PulleySource=>SourceFor("pulley");
        public AudioSource CanopyDripsSource=>SourceFor("canopy_drips");
        public event Action<string,AudioSource> CuePlayed;
        public static BeachEndingAudio Install(BeachGame owner,BeachAudio audio)
        {
            var value=owner.GetComponent<BeachEndingAudio>();
            if(value==null)value=owner.gameObject.AddComponent<BeachEndingAudio>();
            value.Initialize(owner,audio);return value;
        }
        void Initialize(BeachGame owner,BeachAudio audio)
        {
            if(initialized)return;initialized=true;game=owner;bank=audio;
            foreach(string key in RequiredFiles)
            {
                var clip=Resources.Load<AudioClip>("Beach/EndingAudio/"+key);clips[key]=clip;
                var node=new GameObject("Harbor selected "+key);node.transform.SetParent(transform,false);
                var source=node.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;
                source.clip=clip;source.dopplerLevel=0;source.volume=0;
                source.spatialBlend=key=="departure_bgm"||key=="flight"||key=="takeoff"?0:1;
                source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=7;source.maxDistance=90;
                source.priority=key=="departure_bgm"?85:150;sources[key]=source;gains[key]=0;
            }
            if(game.counterweightFinale!=null)
            {
                var node=new GameObject("Canopy runoff droplets");node.transform.SetParent(game.transform,false);
                Drips=node.AddComponent<BeachCanopyDrips>();Drips.Configure(game.counterweightFinale);
            }
            ResetState();
        }
        public AudioClip ClipFor(string key)=>clips.TryGetValue(key,out var clip)?clip:null;
        public AudioSource SourceFor(string key)=>sources.TryGetValue(key,out var source)?source:null;
        public int CountFor(string key)=>counts.TryGetValue(key,out int count)?count:0;
        public bool IsRock(BeachProp prop)=>prop!=null&&game!=null&&game.counterweightFinale!=null&&prop==game.counterweightFinale.Rock;
        public string RockCueFor(BeachProp prop,bool water)
        {
            int size=BeachAudio.SizeIndex(prop);
            return (water?"splash_":"rock_")+(size==0?"small":size==1?"medium":"large");
        }
        public void OnRockPickedUp(){waterLatched=false;}
        public void OnRockReleased(){waterLatched=false;}
        public bool TryRockContact(BeachProp prop,Collider surface,Vector3 point,float speed)
        {
            if(!Running||!IsRock(prop))return false;
            var f=game.counterweightFinale;
            // No extra contact sound on the weight tray, as selected. The latch
            // is the sole confirmation once real weight settles.
            if(f.LoadSupported||(surface!=null&&surface.transform.IsChildOf(f.cradle)))return false;
            string key=RockCueFor(prop,false);
            float gain=key=="rock_large"?LargeRockGain:.30f;
            return Play(key,point,gain*Mathf.Clamp(.5f+speed*.12f,.6f,1));
        }
        public bool TryRockWater(BeachProp prop,Vector3 point)
        {
            if(!Running||!IsRock(prop)||waterLatched||prop.body==null||prop.body.isKinematic)return false;
            waterLatched=true;return Play(RockCueFor(prop,true),point,.32f);
        }
        bool Play(string key,Vector3 position,float gain,bool loop=false,float startOffset=0)
        {
            if(!Running||!Application.isPlaying)return false;
            var source=SourceFor(key);if(source==null||source.clip==null)return false;
            source.Stop();paused.Remove(source);source.transform.position=position;
            source.pitch=1;source.loop=loop;gains[key]=gain;source.volume=gain*EndingEnvelope;
            source.time=Mathf.Clamp(startOffset,0,Mathf.Max(0,source.clip.length-.05f));
            source.Play();counts[key]=CountFor(key)+1;CuePlayed?.Invoke(key,source);return true;
        }
        public void SetRunning(bool value)
        {
            if(value==Running)return;Running=value;
            foreach(var source in sources.Values)
            {
                if(value){if(paused.Remove(source))source.UnPause();}
                else if(source.isPlaying){source.Pause();paused.Add(source);}
            }
        }
        public void ResetState()
        {
            foreach(var source in sources.Values){source.Stop();source.volume=0;}
            paused.Clear();counts.Clear();
            loadSeen=emergedSeen=readySeen=launchSeen=takeoffSeen=flightSeen=waterLatched=musicStarted=false;
            musicWait=musicAge=0;PuzzleMusicEnvelope=EndingEnvelope=ShoreEnvelope=1;DepartureMusicEnvelope=0;MusicPhase="Puzzle";
            foreach(string key in RequiredFiles)gains[key]=0;
            Drips?.ResetState();bank?.SetFinaleMix(1,1,1);
        }
        public void Tick(float dt)
        {
            if(!Running||dt<=0||game==null)return;
            dt=Mathf.Clamp(dt,0,.05f);
            var f=game.counterweightFinale;var ship=game.skyShip;
            if(f!=null)
            {
                Drips?.Tick(dt);
                Vector3 hoist=f.cradleRaisedPosition+Vector3.up*2.8f;
                Vector3 water=new Vector3(f.BridgeNearPoint.x,f.WaterSurface+.4f,f.BridgeNearPoint.z);
                if(f.LoadLatched&&!loadSeen)
                {
                    loadSeen=true;Play("load_latch",hoist,.30f);Play("pulley",hoist,.29f,true);
                }
                if(f.LoadLatched&&!emergedSeen&&f.BridgeVisualBounds.max.y>f.WaterSurface+.04f)
                {emergedSeen=true;Play("water_rise",water,.29f);}
                if(f.Ready&&!readySeen)
                {
                    readySeen=true;PulleySource.Stop();paused.Remove(PulleySource);
                    Play("bridge_stop",water+Vector3.up*1.5f,.30f);
                    Play("canopy_drips",water+Vector3.up*3,.15f,true);
                }
                if(readySeen&&CanopyDripsSource!=null)
                {
                    float wet=Drips==null?0:Drips.Wetness;
                    gains["canopy_drips"]=.15f*wet;
                    if(wet<=0){CanopyDripsSource.Stop();paused.Remove(CanopyDripsSource);}
                }
            }
            bool flying=ship!=null&&ship.Flying;
            bool aboard=ship!=null&&!flying&&ship.IsAboard&&f!=null&&f.Ready;
            if(flying&&!launchSeen)
            {
                launchSeen=true;bank.CancelBoat();Play("departure",ship.transform.position,.30f);
            }
            if(flying&&!takeoffSeen&&ship.FlightTime>=1.1f)
            {takeoffSeen=true;Play("takeoff",ship.transform.position,.22f);}
            if(flying&&!flightSeen&&ship.FlightTime>=3f)
            {flightSeen=true;Play("flight",ship.transform.position,FlightGain,true);}
            // Boarding fades the puzzle song before departure. Walking back off
            // restores it; the new song is never triggered by proximity alone.
            float target=aboard||flying?0:1;
            PuzzleMusicEnvelope=Mathf.MoveTowards(PuzzleMusicEnvelope,target,dt/PuzzleFadeSeconds);
            if(flying)
            {
                ShoreEnvelope=1-Mathf.SmoothStep(0,1,ship.FlightTime/6f);
                EndingEnvelope=1-Mathf.SmoothStep(0,1,(ship.FlightTime-WorldFadeStart)/(WorldFadeEnd-WorldFadeStart));
                float musicEnding=1-Mathf.SmoothStep(0,1,(ship.FlightTime-MusicFadeStart)/(MusicFadeEnd-MusicFadeStart));
                if(!musicStarted&&PuzzleMusicEnvelope<=.0001f)
                {
                    musicWait+=dt;
                    if(musicWait>=.15f)
                    {
                        var music=MusicSource;
                        if(music!=null&&music.clip!=null)
                        {
                            if(Play("departure_bgm",ship.transform.position,0,false,24))musicStarted=true;
                        }
                    }
                }
                if(musicStarted)musicAge+=dt;
                DepartureMusicEnvelope=Mathf.SmoothStep(0,1,musicAge/DepartureFadeSeconds)*musicEnding;
                MusicPhase=musicEnding<.999f?"FadingEnding":!musicStarted?"FadingPuzzle":
                    musicAge<DepartureFadeSeconds?"FadingInDeparture":"Sailing";
                if(musicEnding<=0)MusicPhase="Silent";
            }
            else
            {
                ShoreEnvelope=EndingEnvelope=1;DepartureMusicEnvelope=0;musicWait=0;
                MusicPhase=aboard?(PuzzleMusicEnvelope>0?"FadingPuzzle":"WaitingForDeparture"):"Puzzle";
            }
            // No duplicate HF33 music clip: its chosen music-first fade profile
            // is applied to the currently playing HB02 and world sources.
            foreach(var pair in sources)
            {
                if(pair.Key=="departure_bgm")pair.Value.volume=DepartureMusicGain*DepartureMusicEnvelope;
                else pair.Value.volume=gains[pair.Key]*EndingEnvelope;
            }
            bank.SetFinaleMix(PuzzleMusicEnvelope,ShoreEnvelope,EndingEnvelope);
        }
        void OnDisable()
        {
            foreach(var source in sources.Values)if(source!=null)source.Stop();
            paused.Clear();Running=false;Drips?.ResetState();bank?.SetFinaleMix(1,1,1);
        }
    }
}
