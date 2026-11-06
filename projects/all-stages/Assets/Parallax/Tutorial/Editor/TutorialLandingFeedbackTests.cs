using System;
using System.Reflection;
using ShadeLink.Gallery;
using ShadeLink.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class TutorialLandingFeedbackTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const string PhysicsRunKey = "Parallax.TutorialLandingFeedback.RunPhysics";

        public static void Run()
        {
            var die = new GameObject("Landing feedback test die");
            var cameraObject = new GameObject("Landing feedback test camera");
            object shake = null;
            try
            {
                var feedback = die.AddComponent<TutorialLandingFeedback>();
                int impacts = 0;
                float lastPitch = 0f;
                Call(feedback, "Configure", (Action<float>)(pitch => { impacts++; lastPitch = pitch; }));

                void Release(bool placed, float size) => Call(feedback, "RecordPlacementResult", placed, size);
                void Contact(Vector3 normal, Vector3 velocity)
                {
                    float speed = (float)typeof(TutorialLandingFeedback).GetMethod("SupportSpeed", Static).Invoke(null,
                        new object[] { normal, velocity });
                    Call(feedback, "ConsumeSupport", speed);
                }
                void Land(float speed) => Contact(Vector3.up, Vector3.down * speed);

                Release(false, 10f);
                Land(4f);
                Require(impacts == 0, "A rejected drop armed the impact observer");

                Release(true, .36f);
                Contact(Vector3.right, Vector3.left * 5f);
                Require(impacts == 0, "Side contact produced an impact");
                Land(2f);
                Require(impacts == 1, "Floor contact after a wall scrape did not produce an impact");
                float smallPitch = lastPitch;
                Land(4f);
                Require(impacts == 1, "A bounce produced a second impact");

                Release(true, 9.5f);
                Contact(Vector3.up, new Vector3(8f, -.2f, 0));
                Land(4f);
                Require(impacts == 1, "Gentle contact was not consumed or tangential speed counted as impact");

                Release(true, 9.5f);
                Land(2f);
                Require(impacts == 2 && lastPitch > smallPitch, "A larger die did not shake more at the same impact speed");
                Release(true, 40f);
                Land(20f);
                Require(impacts == 3 && lastPitch <= 1.45f, "Maximum impact exceeded the pitch limit");

                Release(true, 9.5f);
                Call(feedback, "ClearPending");
                Land(4f);
                Require(impacts == 3, "A pickup or reset left a pending impact");

                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(3f, 4f, 5f), Quaternion.Euler(7f, 35f, 0f));
                Vector3 position = camera.transform.position;
                Quaternion rotation = camera.transform.rotation;
                Matrix4x4 baseline = camera.worldToCameraMatrix;
                var shakeType = typeof(TutorialLandingFeedback).Assembly.GetType("ShadeLink.Tutorial.TutorialLandingShake", true);
                shake = Activator.CreateInstance(shakeType, Instance, null, new object[] { camera }, null);

                float Sample(float time) => (float)Call(shake, "Sample", time);
                Call(shake, "Play", .25f, 0f);
                Require(Sample(.024f) > .2f, "The first impact did not reach its expected peak");
                Call(shake, "Play", .10f, .024f);
                Require(Sample(.025f) > .2f, "A weaker overlapping impact replaced a stronger one");
                Call(shake, "Play", 3f, .04f);
                Require(Sample(.064f) > 1.44f && Sample(.064f) <= 1.45f, "Stronger impacts did not reach the new cap or overlapping impacts accumulated");
                Require(Sample(.13f) < Sample(.064f) && Sample(.28f) == 0f, "Pitch did not decay to baseline");

                Call(shake, "ApplyView", .064f);
                Matrix4x4 expected = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
                    Matrix4x4.TRS(position, rotation * Quaternion.Euler(Sample(.064f), 0f, 0f), Vector3.one).inverse;
                Require(Matches(camera.worldToCameraMatrix, expected) &&
                    camera.transform.position == position && Quaternion.Angle(camera.transform.rotation, rotation) < .001f,
                    "Render offset changed the gameplay pose or did not pitch the view downward");
                Call(shake, "RestoreView");
                Require(Matches(camera.worldToCameraMatrix, baseline), "View matrix was not restored after rendering");
                Call(shake, "ApplyView", .064f);
                Call(shake, "Clear");
                Require(Sample(.064f) == 0f && Matches(camera.worldToCameraMatrix, baseline), "Clearing shake left an active offset");

                Debug.Log("TUTORIAL_LANDING_FEEDBACK_OK contact, first landing, release/reset, intensity, overlap and render restoration");
            }
            finally
            {
                (shake as IDisposable)?.Dispose();
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(die);
            }
        }

        // Run this entry point without -quit so the editor can enter Play Mode.
        public static void RunPhysics()
        {
            EditorSceneManager.OpenScene("Assets/Parallax/Tutorial/Scenes/Tutorial.unity");
            SessionState.SetBool(PhysicsRunKey, true);
            ContinuePhysicsRun();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void ContinuePhysicsRun()
        {
            if (!SessionState.GetBool(PhysicsRunKey, false)) return;
            EditorApplication.update -= VerifyPhysicsOnPlay;
            EditorApplication.update += VerifyPhysicsOnPlay;
        }

        static void VerifyPhysicsOnPlay()
        {
            if (!EditorApplication.isPlaying) return;
            var game = UnityEngine.Object.FindFirstObjectByType<TutorialGame>();
            if (game == null || game.Camera == null) return;

            EditorApplication.update -= VerifyPhysicsOnPlay;
            SessionState.EraseBool(PhysicsRunKey);
            SimulationMode previousMode = Physics.simulationMode;
            int status = 0;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Require(Mathf.Abs(game.Controller.transform.position.x) < .01f &&
                    Mathf.Abs(game.Camera.transform.forward.x) < .01f,
                    "Tutorial must start at the centre looking straight down the room");
                VerifyPhysicalContacts(game);
                VerifyClimbArrival(game);
                VerifyStageMenu(game);
                Debug.Log("TUTORIAL_LANDING_PHYSICS_OK wall scrape, floor impacts, quiet support, bounce and pause");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                status = 1;
            }
            finally
            {
                Physics.simulationMode = previousMode;
                EditorApplication.Exit(status);
            }
        }

        static void VerifyPhysicalContacts(TutorialGame game)
        {
            Vector3 area = new Vector3(300f, 0f, 300f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = area + Vector3.down * .5f;
            floor.transform.localScale = new Vector3(18f, 1f, 12f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = area + new Vector3(3.5f, 3f, 0f);
            wall.transform.localScale = new Vector3(1f, 6f, 12f);
            var die = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var body = die.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var feedback = die.AddComponent<TutorialLandingFeedback>();
            int impacts = 0;
            Call(feedback, "Configure", (Action<float>)(_ => impacts++));

            void Simulate(int steps)
            {
                for (int i = 0; i < steps; i++) Physics.Simulate(.01f);
            }

            void Place(Rigidbody prop, Vector3 position, Vector3 velocity)
            {
                prop.isKinematic = false;
                prop.useGravity = true;
                prop.position = area + position;
                prop.rotation = Quaternion.identity;
                prop.linearVelocity = velocity;
                prop.angularVelocity = Vector3.zero;
                prop.WakeUp();
                Physics.SyncTransforms();
            }

            Call(feedback, "RecordPlacementResult", true, 1f);
            Place(body, new Vector3(1f, 3f, 0f), Vector3.right * 8f);
            Simulate(28);
            Require(body.position.x >= area.x + 2.45f && impacts == 0, "A real wall scrape counted as a landing");
            Simulate(100);
            Require(impacts == 1, "First floor contact did not shake after a real wall scrape");
            Place(body, new Vector3(0f, 2f, 0f), Vector3.down * 2f);
            Simulate(100);
            Require(impacts == 1, "Recontact without another release shook again");

            Call(feedback, "RecordPlacementResult", true, 1f);
            Place(body, new Vector3(0f, .502f, 0f), Vector3.zero);
            Simulate(40);
            Place(body, new Vector3(0f, 2f, 0f), Vector3.down * 2f);
            Simulate(100);
            Require(impacts == 1, "Gentle floor contact shook or left the first landing pending");

            var gameDie = game.Props[0];
            var gameBody = gameDie.GetComponent<Rigidbody>();
            var gameFeedback = gameDie.GetComponent<TutorialLandingFeedback>();
            var gameShake = typeof(TutorialGame).GetField("landingShake", Instance).GetValue(game);
            Call(game, "SetPaused", true);
            Call(gameFeedback, "RecordPlacementResult", true, gameDie.transform.localScale.x);
            Place(gameBody, new Vector3(-2f, 2f, 0f), Vector3.down * 3f);
            Simulate(100);
            Call(game, "SetPaused", false);
            Place(gameBody, new Vector3(-2f, 2f, 0f), Vector3.down * 3f);
            Simulate(100);
            Require((float)Call(gameShake, "Sample", Time.unscaledTime + .024f) == 0f,
                "A contact during pause triggered feedback after resume");

            Call(gameFeedback, "RecordPlacementResult", true, gameDie.transform.localScale.x);
            Place(gameBody, new Vector3(-2f, 2f, 0f), Vector3.down * 3f);
            Simulate(100);
            Require((float)Call(gameShake, "Sample", Time.unscaledTime + .024f) > 0f,
                "Tutorial die's normal landing did not reach the camera");
            Call(game, "RestartLesson");
            Require((float)Call(gameShake, "Sample", Time.unscaledTime + .024f) == 0f,
                "Restart left the camera shake active");

            void Aim(Vector3 point)
            {
                game.Camera.transform.rotation = Quaternion.LookRotation(point - game.Camera.transform.position);
                Physics.SyncTransforms();
            }

            Aim(gameDie.transform.position);
            Call(game, "InteractWithLandingFeedback");
            Require(game.Held == gameDie, "Tutorial input path did not pick up the practice die");
            var interaction = (ObjectInteraction<GalleryProp>)typeof(TutorialGame).GetField("interaction", Instance).GetValue(game);
            interaction.Advance(.8f);
            Aim((Vector3)typeof(TutorialGame).GetProperty("AimPoint", Instance).GetValue(game));
            Call(game, "InteractWithLandingFeedback");
            Require(game.Held == null && (float)Call(gameShake, "Sample", Time.unscaledTime + .024f) == 0f,
                "The placement input itself produced shake or failed to release");
            Simulate(100);
            Require((float)Call(gameShake, "Sample", Time.unscaledTime + .024f) > 0f,
                "A normally placed die did not shake when it reached the floor");
        }

        static void VerifyClimbArrival(TutorialGame game)
        {
            var dice = (GalleryProp[])typeof(TutorialGame).GetField("climbDice", Instance).GetValue(game);
            Require(dice.Length == 2, "Stage two needs exactly two arrival dice");
            Require(dice[0].homeScale.x >= 1f && dice[1].homeScale.x >= dice[0].homeScale.x * 1.5f,
                "Arrival needs one medium die and one visibly larger die");

            var room = (Bounds)typeof(TutorialGame).GetField("ClimbRoom", Static).GetValue(null);
            bool Suspended(GalleryProp die) => die.gameObject.activeSelf && die.body.isKinematic &&
                die.WorldBounds.max.y > room.max.y - .3f && die.WorldBounds.max.y < room.max.y;
            Require(Array.TrueForAll(dice, Suspended),
                "Both dice must already exist just below the ceiling before stage two is entered");
            foreach (var die in dice)
                Require(Mathf.Abs(die.homePosition.x - room.center.x) < room.extents.x * .3f &&
                    Mathf.Abs(die.homePosition.z - room.center.z) < room.extents.z * .5f,
                    "Arrival dice should land near the middle of stage two");
            Require(dice[0].homePosition.z > room.center.z && dice[1].homePosition.z < room.center.z,
                "Medium and large dice should arrive on the player's left and right");
            Require(Mathf.Abs(dice[0].homePosition.x - dice[1].homePosition.x) > 3f &&
                Quaternion.Angle(dice[0].homeRotation, dice[1].homeRotation) > 20f,
                "Arrival dice need staggered depths and different diagonal rotations");

            typeof(TutorialGame).GetField("exitUnlocked", Instance).SetValue(game, true);
            game.Controller.enabled = false;
            game.Controller.transform.position = new Vector3(room.min.x + .7f, .03f, room.center.z);
            game.Controller.enabled = true;
            typeof(TutorialGame).GetField("yaw", Instance).SetValue(game, 1.35f);
            typeof(TutorialGame).GetField("pitch", Instance).SetValue(game, -.2f);
            Call(game, "SyncView");
            var entryViewPosition = game.Camera.transform.position;
            var entryViewRotation = game.Camera.transform.rotation;
            Call(game, "UpdateExit", .02f);
            bool entryViewUnchanged = Vector3.Distance(game.Camera.transform.position, entryViewPosition) < .001f &&
                Quaternion.Angle(game.Camera.transform.rotation, entryViewRotation) < .001f;
            Require(game.Props == dice && !game.CanInteract, "Walking into stage two must begin the arrival demonstration");
            float fieldOfView = game.Camera.fieldOfView;
            game.Camera.fieldOfView = 90f;
            var planes = GeometryUtility.CalculateFrustumPlanes(game.Camera);
            Require(Array.TrueForAll(dice, die => !GeometryUtility.TestPlanesAABB(planes, die.WorldBounds)),
                "Ceiling dice should enter the initial view by falling, even at the widest supported field of view");
            game.Camera.fieldOfView = fieldOfView;

            void Simulate(int steps)
            {
                for (int i = 0; i < steps; i++)
                {
                    Call(game, "FixedUpdate");
                    Physics.Simulate(.02f);
                    Physics.SyncTransforms();
                }
            }

            Call(game, "SetPaused", true);
            Simulate(100);
            Require(Array.TrueForAll(dice, Suspended), "Pause released an arrival die");
            Call(game, "SetPaused", false);
            for (int i = 0; i < 100 && dice[0].body.isKinematic; i++) Simulate(1);
            Require(!dice[0].body.isKinematic && dice[0].WorldBounds.max.y > room.max.y - .5f &&
                Suspended(dice[1]), "The medium die must start falling from the ceiling while the large die waits");

            Call(game, "RestartLesson");
            Require(Array.TrueForAll(dice, Suspended),
                "Restarting during arrival must suspend both dice at the ceiling");
            int[] impacts = new int[2];
            float[] strength = new float[2];
            float[] impactTime = { -1f, -1f };
            float elapsed = 0;
            for (int i = 0; i < dice.Length; i++)
            {
                int index = i;
                Call(dice[i].GetComponent<TutorialLandingFeedback>(), "Configure", (Action<float>)(pitch =>
                {
                    impacts[index]++;
                    strength[index] = pitch;
                    impactTime[index] = elapsed;
                    Call(game, "PlayLandingShake", pitch);
                }));
            }
            bool sawSecondFall = false;
            float secondReleaseTime = -1f;
            for (int tick = 0; tick < 500; tick++)
            {
                Simulate(1);
                elapsed += .02f;
                Call(game, "UpdateExit", .02f);
                Require(Array.TrueForAll(dice, die => die.gameObject.activeSelf),
                    "The demonstration activated or hid an arrival die in midair");
                if (!dice[1].body.isKinematic && dice[1].body.position.y > 5f)
                {
                    if (!sawSecondFall) secondReleaseTime = elapsed;
                    sawSecondFall = true;
                    Require(impacts[0] == 1, "The large die fell before the medium die's first landing");
                }
            }
            float releaseGap = secondReleaseTime - impactTime[0];
            Debug.Log("TUTORIAL_TRANSITION_DIAGNOSTIC viewPreserved=" + entryViewUnchanged + " releaseGap=" + releaseGap);
            Require(entryViewUnchanged, "Entering stage two reset the player's camera position or look direction");
            Require(releaseGap >= .18f && releaseGap <= .26f,
                "The large die should start falling about 0.2 seconds after the first landing, got " + releaseGap);
            Require(sawSecondFall && impacts[0] == 1 && impacts[1] == 1 &&
                impactTime[1] - impactTime[0] > 1f && strength[1] > strength[0],
                "Sequential real floor impacts must shake once each, with a stronger second impact");
            Require(strength[0] >= .65f && strength[1] >= .9f,
                "Actual medium and large landings need a more noticeable shake, got " + strength[0] + " and " + strength[1]);
            Require(game.CanInteract && Array.TrueForAll(dice, die => die.placement.Ready),
                "Both landed dice must become usable for the climbing puzzle");
            var mediumAudio = dice[0].GetComponent<AudioSource>();
            var largeAudio = dice[1].GetComponent<AudioSource>();
            Require(mediumAudio != null && largeAudio != null && largeAudio.pitch < mediumAudio.pitch &&
                largeAudio.volume > mediumAudio.volume && mediumAudio.minDistance >= 5f,
                "The real contact audio must be audible from the entrance and deeper and louder for the large die");

            var interaction = (ObjectInteraction<GalleryProp>)typeof(TutorialGame).GetField("interaction", Instance).GetValue(game);
            var direction = dice[0].transform.position - game.Camera.transform.position;
            typeof(TutorialGame).GetField("yaw", Instance).SetValue(game, Mathf.Atan2(direction.x, direction.z));
            typeof(TutorialGame).GetField("pitch", Instance).SetValue(game,
                Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude));
            Call(game, "SyncView");
            interaction.UpdateAim();
            Call(game, "InteractWithLandingFeedback");
            Require(game.Held == dice[0], "A landed arrival die could not be picked up normally");
            var restartViewRotation = game.Camera.transform.rotation;
            Call(game, "RestartLesson");
            Require(game.Held == null && Array.TrueForAll(dice, Suspended),
                "Restart after pickup must replay the arrival with both original dice");
            Require(Quaternion.Angle(game.Camera.transform.rotation, restartViewRotation) < .001f,
                "Restarting the drop demonstration reset the player's look direction");
            Debug.Log("TUTORIAL_CLIMB_ARRIVAL_OK ceiling drops without pop-in, staggered diagonal poses, different feedback, pause, restart and pickup");
        }

        static void VerifyStageMenu(TutorialGame game)
        {
            Require(typeof(TutorialGame).GetMethod("StartTutorialStage", Instance) != null,
                "The pause menu needs a restart and direct stage selection action");
            var practice = (GalleryProp)typeof(TutorialGame).GetField("die", Instance).GetValue(game);
            var dice = (GalleryProp[])typeof(TutorialGame).GetField("climbDice", Instance).GetValue(game);
            var tableHome = (Vector3)typeof(TutorialGame).GetField("TableHome", Static).GetValue(null);
            var playerStart = (Vector3)typeof(TutorialGame).GetField("PlayerStart", Static).GetValue(null);
            var climbSpawn = (Vector3)typeof(TutorialGame).GetField("ClimbSpawn", Static).GetValue(null);
            var shake = typeof(TutorialGame).GetField("landingShake", Instance).GetValue(game);
            var pendingLanding = typeof(TutorialLandingFeedback).GetProperty("AwaitingLanding", Instance);

            // Simulate a saved lesson checkpoint before choosing "start over".
            practice.homePosition = tableHome + Vector3.forward * 6f;
            practice.homeScale = Vector3.one * 3f;
            typeof(TutorialGame).GetField("step", Instance).SetValue(game, 2);
            Call(game, "SetPaused", true);
            Call(game, "StartTutorialStage", 1);
            Require(game.CanInteract && game.Held == null && game.Aimed == null &&
                game.Props.Length == 1 && game.Props[0] == practice &&
                !(bool)typeof(TutorialGame).GetField("inClimbRoom", Instance).GetValue(game) &&
                (int)typeof(TutorialGame).GetField("step", Instance).GetValue(game) == 0,
                "Starting over did not return to the first lesson with its own tool");
            Require(Vector3.Distance(game.Controller.transform.position, playerStart) < .01f &&
                Vector3.Distance(practice.transform.position, tableHome) < .01f &&
                Mathf.Abs(practice.transform.localScale.x - .36f) < .001f &&
                !(bool)typeof(TutorialGame).GetField("exitUnlocked", Instance).GetValue(game),
                "Starting over retained the previous checkpoint's size, position or unlocked door");

            var interaction = (ObjectInteraction<GalleryProp>)typeof(TutorialGame).GetField("interaction", Instance).GetValue(game);
            game.Camera.transform.LookAt(practice.transform.position);
            interaction.UpdateAim();
            Call(game, "InteractWithLandingFeedback");
            Require(game.Held == practice, "The first-stage menu fixture could not pick up its practice die");
            Call(game, "SetPaused", true);
            Call(game, "StartTutorialStage", 2);
            Require(game.Held == null && !practice.placement.Held && game.Aimed == null &&
                game.Props == dice && !game.CanInteract &&
                (bool)typeof(TutorialGame).GetField("inClimbRoom", Instance).GetValue(game) &&
                (bool)typeof(TutorialGame).GetField("climbArrivalPlaying", Instance).GetValue(game) &&
                Vector3.Distance(game.Controller.transform.position, climbSpawn) < .01f &&
                game.Camera.transform.forward.x > .99f,
                "Direct stage-two selection did not cancel pickup, position the player and start the drop demonstration");

            for (int i = 0; i < 30; i++)
            {
                Call(game, "FixedUpdate");
                Physics.Simulate(.02f);
                Physics.SyncTransforms();
            }
            Require(!dice[0].body.isKinematic && dice[1].body.isKinematic,
                "Stage-two menu selection did not release the first die");
            Call(game, "SetPaused", true);
            Call(game, "StartTutorialStage", 1);
            Require(game.CanInteract && Array.TrueForAll(dice, die => die.body.isKinematic &&
                !(bool)pendingLanding.GetValue(die.GetComponent<TutorialLandingFeedback>())) &&
                !(bool)typeof(TutorialGame).GetField("climbArrivalPlaying", Instance).GetValue(game) &&
                (float)Call(shake, "Sample", Time.unscaledTime + .024f) == 0f,
                "Returning to stage one left a pending drop, camera shake or paused input");
            Call(game, "SetPaused", true);
            Call(game, "StartTutorialStage", 2);
            Require(Array.TrueForAll(dice, die => die.body.isKinematic) &&
                (int)typeof(TutorialGame).GetField("nextClimbDrop", Instance).GetValue(game) == 0 &&
                !(bool)typeof(TutorialGame).GetField("paused", Instance).GetValue(game),
                "Repeated stage selection did not restart the arrival sequence");
            Debug.Log("TUTORIAL_STAGE_MENU_OK full restart, direct stage selection, held-prop cleanup and interrupted drop reset");
        }

        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Instance).Invoke(target, args);

        static bool Matches(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(a[i] - b[i]) > .0001f) return false;
            return true;
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
