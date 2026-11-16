using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ShadeLink.Tutorial
{
    // Opt-in native regression helper. Disclosed poses are setup fixtures;
    // CharacterController.Move then reaches the real source-mesh walls. The
    // ordinary automatic ride update supplies every door phase and dwell.
    public static class TutorialElevatorBoardingValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly string[] ContactNames = { "left wall", "right wall", "rear wall", "rear-left corner", "rear-right corner" };
        static readonly Vector3[] ContactDirections = {
            Vector3.left, Vector3.right, Vector3.back,
            new Vector3(-1, 0, -1), new Vector3(1, 0, -1)
        };

        public static IEnumerator Run(TutorialGame tutorial, Action<bool, string, string> check)
        {
            check(tutorial != null && tutorial.Controller != null,
                "Boarding regression uses the actual saved-scene controller", "No replacement controller or trigger fixture");
            bool originalEnabled = tutorial.enabled;
            TutorialElevatorRide ride = null;
            int departures = 0;
            Action<TutorialElevatorRide> observeDeparture = _ => departures++;
            try
            {
                Time.timeScale = 1;
                Call(tutorial, "JumpToElevatorReview");
                Call(tutorial, "SetPaused", false);
                tutorial.enabled = false; // Suppress user input; the ride still updates normally.
                yield return null;
                ride = tutorial.MuseumElevator;
                check(ride != null && ride.Asset != null && ride.AutomaticTick,
                    "Boarding regression keeps the production automatic ride clock", "No Step, phase or completion setters");
                ride.DepartureReady += observeDeparture;
                var controller = tutorial.Controller;
                check(Mathf.Abs(Mathf.DeltaAngle(ride.transform.eulerAngles.y, 180)) < .1f,
                    "Edge boarding runs in the rotated tutorial cabin", "Source yaw=" + ride.transform.eulerAngles.y.ToString("F3"));

                for (int i = 0; i < ContactNames.Length; i++)
                {
                    yield return OpenNormally(ride, controller);
                    SetFeet(controller, ride.WorldBoardingCenter);
                    Vector3 before = ride.transform.InverseTransformPoint(controller.transform.position);
                    Vector3 direction = ContactDirections[i].normalized;
                    CollisionFlags contact = controller.Move(ride.transform.TransformDirection(direction) * 3);
                    contact |= controller.Move(Vector3.down * .2f);
                    Physics.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                    Vector3 local = ride.transform.InverseTransformPoint(controller.transform.position);
                    bool reachedEdge = (contact & CollisionFlags.Sides) != 0;
                    if (direction.x < 0) reachedEdge &= local.x < -.86f;
                    if (direction.x > 0) reachedEdge &= local.x > .79f;
                    // Compare with the old boarding limit rather than an
                    // estimated rear mesh plane; the real rail blocks first.
                    if (direction.z < 0) reachedEdge &= local.z <
                        ride.Asset.cabinBounds.min.z + controller.radius + .005f;
                    check(reachedEdge && Vector3.Distance(local, before) > .55f,
                        "Actual controller reaches the " + ContactNames[i],
                        "CC.Move into source collider; local feet=" + local.ToString("F4") + "; flags=" + contact);
                    check(ride.PlayerWhollyInside() && !ride.DoorwayObstructed(),
                        "Physical " + ContactNames[i] + " contact counts as fully aboard",
                        "local feet=" + local.ToString("F4") + "; doorway remains clear");
                    yield return AssertOrdinaryDwell(ride, ContactNames[i], local, check);
                    ride.ResetRide(); // Stop before full closure; never load the destination here.
                }

                yield return OpenNormally(ride, controller);
                Vector3 partialLocal = new Vector3(ride.Asset.cabinBounds.center.x,
                    ride.Asset.floorY + .03f, ride.Asset.thresholdZ - .25f);
                SetFeet(controller, ride.transform.TransformPoint(new Vector3(partialLocal.x, partialLocal.y, .7f)));
                controller.Move(ride.transform.TransformVector(partialLocal - new Vector3(partialLocal.x, partialLocal.y, .7f)));
                controller.Move(Vector3.down * .2f);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                check(!ride.PlayerWhollyInside(),
                    "Body straddling the physical door plane remains partial boarding",
                    "Actual capsule local feet=" + ride.transform.InverseTransformPoint(controller.transform.position).ToString("F4") +
                    "; the full-capsule boarding guard must keep the doors open");
                double partialUntil = Time.realtimeSinceStartupAsDouble + .95;
                while (Time.realtimeSinceStartupAsDouble < partialUntil) yield return null;
                check(ride.Phase == ElevatorPhase.Boarding && ride.DoorOpenFraction > .999f && ride.BoardingDwell < .05f,
                    "Threshold-straddling capsule cannot start the departure dwell",
                    "Stayed open beyond 0.75 seconds; dwell=" + ride.BoardingDwell.ToString("F4"));

                // The front-most capsule point sits just behind the actual
                // door safety strip. This exercises a useful front position,
                // rather than requiring the passenger to walk to the centre.
                float localRadius = controller.radius / Mathf.Abs(ride.transform.lossyScale.z);
                Vector3 frontLocal = partialLocal;
                frontLocal.z = ride.Asset.doorSafetyBounds.min.z - localRadius - .035f;
                Vector3 currentLocal = ride.transform.InverseTransformPoint(controller.transform.position);
                controller.Move(ride.transform.TransformVector(frontLocal - currentLocal));
                controller.Move(Vector3.down * .2f);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Vector3 reachedFront = ride.transform.InverseTransformPoint(controller.transform.position);
                check(reachedFront.z > ride.Asset.thresholdZ - .85f &&
                    Mathf.Abs(reachedFront.z - frontLocal.z) < .025f &&
                    ride.PlayerWhollyInside() && !ride.DoorwayObstructed(),
                    "A capsule just clear of the real doors is fully aboard near the front",
                    "CC.Move from straddling pose; local feet=" + reachedFront.ToString("F4") +
                    "; safety rear=" + ride.Asset.doorSafetyBounds.min.z.ToString("F4") +
                    "; wholly=" + ride.PlayerWhollyInside() + "; blocked=" + ride.DoorwayObstructed() +
                    "; radius=" + controller.radius + "; scale=" + controller.transform.lossyScale);
                yield return AssertOrdinaryDwell(ride, "front just inside the doors", reachedFront, check);
                ride.ResetRide();
                check(departures == 0 && ride.RideElapsed == 0 && !ride.DestinationReady,
                    "Boarding regressions never depart or supply a destination state",
                    "Six ordinary closing starts; each interrupted by production ResetRide before full closure");
                Debug.Log("TUTORIAL_ELEVATOR_BOARDING_EDGES_OK physicalContacts=5 frontJustClear=1 thresholdStraddle=1 departures=0");
            }
            finally
            {
                if (ride != null)
                {
                    ride.DepartureReady -= observeDeparture;
                    ride.ResetRide();
                    if (tutorial != null && tutorial.Controller != null) SetFeet(tutorial.Controller, ride.WorldEntry);
                }
                if (tutorial != null)
                {
                    Call(tutorial, "SetPaused", false);
                    tutorial.enabled = originalEnabled;
                }
                Time.timeScale = 1;
            }
        }

        static IEnumerator OpenNormally(TutorialElevatorRide ride, CharacterController controller)
        {
            ride.ResetRide();
            SetFeet(controller, ride.WorldEntry);
            double end = Time.realtimeSinceStartupAsDouble + 6;
            while (ride.Phase != ElevatorPhase.Boarding || ride.DoorOpenFraction < .999f)
            {
                if (Time.realtimeSinceStartupAsDouble > end)
                    throw new TimeoutException("Normal approach did not fully open doors for edge boarding; phase=" + ride.Phase);
                yield return null;
            }
        }

        static IEnumerator AssertOrdinaryDwell(TutorialElevatorRide ride, string location, Vector3 local,
            Action<bool, string, string> check)
        {
            double began = Time.timeAsDouble;
            double end = Time.realtimeSinceStartupAsDouble + 3;
            while (ride.Phase == ElevatorPhase.Boarding)
            {
                if (Time.realtimeSinceStartupAsDouble > end)
                    throw new TimeoutException("The " + location + " did not start normal closing; local feet=" + local +
                        "; dwell=" + ride.BoardingDwell + "; whollyInside=" + ride.PlayerWhollyInside() + "; obstructed=" + ride.DoorwayObstructed());
                yield return null;
            }
            double elapsed = Time.timeAsDouble - began;
            check(ride.Phase == ElevatorPhase.ClosingForDeparture && ride.BoardingDwell >= .75f &&
                elapsed >= .68 && elapsed < 1.2 && ride.DoorOpenFraction > .9f && ride.RideElapsed == 0,
                "Normal 0.75-second dwell starts closing at the " + location,
                "local feet=" + local.ToString("F4") + "; dwell=" + ride.BoardingDwell.ToString("F4") +
                "; elapsed=" + elapsed.ToString("F4") + "; open=" + ride.DoorOpenFraction.ToString("F4"));
        }

        static void SetFeet(CharacterController controller, Vector3 position)
        {
            bool enabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = enabled;
            Physics.SyncTransforms();
        }

        static void Call(object target, string method, params object[] arguments)
        {
            var found = target.GetType().GetMethod(method, Flags);
            if (found == null) throw new MissingMethodException(target.GetType().FullName, method);
            found.Invoke(target, arguments);
        }
    }
}
