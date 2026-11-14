using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // A normal movable GalleryProp. This component provides vessel geometry
    // coordinates only; it never derives size from player distance or moves the
    // bottle toward an authored pedestal. The shared interaction owns its pose.
    public sealed class GalleryBottleBalanceVessel : MonoBehaviour
    {
        const int CircumferenceParts = 8;
        const float WallThickness = .042f;
        public Transform glassFrame;
        public Transform shellFrame;

        // Profile of the original lathed glass, in its +X neck coordinate frame.
        // Eight axial intervals and eight circumferential panels make a closed
        // bulb and an open neck with 64 independently queryable thin boxes.
        static readonly Vector2[] Profile =
        {
            new Vector2(-.620f, .000f),
            new Vector2(-.490f, .379f),
            new Vector2(-.220f, .580f),
            new Vector2( .100f, .612f),
            new Vector2( .390f, .482f),
            new Vector2( .600f, .285f),
            new Vector2( .780f, .215f),
            new Vector2( .960f, .215f),
            new Vector2(1.050f, .236f)
        };

        public static GalleryProp CreateMovableBottle(GalleryBottleBalanceArt.View art, Transform parent, int zone)
        {
            if (art == null || art.bottleVisual == null || parent == null)
                throw new ArgumentNullException("A bottle art view and parent are required.");
            Physics.SyncTransforms();

            // Never leave a disabled solid query box among the new prop's Parts:
            // ordinary BeginHold intentionally re-enables every owned part.
            if (art.bottlePickShape != null)
            {
                foreach (Collider shape in art.bottlePickShape.GetComponentsInChildren<Collider>(true))
                    shape.enabled = false;
                art.bottlePickShape.transform.SetParent(art.bottlePivot, false);
                art.bottlePickShape.SetActive(false);
            }

            float supportTop = art.bottleStation.position.y + .32f;
            Transform originalFoot = art.bottleStation.Find("Beveled limestone foot");
            if (originalFoot != null)
            {
                var support = originalFoot.GetComponent<Collider>();
                if (support != null) supportTop = support.bounds.max.y;
            }

            var root = new GameObject("Movable glass bottle · ordinary perspective prop");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(art.bottleStation.position.x, supportTop + .64f, art.bottleStation.position.z);
            // ObjectInteraction's local-X pitch now tilts the neck, rather than
            // spinning a bottle whose long axis was itself local X.
            root.transform.rotation = Quaternion.Euler(0, 90, 0);
            var frame = root.AddComponent<GalleryBottleBalanceVessel>();
            frame.glassFrame = art.bottleVisual;
            frame.glassFrame.SetParent(root.transform, false);
            frame.glassFrame.localPosition = Vector3.zero;
            frame.glassFrame.localRotation = Quaternion.Euler(0, -90, 0);
            frame.glassFrame.localScale = Vector3.one;

            Transform mountingBand = frame.glassFrame.Find("Thin pivot mounting band");
            if (mountingBand != null) mountingBand.gameObject.SetActive(false);
            if (art.bottlePivot != null) art.bottlePivot.gameObject.SetActive(false);
            if (art.cradleArms != null)
                foreach (Transform arm in art.cradleArms) if (arm != null) arm.gameObject.SetActive(false);
            if (art.bridge != null) art.bridge.gameObject.SetActive(false);
            // A low display plinth is ordinary scenery, not an enclosure or a
            // platform the visitor has to stand on to reach the bottle.
            ResizePlinth(art.bottleStation,"Beveled limestone foot",new Vector3(2.4f,.32f,1.9f));
            ResizePlinth(art.bottleStation,"Dark bronze reveal below plinth",new Vector3(2.25f,.05f,1.75f));
            ResizePlinth(art.bottleStation,"Thin brass edging",new Vector3(2.33f,.025f,1.83f));
            var plaque=art.bottleStation.Find("Bottle exhibition title plaque");if(plaque!=null)plaque.localPosition=new Vector3(0,.205f,-.963f);
            foreach(var label in art.bottleStation.GetComponentsInChildren<GalleryLabel>())
                if(label.caption=="병 속의 추")label.transform.localPosition=new Vector3(0,.205f,-.983f);

            frame.shellFrame = new GameObject("Glass bottle · hollow compound collision shell").transform;
            frame.shellFrame.SetParent(root.transform, false);
            frame.shellFrame.localRotation = Quaternion.Euler(0, -90, 0);
            BuildShell(frame.shellFrame);

            var prop = root.AddComponent<GalleryProp>();
            prop.title = "유리병";
            prop.zone = zone;
            prop.minScale = .001f;
            prop.mountedAtHome = false;
            prop.Remember();
            prop.InitializePhysics();
            // Glass contact audio has not been selected yet. Keep ordinary
            // pickup/release cues, but do not misclassify it as a stone impact.
            PropReleasePhysics.Attach(prop.body).SuppressImpactAudio=true;
            // Glass must remain transparent so its unchanged-size contents are
            // visible while moving. This changes appearance, not placement,
            // grounding, player collision, or the shared perspective solver.
            prop.placement.PreviewWhileHeld = false;
            prop.placement.PreviewUntilSettled = false;
            Physics.SyncTransforms();
            prop.placement.ResetPlaced();
            return prop;
        }

        static void ResizePlinth(Transform root,string name,Vector3 size)
        {
            var node=root.Find(name);if(node==null)return;
            var renderer=node.GetComponent<Renderer>();if(renderer==null)return;
            Vector3 old=renderer.bounds.size;
            node.localScale=Vector3.Scale(node.localScale,new Vector3(size.x/Mathf.Max(.001f,old.x),size.y/Mathf.Max(.001f,old.y),size.z/Mathf.Max(.001f,old.z)));
        }

        static void BuildShell(Transform parent)
        {
            float halfAngle = Mathf.PI / CircumferenceParts;
            for (int strip = 0; strip < Profile.Length - 1; strip++)
            {
                Vector2 a = Profile[strip], b = Profile[strip + 1];
                float dx = b.x - a.x, dr = b.y - a.y;
                float length = Mathf.Sqrt(dx * dx + dr * dr);
                float radius = (a.y + b.y) * .5f;
                float apothem = radius * Mathf.Cos(halfAngle);
                float width = Mathf.Max(.014f, 2 * radius * Mathf.Sin(halfAngle) + .012f);
                for (int panel = 0; panel < CircumferenceParts; panel++)
                {
                    float angle = (panel + .5f) * Mathf.PI * 2 / CircumferenceParts;
                    float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
                    Vector3 along = new Vector3(dx, dr * cosine, dr * sine).normalized;
                    Vector3 around = new Vector3(0, -sine, cosine);
                    var section = new GameObject("Glass shell · " + strip.ToString("00") + " · " + panel.ToString("00"));
                    section.transform.SetParent(parent, false);
                    section.transform.localPosition = new Vector3((a.x + b.x) * .5f, apothem * cosine, apothem * sine);
                    section.transform.localRotation = Quaternion.LookRotation(around, along);
                    var box = section.AddComponent<BoxCollider>();
                    box.size = new Vector3(WallThickness, length + .020f, width);
                }
            }
        }

        static GalleryBottleBalanceVessel Frame(GalleryProp prop)
        {
            if (prop == null) throw new ArgumentNullException(nameof(prop));
            var frame = prop.GetComponent<GalleryBottleBalanceVessel>();
            if (frame == null || frame.glassFrame == null)
                throw new InvalidOperationException("The prop is not a movable bottle vessel.");
            return frame;
        }

        public static Vector3 MouthWorld(GalleryProp prop) => Frame(prop).glassFrame.TransformPoint(new Vector3(1.05f, 0, 0));
        public static Vector3 MouthDirection(GalleryProp prop) => Frame(prop).glassFrame.TransformDirection(Vector3.right).normalized;
        public static float MouthRadius(GalleryProp prop) => .19f * Mathf.Abs(prop.transform.lossyScale.x);
        public static float InnerRadiusAt(GalleryProp prop,float axialMetres)
        {
            float scale=Mathf.Abs(prop.transform.lossyScale.x),x=axialMetres/Mathf.Max(.001f,scale);
            for(int i=1;i<Profile.Length;i++)
                if(x>=Profile[i-1].x&&x<=Profile[i].x)
                    return Mathf.Max(0,Mathf.Lerp(Profile[i-1].y,Profile[i].y,Mathf.InverseLerp(Profile[i-1].x,Profile[i].x,x))-WallThickness*.6f)*scale;
            return 0;
        }

        public static Vector3 ContentWorldPosition(GalleryProp prop)
        {
            var frame = Frame(prop);
            float scale = Mathf.Abs(prop.transform.lossyScale.x);
            Vector3 centre = frame.glassFrame.TransformPoint(new Vector3(-.05f, 0, 0));
            // The sculpture stays at its own world size. Guide it toward the
            // gravity-facing bottom of the bulb rather than parenting/scaling it.
            float down = Mathf.Max(0, .55f * scale - .28f - .02f);
            return centre + Vector3.down * down;
        }
    }
}
