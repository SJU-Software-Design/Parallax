using UnityEngine;

namespace ShadeLink.Beach
{
    // Only proposes a small correction for an already-near shadow. The caller
    // retains collision validation, release physics and real shadow matching.
    public static class BeachShadowDropAssist
    {
        public const float MaximumCentreError=.24f,MaximumWidthError=.18f;
        public const float MaximumAngleError=10f,MaximumRootMove=.32f,MaximumScaleError=.16f;

        public struct Alignment
        {
            public Vector3 position,scale;
            public Quaternion rotation;
            public float centreError,widthError,angleError;
        }

        struct Projection
        {
            public Vector2 min,max;
            bool initialized;
            public Vector2 Centre=>(min+max)*.5f;
            public float Width=>max.x-min.x;
            public float Height=>max.y-min.y;
            public void Add(Vector2 point)
            {
                if(!initialized){min=max=point;initialized=true;}
                else {min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
            }
        }

        public static bool TryGetAlignment(BeachProp piece,BoxCollider plate,Transform board,Transform anchor,out Alignment result)
        {
            result=default;
            if(piece==null||plate==null||board==null||anchor==null||!piece.gameObject.activeInHierarchy
                ||!plate.enabled||!plate.gameObject.activeInHierarchy
                ||plate.GetComponentInParent<BeachProp>()!=piece||anchor.IsChildOf(piece.transform))return false;

            Transform root=piece.transform;
            Vector3 parentScale=root.parent==null?Vector3.one:root.parent.lossyScale;
            Vector3 currentScale=root.localScale,targetWorldScale=anchor.lossyScale;
            Vector3 position=root.position,targetPosition=anchor.position;
            Quaternion currentRotation=root.rotation,targetRotation=anchor.rotation;
            if(!UniformPositive(parentScale)||!UniformPositive(currentScale)||!UniformPositive(targetWorldScale)
                ||!Finite(position)||!Finite(targetPosition)||!Finite(currentRotation)||!Finite(targetRotation)
                ||!Finite(plate.center)||!Finite(plate.size)||plate.size.x<=0||plate.size.y<=0||plate.size.z<=0)return false;

            Vector3 targetScale=new Vector3(targetWorldScale.x/parentScale.x,targetWorldScale.y/parentScale.y,targetWorldScale.z/parentScale.z);
            float scaleError=ScaleError(currentScale,targetScale);
            float move=Vector3.Distance(position,targetPosition);
            if(!Finite(scaleError)||!Finite(move)||scaleError>MaximumScaleError||move>MaximumRootMove)return false;

            // Both authored silhouettes are horizontally symmetric. Keep the
            // nearest facing direction instead of turning a back-facing piece.
            Quaternion reverse=targetRotation*Quaternion.Euler(0,180,0);
            float angle=Quaternion.Angle(currentRotation,targetRotation),reverseAngle=Quaternion.Angle(currentRotation,reverse);
            if(reverseAngle<angle){angle=reverseAngle;targetRotation=reverse;}
            if(!Finite(angle)||angle>MaximumAngleError)return false;
            if(move<.002f&&angle<.3f&&scaleError<.002f)return false;

            Vector3 normal=board.forward,right=board.right,up=board.up,origin=board.position;
            if(!Finite(normal)||!Finite(right)||!Finite(up)||!Finite(origin))return false;
            float incidence=Vector3.Dot(normal,BeachLighting.ToSun);
            if(!Finite(incidence)||incidence<=.025f)return false;

            Projection current=default,desired=default;
            Vector3 half=plate.size*.5f;
            for(int i=0;i<8;i++)
            {
                Vector3 corner=plate.center+Vector3.Scale(half,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 world=plate.transform.TransformPoint(corner);
                Vector3 local=root.InverseTransformPoint(world);
                Vector3 candidate=targetPosition+targetRotation*Vector3.Scale(local,targetWorldScale);
                if(!Project(world,origin,normal,right,up,incidence,out var actual)
                    ||!Project(candidate,origin,normal,right,up,incidence,out var expected))return false;
                current.Add(actual);desired.Add(expected);
            }
            if(current.Width<.0001f||desired.Width<.0001f||current.Height<.0001f||desired.Height<.0001f)return false;
            float centreError=Vector2.Distance(current.Centre,desired.Centre);
            float widthError=Mathf.Abs(current.Width/desired.Width-1);
            // A steep sun amplifies small yaw errors into vertical shear, so
            // rotation, centre and width are bounded rather than bbox height.
            if(!Finite(centreError)||!Finite(widthError)||centreError>MaximumCentreError||widthError>MaximumWidthError)return false;

            result=new Alignment{position=targetPosition,rotation=targetRotation,scale=targetScale,
                centreError=centreError,widthError=widthError,angleError=angle};
            return true;
        }

        static bool Project(Vector3 point,Vector3 origin,Vector3 normal,Vector3 right,Vector3 up,float incidence,out Vector2 projected)
        {
            projected=default;
            if(!Finite(point))return false;
            float travel=Vector3.Dot(point-origin,normal)/incidence;
            if(!Finite(travel)||travel<-.01f)return false;
            Vector3 relative=point-BeachLighting.ToSun*travel-origin;
            projected=new Vector2(Vector3.Dot(relative,right),Vector3.Dot(relative,up));
            return Finite(projected.x)&&Finite(projected.y);
        }
        static float ScaleError(Vector3 actual,Vector3 expected)=>Mathf.Max(Mathf.Abs(actual.x/expected.x-1),
            Mathf.Max(Mathf.Abs(actual.y/expected.y-1),Mathf.Abs(actual.z/expected.z-1)));
        static bool UniformPositive(Vector3 value)
        {
            if(!Finite(value)||value.x<=0||value.y<=0||value.z<=0)return false;
            float largest=Mathf.Max(value.x,Mathf.Max(value.y,value.z)),smallest=Mathf.Min(value.x,Mathf.Min(value.y,value.z));
            return largest-smallest<=Mathf.Max(.0001f,largest*.0001f);
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        static bool Finite(Quaternion value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z)&&Finite(value.w)
            &&value.x*value.x+value.y*value.y+value.z*value.z+value.w*value.w>.000001f;
    }
}
