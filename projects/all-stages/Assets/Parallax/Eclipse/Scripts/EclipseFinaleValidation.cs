using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Opt-in exported-player validation: -eclipse-test -eclipse-report <directory>.
    // Uses the same ray picking, scaling, shadow, controller, and doorway code as play.
    public sealed class EclipseFinaleValidation : MonoBehaviour
    {
        public EclipseFinaleGame game;
        [Serializable] public sealed class Check { public string name; public bool passed; public string detail; }
        [Serializable] public sealed class Report
        {
            public string version="Eclipse 0.3.1",validation="Exported Unity player / real renderer and physics";
            public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();
            public float finalMoonDiameter,placedSunCoverage,finalExitScale;
            public int totalChecks,failures;public string finalPhase;
        }
        readonly Report report=new Report();
        string output;
        float watchdog;
        bool finished;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-eclipse-report");
            output=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Environment.CurrentDirectory,"Validation");
            output=Path.GetFullPath(output);
            Directory.CreateDirectory(output);Application.logMessageReceived+=OnLog;
            StartCoroutine(Run());
        }
        void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)report.errors.Add(message+"\n"+stack);}
        void Update(){watchdog+=Time.unscaledDeltaTime;if(!finished&&watchdog>150){report.errors.Add("Validation timed out");Finish();}}
        void Assert(string name,bool condition,string detail="")
        {report.checks.Add(new Check{name=name,passed=condition,detail=detail});Debug.Log("ECLIPSE "+(condition?"PASS ":"FAIL ")+name+" "+detail);}
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));
            yield return null;yield return new WaitForEndOfFrame();
        }
        IEnumerator Frames(int count,Vector2 move)
        {
            for(int i=0;i<count;i++){game.Tick(1f/60,move,false);yield return null;}
        }
        IEnumerator WalkTo(Vector3 point,int max=900)
        {
            for(int i=0;i<max;i++)
            {
                Vector3 delta=point-game.body.transform.position;delta.y=0;
                if(delta.magnitude<.14f)break;
                game.AimAt(game.view.transform.position+delta.normalized*20);
                game.Tick(1f/60,Vector2.up,false);yield return null;
            }
        }
        IEnumerator Run()
        {
            yield return null;yield return Capture("00-title");game.Play();
            yield return null;yield return null;
            Assert("Game clock runs after starting",Time.timeScale==1&&Time.deltaTime>0,"dt="+Time.deltaTime);
            yield return Capture("01-start");
            Assert("Only the moon is pickable before totality",!game.totality&&game.held==FinaleItem.None);
            game.AimAt(game.exitSign.TransformPoint(new Vector3(0,.5f,0)));game.UpdateAim();
            Assert("Exit cannot be picked before darkness",game.aimed!=FinaleItem.Exit);
            Assert("Starting point is safe",game.Safe);
            Assert("Small moon waits off-centre on the left",game.moon.position.x < -2 && game.moon.localScale.x < .6f);
            game.AimAt(new Vector3(0,EclipseFinaleGame.EyeHeight,60));
            yield return Frames(25,Vector2.right); // Walk around the small moon on the floor.
            yield return Frames(190,Vector2.up);
            Assert("Unprotected sunlight returns the player",game.respawns>0,"respawns="+game.respawns);
            yield return MoonDropExperiments();
            game.ResetGame();
            yield return WalkTo(new Vector3(game.moon.position.x,0,game.moon.position.z-1.6f));
            game.AimAt(game.moon.position);game.Interact();
            Assert("Moon ray-pick works",game.held==FinaleItem.Moon);
            var original=game.moon.rotation;var initialPosition=game.moon.position;var initialScale=game.moon.localScale;
            game.AimAt(new Vector3(5,16,100));yield return Frames(25,Vector2.zero);
            Assert("Camera movement does not tilt the held moon",Quaternion.Angle(original,game.moon.rotation)<.05f);
            game.Cancel();
            Assert("Cancel restores the original moon",Vector3.Distance(game.moon.position,initialPosition)<.001f&&Vector3.Distance(game.moon.localScale,initialScale)<.001f);
            game.AimAt(game.moon.position);game.Interact();game.AimAt(EclipseFinaleGame.SunPosition);
            yield return Frames(90,Vector2.zero);yield return Capture("02-moon-preview");
            Assert("Perspective enlarges the little moon",game.moon.localScale.x>20,"diameter="+game.moon.localScale.x);
            Assert("A comfortable close pickup visibly covers the sun",game.MoonPreviewCoverage>.975f,"visible coverage="+game.MoonPreviewCoverage);
            Assert("A held moon never activates the safe eclipse",!game.totality);
            Vector3 placedMoon=game.moon.position;float placedScale=game.moon.localScale.x;
            Assert("Moon can be placed in the sky",game.Drop());
            Assert("Sun placement anchors without an artificial scale jump",game.MoonAnchored&&!game.MoonFalling&&Mathf.Abs(game.moon.localScale.x-placedScale)<.001f);
            yield return Frames(110,Vector2.zero);
            Assert("The anchored moon stays at the chosen sky position",Vector3.Distance(game.moon.position,placedMoon)<.001f);
            report.finalMoonDiameter=game.moon.localScale.x;report.placedSunCoverage=game.coverage;
            Assert("Placed moon covers the sun across the room",game.totality,"coverage="+game.coverage);
            Assert("Full eclipse makes the path safe",game.IsShadowed(new Vector3(0,.5f,30))&&game.IsShadowed(new Vector3(0,.5f,52)));
            game.AimAt(EclipseFinaleGame.SunPosition);yield return Capture("03-total-eclipse");
            int before=game.respawns;
            yield return WalkTo(new Vector3(0,0,49));
            Assert("Player can physically cross the room",game.body.transform.position.z>48&&game.respawns==before,"z="+game.body.transform.position.z);
            game.AimAt(game.exitSign.TransformPoint(new Vector3(.3f,.5f,0)));yield return Capture("04-exit-clue");
            Assert("Small i opening does not fit the player",!game.DoorFits);
            game.UpdateAim();Assert("Exit is now pickable",game.aimed==FinaleItem.Exit);
            game.Interact();Assert("Exit ray-pick works",game.held==FinaleItem.Exit);
            Vector3 savedSignPosition=game.exitSign.position,savedSignScale=game.exitSign.localScale;
            game.AimAt(game.view.transform.position+new Vector3(0,18,35));yield return Frames(70,Vector2.zero);
            Assert("Exit can be lifted freely using perspective",game.exitSign.position.y>3&&game.ExitScale>savedSignScale.x);
            game.Cancel();
            Assert("Cancel restores the lifted exit",Vector3.Distance(game.exitSign.position,savedSignPosition)<.001f);
            game.AimAt(game.exitSign.TransformPoint(new Vector3(.3f,.5f,0)));game.Interact();
            // Back away while holding the sign, then aim at the distant deck.
            game.AimAt(new Vector3(0,1,66));yield return Frames(95,Vector2.down);
            game.AimAt(new Vector3(0,0,65));yield return Frames(100,Vector2.zero);
            Assert("Perspective scales the real i opening",game.DoorFits,"scale="+game.ExitScale);
            // Release the enlarged exit in the air, then use the same door after it lands.
            game.AimAt(game.view.transform.position+new Vector3(0,10,30));yield return Frames(90,Vector2.zero);
            float exitReleaseY=game.exitSign.position.y,exitReleaseScale=game.ExitScale;
            Assert("Exit can be released in the air",game.Drop()&&game.ExitFalling&&exitReleaseY>3);
            yield return Capture("11-exit-released-in-air");yield return Frames(45,Vector2.zero);
            float exitDrop=exitReleaseY-game.exitSign.position.y;
            Assert("Exit descent is quicker while remaining controlled",exitDrop>1.4f&&exitDrop<2.3f,"drop over 0.75s="+exitDrop);
            Assert("Falling exit keeps its perspective size and upright orientation",Mathf.Abs(game.ExitScale-exitReleaseScale)<.001f&&Quaternion.Angle(game.exitSign.rotation,Quaternion.identity)<.01f);
            Vector3 fallingExit=game.exitSign.position;game.Pause();yield return Frames(30,Vector2.zero);
            Assert("Pause freezes the falling exit",Vector3.Distance(fallingExit,game.exitSign.position)<.001f);game.Play();
            game.AimAt(game.exitSign.TransformPoint(new Vector3(0,.5f,0)));game.Interact();
            Assert("Falling exit is picked before the moon behind it",game.held==FinaleItem.Exit&&!game.ExitFalling);
            game.AimAt(new Vector3(0,0,65));yield return Frames(20,Vector2.zero);game.Cancel();
            Assert("Cancel restores the exit falling state",game.ExitFalling&&Vector3.Distance(fallingExit,game.exitSign.position)<.001f);
            for(int i=0;i<1800&&game.ExitFalling;i++){game.Tick(1f/60,Vector2.zero,false);yield return null;}
            Assert("Exit lands flush on the floor without shrinking",!game.ExitFalling&&Mathf.Abs(game.exitSign.position.y)<.01f&&Mathf.Abs(game.ExitScale-exitReleaseScale)<.001f);
            Assert("The anchored moon and eclipse stay stable during exit handling",game.MoonAnchored&&game.totality);
            report.finalExitScale=game.ExitScale;
            game.AimAt(game.exitSign.TransformPoint(new Vector3(DoorX,.45f,0)));
            yield return Capture("05-enlarged-exit");
            // Collision probes: the letter/body blocks movement; the i stem is an actual hole.
            float scale=game.ExitScale;Vector3 wallPoint=game.exitSign.TransformPoint(new Vector3(-.6f,.4f,-.4f));
            Assert("Sign body remains solid",Physics.Raycast(wallPoint,Vector3.forward,scale));
            Vector3 holePoint=game.exitSign.TransformPoint(new Vector3(DoorX,.4f,-.4f));
            Assert("Lowercase i stem is physically open",!Physics.Raycast(holePoint,Vector3.forward,scale*.7f));
            Vector3 approach=game.exitSign.TransformPoint(new Vector3(DoorX,0,-.65f));
            yield return WalkTo(approach);
            game.AimAt(game.exitSign.TransformPoint(new Vector3(DoorX,.45f,2)));
            for(int i=0;i<240&&game.phase==FinalePhase.Chamber;i++){game.Tick(1f/60,Vector2.up,false);yield return null;}
            Assert("Walking through i enters the black corridor",game.phase==FinalePhase.Corridor,"phase="+game.phase+" feet="+game.body.transform.position);
            yield return Capture("06-black-corridor");
            if(game.phase==FinalePhase.Corridor)
            {
                game.AimAt(new Vector3(0,-68.35f,40));
                for(int i=0;i<500&&game.phase==FinalePhase.Corridor;i++){game.Tick(1f/60,Vector2.up,false);yield return null;}
            }
            Assert("Walking out of the corridor starts the space drift",game.phase==FinalePhase.Drifting);
            if(game.phase==FinalePhase.Drifting)yield return Frames(800,Vector2.zero);
            Assert("Space drift reaches THE END",game.phase==FinalePhase.Complete);
            yield return Capture("07-the-end");Finish();
        }

        IEnumerator MoonDropExperiments()
        {
            // Discovering the rule: empty sky cannot hold the moon up.
            game.ResetGame();game.AimAt(game.moon.position);game.Interact();
            game.AimAt(new Vector3(0,60,6));yield return Frames(75,Vector2.zero);
            float releaseY=game.moon.position.y;
            Assert("Off-sun sky placement releases the moon",game.Drop()&&game.MoonFalling&&!game.MoonAnchored);
            yield return Capture("08-moon-released-off-sun");yield return Frames(45,Vector2.zero);
            float moonDrop=releaseY-game.moon.position.y;
            Assert("Moon uses the same quicker controlled descent",moonDrop>1.4f&&moonDrop<2.3f&&!game.totality,"drop over 0.75s="+moonDrop);
            Vector3 fallingPosition=game.moon.position;
            game.Pause();yield return Frames(30,Vector2.zero);
            Assert("Pause stops the falling moon",Vector3.Distance(fallingPosition,game.moon.position)<.001f);game.Play();
            game.AimAt(game.moon.position);game.Interact();
            Assert("The falling moon can be picked back up",game.held==FinaleItem.Moon&&!game.MoonFalling);
            game.AimAt(new Vector3(20,30,80));yield return Frames(15,Vector2.zero);game.Cancel();
            Assert("Cancel restores the falling position and motion",game.MoonFalling&&Vector3.Distance(fallingPosition,game.moon.position)<.001f);
            for(int i=0;i<2400&&game.MoonFalling;i++){game.Tick(1f/60,Vector2.zero,false);yield return null;}
            yield return Capture("09-moon-landed-on-deck");
            Assert("Falling moon lands on the solid room floor",!game.MoonFalling&&!game.MoonAnchored&&game.moon.localScale.x>1&&game.moon.position.y>=game.MoonRadius-.02f);

            // Missing the deck must never lose the sole puzzle object permanently.
            game.ResetGame();game.AimAt(game.moon.position);game.Interact();
            game.AimAt(new Vector3(45,65,130));yield return Frames(75,Vector2.zero);
            Assert("Off-map moon also falls",game.Drop()&&game.MoonFalling);
            for(int i=0;i<2400&&game.MoonFalling;i++){game.Tick(1f/60,Vector2.zero,false);yield return null;}
            Assert("A lost moon returns small to its left starting position",!game.MoonFalling&&!game.MoonAnchored&&game.moon.localScale.x<.6f&&game.moon.position.x< -2);

            // A distant pickup remains visibly small: no automatic enlargement at the sun.
            game.AimAt(game.moon.position);game.Interact();game.AimAt(EclipseFinaleGame.SunPosition);
            yield return Frames(75,Vector2.zero);
            float smallCoverage=game.MoonPreviewCoverage;
            Assert("Distant pickup keeps its smaller apparent size",smallCoverage>.1f&&smallCoverage<.8f,"visible coverage="+smallCoverage);
            Vector3 smallPosition=game.moon.position;float smallScale=game.moon.localScale.x;
            Assert("A small aligned moon anchors at the sun",game.Drop()&&game.MoonAnchored);
            yield return Frames(80,Vector2.zero);yield return Capture("10-small-moon-at-sun");
            Assert("Partial cover stays suspended but does not unlock the room",!game.totality&&Vector3.Distance(game.moon.position,smallPosition)<.001f&&Mathf.Abs(game.moon.localScale.x-smallScale)<.001f);
            Assert("Remaining sunlight does not create a false safe path",!game.IsShadowed(new Vector3(0,.5f,30)));
            game.AimAt(game.moon.position);game.Interact();game.AimAt(new Vector3(0,60,6));yield return Frames(25,Vector2.zero);game.Cancel();
            yield return Frames(30,Vector2.zero);
            Assert("Cancel restores an anchored moon",game.MoonAnchored&&!game.MoonFalling&&Vector3.Distance(game.moon.position,smallPosition)<.001f);
            game.AimAt(game.moon.position);game.Interact();game.AimAt(new Vector3(-2.6f,0,3.4f));
            yield return Frames(90,Vector2.zero);game.Drop();yield return Frames(60,Vector2.zero);
            Assert("A too-small sky moon can be brought back down and re-grabbed",!game.MoonAnchored&&!game.MoonFalling&&game.moon.localScale.x<1.1f&&game.moon.position.z<5);
        }
        const float DoorX=EclipseFinaleWorld.DoorX;
        void Finish()
        {
            if(finished)return;finished=true;report.totalChecks=report.checks.Count;report.failures=report.errors.Count;
            foreach(var check in report.checks)if(!check.passed)report.failures++;
            report.finalPhase=game.phase.ToString();
            File.WriteAllText(Path.Combine(output,"results.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=OnLog;Debug.Log("ECLIPSE_RESULT "+report.totalChecks+" checks / "+report.failures+" failures");
            Application.Quit(report.failures==0?0:1);
        }
    }
}
