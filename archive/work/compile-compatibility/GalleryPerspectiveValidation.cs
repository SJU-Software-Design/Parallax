using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Camera direction is the ONLY placement input in this regression.
    // No scroll, depth setter, scale setter, teleport, or synthetic surface is used.
    public sealed class GalleryPerspectiveValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 120;
        string output;
        int ticks, samples, mismatches, colliderSamples, directionsChecked, committedPosesChecked, blockedPosesChecked, clippedCornerPairs;
        float viewportError, grabDistance, grabScale, nearDistance, nearScale, farDistance, farScale;
        float releasePositionError, releaseRotationError, fallDistance, airborneScale;
        float smallReleasePositionError, smallReleaseRotationError, smallReleaseScaleError, smallReleaseScale;
        bool primarySequencePassed, mountedDetachFixturePassed;
        int mountedDetachFixturePosesChecked;
        SimulationMode previousSimulation;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly List<Observation> observations = new List<Observation>();
        [Serializable] sealed class Observation
        {
            public string name;
            public float yaw, pitch, surfaceDistance, placementDistance, scale, bottomHeight, referenceDistance, referenceScale;
            public Vector3 position;
            public bool valid, blocked;
        }
        [Serializable] sealed class Report
        {
            public bool passed;
            public bool primarySequencePassed, mountedDetachFixturePassed;
            public string scope, unity, gpu, failure;
            public int cameraDirectionsChecked, shadowSamples, shadowMismatches, colliderSamples;
            public int committedPosesChecked, blockedPosesChecked;
            public int mountedDetachFixturePosesChecked;
            public int clippedCornerPairs;
            public float grabDistance, grabScale, nearDistance, nearScale, farDistance, farScale;
            public float maximumViewportError, releasePositionErrorMetres, releaseRotationErrorDegrees, airborneScale, freeFallDistanceMetres;
            public float smallReleasePositionErrorMetres, smallReleaseRotationErrorDegrees, smallReleaseScaleError, smallReleaseScale;
            public Observation[] observations;
            public string[] checks, errors;
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-shade-output");
            output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Application.persistentDataPath, "GalleryPerspectiveValidation");
            Directory.CreateDirectory(output); Application.logMessageReceived += Log;
            previousSimulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
            var sequence = Sequence().GetEnumerator();
            while (true)
            {
                object step = null; string failure = null; bool more;
                try { more = sequence.MoveNext(); if (more) step = sequence.Current; }
                catch (Exception e) { more = false; failure = e.ToString(); }
                if (!more)
                {
                    var report = new Report {
                        passed = failure == null && errors.Count == 0,
                        primarySequencePassed = primarySequencePassed, mountedDetachFixturePassed = mountedDetachFixturePassed,
                        scope = "Primary sequence: original first gallery painting, actual camera Raycast grab and camera-only placement; no wheel, direct distance/scale writes, or object/player relocation. Separate labelled fixture: only the player is moved close to the original third-room mounted painting to check initial detachment; its original object and map geometry are unchanged. Real PhysX drop simulation; not a full puzzle playthrough.",
                        unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, failure = failure,
                        cameraDirectionsChecked = directionsChecked, shadowSamples = samples, shadowMismatches = mismatches, colliderSamples = colliderSamples,
                        committedPosesChecked = committedPosesChecked, blockedPosesChecked = blockedPosesChecked,
                        mountedDetachFixturePosesChecked = mountedDetachFixturePosesChecked,
                        clippedCornerPairs = clippedCornerPairs,
                        grabDistance = grabDistance, grabScale = grabScale, nearDistance = nearDistance, nearScale = nearScale,
                        farDistance = farDistance, farScale = farScale, maximumViewportError = viewportError,
                        releasePositionErrorMetres = releasePositionError, releaseRotationErrorDegrees = releaseRotationError,
                        smallReleasePositionErrorMetres = smallReleasePositionError, smallReleaseRotationErrorDegrees = smallReleaseRotationError,
                        smallReleaseScaleError = smallReleaseScaleError, smallReleaseScale = smallReleaseScale,
                        airborneScale = airborneScale, freeFallDistanceMetres = fallDistance,
                        observations = observations.ToArray(), checks = checks.ToArray(), errors = errors.ToArray()
                    };
                    File.WriteAllText(Path.Combine(output, "perspective-results.json"), JsonUtility.ToJson(report, true));
                    if (failure != null) { Debug.Log("GALLERY_PERSPECTIVE_FAIL " + failure); Capture("perspective-failure"); }
                    Physics.simulationMode = previousSimulation; Application.logMessageReceived -= Log;
                    Application.Quit(report.passed ? 0 : 1); yield break;
                }
                yield return step;
            }
        }
        void Log(string text, string stack, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(text + "\n" + stack); }
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("GALLERY_PERSPECTIVE: " + message); }
        void Check(string message) { checks.Add(message); Debug.Log("GALLERY_PERSPECTIVE_PASS " + message); }
        void Look(Vector3 target)
        {
            var d = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(d.x, d.z); game.pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude); game.SyncView();
        }
        void Grab(GalleryProp prop)
        {
            foreach (var box in prop.parts)
            {
                Look(box.bounds.center); game.UpdateAim();
                if (game.aimed == prop) { game.Interact(); if (game.held == prop) break; }
            }
            Require(game.held == prop, "Cannot grab original painting using visible collider and camera Raycast");
            Require(prop.body.isKinematic, "Held body is not kinematic");
            foreach (var box in prop.parts) Require(box.enabled, "Held collider must remain enabled for collision protection");
        }
        void Aim(float yaw, float pitch)
        {
            game.yaw = yaw; game.pitch = pitch; game.SyncView(); directionsChecked++;
            int stationary = 0;
            for (int frame = 0; frame < 12; frame++)
            {
                var before = game.held.transform.position; var rotation = game.held.transform.rotation;
                game.UpdateHeldPose(); CheckProjection(); CheckCommittedGeometry();
                if (!game.PlacementBlocked) break;
                if (Vector3.Distance(before, game.held.transform.position) < .00002f && Quaternion.Angle(rotation, game.held.transform.rotation) < .002f) stationary++;
                else stationary = 0;
                if (stationary >= 2) break;
            }
        }
        void AimGradually(Vector2 target)
        {
            // Start from the actual accepted anchor, since the crosshair may have moved past a blocked wall.
            Vector3 direction = (game.held.transform.TransformPoint(game.GrabLocalPoint) - game.view.transform.position).normalized;
            float fromYaw = Mathf.Atan2(direction.x, direction.z), fromPitch = Mathf.Asin(Mathf.Clamp(direction.y, -1, 1));
            float yawDelta = Mathf.DeltaAngle(fromYaw * 57.29578f, target.x * 57.29578f) * 0.0174532924f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(yawDelta), Mathf.Abs(target.y - fromPitch)) / .035f));
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Aim(fromYaw + yawDelta * t, Mathf.Lerp(fromPitch, target.y, t));
            }
        }
        bool ReachDirection(Vector2 target)
        {
            Aim(target.x, target.y); if (!game.PlacementBlocked) return true;
            // A target visible from the player can still be behind a wall from the held object.
            // First look at the nearby open floor to bring it back small, then aim farther away.
            Aim(0, -.35f); Aim(target.x, target.y); if (!game.PlacementBlocked) return true;
            // If the object ended above a side wall, follow a slow, elevated camera path around it.
            AimGradually(new Vector2(0, .95f));
            AimGradually(new Vector2(0, -.35f));
            AimGradually(target);
            return !game.PlacementBlocked;
        }
        void CheckProjection()
        {
            GalleryProp prop = game.held;
            Require(prop != null, "Perspective check requires held prop");
            float expected = game.referenceScale * game.holdDistance / game.referenceDistance;
            Require(Mathf.Abs(prop.transform.localScale.x - expected) < .0002f, "Automatic depth and actual scale are not proportional");
            var eye = game.view.transform.position;
            var actualAnchor = prop.transform.TransformPoint(game.GrabLocalPoint);
            Require(Mathf.Abs(Vector3.Distance(actualAnchor, eye) - game.holdDistance) < .0003f, "Accepted anchor distance does not match accepted scale");
            if (game.PlacementBlocked)
            {
                blockedPosesChecked++;
                Require(game.ValidPlacement(prop, out var blockedReason), "Blocked preview is not a safe accepted pose: " + blockedReason);
                return;
            }
            var anchor = eye + game.view.transform.forward * game.holdDistance;
            Require(Vector3.Distance(actualAnchor, anchor) < .0003f, "Picked point rises / leaves unblocked camera ray");
            // Compare against the pickup-sized geometry on THIS camera ray, at pickup depth.
            // This permits deliberate camera/orientation changes without conflating foreshortening with scale drift.
            foreach (var box in prop.parts) for (int i = 0; i < 8; i++)
            {
                var corner = box.center + Vector3.Scale(box.size * .5f,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var currentWorld = box.transform.TransformPoint(corner);
                var rootLocal = prop.transform.InverseTransformPoint(currentWorld);
                var referenceWorld = eye + game.view.transform.forward * game.referenceDistance +
                    game.heldRotation * (rootLocal - game.GrabLocalPoint) * game.referenceScale;
                var actual = game.view.WorldToViewportPoint(currentWorld); var reference = game.view.WorldToViewportPoint(referenceWorld);
                Require((actual.z > 0) == (reference.z > 0), "Distance scaling moves only one version of a corner behind the camera");
                // Very close original wall pictures can already straddle the camera plane.
                // Their clipped corners have no visible pixel to compare, but must retain the same front/back classification.
                if (actual.z <= game.view.nearClipPlane && reference.z <= game.view.nearClipPlane)
                { clippedCornerPairs++; continue; }
                float error = Vector2.Distance(new Vector2(actual.x, actual.y), new Vector2(reference.x, reference.y));
                viewportError = Mathf.Max(viewportError, error); Require(error < .0003f, "Automatic distance changes projected size: " + error);
            }
        }
        void CheckCommittedGeometry()
        {
            var prop = game.held; committedPosesChecked++;
            Require(game.ValidPlacement(prop, out var reason), "A displayed held pose penetrates an obstacle: " + reason);
            Require(prop.body.isKinematic, "Held preview unexpectedly became dynamic");
            foreach (var box in prop.parts)
            {
                Require(box.enabled, "Held collider disabled during a committed pose");
                Vector3 half = Vector3.Scale(box.size, box.transform.lossyScale) * .5f;
                half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
                foreach (var other in Physics.OverlapBox(box.transform.TransformPoint(box.center), half, box.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (other == game.controller || other.GetComponentInParent<GalleryProp>() == prop) continue;
                    if (Physics.ComputePenetration(box, box.transform.position, box.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out var direction, out float depth))
                        Require(depth <= .004f, "Independent PhysX penetration at committed pose: " + other.name + " / " + depth + "m");
                }
            }
        }
        Observation Observe(string name)
        {
            var p = game.held; float surface = -1;
            foreach (var hit in Physics.RaycastAll(game.view.transform.position, game.view.transform.forward, 48, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider != game.controller && hit.collider.GetComponentInParent<GalleryProp>() != p)
                    surface = surface < 0 ? hit.distance : Mathf.Min(surface, hit.distance);
            var value = new Observation { name = name, yaw = game.yaw, pitch = game.pitch, surfaceDistance = surface,
                placementDistance = game.holdDistance, scale = p.transform.localScale.x, bottomHeight = p.WorldBounds.min.y,
                referenceDistance = game.referenceDistance, referenceScale = game.referenceScale,
                position = p.transform.position, valid = !game.invalid, blocked = game.PlacementBlocked };
            observations.Add(value); return value;
        }
        Vector2 FindDirection(bool far, bool airborne)
        {
            bool found = false; float best = far ? -1 : (1f/0f); Vector2 direction = Vector2.zero;
            // Both near/far cases use existing room geometry, never injected planes.
            for (int yi = -12; yi <= 12; yi++) for (int index = 0; index < 26; index++)
            {
                int pi = (yi & 1) == 0 ? -12 + index : 13 - index;
                float yaw = yi * .09f, pitch = pi * .05f; Aim(yaw, pitch);
                if (game.invalid || game.PlacementBlocked) continue;
                if (airborne)
                {
                    var bounds = game.held.WorldBounds;
                    if (pitch < .1f || bounds.min.y < .9f || bounds.max.y > 9.3f || bounds.max.z > 11.8f) continue;
                    // Leave space for the FULL forward sweep as the tilted picture falls over.
                    // The previous farthest sample correctly leaned against the rear partition;
                    // it was unsuitable for independently testing free toppling on an open floor.
                    if (game.held.transform.position.z + 2.5f * game.held.transform.localScale.x > 13.2f) continue;
                }
                float value = game.holdDistance;
                if ((!far && value < best) || (far && value > best)) { found = true; best = value; direction = new Vector2(yaw, pitch); }
            }
            Require(found, "No valid " + (airborne ? "upward airborne" : far ? "far" : "near") + " camera-only placement found");
            Require(ReachDirection(direction), "Selected free placement is obstructed after camera-only return via central floor / gradual look path");
            return direction;
        }
        IEnumerable<object> CollisionAttempts()
        {
            int before = committedPosesChecked, stoppedBefore = blockedPosesChecked;
            var aims = new[] { new Vector2(0, -1.3f), new Vector2(2.8f, .15f), new Vector2(-2.8f, .5f),
                new Vector2(0, .7f), new Vector2(1.35f, -.25f), new Vector2(-1.35f, .4f), new Vector2(0, 1.25f) };
            foreach (var target in aims)
            {
                Aim(target.x, target.y); Observe("collision-attempt-camera-" + committedPosesChecked); yield return null;
            }
            for (int i = 0; i < 8; i++)
            {
                game.RotateHeld(i % 2 == 0 ? 45 : -70, 40);
                Aim(i % 2 == 0 ? .9f : -.9f, i % 3 == 0 ? -.95f : .5f);
                Observe("collision-attempt-rotation-" + i); yield return null;
            }
            Aim(0, -1.3f);
            Require(blockedPosesChecked > stoppedBefore, "Wall / floor attempts never exercised a blocked placement");
            Check("Rapid camera turns, aiming below floor, upward growth and rotation attempts kept all " + (committedPosesChecked - before) + " displayed intermediate poses collision-valid with enabled kinematic colliders");
            Vector3 position = game.held.transform.position, scale = game.held.transform.localScale;
            Quaternion rotation = game.held.transform.rotation; var prop = game.held;
            bool blocked = game.PlacementBlocked;
            Require(game.Drop(), "Last collision-safe held pose cannot be released");
            Require(Vector3.Distance(position, prop.transform.position) < .0003f && Quaternion.Angle(rotation, prop.transform.rotation) < .005f && prop.transform.localScale == scale,
                "Releasing a blocked attempt changes accepted safe pose / size");
            Check("Release after collision attempt preserves accepted safe pose and scale; blocked=" + blocked);
            game.ResetZone(); Grab(prop);
        }
        IEnumerable<object> Simulate(int count)
        {
            for (int i = 0; i < count; i++)
            {
                game.Tick(Dt, Vector2.zero); Physics.Simulate(Dt); Physics.SyncTransforms(); game.lighting.Synchronize();
                if (++ticks % 20 == 0) yield return null;
            }
        }
        void CheckMountedDetachFixture()
        {
            game.ResetAll(); game.Resume();
            var prop = game.props[2];
            Require(prop.transform.position == prop.homePosition && prop.transform.localScale == prop.homeScale,
                "Close-wall fixture must use unchanged original mounted painting");
            Vector3 feet = new Vector3(-6, .025f, 27.85f);
            Require(game.CanStand(feet), "Close-wall player fixture is inside scene geometry");
            game.controller.enabled = false; game.controller.transform.position = feet; game.controller.enabled = true;
            game.player.feet = feet; game.verticalVelocity = 0; game.exposure = 0;
            Physics.SyncTransforms(); game.SyncView();
            int checkedBefore = committedPosesChecked;
            Grab(prop); Observe("independent-close-wall-mounted-detachment");
            CheckProjection(); CheckCommittedGeometry();
            Require(!game.PlacementBlocked, "Original close-wall painting remains stuck immediately after detachment");
            Vector3 detached = prop.transform.position;
            Aim(game.yaw + .12f, game.pitch + .10f);
            Observe("independent-close-wall-small-aim-after-detachment");
            Require(!game.PlacementBlocked && Vector3.Distance(detached, prop.transform.position) > .005f,
                "Detached close-wall painting cannot follow a small camera movement");
            mountedDetachFixturePosesChecked = committedPosesChecked - checkedBefore;
            mountedDetachFixturePassed = true;
            Check("Independent player-only fixture at (-6, 0.025, 27.85): original third-room mounted picture detaches into a valid, nonpenetrating pose and follows a small aim change; " + mountedDetachFixturePosesChecked + " accepted poses checked");
            game.Cancel(); game.ResetAll();
        }
        void Capture(string name)
        {
            var camera = game.view; var old = camera.targetTexture; var active = RenderTexture.active;
            var rt = new RenderTexture(1600, 900, 24); camera.targetTexture = rt; game.lighting.Synchronize(); camera.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            camera.targetTexture = old; RenderTexture.active = active; File.WriteAllBytes(Path.Combine(output, name + ".png"), tex.EncodeToPNG());
            rt.Release(); Destroy(rt); Destroy(tex);
        }
        void CompareShader(float height, string name)
        {
            const int side = 160;
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad); plane.layer = 30;
            plane.transform.SetPositionAndRotation(new Vector3(0, height, 27), Quaternion.Euler(90, 0, 0));
            plane.transform.localScale = new Vector3(28, 54, 1); DestroyImmediate(plane.GetComponent<Collider>());
            var material = new Material(Shader.Find("Parallax/GallerySurface")); material.SetFloat("_Diagnostic", 1); plane.GetComponent<Renderer>().sharedMaterial = material;
            var camera = new GameObject("Perspective GPU verification").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 27; camera.aspect = 1; camera.cullingMask = 1 << 30;
            camera.transform.SetPositionAndRotation(new Vector3(0, height + 12, 27), Quaternion.Euler(90, 0, 0));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            var rt = new RenderTexture(side, side, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            camera.targetTexture = rt; game.lighting.Synchronize(); camera.Render(); var active = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(side, side, TextureFormat.RGB24, false, true); tex.ReadPixels(new Rect(0, 0, side, side), 0, 0); tex.Apply();
            var pixels = tex.GetPixels32(); int tested = 0, wrong = 0;
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
            {
                float wx = (x + .5f) / side * 54 - 27, wz = (y + .5f) / side * 54; if (Mathf.Abs(wx) > 13.8f) continue;
                bool cpu = game.lighting.IsSunlit(new Vector3(wx, height, wz), Vector3.up), gpu = pixels[y * side + x].r > 127;
                if (cpu != gpu) wrong++; tested++;
            }
            File.WriteAllBytes(Path.Combine(output, "perspective-mask-" + name + ".png"), tex.EncodeToPNG()); RenderTexture.active = active;
            camera.targetTexture = null; rt.Release(); DestroyImmediate(plane); DestroyImmediate(material); DestroyImmediate(camera.gameObject); DestroyImmediate(rt); DestroyImmediate(tex);
            samples += tested; mismatches += wrong; Require(wrong == 0, "CPU/GPU shadow mismatch " + name + ": " + wrong + "/" + tested);
            Check("Visible and safe shadow masks match at " + name + ": " + tested + " samples");
        }
        void ComparePhysics(GalleryProp prop)
        {
            var random = new System.Random(268); int tested = 0; Physics.SyncTransforms();
            foreach (var caster in prop.GetComponentsInChildren<GalleryCaster>()) for (int i = 0; i < 120; i++)
            {
                var b = caster.shape.bounds;
                var p = b.center + new Vector3((float)random.NextDouble() * 14 - 7, -(float)random.NextDouble() * 12 - .6f, (float)random.NextDouble() * 14 - 7);
                if (b.Contains(p)) continue;
                bool cpu = GalleryLighting.Intersects(caster.Inverse, p, GalleryLighting.ToSun);
                bool physics = caster.shape.Raycast(new Ray(p, GalleryLighting.ToSun), out var hit, 200);
                Require(cpu == physics, "Actual collider differs from perspective-adjusted shadow geometry"); tested++;
            }
            colliderSamples += tested; Check("PhysX collider rays match perspective-adjusted silhouette: " + tested + " samples");
        }
        IEnumerable<object> Sequence()
        {
            yield return null; yield return null; game.ResetAll(); game.Resume();
            foreach (var step in Simulate(4)) yield return step;
            var painting = game.props[0]; Grab(painting); grabDistance = game.referenceDistance; grabScale = game.referenceScale; Observe("original-pickup");
            foreach (var step in CollisionAttempts()) yield return step;
            var near = FindDirection(false, false); var nearObservation = Observe("near-camera-only");
            nearDistance = nearObservation.placementDistance; nearScale = nearObservation.scale; Capture("perspective-01-near");
            var far = FindDirection(true, false); var farObservation = Observe("far-camera-only");
            farDistance = farObservation.placementDistance; farScale = farObservation.scale; Capture("perspective-02-far");
            Require(farDistance > nearDistance * 1.5f && farScale > nearScale * 1.5f, "Camera direction does not automatically produce a meaningful near/far scale difference");
            Require(nearScale < grabScale * .9f && farScale > grabScale * 1.1f, "Camera-only placement cannot shrink below and grow above pickup size");
            Require(ReachDirection(near), "Camera-only return route to near surface is blocked"); Require(Mathf.Abs(game.held.transform.localScale.x - nearScale) < .001f, "Looking back at near surface does not shrink again");
            Require(ReachDirection(far), "Camera-only return route to far surface is blocked"); Require(Mathf.Abs(game.held.transform.localScale.x - farScale) < .001f, "Looking back at far surface does not grow again");
            Check("Same pickup, camera direction only: near " + nearDistance + "m / " + nearScale + "x -> far " + farDistance + "m / " + farScale + "x -> near -> far; no wheel or depth/scale writes");
            CompareShader(0, "automatic-far-floor"); CompareShader(2, "automatic-far-upper");

            Require(ReachDirection(near), "Camera-only route to small release position is blocked"); Observe("far-to-near-release");
            Vector3 smallPosition = painting.transform.position, smallScale = painting.transform.localScale;
            Quaternion smallRotation = painting.transform.rotation;
            smallReleaseScale = smallScale.x;
            Require(smallReleaseScale < grabScale * .9f, "Far -> near did not return to a smaller-than-pickup size");
            Require(game.Drop(), "Far -> near small placement cannot be released");
            smallReleasePositionError = Vector3.Distance(smallPosition, painting.transform.position);
            smallReleaseRotationError = Quaternion.Angle(smallRotation, painting.transform.rotation);
            smallReleaseScaleError = Vector3.Distance(smallScale, painting.transform.localScale);
            Require(smallReleasePositionError < .0001f && smallReleaseRotationError < .001f && smallReleaseScaleError < .0001f,
                "Far -> near -> drop changes the small preview pose or scale");
            foreach (var step in Simulate(180)) yield return step;
            Require(painting.transform.localScale == smallScale && !painting.body.isKinematic,
                "Small released object restores original / previous far scale during physics");
            Look(painting.WorldBounds.center); Capture("perspective-03-small-release");
            Check("Far -> near -> drop keeps the small " + smallReleaseScale + "x size and exact release pose; 1.5 seconds of real physics preserves that size");
            game.ResetZone(); Grab(painting); Observe("airborne-test-original-regrab");
            game.RotateHeld(0, 25); FindDirection(true, true); var air = Observe("upward-airborne-camera-only");
            airborneScale = air.scale;
            Require(airborneScale > grabScale * 1.1f && painting.WorldBounds.min.y > .9f, "Upward camera placement is not enlarged and airborne");
            Vector3 position = painting.transform.position, scale = painting.transform.localScale; Quaternion rotation = painting.transform.rotation;
            Capture("perspective-03-upward-airborne"); Require(game.Drop(), "Camera-only airborne drop rejected: " + game.invalidReason);
            releasePositionError = Vector3.Distance(position, painting.transform.position); releaseRotationError = Quaternion.Angle(rotation, painting.transform.rotation);
            Require(releasePositionError < .0001f && releaseRotationError < .001f && painting.transform.localScale == scale, "Drop changes preview pose or enlarged scale");
            Require(!painting.body.isKinematic && painting.body.useGravity && painting.body.constraints == RigidbodyConstraints.None, "Drop does not release gravity / rotation physics");
            foreach (var step in Simulate(36)) yield return step;
            fallDistance = position.y - painting.transform.position.y;
            Require(fallDistance > .18f && painting.body.linearVelocity.y < -.5f, "Camera-only enlarged airborne object does not fall naturally");
            Require(painting.transform.localScale == scale, "Falling object loses enlarged scale");
            ComparePhysics(painting); CompareShader(0, "falling-floor"); CompareShader(2, "falling-upper"); Capture("perspective-04-falling");
            Check("Upward aiming automatically enlarges object; drop preserves exact pose/scale then real gravity lowers it by " + fallDistance + "m in 0.3s");
            foreach (var step in Simulate(1800)) yield return step;
            Require(painting.WorldBounds.min.y > -.05f && painting.WorldBounds.min.y < .15f, "Released enlarged object did not reach floor");
            Require(painting.body.linearVelocity.magnitude < .09f && painting.body.angularVelocity.magnitude < .09f, "Released object did not settle after collision");
            Require(Quaternion.Angle(rotation, painting.transform.rotation) > 8, "Inclined painting remains artificially rotation-locked");
            Require(painting.transform.localScale == scale && !painting.body.isKinematic, "Landing changes enlarged scale or forcibly freezes body");
            ComparePhysics(painting); CompareShader(0, "settled-floor");
            Look(painting.WorldBounds.center); Capture("perspective-05-settled");
            Check("Camera-only placed tilted object collides, rotates and settles under PhysX, retaining the enlarged scale");
            game.ResetZone(); Require(painting.transform.position == painting.homePosition && painting.transform.localScale == painting.homeScale && painting.body.isKinematic,
                "Reset does not restore original mounted exhibit");
            Check("Zone reset restores original exhibit; camera-only perspective regression completed");
            primarySequencePassed = true;
            CheckMountedDetachFixture();
        }
    }
}
