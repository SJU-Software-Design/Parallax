using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;
using ShadeLink.Beach;

namespace ShadeLink
{
    // Uses the public pause-menu transport, never SceneManager.LoadScene from
    // the validator. Explicit sceneLoaded automation prevents hidden-window
    // focus from pausing the destination; it does not resume or reset games.
    public sealed class StageTravelValidation : MonoBehaviour
    {
        static bool claimed;
        readonly List<Check> checks=new List<Check>();
        readonly List<string> errors=new List<string>();
        readonly List<string> route=new List<string>();
        readonly List<string> cursorObservations=new List<string>();
        string output;
        double deadline;
        bool observedDirectGallery;
        int transitions;
        static readonly BindingFlags PrivateInstance=BindingFlags.NonPublic|BindingFlags.Instance;
        [Serializable] sealed class Check { public string name,detail; public bool passed; }
        [Serializable] sealed class Report
        {
            public bool passed,batchMode,normalWindowCursorLockVerified,osPointerLockRequiresVisibleReview;
            public string scope,failure,scene,unity;
            public int completedTransitions;
            public Check[] checks;
            public string[] errors,route,cursorObservations;
        }
        static bool Requested()=>Array.IndexOf(Environment.GetCommandLineArgs(),"-stage-travel-validate")>=0;
        static string Argument(string key)
        {var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,key);return at>=0&&at+1<args.Length?args[at+1]:null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(!Requested()||claimed)return;claimed=true;
            var node=new GameObject("Pause-menu stage travel native validation");
            DontDestroyOnLoad(node);node.AddComponent<StageTravelValidation>();
        }
        void Awake(){SceneManager.sceneLoaded+=Loaded;Application.logMessageReceived+=Log;}
        void Loaded(Scene scene,LoadSceneMode mode)
        {
            Application.runInBackground=true;
            var gallery=FindFirstObjectByType<GalleryGame>();if(gallery!=null)gallery.automated=true;
            var beach=FindFirstObjectByType<BeachGame>();if(beach!=null)beach.automated=true;
            if(scene.name=="Gallery"&&StageTravelMenu.Loading)
                observedDirectGallery=StageTravelMenu.DirectGalleryEntryRequested;
        }
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        void CheckThat(bool value,string name,string detail="")
        {
            checks.Add(new Check{name=name,detail=detail,passed=value});
            Debug.Log((value?"STAGE_TRAVEL_PASS ":"STAGE_TRAVEL_FAIL ")+name+" "+detail);
            if(!value)throw new InvalidOperationException(name+": "+detail);
        }
        void Guard(string phase)
        {if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Stage travel watchdog: "+phase+" scene="+SceneManager.GetActiveScene().name+" loading="+StageTravelMenu.Loading+" failure="+StageTravelMenu.LastFailure);}
        IEnumerator Start()
        {
            output=Path.GetFullPath(Argument("-validation-output")??Path.Combine(Application.persistentDataPath,"StageTravelValidation"));
            Directory.CreateDirectory(output);Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
            deadline=Time.realtimeSinceStartupAsDouble+240;
            string failure=null;var stack=new Stack<IEnumerator>();stack.Push(Sequence());
            while(stack.Count>0)
            {
                bool more=false;object item=null;
                try{more=stack.Peek().MoveNext();if(more)item=stack.Peek().Current;}
                catch(Exception error){failure=error.ToString();break;}
                if(!more){stack.Pop();continue;}
                if(item is IEnumerator nested){stack.Push(nested);continue;}yield return item;
            }
            bool passed=failure==null&&errors.Count==0&&transitions==6&&checks.All(c=>c.passed);
            var report=new Report{passed=passed,batchMode=Application.isBatchMode,normalWindowCursorLockVerified=!Application.isBatchMode&&passed,osPointerLockRequiresVisibleReview=Application.isBatchMode,cursorObservations=cursorObservations.ToArray(),failure=failure,scene=SceneManager.GetActiveScene().name,unity=Application.unityVersion,
                completedTransitions=transitions,checks=checks.ToArray(),errors=errors.ToArray(),route=route.ToArray(),
                scope="Actual initial Tutorial and six public pause-menu LoadStage operations: Tutorial→Gallery→Beach→Tutorial→Tutorial(fresh same-scene restart)→Gallery→Tutorial. Each request follows the game's production pause command. Tutorial's private SetPaused command and initial step number use minimal reflection because no public pause/lesson accessor exists. Destination automation only prevents hidden-window focus pause, without supplying ResetAll/Resume or completion state. Assertions inspect real scene/game/source destruction, fresh first-room state, display camera/controller/listener ownership, Dike installation, absence of fake elevator arrival, exact source music identity, preserved per-stage import policy (Tutorial/Beach Streaming; existing Museum DecompressOnLoad) and live PCM cursor advancement, cursor/time-scale, invalid requests and synchronous reentrancy rejection. In batch mode, logical unpaused gameplay, time scale and pointer visibility are asserted while the observed OS cursor-lock value is recorded without asserting physical lock; a separate normal visible-window computer-use check is required for physical cursor lock. Non-batch execution still strictly asserts Locked and hidden at each destination. UI tab clicking/layout, completed-game UI rendering, continuous puzzle solving and subjective listening are outside this test."};
            File.WriteAllText(Path.Combine(output,"stage-travel-results.json"),JsonUtility.ToJson(report,true));
            Debug.Log("STAGE_TRAVEL_VALIDATION "+(passed?"PASSED":"FAILED")+" transitions="+transitions+" checks="+checks.Count+" "+failure);
            Application.Quit(passed?0:1);
        }
        IEnumerator Sequence()
        {
            yield return null;yield return null;
            CheckThat(SceneManager.GetActiveScene().name=="Tutorial"&&FindFirstObjectByType<TutorialGame>()!=null,
                "Travel validation starts in the real Tutorial entry scene");
            foreach(string stage in new[]{"Tutorial","Gallery","Beach"})
                CheckThat(Application.CanStreamedLevelBeLoaded(stage),"Destination belongs to the actual build: "+stage);
            route.Add("Tutorial initial");yield return VerifyDestination("Tutorial",false);
            string[] destinations={"Gallery","Beach","Tutorial","Tutorial","Gallery","Tutorial"};
            for(int index=0;index<destinations.Length;index++)
            {
                string target=destinations[index];var priorScene=SceneManager.GetActiveScene();
                MonoBehaviour prior=CurrentGame();int priorId=prior==null?0:prior.GetInstanceID();
                var priorSources=FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Where(s=>s.gameObject.scene==priorScene).ToArray();
                var priorFlow=TutorialMuseumElevatorFlow.Instance;
                PauseCurrent();
                if(index==0)
                {
                    foreach(string invalid in new string[]{null,"","Library","gallery","Beach/../Gallery"})
                        CheckThat(!StageTravelMenu.LoadStage(invalid)&&!StageTravelMenu.Loading&&SceneManager.GetActiveScene()==priorScene,
                            "Invalid destination rejected without a scene mutation",invalid??"<null>");
                }
                observedDirectGallery=false;
                CheckThat(StageTravelMenu.LoadStage(target)&&StageTravelMenu.Loading,
                    "Paused game accepts one public travel request: "+priorScene.name+"→"+target);
                CheckThat(!StageTravelMenu.LoadStage(target)&&!StageTravelMenu.LoadStage(target=="Beach"?"Tutorial":"Beach"),
                    "Loading synchronously rejects same and competing duplicate requests",target);
                double until=Time.realtimeSinceStartupAsDouble+45;
                while(StageTravelMenu.Loading||SceneManager.GetActiveScene().name!=target)
                {
                    Guard(target);
                    if(Time.realtimeSinceStartupAsDouble>until)throw new TimeoutException("Destination load did not finish: "+target+" failure="+StageTravelMenu.LastFailure);
                    yield return null;
                }
                yield return null;yield return null;
                CheckThat(string.IsNullOrEmpty(StageTravelMenu.LastFailure)&&StageTravelMenu.LastCompletedStage==target&&!StageTravelMenu.DirectGalleryEntryRequested,
                    "Public travel completion clears its request state",target+" completed="+StageTravelMenu.LastCompletedStage+" failure="+StageTravelMenu.LastFailure);
                CheckThat(prior==null&&priorSources.All(s=>s==null)&&priorFlow==null,
                    "Previous game, scene audio sources and elevator owner were destroyed",priorScene.name+"→"+target+" priorSources="+priorSources.Length);
                var current=CurrentGame();
                CheckThat(current!=null&&current.GetInstanceID()!=priorId,"Single-load creates a fresh destination game instance",target+" oldId="+priorId+" newId="+(current==null?0:current.GetInstanceID()));
                if(target=="Gallery")CheckThat(observedDirectGallery,"Direct-Gallery intent was present during destination construction");
                route.Add(priorScene.name+"→"+target+(priorScene.name==target?" fresh restart":""));
                yield return VerifyDestination(target,true);transitions++;
            }
        }
        MonoBehaviour CurrentGame()
        {
            switch(SceneManager.GetActiveScene().name)
            {
                case "Tutorial":return FindFirstObjectByType<TutorialGame>();
                case "Gallery":return FindFirstObjectByType<GalleryGame>();
                case "Beach":return FindFirstObjectByType<BeachGame>();
                default:return null;
            }
        }
        void PauseCurrent()
        {
            var tutorial=FindFirstObjectByType<TutorialGame>();var gallery=FindFirstObjectByType<GalleryGame>();var beach=FindFirstObjectByType<BeachGame>();
            if(tutorial!=null)
            {
                var method=typeof(TutorialGame).GetMethod("SetPaused",PrivateInstance);
                CheckThat(method!=null,"Tutorial exposes its existing production pause command to the test harness");
                method.Invoke(tutorial,new object[]{true});
                CheckThat(tutorial.ElevatorPaused,"Tutorial production pause actually pauses gameplay");
            }
            else if(gallery!=null){gallery.Pause();CheckThat(!gallery.active&&Time.timeScale==0,"Gallery production Pause enters the pause menu");}
            else if(beach!=null){beach.Pause();CheckThat(!beach.active&&Time.timeScale==0,"Beach production Pause enters the pause menu");}
            else CheckThat(false,"A real game must own the pause request");
            CheckThat(Cursor.lockState==CursorLockMode.None&&Cursor.visible,"Pause releases the pointer before travel");
        }
        IEnumerator VerifyDestination(string expected,bool travelled)
        {
            Guard("verify "+expected);yield return new WaitForSecondsRealtime(.24f);
            var scene=SceneManager.GetActiveScene();var game=CurrentGame();
            int tutorialCount=FindObjectsByType<TutorialGame>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            int galleryCount=FindObjectsByType<GalleryGame>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            int beachCount=FindObjectsByType<BeachGame>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            CheckThat(scene.name==expected&&SceneManager.sceneCount==1&&game!=null&&game.gameObject.scene==scene&&tutorialCount+galleryCount+beachCount==1,
                "Only the destination game and its one loaded stage remain",expected+" gameCounts="+tutorialCount+"/"+galleryCount+"/"+beachCount);
            var controllers=FindObjectsByType<CharacterController>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.gameObject.activeInHierarchy).ToArray();
            var cameras=FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.isActiveAndEnabled&&c.targetTexture==null).ToArray();
            var listeners=FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l=>l.isActiveAndEnabled).ToArray();
            CheckThat(controllers.Length==1&&cameras.Length==1&&listeners.Length==1&&controllers[0].gameObject.scene==scene&&cameras[0].gameObject.scene==scene&&listeners[0].gameObject.scene==scene,
                "Destination owns one live controller, display camera and listener",expected+" counts="+controllers.Length+"/"+cameras.Length+"/"+listeners.Length);
            CheckThat(!TutorialMuseumElevatorFlow.ArrivalPending&&!StageTravelMenu.Loading&&Time.timeScale==1,
                "Travel leaves no elevator arrival or loading clock behind",expected+" timeScale="+Time.timeScale);
            string pointer=expected+" batch="+Application.isBatchMode+" observedLock="+Cursor.lockState+" visible="+Cursor.visible;
            cursorObservations.Add(pointer);
            if(Application.isBatchMode)
                CheckThat(!Cursor.visible,"Batch-mode destination hides the pointer; physical OS lock requires visible-window review",pointer);
            else
                CheckThat(Cursor.lockState==CursorLockMode.Locked&&!Cursor.visible,"Destination restores the normal gameplay pointer policy",pointer);
            if(travelled)CheckThat(FindObjectsByType<StageTravelMenu>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"One persistent menu-transport owner serves all repeated trips",expected);
            string musicName;
            if(expected=="Tutorial")
            {
                var tutorial=(TutorialGame)game;var step=typeof(TutorialGame).GetField("step",PrivateInstance);
                CheckThat(step!=null&&(int)step.GetValue(tutorial)==0&&!tutorial.ElevatorPaused&&!tutorial.ElevatorDeparted&&tutorial.Controller!=null&&tutorial.Camera!=null,
                    "Fresh Tutorial starts unpaused at lesson one without an elevator departure");
                CheckThat(new Vector2(tutorial.Controller.transform.position.x,tutorial.Controller.transform.position.z).magnitude<.2f,
                    "Tutorial controller begins at its actual first-room spawn",tutorial.Controller.transform.position.ToString());
                CheckThat(TutorialAudio.Available&&TutorialAudio.Instance.gameObject.scene==scene&&MuseumAudio.Instance==null&&BeachAudio.Instance==null,
                    "Tutorial audio singleton is fresh and previous stage audio singletons are gone");
                CheckThat(TutorialMuseumElevatorFlow.Instance!=null&&TutorialMuseumElevatorFlow.Instance.CurrentTutorial==tutorial&&!TutorialMuseumElevatorFlow.Instance.IsLoading,
                    "Fresh Tutorial owns exactly its idle elevator flow");
                musicName="BgmTB01_000_179";
            }
            else if(expected=="Gallery")
            {
                var gallery=(GalleryGame)game;
                CheckThat(gallery.active&&gallery.started&&!gallery.won&&gallery.checkpoint==0&&!gallery.ElevatorArrival&&gallery.bottleBalance!=null&&!gallery.bottleBalance.Solved,
                    "Direct Gallery starts in its first exhibit with Dike installed and no fake elevator arrival");
                CheckThat(Vector2.Distance(new Vector2(gallery.controller.transform.position.x,gallery.controller.transform.position.z),new Vector2(gallery.SpawnPoints[0].x,gallery.SpawnPoints[0].z))<.2f,
                    "Gallery uses its first exhibit spawn",gallery.controller.transform.position.ToString());
                CheckThat(MuseumAudio.Available&&MuseumAudio.Instance.gameObject.scene==scene&&MuseumAudio.Instance.GameplayRunning&&!MuseumAudio.Instance.PortraitSilenced&&TutorialAudio.Instance==null&&BeachAudio.Instance==null,
                    "Direct Gallery resumes its own music service and clears earlier audio singletons");
                CheckThat(TutorialMuseumElevatorFlow.Instance==null&&FindObjectsByType<TutorialElevatorRide>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0,
                    "Direct Gallery contains no old persistent elevator cabin");
                musicName="MuseumBgmC01";
            }
            else
            {
                var beach=(BeachGame)game;
                CheckThat(beach.active&&beach.started&&!beach.won&&beach.checkpoint==0&&beach.Audio!=null&&beach.Audio.Running,
                    "Direct Beach automatically resumes its fresh first checkpoint");
                CheckThat(Vector2.Distance(new Vector2(beach.controller.transform.position.x,beach.controller.transform.position.z),new Vector2(BeachWorld.Checkpoints[0].x,BeachWorld.Checkpoints[0].z))<.2f,
                    "Beach uses its actual first shoreline spawn",beach.controller.transform.position.ToString());
                CheckThat(BeachAudio.Available&&BeachAudio.Instance==beach.Audio&&BeachAudio.Instance.gameObject.scene==scene&&TutorialAudio.Instance==null&&MuseumAudio.Instance==null&&TutorialMuseumElevatorFlow.Instance==null,
                    "Beach owns its new audio service and no previous game audio/elevator singleton");
                musicName="BgmBB01_BossaAntigua";
            }
            var candidates=game.GetComponentsInChildren<AudioSource>(true).Where(s=>s.clip!=null&&s.clip.name==musicName).ToArray();
            var music=candidates.FirstOrDefault(s=>s.isPlaying)??candidates.FirstOrDefault();
            // Existing museum C01 is intentionally DecompressOnLoad in both
            // its default and Standalone importer. Do not change that bank to
            // satisfy a validator's assumption about the newer streaming songs.
            var expectedLoad=expected=="Gallery"?AudioClipLoadType.DecompressOnLoad:AudioClipLoadType.Streaming;
            CheckThat(music!=null&&music.isPlaying&&music.volume>0&&music.spatialBlend==0&&Mathf.Abs(music.pitch-1)<.0001f&&music.clip.loadType==expectedLoad,
                "Destination starts its selected music with its preserved import policy",expected+" expectedClip="+musicName+" expectedLoad="+expectedLoad+" "+MusicState(music));
            int sample=music.timeSamples;yield return new WaitForSecondsRealtime(.16f);
            CheckThat(music!=null&&music.isPlaying&&music.timeSamples>sample,
                "Destination music advances real PCM samples after its load buffers settle",expected+" samples="+sample+"→"+(music==null?-1:music.timeSamples)+" "+MusicState(music));
        }
        static string MusicState(AudioSource source)
        {
            if(source==null)return "source=none playing=false volume=n/a spatial=n/a pitch=n/a loadType=n/a";
            return "source="+source.name+" playing="+source.isPlaying+" volume="+source.volume+" spatial="+source.spatialBlend+
                " pitch="+source.pitch+" loadType="+(source.clip==null?"none":source.clip.loadType.ToString())+
                " clip="+(source.clip==null?"none":source.clip.name)+" loadState="+(source.clip==null?"none":source.clip.loadState.ToString())+
                " enabled="+source.isActiveAndEnabled;
        }
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;Application.logMessageReceived-=Log;}
    }
}
