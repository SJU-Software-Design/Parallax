using System;
using System.Collections.Generic;
using ShadeLink.Starship;
using ShadeLink;
using UnityEngine;

// Companion patch: the shipped Prop, support, water and release code stay intact.
public static partial class StarshipHold
{
    internal const int Mask = -1073741825;
    static readonly Dictionary<StarshipGame, Session> Sessions = new Dictionary<StarshipGame, Session>();
    internal struct Shape { public Vector3 center, half; public Quaternion rotation; public BoxCollider collider; }
    internal struct Pose
    { public Vector3 position, direction; public Quaternion rotation; public float depth, scale; }
    internal sealed class Session
    {
        public StarshipGame game;
        public StarshipProp prop;
        public Shape[] shapes;
        public Vector3 shapeRatio, grab;
        public float ratio, radius, desired, minDepth, maxDepth, roomMin, roomMax;
        public bool feasible;
        public Action<float> setDepth;
        public Action<bool> setSurface, setBlocked;
        public Action<string> setObstacle;
        public Func<BoxCollider, bool> intersectsPlayer;
        public RaycastHit[] hits = new RaycastHit[64];
        public Collider[] overlaps = new Collider[64];
        public string status = "Tracking";
        public int queries = 0;
        public Vector3 eye, forward;
        public Quaternion requestedRotation;
        public int routeStage = -1, routeCandidate;
        public float routeBase, routeDepth;
        public Vector3 routeDirection;
        public Vector3 routeEye, routeAim;
        public Quaternion routeRotation;
        public float routeRequest;
        public bool exhausted;
        public bool finishing;
        public string obstacle;
        public readonly List<Pose> history = new List<Pose>();
        public int backSteps;

        public Session(StarshipGame value)
        {
            game = value; prop = value.held;
            setDepth = x=>value.holdDistance=x;
            setSurface = x=>value.HasPlacementSurface=x;
            setBlocked = x=>value.PlacementBlocked=x;
            setObstacle = x=>value.PlacementObstacle=x;
            intersectsPlayer = value.IntersectsPlayer;
            ratio = value.referenceScale / Mathf.Max(.01f, value.referenceDistance);
            shapeRatio = prop.transform.localScale / value.referenceScale;
            grab = Vector3.Scale(value.GrabLocalPoint, shapeRatio);
            desired = value.referenceDistance;
            shapes = new Shape[prop.parts.Length];
            for (int i = 0; i < shapes.Length; i++)
            {
                BoxCollider part = prop.parts[i];
                shapes[i] = new Shape { center = Vector3.Scale(prop.transform.InverseTransformPoint(part.transform.TransformPoint(part.center)), shapeRatio),
                    half = Vector3.Scale(part.size * .5f, Abs(part.transform.lossyScale)) / value.referenceScale,
                    rotation = Quaternion.Inverse(prop.transform.rotation) * part.transform.rotation, collider = part };
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = shapes[i].center + shapes[i].rotation * Vector3.Scale(shapes[i].half,
                        new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    radius = Mathf.Max(radius, (corner-grab).magnitude);
                }
            }
        }
    }
    
    internal static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }
    public static void Clear(StarshipGame game) { Sessions.Remove(game); QueuedDrops.Remove(game); }
    public static void Begin(StarshipGame game) { Sessions[game] = new Session(game); }
    internal static Session Get(StarshipGame game)
    {
        Session s;
        if (!Sessions.TryGetValue(game, out s) || s.prop != game.held) { Begin(game); s = Sessions[game]; }
        return s;
    }
    public static float RequestedScale(StarshipGame game)
    { return game.held == null ? 1f : Get(game).desired * Get(game).ratio; }

    public static void Resolve(StarshipGame game)
    {
        if (game.held == null) return;
        Session s = Get(game);
        s.eye = game.view.transform.position; s.forward = game.HeldDirection; s.requestedRotation = game.heldRotation;
        s.minDepth = game.held.minScale / s.ratio;
        s.maxDepth = game.held.maxScale / s.ratio;
        var result=game.ResolveSharedPlacement(StarshipWorld.ZoneBounds(game.held.zone));
        s.roomMin=result.RoomMinimum;s.roomMax=result.RoomMaximum;s.feasible=result.Feasible;
        s.desired=result.Depth;
        s.setDepth(s.desired);s.setSurface(result.HasSurface);
    }
    internal static bool Own(Session s, Collider other)
    { return other.GetComponentInParent<StarshipProp>() == s.prop; }
}
