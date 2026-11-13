using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Actual automatic PhysX FixedUpdate regression, using the saved museum.
    // Disclosed poses isolate the book extraction/load; solved/extracted/reward
    // state is never set through reflection, and Physics.Simulate is not used.
    [DefaultExecutionOrder(90)]
    public sealed class GalleryDikeSwordValidation : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed, automaticFixedPhysics, externalSwordAndStatueReady, flatBookRoot;
            public bool contentsSizeFixed, publicBookExtraction, releasedBookDynamic, shortDwellRejected;
            public bool balancedForTwoSeconds, statueEmissionApplied, swordIndependentOfStatue;
            public bool realGravityDrop, noAutomaticRecovery, doorLockedAfterBalance, actualPauseFreezesSword;
            public bool publicSwordPickup, carryCollisionProtected, publicCancelReturnsSword, publicSocketUse;
            public bool insertUnlocksDoor, woodenLeavesSwing, resetRestores, unverifiedAssetFailsClosed;
            public bool enlargedFixedSwordFitsPan, slowerInsertion, pauseFreezesInsertion, seatDelayKeepsDoorClosed;
            public bool insertionRumbleOnce, rumbleWeakerThanHands, pauseFreezesRumble, cameraReturnsToBase, slowDoorOpening;
            public bool alignedVerticalInsertion;
            public bool selectedDikeAudioReady, firstCollisionAudioOnce, selectedSwordAudioSequence, sizeMappedFloorAudio, compactRoom;
            public bool g01GlowPlaybackObserved, g01GlowOnceAtBalance;
            public bool actualP02Pedestal, realPedestalSlotOpening, rearWallExhibits, centreReceiverCirculation;
            public string[] dikeAudioEvents;
            public float roomWidth,roomLength,rumbleDurationSeconds;
            public int fixedSteps, balanceEvents, releaseEvents, releaseCount, recoveryCount;
            public int pickupApproachesTried, pickupClearViews;
            public string pickupApproach;
            public float stableSeconds, maximumDrop, maximumSpeed, maximumEmission, statueRootMovement;
            public float maximumBookMeshSizeError, insertionProgress, leftDoorAngle, rightDoorAngle;
            public float pausedBodyPositionChange, pausedBodyRotationChange, pausedVisualPositionChange;
            public float pausedFixedTimeChange;
            public float fixedSwordLength, insertionSeconds, seatedDoorDelaySeconds, doorOpeningSeconds;
            public float maximumRumbleIntensity, maximumCameraOffset, maximumCameraRotation, maximumRumbleToHandRatio;
            public float maximumInsertionSideways, verticalInsertionTravel;
            public int pausedFixedSteps;
            public string phase, failure, unity, gpu, physicsMode, rewardStage, availabilityReason;
            public string scope = "Saved museum, actual automatic FixedUpdate PhysX. Disclosed fixtures position/enlarge the freely held bottle, rotate it through public RotateHeld, and position an extracted law book at TargetSculptureScale*1.04 on the actual pan. Real containment exit, public sword pickup/cancel/socket use, release Rigidbody gravity, pause, and door swing use production paths. No private solved/extracted flags, kinematic sword fall, manual Physics.Simulate, or snapshot image are substituted for release verification. Emission is verified on the actual statue renderer property and captured separately. This is focused integration QA, not continuous human play or subjective art approval.";
            public string[] checks, errors, pickupGroundDiagnostics;
        }
        readonly Report report = new Report();
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly List<string> pickupGroundDiagnostics = new List<string>();
        GalleryGame game;
        GalleryBottleBalanceCourse course;
        GalleryBottleBalanceKeyReward reward;
        Renderer[] bookRenderers;
        Vector3[] bookSizes;
        Transform statueRoot;
        Vector3 statueStart;
        Quaternion statueStartRotation;
        string output;
        double deadline;
        int fixedSteps, balances, releases;
        bool drive, configured, flashCaptured;
        float oldFixedDelta, oldTimeScale;
        Vector2 move;
        bool trackingVerticalInsertion;
        Vector3 verticalInsertionStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gallery-dike-sword-test") < 0) return;
            new GameObject("Dike actual gravity and sword socket validation").AddComponent<GalleryDikeSwordValidation>();
        }

        IEnumerator Start()
        {
            oldFixedDelta = Time.fixedDeltaTime; oldTimeScale = Time.timeScale;
            configured = true;
            Application.logMessageReceived += Log;
            deadline = Time.realtimeSinceStartupAsDouble + 240;
            report.unity = Application.unityVersion; report.gpu = SystemInfo.graphicsDeviceName;
            report.physicsMode = Physics.simulationMode.ToString();
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-validationDir");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at+1]) :
                Path.Combine(Application.persistentDataPath, "MuseumDikeSwordValidation");
            Directory.CreateDirectory(output);
            IEnumerator sequence = Sequence();
            while (true)
            {
                bool more; object next = null;
                try { Guard(); more = sequence.MoveNext(); if (more) next = sequence.Current; }
                catch (Exception exception) { report.failure = exception.ToString(); break; }
                if (!more) break;
                yield return next;
            }
            drive = false; Restore();
            report.fixedSteps = fixedSteps; report.balanceEvents = balances; report.releaseEvents = releases;
            if (reward != null)
            {
                report.rewardStage = reward.Stage.ToString(); report.availabilityReason = reward.AvailabilityReason;
                report.releaseCount = Mathf.Max(report.releaseCount,reward.ReleaseCount);
                report.recoveryCount = Mathf.Max(report.recoveryCount,reward.RecoveryCount);
            }
            report.checks = checks.ToArray(); report.errors = errors.ToArray();
            if(course!=null&&course.Sounds!=null)
            {
                var audioEvents=new List<string>();
                foreach(var stamp in course.Sounds.History)audioEvents.Add(stamp.cue+" @ "+stamp.time.ToString("F3"));
                if(audioEvents.Count>0)report.dikeAudioEvents=audioEvents.ToArray();
            }
            report.pickupGroundDiagnostics=pickupGroundDiagnostics.ToArray();
            report.passed = string.IsNullOrEmpty(report.failure) && errors.Count == 0 &&
                report.automaticFixedPhysics && report.externalSwordAndStatueReady && report.flatBookRoot &&
                report.contentsSizeFixed && report.publicBookExtraction && report.releasedBookDynamic &&
                report.shortDwellRejected && report.balancedForTwoSeconds && report.statueEmissionApplied &&
                report.swordIndependentOfStatue && report.realGravityDrop && report.noAutomaticRecovery &&
                report.doorLockedAfterBalance && report.actualPauseFreezesSword && report.publicSwordPickup &&
                report.carryCollisionProtected && report.publicCancelReturnsSword && report.publicSocketUse &&
                report.insertUnlocksDoor && report.woodenLeavesSwing && report.resetRestores && report.unverifiedAssetFailsClosed &&
                report.enlargedFixedSwordFitsPan && report.slowerInsertion && report.pauseFreezesInsertion &&
                report.seatDelayKeepsDoorClosed && report.insertionRumbleOnce && report.rumbleWeakerThanHands &&
                report.pauseFreezesRumble && report.cameraReturnsToBase && report.slowDoorOpening && report.alignedVerticalInsertion &&
                report.selectedDikeAudioReady&&report.firstCollisionAudioOnce&&report.selectedSwordAudioSequence&&report.sizeMappedFloorAudio&&report.compactRoom&&
                report.g01GlowPlaybackObserved&&report.g01GlowOnceAtBalance&&report.actualP02Pedestal&&
                report.realPedestalSlotOpening&&report.rearWallExhibits&&report.centreReceiverCirculation;
            if (!report.passed && game != null) Capture("dike-validation-failure");
            File.WriteAllText(Path.Combine(output, "dike-sword-validation-results.json"), JsonUtility.ToJson(report, true));
            if (report.passed) Debug.Log("GALLERY_DIKE_SWORD_VALIDATION_OK " + checks.Count + " checks");
            else Debug.LogError("GALLERY_DIKE_SWORD_VALIDATION_FAIL " + report.phase + " " + report.failure);
            Application.Quit(report.passed ? 0 : 1);
        }

        void FixedUpdate()
        {
            if (!drive || game == null || !game.active || game.won) return;
            game.Tick(Time.fixedDeltaTime, move, false); fixedSteps++;
            if(reward!=null&&reward.Busy&&reward.BladeTravelProgress>.04f)
            {
                if(!trackingVerticalInsertion){trackingVerticalInsertion=true;verticalInsertionStart=reward.SwordTransform.position;}
                Vector3 displacement=reward.SwordTransform.position-verticalInsertionStart;
                report.maximumInsertionSideways=Mathf.Max(report.maximumInsertionSideways,new Vector2(displacement.x,displacement.z).magnitude);
            }
            if(reward!=null){report.releaseCount=Mathf.Max(report.releaseCount,reward.ReleaseCount);report.recoveryCount=Mathf.Max(report.recoveryCount,reward.RecoveryCount);}
            if (reward != null && reward.SwordBody != null && !reward.SwordBody.isKinematic)
                report.maximumSpeed = Mathf.Max(report.maximumSpeed, reward.SwordBody.linearVelocity.magnitude);
            if (statueRoot != null)
                report.statueRootMovement = Mathf.Max(report.statueRootMovement, Vector3.Distance(statueStart, statueRoot.position));
            if (course != null && course.Art.statueRenderers != null)
                foreach (var renderer in course.Art.statueRenderers)
                {
                    if (renderer == null) continue;
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                    report.maximumEmission = Mathf.Max(report.maximumEmission, block.GetColor("_EmissionColor").maxColorComponent);
                }
        }

        void LateUpdate()
        {
            var effect=course?.InsertionRumble;
            if(game==null||game.view==null||!game.active||effect==null||effect.Intensity<=0)return;
            Vector3 baseEye=game.controller.transform.position+Vector3.up*1.55f;
            float offset=Vector3.Distance(game.view.transform.position,baseEye);
            report.maximumRumbleIntensity=Mathf.Max(report.maximumRumbleIntensity,effect.Intensity);
            report.maximumCameraOffset=Mathf.Max(report.maximumCameraOffset,offset);
            report.maximumCameraRotation=Mathf.Max(report.maximumCameraRotation,effect.AppliedEulerOffset.magnitude);
            GalleryCathedralRumble.Sample(effect.Elapsed,effect.Intensity/GalleryDikeInsertionRumble.Strength,out var handPosition,out var handRotation);
            if(handPosition.magnitude>.001f)
                report.maximumRumbleToHandRatio=Mathf.Max(report.maximumRumbleToHandRatio,offset/handPosition.magnitude);
        }

        IEnumerator Sequence()
        {
            report.phase = "saved museum and verified external components";
            for (int i=0;i<8;i++) yield return null;
            game = FindFirstObjectByType<GalleryGame>();
            Require(game != null && game.conceptLayout && game.bottleBalance != null, "Saved museum with the inserted room is required.");
            Require(Physics.simulationMode == SimulationMode.FixedUpdate, "Run this validation in automatic FixedUpdate PhysX mode.");
            report.automaticFixedPhysics = true;
            game.automated = true; Application.runInBackground = true; Application.targetFrameRate = 90;
            course = game.bottleBalance; reward = course.Reward;
            Require(reward != null && reward.Available && reward.HasExternalKeyModel && course.Art.SwordReleaseVerified,
                "An independently verified external sword is required; unverified/missing assets must not pass this test. " + reward?.AvailabilityReason);
            bool conceptSwordUsed=false;
            foreach(var part in reward.SwordTransform.GetComponentsInChildren<Transform>(true))
                if(part.name=="Dike · concept-matched ceremonial sword"){conceptSwordUsed=true;break;}
            Require(Resources.Load<GameObject>("BottleBalance/Dike/ConceptSword")!=null&&conceptSwordUsed,
                "The new concept-matched sword resource must actually be used; the older statue sword is not an acceptable fallback for this validation.");
            Require(course.Art.dikeExhibit != null && course.Art.dikeExhibit.StatueRoot != null,
                "Actual statue root is needed to prove the whole statue does not fall.");
            statueRoot = course.Art.dikeExhibit.StatueRoot;
            foreach (var renderer in course.Art.statueRenderers)
                Require(renderer == null || !renderer.transform.IsChildOf(reward.SwordTransform), "The reward transform also owns a statue-body renderer.");
            report.externalSwordAndStatueReady = true;
            bookRenderers = course.Sculpture.GetComponentsInChildren<Renderer>();
            Require(bookRenderers.Length > 0 && course.Sculpture.title.Contains("법"), "The bottle's movable object must be the law book.");
            Bounds book = LocalBounds(course.Sculpture.transform);
            Require(book.size.y < Mathf.Max(book.size.x, book.size.z) * .65f, "Law-book root is not flat; check visual and collider orientation.");
            report.flatBookRoot = true;
            var fixedSword=course.Art.fixedSculpture.Find("Fixed ceremonial dagger · independent sword miniature");
            Require(fixedSword!=null,"Fixed left-pan sword is missing.");
            Bounds fixedBounds=GalleryDikeExhibit.WorldBounds(fixedSword);
            report.fixedSwordLength=Mathf.Max(fixedBounds.size.x,Mathf.Max(fixedBounds.size.y,fixedBounds.size.z));
            Bounds panBounds=course.Art.leftPan.GetComponent<BoxCollider>().bounds;
            Require(report.fixedSwordLength>.76f&&report.fixedSwordLength<.80f&&
                fixedBounds.min.x>panBounds.min.x&&fixedBounds.max.x<panBounds.max.x&&
                fixedBounds.min.z>panBounds.min.z&&fixedBounds.max.z<panBounds.max.z,
                "Enlarged fixed sword does not visibly fit within the left pan.");
            report.enlargedFixedSwordFitsPan=true;
            bookSizes = new Vector3[bookRenderers.Length];
            for(int i=0;i<bookRenderers.Length;i++)bookSizes[i]=IntrinsicSize(bookRenderers[i]);
            Time.fixedDeltaTime = 1f/90;
            course.BalanceCompleted += OnBalance; reward.SwordReleased += OnRelease;
            game.StartBottleBalanceDemo(); drive = true;
            Require(course.Sounds!=null&&MuseumAudio.Available,"Selected Dike audio routing was not installed.");
            foreach(string cue in new[]{"dike.glass.contact","dike.bottle.small","dike.bottle.medium","dike.bottle.large",
                "dike.book.small","dike.book.medium","dike.book.large","dike.scale.motion","dike.sword.land","dike.sword.pickup",
                "dike.sword.insert","dike.sword.seat","dike.socket.rumble","dike.door.open"})
                Require(MuseumAudio.Instance.HasClip(cue),"Missing selected cue "+cue);
            Require(MuseumAudio.Instance.Clip("dike.bottle.small")==MuseumAudio.Instance.Clip("dike.bottle.medium")&&
                MuseumAudio.Instance.Clip("dike.book.medium")==MuseumAudio.Instance.Clip("dike.book.large")&&
                MuseumAudio.Instance.Clip("dike.book.small").name.Contains("D14")&&
                MuseumAudio.Instance.Clip("dike.book.medium").name.Contains("D11"),"User-selected cross-size D04/D14/D11 mapping changed.");
            Require(!course.Sounds.GlowAwaitingSelection&&MuseumAudio.Instance.HasClip("dike.glow")&&
                MuseumAudio.Instance.Clip("dike.glow").name=="G01_StatueGlow"&&
                MuseumAudio.Duration("dike.glow")>.75f&&MuseumAudio.Duration("dike.glow")<1.05f,
                "The selected original G01 statue-glow clip was not loaded.");
            Require(Mathf.Abs(MuseumAudio.Duration("dike.socket.rumble")-1.6f)<.02f&&GalleryDikeInsertionRumble.Duration==1.6f,
                "Slowed D37 and longer camera vibration must both last 1.60 seconds.");
            report.rumbleDurationSeconds=GalleryDikeInsertionRumble.Duration;report.selectedDikeAudioReady=true;
            report.roomWidth=GalleryBottleBalanceLayout.HalfWidth*2;report.roomLength=GalleryBottleBalanceLayout.AddedLength;
            Require(Mathf.Abs(report.roomWidth-22)<.001f&&Mathf.Abs(report.roomLength-17)<.001f&&
                Mathf.Abs(course.Door.Root.position.z-(GalleryBottleBalanceLayout.EndZ-.22f))<.001f&&
                Vector2.Distance(new Vector2(reward.LockTransform.position.x,reward.LockTransform.position.z),
                    new Vector2(GalleryDikeGalleryLayout.ReceiverStation.x,GalleryDikeGalleryLayout.ReceiverStation.z))<.001f,
                "Compact room boundaries, door or receiver are disconnected.");
            report.compactRoom=true;
            Require(Vector3.Distance(course.Art.bottleStation.position,GalleryDikeGalleryLayout.BottleStation)<.001f&&
                Vector3.Distance(course.Art.scaleStation.position,GalleryDikeGalleryLayout.ScaleStation)<.001f,
                "Bottle and scale did not move to opposite sides of the exit wall.");report.rearWallExhibits=true;
            var pedestal=reward.LockTransform.parent.Find("P02 · actual Treleau Versailles pedestal body");
            Require(pedestal!=null&&pedestal.GetComponent<MeshFilter>()!=null&&pedestal.GetComponent<MeshCollider>()!=null&&
                pedestal.GetComponent<MeshFilter>().sharedMesh.name.Contains("Treleau Versailles P02"),"Actual external P02 body is missing.");
            var marble=pedestal.GetComponent<Renderer>().sharedMaterial;
            Require(marble.mainTexture is Texture2D&&marble.mainTexture.width>=2048&&marble.GetTexture("_BumpMap") is Texture2D&&
                marble.GetTexture("_SpecGlossMap") is Texture2D,"P02 scan textures did not bind to the rendered marble material.");
            report.actualP02Pedestal=true;
            Vector3 holeRay=reward.LockTransform.position+Vector3.up*.10f;
            Require(Physics.Raycast(holeRay,Vector3.down,out var cavity,.95f,~0,QueryTriggerInteraction.Ignore)&&
                cavity.point.y<GalleryDikeGalleryLayout.ReceiverStation.y+.70f,"P02 top is still solid across the visible blade opening.");
            Require(Physics.Raycast(holeRay+Vector3.right*.22f,Vector3.down,out var cap,.45f,~0,QueryTriggerInteraction.Ignore)&&
                cap.collider==pedestal.GetComponent<MeshCollider>(),"P02 scan has no physical top cap beside its real opening.");
            report.realPedestalSlotOpening=true;
            statueStart = statueRoot.position; statueStartRotation = statueRoot.rotation;
            Check("Saved room contains the flat law book and an actual separately verified sword/statue");
            Capture("00-dike-room-before-balance");
            // Detail captures only: these are not extra pass conditions. The
            // following extraction phase explicitly resets its own viewpoint.
            Vector3 detailFeet;string detailFloor;
            Vector3 statueApproach=GalleryDikeGalleryLayout.ScaleStation+new Vector3(0,0,-3.5f);
            if(!TryGalleryFloorFeet(statueApproach,out detailFeet,out detailFloor))detailFeet=statueApproach+Vector3.up*.03f;
            game.PlacePlayer(detailFeet);
            bool firstStatue=true;Bounds statueBounds=new Bounds();
            foreach(var renderer in statueRoot.GetComponentsInChildren<Renderer>())
                if(firstStatue){statueBounds=renderer.bounds;firstStatue=false;}else statueBounds.Encapsulate(renderer.bounds);
            Look(statueBounds.center+Vector3.up*.4f);Capture("00b-statue-detail");
            Vector3 socketDetail=reward.LockTransform.position;
            if(!TryGalleryFloorFeet(new Vector3(socketDetail.x,0,socketDetail.z-2),out detailFeet,out detailFloor))
                detailFeet=new Vector3(socketDetail.x,.03f,socketDetail.z-2);
            game.PlacePlayer(detailFeet);Look(socketDetail);Capture("00c-stone-socket-detail");
            CaptureSwordCloseup("00d-sword-held-detail");
            Vector3 leftPan=course.Art.leftPan.position;
            game.PlacePlayer(new Vector3(leftPan.x,0,leftPan.z-1.9f));Look(leftPan+Vector3.up*.12f);Capture("00e-left-pan-enlarged-sword");

            report.phase = "fixed-size contained law book and public physical extraction";
            Pose(course.Bottle, new Vector3(0,3.2f,34), Quaternion.identity, 1);
            for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();
            CheckBookSize();
            Pose(course.Bottle, new Vector3(0,3.2f,34), Quaternion.identity, 1.95f);
            for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();
            CheckBookSize(); report.contentsSizeFixed=true;
            var glass = course.Art.bottleVisual.Find("Open hand-blown vessel")?.GetComponent<Renderer>();
            Require(glass != null, "Actual open glass vessel renderer is missing.");
            game.PlacePlayer(new Vector3(0,0,30.2f)); Look(glass.bounds.center); game.UpdateAim(); game.Interact();
            Require(game.held == course.Bottle, "Public bottle pickup failed.");
            game.RotateHeld(-90,0);
            for(int i=0;i<6;i++)yield return new WaitForFixedUpdate();
            for(int i=0;i<80&&GalleryBottleBalanceVessel.MouthDirection(course.Bottle).y>-.96f;i++)
            { game.RotateHeld(0,2); yield return new WaitForFixedUpdate(); CheckBookSize(); }
            for(int lift=0;lift<8&&(course.ExtractionExitPosition.y<.70f||course.ExtractionBlocked);lift++)
            {
                Look(game.view.transform.position+game.view.transform.forward*8+Vector3.up*.55f);
                for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();
            }
            int until=fixedSteps+1200;
            while(!course.Extracted&&fixedSteps<until){yield return new WaitForFixedUpdate();CheckBookSize();}
            Require(course.Extracted&&!course.Sculpture.body.isKinematic&&course.Sculpture.body.useGravity,
                "The real law book never fully exited into ordinary gravity: "+course.Status);
            report.publicBookExtraction=report.releasedBookDynamic=true;
            if(game.held!=null)game.Cancel(true);
            Pose(course.Bottle,new Vector3(-7,1,32.5f),Quaternion.identity,1);
            Check("Bottle resizing preserves the law-book mesh size; public held tilt produces actual containment exit and gravity");

            report.phase = "level dwell, applied statue emission and actual sword release";
            var pan=course.Art.rightPan.GetComponent<BoxCollider>();
            Pose(course.Sculpture,new Vector3(2,2.5f,33),Quaternion.identity,1);
            for(int i=0;i<35;i++)yield return new WaitForFixedUpdate();
            Require(!course.Solved&&course.StableSeconds<.01f,"Earlier load kept a stale balance dwell.");
            PoseOnPan(course.Sculpture,pan,GalleryBottleBalanceCourse.TargetSculptureScale*1.04f);
            course.Sculpture.Release();
            PoseRewardView();
            for(int i=0;i<70;i++)yield return new WaitForFixedUpdate();
            Require(!course.Solved&&reward.ReleaseCount==0&&course.Door.Opening<.001f&&course.Sounds.Count("dike.glow")==0,
                "Short dwell released the reward, glow sound or door.");
            report.shortDwellRejected=true;
            until=fixedSteps+1200;
            while(!course.Solved&&fixedSteps<until)yield return new WaitForFixedUpdate();
            Require(course.Solved&&balances==1&&course.StableSeconds>=2,"The released book did not complete exactly one two-second balance.");
            report.stableSeconds=course.StableSeconds;report.balancedForTwoSeconds=true;
            Require(course.Sounds.Count("dike.glow")==1&&reward.Stage==GalleryBottleBalanceKeyReward.RewardStage.Flashing,
                "G01 did not fire once at the beginning of the real statue glow.");
            foreach(var voice in MuseumAudio.Instance.GetComponentsInChildren<AudioSource>())
                if(voice.clip==MuseumAudio.Instance.Clip("dike.glow")&&voice.isPlaying&&voice.pitch==1)
                    report.g01GlowPlaybackObserved=true;
            Require(report.g01GlowPlaybackObserved,"No playing production G01 AudioSource was observed during the statue glow.");
            Require(!reward.Unlocked&&course.Door.Opening<.001f&&course.Door.Barrier.enabled,"Balance alone unlocked the door.");
            until=fixedSteps+180;
            while(reward.Stage==GalleryBottleBalanceKeyReward.RewardStage.Flashing&&fixedSteps<until)
            { if(!flashCaptured&&reward.FlashProgress>.35f&&reward.FlashProgress<.7f){Capture("01-balanced-statue-flash");flashCaptured=true;}yield return new WaitForFixedUpdate(); }
            Require(report.maximumEmission>.12f,"No emission was applied to the actual statue renderers during the success pulse.");
            report.statueEmissionApplied=true;
            Require(reward.Stage==GalleryBottleBalanceKeyReward.RewardStage.Falling&&reward.ReleaseCount==1&&releases==1&&
                !reward.SwordBody.isKinematic&&reward.SwordBody.useGravity,"The sword did not detach into a real dynamic Rigidbody.");
            Require(!reward.SwordTransform.IsChildOf(statueRoot),"Sword is still parented to the statue during its physical fall.");
            report.swordIndependentOfStatue=true;PoseRewardView();Capture("02-sword-independent-gravity-drop");

            report.phase="actual pause during gravity fall";
            // WaitForFixedUpdate can resume before the current physics step is
            // fully presented. A user pauses from Update, so first finish that
            // fixed cycle and sample authoritative Rigidbody poses, not the
            // interpolated renderer Transform from the previous physics state.
            yield return null;
            game.Pause();
            Vector3 pausedBodyPosition=reward.SwordBody.position;
            Quaternion pausedBodyRotation=reward.SwordBody.rotation;
            Vector3 pausedVisualPosition=reward.SwordTransform.position;
            float pausedFlash=reward.FlashProgress;int pausedStep=fixedSteps;
            double pausedFixedTime=Time.fixedTimeAsDouble;
            yield return new WaitForSecondsRealtime(.20f);
            report.pausedBodyPositionChange=Vector3.Distance(pausedBodyPosition,reward.SwordBody.position);
            report.pausedBodyRotationChange=Quaternion.Angle(pausedBodyRotation,reward.SwordBody.rotation);
            report.pausedVisualPositionChange=Vector3.Distance(pausedVisualPosition,reward.SwordTransform.position);
            report.pausedFixedTimeChange=(float)Math.Abs(Time.fixedTimeAsDouble-pausedFixedTime);
            report.pausedFixedSteps=fixedSteps-pausedStep;
            Require(!game.active&&Time.timeScale==0&&report.pausedBodyPositionChange<.0001f&&
                report.pausedBodyRotationChange<.001f&&report.pausedFixedSteps==0&&report.pausedFixedTimeChange<.000001f&&
                Mathf.Abs(pausedFlash-reward.FlashProgress)<.0001f,"Pause advanced the physical sword/fixed clock or reward. body="+
                report.pausedBodyPositionChange+" rotation="+report.pausedBodyRotationChange+" steps="+report.pausedFixedSteps+
                " fixedTime="+report.pausedFixedTimeChange+" visualInterpolation="+report.pausedVisualPositionChange);
            game.Resume();report.actualPauseFreezesSword=true;
            until=fixedSteps+1300;
            while(!reward.SwordAvailable&&fixedSteps<until)yield return new WaitForFixedUpdate();
            report.maximumDrop=reward.MaximumDropDistance;
            Require(reward.SwordAvailable&&reward.FirstFallCompletedPhysically&&reward.MaximumDropDistance>.20f&&report.maximumSpeed>.3f,
                "A real gravity drop and physical settle were not observed.");
            Require(reward.RecoveryCount==0,"Automatic recovery substituted for the first physical sword fall.");
            Require(report.statueRootMovement<.002f&&Quaternion.Angle(statueStartRotation,statueRoot.rotation)<.1f,
                "The statue body moved with the released sword.");
            Require(!reward.Unlocked&&course.Door.Opening<.001f&&course.Door.Barrier.enabled,"Sword drop opened the door without insertion.");
            report.realGravityDrop=report.noAutomaticRecovery=report.doorLockedAfterBalance=true;
            Require(course.Sounds.FirstSwordContactCount==1&&course.Sounds.Count("dike.sword.land")==1&&
                course.Sounds.FirstSwordContactTime<Time.time-.10f,"D28 must play once at the first collision, before the sword finishes settling.");
            report.firstCollisionAudioOnce=true;
            CaptureSwordCloseup("02b-sword-on-recovery-detail");
            Check("One emission pulse releases only the sword into real gravity; pause freezes it and first settle needs no recovery while the door remains locked");

            report.phase="public pickup, long-sword carry collision and explicit cancel";
            AimAtSword();game.UpdateAim();game.Interact();
            Require(reward.Carrying&&!reward.Unlocked&&!reward.SwordBody.useGravity&&course.Door.Opening<.001f,"Public sword pickup failed or opened the door.");
            report.publicSwordPickup=true;
            game.PlacePlayer(new Vector3(GalleryBottleBalanceLayout.HalfWidth-.55f,0,34));
            Look(new Vector3(GalleryBottleBalanceLayout.HalfWidth+2,1.55f,34));
            for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();
            Require(reward.CarryPoseBlocked,"Long carried sword did not detect the close wall.");
            foreach(var renderer in reward.SwordTransform.GetComponentsInChildren<Renderer>())
                Require(!renderer.enabled,"A blocked carried sword remained visible through the wall.");
            report.carryCollisionProtected=true;
            game.Cancel();
            Require(!reward.Carrying&&reward.SwordAvailable&&!reward.Unlocked,"Public cancel did not return the sword to its recovery pad.");
            report.publicCancelReturnsSword=true;
            Check("Public pickup/cancel work for the long sword, and full-length carry collision prevents drawing it through a close wall");

            report.phase="public sword socket insertion and door opening";
            AimAtSword();game.UpdateAim();game.Interact();Require(reward.Carrying,"Returned sword cannot be picked again.");
            Vector3 socket=reward.LockTransform.position;
            game.PlacePlayer(new Vector3(socket.x,0,socket.z-2.2f));Look(socket+Vector3.up*.08f);
            for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();
            reward.RefreshAim(game.view);
            Require(reward.TargetedLock&&!reward.CarryPoseBlocked,"Floor camera cannot target the real socket with a clear carry pose.");
            game.UpdateAim();game.Interact();
            Require(reward.Busy&&!reward.Unlocked,"Explicit socket click did not begin insertion.");
            report.publicSocketUse=true;Capture("03-sword-socket-insertion");
            var rumble=course.InsertionRumble;
            Require(rumble!=null&&rumble.TriggerCount==0,"The seating rumble fired before the sword entered the slot.");
            double insertStarted=Time.fixedTimeAsDouble;
            until=fixedSteps+200;
            while(reward.InsertionProgress<.38f&&fixedSteps<until)
            {
                Require(course.Door.Opening<.001f&&!reward.Unlocked&&rumble.TriggerCount==0,
                    "Door or rumble started before the blade seated.");
                yield return new WaitForFixedUpdate();
            }
            Require(reward.Busy&&reward.InsertionProgress>.3f&&reward.InsertionProgress<.6f,"No gradual midpoint was observed during insertion.");
            Capture("03a-slow-insertion-midpoint");
            yield return null;game.Pause();
            float pausedInsert=reward.InsertionProgress;Vector3 pausedInsertPosition=reward.SwordTransform.position;
            yield return new WaitForSecondsRealtime(.18f);
            Require(Mathf.Abs(reward.InsertionProgress-pausedInsert)<.00001f&&
                Vector3.Distance(pausedInsertPosition,reward.SwordTransform.position)<.0001f&&
                rumble.TriggerCount==0&&course.Door.Opening<.001f,"Pause advanced insertion or prematurely triggered the next phase.");
            game.Resume();report.pauseFreezesInsertion=true;
            until=fixedSteps+200;
            while(!reward.Unlocked&&fixedSteps<until)yield return new WaitForFixedUpdate();
            report.insertionSeconds=(float)(Time.fixedTimeAsDouble-insertStarted);
            Require(reward.Unlocked&&report.insertionSeconds>=1.48f&&report.insertionSeconds<1.58f,
                "Actual insertion duration was not approximately 1.5 gameplay seconds: "+report.insertionSeconds);
            report.slowerInsertion=true;
            Vector3 finalInsertionDelta=reward.SwordTransform.position-verticalInsertionStart;
            report.verticalInsertionTravel=-finalInsertionDelta.y;
            Require(trackingVerticalInsertion&&report.maximumInsertionSideways<.001f&&report.verticalInsertionTravel>.5f,
                "The aligned blade did not enter vertically along the socket axis.");
            report.alignedVerticalInsertion=true;
            Require(rumble.TriggerCount==1&&course.Door.Opening<.001f,"Seated sword must trigger one pulse before door movement.");
            double seatedAt=Time.fixedTimeAsDouble;
            while(rumble.Elapsed<.12f)yield return new WaitForFixedUpdate();
            yield return null;game.Pause();
            float pausedPulseTime=rumble.Elapsed,pausedSeatedTime=reward.SeatedTime;
            yield return new WaitForSecondsRealtime(.18f);
            Require(Mathf.Abs(rumble.Elapsed-pausedPulseTime)<.00001f&&Mathf.Abs(reward.SeatedTime-pausedSeatedTime)<.00001f&&
                rumble.AppliedLocalOffset.sqrMagnitude<.00000001f&&course.Door.Opening<.001f,
                "Pause advanced the seat/pulse clock or left a visible camera offset.");
            game.Resume();report.pauseFreezesRumble=true;
            while(!reward.DoorReleaseReady)
            {Require(course.Door.Opening<.001f,"Door moved before the seating delay completed.");yield return new WaitForFixedUpdate();}
            report.seatDelayKeepsDoorClosed=true;
            until=fixedSteps+100;
            while(course.Door.Opening<=0&&fixedSteps<until)yield return new WaitForFixedUpdate();
            report.seatedDoorDelaySeconds=(float)(Time.fixedTimeAsDouble-seatedAt);
            double doorStarted=Time.fixedTimeAsDouble;
            Require(report.seatedDoorDelaySeconds>=.38f&&report.seatedDoorDelaySeconds<.46f,"Seating delay is not approximately 0.4 seconds.");
            while(course.Door.Opening<.48f)yield return new WaitForFixedUpdate();
            Require(course.Door.Opening<.6f&&course.Door.Barrier.enabled,"Door jumped to open instead of moving slowly.");
            Capture("03b-door-opening-after-seating");
            until=fixedSteps+300;
            while(course.Door.Opening<.999f&&fixedSteps<until)yield return new WaitForFixedUpdate();
            report.doorOpeningSeconds=(float)(Time.fixedTimeAsDouble-doorStarted);
            Require(report.doorOpeningSeconds>=2.94f&&report.doorOpeningSeconds<3.08f,"Door did not take approximately three seconds to open.");
            report.slowDoorOpening=true;
            yield return null;
            Require(rumble.TriggerCount==1&&!rumble.Pulsing&&rumble.Intensity==0,"Seating pulse repeated or did not finish.");
            Require(report.maximumRumbleIntensity>.65f&&report.maximumRumbleIntensity<=.751f&&
                report.maximumCameraOffset>.001f&&report.maximumCameraOffset<=GalleryDikeInsertionRumble.MaximumPositionOffset+.0001f&&
                report.maximumCameraRotation<=GalleryDikeInsertionRumble.MaximumRotationDegrees+.0001f&&
                report.maximumRumbleToHandRatio>.70f&&report.maximumRumbleToHandRatio<.79f,
                "Actual camera pulse did not stay below the final-hand rumble strength. ratio="+report.maximumRumbleToHandRatio);
            report.insertionRumbleOnce=report.rumbleWeakerThanHands=true;
            Require(Vector3.Distance(game.view.transform.position,game.controller.transform.position+Vector3.up*1.55f)<.0001f,
                "Camera did not return to its unshaken base pose.");
            report.cameraReturnsToBase=true;
            Require(reward.Unlocked&&!course.Door.Barrier.enabled&&course.Door.Opening>.999f,"Sword insertion did not unlock and open the exit.");
            report.insertionProgress=reward.InsertionProgress;report.insertUnlocksDoor=true;
            report.leftDoorAngle=Quaternion.Angle(Quaternion.identity,course.Door.LeftLeaf.localRotation);
            report.rightDoorAngle=Quaternion.Angle(Quaternion.identity,course.Door.RightLeaf.localRotation);
            Require(report.leftDoorAngle>95&&report.rightDoorAngle>95&&Mathf.Abs(course.Door.Root.position.y)<.001f,
                "Wooden door leaves did not swing at fixed floor height.");
            report.woodenLeavesSwing=true;Capture("04-sword-inserted-door-open");
            Check("The enlarged left sword fits its pan; 1.5s insertion seats before one reduced camera pulse, a 0.4s delay and a 3s door swing, with pause restoration");

            Require(course.Sounds.Count("dike.glow")==1&&course.Sounds.Count("dike.sword.pickup")==2&&course.Sounds.Count("dike.sword.insert")==1&&
                course.Sounds.Count("dike.sword.seat")==1&&course.Sounds.Count("dike.socket.rumble")==1&&
                course.Sounds.Count("dike.door.open")==1&&course.Sounds.Count("dike.scale.motion")>0&&
                course.Sounds.Count("dike.glass.contact")>0,"Selected contact/scale/sword/door events did not play in the physical puzzle.");
            report.selectedSwordAudioSequence=true;
            report.g01GlowOnceAtBalance=true;
            report.phase="real circulation around central P02 and through the opened exit";
            game.yaw=0;game.pitch=0;game.SyncView();
            foreach(Vector3 target in new[]{
                new Vector3(1.3f,0,GalleryDikeGalleryLayout.ReceiverStation.z-2.2f),
                new Vector3(1.3f,0,GalleryDikeGalleryLayout.ReceiverStation.z+1.5f),
                new Vector3(0,0,GalleryBottleBalanceLayout.EndZ+.7f)})
            {
                int routeDeadline=fixedSteps+600;
                while(Vector2.Distance(new Vector2(game.player.feet.x,game.player.feet.z),new Vector2(target.x,target.z))>.16f)
                {
                    Require(fixedSteps<routeDeadline,"The actual controller could not walk around the central P02 to the exit.");
                    Vector3 delta=target-game.player.feet;move=new Vector2(delta.x,delta.z).normalized;
                    yield return new WaitForFixedUpdate();
                }
                move=Vector2.zero;
            }
            Require(game.player.feet.z>GalleryBottleBalanceLayout.EndZ+.45f,"The player did not physically pass into the next gallery.");
            report.centreReceiverCirculation=true;Capture("04b-central-pedestal-exit-circulation");
            report.phase="actual size-mapped bottle and law-book floor collisions";
            game.PlacePlayer(new Vector3(0,0,31));Look(new Vector3(0,1,34));
            foreach(var piece in new[]{course.Bottle,course.Sculpture})
            {
                bool bottle=piece==course.Bottle;
                foreach(float size in new[]{.8f,1.65f,2.6f})
                {
                    string cue=GalleryDikeAudio.ImpactCue(bottle,size);int beforeCount=course.Sounds.Count(cue);
                    piece.body.isKinematic=true;piece.body.useGravity=false;
                    piece.transform.localScale=Vector3.one*size;
                    piece.transform.SetPositionAndRotation(new Vector3(-2,3.2f,33),Quaternion.identity);
                    piece.body.position=piece.transform.position;piece.body.rotation=piece.transform.rotation;
                    piece.GetComponent<GalleryDikePropImpact>().ResetContact();Physics.SyncTransforms();piece.Release();
                    until=fixedSteps+300;
                    while(course.Sounds.Count(cue)==beforeCount&&fixedSteps<until)yield return new WaitForFixedUpdate();
                    Require(course.Sounds.Count(cue)>beforeCount,"Actual floor impact did not use selected size cue "+cue);
                    piece.body.isKinematic=true;piece.body.useGravity=false;
                    piece.transform.position=new Vector3(-7,2,42);piece.body.position=piece.transform.position;
                    Physics.SyncTransforms();
                }
            }
            report.sizeMappedFloorAudio=true;
            var finalAudioEvents=new List<string>();
            foreach(var stamp in course.Sounds.History)finalAudioEvents.Add(stamp.cue+" @ "+stamp.time.ToString("F3"));
            report.dikeAudioEvents=finalAudioEvents.ToArray();
            Check("Selected D02/D21 and physical sword events fire once at their actions; six real floor drops use the requested D04/D09/D14/D11 size mapping");

            report.phase="reset and missing-proof failure";
            course.ResetCourse();
            Require(!course.Extracted&&!course.Solved&&!reward.Unlocked&&!reward.Carrying&&!reward.SwordAvailable&&
                reward.Stage==GalleryBottleBalanceKeyReward.RewardStage.WaitingBalance&&reward.ReleaseCount==0&&
                reward.SwordBody.isKinematic&&!reward.SwordBody.useGravity&&course.Door.Barrier.enabled&&course.Door.Opening<.001f&&
                !course.InsertionRumble.Pulsing&&course.InsertionRumble.TriggerCount==0&&course.InsertionRumble.Intensity==0,
                "Reset did not restore the held statue sword and locked room.");
            report.resetRestores=true;
            var dummy=new GameObject("Validation only · unverified asset fails closed");dummy.transform.position=new Vector3(-99,-50,0);
            var dummyCourse=dummy.AddComponent<GalleryBottleBalanceCourse>();dummyCourse.game=game;
            var dummyArt=new GalleryBottleBalanceArt.View{SwordReleaseVerified=false,statueRenderers=Array.Empty<Renderer>()};
            dummyArt.rewardHandAnchor=Child(dummy.transform,"unverified hand");
            dummyArt.rewardCatchPad=Child(dummy.transform,"unverified recovery");
            dummyArt.swordSocketAnchor=Child(dummy.transform,"unverified slot");
            var unavailable=GalleryBottleBalanceKeyReward.Install(dummyCourse,dummy.transform,dummyArt,course.Door);
            unavailable.CompleteBalance();unavailable.Tick(1);
            Require(!unavailable.Available&&!unavailable.Unlocked&&unavailable.ReleaseCount==0&&
                unavailable.Stage==GalleryBottleBalanceKeyReward.RewardStage.AssetUnavailable,
                "An unverified asset was allowed to release/unlock.");
            Destroy(dummy);report.unverifiedAssetFailsClosed=true;
            Check("Reset restores the original sequence; missing independent-sword proof fails closed");
            report.phase="complete";
        }

        void AimAtSword()
        {
            Vector3 at=reward.SwordPhysicalCollider.bounds.center;
            // A real visitor can walk around a visible recovery tray. Test
            // three disclosed floor approaches rather than requiring the
            // statue/scale to be transparent from one exact front position.
            // Every accepted aim still goes through the production target
            // collider ray and its full reach/occlusion checks.
            Vector3[] offsets={new Vector3(0,0,-2.25f),new Vector3(2.3f,0,-.5f),new Vector3(1.85f,0,-2.1f)};
            string[] names={"front","right","front-right"};
            var shape=reward.SwordPhysicalCollider;
            Vector3[] targets={at,
                shape.transform.TransformPoint(shape.center+Vector3.up*shape.size.y*.35f),
                shape.transform.TransformPoint(shape.center-Vector3.up*shape.size.y*.30f)};
            for(int approach=0;approach<offsets.Length;approach++)
            {
                report.pickupApproachesTried++;
                Vector3 rawFeet=new Vector3(at.x+offsets[approach].x,0,at.z+offsets[approach].z);
                if(Mathf.Abs(rawFeet.x)>GalleryBottleBalanceLayout.HalfWidth-.4f)
                {pickupGroundDiagnostics.Add(names[approach]+": outside room");continue;}
                bool canStandAtZero=game.CanStand(rawFeet);
                Vector3 feet;string ground;
                if(!TryGalleryFloorFeet(rawFeet,out feet,out ground))
                {pickupGroundDiagnostics.Add(names[approach]+": no named low gallery floor; zeroStand="+canStandAtZero);continue;}
                bool canStandOnFloor=game.CanStand(feet);
                pickupGroundDiagnostics.Add(names[approach]+": "+ground+"; zeroStand="+canStandAtZero+
                    "; adjustedFeet="+feet.ToString("F3")+"; adjustedStand="+canStandOnFloor+
                    (canStandOnFloor?"":"; blockers="+StandingBlockers(feet)));
                if(!canStandOnFloor)continue;
                game.PlacePlayer(feet);
                foreach(Vector3 target in targets)
                {
                    Look(target);reward.RefreshAim(game.view);
                    if(!reward.TargetedKey)continue;
                    report.pickupClearViews++;report.pickupApproach=names[approach];return;
                }
            }
            Require(false,"No clear reachable floor pickup ray from front, right or front-right approach. " +
                "Sword remains occluded or outside production reach; no pickup through a pan/wall is allowed. Ground diagnostics: "+
                string.Join(" | ",pickupGroundDiagnostics));
        }
        bool TryGalleryFloorFeet(Vector3 candidate,out Vector3 feet,out string ground)
        {
            feet=candidate;ground=null;float best=float.NegativeInfinity;
            // Only ordinary floor surfaces close to room ground qualify. A
            // plinth, pan, recovery tray or pedestal must never become a
            // convenient raised pickup fixture.
            foreach(var hit in Physics.RaycastAll(new Vector3(candidate.x,1.2f,candidate.z),Vector3.down,1.6f,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider==null||hit.normal.y<.95f||hit.point.y<-.08f||hit.point.y>.12f)continue;
                string name=hit.collider.name;
                if(name.IndexOf("floor",StringComparison.OrdinalIgnoreCase)<0&&name.IndexOf("바닥",StringComparison.Ordinal)<0)continue;
                if(hit.point.y<=best)continue;
                best=hit.point.y;feet=new Vector3(candidate.x,hit.point.y+.03f,candidate.z);
                ground=name+" y="+hit.point.y.ToString("F4")+" normalY="+hit.normal.y.ToString("F3");
            }
            return ground!=null;
        }
        string StandingBlockers(Vector3 feet)
        {
            var controller=game.controller;var names=new List<string>();
            Vector3 half=Vector3.up*(controller.height*.5f-controller.radius);
            foreach(var other in Physics.OverlapCapsule(feet+controller.center-half,feet+controller.center+half,
                controller.radius-.012f,~0,QueryTriggerInteraction.Ignore))
            {
                if(other==controller||!PlacementSupport.IsUsableGround(other))continue;
                names.Add(other.name);if(names.Count>=6)break;
            }
            return string.Join(",",names);
        }
        void Pose(GalleryProp prop,Vector3 at,Quaternion rotation,float scale)
        {
            PropReleasePhysics.Attach(prop.body).CancelPendingRelease();HeldCollisionState.Attach(prop.body).Restore();
            if(!prop.body.isKinematic){prop.body.linearVelocity=Vector3.zero;prop.body.angularVelocity=Vector3.zero;}
            prop.body.isKinematic=true;prop.body.useGravity=false;
            prop.transform.SetPositionAndRotation(at,rotation);prop.transform.localScale=Vector3.one*scale;
            prop.body.position=at;prop.body.rotation=rotation;prop.RefreshMass();Physics.SyncTransforms();prop.placement.ResetPlaced();
            if(prop==course.Bottle&&!course.Extracted)course.Contents.Reset();
        }
        void PoseOnPan(GalleryProp prop,BoxCollider pan,float scale)
        {
            Bounds b=pan.bounds;Vector3 top=new Vector3(b.center.x,b.max.y,b.center.z);
            Pose(prop,top+Vector3.up,Quaternion.identity,scale);Bounds actual=prop.WorldBounds;
            prop.transform.position+=top-new Vector3(actual.center.x,actual.min.y,actual.center.z)+Vector3.up*.008f;
            prop.body.position=prop.transform.position;Physics.SyncTransforms();prop.placement.ResetPlaced();
        }
        void Look(Vector3 at)
        { Vector3 d=at-game.view.transform.position;game.yaw=Mathf.Atan2(d.x,d.z);game.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);game.SyncView(); }
        void CheckBookSize()
        {
            for(int i=0;i<bookRenderers.Length;i++)report.maximumBookMeshSizeError=Mathf.Max(report.maximumBookMeshSizeError,
                Vector3.Distance(IntrinsicSize(bookRenderers[i]),bookSizes[i])/Mathf.Max(.001f,bookSizes[i].magnitude));
            Require(report.maximumBookMeshSizeError<.015f,"Resizing the bottle resized the actual law-book mesh.");
        }
        static Vector3 IntrinsicSize(Renderer r)
        {
            var filter=r.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)return Vector3.zero;
            Vector3 s=r.transform.lossyScale;return Vector3.Scale(filter.sharedMesh.bounds.size,new Vector3(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z)));
        }
        static Bounds LocalBounds(Transform root)
        {
            bool first=true;Bounds result=new Bounds();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null)
            {
                Bounds b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 q=root.InverseTransformPoint(filter.transform.TransformPoint(p));
                    if(first){first=false;result=new Bounds(q,Vector3.zero);}else result.Encapsulate(q);
                }
            }
            return result;
        }
        static Transform Child(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        void PoseRewardView()
        {
            // Keep the actual lowered hand, blade and recovery tray in frame.
            // The extraction phase previously left the camera aimed upward.
            Vector3 hand=course.Art.rewardHandAnchor.position;
            Vector3 pad=course.Art.rewardCatchPad.position;
            Vector3 feet;string floor;
            Vector3 candidate=new Vector3(hand.x+2.25f,0,hand.z-2.55f);
            if(!TryGalleryFloorFeet(candidate,out feet,out floor))feet=candidate+Vector3.up*.03f;
            game.PlacePlayer(feet);
            game.view.fieldOfView=55;
            Look(Vector3.Lerp(hand,pad,.42f));
        }
        void CaptureSwordCloseup(string name)
        {
            Vector3 oldFeet=game.player.feet;float oldYaw=game.yaw,oldPitch=game.pitch,oldFov=game.view.fieldOfView;
            try
            {
                bool first=true;Bounds bounds=new Bounds();
                foreach(var renderer in reward.SwordTransform.GetComponentsInChildren<Renderer>())
                    if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
                if(first)return;
                Vector3 candidate=new Vector3(bounds.center.x+2.45f,0,bounds.center.z-.8f);
                Vector3 feet;string floor;
                if(!TryGalleryFloorFeet(candidate,out feet,out floor))feet=candidate+Vector3.up*.03f;
                game.PlacePlayer(feet);game.view.fieldOfView=38;Look(bounds.center);Capture(name);
            }
            finally
            {
                // PlacePlayer applies its own .025m skin lift.
                game.PlacePlayer(oldFeet-Vector3.up*.025f);game.yaw=oldYaw;game.pitch=oldPitch;
                game.view.fieldOfView=oldFov;game.SyncView();
            }
        }
        void Capture(string name)
        {
            if(game==null||game.view==null||output==null)return;
            Camera camera=game.view;RenderTexture prior=camera.targetTexture,active=RenderTexture.active;
            var rt=new RenderTexture(1600,900,24);Texture2D image=null;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(1600,900,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;rt.Release();Destroy(rt);if(image!=null)Destroy(image);}
        }
        void OnBalance(){balances++;}void OnRelease(){releases++;}
        void Check(string text){checks.Add(text);Debug.Log("DIKE_SWORD_PASS "+text);}
        void Guard(){Require(Time.realtimeSinceStartupAsDouble<=deadline,"Validation exceeded 240 seconds.");}
        static void Require(bool condition,string text){if(!condition)throw new InvalidOperationException("DIKE_SWORD_VALIDATION: "+text);}
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Restore()
        {
            Application.logMessageReceived-=Log;
            if(course!=null)course.BalanceCompleted-=OnBalance;if(reward!=null)reward.SwordReleased-=OnRelease;
            if(configured){Time.fixedDeltaTime=oldFixedDelta;Time.timeScale=oldTimeScale;}
        }
        void OnDestroy(){drive=false;Restore();}
    }
}
