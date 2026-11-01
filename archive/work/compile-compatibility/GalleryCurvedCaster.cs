using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // A surface of revolution using the same sampled Bezier profile as the mesh.
    // Physical placement retains its coarse hull; sunlight never uses that hull.
    public sealed class GalleryCurvedCaster : MonoBehaviour
    {
        public const int HeaderSlot=127;
        Vector2[] profile;
        Vector2[] Profile { get { if(profile==null)profile=GalleryCeramicMesh.ShadowProfile();return profile; } }
        public int DataSlots { get { return (Profile.Length+7)/8; } }
        Matrix4x4 Inverse { get { return Matrix4x4.TRS(transform.position,transform.rotation,transform.lossyScale).inverse; } }
        static void Element(ref Matrix4x4 m,int at,float value)
        {
            switch(at)
            {
                case 0:m.m00=value;break;case 1:m.m01=value;break;case 2:m.m02=value;break;case 3:m.m03=value;break;
                case 4:m.m10=value;break;case 5:m.m11=value;break;case 6:m.m12=value;break;case 7:m.m13=value;break;
                case 8:m.m20=value;break;case 9:m.m21=value;break;case 10:m.m22=value;break;case 11:m.m23=value;break;
                case 12:m.m30=value;break;case 13:m.m31=value;break;case 14:m.m32=value;break;case 15:m.m33=value;break;
            }
        }
        public void Pack(Matrix4x4[] target,int start)
        {
            if(start+DataSlots>HeaderSlot)throw new InvalidOperationException("Gallery curve shadow data exceeds available uniform slots");
            for(int i=0;i<DataSlots;i++)target[start+i]=new Matrix4x4();
            for(int i=0;i<Profile.Length;i++)
            {
                int matrix=start+i/8,element=(i%8)*2;
                Element(ref target[matrix],element,profile[i].x);Element(ref target[matrix],element+1,profile[i].y);
            }
            var header=Inverse;
            header.m30=profile.Length;header.m31=start;header.m32=.8f;header.m33=2;
            target[HeaderSlot]=header;
        }
        public bool Intersects(Vector3 origin,Vector3 direction)
        {
            var m=Inverse;var o=m.MultiplyPoint3x4(origin);var d=m.MultiplyVector(direction);
            float enter=0,leave=1e20f;
            for(int axis=0;axis<3;axis++)
            {
                float half=axis==1?1.06f:.8f;
                if(Mathf.Abs(d[axis])<1e-7f){if(Mathf.Abs(o[axis])>half)return false;continue;}
                float a=(-half-o[axis])/d[axis],b=(half-o[axis])/d[axis];
                enter=Mathf.Max(enter,Mathf.Min(a,b));leave=Mathf.Min(leave,Mathf.Max(a,b));
            }
            if(leave<enter)return false;
            o+=d*Mathf.Max(0,enter-.01f);
            var points=Profile;
            for(int i=0;i<points.Length-1;i++)if(Segment(o,d,points[i],points[i+1]))return true;
            return false;
        }
        static bool Root(float t,Vector3 o,Vector3 d,float low,float high)
        {
            float y=o.y+d.y*t;
            return t>.0001f&&y>=low-.00001f&&y<=high+.00001f;
        }
        static bool Segment(Vector3 o,Vector3 d,Vector2 p,Vector2 q)
        {
            float h=q.y-p.y,low=Mathf.Min(p.y,q.y),high=Mathf.Max(p.y,q.y);
            if(Mathf.Abs(h)<.000001f)
            {
                if(Mathf.Abs(d.y)<1e-7f)return false;
                float t=(p.y-o.y)/d.y;if(t<=.0001f)return false;
                float x=o.x+d.x*t,z=o.z+d.z*t,r2=x*x+z*z;
                return r2>=Mathf.Min(p.x*p.x,q.x*q.x)&&r2<=Mathf.Max(p.x*p.x,q.x*q.x);
            }
            float slope=(q.x-p.x)/h,r=p.x+slope*(o.y-p.y),rd=slope*d.y;
            float a=d.x*d.x+d.z*d.z-rd*rd;
            float b=2*(o.x*d.x+o.z*d.z-r*rd);
            float c=o.x*o.x+o.z*o.z-r*r;
            if(Mathf.Abs(a)<1e-7f)return Mathf.Abs(b)>1e-7f&&Root(-c/b,o,d,low,high);
            float disc=b*b-4*a*c;if(disc<0)return false;
            float root=Mathf.Sqrt(disc),stable=-.5f*(b+(b<0?-root:root));
            if(Mathf.Abs(stable)<1e-10f)return Root(-b/(2*a),o,d,low,high);
            return Root(stable/a,o,d,low,high)||Root(c/stable,o,d,low,high);
        }
    }
}
