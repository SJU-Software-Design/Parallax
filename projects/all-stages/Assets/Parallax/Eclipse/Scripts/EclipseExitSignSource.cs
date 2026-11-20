using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Eclipse
{
    // A derivative of the officially downloaded original FBX, not a replacement model.
    // Subtract the established i-stem rectangle from every source triangle in sign space.
    public sealed class EclipseExitSignSource : MonoBehaviour
    {
        public const string ModelResource="Eclipse/Selected/ExitSign/ExitSignOriginal";
        public const string TextureResource="Eclipse/Selected/ExitSign/Background";
        public bool Ready {get;private set;}
        public int OriginalVertices {get;private set;}
        public int OriginalTriangles {get;private set;}
        public int AdaptedTriangles {get;private set;}
        public bool OpeningMeshVerified {get;private set;}
        public bool SurfaceUvVerified {get;private set;}
        public Material SourceMaterial => surface;
        public Bounds OriginalBounds {get;private set;}
        public Transform SourceVisual {get;private set;}
        public string Status {get;private set;}="Not built";
        Material surface;
        readonly List<Mesh> meshes=new List<Mesh>();
        struct V
        {
            public Vector3 p,n;public Vector2 uv;
            public static V Lerp(V a,V b,float t)=>new V{p=Vector3.LerpUnclamped(a.p,b.p,t),n=Vector3.LerpUnclamped(a.n,b.n,t).normalized,uv=Vector2.LerpUnclamped(a.uv,b.uv,t)};
        }
        public static EclipseExitSignSource Install(Transform sign)
        {
            var owner=sign.gameObject.AddComponent<EclipseExitSignSource>();owner.Build(sign);return owner;
        }
        void Build(Transform sign)
        {
            var model=Resources.Load<GameObject>(ModelResource);
            var texture=Resources.Load<Texture2D>(TextureResource);
            var shader=Resources.Load<Shader>("Eclipse/Selected/EclipseExitSource");
            if(model==null||texture==null||shader==null)
            {Status="Official original resource missing";Debug.LogWarning("ECLIPSE_EXIT_SOURCE_INCOMPLETE "+Status);return;}
            var original=Instantiate(model);original.name="Temporary original Exit FBX / source conversion";
            original.SetActive(false);original.transform.position=Vector3.zero;
            try
            {
                var filters=original.GetComponentsInChildren<MeshFilter>(true);
                bool first=true;Bounds bounds=new Bounds();
                foreach(var f in filters)
                {
                    if(f.sharedMesh==null)continue;Bounds b=f.sharedMesh.bounds;
                    for(int i=0;i<8;i++)
                    {
                        Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        p=f.transform.TransformPoint(p);
                        if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                    }
                }
                OriginalBounds=bounds;
                if(first||bounds.size.x<.00001f||bounds.size.y<.00001f)
                    throw new InvalidOperationException("The original Exit FBX has no readable upright source geometry");
                Vector3 scale=new Vector3(EclipseFinaleWorld.SignWidth/bounds.size.x,EclipseFinaleWorld.SignHeight/bounds.size.y,
                    .10f/Mathf.Max(.00001f,bounds.size.z));
                var fit=Matrix4x4.Scale(scale)*Matrix4x4.Translate(-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
                surface=new Material(shader){name="vietp6105 original Exit image / double-sided planar UV adaptation",mainTexture=texture};
                SourceVisual=new GameObject("Official Exit Sign / vietp6105 / cut source mesh").transform;
                SourceVisual.SetParent(sign,false);
                var marker=SourceVisual.gameObject.AddComponent<EclipseSelectedAssetMarker>();
                marker.assetId="vietp6105/ExitSign/d554b032aa154df295e42f6e29714b70";marker.author="vietp6105";
                marker.sourceUrl="https://sketchfab.com/3d-models/exit-sign-d554b032aa154df295e42f6e29714b70";marker.license="CC BY 4.0";
                OpeningMeshVerified=true;SurfaceUvVerified=true;
                foreach(var f in filters)
                {
                    Mesh source=f.sharedMesh;if(source==null)continue;
                    OriginalVertices+=source.vertexCount;OriginalTriangles+=source.triangles.Length/3;
                    var matrix=fit*f.transform.localToWorldMatrix;
                    Mesh derivative=Cut(source,matrix);
                    var part=new GameObject("Original source part / "+f.name);part.transform.SetParent(SourceVisual,false);
                    part.AddComponent<MeshFilter>().sharedMesh=derivative;
                    var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=surface;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    meshes.Add(derivative);AdaptedTriangles+=derivative.triangles.Length/3;
                }
                Ready=OriginalTriangles>0&&AdaptedTriangles>0&&OpeningMeshVerified&&SurfaceUvVerified;
                if(Ready)
                {
                    // Existing SignPart colliders remain the sole gameplay geometry.
                    foreach(var r in sign.GetComponentsInChildren<Renderer>(true))
                        if(!r.transform.IsChildOf(SourceVisual)&&r.name!="Black depth inside the i")r.enabled=false;
                }
                Status="Original vertices="+OriginalVertices+" triangles="+OriginalTriangles+" cutTriangles="+AdaptedTriangles+
                    " openingVerified="+OpeningMeshVerified+" planarUvVerified="+SurfaceUvVerified+" sourceTexture="+texture.width+"x"+texture.height;
                Debug.Log("ECLIPSE_EXIT_ORIGINAL_READY "+Status);
            }
            finally{if(Application.isPlaying)Destroy(original);else DestroyImmediate(original);}
        }
        Mesh Cut(Mesh source,Matrix4x4 matrix)
        {
            var src=source.vertices;var normals=source.normals;var uv=source.uv;var indices=source.triangles;
            var vertices=new List<Vector3>();var targetNormals=new List<Vector3>();var targetUV=new List<Vector2>();var triangles=new List<int>();
            var normalMatrix=matrix.inverse.transpose;
            float left=EclipseFinaleWorld.DoorX-EclipseFinaleWorld.DoorWidth*.5f;
            float right=EclipseFinaleWorld.DoorX+EclipseFinaleWorld.DoorWidth*.5f;
            float bottom=EclipseFinaleWorld.DoorBottom,top=bottom+EclipseFinaleWorld.DoorHeight;
            for(int i=0;i<indices.Length;i+=3)
            {
                var polygon=new List<V>(3);
                for(int j=0;j<3;j++)
                {
                    int k=indices[i+j];polygon.Add(new V{p=matrix.MultiplyPoint3x4(src[k]),
                        n=normals.Length==src.Length?normalMatrix.MultiplyVector(normals[k]).normalized:Vector3.back,
                        uv=uv.Length==src.Length?uv[k]:Vector2.zero});
                }
                // The four disjoint regions are the exact complement of the hole.
                Append(Clip(polygon,0,left,false),vertices,targetNormals,targetUV,triangles);
                Append(Clip(polygon,0,right,true),vertices,targetNormals,targetUV,triangles);
                var middle=Clip(Clip(polygon,0,left,true),0,right,false);
                Append(Clip(middle,1,bottom,false),vertices,targetNormals,targetUV,triangles);
                Append(Clip(middle,1,top,true),vertices,targetNormals,targetUV,triangles);
            }
            var mesh=new Mesh{name="vietp6105 source Exit / real i opening cut",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetNormals(targetNormals);mesh.SetUVs(0,targetUV);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            return mesh;
        }
        static List<V> Clip(List<V> input,int axis,float bound,bool greater)
        {
            var output=new List<V>();if(input.Count==0)return output;
            V previous=input[input.Count-1];float pd=(greater?1:-1)*(previous.p[axis]-bound);bool pin=pd>=-1e-7f;
            foreach(V current in input)
            {
                float cd=(greater?1:-1)*(current.p[axis]-bound);bool cin=cd>=-1e-7f;
                if(cin!=pin){float t=pd/(pd-cd);output.Add(V.Lerp(previous,current,t));}
                if(cin)output.Add(current);previous=current;pd=cd;pin=cin;
            }
            return output;
        }
        static readonly float[] UvKnots={-1.56f,-1.50f,.50f,.70f,1.00f,1.08f,1.56f};
        public static Vector2 SignSpaceUv(Vector3 local)
        {
            float x=local.x,u;
            if(x< -1.56f)u=Mathf.Lerp(0,.037f,Mathf.Clamp01((x+1.7f)/.14f));
            else if(x< -1.50f)u=Mathf.Lerp(.037f,.12857f,Mathf.Clamp01((x+1.56f)/.06f));
            else if(x< .50f)u=Mathf.Lerp(.12857f,.45f,Mathf.Clamp01((x+1.50f)/2));
            else if(x< .70f)u=Mathf.Lerp(.45f,.49f,Mathf.Clamp01((x-.50f)/.20f));
            else if(x< 1.00f)u=Mathf.Lerp(.49f,.605f,Mathf.Clamp01((x-.70f)/.30f));
            else if(x< 1.08f)u=Mathf.Lerp(.605f,.70f,Mathf.Clamp01((x-1.00f)/.08f));
            else if(x< 1.56f)u=Mathf.Lerp(.70f,.94f,Mathf.Clamp01((x-1.08f)/.48f));
            else u=Mathf.Lerp(.94f,1,Mathf.Clamp01((x-1.56f)/.14f));
            return new Vector2(u,Mathf.Clamp01(local.y/EclipseFinaleWorld.SignHeight));
        }
        void Append(List<V> polygon,List<Vector3> vertices,List<Vector3> normals,List<Vector2> uv,List<int> triangles)
        {
            if(polygon.Count<3)return;
            var remaining=polygon;
            foreach(float knot in UvKnots)
            {
                var band=Clip(remaining,0,knot,false);
                AppendBand(band,vertices,normals,uv,triangles);
                remaining=Clip(remaining,0,knot,true);
                if(remaining.Count<3)return;
            }
            AppendBand(remaining,vertices,normals,uv,triangles);
        }
        void AppendBand(List<V> polygon,List<Vector3> vertices,List<Vector3> normals,List<Vector2> uv,List<int> triangles)
        {
            for(int i=1;i+1<polygon.Count;i++)
            {
                V a=polygon[0],b=polygon[i],c=polygon[i+1];
                if(Vector3.Cross(b.p-a.p,c.p-a.p).sqrMagnitude<1e-14f)continue;
                Vector3 center=(a.p+b.p+c.p)/3;
                if(center.x>EclipseFinaleWorld.DoorX-EclipseFinaleWorld.DoorWidth*.5f+1e-5f&&
                    center.x<EclipseFinaleWorld.DoorX+EclipseFinaleWorld.DoorWidth*.5f-1e-5f&&
                    center.y>EclipseFinaleWorld.DoorBottom+1e-5f&&center.y<EclipseFinaleWorld.DoorBottom+EclipseFinaleWorld.DoorHeight-1e-5f)
                    OpeningMeshVerified=false;
                int n=vertices.Count;vertices.Add(a.p);vertices.Add(b.p);vertices.Add(c.p);
                normals.Add(a.n);normals.Add(b.n);normals.Add(c.n);
                Vector2 ua=SignSpaceUv(a.p),ub=SignSpaceUv(b.p),uc=SignSpaceUv(c.p);
                uv.Add(ua);uv.Add(ub);uv.Add(uc);
                SurfaceUvVerified &= ua.x>=0&&ua.x<=1&&ua.y>=0&&ua.y<=1&&ub.x>=0&&ub.x<=1&&ub.y>=0&&ub.y<=1&&uc.x>=0&&uc.x<=1&&uc.y>=0&&uc.y<=1;
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
            }
        }
        public void SetDarkness(float darkness){if(surface!=null)surface.SetFloat("_Darkness",darkness);}
        void OnDestroy(){if(surface!=null)Destroy(surface);foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}