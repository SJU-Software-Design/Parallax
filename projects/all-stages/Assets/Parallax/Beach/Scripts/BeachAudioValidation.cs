using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Beach
{
    // Focused native-player checks. Source poses are explicit fixtures; every
    // measured cue comes from the normal movement, prop, puzzle or boat hook.
    public sealed class BeachAudioValidation : MonoBehaviour
    {
        public BeachGame game;
        const float Dt=1f/60;
        static bool claimed;
        string output;
        BeachAudio audio;
        BeachWater[] waters;
        SimulationMode previousSimulation;
        readonly List<Check> checks=new List<Check>();
        readonly List<string> errors=new List<string>();
        readonly List<Playback> events=new List<Playback>();
        [Serializable] sealed class Check { public string name,detail; public bool passed; }
        [Serializable] sealed class Playback
        { public string cue,clip; public bool playing; public int sample; public float gain; public double dsp; }
        [Serializable] sealed class Report
        { public bool passed; public string scope,scene,unity,failure; public int resourceCount; public Check[] checks; public Playback[] playback; public string[] errors; }

        static bool Requested()=>Array.IndexOf(Environment.GetCommandLineArgs(),"-beach-audio-validate")>=0;
        static string Argument(string key)
        { var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(!Requested()||SceneManager.GetActiveScene().name=="Beach")return;
            var node=new GameObject("Beach audio validation scene loader");DontDestroyOnLoad(node);node.AddComponent<BeachAudioValidation>();
        }
        void CheckThat(bool value,string name,string detail="")
        {
            checks.Add(new Check{name=name,detail=detail,passed=value});
            Debug.Log((value?"BEACH_AUDIO_PASS ":"BEACH_AUDIO_FAIL ")+name+" "+detail);
            if(!value)throw new InvalidOperationException(name+": "+detail);
        }
        void CaptureLog(string message,string stack,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack); }
        void Played(string cue,AudioSource source)
        { events.Add(new Playback{cue=cue,clip=source.clip==null?"null":source.clip.name,playing=source.isPlaying,sample=source.timeSamples,gain=source.volume,dsp=AudioSettings.dspTime}); }
        IEnumerator Start()
        {
            if(!Requested()||claimed){Destroy(this);yield break;}claimed=true;
            output=Path.GetFullPath(Argument("-validation-output")??Argument("-shade-output")??Path.Combine(Application.persistentDataPath,"BeachAudioValidation"));
            Directory.CreateDirectory(output);Application.logMessageReceived+=CaptureLog;
            if(game==null)
            {
                yield return SceneManager.LoadSceneAsync("Beach",LoadSceneMode.Single);
                game=FindFirstObjectByType<BeachGame>();
            }
            yield return null;yield return null;
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            string failure=null;var sequence=Sequence();var stack=new Stack<IEnumerator>();stack.Push(sequence);
            while(stack.Count>0)
            {
                object item=null;bool more=false;
                try{more=stack.Peek().MoveNext();if(more)item=stack.Peek().Current;}
                catch(Exception ex){failure=ex.ToString();break;}
                if(!more){stack.Pop();continue;}
                if(item is IEnumerator nested){stack.Push(nested);continue;}yield return item;
            }
            if(audio!=null)audio.CuePlayed-=Played;
            Time.timeScale=1;Physics.simulationMode=previousSimulation;Application.logMessageReceived-=CaptureLog;
            bool passed=failure==null&&errors.Count==0&&checks.Count>0&&checks.All(c=>c.passed);
            var report=new Report{passed=passed,scene=SceneManager.GetActiveScene().name,unity=Application.unityVersion,
                scope="Focused selected-audio native PhysX/controller tests in the actual Beach scene. Explicit prop dimensions/positions, aligned mirror, shadow solution source poses and enlarged released boat/rider are declared fixtures. Puzzle completion, contacts, water entry, shade, support, launch and berth state are production results; no counts/completion/phase fields are injected. Script physics advances gameplay faster than real time; DSP playback/pause checks use actual realtime waits. Not an unassisted full-stage solve or subjective mix approval.",
                failure=failure,resourceCount=BeachAudio.RequiredFiles.Length,checks=checks.ToArray(),playback=events.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(Path.Combine(output,"beach-audio-results.json"),JsonUtility.ToJson(report,true));
            Debug.Log("BEACH_AUDIO_VALIDATION "+(passed?"PASSED":"FAILED")+" checks="+checks.Count+" output="+output);
            Application.Quit(passed?0:1);
        }
        IEnumerator Group(string name,IEnumerator sequence)
        {
            var stack=new Stack<IEnumerator>();stack.Push(sequence);
            while(stack.Count>0)
            {
                bool more=false;object item=null;string failure=null;
                try{more=stack.Peek().MoveNext();if(more)item=stack.Peek().Current;}
                catch(Exception ex){failure=ex.ToString();}
                if(failure!=null){checks.Add(new Check{name=name,detail=failure,passed=false});Debug.Log("BEACH_AUDIO_GROUP_FAIL "+name+" "+failure);yield break;}
                if(!more){stack.Pop();continue;}
                if(item is IEnumerator nested){stack.Push(nested);continue;}yield return item;
            }
        }
        IEnumerator Sequence()
        {
            CheckThat(game!=null&&game.Audio!=null,"Actual Beach scene and scene-local audio bank loaded");
            audio=game.Audio;audio.CuePlayed+=Played;waters=FindObjectsByType<BeachWater>(FindObjectsSortMode.None);
            game.automated=true;game.ResetAll();game.Resume();
            var clips=BeachAudio.RequiredFiles.Select(f=>Resources.Load<AudioClip>("BeachAudio/Selected/"+f)).ToArray();
            CheckThat(clips.Length==27&&clips.All(c=>c!=null&&c.samples>0&&c.frequency>0),"All 26 selected WAV resources including BC02 and full BB01 music load",string.Join(", ",clips.Where(c=>c!=null).Select(c=>c.name)));
            CheckThat(audio.ClipFor("gate").name=="GateMove027"&&audio.ClipFor("complete").name=="Completion036"&&audio.ClipFor("solar.progress")==null,
                "027 gate and 036 completion selected; no 035 solar charging cue");
            yield return Group("movement and ground materials",Movement());
            yield return Group("real prop impacts",Impacts());
            yield return Group("water entry and suppression",Water());
            yield return Group("shore exposure and distance",Shore());
            yield return Group("solar and shadow puzzle completion",Puzzles());
            yield return Group("actual boat engine lifecycle",Engine());
            yield return Group("pause and reset",PauseReset());
            CheckThat(events.Count>0&&events.All(e=>e.playing&&e.clip!="null"),"Every counted production cue started a real AudioSource", "playbacks="+events.Count);
        }
        void ResetFixture()
        { game.ResetAll();game.Resume();game.automated=true;Physics.SyncTransforms();game.lighting.Synchronize(); }
        void Step(Vector2 movement=default,bool jump=false)
        { game.Tick(Dt,movement,jump);PhysicsStep();game.lighting.Synchronize(); }
        void PhysicsStep()
        {
            foreach(var p in game.props)if(p.gameObject.activeInHierarchy)p.placement.Tick(Dt);
            foreach(var w in waters)w.Simulate(Dt);Physics.Simulate(Dt);Physics.SyncTransforms();
            foreach(var p in game.props)if(p.gameObject.activeInHierarchy)p.GetComponent<BeachPropAudio>()?.SampleWater();
        }
        IEnumerator Frames(int count,bool full=true)
        { for(int i=0;i<count;i++){if(full)Step();else PhysicsStep();if(i%20==19)yield return null;} }
        void Pose(BeachProp p,Vector3 position,float scale,Quaternion rotation,bool release)
        {
            p.Restore();p.transform.SetPositionAndRotation(position,rotation);p.transform.localScale=Vector3.one*scale;
            p.body.position=position;p.body.rotation=rotation;Physics.SyncTransforms();if(release)p.Release();game.lighting.Synchronize();
        }
        void Drop(BeachProp p,Vector3 surface,float metres)
        {
            p.Restore();p.BeginHold();p.transform.localScale=Vector3.one;p.transform.rotation=Quaternion.identity;
            p.transform.localScale=Vector3.one*(metres/BeachAudio.PhysicalExtent(p));p.transform.position=new Vector3(surface.x,3,surface.z);Physics.SyncTransforms();
            p.transform.position+=Vector3.up*(surface.y+.70f-p.WorldBounds.min.y);p.body.position=p.transform.position;p.body.rotation=p.transform.rotation;
            Physics.SyncTransforms();p.Release();p.body.linearVelocity=Vector3.down*2;
        }
        bool LiveEvent(string cue,int from)=>events.Skip(from).Any(e=>e.cue==cue&&e.playing);
        IEnumerator Movement()
        {
            ResetFixture();yield return Frames(8);int start=events.Count;int steps=audio.CountFor("step.sand"),lands=audio.CountFor("land.sand");
            Vector3 before=game.player.feet;
            for(int i=0;i<70;i++){Step(i<35?Vector2.left:Vector2.right);if(i%20==19)yield return null;}
            CheckThat(Vector3.Distance(before,game.player.feet)>.05f&&audio.CountFor("step.sand")>steps&&LiveEvent("step.sand",start),"Native controller walking on sand plays 097");
            Step(default,true);yield return Frames(100);
            CheckThat(audio.CountFor("land.sand")>lands&&LiveEvent("land.sand",start),"Native sand jump landing plays 085");
            ResetFixture();game.checkpoint=2;game.PlacePlayer(new Vector3(0,.24f,54.4f));yield return Frames(12);
            CheckThat(game.player.grounded&&audio.GroundIsWood(game.player.feet),"Actual departure-pier support is wood");
            int woodStart=events.Count;int woodSteps=audio.CountFor("step.wood"),woodLands=audio.CountFor("land.wood");
            for(int i=0;i<200;i++){Step(game.player.feet.x>1.25f?Vector2.left:game.player.feet.x< -1.25f?Vector2.right:(i/35)%2==0?Vector2.right:Vector2.left);if(i%20==19)yield return null;}
            var variations=events.Skip(woodStart).Where(e=>e.cue=="step.wood").Select(e=>e.clip).Distinct().ToArray();
            CheckThat(audio.CountFor("step.wood")>=woodSteps+4&&variations.Length==4,"Native wood walking cycles all four BE13 single steps",string.Join(",",variations));
            Step(default,true);yield return Frames(100);
            CheckThat(audio.CountFor("land.wood")>woodLands,"Native wood landing plays 102");
            var actualSteps=events.Skip(start).Where(e=>e.cue=="step.sand"||e.cue=="step.wood").ToArray();
            CheckThat(actualSteps.Any(e=>e.cue=="step.sand")&&actualSteps.Any(e=>e.cue=="step.wood")&&actualSteps.All(e=>Mathf.Abs(e.gain-.10f)<.0001f),
                "Actual sand and wood footsteps use quieter 0.10 gain (-8 dB)",string.Join(",",actualSteps.Select(e=>e.cue+"="+e.gain)));

            var deckPoints=new[]{new Vector3(-4,.30f,35.2f),new Vector3(4.5f,.30f,38.5f),new Vector3(-3.6f,.30f,33.95f),new Vector3(4.1f,.30f,39.9f),new Vector3(0,.24f,54.5f),new Vector3(0,.24f,69.3f)};
            CheckThat(deckPoints.All(p=>audio.GroundIsWood(p))&&!audio.GroundIsWood(new Vector3(0,0,82)),"All six actual fixed timber decks/piers and sand resolve by nearest real support");
        }
        IEnumerator Impacts()
        {
            var ordinary=game.mirror.prop;var parasol=game.props.First(p=>p.title=="01 · 파라솔");
            var sizes=new[]{.70f,1.8f,3.5f};var suffixes=new[]{"small","medium","large"};
            for(int i=0;i<3;i++)
            {
                ResetFixture();int from=events.Count;string cue="sand."+suffixes[i];int count=audio.CountFor(cue);
                Drop(ordinary,new Vector3(0,0,22),sizes[i]);yield return Frames(100,false);
                CheckThat(BeachAudio.SizeIndex(ordinary)==i&&audio.CountFor(cue)==count+1&&LiveEvent(cue,from),"Actual gravity sand impact "+suffixes[i],"extent="+BeachAudio.PhysicalExtent(ordinary));
                int impacts=audio.CountFor(cue);var state=ordinary.GetComponent<BeachPropAudio>();
                bool duplicate=state.Contact(null,ordinary.WorldBounds.center,3);
                CheckThat(!duplicate&&audio.CountFor(cue)==impacts,"Immediate duplicate contact is suppressed "+suffixes[i]);
                Drop(ordinary,new Vector3(0,0,22),sizes[i]);yield return Frames(100,false);
                CheckThat(audio.CountFor(cue)==impacts+1,"Fresh regrab and release restores first sand impact "+suffixes[i]);
                ResetFixture();from=events.Count;cue="wood."+suffixes[i];count=audio.CountFor(cue);
                // Isolate timber impact from the unrelated fixed shade roof.
                // A 3.5 m parasol released 0.70 m above this deck otherwise
                // initially intersects that 4.3 m roof before reaching timber.
                var shade=GameObject.Find("03 · 승선 준비 그늘");bool shadeWasActive=shade!=null&&shade.activeSelf;
                try
                {
                    if(shadeWasActive)shade.SetActive(false);Physics.SyncTransforms();
                    Drop(parasol,new Vector3(0,.24f,54.2f),sizes[i]);yield return Frames(100,false);
                    CheckThat(BeachAudio.SizeIndex(parasol)==i&&audio.CountFor(cue)==count+1&&LiveEvent(cue,from),"Actual parasol contact with timber plays 005/008/132 "+suffixes[i],
                        "Only fixed shade roof disabled; timber and prop unchanged; actual cues="+string.Join(",",events.Skip(from).Select(e=>e.cue+":"+e.clip)));
                }
                finally{if(shadeWasActive)shade.SetActive(true);Physics.SyncTransforms();}
            }
            var plank=game.props.First(p=>p.title=="02 · 짧은 판자");var special=new[]{plank,game.shadowHarbor.Pieces[0],game.shadowHarbor.Pieces[1]};
            CheckThat(special.All(audio.UsesWoodSand)&&game.props.Count(audio.UsesWoodSand)==3&&!audio.UsesWoodSand(parasol),"BE06 scope is exactly movable plank and both assembly pieces");
            foreach(var prop in special)
            {
                ResetFixture();int count=audio.CountFor("wood.sand");Drop(prop,new Vector3(0,0,22),1.8f);yield return Frames(100,false);
                CheckThat(audio.CountFor("wood.sand")==count+1,"Actual BE06 source prop falls onto sand",prop.title);
            }
        }
        IEnumerator Water()
        {
            var p=game.mirror.prop;var sizes=new[]{.7f,1.8f,3.5f};var suffixes=new[]{"small","medium","large"};
            for(int i=0;i<3;i++)
            {
                ResetFixture();string cue="water."+suffixes[i];int count=audio.CountFor(cue),from=events.Count;
                Drop(p,new Vector3(0,-.04f,36.8f),sizes[i]);
                for(int n=0;n<120&&audio.CountFor(cue)==count;n++){PhysicsStep();if(n%20==19)yield return null;}
                CheckThat(audio.CountFor(cue)==count+1&&LiveEvent(cue,from),"Actual falling bounds cross water surface "+suffixes[i]);
                var state=p.GetComponent<BeachPropAudio>();
                for(int n=0;n<12;n++){p.body.position+=Vector3.up*(n%2==0?.02f:-.02f);Physics.SyncTransforms();state.SampleWater();}
                CheckThat(audio.CountFor(cue)==count+1,"One released prop cannot retrigger splash by bobbing "+suffixes[i]);
            }
            ResetFixture();int ocean=audio.CountFor("water.small");Drop(p,new Vector3(17,-.17f,82),.7f);
            for(int n=0;n<100&&audio.CountFor("water.small")==ocean;n++){PhysicsStep();if(n%20==19)yield return null;}
            CheckThat(audio.CountFor("water.small")==ocean+1,"Actual external ocean entry works without a BeachWater component");
            CheckThat(!audio.WaterContact(new Bounds(new Vector3(0,-.17f,82),Vector3.one*.2f),out _)&&
                !audio.WaterContact(new Bounds(new Vector3(-4,-.04f,35.2f),Vector3.one*.1f),out _),"Hidden ocean under dry land and water under fixed island do not splash");
        }
        IEnumerator Shore()
        {
            ResetFixture();audio.RebuildWaterSamples();
            float dry=audio.DistanceToExposedWater(new Vector3(0,0,82)),near=audio.DistanceToExposedWater(new Vector3(13,0,82)),island=audio.DistanceToExposedWater(new Vector3(-4,.3f,35.2f));
            CheckThat(dry>8&&near<1.2f&&island>.65f,"Shore distance uses exposed water rather than giant ocean bounds", "dry="+dry+" near="+near+" island="+island);
            game.PlacePlayer(new Vector3(0,0,82));for(int i=0;i<150;i++)audio.Tick(Dt);float farGain=audio.ShoreGain;
            game.PlacePlayer(new Vector3(13,0,82));for(int i=0;i<150;i++)audio.Tick(Dt);float nearGain=audio.ShoreGain;
            CheckThat(nearGain>farGain*1.5f&&audio.ShoreSource.isPlaying&&audio.ShoreSource.loop,"115 shore ambience becomes louder near exposed shoreline", "far="+farGain+" near="+nearGain);
            int sample=audio.ShoreSource.timeSamples;yield return new WaitForSecondsRealtime(.09f);
            CheckThat(audio.ShoreSource.timeSamples!=sample,"Selected shore source advances real DSP samples");
        }
        IEnumerator Puzzles()
        {
            ResetFixture();int complete=audio.CountFor("complete"),gate=audio.CountFor("gate");var mirror=game.mirror.prop;
            Pose(mirror,new Vector3(-4,.03f,10),2.5f,Quaternion.identity,false);
            Vector3 target=(game.lightGate.sensors[0].position+game.lightGate.sensors[1].position)*.5f;
            game.mirror.transform.rotation=Quaternion.LookRotation((target-game.mirror.transform.position).normalized+BeachLighting.ToSun);game.lighting.Synchronize();
            yield return Frames(35);
            CheckThat(!game.lightGate.unlocked&&audio.CountFor("complete")==complete,"Incomplete solar charge has no completion sound");
            yield return Frames(65);
            CheckThat(game.lightGate.unlocked&&audio.CountFor("complete")==complete+1&&audio.CountFor("gate")==gate+1,"Actual aligned mirror completes solar once with 036 and opens gate with 027");
            yield return Frames(120);
            CheckThat(audio.CountFor("complete")==complete+1&&audio.CountFor("gate")==gate+1,"Solved solar gate does not repeat completion or movement sound");
            ResetFixture();game.checkpoint=2;game.PlacePlayer(BeachWorld.Checkpoints[2]);complete=audio.CountFor("complete");int boatComplete=audio.CountFor("boat.complete");var harbor=game.shadowHarbor;
            for(int i=0;i<harbor.Pieces.Length;i++)Pose(harbor.Pieces[i],harbor.SolutionPositions[i],harbor.SolutionScales[i],harbor.SolutionRotations[i],true);
            game.lighting.Synchronize();
            for(int i=0;i<480&&!harbor.Solved;i++){Step();if(i%20==19)yield return null;}
            CheckThat(harbor.Solved&&harbor.RevealCount>0&&audio.CountFor("complete")==complete&&audio.CountFor("boat.complete")==boatComplete+1&&audio.ClipFor("boat.complete")!=null&&audio.ClipFor("boat.complete").name=="BoatCompleteBC02","Actual Ready source silhouettes reveal boat with BC02 exactly once without reusing solar 036", "coverage="+harbor.Coverage+" charge="+harbor.Charge);
            yield return Frames(120);
            CheckThat(audio.CountFor("complete")==complete&&audio.CountFor("boat.complete")==boatComplete+1,"Boat materialization does not repeat BC02 or reuse solar completion");
        }
        IEnumerator Engine()
        {
            ResetFixture();game.checkpoint=2;game.PlacePlayer(BeachWorld.Checkpoints[2]);var boat=game.boat;boat.gameObject.SetActive(true);boat.ResetBoat();game.lighting.Rebuild();
            Pose(boat.Prop,new Vector3(0,.08f,59.8f),1.6f,Quaternion.identity,true);yield return Frames(260);
            CheckThat(boat.Prop.placement.Ready&&BeachWater.Supports(boat.Prop)&&!boat.TryLaunch(),"Native buoyant boat settles and cannot start engine without a rider");
            game.lighting.Synchronize();bool found=false;Vector3 rider=default;
            for(int z=0;z<16&&!found;z++)for(int x=0;x<11&&!found;x++)
            {
                Vector3 feet=boat.transform.TransformPoint(new Vector3(-.5f+x*.1f,.12f,-.9f+z*.1f));
                if(!Physics.Raycast(feet+Vector3.up*.1f,Vector3.down,out var support,.20f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)||support.collider.GetComponentInParent<BeachProp>()!=boat.Prop||!game.FootSafe(feet+Vector3.up*.025f))continue;
                rider=feet;found=true;
            }
            CheckThat(found,"Prepared boat has a genuinely shaded walkable deck");game.PlacePlayer(rider);yield return Frames(12);
            int starts=audio.CountFor("boat.start"),runs=audio.CountFor("boat.run"),stops=audio.CountFor("boat.stop");
            CheckThat(boat.IsAboard&&game.FootSafe(game.player.feet)&&audio.GroundIsWood(game.player.feet)&&boat.TryLaunch()&&audio.CountFor("boat.start")==starts+1,"Actual shaded rider launch starts BE12 exactly once");
            boat.TryLaunch();CheckThat(audio.CountFor("boat.start")==starts+1,"Repeated launch command does not restart engine");
            for(int i=0;i<Mathf.CeilToInt((audio.ClipFor("boat.start").length+.6f)/Dt);i++)audio.Tick(Dt);
            yield return new WaitForSecondsRealtime(.08f);
            CheckThat(audio.EngineState=="Running"&&audio.BoatRunSource.isPlaying&&audio.BoatRunSource.loop&&audio.CountFor("boat.run")==runs+1,"BE11 engine loop follows the BE12 start");
            // Near-arrival pose fixture; berth is still decided by real hull/pier bounds.
            game.PlacePlayer(new Vector3(0,.24f,69.3f));
            for(int i=0;i<100&&!boat.AtBerth;i++)
            {boat.Prop.body.position+=Vector3.forward*.15f;boat.transform.position=boat.Prop.body.position;Physics.SyncTransforms();boat.Tick(Dt);}
            CheckThat(boat.AtBerth&&audio.EngineState=="Fading","Actual hull/pier proximity starts shutdown fade");
            for(int i=0;i<60;i++)audio.Tick(Dt);
            CheckThat(audio.EngineState=="Stopped"&&!audio.BoatRunSource.isPlaying&&audio.BoatStopSource.isPlaying&&audio.CountFor("boat.stop")==stops+1,"Fade stops BE11 then plays BE12 shutdown once");
            for(int i=0;i<90;i++){boat.Tick(Dt);audio.Tick(Dt);}
            CheckThat(audio.CountFor("boat.stop")==stops+1,"Stationary berth does not repeat shutdown");
            boat.Prop.Restore();CheckThat(audio.EngineState=="Silent"&&!audio.BoatRunSource.isPlaying&&!audio.BoatStopSource.isPlaying,"Actual boat restore cancels all engine voices");
        }
        IEnumerator PauseReset()
        {
            ResetFixture();game.PlacePlayer(BeachWorld.Checkpoints[0]);audio.Tick(Dt);yield return new WaitForSecondsRealtime(.15f);
            var music=audio.MusicSource;
            CheckThat(audio.MusicClip!=null&&Mathf.Abs(audio.MusicClip.length-283.3502f)<.15f&&music!=null&&music.isPlaying&&music.spatialBlend==0&&audio.MusicIncomingSource!=null,
                "BB01 full official track uses the scene-local two-source music transport", "seconds="+(audio.MusicClip==null?0:audio.MusicClip.length));
            CheckThat(Mathf.Abs(music.volume-.14f)<.001f,"Actual BB01 source gain increased to 0.14 (+5 dB)","gain="+music.volume);
            int musicBefore=music.timeSamples;yield return new WaitForSecondsRealtime(.12f);
            CheckThat(music.timeSamples!=musicBefore,"BB01 music advances real DSP samples");
            float gain=audio.ShoreGain,distance=audio.ShoreDistance;game.Pause();int cues=events.Count;
            CheckThat(!audio.Running&&!audio.ShoreSource.isPlaying&&!music.isPlaying,"Pause synchronously pauses shore and BB01 sources",
                "running="+audio.Running+" shore="+audio.ShoreSource.isPlaying+" music="+music.isPlaying);
            AudioSettings.GetDSPBufferSize(out int buffer,out int buffers);
            yield return new WaitForSecondsRealtime(Mathf.Max(.05f,2f*buffer/AudioSettings.outputSampleRate+.03f));
            int sample=audio.ShoreSource.timeSamples,musicPaused=music.timeSamples;
            yield return new WaitForSecondsRealtime(.10f);audio.Tick(.5f);
            CheckThat(Mathf.Abs(audio.ShoreSource.timeSamples-sample)<=64&&Mathf.Abs(music.timeSamples-musicPaused)<=64,
                "Paused streaming cursors stay frozen after DSP buffers settle",
                "buffer="+buffer+" count="+buffers+" shore="+sample+"->"+audio.ShoreSource.timeSamples+" music="+musicPaused+"->"+music.timeSamples);
            CheckThat(Mathf.Abs(audio.ShoreGain-gain)<.0001f&&Mathf.Abs(audio.ShoreDistance-distance)<.0001f,
                "Paused shore distance mix does not advance", "gain="+gain+"->"+audio.ShoreGain+" distance="+distance+"->"+audio.ShoreDistance);
            var prop=game.mirror.prop;prop.BeginHold();prop.Release();PhysicsStep();
            CheckThat(events.Count==cues,"Paused prop interaction produces no selected audio");
            game.Resume();yield return new WaitForSecondsRealtime(.15f);
            CheckThat(audio.Running&&audio.ShoreSource.isPlaying&&audio.ShoreSource.timeSamples!=sample&&music.isPlaying&&music.timeSamples!=musicPaused,
                "Resume continues real shore and BB01 samples");
            int shore=audio.CountFor("shore"),musicAtReset=music.timeSamples,bgmCount=audio.CountFor("bgm");double resetDsp=AudioSettings.dspTime;game.ResetAll();game.Resume();
            CheckThat(audio.CountFor("shore")==shore+1&&audio.EngineState=="Silent"&&!audio.BoatRunSource.isPlaying&&!audio.BoatStopSource.isPlaying&&audio.ShoreSource.isPlaying,
                "Full reset leaves one shore loop and no engine leakage");
            int afterReset=music.timeSamples;
            double resetElapsed=AudioSettings.dspTime-resetDsp;
            // ResetAll rebuilds scene/lighting synchronously while music keeps
            // playing on the DSP thread. Expect that elapsed playback, not a
            // frozen cursor. The modulo also accepts a genuine clip boundary.
            int advance=(afterReset-musicAtReset+music.clip.samples)%music.clip.samples;
            double expectedAdvance=resetElapsed*music.clip.frequency;
            float tolerance=Mathf.Max(128,2f*buffer*music.clip.frequency/AudioSettings.outputSampleRate);
            CheckThat(audio.CountFor("bgm")==bgmCount&&audio.MusicSource==music&&music.isPlaying&&Math.Abs(advance-expectedAdvance)<=tolerance,
                "Full reset preserves BB01 music transport instead of restarting it",
                "samples="+musicAtReset+"->"+afterReset+" advance="+advance+" expected="+expectedAdvance+" dspSeconds="+resetElapsed+" tolerance="+tolerance+" bgmCount="+bgmCount+"->"+audio.CountFor("bgm")+" sameSource="+(audio.MusicSource==music)+" playing="+music.isPlaying);
            var loops=game.GetComponentsInChildren<AudioSource>(true).Where(s=>s.isPlaying&&s.loop).ToArray();
            CheckThat(loops.Length==1&&loops[0]==audio.ShoreSource&&FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.isActiveAndEnabled)==1,
                "Beach owns one shore loop, music transport and one active listener");
        }
    }
}