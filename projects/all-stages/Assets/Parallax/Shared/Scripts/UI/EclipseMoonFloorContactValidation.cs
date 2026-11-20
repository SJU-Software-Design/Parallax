using System;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Position/view fixtures exercise the original source Moon through public
    // interaction and game ticks. No injected Moon poses, OS input or rendering.
    public static class EclipseMoonFloorContactValidation
    {
        const float Dt = 1f / 60;
        const string Fixture = "Declared feet/view fixture; actual source Moon, production hold/drop/fall/cancel; no capture or OS input";

        sealed class Observations
        {
            public float ratioError, minimumMeshY = float.PositiveInfinity, minimumScale = float.PositiveInfinity;
            public float maximumScale, maximumDepth, travel, maximumHeadingError, maximumFrameTravel, maximumDepthChange, maximumHorizontalRadius;
            public float minimumHorizontalRadius = float.PositiveInfinity;
            public int frames, blocked, constrained;
            public bool clear = true, allVisible = true;
            public float maximumViewAngle;
            Vector3 previous;
            float previousDepth;
            bool observed;
            public void Sample(EclipseFinaleGame game, float expectedRatio, Vector3[] vertices, float outerRadius, float expectedYaw)
            {
                frames++;
                Vector3 position = game.moon.position;
                float depth = Vector3.Distance(game.view.transform.position, position);
                maximumViewAngle = Mathf.Max(maximumViewAngle, Vector3.Angle(position - game.view.transform.position, game.view.transform.forward));
                Vector3 horizontal = Vector3.ProjectOnPlane(position - game.view.transform.position, Vector3.up);
                minimumHorizontalRadius = Mathf.Min(minimumHorizontalRadius, horizontal.magnitude);
                maximumHorizontalRadius = Mathf.Max(maximumHorizontalRadius, horizontal.magnitude);
                Vector3 heading = new Vector3(Mathf.Sin(expectedYaw * Mathf.Deg2Rad), 0, Mathf.Cos(expectedYaw * Mathf.Deg2Rad));
                maximumHeadingError = Mathf.Max(maximumHeadingError, Vector3.Angle(horizontal, heading));
                ratioError = Mathf.Max(ratioError, Mathf.Abs(game.moon.localScale.x / depth / expectedRatio - 1));
                minimumScale = Mathf.Min(minimumScale, game.moon.localScale.x);
                maximumScale = Mathf.Max(maximumScale, game.moon.localScale.x);
                maximumDepth = Mathf.Max(maximumDepth, depth);
                if (game.MoonPlacementBlocked) blocked++;
                if (game.MoonFloorConstrained) constrained++;
                if (observed)
                {
                    float step = Vector3.Distance(position, previous);
                    travel += step; maximumFrameTravel = Mathf.Max(maximumFrameTravel, step);
                    maximumDepthChange = Mathf.Max(maximumDepthChange, Mathf.Abs(depth - previousDepth));
                }
                previous = position; previousDepth = depth; observed = true;
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 point = game.moon.TransformPoint(vertex);
                    minimumMeshY = Mathf.Min(minimumMeshY, point.y);
                    Vector3 projected = game.view.WorldToViewportPoint(point);
                    allVisible &= projected.z > game.view.nearClipPlane &&
                        projected.x >= 0 && projected.x <= 1 && projected.y >= 0 && projected.y <= 1;
                }
                clear &= !Physics.CheckSphere(position, Mathf.Max(.0001f, outerRadius * game.moon.localScale.x - .002f),
                    EclipseFinaleWorld.MoonSupportMask, QueryTriggerInteraction.Ignore);
            }
            public string Details => "frames=" + frames + " blocked=" + blocked + " constrained=" + constrained +
                " travel=" + travel.ToString("F4") + " minHorizontalRadius=" + minimumHorizontalRadius.ToString("F4") +
                " maxFrameTravel=" + maximumFrameTravel.ToString("F4") + " maxDepthChange=" + maximumDepthChange.ToString("F4") +
                " maxHeadingError=" + maximumHeadingError.ToString("F5") + " meshMinY=" + minimumMeshY.ToString("F5") +
                " maxViewAngle=" + maximumViewAngle.ToString("F5") + " visible=" + allVisible +
                " ratioError=" + ratioError.ToString("F7") + " scale=" + minimumScale.ToString("F4") + ".." +
                maximumScale.ToString("F4") + " maxDepth=" + maximumDepth.ToString("F4");
        }

        public static void Run(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            bool allowed = game != null && Application.isBatchMode &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-test") >= 0;
            check(allowed, "Moon floor/wall contact fixtures are restricted to the isolated batch validation player", Fixture);
            if (!allowed) return;
            bool automated = game.automated, started = game.started, paused = game.paused;
            float fov = game.view.fieldOfView;
            var originalSettings = GameShellSettingsRuntime.Current.DeepCopy();
            Vector3[] vertices = game.moon.GetComponent<MeshFilter>().sharedMesh.vertices;
            float outerRadius = .5f;
            foreach (Vector3 vertex in vertices) outerRadius = Mathf.Max(outerRadius, vertex.magnitude);
            float wallHeight = 0, wallFront = float.PositiveInfinity;
            int solidLowWalls = 0;
            foreach (BoxCollider collider in game.world.ShelterRoofColliders)
                if (collider.enabled && collider.gameObject.layer == 8 && collider.bounds.size.y < 1.5f)
                {
                    solidLowWalls++;
                    wallHeight = Mathf.Max(wallHeight, collider.bounds.size.y);
                    wallFront = Mathf.Min(wallFront, collider.bounds.min.z);
                }
            check(solidLowWalls == 2 && wallHeight >= 1,
                "Contact validation retains the two real solid low shade walls before credits",
                "walls=" + solidLowWalls + " height=" + wallHeight + " frontZ=" + wallFront);
            try
            {
                foreach (string sample in new[] { "near 1.5m", "ordinary solar pickup", "far 4m", "real Reset spawn / default FOV70" })
                {
                    bool realSpawn = sample == "real Reset spawn / default FOV70";
                    if (realSpawn)
                    {
                        var defaultViewSettings = originalSettings.DeepCopy();
                        defaultViewSettings.fieldOfView = 70;
                        GameShellSettingsRuntime.Apply(defaultViewSettings, false);
                    }
                    game.ResetGame(true); game.automated = true;
                    Vector3 sourcePosition = game.moon.position, sourceScale = game.moon.localScale;
                    Quaternion sourceRotation = game.moon.rotation;
                    if (!realSpawn)
                    {
                        Vector3 feet = sample == "ordinary solar pickup" ? EclipsePickupContinuityValidation.SolvableMoonPickupFeet(game) :
                            PickupFeet(game, sample == "near 1.5m" ? 1.5f : 4);
                        game.Teleport(feet);
                    }
                    else
                        check(Vector3.Distance(game.body.transform.position, game.Spawn) < .0001f &&
                            Mathf.Abs(game.view.fieldOfView - 70) < .001f &&
                            Mathf.Abs(GameShellSettingsRuntime.Current.fieldOfView - 70) < .001f,
                            "The opening-floor case starts at the actual Reset Spawn and default 70 FOV without fixture teleport",
                            "feet=" + game.body.transform.position + " FOV=" + game.view.fieldOfView + " activeSetting=" + GameShellSettingsRuntime.Current.fieldOfView);
                    Tick(game, 15);
                    game.AimAt(game.moon.position);
                    float ratio = game.moon.localScale.x / Vector3.Distance(game.view.transform.position, game.moon.position);
                    game.Interact();
                    check(game.held == FinaleItem.Moon, "Floor contact begins with public pickup of the unchanged source Moon: " + sample, Fixture);

                    if (realSpawn)
                    {
                        var straightDown = new Observations();
                        Aim(game, 0, -80);
                        for (int frame = 0; frame < 60; frame++)
                        { game.Tick(Dt, Vector2.zero, false); straightDown.Sample(game, ratio, vertices, outerRadius, 0); }
                        check(straightDown.blocked == 0 && straightDown.maximumViewAngle < .15f,
                            "The real opening Spawn can aim 80 degrees down with the Moon centred instead of frozen or off screen",
                            straightDown.Details);
                        CheckSafe(straightDown, "real opening Spawn at FOV70 / forward ground", check);
                    }

                    var floor = new Observations();
                    for (int step = 0; step <= 20; step++)
                    {
                        // Aim across open ground beside the low wall, then sweep
                        // a 40-degree arc. A fixed world pose cannot pass.
                        Aim(game, 70 + step * 2, -80);
                        for (int frame = 0; frame < 3; frame++)
                        { game.Tick(Dt, Vector2.zero, false); floor.Sample(game, ratio, vertices, outerRadius, 70 + step * 2); }
                    }
                    check(floor.blocked == 0 && floor.maximumHeadingError < .15f &&
                        floor.travel >= floor.minimumHorizontalRadius * 40 * Mathf.Deg2Rad * .95f,
                        "Steep ground aim remains movable along the floor: " + sample, floor.Details);
                    CheckSafe(floor, sample + " open floor", check);

                    var wall = new Observations();
                    for (int step = 0; step <= 26; step++)
                    {
                        // This crosses the real 1.25m layer8 front shade wall
                        // in both lateral directions while it is still solid.
                        Aim(game, -65 + step * 5, -45);
                        for (int frame = 0; frame < 3; frame++)
                        { game.Tick(Dt, Vector2.zero, false); wall.Sample(game, ratio, vertices, outerRadius, -65 + step * 5); }
                    }
                    check(wall.blocked == 0 && wall.maximumHeadingError < .15f &&
                        wall.travel >= wall.minimumHorizontalRadius * 130 * Mathf.Deg2Rad * .95f,
                        "Downward wall contact accepts lateral control without an indefinite frozen pose: " + sample, wall.Details);
                    CheckSafe(wall, sample + " actual low wall", check);
                    CheckWallContinuity(wall, wallHeight, 5, sample + " original pickup position", check);
                    if (sample == "far 4m")
                    {
                        check(floor.constrained > 0 && wall.constrained > 0,
                            "The far-pickup fixtures actually require floor and low-wall contact recovery",
                            "floor=" + floor.constrained + " wall=" + wall.constrained);
                        float beforeZ = game.body.transform.position.z;
                        Aim(game, 0, -55);
                        var approach = new Observations();
                        for (int frame = 0; frame < 120; frame++)
                        {
                            game.Tick(Dt, Vector2.up, false);
                            approach.Sample(game, ratio, vertices, outerRadius, 0);
                        }
                        float stoppedZ = game.body.transform.position.z;
                        check(stoppedZ - beforeZ > .7f && stoppedZ < wallFront &&
                            wallFront - stoppedZ < game.body.radius + .15f,
                            "Actual CharacterController movement reaches and is stopped by the low wall while holding the Moon",
                            "beforeZ=" + beforeZ + " stoppedZ=" + stoppedZ + " wallFrontZ=" + wallFront);
                        CheckSafe(approach, "actual walking toward the solid low wall", check);
                        var adjacent = new Observations();
                        for (int step = 0; step <= 20; step++)
                        {
                            Aim(game, -50 + step * 5, -55);
                            for (int frame = 0; frame < 3; frame++)
                            { game.Tick(Dt, Vector2.zero, false); adjacent.Sample(game, ratio, vertices, outerRadius, -50 + step * 5); }
                        }
                        check(adjacent.blocked == 0 && adjacent.maximumHeadingError < .15f &&
                            adjacent.travel >= adjacent.minimumHorizontalRadius * 100 * Mathf.Deg2Rad * .95f,
                            "Ground-directed lateral control remains responsive immediately beside the real low wall",
                            adjacent.Details);
                        CheckSafe(adjacent, "beside the actual low wall", check);
                        CheckWallContinuity(adjacent, wallHeight, 5, "beside the actual low wall", check);
                    }

                    game.AimAt(EclipseFinaleGame.SunPosition); Tick(game, 180);
                    float centreError = Vector3.Angle(game.moon.position - game.view.transform.position, game.view.transform.forward);
                    check(!game.MoonPlacementBlocked && !game.MoonFloorConstrained && centreError < .15f &&
                        Mathf.Abs(game.moon.localScale.x / Vector3.Distance(game.view.transform.position, game.moon.position) / ratio - 1) < .005f,
                        "Lifting the view restores normal centred perspective control after floor/wall contact: " + sample,
                        "centreDegrees=" + centreError);

                    game.Cancel();
                    check(game.held == FinaleItem.None && Vector3.Distance(game.moon.position, sourcePosition) < .0001f &&
                        Vector3.Distance(game.moon.localScale, sourceScale) < .0001f && Quaternion.Angle(game.moon.rotation, sourceRotation) < .01f,
                        "Cancel restores the exact original Moon after floor and wall sweeps: " + sample, "");

                    game.AimAt(game.moon.position); game.Interact();
                    Aim(game, 90, -80); Tick(game, 120);
                    Vector3 underfootPosition = game.moon.position, underfootScale = game.moon.localScale;
                    check(!game.Drop() && game.held == FinaleItem.Moon &&
                        Vector3.Distance(game.moon.position, underfootPosition) < .0001f &&
                        Vector3.Distance(game.moon.localScale, underfootScale) < .0001f,
                        "Dropping into the real foot capsule is refused without changing the held Moon: " + sample, Fixture);
                    Aim(game, 90, -50); Tick(game, 120);
                    float releasedScale = game.moon.localScale.x;
                    check(game.Drop() && game.held == FinaleItem.None && !game.MoonAnchored && !PlayerTouchesMoon(game),
                        "A slightly farther clear floor point accepts release with the actual player capsule clear: " + sample, Fixture);
                    Tick(game, 120);
                    float meshY = float.PositiveInfinity;
                    foreach (Vector3 vertex in vertices) meshY = Mathf.Min(meshY, game.moon.TransformPoint(vertex).y);
                    Vector3 restingPosition = game.moon.position;
                    Tick(game, 120);
                    check(!game.MoonFalling && meshY >= -.0005f &&
                        Vector3.Distance(game.moon.position, restingPosition) < .001f && Mathf.Abs(game.moon.localScale.x - releasedScale) < .0001f,
                        "Released Moon settles above the deck without sinking or creeping: " + sample,
                        "meshMinY=" + meshY + " settled=" + !game.MoonFalling);
                    Vector3 beforeWalking = game.body.transform.position;
                    bool capsuleClear = !PlayerTouchesMoon(game);
                    for (int frame = 0; frame < 36; frame++)
                    {
                        game.Tick(Dt, Vector2.down, false);
                        capsuleClear &= !PlayerTouchesMoon(game);
                    }
                    check(capsuleClear && Vector3.Distance(beforeWalking, game.body.transform.position) > .7f,
                        "Actual CharacterController steps freely away from the released floor Moon without a foot trap: " + sample,
                        "distance=" + Vector3.Distance(beforeWalking, game.body.transform.position));
                    game.AimAt(game.moon.position); game.Interact();
                    check(game.held == FinaleItem.Moon, "A floor-settled Moon remains publicly pickable: " + sample, "");
                    game.Cancel();
                }
                GameShellSettingsRuntime.Apply(originalSettings, false);
                check(Mathf.Abs(game.view.fieldOfView - fov) < .001f &&
                    Mathf.Abs(GameShellSettingsRuntime.Current.fieldOfView - originalSettings.fieldOfView) < .001f,
                    "Moon contact fixtures restore actual shared settings and camera FOV after the default-70 fixture", "");
            }
            finally
            {
                GameShellSettingsRuntime.Apply(originalSettings, false);
                game.ResetGame(started);
                if (paused) game.Pause(); else if (started) game.Play();
                game.view.fieldOfView = fov; game.automated = automated;
            }
        }

        static void CheckWallContinuity(Observations sample, float wallHeight, float yawStep,
            string label, Action<bool, string, string> check)
        {
            // A five-degree look step may raise the Moon over this physical
            // wall and move it around the observer. It must not launch several
            // metres down a newly clear ray. This checks observed frame motion,
            // using measured world geometry rather than solver return values.
            float lateralStep = 2 * sample.maximumHorizontalRadius * Mathf.Sin(yawStep * Mathf.Deg2Rad * .5f);
            check(sample.maximumDepthChange <= wallHeight + .05f &&
                sample.maximumFrameTravel <= wallHeight + lateralStep + .05f,
                "Continuous wall aiming avoids sudden distant jumps: " + label,
                sample.Details + " physicalWallHeight=" + wallHeight + " lateralStep=" + lateralStep);
        }

        static void CheckSafe(Observations sample, string label, Action<bool, string, string> check)
        {
            check(sample.clear && sample.minimumMeshY >= -.0005f,
                "Every held frame clears real structural collision and the unchanged visible mesh stays above the deck: " + label, sample.Details);
            check(sample.ratioError <= .005f && sample.minimumScale >= .049f && sample.maximumScale <= 48.001f && sample.maximumDepth <= 160.001f,
                "Floor/wall adjustment preserves pickup angular size and bounded ground-only size limits: " + label, sample.Details);
            check(sample.allVisible,
                "The entire actual Moon mesh remains inside the applied camera viewport during floor/wall control: " + label, sample.Details);
        }

        static bool PlayerTouchesMoon(EclipseFinaleGame game)
        {
            Vector3 centre = game.body.transform.TransformPoint(game.body.center);
            float halfSegment = Mathf.Max(0, game.body.height * .5f - game.body.radius);
            foreach (Collider collider in Physics.OverlapCapsule(centre - Vector3.up * halfSegment,
                centre + Vector3.up * halfSegment, game.body.radius, ~0, QueryTriggerInteraction.Ignore))
                if (collider.transform == game.moon || collider.transform.IsChildOf(game.moon)) return true;
            return false;
        }

        static Vector3 PickupFeet(EclipseFinaleGame game, float distance)
        {
            float vertical = .04f + EclipseFinaleGame.EyeHeight - game.moon.position.y;
            return new Vector3(game.moon.position.x - Mathf.Sqrt(Mathf.Max(.01f, distance * distance - vertical * vertical)), .04f, game.moon.position.z);
        }
        static void Aim(EclipseFinaleGame game, float yaw, float elevation)
        {
            float angle = elevation * Mathf.Deg2Rad, heading = yaw * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(heading) * Mathf.Cos(angle), Mathf.Sin(angle), Mathf.Cos(heading) * Mathf.Cos(angle));
            game.AimAt(game.view.transform.position + direction * 100);
        }
        static void Tick(EclipseFinaleGame game, int frames)
        { for (int i = 0; i < frames; i++) game.Tick(Dt, Vector2.zero, false); }
    }
}