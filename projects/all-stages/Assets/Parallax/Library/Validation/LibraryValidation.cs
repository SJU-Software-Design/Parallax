using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Library
{
    // Fixtures may move only the player. ResetAll separates these from the continuous
    // playthrough, which uses Tick, camera aiming, rotation, Interact and Drop only.
    public sealed class LibraryValidation : MonoBehaviour
    {
        public LibraryGame game;
        const float Dt = 1f / 120f;
        string output, stage = "initialization";
        int ticks, booksChecked, posesChecked, blockedPoses, shadowSamples, shadowMismatches, colliderSamples, completedZones;
        float nearDistance, nearScale, farDistance, farScale, projectionError, dropScaleError, fallDistance;
        bool booksPassed, perspectivePassed, shadowsPassed, routeStarted;
        SimulationMode previousSimulation;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        [Serializable] sealed class Report
        {
            public bool passed, booksPassed, perspectivePassed, shadowsPassed, escaped;
            public string scope, lastStage, failure, unity, gpu;
            public int booksIndividuallyGrabbed, heldPosesChecked, blockedPoses, shadowSamples, shadowMismatches, colliderSamples;
            public int completedZones, checkpoint, respawns, grabs, drops;
            public float nearDistance, nearScale, farDistance, farScale, maximumViewportError, dropScaleError, fallDistance;
            public string[] checks, errors;
        }
        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-shade-output");
            output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Application.persistentDataPath, "LibraryValidation");
            Directory.CreateDirectory(output); Application.logMessageReceived += Log;
            previousSimulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
            var sequence = Sequence().GetEnumerator();
            while (true)
            {
                object next = null; bool more; string failure = null;
                try { more = sequence.MoveNext(); if (more) next = sequence.Current; }
                catch (Exception e) { more = false; failure = e.ToString(); }
                if (!more)
                {
                    bool passed = failure == null && errors.Count == 0 && booksPassed && perspectivePassed && shadowsPassed && game.won;
                    File.WriteAllText(Path.Combine(output, "library-results.json"), JsonUtility.ToJson(new Report {
                        passed = passed, booksPassed = booksPassed, perspectivePassed = perspectivePassed, shadowsPassed = shadowsPassed, escaped = game.won,
                        scope = "Independent book/perspective fixtures move only the player to existing objects and use camera Raycast interaction. After ResetAll the continuous start-to-exit route uses Tick movement, camera aiming, rotation, grab and drop; no teleport, direct object transform/scale/depth write, exposure override or win-state injection.",
                        lastStage = stage, failure = failure, unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                        booksIndividuallyGrabbed = booksChecked, heldPosesChecked = posesChecked, blockedPoses = blockedPoses,
                        shadowSamples = shadowSamples, shadowMismatches = shadowMismatches, colliderSamples = colliderSamples,
                        completedZones = completedZones, checkpoint = game.checkpoint, respawns = game.respawns, grabs = game.grabs, drops = game.drops,
                        nearDistance = nearDistance, nearScale = nearScale, farDistance = farDistance, farScale = farScale,
                        maximumViewportError = projectionError, dropScaleError = dropScaleError, fallDistance = fallDistance,
                        checks = checks.ToArray(), errors = errors.ToArray()
                    }, true));
                    if (failure != null) Debug.Log("LIBRARY_TEST_FAIL " + failure);
                    Capture(passed ? "library-clear" : "library-stopped");
                    Physics.simulationMode = previousSimulation; Application.logMessageReceived -= Log;
                    Application.Quit(passed ? 0 : 1); yield break;
                }
                yield return next;
            }
        }
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(stage + ": " + message + "; feet=" + game.player.feet + "; exposure=" + game.exposure); }
        void Check(string message) { checks.Add(message); Debug.Log("LIBRARY_TEST_PASS " + message); }
        void Look(Vector3 target)
        {
            var d = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(d.x, d.z); game.pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude); game.SyncView();
        }
        LibraryProp Prop(string id)
        {
            foreach (var p in game.props) if (p.objectId == id) return p;
            throw new InvalidOperationException("Unknown library objectId " + id);
        }
        void FixturePlayer(Vector3 point)
        {
            Require(!routeStarted, "Fixture relocation called during continuous route");
            game.controller.enabled = false; game.controller.transform.position = point; game.controller.enabled = true;
            game.player.feet = point; game.verticalVelocity = 0; game.exposure = 0; Physics.SyncTransforms(); game.SyncView();
        }
        bool AimProp(LibraryProp prop)
        {
            foreach (var part in prop.parts)
            {
                Look(part.bounds.center); game.UpdateAim(); if (game.aimed == prop) return true;
            }
            return false;
        }
        void FixtureVantage(LibraryProp prop)
        {
            var center = prop.WorldBounds.center;
            for (float distance = 1.2f; distance <= 5; distance += .8f) for (int a = 0; a < 16; a++)
            {
                var point = new Vector3(center.x + Mathf.Sin(a * Mathf.PI / 8) * distance, .025f,
                    center.z + Mathf.Cos(a * Mathf.PI / 8) * distance);
                if (!game.CanStand(point)) continue;
                FixturePlayer(point); if (AimProp(prop)) return;
            }
            Require(false, "No unobstructed independent camera vantage for " + prop.objectId);
        }
        void Grab(LibraryProp prop)
        {
            Require(AimProp(prop), "Camera cannot select " + prop.objectId); game.Interact();
            Require(game.held == prop, "Raycast interaction could not grab " + prop.objectId + "; " + game.notice);
            Require(prop.body.isKinematic, "Held object is not kinematic");
            CheckPose();
        }
        void CheckPose()
        {
            var prop = game.held; Require(prop != null, "Expected held prop"); posesChecked++;
            Require(game.ValidPlacement(prop, out var reason), "Held collider penetrates world: " + reason);
            foreach (var part in prop.parts) Require(part.enabled, "Held collider disabled");
            float expected = game.referenceScale * game.holdDistance / game.referenceDistance;
            Require(Mathf.Abs(prop.transform.localScale.x - expected) < .0003f, "Scale is not proportional to camera distance");
            var eye = game.view.transform.position; var anchor = prop.transform.TransformPoint(game.GrabLocalPoint);
            Require(Mathf.Abs(Vector3.Distance(eye, anchor) - game.holdDistance) < .0005f, "Actual camera distance differs from scale distance");
            if (game.PlacementBlocked) { blockedPoses++; return; }
            Require(Vector3.Distance(anchor, eye + game.view.transform.forward * game.holdDistance) < .0005f, "Grab anchor drifts away from view ray");
            foreach (var part in prop.parts) for (int i = 0; i < 8; i++)
            {
                var corner = part.center + Vector3.Scale(part.size * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var world = part.transform.TransformPoint(corner); var local = prop.transform.InverseTransformPoint(world);
                var reference = eye + game.view.transform.forward * game.referenceDistance + game.heldRotation * (local - game.GrabLocalPoint) * game.referenceScale;
                var actualPixel = game.view.WorldToViewportPoint(world); var referencePixel = game.view.WorldToViewportPoint(reference);
                if (actualPixel.z <= game.view.nearClipPlane || referencePixel.z <= game.view.nearClipPlane) continue;
                float error = Vector2.Distance(new Vector2(actualPixel.x, actualPixel.y), new Vector2(referencePixel.x, referencePixel.y));
                projectionError = Mathf.Max(projectionError, error); Require(error < .0005f, "Projected screen size changes with placement distance");
            }
        }
        void Aim(float yaw, float pitch)
        {
            game.yaw = yaw; game.pitch = pitch; game.SyncView();
            for (int i = 0; i < 5; i++) { game.UpdateHeldPose(); CheckPose(); if (!game.PlacementBlocked) break; }
        }
        void AimTarget(Vector3 point) { Look(point); Aim(game.yaw, game.pitch); }
        void AimGradually(float targetYaw, float targetPitch)
        {
            var direction = (game.held.transform.TransformPoint(game.GrabLocalPoint) - game.view.transform.position).normalized;
            float fromYaw = Mathf.Atan2(direction.x, direction.z), fromPitch = Mathf.Asin(Mathf.Clamp(direction.y, -1, 1));
            float delta = Mathf.DeltaAngle(fromYaw * Mathf.Rad2Deg, targetYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            int count = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(delta), Mathf.Abs(targetPitch - fromPitch)) / .06f));
            for (int i = 1; i <= count; i++) Aim(fromYaw + delta * i / count, Mathf.Lerp(fromPitch, targetPitch, (float)i / count));
        }
        void Step(Vector2 move, bool jump = false)
        {
            game.Tick(Dt, move, jump); Physics.Simulate(Dt); Physics.SyncTransforms(); game.lighting.Synchronize();
            if (routeStarted) Require(game.respawns == 0, "Continuous route caused a respawn");
        }
        IEnumerable<object> Wait(float seconds, bool fixture = false)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / Dt); i++)
            {
                if (fixture) { Physics.Simulate(Dt); Physics.SyncTransforms(); game.lighting.Synchronize(); }
                else Step(Vector2.zero);
                if (++ticks % 24 == 0) yield return null;
            }
        }
        IEnumerable<object> Walk(float x, float z, bool jump = false, float height = -1)
        {
            Vector2 target = new Vector2(x, z); int n = 0;
            while ((target - game.player.Point).magnitude > .003f && !game.won)
            {
                var delta = target - game.player.Point;
                Step(delta.normalized * Mathf.Min(1, delta.magnitude / (LibraryGame.Speed * Dt)), jump && n == 0);
                Require(++n < 2000, "Movement blocked toward " + target);
                if (++ticks % 24 == 0) yield return null;
            }
            if (height >= 0) { foreach (var s in Wait(.5f)) yield return s; Require(Mathf.Abs(game.player.feet.y - height) < .12f, "Unexpected landing height, expected " + height); }
        }
        void Capture(string name)
        {
            var camera = game.view; var old = camera.targetTexture; var active = RenderTexture.active;
            var rt = new RenderTexture(1280, 720, 24); camera.targetTexture = rt; game.lighting.Synchronize(); camera.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            camera.targetTexture = old; RenderTexture.active = active; File.WriteAllBytes(Path.Combine(output, name + ".png"), tex.EncodeToPNG());
            rt.Release(); Destroy(rt); Destroy(tex);
        }
        void CompareShader(float height, string label)
        {
            const int side = 96;
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad); plane.layer = 30;
            plane.transform.SetPositionAndRotation(new Vector3(0, height, 27), Quaternion.Euler(90, 0, 0)); plane.transform.localScale = new Vector3(28, 54, 1);
            DestroyImmediate(plane.GetComponent<Collider>());
            var material = new Material(Shader.Find("Parallax/LibrarySurface")); material.SetFloat("_Diagnostic", 1); plane.GetComponent<Renderer>().sharedMaterial = material;
            var camera = new GameObject("Library GPU shadow verification").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 27;
            camera.aspect = 1; camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            camera.transform.SetPositionAndRotation(new Vector3(0, height + 12, 27), Quaternion.Euler(90, 0, 0));
            var rt = new RenderTexture(side, side, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); camera.targetTexture = rt;
            game.lighting.Synchronize(); camera.Render(); var active = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(side, side, TextureFormat.RGB24, false, true); tex.ReadPixels(new Rect(0, 0, side, side), 0, 0); tex.Apply(); var pixels = tex.GetPixels32();
            int tested = 0, wrong = 0;
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
            {
                float wx = (x + .5f) / side * 54 - 27, wz = (y + .5f) / side * 54; if (Mathf.Abs(wx) > 13.7f) continue;
                bool cpu = game.lighting.IsSunlit(new Vector3(wx, height, wz), Vector3.up), gpu = pixels[y * side + x].r > 127;
                tested++; if (cpu != gpu) wrong++;
            }
            File.WriteAllBytes(Path.Combine(output, "shadow-" + label + ".png"), tex.EncodeToPNG()); RenderTexture.active = active; camera.targetTexture = null;
            rt.Release(); DestroyImmediate(plane); DestroyImmediate(material); DestroyImmediate(camera.gameObject); DestroyImmediate(rt); DestroyImmediate(tex);
            shadowSamples += tested; shadowMismatches += wrong; Require(wrong == 0, "Rendered shadow / safety mismatch " + label + " " + wrong + "/" + tested);
            Check("GPU/CPU shadow mask matches at " + label + ": " + tested + " samples");
        }
        void CompareColliders(LibraryProp prop)
        {
            var random = new System.Random(286);
            foreach (var caster in prop.GetComponentsInChildren<LibraryCaster>()) for (int i = 0; i < 64; i++)
            {
                var b = caster.shape.bounds; var p = b.center + new Vector3((float)random.NextDouble() * 12 - 6, -(float)random.NextDouble() * 8 - .4f, (float)random.NextDouble() * 12 - 6);
                if (b.Contains(p)) continue;
                bool cpu = LibraryLighting.Intersects(caster.Inverse, p, LibraryLighting.ToSun), physics = caster.shape.Raycast(new Ray(p, LibraryLighting.ToSun), out var hit, 200);
                Require(cpu == physics, "Scaled collider does not match shadow silhouette"); colliderSamples++;
            }
        }
        IEnumerable<object> Fixtures()
        {
            stage = "individual shelf-book fixtures";
            foreach (var prop in game.props) if (string.Equals(prop.kind, "Book", StringComparison.OrdinalIgnoreCase))
            {
                FixtureVantage(prop); var originalPosition = prop.transform.position; Grab(prop);
                float yaw = game.yaw, pitch = game.pitch;
                Aim(yaw + .08f, pitch + .04f);
                Require(Vector3.Distance(originalPosition, prop.transform.position) > .035f, "Selected book cannot be detached/moved: " + prop.objectId);
                booksChecked++; game.Cancel(); yield return null;
            }
            Require(booksChecked > 0, "No individually selectable books"); booksPassed = true; Check("Every book individually selected, grabbed and moved via real camera ray: " + booksChecked);
            stage = "camera-distance perspective fixture";
            LibraryProp book = null; foreach (var prop in game.props) if (string.Equals(prop.kind, "Book", StringComparison.OrdinalIgnoreCase)) { book = prop; break; }
            FixtureVantage(book); Grab(book); float originalScale = game.referenceScale;
            float smallest = float.PositiveInfinity, largest = -1; Vector2 near = Vector2.zero, far = Vector2.zero;
            // A bounded set of camera directions; no scale/depth writes and no synthetic surfaces.
            for (int y = 0; y < 12; y++) for (int p = 0; p < 8; p++)
            {
                float yaw = y * Mathf.PI / 6, pitch = -.95f + p * .22f; Aim(yaw, pitch);
                if (game.PlacementBlocked) continue;
                if (game.holdDistance < smallest) { smallest = game.holdDistance; near = new Vector2(yaw, pitch); }
                if (game.holdDistance > largest) { largest = game.holdDistance; far = new Vector2(yaw, pitch); }
                if ((y * 8 + p) % 12 == 0) yield return null;
            }
            Aim(near.x, near.y); nearDistance = game.holdDistance; nearScale = book.transform.localScale.x; Capture("fixture-near");
            Aim(far.x, far.y); farDistance = game.holdDistance; farScale = book.transform.localScale.x; Capture("fixture-far");
            Require(farScale > nearScale * 1.4f && farDistance > nearDistance * 1.4f, "Camera-only aiming did not produce near/far growth");
            Require(nearScale < originalScale * .95f && farScale > originalScale * 1.05f, "Camera-only aiming must shrink and enlarge from pickup scale");
            CompareShader(0, "enlarged-floor"); CompareShader(2, "enlarged-upper"); CompareColliders(book);
            Aim(near.x, near.y); Require(Mathf.Abs(book.transform.localScale.x - nearScale) < .003f, "Returning near does not shrink");
            var size = book.transform.localScale; var pos = book.transform.position; Require(game.Drop(), "Cannot drop near book");
            Require(Vector3.Distance(book.transform.position, pos) < .0001f, "Release teleports book");
            foreach (var s in Wait(1, true)) yield return s;
            dropScaleError = Vector3.Distance(book.transform.localScale, size); Require(dropScaleError < .0001f, "Drop loses perspective scale");
            Require(!book.body.isKinematic && book.body.useGravity && book.body.constraints == RigidbodyConstraints.None, "Release physics is locked");
            Check("Camera-only far " + farDistance + "m / " + farScale + "x then near " + nearDistance + "m / " + nearScale + "x; release preserves size and dynamic gravity");
            stage = "enlarged airborne tilted-book physics fixture";
            game.ResetAll(); game.Resume(); FixtureVantage(book); Grab(book);
            // Lift straight away from its supporting table before asking the player rotation control to tilt it.
            float pickupYaw = game.yaw;
            for (int lift = 1; lift <= 8; lift++) Aim(pickupYaw, -.1f + lift * .08f);
            game.RotateHeld(0, 25); Aim(pickupYaw, .54f);
            bool foundAir = false; float bestAir = -1; Vector2 air = Vector2.zero;
            for (int y = 0; y < 12; y++) for (int p = 0; p < 8; p++)
            {
                float yaw = y * Mathf.PI / 6, pitch = .12f + p * .12f; Aim(yaw, pitch);
                var b = book.WorldBounds;
                if (!game.PlacementBlocked && book.transform.localScale.x > 1.15f && b.min.y > .8f && b.max.x < 7 && b.min.x > -12 && b.min.z > 1 && b.max.z < 12 && game.holdDistance > bestAir)
                { foundAir = true; bestAir = game.holdDistance; air = new Vector2(yaw, pitch); }
                if ((y * 8 + p) % 12 == 0) yield return null;
            }
            Require(foundAir, "No camera-only enlarged airborne position with floor clearance");
            Aim(air.x, air.y); Require(!game.PlacementBlocked && book.WorldBounds.min.y > .8f, "Cannot return to airborne placement");
            var airPosition = book.transform.position; var airSize = book.transform.localScale; var airRotation = book.transform.rotation;
            Capture("fixture-airborne-tilted"); Require(game.Drop(), "Airborne tilted release rejected");
            Require(Vector3.Distance(book.transform.position, airPosition) < .0001f && Quaternion.Angle(book.transform.rotation, airRotation) < .001f && book.transform.localScale == airSize, "Airborne release changed displayed pose/size");
            foreach (var s in Wait(.3f, true)) yield return s;
            fallDistance = airPosition.y - book.transform.position.y; Require(fallDistance > .18f, "Enlarged airborne book did not fall under gravity");
            Require(book.transform.localScale == airSize, "Falling book lost enlarged scale"); CompareColliders(book); CompareShader(0, "falling-book-floor");
            foreach (var s in Wait(8, true)) yield return s;
            Require(book.transform.localScale == airSize && !book.body.isKinematic && book.body.constraints == RigidbodyConstraints.None, "Settled book was forcibly resized or frozen");
            Require(book.WorldBounds.min.y > -.06f && book.WorldBounds.min.y < .15f, "Dropped book did not collide with the floor");
            Require(book.body.linearVelocity.magnitude < .12f && book.body.angularVelocity.magnitude < .12f, "Dropped book did not settle");
            Require(Quaternion.Angle(airRotation, book.transform.rotation) > 5, "Tilted book did not rotate physically");
            Capture("fixture-landed-book"); Check("Enlarged tilted book fell " + fallDistance + "m in 0.3s, rotated and settled on floor with unchanged size");
            perspectivePassed = true; shadowsPassed = true;
        }
        IEnumerable<object> Sequence()
        {
            yield return null; yield return null; game.ResetAll(); game.Resume();
            foreach (var s in Fixtures()) yield return s;
            game.ResetAll(); game.Resume(); routeStarted = true; stage = "continuous route";
            foreach (var s in Playthrough()) yield return s;
            Require(game.won && game.respawns == 0, "Exit was not reached continuously");
            yield return null;
            yield return new WaitForEndOfFrame();
            var finalFrame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, "library-clear-ui.png"), finalFrame.EncodeToPNG());
            Destroy(finalFrame);
        }
        IEnumerable<object> Playthrough()
        {
            foreach (var s in Wait(.15f)) yield return s;
            Capture("route-00-start");
            foreach (var s in Place("shelf-a", new Vector2(-7, 7.5f), 2, 0, 0, 1, 8)) yield return s;
            stage = "first shelf-shadow crossing";
            foreach (var s in SafeWalk(new Vector2(-7, 16.2f))) yield return s;
            Require(game.checkpoint >= 1, "First checkpoint absent"); completedZones = 1;
            Check("First room crossed through an enlarged bookshelf shadow"); Look(Prop("shelf-a").WorldBounds.center); Capture("route-01-shelf-crossing");
            // Free both volumes from the case into the open reading area first. Placing the
            // farther shadow book before the nearer one avoids projecting through the latter.
            foreach (var s in Place("hero-book-a", new Vector2(6, 16.8f), 2, 0, 0, 14.6f, 16.8f)) yield return s;
            foreach (var s in Place("hero-book-b", new Vector2(4, 16.8f), 2, 0, 0, 14.6f, 16.8f)) yield return s;
            foreach (var s in Place("hero-book-b", new Vector2(6, 23.15f), 4.7f, 0, 0, 14.6f, 19.5f)) yield return s;
            foreach (var s in Place("hero-book-a", new Vector2(6, 19.0f), 5.5f, 0, 0, 14.6f, 19.5f)) yield return s;
            stage = "individual-book shadow relay";
            foreach (var s in SafeWalk(new Vector2(6, 29))) yield return s;
            Require(game.checkpoint >= 2, "Second checkpoint absent"); completedZones = 2;
            Check("Two separate shelf books enlarged and linked across the second room"); Look(Prop("hero-book-b").WorldBounds.center); Capture("route-02-book-relay");
            foreach (var s in Place("table-a", new Vector2(-5, 33.55f), 3, 0, 0, 27.5f, 33)) yield return s;
            stage = "walk under enlarged table canopy";
            foreach (var s in SafeWalk(new Vector2(-5, 42))) yield return s;
            Require(game.checkpoint >= 3, "Third checkpoint absent"); completedZones = 3;
            Check("Third room crossed beneath a physically enlarged table"); Look(Prop("table-a").WorldBounds.center); Capture("route-03-table-canopy");
            foreach (var s in Place("shelf-b", new Vector2(-5, 42.8f), 2, 0, 0, 40.45f, 43)) yield return s;
            stage = "final shelf first crossing";
            foreach (var s in SafeWalk(new Vector2(-5, 48))) yield return s;
            Check("Final room intermediate island reached using the first placement of the shelf"); Look(Prop("shelf-b").WorldBounds.center); Capture("route-04-island");
            foreach (var s in Place("shelf-b", new Vector2(6, 48.4f), 2, 0, 0, 47.5f, 49)) yield return s;
            stage = "reused shelf final crossing";
            foreach (var s in SafeWalk(new Vector2(6, 53.3f))) yield return s;
            Require(game.won && game.respawns == 0, "Exit not reached"); completedZones = 4;
            Look(new Vector3(6,1.65f,54));
            Check("Full four-room route cleared from original spawn, reusing the same final shelf; no teleport, reset, direct object pose/scale/depth write or exposure override");
        }
        Vector3 CenterFor(LibraryProp prop, Vector2 position, float support, float scale, Quaternion rotation)
        {
            float lowest = float.PositiveInfinity;
            foreach (var part in prop.parts) for (int i = 0; i < 8; i++)
            {
                var corner = part.center + Vector3.Scale(part.size * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = prop.transform.InverseTransformPoint(part.transform.TransformPoint(corner)); lowest = Mathf.Min(lowest, (rotation * (local * scale)).y);
            }
            return new Vector3(position.x, support + .025f - lowest, position.y);
        }
        bool Residual(LibraryProp prop, Vector3 aim, Vector3 target, Quaternion rotation, float scale, Vector3 camera, out float residual)
        {
            residual = 0; var ray = aim - camera;
            if (!Physics.Raycast(camera, ray.normalized, out var hit, LibraryGame.Reach, ~0, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<LibraryProp>() != prop) return false;
            var local = prop.transform.InverseTransformPoint(hit.point); var anchor = target + rotation * (local * scale);
            residual = Vector3.Distance(camera, anchor) - hit.distance * scale / prop.transform.localScale.x; return true;
        }
        Vector2 FindVantage(LibraryProp prop, Vector3 aim, Vector3 target, Quaternion rotation, float scale, float zMin, float zMax)
        {
            float eye = game.view.transform.position.y, best = float.PositiveInfinity; Vector2 result = Vector2.zero; bool found = false;
            // A book between shelf sides must first be pulled straight through the open front.
            // Solve the camera's z coordinate with x aligned to its spine before trying diagonal views.
            if (prop.kind == "Book" || prop.kind == "Table" || prop.objectId == "shelf-b") for (float z = zMin; z < Mathf.Min(zMax, aim.z); z += .04f)
            {
                var a = new Vector3(aim.x, eye, z); var b = new Vector3(aim.x, eye, z + .04f);
                if (!Residual(prop, aim, target, rotation, scale, a, out float ra) || !Residual(prop, aim, target, rotation, scale, b, out float rb) || ra * rb > 0) continue;
                for (int k = 0; k < 20; k++)
                {
                    var middle = (a + b) * .5f; if (!Residual(prop, aim, target, rotation, scale, middle, out float rm)) break;
                    if (ra * rm <= 0) { b = middle; rb = rm; } else { a = middle; ra = rm; }
                }
                var c = (a + b) * .5f; var feet = c - Vector3.up * Rules.EyeHeight;
                if (game.CanStand(feet) && game.FootSafe(feet)) return new Vector2(c.x, c.z);
            }
            for (float z = zMin; z <= zMax; z += .25f) for (float x = -12.5f; x < 12.5f; x += .25f)
            {
                var a = new Vector3(x, eye, z); var b = new Vector3(x + .25f, eye, z);
                if (!Residual(prop, aim, target, rotation, scale, a, out float ra) || !Residual(prop, aim, target, rotation, scale, b, out float rb) || ra * rb > 0) continue;
                for (int k = 0; k < 18; k++)
                {
                    var middle = (a + b) * .5f; if (!Residual(prop, aim, target, rotation, scale, middle, out float rm)) break;
                    if (ra * rm <= 0) { b = middle; rb = rm; } else { a = middle; ra = rm; }
                }
                var c = (a + b) * .5f; var feet = c - Vector3.up * Rules.EyeHeight;
                if (!game.CanStand(feet) || !game.FootSafe(feet)) continue;
                var xz = new Vector2(c.x, c.z); float distance = Vector2.Distance(game.player.Point, xz);
                if (distance < best) { best = distance; result = xz; found = true; }
            }
            Require(found, "No safe camera-only perspective vantage for " + prop.objectId + " scale=" + scale + " target=" + target); return result;
        }
        IEnumerable<object> Place(string id, Vector2 destination, float scale, float support, float yaw, float zMin, float zMax)
        {
            var prop = Prop(id); stage = "place " + id; var rotation = Quaternion.Euler(0, yaw, 0);
            var target = CenterFor(prop, destination, support, scale, rotation);
            // Select the largest collider, usually the solid shelf rear panel or table top.
            BoxCollider sourcePart = prop.parts[0]; float area = 0;
            foreach (var part in prop.parts) { float a = part.size.x * part.size.y + part.size.x * part.size.z + part.size.y * part.size.z; if (a > area) { area = a; sourcePart = part; } }
            var sourceAim = sourcePart.bounds.center;
            var vantage = FindVantage(prop, sourceAim, target, rotation, scale, zMin, zMax);
            foreach (var s in SafeWalk(vantage)) yield return s;
            Physics.SyncTransforms(); Look(sourcePart.bounds.center); game.Interact();
            Require(game.held == prop, "Planned camera ray did not grab " + id + "; aimed=" + (game.aimed == null ? "null" : game.aimed.objectId) + "; held=" + (game.held == null ? "null" : game.held.objectId) + "; active=" + game.active + "; current=" + prop.transform.position + "; aim=" + sourcePart.bounds.center + "; " + game.notice);
            // Rotation is a normal player control; distance is calculated only by UpdateHeldPose.
            game.RotateHeld(Mathf.DeltaAngle(game.heldRotation.eulerAngles.y, yaw), -Mathf.DeltaAngle(0, game.heldRotation.eulerAngles.x));
            var anchor = target + game.heldRotation * (game.GrabLocalPoint * scale);
            AimTarget(anchor);
            if (game.PlacementBlocked)
            {
                float away = Mathf.Atan2(game.view.transform.position.x - prop.transform.position.x, game.view.transform.position.z - prop.transform.position.z);
                Aim(away, -.6f); AimTarget(anchor);
            }
            if (game.PlacementBlocked)
            {
                var current = (prop.transform.TransformPoint(game.GrabLocalPoint) - game.view.transform.position).normalized;
                float currentYaw = Mathf.Atan2(current.x, current.z);
                Look(anchor); float destinationYaw = game.yaw, destinationPitch = game.pitch;
                AimGradually(currentYaw, .9f); AimGradually(destinationYaw, .9f); AimGradually(destinationYaw, destinationPitch);
            }
            Require(!game.PlacementBlocked && !game.invalid, "Camera-only placement blocked for " + id + "; actual=" + prop.transform.position + "; " + game.invalidReason);
            Require(Vector3.Distance(prop.transform.position, target) < .45f, "Camera-only target mismatch " + id + ": actual=" + prop.transform.position + " expected=" + target);
            Require(Mathf.Abs(prop.transform.localScale.x - scale) < .12f, "Camera-only scale mismatch for " + id + ": " + prop.transform.localScale.x + " expected=" + scale);
            var size = prop.transform.localScale; var release = prop.transform.position; Require(game.Drop(), "Cannot release " + id);
            Require(Vector3.Distance(prop.transform.position, release) < .0001f, "Drop changed displayed pose");
            foreach (var s in Wait(1.2f)) yield return s;
            Require(prop.transform.localScale == size && !prop.body.isKinematic, "Released scale/physics changed");
            Check("Camera-only placement " + id + ": vantage=" + vantage + ", actual=" + prop.transform.position + ", scale=" + size.x);
        }
        bool SafeSegment(Vector2 from, Vector2 to)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .15f));
            for (int i = 1; i <= count; i++) { var p = Vector2.Lerp(from, to, (float)i / count); var feet = new Vector3(p.x, .025f, p.y); if (!game.CanStand(feet) || !game.FootSafe(feet)) return false; }
            return true;
        }
        IEnumerable<object> SafeWalk(Vector2 target)
        {
            if (SafeSegment(game.player.Point, target)) { foreach (var s in Walk(target.x, target.y)) yield return s; yield break; }
            const float grid = .4f; int width = 66, depth = 136;
            Func<int, Vector2> point = id => new Vector2(-13 + (id % width) * grid, .2f + (id / width) * grid);
            Func<Vector2, int> index = p => Mathf.Clamp(Mathf.RoundToInt((p.y - .2f) / grid), 0, depth - 1) * width + Mathf.Clamp(Mathf.RoundToInt((p.x + 13) / grid), 0, width - 1);
            int start = index(game.player.Point), goal = index(target); var open = new List<int> { start }; var parent = new Dictionary<int, int>();
            var cost = new Dictionary<int, float> { [start] = 0 }; var closed = new HashSet<int>(); bool found = false;
            int[] ox = { -1, 0, 1, -1, 1, -1, 0, 1 }, oz = { -1, -1, -1, 0, 0, 1, 1, 1 };
            while (open.Count > 0)
            {
                int best = 0; float bestScore = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++) { float score = cost[open[i]] + Vector2.Distance(point(open[i]), target); if (score < bestScore) { best = i; bestScore = score; } }
                int current = open[best]; open.RemoveAt(best); if (!closed.Add(current)) continue;
                if ((current == goal || Vector2.Distance(point(current), target) < .65f) && SafeSegment(point(current), target)) { goal = current; found = true; break; }
                for (int i = 0; i < 8; i++)
                {
                    int x = current % width + ox[i], z = current / width + oz[i]; if (x < 0 || x >= width || z < 0 || z >= depth) continue;
                    int next = z * width + x; if (closed.Contains(next) || !SafeSegment(point(current), point(next))) continue;
                    float proposed = cost[current] + Vector2.Distance(point(current), point(next));
                    if (cost.TryGetValue(next, out float existing) && existing <= proposed) continue;
                    parent[next] = current; cost[next] = proposed; if (!open.Contains(next)) open.Add(next);
                }
                if (closed.Count % 200 == 0) yield return null;
            }
            Require(found, "No collision-free shadow route to " + target + "; explored=" + closed.Count);
            var path = new List<Vector2> { target }; for (int node = goal; node != start; node = parent[node]) path.Add(point(node)); path.Reverse();
            int at = 0;
            while (at < path.Count)
            {
                int far = at; while (far + 1 < path.Count && SafeSegment(game.player.Point, path[far + 1])) far++;
                foreach (var s in Walk(path[far].x, path[far].y)) yield return s; at = far + 1;
            }
        }
    }
}
