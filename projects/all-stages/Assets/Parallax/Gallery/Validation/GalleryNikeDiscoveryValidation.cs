using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // The cover positive uses the normal perspective pickup/carry path and
    // public Drop. Nike/key solution poses are disclosed fixtures.
    public sealed class GalleryNikeDiscoveryValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt=1f/90;
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly Report report=new Report();
        string output;
        int ticks;
        double deadline;
        SimulationMode previousSimulation;
        bool ownsSimulation,logging;
        GalleryNikeLight nikeLight;
        GalleryNikeLightCover cover;
        Vector3 sourcePosition;
        Quaternion sourceRotation;

        [Serializable] sealed class Report
        {
            public bool passed,physicsRestored,selectedScanPresent,nikeShifted,contourOnly;
            public bool coveredSolutionRejected,heldCoverStillBlocks,inputPickup,inputSlide,inputDrop,coverSettled;
            public bool inputLift,inputFreeCarry,perspectiveScaling;
            public bool lightDiscovered,fixedSourcePreserved,brushObserved,frameEjected,doorOpened,resetRestored;
            public int simulatedTicks,scanTriangles,contourInkPixels,contourLinenPixels,respawns,lightmapCount;
            public float maximumBrushProgress,maximumLift,carriedScaleChange;
            public Vector3 coverStart,coverLifted,coverCarried,coverReleased,fixedLamp;
            public string phase,failure,version,unity,gpu;
            public string scope="Actual saved museum, original Aurelius scan, guide texture histogram and production CPU light/goal queries. The cover positive starts at its original pose and uses public camera Interact, ordinary perspective carry with upward and roomward camera aiming, then Drop/PhysX. There are no cover transform/scale/rigidbody/readiness writes in the positive case. A player-only pickup approach fixture is disclosed. Matching Nike and final key poses are explicitly assigned mathematical fixtures; they test source blocking and the existing brush/ejection/docking sequence, not an input-only solution of those puzzles. Before/lifted/after captures use the identical camera. This is not a GPU/CPU pixel-parity claim.";
            public string[] checks,errors;
        }

        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-validationDir");
            output=at>=0&&at+1<args.Length?Path.GetFullPath(args[at+1]):Path.Combine(Application.persistentDataPath,"GalleryNikeDiscoveryValidation");
            Directory.CreateDirectory(output);
            previousSimulation=Physics.simulationMode;
            Application.logMessageReceived+=Log;logging=true;
            deadline=Time.realtimeSinceStartupAsDouble+120;
            report.version=GalleryGame.Version;report.unity=Application.unityVersion;report.gpu=SystemInfo.graphicsDeviceName;
            var sequence=Sequence();
            while(true)
            {
                bool more;object next=null;
                try{Guard();more=sequence.MoveNext();if(more)next=sequence.Current;}
                catch(Exception exception){report.failure=exception.ToString();break;}
                if(!more)break;
                yield return next;
            }
            RestoreSimulation();report.physicsRestored=Physics.simulationMode==previousSimulation;
            report.simulatedTicks=ticks;report.checks=checks.ToArray();report.errors=errors.ToArray();
            if(game!=null)report.respawns=game.respawns;
            report.passed=report.failure==null&&errors.Count==0&&report.physicsRestored&&report.selectedScanPresent&&
                report.nikeShifted&&report.contourOnly&&report.coveredSolutionRejected&&report.heldCoverStillBlocks&&
                report.inputPickup&&report.inputLift&&report.inputFreeCarry&&report.perspectiveScaling&&report.inputDrop&&report.coverSettled&&report.lightDiscovered&&
                report.fixedSourcePreserved&&report.brushObserved&&report.frameEjected&&report.doorOpened&&report.resetRestored;
            if(!report.passed&&game!=null)try{Capture("nike-discovery-failure");}catch(Exception){}
            File.WriteAllText(Path.Combine(output,"nike-discovery-results.json"),JsonUtility.ToJson(report,true));
            Unsubscribe();
            if(report.passed)Debug.Log("NIKE_DISCOVERY_OK "+checks.Count+" checks");
            else Debug.LogError("NIKE_DISCOVERY_FAIL phase="+report.phase+" "+report.failure);
            Application.Quit(report.passed?0:1);
        }

        IEnumerator Sequence()
        {
            Require(game!=null&&game.automated&&game.conceptLayout,"Actual automated museum is required");
            Physics.simulationMode=SimulationMode.Script;ownsSimulation=true;
            game.conceptCapture=true;
            yield return null;yield return null;
            game.StartConceptDemo(1);
            nikeLight=game.nikeLight;cover=nikeLight==null?null:nikeLight.cover;
            Require(nikeLight!=null&&cover!=null&&cover.Prop!=null,"Original discovery cover and fixed light are required");
            sourcePosition=nikeLight.emitter.position;sourceRotation=nikeLight.emitter.rotation;
            report.fixedLamp=nikeLight.fixedLamp.position;report.coverStart=cover.transform.position;
            report.lightmapCount=LightmapSettings.lightmaps.Length;
            Require(report.lightmapCount>0,"Saved museum baked lightmaps must remain present");
            for(int i=0;i<24;i++){Step();if(i%8==0)yield return null;}
            Require(game.props.Length==7,"Fresh museum must contain seven movable exhibits");
            Require(Vector3.Distance(nikeLight.fixedLamp.position,GalleryNikeLightCover.StartPosition)<.001f,"Fixed lamp was repositioned");
            Require(Vector3.Distance(nikeLight.statue.homePosition,new Vector3(1.5f,0,21.65f))<.001f,"Nike home position must be 1.5m to the right");
            report.nikeShifted=true;
            foreach(var filter in cover.GetComponentsInChildren<MeshFilter>(true))
                if(filter.transform.name.Contains("Aurelius")||filter.GetComponentInParent<GalleryNikeLightCover>()!=null)
                    if(filter.sharedMesh!=null)report.scanTriangles+=filter.sharedMesh.triangles.Length/3;
            Require(report.scanTriangles>200000,"Selected high-detail Aurelius scan is missing");report.selectedScanPresent=true;
            CheckGuide();
            Require(nikeLight.SourceCovered&&!nikeLight.LightDiscovered&&!nikeLight.SolutionReady,"Initial cover must hide and block the light");
            Capture("nike-discovery-01-covered");
            Check("Original scan and right-shifted Nike are present; guide is contour-only and source starts covered");

            report.phase="matching Nike cannot complete while the source is covered";
            PoseFixture(nikeLight.statue,GalleryNikeCanvas.SolutionPosition,nikeLight.statue.homeRotation,GalleryNikeCanvas.SolutionScale);
            Require(nikeLight.canvas.Match()>=nikeLight.canvas.RequiredMatch,"Disclosed Nike fixture must actually match the target");
            for(int i=0;i<36;i++){Step();if(i%8==0)yield return null;}
            Require(nikeLight.SourceCovered&&!nikeLight.Ready&&!nikeLight.SolutionReady&&!nikeLight.canvas.Fusing&&!nikeLight.canvas.Complete,
                "Covered matching Nike incorrectly completed the puzzle");report.coveredSolutionRejected=true;
            nikeLight.statue.Restore();
            Check("Even the exact matching Nike fixture cannot complete through the opaque cover");

            report.phase="actual camera pickup and ordinary perspective lift";
            game.PlacePlayer(new Vector3(-3.5f,0,15.9f));
            for(int i=0;i<12;i++){Step();if(i%6==0)yield return null;}
            foreach(var part in cover.Prop.parts)
            {
                if(part==null||!part.enabled)continue;
                Look(part.bounds.center);game.UpdateAim();
                if(game.aimed==cover.Prop){game.Interact();if(game.held==cover.Prop)break;}
            }
            Require(game.held==cover.Prop,"Public camera ray could not pick up the original cover");report.inputPickup=true;
            Require(!cover.Prop.placement.PreviewWhileHeld&&!cover.Prop.placement.PreviewUntilSettled,
                "Held cover must keep its opaque material instead of becoming a placement ghost");
            Require(!nikeLight.SourceCovered||!nikeLight.SolutionReady,
                "A held cover that still physically blocks the source must not permit completion");
            report.heldCoverStillBlocks=true;
            Require(float.IsPositiveInfinity(((IInteractionProp)cover.Prop).MaxScale),
                "The cover must use ordinary perspective scaling, not a fixed-scale slider");
            float pickupScale=cover.transform.localScale.x;
            Vector3 liftTarget=new Vector3(-6.16f,3.0f,18.2f);
            for(int i=0;i<180;i++)
            {
                Look(liftTarget);Step();
                Require(game.held==cover.Prop,"Ordinary carry unexpectedly lost the cover");
                report.maximumLift=Mathf.Max(report.maximumLift,cover.Prop.WorldBounds.min.y-report.coverStart.y);
                if(i>90&&cover.Prop.WorldBounds.min.y>nikeLight.emitter.position.y+.12f&&!nikeLight.SourceCovered)break;
                if(i%8==0)yield return null;
            }
            Require(cover.Prop.WorldBounds.min.y>nikeLight.emitter.position.y+.12f&&!nikeLight.SourceCovered,
                "Actual perspective pickup did not lift the cover above the fixed lamp: "+cover.transform.position+
                " minY="+cover.Prop.WorldBounds.min.y+" scale="+cover.transform.localScale.x+" "+game.invalidReason+" "+game.PlacementObstacle);
            report.inputLift=true;report.coverLifted=cover.transform.position;
            Capture("nike-discovery-02-lifted");

            report.phase="ordinary three-dimensional perspective carry";
            Vector3 roomward=new Vector3(-1.5f,2.8f,20.5f);
            for(int i=0;i<180;i++)
            {
                Look(roomward);Step();
                Require(game.held==cover.Prop,"Roomward carry unexpectedly lost the cover");
                report.carriedScaleChange=Mathf.Max(report.carriedScaleChange,Mathf.Abs(cover.transform.localScale.x/pickupScale-1));
                if(i%8==0)yield return null;
            }
            report.coverCarried=cover.transform.position;
            Require(Mathf.Abs(report.coverCarried.x-report.coverLifted.x)>.5f&&
                Mathf.Abs(report.coverCarried.z-report.coverStart.z)>.4f,
                "Carry is still constrained to the old horizontal floor track: "+report.coverCarried+" "+game.invalidReason+" "+game.PlacementObstacle);
            Require(report.carriedScaleChange>.03f,"Normal perspective carry did not change object scale");
            report.inputFreeCarry=true;report.perspectiveScaling=true;report.inputSlide=true;
            report.phase="ordinary cover placement and public drop";
            for(int i=0;i<150;i++){Look(new Vector3(-2.8f,.25f,20.5f));Step();if(i%8==0)yield return null;}
            Require(game.Drop()&&game.held==null,"Public Drop rejected moved cover: "+game.invalidReason+" "+game.PlacementObstacle);
            report.inputDrop=true;report.coverReleased=cover.transform.position;
            for(int i=0;i<600&&!cover.Prop.placement.Ready;i++){Step();if(i%8==0)yield return null;}
            Require(cover.Prop.placement.Ready&&game.ValidPlacement(cover.Prop,out _),"Released cover failed to settle: "+cover.Prop.placement.Status);
            report.coverSettled=true;
            Require(nikeLight.LightDiscovered&&!nikeLight.SourceCovered,"Revealed source did not advance discovery");report.lightDiscovered=true;
            Require(Vector3.Distance(sourcePosition,nikeLight.emitter.position)<.0001f&&Quaternion.Angle(sourceRotation,nikeLight.emitter.rotation)<.001f,
                "Cover interaction moved the fixed spotlight");report.fixedSourcePreserved=true;
            Capture("nike-discovery-03-revealed");
            Check("Public ordinary perspective pickup lifts the bust and pedestal, allows free three-dimensional carry and scaling, then Drop settles it beside the same fixed source");

            report.phase="existing Nike brush, frame ejection and door socket with explicit solution fixtures";
            PoseFixture(nikeLight.statue,GalleryNikeCanvas.SolutionPosition,nikeLight.statue.homeRotation,GalleryNikeCanvas.SolutionScale);
            for(int i=0;i<600&&!nikeLight.canvas.Complete;i++)
            {
                Step();report.brushObserved|=nikeLight.canvas.Drawing;
                report.maximumBrushProgress=Mathf.Max(report.maximumBrushProgress,nikeLight.canvas.BrushProgress);
                if(i%8==0)yield return null;
            }
            Require(report.brushObserved&&nikeLight.canvas.Complete&&nikeLight.canvas.portablePainting.gameObject.activeSelf,
                "Revealed matching shadow failed the existing brush/ejection flow");report.frameEjected=true;
            Require(nikeLight.canvas.painting.sharedMaterial.mainTexture==Resources.Load<Texture2D>("MuseumArt/NikeShadowStudy"),
                "Teammate's completed Nike painting was replaced");
            var socket=game.shadowCourse.nikeSocket;
            PoseFixture(nikeLight.canvas.portablePainting,socket.Center-Vector3.forward*.30f,Quaternion.identity,GalleryNikeDoorSocket.TargetScale);
            Require(socket.FitHint(nikeLight.canvas.portablePainting)==null,"Disclosed painting fixture must fit the socket");
            socket.BeginDock(nikeLight.canvas.portablePainting);
            for(int i=0;i<180&&!game.shadowCourse.NikeGateOpen;i++){Step();if(i%8==0)yield return null;}
            Require(socket.Seated&&game.shadowCourse.NikeGateOpen,"Seated original painting failed to open the door");report.doorOpened=true;
            Capture("nike-discovery-04-existing-door-flow");
            Check("After light discovery, matching shadow still performs the original brush, frame ejection and door opening");

            report.phase="current-room reset";
            game.checkpoint=1;game.ResetZone();game.Resume();nikeLight.SyncPose();
            Require(game.held==null&&nikeLight.SourceCovered&&!nikeLight.LightDiscovered&&!nikeLight.canvas.Complete&&!game.shadowCourse.NikeGateOpen&&
                Vector3.Distance(cover.transform.position,GalleryNikeLightCover.StartPosition)<.001f&&
                Vector3.Distance(nikeLight.statue.transform.position,nikeLight.statue.homePosition)<.001f,
                "Current-room reset failed to restore the discovery setup");
            report.resetRestored=true;
            Check("Public room reset restores covered light, discovery state, shifted Nike and closed door");
        }

        void CheckGuide()
        {
            var texture=nikeLight.canvas.guide.sharedMaterial.mainTexture as Texture2D;
            Require(texture!=null&&texture.isReadable,"Saved contour guide must be readable for image QA");
            var pixels=texture.GetPixels32();
            foreach(var pixel in pixels)
            {if(pixel.b<170)report.contourInkPixels++;else if(pixel.r>195&&pixel.g>190&&pixel.b>180)report.contourLinenPixels++;}
            Require(report.contourInkPixels>200&&report.contourInkPixels<pixels.Length*.075f&&report.contourLinenPixels>pixels.Length*.90f,
                "Guide still contains a filled silhouette or lost its visible contour: ink="+report.contourInkPixels+" linen="+report.contourLinenPixels);
            report.contourOnly=true;
        }
        void PoseFixture(GalleryProp prop,Vector3 position,Quaternion rotation,float scale)
        {
            prop.Restore();prop.transform.SetPositionAndRotation(position,rotation);prop.transform.localScale=Vector3.one*scale;
            prop.body.position=position;prop.body.rotation=rotation;Physics.SyncTransforms();prop.placement.ResetPlaced();
        }
        void Look(Vector3 point)
        {
            Vector3 direction=point-game.view.transform.position;
            game.yaw=Mathf.Atan2(direction.x,direction.z);game.pitch=Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude);game.SyncView();
        }
        void Step()
        {
            Guard();game.Tick(Dt,Vector2.zero,false);Physics.Simulate(Dt);Physics.SyncTransforms();
            game.nikeLight.SyncPose();game.lighting.Synchronize();ticks++;
        }
        void Guard(){Require(Time.realtimeSinceStartupAsDouble<=deadline&&ticks<5000,"Discovery validation budget exceeded");}
        void Require(bool condition,string message){if(!condition)throw new InvalidOperationException("NIKE_DISCOVERY: "+message);}
        void Check(string message){checks.Add(message);Debug.Log("NIKE_DISCOVERY_PASS "+message);}
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        void RestoreSimulation(){if(ownsSimulation){Physics.simulationMode=previousSimulation;ownsSimulation=false;}}
        void Unsubscribe(){if(logging){Application.logMessageReceived-=Log;logging=false;}}
        void OnDestroy(){RestoreSimulation();Unsubscribe();}
        void Capture(string name)
        {
            var camera=game.view;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=new RenderTexture(1600,900,24);Texture2D image=null;
            try
            {
                Vector3 entranceCamera=new Vector3(0,1.75f,15.5f);
                camera.transform.SetPositionAndRotation(entranceCamera,Quaternion.LookRotation(new Vector3(-3,1.8f,21.5f)-entranceCamera));
                camera.targetTexture=target;nikeLight.SyncPose();game.lighting.Synchronize();camera.Render();RenderTexture.active=target;
                image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
                target.Release();Destroy(target);if(image!=null)Destroy(image);
            }
        }
    }
}
