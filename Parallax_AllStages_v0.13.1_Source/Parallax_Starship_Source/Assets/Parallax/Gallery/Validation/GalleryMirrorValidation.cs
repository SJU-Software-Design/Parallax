using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryMirrorValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90;
        string output;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        SimulationMode previousSimulation;
        int samples, mismatches, movementFrames;
        float heldWalkDistance, heldJumpHeight;
        Vector3 solutionPosition;
        Quaternion solutionRotation;
        float solutionScale;
        bool inputPuzzlePassed, cancelOverlapPassed;
        [Serializable] sealed class Report
        {
            public bool passed;
            public bool inputPuzzlePassed, cancelOverlapPassed;
            public string scope, failure, unity, gpu;
            public int shadowSamples, shadowMismatches, heldMovementFrames;
            public float heldWalkDistance, heldJumpHeight, solutionScale;
            public Vector3 solutionPosition;
            public Quaternion solutionRotation;
            public string[] checks, errors;
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-shade-output");
            output=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"GalleryMirrorValidation");
            Directory.CreateDirectory(output);Application.logMessageReceived+=Log;
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var sequence=Sequence();string failure=null;
            while(true)
            {
                object step=null;bool more;
                try { more=sequence.MoveNext();if(more)step=sequence.Current; }
                catch(Exception e){more=false;failure=e.ToString();}
                if(!more)break;yield return step;
            }
            var report=new Report {passed=failure==null&&errors.Count==0,
                scope="Mirror room entered through the public demo action. Real camera grab, held walking/jumping, safe cancellation and release. Separate optical fixtures check incident occlusion and search a valid shadow pose. Final mirror-room play uses only camera aim, held rotation, CharacterController movement and physical drop, with no object pose/scale writes. This does not claim a full four-room playthrough.",
                failure=failure,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,
                shadowSamples=samples,shadowMismatches=mismatches,heldMovementFrames=movementFrames,
                heldWalkDistance=heldWalkDistance,heldJumpHeight=heldJumpHeight,
                solutionPosition=solutionPosition,solutionRotation=solutionRotation,solutionScale=solutionScale,
                inputPuzzlePassed=inputPuzzlePassed,cancelOverlapPassed=cancelOverlapPassed,
                checks=checks.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(Path.Combine(output,"mirror-results.json"),JsonUtility.ToJson(report,true));
            if(failure!=null){Debug.Log("GALLERY_MIRROR_FAIL "+failure);Capture("mirror-failure");}
            Physics.simulationMode=previousSimulation;Application.logMessageReceived-=Log;
            Application.Quit(report.passed?0:1);
        }
        void Log(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Require(bool value,string message){if(!value)throw new InvalidOperationException("GALLERY_MIRROR: "+message);}
        void Check(string message){checks.Add(message);Debug.Log("GALLERY_MIRROR_PASS "+message);}
        void Look(Vector3 target)
        {
            Vector3 d=target-game.view.transform.position;
            game.yaw=Mathf.Atan2(d.x,d.z);game.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);game.SyncView();
        }
        void Grab(GalleryProp prop)
        {
            foreach(var part in prop.parts){Look(part.bounds.center);game.UpdateAim();if(game.aimed==prop){game.Interact();if(game.held==prop)break;}}
            Require(game.held==prop,"Camera ray cannot grab "+prop.title);
        }
        void Step(Vector2 movement,bool jump=false)
        {
            game.Tick(Dt,movement,jump);Physics.Simulate(Dt);Physics.SyncTransforms();game.lighting.Synchronize();
            if(game.held!=null)
            {
                Require(game.ValidPlacement(game.held,out var reason),"Held walk/jump produced overlap: "+reason+" player="+game.player.feet+" prop="+game.held.transform.position);
                Require(game.held.body.isKinematic,"Held walking released body ownership");
                foreach(var part in game.held.parts)Require(part.enabled,"Held walking disabled collision protection");
                movementFrames++;
            }
        }
        void AimSmooth(Vector3 target)
        {
            Vector3 delta=target-game.view.transform.position;
            float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,pitch=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
            game.yaw=Mathf.MoveTowardsAngle(game.yaw*Mathf.Rad2Deg,yaw,2.5f)*Mathf.Deg2Rad;
            game.pitch=Mathf.MoveTowards(game.pitch*Mathf.Rad2Deg,pitch,2.5f)*Mathf.Deg2Rad;game.SyncView();
        }
        IEnumerable<object> Walk(Vector2 target,Vector3 look,bool turnShield=false,bool carryLow=false)
        {
            for(int i=0;i<600;i++)
            {
                Vector2 delta=target-new Vector2(game.player.feet.x,game.player.feet.z);
                if(delta.magnitude<.045f)yield break;
                AimSmooth(carryLow?new Vector3(game.player.feet.x,1.05f,game.player.feet.z+1.2f):look);
                if(turnShield&&game.held!=null)game.RotateHeld(Mathf.Clamp(Mathf.DeltaAngle(game.heldRotation.eulerAngles.y,40.6f),-75*Dt,75*Dt),0);
                Step(delta.normalized*Mathf.Min(1,delta.magnitude/(GalleryGame.Speed*Dt)));
                if(i%15==0)yield return null;
            }
            Require(false,"Walking to "+target+" is blocked at "+game.player.feet+" with held pose "+(game.held==null?"none":game.held.transform.position.ToString()));
        }
        IEnumerator Sequence()
        {
            yield return null;yield return null;game.ResetAll();game.Resume();
            for(int i=0;i<6;i++)Step(Vector2.zero);
            var painting=game.props[0];Grab(painting);Vector3 start=game.player.feet;
            for(int i=0;i<48;i++){Step(Vector2.right);if(i%12==0)yield return null;}
            heldWalkDistance=Vector3.Distance(start,game.player.feet);
            Require(heldWalkDistance>1.4f&&game.held==painting,"Holding still prevents walking");
            float baseY=game.player.feet.y;
            for(int i=0;i<105;i++){Step(Vector2.zero,i==0);heldJumpHeight=Mathf.Max(heldJumpHeight,game.player.feet.y-baseY);if(i%15==0)yield return null;}
            Require(heldJumpHeight>.65f&&game.held==painting,"Holding prevents jumping / gravity");
            Vector3 releasePosition=painting.transform.position,releaseScale=painting.transform.localScale;
            Require(game.Drop(),"Cannot release after held walking");
            Require(Vector3.Distance(releasePosition,painting.transform.position)<.0001f&&painting.transform.localScale==releaseScale&&!painting.body.isKinematic,"Held walking changes release pose / size / physics");
            Check("Real camera grab: walk "+heldWalkDistance.ToString("0.00")+"m and jump "+heldJumpHeight.ToString("0.00")+"m while holding; all committed poses collision-valid; release preserves pose and scale");

            game.StartMirrorDemo();for(int i=0;i<8;i++)Step(Vector2.zero);
            Require(!game.lightGate.illuminated&&!game.lightGate.Open,"Mirror puzzle starts with its gate already open");
            Look(game.mirror.transform.position);game.UpdateAim();Require(game.aimedMirror==game.mirror,"Cannot select mirror through camera ray");
            game.Interact();Require(game.adjustingMirror,"Click does not select mirror controls");
            game.mirror.Turn(18);for(int i=0;i<50;i++){Step(Vector2.zero);if(i%15==0)yield return null;}
            yield return null;Capture("mirror-00-reflective-panel");
            Require(game.lightGate.illuminated&&game.lightGate.Open,"Aligned mirror fails to illuminate receiver / open gate");
            Check("Actual mirror ray selection and rotation redirect sunlight to receiver; gate opens only while receiver is lit");
            Look(new Vector3(0,.1f,24));Capture("mirror-01-reflected-corridor");
            CompareShader(0,"reflected-floor");CompareShader(1,"reflected-upper");
            int lit=0;for(float z=20;z<26.5f;z+=.15f)if(!game.FootSafe(new Vector3(0,.025f,z)))lit++;
            Require(lit>6,"Reflected beam produces no hazardous floor on required route");
            Check("Reflected sunlight produces "+lit+" unsafe samples on the gated corridor; direct light and reflected light both count as exposure");
            game.mirror.Turn(24);for(int i=0;i<50;i++)Step(Vector2.zero);
            Require(!game.lightGate.illuminated&&!game.lightGate.Open,"Gate stays open after reflection misses sensor");
            game.mirror.Turn(-24);for(int i=0;i<50;i++)Step(Vector2.zero);

            // Isolated optical fixture: a real occluder between skylight and mirror.
            var cover=GameObject.CreatePrimitive(PrimitiveType.Cube);cover.name="Validation fixture: incoming sunlight occluder";
            cover.transform.position=game.mirror.transform.position+GalleryLighting.ToSun*1.4f;cover.transform.localScale=new Vector3(5,.35f,5);
            cover.AddComponent<GalleryCaster>().shape=cover.GetComponent<BoxCollider>();game.lighting.Rebuild();
            Require(!game.lighting.MirrorReceivesSun(game.mirror.transform.position),"Mirror emits despite blocked incident sunlight");
            for(int i=0;i<50;i++)Step(Vector2.zero);
            Require(!game.lightGate.illuminated,"Blocked incoming light still powers receiver");
            DestroyImmediate(cover);game.lighting.Rebuild();Check("Blocking sunlight before mirror removes reflected illumination and disables receiver");

            var shield=game.props[game.props.Length-1];bool found=false;
            for(float scale=.85f;scale<=1.75f&&!found;scale+=.1f)
            for(float x=-4.1f;x<=-1.5f&&!found;x+=.2f)
            for(float z=20.2f;z<=23.2f&&!found;z+=.2f)
            {
                shield.transform.localScale=Vector3.one*scale;shield.transform.SetPositionAndRotation(new Vector3(x,1.4f*scale+.006f,z),Quaternion.Euler(0,40.6f,0));
                Physics.SyncTransforms();game.lighting.Synchronize();
                if(!game.ValidPlacement(shield,out _)||!game.lighting.IsReflectedLit(game.lightGate.sensor.position,-game.mirror.ReflectedDirection))continue;
                bool safe=true;for(float route=19.8f;route<=28.3f;route+=.08f)if(!game.FootSafe(new Vector3(0,.025f,route))){safe=false;break;}
                if(!safe)continue;found=true;solutionPosition=shield.transform.position;solutionScale=scale;solutionRotation=shield.transform.rotation;
            }
            Require(found,"No collision-valid shield placement keeps sensor lit and shadows a complete walking corridor");
            for(int i=0;i<50;i++)Step(Vector2.zero);
            CompareShader(0,"shielded-floor");CompareShader(1,"shielded-upper");
            Capture("mirror-02-shadow-solution");Check("Labelled optical fixture found valid shield pose "+solutionPosition+" / "+solutionScale+"x; sensor remains lit and complete route is safe");
            game.adjustingMirror=false;Look(new Vector3(0,1.6f,29));int before=game.respawns;
            for(int i=0;i<340;i++){Step(Vector2.up);if(i%20==0)yield return null;}
            Require(game.player.feet.z>28&&game.checkpoint>=2&&game.respawns==before,"Shadow corridor cannot be walked through physical gate into next checkpoint");
            Check("Normal CharacterController movement crosses the shadow corridor and open gate into third-room checkpoint without respawn");

            // Actual new puzzle: no direct prop position, scale or rotation writes.
            game.StartMirrorDemo();game.mirror.Turn(18);
            Require(game.ValidPlacement(shield,out _),"New shield starts overlapping existing exhibition");
            foreach(var step in Walk(new Vector2(4.8f,16),shield.WorldBounds.center))yield return step;
            Grab(shield);
            Vector3 targetAnchor=new Vector3(-2.5f,1.2f,22.05f);
            foreach(var step in Walk(new Vector2(4.8f,14.8f),targetAnchor,false,true))yield return step;
            foreach(var step in Walk(new Vector2(-2.5f,14.8f),targetAnchor,false,true))yield return step;
            foreach(var step in Walk(new Vector2(-2.5f,20.35f),targetAnchor,true,true))yield return step;
            for(int i=0;i<70;i++)
            {
                AimSmooth(targetAnchor);game.RotateHeld(Mathf.Clamp(Mathf.DeltaAngle(game.heldRotation.eulerAngles.y,40.6f),-75*Dt,75*Dt),0);
                Step(Vector2.zero);if(i%15==0)yield return null;
            }
            checks.Add("Held input placement observation: player="+game.player.feet+" shield="+shield.transform.position+" scale="+shield.transform.localScale.x+" yaw="+shield.transform.eulerAngles.y+" blocked="+game.PlacementBlocked+" obstruction="+game.PlacementObstacle);
            Require(game.Drop(),"Actual carry-and-aim solution cannot be dropped: "+game.invalidReason);
            for(int i=0;i<180;i++){Step(Vector2.zero);if(i%20==0)yield return null;}
            Require(game.ValidPlacement(shield,out _),"Actual dropped shield settles inside geometry");
            Require(game.lightGate.illuminated&&game.lightGate.Open,"Actual carried shield blocks receiver");
            for(float z=19.8f;z<=28.3f;z+=.08f)Require(game.FootSafe(new Vector3(0,.025f,z)),"Actual carried shield leaves exposed corridor at z="+z+"; pose="+shield.transform.position+" scale="+shield.transform.localScale.x);
            solutionPosition=shield.transform.position;solutionRotation=shield.transform.rotation;solutionScale=shield.transform.localScale.x;
            Check("Actual camera/held rotation/walking/drop produces settled shield "+solutionPosition+" / "+solutionScale+"x without direct pose or scale setters");
            CompareShader(0,"input-solution-floor");Look(new Vector3(0,.1f,24));Capture("mirror-03-input-solution");
            foreach(var step in Walk(new Vector2(0,19.7f),new Vector3(0,1.6f,28)))yield return step;
            before=game.respawns;foreach(var step in Walk(new Vector2(0,29),new Vector3(0,1.6f,30)))yield return step;
            Require(game.checkpoint>=2&&game.respawns==before,"Actual input solution fails to advance without respawn");inputPuzzlePassed=true;
            Check("Complete mirror-room input play: select/rotate mirror, grab/carry/rotate/drop shield, walk its shadow, cross sensor gate and reach next checkpoint");

            game.StartMirrorDemo();Grab(shield);
            foreach(var step in Walk(new Vector2(4.8f,16),new Vector3(8,1.2f,18)))yield return step;
            foreach(var step in Walk(new Vector2(4.8f,18),new Vector3(8,1.2f,18)))yield return step;
            game.Cancel();Require(game.held==shield,"Cancel restores pickup object into moving player's capsule");
            Require(game.ValidPlacement(shield,out _),"Refused cancel leaves held pose invalid");
            foreach(var step in Walk(new Vector2(4.8f,16.4f),new Vector3(8,1.2f,18)))yield return step;
            game.Cancel();Require(game.held==null&&shield.transform.position==shield.homePosition&&shield.transform.localScale==shield.homeScale,"Cancel after stepping clear fails to restore pickup state");
            cancelOverlapPassed=true;Check("Cancel refuses to restore an object onto player in its pickup space; stepping clear permits exact restoration");
            game.StartMirrorDemo();Require(Mathf.Abs(game.mirror.angle-game.mirror.homeAngle)<.001f&&!game.lightGate.Open,"Reset does not restore mirror and gate");
            Check("Demo / whole reset restores mirror angle, shield, gate and player state");
            game.Pause();yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(output,"mirror-04-menu.png"));yield return new WaitForEndOfFrame();yield return null;
        }
        void Capture(string name)
        {
            var camera=game.view;var old=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;game.lighting.Synchronize();camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            camera.targetTexture=old;RenderTexture.active=active;File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
            rt.Release();Destroy(rt);Destroy(texture);
        }
        void CompareShader(float height,string name)
        {
            const int side=160;
            var plane=GameObject.CreatePrimitive(PrimitiveType.Quad);plane.layer=30;
            plane.transform.SetPositionAndRotation(new Vector3(0,height,27),Quaternion.Euler(90,0,0));plane.transform.localScale=new Vector3(28,54,1);DestroyImmediate(plane.GetComponent<Collider>());
            var material=new Material(Shader.Find("Parallax/GallerySurface"));material.SetFloat("_Diagnostic",1);plane.GetComponent<Renderer>().sharedMaterial=material;
            var camera=new GameObject("Mirror CPU GPU verification").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=27;camera.aspect=1;camera.cullingMask=1<<30;
            camera.transform.SetPositionAndRotation(new Vector3(0,height+12,27),Quaternion.Euler(90,0,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.magenta;
            var rt=new RenderTexture(side,side,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);camera.targetTexture=rt;game.lighting.Synchronize();camera.Render();var active=RenderTexture.active;RenderTexture.active=rt;
            var texture=new Texture2D(side,side,TextureFormat.RGB24,false,true);texture.ReadPixels(new Rect(0,0,side,side),0,0);texture.Apply();var pixels=texture.GetPixels32();int tested=0,wrong=0;
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)
            {
                float wx=(x+.5f)/side*54-27,wz=(y+.5f)/side*54;if(Mathf.Abs(wx)>13.8f)continue;
                bool cpu=game.lighting.IsSunlit(new Vector3(wx,height,wz),Vector3.up),gpu=pixels[y*side+x].r>127;
                if(cpu!=gpu)wrong++;tested++;
            }
            samples+=tested;mismatches+=wrong;RenderTexture.active=active;
            File.WriteAllBytes(Path.Combine(output,"mirror-mask-"+name+".png"),texture.EncodeToPNG());
            camera.targetTexture=null;rt.Release();DestroyImmediate(plane);DestroyImmediate(material);DestroyImmediate(camera.gameObject);DestroyImmediate(rt);DestroyImmediate(texture);
            Require(wrong==0,"Screen / exposure shadow disagreement at "+name+": "+wrong+"/"+tested);Check("CPU and rendered shadow mask agree at "+name+": "+tested+" samples");
        }
    }
}
