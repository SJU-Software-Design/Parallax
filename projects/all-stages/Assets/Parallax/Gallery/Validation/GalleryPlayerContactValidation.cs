using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Gallery
{
    /// <summary>Real PhysX CharacterController contact probes on copies of the current gallery's fixed surfaces.</summary>
    public static class GalleryPlayerContactValidation
    {
        [Serializable] public sealed class Sample
        {
            public string name, mode;
            public float dt, tangentDistance, escapeDistance, maximumRise;
            public bool contacts, tangentClear, escapeClear, passed;
            public Vector3 start, contact, rubbed, escaped;
            public int copiedColliders;
            public float capsuleRadius, capsuleSkinWidth, capsuleHeight, capsuleStepOffset;
            public float tangentClearanceRadius, escapeClearanceRadius;
            public Vector3 outward, tangent;
            public ColliderDiagnostic[] actualColliders;
            public SweepDiagnostic[] clearanceDiagnostics;
        }
        [Serializable] public sealed class Report
        {
            public int samples, failures, legacyFailures, clearTangentStalls, blockedEscapes, noContact, sourceColliderCount;
            public int legacyBlockedEscapes, clearTangentSamples, escapeEligibleSamples;
            public string sourceGeometryBefore, sourceGeometryAfter;
            public bool sourcesUnchanged, galleryRecoveryDisabled;
            public bool passed;
            public Sample[] details;
        }
        [Serializable] public sealed class ColliderDiagnostic
        {
            public string path, type, mesh;
            public Vector3 center, size;
            public int triangles;
        }
        [Serializable] public sealed class HitDiagnostic
        {
            public string path;
            public float distance;
            public Vector3 point, normal;
            public bool blocksExistingCriterion;
        }
        [Serializable] public sealed class SweepDiagnostic
        {
            public string phase;
            public float radius, lift, distance;
            public Vector3 feet, direction;
            public HitDiagnostic[] hits;
        }
        [Serializable] public sealed class FailureDetails { public Sample[] failures; }
        public static Report LatestReport { get; private set; }
        struct Probe
        {
            public string name;
            public Vector3 surface, outward;
        }
        static readonly Vector3 IsolationOffset = new Vector3(2048, 0, 2048);

        public static IEnumerator Run(GalleryGame game, Action<bool, string, string> check)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (check == null) throw new ArgumentNullException(nameof(check));
            LatestReport = new Report { passed = false, details = new Sample[0] };
            bool checksPassed = true;
            Action<bool,string,string> emit = (pass, name, detail) => { checksPassed &= pass; check(pass, name, detail); };
            bool recoveryDisabled = !game.controller.enableOverlapRecovery;
            emit(recoveryDisabled, "Gallery visitor alone disables native overlap recovery", "enableOverlapRecovery=" + game.controller.enableOverlapRecovery);
            Collider[] originalShapes = game.GetComponentsInChildren<Collider>(true);
            string geometryBefore = GeometrySignature(originalShapes);
            var colliders = new List<Collider>();
            foreach (var source in game.GetComponentsInChildren<Collider>(false))
                if (source.enabled && !source.isTrigger && !(source is CharacterController) &&
                    source.attachedRigidbody == null && source.GetComponentInParent<GalleryProp>() == null &&
                    source.gameObject.layer != GalleryCathedralPortal.PreviewLayer) colliders.Add(source);
            var probes = new List<Probe>();
            bool inserted = game.GetComponent<GalleryBottleBalanceCourse>() != null;
            foreach (float z in new[] { 3.5f, 6f, 9f, 12f, 18f, 21f, 24f, 27f, 31f, 34f, 38f, 42f, 48f, 52f, 63f, 68f })
            {
                if (!inserted && z > 55) continue;
                float width = inserted && z >= GalleryBottleBalanceLayout.InsertAt && z < GalleryBottleBalanceLayout.EndZ ? 11 : 8;
                for (int side = -1; side <= 1; side += 2)
                    probes.Add(new Probe { name = "Wall " + side + " at " + z,
                        surface = game.transform.TransformPoint(new Vector3(side * width, .025f, z)),
                        outward = game.transform.TransformDirection(Vector3.right * -side) });
            }
            var exhibits = new HashSet<string>();
            foreach (var source in colliders)
            {
                string path = GalleryStaticExhibitCollision.Path(game.transform, source.transform);
                string name = path.ToLowerInvariant();
                Bounds b = source.bounds;
                if (!(name.Contains("plinth") || name.Contains("bust") || name.Contains("pedestal") ||
                    name.Contains("statue") || name.Contains("dike body"))) continue;
                if (b.min.y > game.transform.position.y + 1.3f || b.max.y < game.transform.position.y + .15f ||
                    b.size.x > 7 || b.size.z > 7) continue;
                string cell = Mathf.RoundToInt(b.center.x * 2) + ":" + Mathf.RoundToInt(b.center.z * 2);
                if (!exhibits.Add(cell)) continue;
                foreach (Vector3 outward in new[] { Vector3.left, Vector3.right, Vector3.back, Vector3.forward })
                {
                    Vector3 extent = Vector3.Scale(b.extents, new Vector3(Mathf.Abs(outward.x), 0, Mathf.Abs(outward.z)));
                    Vector3 face = b.center + Vector3.Scale(outward, extent);
                    face.y = game.transform.position.y + .025f;
                    probes.Add(new Probe { name = path, surface = face, outward = outward });
                }
                if (exhibits.Count >= 10) break;
            }
            var results = new List<Sample>(); int failures = 0, legacyFailures = 0, tangentStalls = 0, escapes = 0, noContact = 0;
            int legacyEscapes = 0, clearTangents = 0, eligibleEscapes = 0;
            foreach (var probe in probes)
            {
                foreach (float dt in new[] { 1f / 60, 1f / 144 })
                foreach (bool legacy in new[] { true, false })
                {
                    Sample sample;
                    using (var fixture = new Fixture(game, colliders, probe.surface))
                        sample = fixture.Measure(probe, dt, legacy);
                    results.Add(sample);
                    if (!sample.contacts) noContact++;
                    if (legacy && sample.contacts && sample.escapeClear && sample.escapeDistance < .35f) legacyEscapes++;
                    if (!legacy && sample.contacts && sample.tangentClear) clearTangents++;
                    if (!legacy && sample.contacts && sample.escapeClear) eligibleEscapes++;
                    if (!sample.passed && legacy) legacyFailures++;
                    if (!sample.passed && !legacy)
                    {
                        failures++;
                        if (sample.tangentClear && sample.tangentDistance < .35f) tangentStalls++;
                        if (sample.escapeClear && sample.escapeDistance < .35f) escapes++;
                    }
                    if (results.Count % 8 == 0) yield return null;
                }
            }
            // Publish complete physical samples before a caller's failing assertion can throw.
            string geometryAfter = GeometrySignature(game.GetComponentsInChildren<Collider>(true));
            bool unchanged = originalShapes.Length == game.GetComponentsInChildren<Collider>(true).Length && geometryBefore == geometryAfter;
            var failedSamples = results.FindAll(sample => sample.mode == "Gallery configured" && !sample.passed);
            LatestReport = new Report { samples = results.Count, failures = failures, legacyFailures = legacyFailures,
                legacyBlockedEscapes = legacyEscapes, clearTangentSamples = clearTangents, escapeEligibleSamples = eligibleEscapes,
                passed = checksPassed && failures == 0 && unchanged, clearTangentStalls = tangentStalls,
                blockedEscapes = escapes, noContact = noContact, details = results.ToArray(),
                sourceColliderCount = originalShapes.Length, sourceGeometryBefore = geometryBefore,
                sourceGeometryAfter = geometryAfter, sourcesUnchanged = unchanged, galleryRecoveryDisabled = recoveryDisabled };
            emit(legacyFailures > 0, "Original native recovery reproduces wall/exhibit sticking", "legacy failed contacts=" + legacyFailures);
            emit(legacyEscapes > 0, "Original native recovery reproduces blocked withdrawal from contact",
                "legacy blocked escapes=" + legacyEscapes + " current blocked escapes=" + escapes);
            emit(failures == 0, "Configured Gallery controller releases all clear wall/exhibit contacts",
                "failed=" + failures + " current samples=" + results.Count / 2 + " full-body clear tangents=" + clearTangents + " escape-eligible=" + eligibleEscapes + " " + JsonUtility.ToJson(new FailureDetails { failures = failedSamples.ToArray() }));
            foreach (Vector3 spawn in game.SpawnPoints)
                emit(game.CanStand(spawn + Vector3.up * .025f), "Gallery respawn capsule remains clear", spawn.ToString("F3"));
            if (inserted)
                emit(game.CanStand(game.bottleBalance.DemoPosition + Vector3.up * .025f),
                    "Dike ResetZone capsule remains clear", game.bottleBalance.DemoPosition.ToString("F3"));
            RunBasicMovement(game.controller.enableOverlapRecovery, emit);
            emit(unchanged, "Gallery fixed and interactive collider geometry is unchanged",
                originalShapes.Length + " colliders; " + geometryBefore + " / " + geometryAfter);
            LatestReport = new Report { samples = results.Count, failures = failures, legacyFailures = legacyFailures,
                legacyBlockedEscapes = legacyEscapes, clearTangentSamples = clearTangents, escapeEligibleSamples = eligibleEscapes,
                passed = checksPassed, clearTangentStalls = tangentStalls, blockedEscapes = escapes, noContact = noContact,
                details = results.ToArray(), sourceColliderCount = originalShapes.Length, sourceGeometryBefore = geometryBefore,
                sourceGeometryAfter = geometryAfter, sourcesUnchanged = unchanged, galleryRecoveryDisabled = recoveryDisabled };
            check(checksPassed, "Gallery native player contact regression report", JsonUtility.ToJson(LatestReport));
        }

        static void RunBasicMovement(bool recovery, Action<bool,string,string> check)
        {
            const float dt = 1f / 120;
            using (var f = new BasicFixture(recovery))
            {
                for (int i=0;i<120;i++) f.Step(Vector2.up, dt);
                check(f.LocalPosition.z > 3.4f && Mathf.Abs(f.LocalPosition.y) < .1f,
                    "Overlap-off floor walking stays grounded", f.LocalPosition.ToString("F3"));
            }
            using (var f = new BasicFixture(recovery))
            {
                float peak=0;
                f.Step(Vector2.zero, dt, true);
                for(int i=0;i<150;i++) { f.Step(Vector2.zero,dt); peak=Mathf.Max(peak,f.LocalPosition.y); }
                check(peak > .8f && peak < 1.15f && f.Motor.Grounded && f.LocalPosition.y < .1f,
                    "Overlap-off jump lands at the same floor", "apex="+peak+" end="+f.LocalPosition);
            }
            using (var f = new BasicFixture(recovery))
            {
                f.Box("Real .15m step",new Vector3(0,.075f,3),new Vector3(8,.15f,4));
                float peak=0;
                for(int i=0;i<180;i++) { f.Step(Vector2.up,dt); peak=Mathf.Max(peak,f.LocalPosition.y); }
                check(f.LocalPosition.z > 5.3f && peak > .12f && peak < .3f && f.Motor.Grounded,
                    "Overlap-off low step and descent remain traversable","peak="+peak+" end="+f.LocalPosition);
            }
            using (var f = new BasicFixture(recovery))
            {
                f.Box("Full wall",new Vector3(.8f,1,0),new Vector3(.2f,3,30));
                for(int i=0;i<150;i++) f.Step(Vector2.one.normalized,dt);
                float along=f.LocalPosition.z, at=f.LocalPosition.x;
                for(int i=0;i<75;i++) f.Step(Vector2.left,dt);
                check(along > 2.5f && at < .5f && f.LocalPosition.x < at-1,
                    "Overlap-off diagonal wall slide and withdrawal","along="+along+" contactX="+at+" away="+f.LocalPosition);
            }
            using (var f = new BasicFixture(recovery))
            {
                f.Box("Solid exhibit barrier",new Vector3(0,1.5f,2),new Vector3(8,3,.2f));
                for(int i=0;i<240;i++) f.Step(Vector2.up,dt);
                float blocked=f.LocalPosition.z;
                for(int i=0;i<90;i++) f.Step(Vector2.down,dt);
                check(blocked > .4f && blocked < 1.75f && f.LocalPosition.z < blocked-1,
                    "Overlap-off capsule still collides with and leaves solid exhibits","blockedZ="+blocked+" away="+f.LocalPosition);
            }
        }

        sealed class BasicFixture : IDisposable
        {
            readonly GameObject root;
            readonly Vector3 origin=new Vector3(2048,0,2560);
            public readonly CharacterController Controller;
            public readonly FirstPersonMotor Motor=new FirstPersonMotor { SmoothKeyboardInput=false };
            public Vector3 LocalPosition => Controller.transform.position-origin;
            public BasicFixture(bool recovery)
            {
                root=new GameObject("Validation only - overlap recovery movement");
                root.transform.position=origin;
                Box("Floor",new Vector3(0,-.5f,0),new Vector3(40,1,40));
                var avatar=new GameObject("Capsule");avatar.transform.SetParent(root.transform,false);
                avatar.transform.localPosition=Vector3.up*.025f;
                Controller=avatar.AddComponent<CharacterController>();
                FirstPersonMotor.Configure(Controller,null);Controller.enableOverlapRecovery=recovery;
                Physics.SyncTransforms();
                for(int i=0;i<10;i++)Step(Vector2.zero,1f/120);
            }
            public void Box(string name,Vector3 centre,Vector3 size)
            {
                var node=new GameObject(name);node.transform.SetParent(root.transform,false);node.transform.localPosition=centre;
                node.AddComponent<BoxCollider>().size=size;Physics.SyncTransforms();
            }
            public void Step(Vector2 input,float dt,bool jump=false)
            { Motor.Step(Controller,input,0,jump,false,dt);Physics.SyncTransforms(); }
            public void Dispose(){UnityEngine.Object.DestroyImmediate(root);Physics.SyncTransforms();}
        }

        // Full readable mesh data, primitive dimensions and world transforms are sampled before/after the fixture.
        static string GeometrySignature(Collider[] shapes)
        {
            ulong hash=14695981039346656037UL;
            Action<int> mix=value=>{unchecked{hash^=(uint)value;hash*=1099511628211UL;}};
            Action<Vector3> vector=v=>{mix(v.x.GetHashCode());mix(v.y.GetHashCode());mix(v.z.GetHashCode());};
            var meshes=new Dictionary<Mesh,ulong>();
            foreach(var shape in shapes)
            {
                mix(shape.GetInstanceID());mix(shape.enabled?1:0);mix(shape.isTrigger?1:0);
                Matrix4x4 matrix=shape.transform.localToWorldMatrix;
                for(int i=0;i<16;i++)mix(matrix[i].GetHashCode());
                if(shape is BoxCollider box){vector(box.center);vector(box.size);}
                else if(shape is SphereCollider sphere){vector(sphere.center);mix(sphere.radius.GetHashCode());}
                else if(shape is CapsuleCollider capsule){vector(capsule.center);mix(capsule.radius.GetHashCode());mix(capsule.height.GetHashCode());mix(capsule.direction);}
                else if(shape is MeshCollider collider && collider.sharedMesh!=null)
                {
                    Mesh mesh=collider.sharedMesh;
                    mix(mesh.GetInstanceID());mix(mesh.vertexCount);mix(mesh.subMeshCount);
                    vector(mesh.bounds.center);vector(mesh.bounds.size);
                    if(!meshes.TryGetValue(mesh,out ulong digest))
                    {
                        ulong before=hash;
                        if(mesh.isReadable)
                        {
                            foreach(Vector3 vertex in mesh.vertices)vector(vertex);
                            for(int sub=0;sub<mesh.subMeshCount;sub++)foreach(int index in mesh.GetTriangles(sub))mix(index);
                        }
                        else for(int sub=0;sub<mesh.subMeshCount;sub++)mix((int)mesh.GetIndexCount(sub));
                        digest=hash;hash=before;meshes[mesh]=digest;
                    }
                    mix((int)digest);mix((int)(digest>>32));
                }
            }
            return hash.ToString("x16");
        }

        sealed class Fixture : IDisposable
        {
            readonly GameObject root;
            readonly Transform originalRoot;
            readonly Dictionary<Transform, Transform> mirrored = new Dictionary<Transform, Transform>();
            readonly List<Collider> copies = new List<Collider>();
            readonly Dictionary<Collider,string> sourcePaths = new Dictionary<Collider,string>();
            readonly CharacterController controller;
            readonly bool configuredOverlapRecovery;
            readonly FirstPersonMotor motor = new FirstPersonMotor { SmoothKeyboardInput = false,
                GroundFilter = PlacementSupport.IsUsableGround };
            public Fixture(GalleryGame game, List<Collider> source, Vector3 anchor)
            {
                root = new GameObject("Validation only - gallery contact fixture");
                originalRoot = game.transform;
                root.transform.SetPositionAndRotation(game.transform.position + IsolationOffset, game.transform.rotation);
                root.transform.localScale = game.transform.lossyScale;
                mirrored[game.transform] = root.transform;
                var region = new Bounds(anchor + Vector3.up, new Vector3(10, 4, 10));
                foreach (var shape in source)
                {
                    if (!shape.bounds.Intersects(region)) continue;
                    var at = Mirror(shape.transform);
                    Collider copy = null;
                    if (shape is BoxCollider box)
                    { var c = at.gameObject.AddComponent<BoxCollider>(); c.center = box.center; c.size = box.size; copy = c; }
                    else if (shape is MeshCollider mesh)
                    { var c = at.gameObject.AddComponent<MeshCollider>(); c.cookingOptions = mesh.cookingOptions;
                        c.convex = mesh.convex; c.sharedMesh = mesh.sharedMesh; copy = c; }
                    else if (shape is SphereCollider sphere)
                    { var c = at.gameObject.AddComponent<SphereCollider>(); c.center = sphere.center; c.radius = sphere.radius; copy = c; }
                    else if (shape is CapsuleCollider capsule)
                    { var c = at.gameObject.AddComponent<CapsuleCollider>(); c.center = capsule.center; c.radius = capsule.radius;
                        c.height = capsule.height; c.direction = capsule.direction; copy = c; }
                    if (copy != null) { copy.sharedMaterial = shape.sharedMaterial; copies.Add(copy); sourcePaths[copy] = GalleryStaticExhibitCollision.Path(game.transform, shape.transform); }
                }
                var avatar = new GameObject("Validation capsule");
                avatar.transform.SetParent(root.transform, false);
                controller = avatar.AddComponent<CharacterController>();
                FirstPersonMotor.Configure(controller, null);
                configuredOverlapRecovery = game.controller.enableOverlapRecovery;
                controller.enableOverlapRecovery = configuredOverlapRecovery;
                Physics.SyncTransforms();
            }
            Transform Mirror(Transform source)
            {
                if (mirrored.TryGetValue(source, out var found)) return found;
                var parent = source.parent == null || !source.IsChildOf(originalRoot) ? root.transform : Mirror(source.parent);
                var node = new GameObject(source.name); node.transform.SetParent(parent, false);
                node.transform.localPosition = source.localPosition; node.transform.localRotation = source.localRotation;
                node.transform.localScale = source.localScale;
                mirrored[source] = node.transform; return node.transform;
            }
            public Sample Measure(Probe probe, float dt, bool legacy)
            {
                controller.enableOverlapRecovery = legacy ? true : configuredOverlapRecovery;
                Vector3 normal = probe.outward.normalized;
                Vector3 tangent = Vector3.Cross(Vector3.up, normal);
                var sample = new Sample { name = probe.name, mode = legacy ? "before / recovery on" : "Gallery configured", dt = dt, copiedColliders = copies.Count };
                sample.start = probe.surface + IsolationOffset + normal * .7f;
                controller.enabled = false; controller.transform.position = sample.start; controller.enabled = true;
                Physics.SyncTransforms(); motor.Reset();
                for (int i = 0; i < Mathf.CeilToInt(.35f / dt); i++) Step(Vector3.zero, dt);
                for (int i = 0; i < Mathf.CeilToInt(.65f / dt); i++) Step(-normal, dt);
                sample.contact = controller.transform.position;
                sample.contacts = Vector3.Dot(sample.start - sample.contact, normal) < 1.2f;
                sample.capsuleRadius = controller.radius; sample.capsuleSkinWidth = controller.skinWidth;
                sample.capsuleHeight = controller.height; sample.capsuleStepOffset = controller.stepOffset;
                sample.tangentClearanceRadius = controller.radius + controller.skinWidth;
                sample.escapeClearanceRadius = .245f;
                sample.tangentClear = TangentBodyPathClear(sample.contact, tangent);
                float highest = sample.contact.y;
                for (int i = 0; i < Mathf.CeilToInt(1.15f / dt); i++)
                { Step((-normal + tangent).normalized, dt); highest = Mathf.Max(highest, controller.transform.position.y); }
                sample.rubbed = controller.transform.position;
                sample.tangentDistance = Vector3.Dot(sample.rubbed - sample.contact, tangent);
                sample.maximumRise = highest - sample.contact.y;
                sample.escapeClear = EscapeFromContactClear(sample.rubbed, normal);
                for (int i = 0; i < Mathf.CeilToInt(.8f / dt); i++) Step(normal, dt);
                sample.escaped = controller.transform.position;
                sample.escapeDistance = Vector3.Dot(sample.escaped - sample.rubbed, normal);
                sample.passed = !sample.contacts || ((!sample.tangentClear || sample.tangentDistance >= .35f) &&
                    (!sample.escapeClear || sample.escapeDistance >= .35f));
                if (!legacy && !sample.passed)
                {
                    sample.capsuleRadius = controller.radius; sample.capsuleSkinWidth = controller.skinWidth;
                    sample.capsuleHeight = controller.height; sample.capsuleStepOffset = controller.stepOffset;
                    sample.outward = normal; sample.tangent = tangent;
                    var diagnostics = new List<ColliderDiagnostic>();
                    foreach (var shape in copies)
                    {
                        var mesh = shape as MeshCollider; int triangles = 0;
                        if (mesh != null && mesh.sharedMesh != null)
                            for (int sub=0;sub<mesh.sharedMesh.subMeshCount;sub++) triangles += (int)(mesh.sharedMesh.GetIndexCount(sub)/3);
                        diagnostics.Add(new ColliderDiagnostic { path = sourcePaths[shape], type = shape.GetType().Name,
                            mesh = mesh == null || mesh.sharedMesh == null ? "" : mesh.sharedMesh.name,
                            center = shape.bounds.center-IsolationOffset, size = shape.bounds.size, triangles = triangles });
                    }
                    sample.actualColliders = diagnostics.ToArray();
                    var sweeps = new List<SweepDiagnostic>();
                    foreach (float radius in new[] { .245f, controller.radius, controller.radius + controller.skinWidth })
                    {
                        sweeps.Add(TraceSweep("tangent", sample.contact, tangent, 1.4f, .21f, radius));
                        sweeps.Add(TraceSweep("escape", sample.rubbed, normal, .8f, 0, radius));
                    }
                    sample.clearanceDiagnostics = sweeps.ToArray();
                }
                // Keep diagnostics in the authored gallery coordinate system.
                sample.start -= IsolationOffset; sample.contact -= IsolationOffset;
                sample.rubbed -= IsolationOffset; sample.escaped -= IsolationOffset;
                return sample;
            }
            void Step(Vector3 input, float dt)
            {
                motor.Step(controller, new Vector2(input.x, input.z), 0, false, false, dt);
                Physics.SyncTransforms();
            }
            SweepDiagnostic TraceSweep(string phase, Vector3 feet, Vector3 direction, float distance, float lift, float radius)
            {
                var hits = new List<HitDiagnostic>();
                Vector3 bottom=feet+Vector3.up*(.3f+lift), top=feet+Vector3.up*(1.3f+lift);
                foreach (var hit in Physics.CapsuleCastAll(bottom,top,radius,direction,distance,~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.collider == controller || hit.collider.transform.IsChildOf(controller.transform)) continue;
                    string path = sourcePaths.TryGetValue(hit.collider,out var original) ? original : hit.collider.name;
                    hits.Add(new HitDiagnostic { path=path, distance=hit.distance, point=hit.point-IsolationOffset,
                        normal=hit.normal, blocksExistingCriterion=Vector3.Dot(hit.normal,direction)<-.08f && hit.normal.y<=.55f });
                }
                return new SweepDiagnostic { phase=phase, feet=feet-IsolationOffset, direction=direction,
                    radius=radius, lift=lift, distance=distance, hits=hits.ToArray() };
            }
            bool TangentBodyPathClear(Vector3 feet, Vector3 direction)
            {
                // Tangential clearance asks whether the entire swept body plus its contact skin fits.
                // The smaller .245 probe could fit beside a frame where the real .3+.05 envelope cannot.
                return SweepClear(feet, direction, 1.4f, .21f, controller.radius + controller.skinWidth);
            }
            bool EscapeFromContactClear(Vector3 feet, Vector3 direction)
            {
                // Withdrawal starts in an existing skin contact. Keep the original inset probe and
                // departing-normal filter: treating skin's zero-distance hit as a new barrier would
                // hide the legacy failure to leave that contact. The real controller must still move .35m.
                return SweepClear(feet, direction, .8f, 0, .245f);
            }
            bool SweepClear(Vector3 feet, Vector3 direction, float distance, float lift, float radius)
            {
                Vector3 bottom = feet + Vector3.up * (.3f + lift), top = feet + Vector3.up * (1.3f + lift);
                foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider == controller || hit.collider.transform.IsChildOf(controller.transform)) continue;
                    if (Vector3.Dot(hit.normal, direction) >= -.08f || hit.normal.y > .55f) continue;
                    return false;
                }
                return true;
            }
            public void Dispose() { if (root != null) UnityEngine.Object.DestroyImmediate(root); Physics.SyncTransforms(); }
        }
    }
}
