using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Beach
{
    // Scene-local selected sounds. Does not add listeners, water colliders or puzzle rules.
    public sealed class BeachAudio : MonoBehaviour
    {
        sealed class Voice { public AudioSource source; public bool paused; public float gain; }
        static readonly Dictionary<string,string> Files=new Dictionary<string,string>
        {
            {"step.sand","SandFootstep097"},{"land.sand","SandLand085"},
            {"step.wood","WoodFootstepBE13_01"},{"land.wood","WoodLand102"},
            {"prop.grab","PropGrab128"},{"prop.release","PropRelease156"},
            {"sand.small","SandImpactSmall091"},{"sand.medium","SandImpactMedium083"},{"sand.large","SandImpactLargeTeamBreak3"},
            {"wood.sand","WoodRodSandBE06"},
            {"wood.small","WoodImpactSmall005"},{"wood.medium","WoodImpactMedium008"},{"wood.large","WoodImpactLarge132"},
            {"water.small","WaterImpactSmall124"},{"water.medium","WaterImpactMediumTeamEnter1"},{"water.large","WaterImpactLargeBE10"},
            {"gate","GateMove027"},{"complete","Completion036"},{"boat.complete","BoatCompleteBC02"},{"bgm","BgmBB01_BossaAntigua"},
            {"shore","ShoreAmbience115"},{"boat.start","BoatStartBE12"},{"boat.run","BoatRunBE11"},{"boat.stop","BoatStopBE12"}
        };
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,int> played=new Dictionary<string,int>();
        readonly List<Voice> voices=new List<Voice>();
        readonly HashSet<BeachProp> specialWoodSand=new HashSet<BeachProp>();
        readonly HashSet<BeachLightGate> completedSolar=new HashSet<BeachLightGate>();
        readonly List<Vector3> exposedWater=new List<Vector3>();
        readonly Collider[] waterOverlap=new Collider[24];
        readonly RaycastHit[] shoreHits=new RaycastHit[32];
        AudioClip[] woodSteps;
        int woodStepIndex;
        AudioSource shore,boatStart,boatRun,boatStop;
        AudioSource musicA,musicB,currentMusic,incomingMusic;
        bool musicStarted,musicCrossfading;
        float musicCrossfadeElapsed;
        public const float MusicGain=.14f,MusicCrossfadeSeconds=1.5f;
        // User mix adjustment: footsteps -8 dB, music +5 dB; other cues retain their gains.
        public const float FootstepGain=.10f;
        public AudioClip MusicClip=>currentMusic==null?null:currentMusic.clip;
        public AudioSource MusicSource=>currentMusic;
        public AudioSource MusicIncomingSource=>incomingMusic;
        public bool MusicCrossfading=>musicCrossfading;
        public int MusicLoopCount {get;private set;}
        bool running,harborCompleted,boatRunStarted;
        float boatTime;
        enum Engine { Silent, Starting, Running, Fading, Stopped }
        Engine engine;
        BeachGame game;
        BeachWater[] waters=new BeachWater[0];
        public static BeachAudio Instance {get;private set;}
        public static bool Available => Instance!=null&&Instance.isActiveAndEnabled&&Instance.game!=null;
        public static readonly string[] RequiredFiles={
            "SandFootstep097","SandLand085","PropGrab128","PropRelease156",
            "SandImpactSmall091","SandImpactMedium083","SandImpactLargeTeamBreak3","WoodRodSandBE06",
            "WoodFootstepBE13_01","WoodFootstepBE13_02","WoodFootstepBE13_03","WoodFootstepBE13_04","WoodLand102",
            "ShoreAmbience115","WaterImpactSmall124","WaterImpactMediumTeamEnter1","WaterImpactLargeBE10",
            "WoodImpactSmall005","WoodImpactMedium008","WoodImpactLarge132","GateMove027","Completion036",
            "BoatStartBE12","BoatRunBE11","BoatStopBE12","BgmBB01_BossaAntigua","BoatCompleteBC02"};
        public BeachEndingAudio Ending {get;private set;}
        public float PuzzleMusicEnvelope {get;private set;}=1;
        float finaleShoreEnvelope=1,finaleWorldEnvelope=1;
        public void SetFinaleMix(float puzzle,float shoreGain,float world)
        {PuzzleMusicEnvelope=Mathf.Clamp01(puzzle);finaleShoreEnvelope=Mathf.Clamp01(shoreGain);finaleWorldEnvelope=Mathf.Clamp01(world);}
        public bool Running=>running;
        public float ShoreDistance {get;private set;}
        public float ShoreGain=>shore==null?0:shore.volume;
        public AudioSource ShoreSource=>shore;
        public AudioSource BoatRunSource=>boatRun;
        public AudioSource BoatStopSource=>boatStop;
        public string EngineState=>engine.ToString();
        public string LastMovementCue {get;private set;}
        public event Action<string,AudioSource> CuePlayed;

        public static BeachAudio Install(BeachGame game)
        {
            var a=game.GetComponent<BeachAudio>();if(a==null)a=game.gameObject.AddComponent<BeachAudio>();
            a.Initialize(game);return a;
        }
        void Awake(){Instance=this;}
        void Initialize(BeachGame owner)
        {
            game=owner;Instance=this;
            foreach(var f in Files)clips[f.Key]=Resources.Load<AudioClip>("BeachAudio/Selected/"+f.Value);
            woodSteps=new AudioClip[4];
            for(int i=0;i<woodSteps.Length;i++)woodSteps[i]=Resources.Load<AudioClip>("BeachAudio/Selected/WoodFootstepBE13_0"+(i+1));
            shore=Source("Beach shore 115",false,true,0);
            boatStart=Source("Beach boat BE12 start",true,false,.35f);
            boatRun=Source("Beach boat BE11 run",true,true,0);
            boatStop=Source("Beach boat BE12 shutdown",true,false,.35f);
            shore.clip=clips["shore"];boatStart.clip=clips["boat.start"];boatRun.clip=clips["boat.run"];boatStop.clip=clips["boat.stop"];
            // Full selected song; independent 2D voices crossfade only at its end.
            // Neither source loops by itself, so no hard PCM-boundary restart.
            musicA=Source("Beach BB01 music A",false,false,0);
            musicB=Source("Beach BB01 music B",false,false,0);
            musicA.clip=musicB.clip=clips["bgm"];musicA.priority=musicB.priority=90;
            currentMusic=musicA;incomingMusic=musicB;
            foreach(var p in game.props)
            {
                var state=p.GetComponent<BeachPropAudio>();if(state==null)state=p.gameObject.AddComponent<BeachPropAudio>();
                state.Initialize(p,this);
                if(p.title=="02 · 짧은 판자")specialWoodSand.Add(p);
            }
            if(game.shadowHarbor!=null)foreach(var p in game.shadowHarbor.Pieces)if(p!=null)specialWoodSand.Add(p);
            RebuildWaterSamples();
            Ending=BeachEndingAudio.Install(game,this);
        }
        AudioSource Source(string name,bool spatial,bool loop,float gain)
        {
            var node=new GameObject(name);node.transform.SetParent(transform,false);
            var s=node.AddComponent<AudioSource>();s.playOnAwake=false;s.loop=loop;s.volume=gain;
            s.spatialBlend=spatial?1:0;s.dopplerLevel=0;s.rolloffMode=AudioRolloffMode.Logarithmic;s.minDistance=2;s.maxDistance=35;s.priority=140;return s;
        }
        void Count(string cue,AudioSource source)
        {
            played[cue]=CountFor(cue)+1;CuePlayed?.Invoke(cue,source);
        }
        public int CountFor(string cue)=>played.TryGetValue(cue,out int n)?n:0;
        public AudioClip ClipFor(string cue)=>clips.TryGetValue(cue,out var clip)?clip:null;
        public bool UsesWoodSand(BeachProp p)=>specialWoodSand.Contains(p);
        public const float SmallSizeLimit=1.15f,LargeSizeLimit=2.75f;
        public static float PhysicalExtent(BeachProp p)
        {
            // Measure real collider dimensions in the prop's frame, in metres.
            // This avoids prefab normalization and orientation changing the band.
            Bounds local=default;bool first=true;
            foreach(var box in p.parts)
            {
                if(box==null)continue;
                for(int i=0;i<8;i++)
                {
                    Vector3 offset=Vector3.Scale(box.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 point=p.transform.InverseTransformPoint(box.transform.TransformPoint(box.center+offset));
                    if(first){local=new Bounds(point,Vector3.zero);first=false;}else local.Encapsulate(point);
                }
            }
            Vector3 actual=Vector3.Scale(local.size,p.transform.lossyScale);
            return Mathf.Max(Mathf.Abs(actual.x),Mathf.Abs(actual.y),Mathf.Abs(actual.z));
        }
        public static int SizeIndex(BeachProp p)
        {
            float metres=PhysicalExtent(p);return metres<SmallSizeLimit?0:metres<LargeSizeLimit?1:2;
        }
        static string SizeSuffix(int size)=>size<=0?"small":size==1?"medium":"large";
        public string ImpactCueFor(BeachProp prop,bool onWood,bool inWater=false)
        {
            if(inWater)return "water."+SizeSuffix(SizeIndex(prop));
            if(!onWood&&UsesWoodSand(prop))return "wood.sand";
            return (onWood?"wood.":"sand.")+SizeSuffix(SizeIndex(prop));
        }
        public float RetriggerDelay(string cue)
        {
            var c=ClipFor(cue);return Mathf.Max(.65f,c==null?.7f:c.length+.10f);
        }
        public bool Play(string cue,Vector3 position,float gain=1,AudioClip alternate=null)
        {
            if(!running||!Application.isPlaying)return false;
            AudioClip clip=alternate!=null?alternate:ClipFor(cue);if(clip==null)return false;
            Voice voice=null;
            foreach(var v in voices)if(!v.paused&&!v.source.isPlaying){voice=v;break;}
            if(voice==null)
            {
                if(voices.Count>=24)return false;
                voice=new Voice{source=Source("Beach one shot",true,false,1)};voices.Add(voice);
            }
            var s=voice.source;s.transform.position=position;s.pitch=1;voice.gain=Gain(cue)*gain;s.volume=voice.gain*finaleWorldEnvelope;s.clip=clip;s.loop=false;s.Play();voice.paused=false;Count(cue,s);return true;
        }
        static float Gain(string cue)
        {
            if(cue.StartsWith("step."))return FootstepGain;
            if(cue.StartsWith("land."))return .34f;
            if(cue=="shore")return .60f;
            if(cue=="prop.grab"||cue=="prop.release")return .28f;
            if(cue=="complete")return .24f;
            return .34f;
        }
        public void SetRunning(bool value)
        {
            if(value==running)return;running=value;
            Ending?.SetRunning(value);
            SetMusicRunning(value);
            foreach(var v in voices)
            {
                if(value){if(v.paused)v.source.UnPause();v.paused=false;}
                else if(v.source.isPlaying){v.source.Pause();v.paused=true;}
            }
            foreach(var s in new[]{shore,boatStart,boatRun,boatStop})
            {
                if(s==null)continue;
                if(value)s.UnPause();else s.Pause();
            }
            if(value&&shore!=null&&shore.clip!=null&&!shore.isPlaying){shore.volume=0;shore.Play();Count("shore",shore);}
        }
        void SetMusicRunning(bool value)
        {
            if(currentMusic==null||currentMusic.clip==null||!Application.isPlaying)return;
            if(value)
            {
                if(!musicStarted)
                {
                    currentMusic.time=0;currentMusic.volume=0;currentMusic.Play();musicStarted=true;
                    Count("bgm",currentMusic);
                }
                else
                {
                    currentMusic.UnPause();if(musicCrossfading)incomingMusic.UnPause();
                }
            }
            else
            {
                if(musicA.isPlaying)musicA.Pause();if(musicB.isPlaying)musicB.Pause();
            }
        }
        void Update()
        {
            // Same equal-power loop transport as the verified Tutorial music:
            // source sample time finds the boundary; paused gameplay never
            // advances the overlap envelope, even while DSP wall time advances.
            if(!running||currentMusic==null||currentMusic.clip==null||!musicStarted)return;
            if(!musicCrossfading&&!currentMusic.isPlaying)
            {
                currentMusic.time=0;currentMusic.volume=0;currentMusic.Play();
                Count("bgm.restart",currentMusic);
            }
            float overlap=Mathf.Min(MusicCrossfadeSeconds,currentMusic.clip.length*.2f);
            if(!musicCrossfading&&currentMusic.time>=currentMusic.clip.length-overlap)
            {
                incomingMusic.time=0;incomingMusic.volume=0;incomingMusic.Play();
                musicCrossfading=true;musicCrossfadeElapsed=0;Count("bgm.loop",incomingMusic);
            }
            if(!musicCrossfading){currentMusic.volume=MusicGain*PuzzleMusicEnvelope;return;}
            musicCrossfadeElapsed+=Time.unscaledDeltaTime;
            float t=Mathf.Clamp01(musicCrossfadeElapsed/overlap);
            currentMusic.volume=MusicGain*PuzzleMusicEnvelope*Mathf.Cos(t*Mathf.PI*.5f);
            incomingMusic.volume=MusicGain*PuzzleMusicEnvelope*Mathf.Sin(t*Mathf.PI*.5f);
            if(t>=1)
            {
                currentMusic.Stop();var previous=currentMusic;currentMusic=incomingMusic;incomingMusic=previous;
                musicCrossfading=false;MusicLoopCount++;
            }
        }
        public void ResetAudio()
        {
            foreach(var v in voices){v.source.Stop();v.paused=false;}
            CancelBoat();shore?.Stop();Ending?.ResetState();running=false;Ending?.SetRunning(false);woodStepIndex=0;completedSolar.Clear();harborCompleted=false;
            foreach(var p in game.props)p.GetComponent<BeachPropAudio>()?.ResetState();
            SetRunning(game.active&&!game.won);
        }
        public void ResetProp(BeachProp p)=>p.GetComponent<BeachPropAudio>()?.ResetState();
        public void SolarCompleted(BeachLightGate gate)
        {
            if(gate!=null&&completedSolar.Add(gate))Play("complete",gate.panel!=null?gate.panel.bounds.center:gate.transform.position);
        }
        public void BoatCompleted()
        {
            if(harborCompleted)return;harborCompleted=true;
            // Selected BC02 is independent of solar036 and starts at the reveal anchor.
            var harbor=game.shadowHarbor;
            Vector3 at=harbor!=null&&harbor.boatReleaseAnchor!=null?harbor.boatReleaseAnchor.position:
                harbor!=null&&harbor.target!=null?harbor.target.position:game.view.transform.position;
            Play("boat.complete",at);
        }
        public void GateOpened(BeachLightGate gate)=>Play("gate",gate.panel.bounds.center);
        public void BoatLaunched(BeachBoat boat)
        {
            if(boat==null)return;CancelBoat();engine=Engine.Starting;boatTime=0;
            boatStart.transform.position=boatRun.transform.position=boatStop.transform.position=boat.transform.position;
            if(running&&boatStart.clip!=null){boatStart.volume=.35f;boatStart.Play();Count("boat.start",boatStart);}
        }
        public void BoatDocked(BeachBoat boat)
        {
            if(engine==Engine.Silent||engine==Engine.Stopped||engine==Engine.Fading)return;
            engine=Engine.Fading;boatTime=0;
        }
        public Action CaptureBoatAudioState()
        {
            Engine savedEngine=engine;float elapsed=boatTime;bool savedRunStarted=boatRunStarted;
            var sources=new[]{boatStart,boatRun,boatStop};
            var samples=new int[sources.Length];var live=new bool[sources.Length];var gains=new float[sources.Length];
            for(int i=0;i<sources.Length;i++){samples[i]=sources[i].timeSamples;live[i]=sources[i].isPlaying;gains[i]=sources[i].volume;}
            return ()=>
            {
                CancelBoat();engine=savedEngine;boatTime=elapsed;boatRunStarted=savedRunStarted;
                for(int i=0;i<sources.Length;i++)
                {
                    var source=sources[i];source.volume=gains[i];
                    if(!live[i]||source.clip==null)continue;
                    source.timeSamples=Mathf.Clamp(samples[i],0,source.clip.samples-1);source.Play();
                    if(!running)source.Pause();
                }
            };
        }
        public void CancelBoat()
        {
            boatStart?.Stop();boatRun?.Stop();boatStop?.Stop();engine=Engine.Silent;boatTime=0;boatRunStarted=false;
        }
        public void Tick(float dt)
        {
            if(!running||dt<=0)return;
            Ending?.Tick(dt);
            foreach(var voice in voices)voice.source.volume=voice.gain*finaleWorldEnvelope;
            var feet=game.controller.transform.position;ShoreDistance=DistanceToExposedWater(feet);
            float target=.60f*Mathf.Lerp(.10f,1,Mathf.Exp(-ShoreDistance/5.5f))*finaleShoreEnvelope*finaleWorldEnvelope;
            shore.volume=Mathf.MoveTowards(shore.volume,target,dt*.24f);
            if(game.boat!=null)
            {
                Vector3 at=game.boat.transform.position;boatStart.transform.position=boatRun.transform.position=boatStop.transform.position=at;
            }
            boatTime+=dt;
            if(engine==Engine.Starting)
            {
                float startLength=boatStart.clip==null?0:boatStart.clip.length;
                if(boatTime>=Mathf.Max(0,startLength-.20f))
                {
                    if(!boatRunStarted&&boatRun.clip!=null){boatRun.volume=0;boatRun.Play();boatRunStarted=true;Count("boat.run",boatRun);}
                    boatRun.volume=Mathf.MoveTowards(boatRun.volume,.24f,dt*.8f);
                    if(boatTime>=startLength+.25f){engine=Engine.Running;boatTime=0;}
                }
            }
            else if(engine==Engine.Running)boatRun.volume=Mathf.MoveTowards(boatRun.volume,.24f,dt*.8f);
            else if(engine==Engine.Fading)
            {
                float remain=Mathf.Clamp01(1-boatTime/.75f);boatRun.volume=Mathf.Min(boatRun.volume,.24f*remain);boatStart.volume=.35f*remain;
                if(boatTime>=.75f)
                {
                    boatRun.Stop();boatStart.Stop();
                    if(boatStop.clip!=null){boatStop.volume=.35f;boatStop.Play();Count("boat.stop",boatStop);}
                    engine=Engine.Stopped;boatTime=0;
                }
            }
        }
        public static bool TryPlayMovement(PlayerMovementAudio.Cue cue,float impact,Vector3 feet)
        {
            if(!Available)return false;var a=Instance;
            // No unselected procedural jump cue; landing/steps use the support actually underfoot.
            if(cue==PlayerMovementAudio.Cue.Jump||cue==PlayerMovementAudio.Cue.None)return true;
            if(cue==PlayerMovementAudio.Cue.Land&&impact<.08f&&a.game.boat!=null&&a.game.boat.IsAboard)return true;
            bool wood=a.GroundIsWood(feet);string key=(cue==PlayerMovementAudio.Cue.Land?"land.":"step.")+(wood?"wood":"sand");
            a.LastMovementCue=key;
            AudioClip variation=null;
            if(key=="step.wood"&&a.woodSteps!=null){variation=a.woodSteps[a.woodStepIndex++%a.woodSteps.Length];}
            a.Play(key,feet,cue==PlayerMovementAudio.Cue.Land?Mathf.Lerp(.7f,1,impact):1,variation);return true;
        }
        public bool GroundIsWood(Vector3 feet)
        {
            var hits=Physics.RaycastAll(feet+Vector3.up*.22f,Vector3.down,.7f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity;Collider best=null;
            foreach(var h in hits)
            {
                if(h.collider==game.controller||h.normal.y<.45f||!PlacementSupport.IsUsableGround(h.collider))continue;
                var p=h.collider.GetComponentInParent<BeachProp>();if(p!=null&&p.placement!=null&&p.placement.Held)continue;
                if(h.distance<nearest){nearest=h.distance;best=h.collider;}
            }
            return IsWood(best);
        }
        public bool IsWood(Collider collider)
        {
            if(collider==null)return false;
            var p=collider.GetComponentInParent<BeachProp>();
            if(p!=null&&(p.GetComponent<BeachBoat>()!=null||UsesWoodSand(p)))return true;
            for(Transform t=collider.transform;t!=null&&t!=game.transform;t=t.parent)
            {
                string n=t.name.ToLowerInvariant();
                if(n.Contains("timber")||n.Contains("wood")||n.Contains("plank")||n.Contains("deck")||n.Contains("pier")||n.Contains("판자")||n.Contains("갑판")||n.Contains("부두")||n.Contains("목재"))return true;
                var renderer=t.GetComponent<Renderer>();
                if(renderer!=null)foreach(var m in renderer.sharedMaterials)
                {
                    if(m==null)continue;string material=m.name.ToLowerInvariant();
                    if(material.Contains("wood")||material.Contains("timber"))return true;
                }
            }
            return false;
        }
        public static bool TryGrab(Rigidbody body)
        {
            if(!Available||body==null||body.GetComponent<BeachProp>()==null)return false;
            body.GetComponent<BeachPropAudio>()?.BeginHold();
            if(Instance.Ending!=null&&Instance.Ending.IsRock(body.GetComponent<BeachProp>()))Instance.Ending.OnRockPickedUp();
            else Instance.Play("prop.grab",body.position);return true;
        }
        public static bool TryRelease(Rigidbody body)
        {
            if(!Available||body==null||body.GetComponent<BeachProp>()==null)return false;
            body.GetComponent<BeachPropAudio>()?.Released();
            if(Instance.Ending!=null&&Instance.Ending.IsRock(body.GetComponent<BeachProp>()))Instance.Ending.OnRockReleased();
            Instance.Play("prop.release",body.position);return true;
        }
        public static bool TryCollision(Rigidbody body,Collision collision)
        {
            if(!Available||body==null||body.GetComponent<BeachProp>()==null)return false;
            body.GetComponent<BeachPropAudio>()?.Collision(collision);return true;
        }
        public static bool TryImpact(Rigidbody body,float speed)
        {
            if(!Available||body==null||body.GetComponent<BeachProp>()==null)return false;
            var p=body.GetComponent<BeachProp>();body.GetComponent<BeachPropAudio>()?.Contact(null,p.WorldBounds.center,speed);return true;
        }
        bool IsCoveredByGround(Vector3 p)
        {
            // The fixed islands and piers hide water even when their deck sits
            // above the wave plane. A short vertical probe includes those floors,
            // excluding high canopies and every movable prop.
            int count=Physics.RaycastNonAlloc(p+Vector3.up*1.2f,Vector3.down,shoreHits,1.25f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=shoreHits[i];var floor=hit.collider;
                if(floor==game.controller||floor.GetComponentInParent<BeachProp>()!=null||hit.normal.y<.55f)continue;
                if(hit.point.y>=p.y-.02f)return true;
            }
            int n=Physics.OverlapSphereNonAlloc(p+Vector3.up*.015f,.03f,waterOverlap,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)
            {
                var c=waterOverlap[i];
                if(c==null||c==game.controller||c.GetComponentInParent<BeachProp>()!=null)continue;
                if(c.bounds.min.y<p.y+.04f&&c.bounds.max.y>p.y+.015f)return true;
            }
            return false;
        }
        public void RebuildWaterSamples()
        {
            waters=FindObjectsByType<BeachWater>(FindObjectsSortMode.None);exposedWater.Clear();Physics.SyncTransforms();
            foreach(var w in waters)
            {
                for(float x=w.min.x+.20f;x<w.max.x;x+=.40f)for(float z=w.min.y+.20f;z<w.max.y;z+=.40f)
                {
                    Vector3 point=new Vector3(x,w.surface,z);if(!IsCoveredByGround(point))exposedWater.Add(point);
                }
            }
        }
        public float DistanceToExposedWater(Vector3 point)
        {
            // Same dry-land cutout used by BeachSimpleWater: the huge ocean plane
            // covers the whole level but its interior [-14,14] x [0,108] is clipped.
            float coast;
            if(point.x< -14||point.x>14||point.z<0||point.z>108)coast=0;
            else coast=Mathf.Min(point.x+14,14-point.x,point.z,108-point.z);
            float oceanHeight=point.y-OceanSurface;float closest=coast*coast+oceanHeight*oceanHeight;
            foreach(var p in exposedWater)
            {
                float dx=p.x-point.x,dy=p.y-point.y,dz=p.z-point.z;closest=Mathf.Min(closest,dx*dx+dy*dy+dz*dz);
            }
            return Mathf.Sqrt(closest);
        }
        const float OceanSurface=-.17f;
        bool IntersectWater(Bounds bounds,Vector2 min,Vector2 max,float surface,out Vector3 point)
        {
            float left=Mathf.Max(bounds.min.x,min.x),right=Mathf.Min(bounds.max.x,max.x);
            float near=Mathf.Max(bounds.min.z,min.y),far=Mathf.Min(bounds.max.z,max.y);
            if(left>right||near>far||bounds.min.y>surface+.015f||bounds.max.y<surface-2){point=default;return false;}
            Vector3[] points={
                new Vector3(Mathf.Clamp(bounds.center.x,left,right),surface,Mathf.Clamp(bounds.center.z,near,far)),
                new Vector3(left,surface,near),new Vector3(left,surface,far),
                new Vector3(right,surface,near),new Vector3(right,surface,far)};
            foreach(var candidate in points)if(!IsCoveredByGround(candidate)){point=candidate;return true;}
            point=default;return false;
        }
        public bool WaterContact(Bounds bounds,out Vector3 point)
        {
            foreach(var w in waters)if(IntersectWater(bounds,w.min,w.max,w.surface,out point))return true;
            // The ocean renderer has no BeachWater component. Only its exposed
            // outside-cutout rectangles count, never the hidden plane under land.
            if(IntersectWater(bounds,new Vector2(-450,-410),new Vector2(-14,490),OceanSurface,out point)||
               IntersectWater(bounds,new Vector2(14,-410),new Vector2(450,490),OceanSurface,out point)||
               IntersectWater(bounds,new Vector2(-14,-410),new Vector2(14,0),OceanSurface,out point)||
               IntersectWater(bounds,new Vector2(-14,108),new Vector2(14,490),OceanSurface,out point))return true;
            point=default;return false;
        }
        public bool IsSubmergedPoint(Vector3 point)
        {
            foreach(var w in waters)if(w.Contains(point)&&point.y<w.surface-.04f&&!IsCoveredByGround(new Vector3(point.x,w.surface,point.z)))return true;
            bool ocean=point.x>=-450&&point.x<=450&&point.z>=-410&&point.z<=490&&
                (point.x< -14||point.x>14||point.z<0||point.z>108);
            return ocean&&point.y<OceanSurface-.04f&&!IsCoveredByGround(new Vector3(point.x,OceanSurface,point.z));
        }
        void OnDisable()
        {
            Ending?.SetRunning(false);
            foreach(var v in voices)v.source?.Stop();shore?.Stop();CancelBoat();
            musicA?.Stop();musicB?.Stop();musicStarted=musicCrossfading=false;
            if(Instance==this)Instance=null;
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
