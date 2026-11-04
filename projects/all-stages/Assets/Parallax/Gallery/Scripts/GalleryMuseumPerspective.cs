using UnityEngine;

namespace ShadeLink.Gallery
{
    // This exhibit evaluates the same physical objects the common interaction
    // system moves. Its solution is a projection, not a second resize control.
    public sealed class GalleryMuseumPerspective : MonoBehaviour
    {
        public GalleryGame game;
        public Transform viewingPoint;
        public GalleryProp[] pieces;
        public Vector3[] solutionPositions,markerLocalPositions;
        public float[] solutionScales;
        public Renderer feedback;
        public int AlignedPieces { get; private set; }
        public bool Complete { get; private set; }
        public bool AtViewingPoint { get; private set; }
        bool announced;
        MaterialPropertyBlock properties;
        public void Evaluate()
        {
            if(game==null||game.player==null||viewingPoint==null||pieces==null)return;
            Vector3 eye=viewingPoint.position+Vector3.up*1.55f;
            AlignedPieces=0;
            for(int i=0;i<pieces.Length;i++)
            {
                var prop=pieces[i];if(prop==null||prop==game.held||prop.placement==null||!prop.placement.Ready)continue;
                Vector3 actual=prop.transform.TransformPoint(markerLocalPositions[i])-eye;
                Vector3 desired=solutionPositions[i]+markerLocalPositions[i]*solutionScales[i]-eye;
                if(actual.z<1||desired.z<1)continue;
                Vector2 a=new Vector2(actual.x,actual.y)/actual.z,b=new Vector2(desired.x,desired.y)/desired.z;
                float sizeRatio=(prop.transform.lossyScale.x/actual.z)/(solutionScales[i]/desired.z);
                // A generous visual tolerance rewards readable faces rather than
                // requiring mathematically exact placements with a mouse.
                if(Vector2.Distance(a,b)<.05f&&Mathf.Abs(sizeRatio-1)<.24f&&Vector3.Dot(prop.transform.forward,Vector3.forward)>.90f)AlignedPieces++;
            }
            AtViewingPoint=Vector2.Distance(new Vector2(game.player.feet.x,game.player.feet.z),new Vector2(viewingPoint.position.x,viewingPoint.position.z))<1.15f;
            Complete=AlignedPieces==pieces.Length;
            if(feedback!=null)
            {
                if(properties==null)properties=new MaterialPropertyBlock();
                properties.SetColor("_Selection",Complete?new Color(.40f,.31f,.10f):new Color(.04f,.025f,.008f)*AlignedPieces);
                feedback.SetPropertyBlock(properties);
            }
            if(!Complete)announced=false;
            if(Complete&&AtViewingPoint&&!announced&&game.active)
            {
                announced=true;game.Tell("시점의 초상이 완성되었습니다. 눈·코·입이 하나의 얼굴로 보입니다.",6);
            }
        }
        void LateUpdate(){Evaluate();}
    }
}
