using System;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Run from the museum audio validation entry point after importing the
    // selected clips. These checks do not depend on an audible output device.
    public static class MuseumSelectedAudioMovementTests
    {
        public static void Run()
        {
            RunTiming();
            RunRouting();
        }

        public static void RunTiming()
        {
            var player = new GameObject("Museum audio movement timing fixture");
            try
            {
                var movement = player.AddComponent<PlayerMovementAudio>();
                Require(movement.Tick(Vector3.forward * 4, true, true, .42f) == PlayerMovementAudio.Cue.None,
                    "Walking interval was consumed before takeoff");
                Require(movement.Tick(Vector3.up * 6, false, true, .02f) == PlayerMovementAudio.Cue.Jump,
                    "A step at the interval boundary displaced the takeoff cue");
                Require(movement.Tick(Vector3.forward * 4 + Vector3.down * 8, false, true, .2f) == PlayerMovementAudio.Cue.None,
                    "Walking input in the air produced a step");
                Require(movement.Tick(Vector3.forward * 4, true, true, .02f) == PlayerMovementAudio.Cue.Land,
                    "Contact with the ground did not own the landing frame");
                Require(movement.Tick(Vector3.forward * 4, true, true, .4f) == PlayerMovementAudio.Cue.None,
                    "Landing produced an immediate duplicate footstep");
                Require(movement.Tick(Vector3.forward * 4, true, true, .03f) == PlayerMovementAudio.Cue.Footstep,
                    "Walking cadence did not recover after landing");

                movement.Reset();
                Require(movement.Tick(Vector3.up * 6, false, true, 0) == PlayerMovementAudio.Cue.None,
                    "A paused frame produced a takeoff cue");
                Require(movement.Tick(Vector3.up * 6, false, true, .01f) == PlayerMovementAudio.Cue.Jump,
                    "The paused frame swallowed the following real takeoff");
                movement.Tick(Vector3.down * 8, false, false, .1f);
                movement.Reset();
                Require(movement.Tick(Vector3.zero, true, false, .01f) == PlayerMovementAudio.Cue.None,
                    "Reset/teleport incorrectly produced a landing");
                Debug.Log("MUSEUM_SELECTED_AUDIO_MOVEMENT_TIMING_OK 9");
            }
            finally { UnityEngine.Object.DestroyImmediate(player); }
        }

        public static void RunRouting()
        {
            var museum = new GameObject("Museum selected-audio routing fixture");
            var outside = new GameObject("Other stage prop fixture");
            try
            {
                var listener = new GameObject("Museum listener");
                listener.transform.SetParent(museum.transform, false);
                var player = new GameObject("Museum player");
                player.transform.SetParent(museum.transform, false);
                var controller = player.AddComponent<CharacterController>();
                var movement = PlayerMovementAudio.Attach(controller);
                var prop = new GameObject("Wood museum prop");
                prop.transform.SetParent(museum.transform, false);
                prop.AddComponent<BoxCollider>();
                var body = prop.AddComponent<Rigidbody>();
                body.mass = 8;
                var physics = PropReleasePhysics.Attach(body);
                physics.InitializeProfile(false, 8);
                outside.AddComponent<BoxCollider>();
                var outsideBody = outside.AddComponent<Rigidbody>();

                MuseumAudio.Install(museum, listener.transform);
                Require(MuseumAudio.TryPlayPropGrab(body), "Owned museum prop was not routed to selected audio");
                Require(!MuseumAudio.TryPlayPropGrab(outsideBody), "Museum audio captured another stage's prop");
                Require(MuseumAudio.TryPlayMovement(PlayerMovementAudio.Cue.Jump, 0, player.transform.position),
                    "Unselected museum takeoff must be deliberately silent, not synthetic fallback");
                movement.Tick(Vector3.forward * 4, true, true, .44f);
                movement.Tick(Vector3.up * 6, false, true, .01f);
                movement.Tick(Vector3.down * 8, false, false, .2f);
                movement.Tick(Vector3.zero, true, false, .01f);
                Require(player.GetComponents<AudioSource>().Length == 0,
                    "Selected movement reused the legacy mutable-pitch source");

                physics.BeginHold();
                physics.Release();
                Require(body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic,
                    "Audio routing changed release CCD policy");
                Require(!physics.PlayImpact(.249f), "Tiny contact produced an impact cue");
                Require(physics.PlayImpact(.25f), "Real contact was dropped by selected-audio routing");
                Require(!physics.PlayImpact(4), "Selected-audio contact bypassed the impact cooldown");
                physics.Tick(.11f);
                Require(physics.PlayImpact(4), "Selected-audio contact did not recover after cooldown");
                physics.BeginHold();
                Require(prop.GetComponents<AudioSource>().Length == 0,
                    "A re-grab could stop the selected impact through a local legacy source");

                var legacy = PropReleasePhysics.Attach(outsideBody);
                legacy.InitializeProfile(false, 8);
                legacy.BeginHold();
                Require(outside.GetComponents<AudioSource>().Length == 1,
                    "Other stage no longer has its original fallback audio");
                Debug.Log("MUSEUM_SELECTED_AUDIO_MOVEMENT_ROUTING_OK 11");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(museum);
                UnityEngine.Object.DestroyImmediate(outside);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("MUSEUM_SELECTED_AUDIO_MOVEMENT: " + message);
        }
    }
}
