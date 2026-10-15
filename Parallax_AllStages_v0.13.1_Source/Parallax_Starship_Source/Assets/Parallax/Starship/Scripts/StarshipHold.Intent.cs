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
        public Vector3[] corners;
        public float ratio, radius, desired, lastIntent, minDepth, maxDepth, roomMin, roomMax;
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
            desired = lastIntent = value.referenceDistance;
            var vertices = new List<Vector3>();
            shapes = new Shape[prop.parts.Length];
            for (int i = 0; i < shapes.Length; i++)
            {
                BoxCollider part = prop.parts[i];
                shapes[i] = new Shape { center = prop.transform.InverseTransformPoint(part.transform.TransformPoint(part.center)),
                    half = Vector3.Scale(part.size * .5f, Abs(part.transform.lossyScale)) / value.referenceScale,
                    rotation = Quaternion.Inverse(prop.transform.rotation) * part.transform.rotation, collider = part };
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = shapes[i].center + shapes[i].rotation * Vector3.Scale(shapes[i].half,
                        new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    vertices.Add(corner); radius = Mathf.Max(radius, (corner-value.GrabLocalPoint).magnitude);
                }
            }
            corners = vertices.ToArray();
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
        s.eye = game.view.transform.position; s.forward = game.view.transform.forward; s.requestedRotation = game.heldRotation;
        s.minDepth = game.held.minScale / s.ratio;
        s.maxDepth = game.held.maxScale / s.ratio;
        s.roomMin = s.minDepth; s.roomMax = s.maxDepth;
        RaycastHit selected = default(RaycastHit);
        bool surface = CenterSurface(s, out selected);
        float intent = s.lastIntent;
        if (surface)
        {
            intent = selected.distance;
            // All support points are corrected against THIS selected plane,
            // never against an unrelated peripheral ray's near obstacle.
            float numerator = Vector3.Dot(selected.normal, selected.point-s.eye) + .02f;
            foreach (Vector3 local in s.corners)
            {
                Vector3 velocity = s.forward + s.requestedRotation * ((local-game.GrabLocalPoint) * s.ratio);
                float denominator = Vector3.Dot(selected.normal, velocity);
                if (denominator < -1e-6f) intent = Mathf.Min(intent, numerator/denominator);
            }
            s.lastIntent = Mathf.Clamp(intent, s.minDepth, s.maxDepth);
        }
        Bounds room=StarshipWorld.ZoneBounds(game.held.zone);
        foreach (Vector3 local in s.corners)
        {
            Vector3 v = s.forward + s.requestedRotation * ((local-game.GrabLocalPoint) * s.ratio);
            Clip(s.eye.x,v.x,room.min.x+.01f,room.max.x-.01f,ref s.roomMin,ref s.roomMax);
            Clip(s.eye.y,v.y,room.min.y+.05f,room.max.y-.01f,ref s.roomMin,ref s.roomMax);
            Clip(s.eye.z,v.z,room.min.z+.01f,room.max.z-.01f,ref s.roomMin,ref s.roomMax);
        }
        s.feasible = s.roomMin <= s.roomMax;
        s.desired = s.feasible ? Mathf.Clamp(s.lastIntent,s.roomMin,s.roomMax) : Mathf.Clamp(s.lastIntent,s.minDepth,s.maxDepth);
        s.setDepth(s.desired); s.setSurface(surface);
    }
    static bool CenterSurface(Session s, out RaycastHit selected)
    {
        bool found=InteractionRaycast.Nearest(s.eye,s.forward,s.game.controller,s.prop.transform,Mask,
            ref s.hits,out selected,out int queries);
        s.queries+=queries;return found;
    }
    internal static bool Own(Session s, Collider other)
    { return other.GetComponentInParent<StarshipProp>() == s.prop; }
    internal static void Clip(float origin,float velocity,float min,float max,ref float lo,ref float hi)
    {
        InteractionRaycast.ClipDepth(origin,velocity,min,max,ref lo,ref hi);
    }
}
