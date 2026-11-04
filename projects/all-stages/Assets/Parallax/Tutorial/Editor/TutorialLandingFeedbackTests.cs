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
                Require(impacts == 3 && lastPitch <= .45f, "Maximum impact exceeded the pitch limit");

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
                Call(shake, "Play", .45f, .04f);
                Require(Sample(.064f) > .40f && Sample(.064f) <= .45f, "Overlapping impacts accumulated or failed to replace the earlier peak");
                Require(Sample(.13f) < Sample(.064f) && Sample(.22f) == 0f, "Pitch did not decay to baseline");

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
                VerifyPhysicalContacts(game);
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
