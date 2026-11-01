using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    // Cut only this scene instance. Clip intersecting triangles at the notch
    // planes so the original towers, adjoining walls and imported asset survive.
    public static class BeachCastleCut
    {
        struct Vertex
        {
            public Vector3 point,normal;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{point=Vector3.Lerp(a.point,b.point,t),normal=Vector3.Lerp(a.normal,b.normal,t).normalized};
        }
        static List<Vertex> Half(List<Vertex> polygon,int axis,float edge,bool less)
        {
            var result=new List<Vertex>();if(polygon.Count==0)return result;
            Vertex previous=polygon[polygon.Count-1];float a=(less?1:-1)*(previous.point[axis]-edge);bool was=a<=0;
            foreach(var current in polygon)
            {
                float b=(less?1:-1)*(current.point[axis]-edge);bool inside=b<=0;
                if(inside!=was)result.Add(Vertex.Lerp(previous,current,a/(a-b)));
                if(inside)result.Add(current);
                previous=current;a=b;was=inside;
            }
            return result;
        }
        static IEnumerable<List<Vertex>> Outside(List<Vertex> polygon,Bounds cut)
        {
            for(int axis=0;axis<3;axis++)
            {
                var below=Half(polygon,axis,cut.min[axis],true);if(below.Count>=3)yield return below;
                polygon=Half(polygon,axis,cut.min[axis],false);
                var above=Half(polygon,axis,cut.max[axis],false);if(above.Count>=3)yield return above;
                polygon=Half(polygon,axis,cut.max[axis],true);
                if(polygon.Count<3)yield break;
            }
        }
        public static void Apply(Transform castle)
        {
            var worldCuts=new[]{new Bounds(new Vector3(0,2.605f,91.425f),new Vector3(2.8f,3.09f,1.75f)),
                new Bounds(new Vector3(0,5.025f,94.35f),new Vector3(2.8f,3.25f,1.7f))};
            foreach(var filter in castle.GetComponentsInChildren<MeshFilter>())
            {
                var original=filter.sharedMesh;var vertices=original.vertices;var normals=original.normals;
                var cuts=new List<Bounds>();foreach(var cut in worldCuts)
                {Vector3 lo=filter.transform.InverseTransformPoint(cut.min),hi=filter.transform.InverseTransformPoint(cut.max);cuts.Add(new Bounds((lo+hi)*.5f,hi-lo));}
                var points=new List<Vector3>();var smooth=new List<Vector3>();var submeshes=new List<int[]>();
                for(int sub=0;sub<original.subMeshCount;sub++)
                {
                    var triangles=original.GetTriangles(sub);var indices=new List<int>();
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        var pieces=new List<List<Vertex>>();var triangle=new List<Vertex>();
                        for(int k=0;k<3;k++){int index=triangles[i+k];triangle.Add(new Vertex{point=vertices[index],normal=normals[index]});}
                        pieces.Add(triangle);
                        foreach(var cut in cuts)
                        {var remaining=new List<List<Vertex>>();foreach(var piece in pieces)remaining.AddRange(Outside(piece,cut));pieces=remaining;}
                        foreach(var polygon in pieces)for(int k=1;k<polygon.Count-1;k++)
                        {
                            var a=polygon[0];var b=polygon[k];var c=polygon[k+1];
                            if(Vector3.Cross(b.point-a.point,c.point-a.point).sqrMagnitude<1e-14f)continue;
                            foreach(var v in new[]{a,b,c}){indices.Add(points.Count);points.Add(v.point);smooth.Add(v.normal);}
                        }
                    }
                    submeshes.Add(indices.ToArray());
                }
                var mesh=new Mesh{name="Sandcastle with two front restoration notches",indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(points);mesh.SetNormals(smooth);mesh.subMeshCount=submeshes.Count;
                for(int i=0;i<submeshes.Count;i++)mesh.SetTriangles(submeshes[i],i);
                mesh.RecalculateBounds();filter.sharedMesh=mesh;
                foreach(var collider in filter.GetComponents<MeshCollider>())collider.sharedMesh=mesh;
            }
        }
    }
}
