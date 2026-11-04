using System;
using System.IO;
using System.Linq;
using ShadeLink.Gallery;
using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    // The fixture releases no object and advances no physics. It establishes a
    // real room-edge regression: a valid resting statue whose opened collision
    // envelope would enter the wall margin must refuse its opening animation.
    public static class GalleryCathedralPlacementChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed, centralAccepted, edgeClosedValid, edgeOpenedInvalid, edgeRejected;
            public bool probeCentersRestored, probePoseUnchanged, authoredPoseRestored;
            public float scale, edgeMargin, closedMinimumX, openedMinimumX;
            public Vector3 fixturePosition;
            public string failure, closedRestriction, openedRestriction;
        }

        [MenuItem("Parallax/Check cathedral opening placement")]
        public static void Run()
        {
            GalleryBuild.Prepare();
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            Require(game != null && game.orcusExit != null, "Prepared museum has no sculpture entrance.");
            var prop = game.orcusExit.prop;
            var activation = game.orcusExit.cathedralActivation;
            Require(prop != null && activation != null && activation.movingParts != null &&
                activation.closedCenters != null && activation.openedCenters != null &&
                activation.movingParts.Length == activation.closedCenters.Length &&
                activation.movingParts.Length == activation.openedCenters.Length,
                "Cathedral opening collision authoring is incomplete.");
            Require(activation.movingParts.All(part => part != null), "Cathedral opening has a missing collision section.");

            Vector3 authoredPosition = prop.transform.position, authoredScale = prop.transform.localScale;
            Quaternion authoredRotation = prop.transform.rotation;
            Vector3[] authoredCenters = Centers(activation);
            var report = new Report { scale = GalleryCathedralGeometry.RequiredScale };
            try
            {
                activation.ResetToRest();
                Place(prop, new Vector3(0, .006f, GalleryConceptExit.Home.z), report.scale);
                Require(game.ValidPlacement(prop, out var centerRestriction),
                    "Central enlarged fixture is not a valid resting placement: " + centerRestriction);
                Vector3[] centerBefore = Centers(activation);
                Vector3 positionBefore = prop.transform.position, scaleBefore = prop.transform.localScale;
                Quaternion rotationBefore = prop.transform.rotation;
                Require(activation.CanOpenAtCurrentPlacement(), "Central enlarged fixture incorrectly refuses opening.");
                report.centralAccepted = true;
                Require(SameCenters(activation, centerBefore) && SamePose(prop, positionBefore, rotationBefore, scaleBefore),
                    "The successful opening probe changed physical authoring or the statue pose.");

                bool found = false;
                // Include several nearby depths and small wall margins so the
                // test remains independent of decorative room details. Every
                // chosen negative is first proved to be a valid closed pose.
                foreach (float depth in new[] { GalleryConceptExit.Home.z, GalleryConceptExit.Home.z + .5f, GalleryConceptExit.Home.z - .5f })
                {
                    foreach (float margin in new[] { .025f, .05f, .075f, .10f, .125f, .15f })
                    {
                        activation.ResetToRest();
                        Place(prop, new Vector3(0, .006f, depth), report.scale);
                        Bounds centeredBounds = prop.WorldBounds;
                        float wantedLeft = -GalleryConceptWorld.Width * .5f + .2f + margin;
                        prop.transform.position += Vector3.right * (wantedLeft - centeredBounds.min.x);
                        Physics.SyncTransforms();
                        if (!game.ValidPlacement(prop, out var closedRestriction)) continue;
                        Bounds closedBounds = prop.WorldBounds;
                        bool openedValid = OpenedPoseValid(game, prop, activation, out Bounds openedBounds, out string openedRestriction);
                        if (openedValid) continue;

                        report.edgeClosedValid = true; report.edgeOpenedInvalid = true;
                        report.edgeMargin = margin; report.fixturePosition = prop.transform.position;
                        report.closedMinimumX = closedBounds.min.x; report.openedMinimumX = openedBounds.min.x;
                        report.closedRestriction = closedRestriction; report.openedRestriction = openedRestriction;
                        Vector3[] before = Centers(activation);
                        positionBefore = prop.transform.position; scaleBefore = prop.transform.localScale; rotationBefore = prop.transform.rotation;
                        Require(!activation.CanOpenAtCurrentPlacement(), "A valid closed statue beside the wall accepted an obstructed opened envelope.");
                        report.edgeRejected = true;
                        report.probeCentersRestored = SameCenters(activation, before);
                        report.probePoseUnchanged = SamePose(prop, positionBefore, rotationBefore, scaleBefore);
                        Require(report.probeCentersRestored && report.probePoseUnchanged,
                            "The rejected opening probe left changed colliders or moved the statue.");
                        found = true; break;
                    }
                    if (found) break;
                }
                Require(found, "No closed-valid/opened-obstructed left-wall fixture was found in the authored final room.");
                report.passed = report.centralAccepted && report.edgeClosedValid && report.edgeOpenedInvalid &&
                    report.edgeRejected && report.probeCentersRestored && report.probePoseUnchanged;
            }
            catch (Exception exception) { report.failure = exception.ToString(); throw; }
            finally
            {
                activation.ResetToRest();
                for (int i = 0; i < authoredCenters.Length; i++) activation.movingParts[i].center = authoredCenters[i];
                prop.transform.SetPositionAndRotation(authoredPosition, authoredRotation);
                prop.transform.localScale = authoredScale;
                Physics.SyncTransforms();
                report.authoredPoseRestored = SameCenters(activation, authoredCenters) &&
                    SamePose(prop, authoredPosition, authoredRotation, authoredScale);
                report.passed &= report.authoredPoseRestored;
                Directory.CreateDirectory("Validation/Gallery");
                File.WriteAllText("Validation/Gallery/cathedral-placement-checks.json", JsonUtility.ToJson(report, true));
            }
            Require(report.passed, "Opening-placement fixture did not restore its authored state.");
            Debug.Log("CATHEDRAL_PLACEMENT_CHECKS_OK central=true edgeClosedValid=true edgeOpenedInvalid=true" +
                " edgeRejected=true centersRestored=true poseRestored=true position=" + report.fixturePosition.ToString("F4"));
        }

        static bool OpenedPoseValid(GalleryGame game, GalleryProp prop, GalleryCathedralActivation activation,
            out Bounds bounds, out string restriction)
        {
            Vector3[] before = Centers(activation);
            try
            {
                for (int i = 0; i < activation.movingParts.Length; i++) activation.movingParts[i].center = activation.openedCenters[i];
                Physics.SyncTransforms(); bounds = prop.WorldBounds;
                return game.ValidPlacement(prop, out restriction);
            }
            finally
            {
                for (int i = 0; i < activation.movingParts.Length; i++) activation.movingParts[i].center = before[i];
                Physics.SyncTransforms();
            }
        }

        static void Place(GalleryProp prop, Vector3 position, float scale)
        {
            prop.transform.SetPositionAndRotation(position, Quaternion.identity);
            prop.transform.localScale = Vector3.one * scale;
            Physics.SyncTransforms();
        }
        static Vector3[] Centers(GalleryCathedralActivation activation)
        { return activation.movingParts.Select(part => part.center).ToArray(); }
        static bool SameCenters(GalleryCathedralActivation activation, Vector3[] expected)
        {
            for (int i = 0; i < expected.Length; i++)
                if ((activation.movingParts[i].center - expected[i]).sqrMagnitude > 1e-12f) return false;
            return true;
        }
        static bool SamePose(GalleryProp prop, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return (prop.transform.position - position).sqrMagnitude < 1e-12f &&
                Quaternion.Angle(prop.transform.rotation, rotation) < .0001f &&
                (prop.transform.localScale - scale).sqrMagnitude < 1e-12f;
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("CATHEDRAL_PLACEMENT_CHECK: " + message); }
    }
}
