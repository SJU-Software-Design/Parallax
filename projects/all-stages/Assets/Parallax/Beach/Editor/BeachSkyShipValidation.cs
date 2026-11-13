using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShadeLink.Beach;

// Native final-gangway boarding and flight; earlier shade puzzles are outside this fixture.
[InitializeOnLoad]
public static class BeachSkyShipValidation
{
    const string Pending="Parallax.SkyShipValidation";
    const string Output="/tmp/parallax-sky-ship-checks";
    const float Dt=1f/60;
    static int frames;
    static BeachGame game;
    static BeachSkyShip ship;
    static BeachSkyShipValidation(){EditorApplication.update+=Update;}
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Parallax/Beach/Scenes/Beach.unity");
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    static void Update()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||++frames<20)return;
        SessionState.SetBool(Pending,false);
        try
        {
            game=UnityEngine.Object.FindFirstObjectByType<BeachGame>();ship=game.skyShip;
            Require(!game.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("테스트 차양")||t.name.Contains("전체 그늘 차양")),"Temporary ceiling remains");
            Require(ship!=null&&game.castleRoute.LowerFragment!=null&&game.castleRoute.UpperFragment!=null,"Castle or ship references missing");
            Require(ship.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.isReadable),"Native ship shadow meshes are not readable");
            Require(!game.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("Tenkoku")||t.name.Contains("True Clouds")),"Cloud sky remains in the scene");
            var sun=game.GetComponentsInChildren<Light>().Single(l=>l.type==LightType.Directional);
            Require(Vector3.Angle(-sun.transform.forward,BeachLighting.ToSun)<.01f,"Original beach sun direction changed");
            float originalFog=RenderSettings.fogDensity,originalFar=game.view.farClipPlane;var originalOcean=game.transform.Find("수평선").localScale;
            game.automated=true;game.StartCastleDemo();game.Resume();Physics.simulationMode=SimulationMode.Script;
            for(int i=0;i<6;i++)Step(Vector2.zero);
            game.castleRoute.FirstRestored=game.castleRoute.SecondRestored=true;
            game.PlacePlayer(BeachCastleRestoration.Summit);for(int i=0;i<12;i++)Step(Vector2.zero);
            Require(!game.won,"Restored castle summit ended play before boarding");
            game.StartCastleDemo();game.Resume();for(int i=0;i<6;i++)Step(Vector2.zero);
            Require(!ship.TryLaunch(),"Shore-side launch succeeded");
            Capture("Beach-sky",new Vector3(0,3,0),new Vector3(0,38,110));
            Capture("Castle-approach",new Vector3(17,11,78),new Vector3(0,4,106));
            Require(!game.FootSafe(new Vector3(9.3f,.025f,110)),"Removed ceiling still protects the unshaded harbor approach");
            Capture("Castle-front",new Vector3(0,1.575f,80.5f),new Vector3(0,1.575f,80.5f)+ShadeLink.IndoorRules.Look(0,.12f));
            var parasol=game.castleRoute.Parasol;
            parasol.transform.SetPositionAndRotation(new Vector3(7,2.2f,112.5f),Quaternion.identity);parasol.transform.localScale=Vector3.one;
            Physics.SyncTransforms();
            Require(game.ValidPlacement(parasol,out var placementReason),"Player shade cannot be placed at the harbor: "+placementReason+" bounds="+parasol.WorldBounds+" zone="+parasol.zone);parasol.Restore();
            // Approach shade is a player puzzle. This flight fixture begins on the final gangway.
            game.PlacePlayer(new Vector3(.4f,3.06f,125));for(int i=0;i<4;i++)Step(Vector2.zero);
            Capture("Before-boarding",game.view.transform.position,new Vector3(.4f,5,129));
            Require(!ship.TryLaunch(),"Dock-side launch succeeded");
            Travel(new Vector3(.4f,3.06f,126.8f));
            Require(ship.IsAboard,"Native gangway did not reach the real deck");
            var aboard=game.player.feet;var home=ship.transform.position;var homeScale=ship.transform.localScale;
            Require(ship.TryLaunch(),"Actual deck boarding could not launch");
            for(int i=0;i<Mathf.RoundToInt(17/Dt);i++)Step(Vector2.zero);
            var paused=ship.transform.position;var pausedView=game.view.transform.position;game.Pause();game.Tick(1,Vector2.zero);
            Require(ship.transform.position==paused&&game.view.transform.position==pausedView,"Pause advanced the ship or detached camera");game.Resume();
            game.ResetZone();
            Require(!ship.Flying&&game.controller.enabled&&Vector3.Distance(home,ship.transform.position)<.001f&&ship.transform.localScale==homeScale,"Mid-flight reset did not restore ship and controller");
            Require(Vector3.Distance(game.view.transform.position,game.controller.transform.position+Vector3.up*1.55f)<.001f&&RenderSettings.fogDensity==originalFog&&game.view.farClipPlane==originalFar&&game.transform.Find("수평선").localScale==originalOcean,"Detached camera, fog or horizon survived reset");
            // The native boarding above is already proved. Reuse that grounded pose
            // only to exercise a second takeoff after resetting the first flight.
            game.PlacePlayer(aboard);for(int i=0;i<6;i++)Step(Vector2.zero);
            Require(ship.TryLaunch(),"Second flight could not start after reset");
            Vector3 rider=ship.transform.InverseTransformPoint(game.player.feet);
            var center=new Vector3(0,0,52);float orbit=0,previousBearing=0,previousHeight=ship.transform.position.y;
            float maxTurn=0,maxCameraStep=0;
            bool halfTurnChecked=false,fadePaused=false,launchSpeedChecked=false;float previousFade=0;Vector3 straightDirection=Vector3.zero;
            for(int i=0;i<Mathf.CeilToInt(BeachSkyShip.FlightDuration/Dt)+10&&!game.won;i++)
            {
                Vector3 previous=ship.transform.position;var previousRotation=ship.transform.rotation;
                var previousCamera=game.view.transform.position;Step(Vector2.zero);
                maxCameraStep=Mathf.Max(maxCameraStep,Vector3.Distance(previousCamera,game.view.transform.position));
                var radial=ship.transform.position-center;radial.y=0;
                float bearing=Mathf.Atan2(radial.x,radial.z)*Mathf.Rad2Deg;
                orbit+=Mathf.DeltaAngle(previousBearing,bearing);previousBearing=bearing;
                maxTurn=Mathf.Max(maxTurn,Quaternion.Angle(previousRotation,ship.transform.rotation));
                if(ship.FlightTime>1.2f)
                {
                    Require(ship.transform.position.y>=previousHeight-.001f,"Spiral flight lost altitude");
                    float phase=ship.FlightTime/BeachSkyShip.FlightDuration;
                    if(phase>.22f&&phase<.4f)
                    {
                        Require(Vector3.Dot(ship.transform.right,-radial.normalized)<-.85f,"Beach is not beside the left rail");
                        Require(ship.transform.right.y>.04f,"Ship does not bank inward");
                    }
                }
                if(!halfTurnChecked&&ship.FlightTime>=11.6f)
                {
                    Require(orbit<-179&&orbit>-181,"Ship did not leave after half a turn: "+orbit);halfTurnChecked=true;
                }
                if(ship.FlightTime>13)
                {
                    var direction=ship.transform.position-previous;direction.y=0;direction.Normalize();
                    if(straightDirection==Vector3.zero)straightDirection=direction;
                    Require(Vector3.Angle(straightDirection,direction)<.2f,"Departure kept circling instead of flying straight");
                }
                if(ship.FlightTime>16)
                    Require(Vector3.Distance(game.view.transform.position,game.controller.transform.position)>30,"Camera remained aboard during departure");
                if(ship.FlightTime>17&&ship.FlightTime<19)
                {
                    foreach(var point in new[]{ship.transform.position+Vector3.up*5,new Vector3(0,0,5),new Vector3(0,0,105)})
                    {
                        var screen=game.view.WorldToViewportPoint(point);
                        Require(screen.z>0&&screen.x>.03f&&screen.x<.97f&&screen.y>.03f&&screen.y<.95f,"Wide departure shot lost the ship or beach: "+screen);
                    }
                }
                Require(ship.EndingFade>=previousFade&&ship.EndingFade>=0&&ship.EndingFade<=1,"Ending fade is not monotonic");
                previousFade=ship.EndingFade;
                if(!fadePaused&&ship.EndingFade>.2f)
                {
                    var frozenCamera=game.view.transform.position;game.Pause();game.Tick(1,Vector2.zero);
                    Require(ship.EndingFade==previousFade&&game.view.transform.position==frozenCamera,"Pause advanced the ending fade");game.Resume();fadePaused=true;
                }
                if(!launchSpeedChecked&&ship.FlightTime>1.1f)
                {
                    float speed=Vector3.Distance(previous,ship.transform.position)/Dt;
                    Require(speed<.4f,"Ship jumped to cruising speed at takeoff: "+speed+" m/s");launchSpeedChecked=true;
                }
                previousHeight=ship.transform.position.y;
                Require(Vector3.Distance(previous,ship.transform.position)<.7f,"Flight position jumped");
                Require(Vector3.Distance(rider,ship.transform.InverseTransformPoint(game.player.feet))<.003f,"Rider drifted from the deck");
                Require(!float.IsNaN(game.view.transform.rotation.x),"Camera rotation became invalid");
                if(i==Mathf.RoundToInt(BeachSkyShip.FlightDuration/Dt*.3f))
                {
                    Capture("In-flight",ship.transform.position+new Vector3(19,12,-26),ship.transform.position+Vector3.up*4);
                    Capture("Rider-sky",game.view.transform.position,game.view.transform.position+game.view.transform.forward);
                }
                if(i==Mathf.RoundToInt(BeachSkyShip.FlightDuration/Dt*.35f))
                {
                    float yaw=game.yaw,pitch=game.pitch;var before=game.view.transform.rotation;
                    game.yaw-=Mathf.PI*.5f;game.pitch=-.2f;game.SyncView();
                    Require(Quaternion.Angle(before,game.view.transform.rotation)>60,"Free look did not turn independently while flying");
                    Capture("Orbit-left",game.view.transform.position,game.view.transform.position+game.view.transform.forward);
                    game.yaw=yaw;game.pitch=pitch;game.SyncView();
                }
                if(i==Mathf.RoundToInt(BeachSkyShip.FlightDuration/Dt*.85f))Capture("Late-flight",game.view.transform.position,game.view.transform.position+game.view.transform.forward);
            }
            Require(halfTurnChecked&&straightDirection!=Vector3.zero,"Half-turn departure did not run");
            Require(maxCameraStep<3,"Detached camera jumped instead of pulling back smoothly: "+maxCameraStep);
            Require(maxTurn<3,"Flight heading or bank snapped: "+maxTurn);
            Require(game.won&&!game.active&&ship.transform.position.y>90,"Flying ending did not finish in the sky");
            Debug.Log("SKY_DEPARTURE_METRICS maxTurnPerFrame="+maxTurn+" maxCameraStep="+maxCameraStep);
            game.SyncView();Capture("Departure-wide",game.view.transform.position,game.view.transform.position+game.view.transform.forward);
            Require(ship.EndingFade==1&&ship.EndingTitleAlpha==1,"Ending did not reach solid black and full title opacity");
            game.ResetAll();Require(!game.won&&!ship.Flying&&game.controller.enabled&&ship.EndingFade==0&&ship.EndingTitleAlpha==0,"Finished ending could not reset");
            File.WriteAllText(Output+"/result.txt","PASS: no temporary ceiling or cloud renderer; unshaded approach remains hazardous; harbor parasol placement accepted; declared final-gangway fixture, actual deck boarding, rejected shore launch, half-turn, straight departure, detached camera framing, fade/title and their reset, inward bank, free look, continuous flight, rider lock, pause, mid-flight reset, sky ending and full reset.\n");
            Debug.Log("BEACH_SKY_SHIP_VALIDATION_OK");EditorApplication.Exit(0);
        }
        catch(Exception error)
        {
            if(game!=null)try{Capture("Failure",game.view.transform.position,game.view.transform.position+game.view.transform.forward);}catch{}
            File.WriteAllText(Output+"/result.txt",error.ToString());Debug.LogException(error);EditorApplication.Exit(1);
        }
    }
    static void Step(Vector2 move)
    {game.Tick(Dt,move);Physics.Simulate(Dt);Physics.SyncTransforms();}
    static void Travel(Vector3 target)
    {
        for(int i=0;i<950;i++)
        {
            var delta=new Vector2(target.x-game.player.feet.x,target.z-game.player.feet.z);
            if(delta.magnitude<.12f&&Mathf.Abs(target.y-game.player.feet.y)<.22f&&game.player.grounded)return;
            if(delta.sqrMagnitude>.0001f)game.yaw=Mathf.Atan2(delta.x,delta.y);
            Step(delta.normalized*Mathf.Sqrt(Mathf.Clamp01(delta.magnitude/.6f)));
            Require(game.respawns==0,"Native path caused a respawn at "+game.player.feet);
        }
        throw new InvalidOperationException("Native path blocked: target="+target+" actual="+game.player.feet);
    }
    static void Capture(string name,Vector3 at,Vector3 target)
    {
        var camera=game.view;var position=camera.transform.position;var rotation=camera.transform.rotation;var oldTarget=camera.targetTexture;
        var texture=new RenderTexture(1600,900,24);var previous=RenderTexture.active;Texture2D pixels=null;
        try
        {
            camera.transform.SetPositionAndRotation(at,Quaternion.LookRotation(target-at));camera.targetTexture=texture;
            camera.Render();RenderTexture.active=texture;
            pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
            File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=oldTarget;camera.transform.SetPositionAndRotation(position,rotation);
            texture.Release();UnityEngine.Object.DestroyImmediate(texture);if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
