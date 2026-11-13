using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static class BeachPropGeometryTests
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Parallax/Beach/Scenes/Beach.unity",OpenSceneMode.Single);
            var game=UnityEngine.Object.FindFirstObjectByType<BeachGame>();Physics.SyncTransforms();
            var errors=new List<string>();int checks=0;
            void Check(string name,Action action)
            {
                checks++;try{action();Debug.Log("BEACH_PROP_GEOMETRY_PASS "+name);}
                catch(Exception error){errors.Add(name+": "+error.Message);Debug.Log("BEACH_PROP_GEOMETRY_FAIL "+errors.Last());}
            }
            Check("Static model collisions use the visible replacement mesh",()=>
            {
                foreach(var filter in game.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<Renderer>()?.enabled==true))
                    foreach(var collider in filter.GetComponents<MeshCollider>().Where(c=>c.enabled))
                        if(collider.sharedMesh==null||!collider.sharedMesh.vertices.SequenceEqual(filter.sharedMesh.vertices)||
                            !Triangles(collider.sharedMesh).SequenceEqual(Triangles(filter.sharedMesh)))
                            throw new Exception(filter.name+" retained different collision geometry.");
            });
            Check("Decorative furniture and balls have no empty box collision",()=>
            {
                var decorations=game.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("장식 · ")).ToArray();
                if(decorations.Length!=3||decorations.Any(t=>t.GetComponentsInChildren<Collider>().Any(c=>!(c is MeshCollider))))
                    throw new Exception("Static decoration still uses a placeholder box or disabled sphere.");
            });
            foreach(var prop in game.props.Where(p=>p.transform.Find("파라솔 기둥")!=null))
                Check(prop.title+" empty canopy corners, underside and pole foot",()=>
                {
                    var filter=prop.GetComponentInChildren<MeshFilter>();var mesh=filter.sharedMesh;
                    foreach(var part in prop.parts)for(int k=0;k<8;k++)
                    {
                        var corner=part.transform.TransformPoint(part.center+Vector3.Scale(part.size*.5f,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));
                        var point=filter.transform.InverseTransformPoint(corner);var bounds=mesh.bounds;
                        if(point.x<bounds.min.x-.002f||point.x>bounds.max.x+.002f||point.z<bounds.min.z-.002f||point.z>bounds.max.z+.002f)
                            throw new Exception("Collision extends beyond the visible umbrella width.");
                    }
                    foreach(var point in new[]{new Vector3(1.2f,2.005f,1.2f),new Vector3(.7f,1.78f,0),new Vector3(.18f,.05f,0)})
                        if(Solid(prop,filter.transform.TransformPoint(point)))throw new Exception("Invisible collision at mesh local "+point);
                    var vertices=mesh.vertices;var triangles=mesh.triangles;
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                        var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.y<.5f)continue;
                        var world=filter.transform.TransformPoint((a+b+c)/3-normal*.005f);
                        if(!Solid(prop,world))throw new Exception("Visible fabric triangle has no matching collision.");
                    }
                });
            Check("Mirror has no obsolete central post",()=>Free(game.mirror.prop,new Vector3(0,.7f,0)));
            Check("Regeneration preserves all shared signboard meshes",()=>
            {
                var boards=game.GetComponentsInChildren<Transform>().Where(t=>t.name=="Beach rental sign").Select(t=>t.Find("Sign board").GetComponent<MeshFilter>()).ToArray();
                if(boards.Length!=4||boards.Any(f=>f.sharedMesh==null))throw new Exception("A visible signboard lost its mesh while retaining collision.");
            });
            Check("Boat hull follows its tapered sides and bow",()=>
            {Free(game.boat.Prop,new Vector3(.64f,-.14f,0));Free(game.boat.Prop,new Vector3(.15f,-.02f,1.08f));});
            for(int i=0;i<game.shadowHarbor.pieces.Length;i++)
            {
                var piece=game.shadowHarbor.pieces[i];var plate=game.shadowHarbor.assistPlates[i];int index=i;
                Check(piece.title+" respects its cutout corners and keeps drop assistance",()=>
                {
                    var point=plate.center+Vector3.Scale(plate.size,new Vector3(.45f,index==0?-.4f:.4f,0));
                    var world=plate.transform.TransformPoint(point);
                    if(Solid(piece,world))throw new Exception("Empty cutout corner is solid.");
                    if(!plate.enabled)throw new Exception("Shadow alignment reference was disabled.");
                    if(piece.parts.Any(p=>p.isTrigger))throw new Exception("Alignment reference entered the placement silhouette.");
                });
            }
            if(game.props.Length!=11)errors.Add("Unexpected puzzle prop registry; audit scope must be updated.");
            if(errors.Count>0)throw new Exception(string.Join("\n",errors));
            Debug.Log("BEACH_PROP_GEOMETRY_OK checks="+checks+" props="+game.props.Length);
        }
        static bool Solid(BeachProp prop,Vector3 world)=>prop.GetComponentsInChildren<Collider>()
            .Any(c=>c.enabled&&!c.isTrigger&&(c.ClosestPoint(world)-world).sqrMagnitude<.00000001f);
        static IEnumerable<(int,int,int)> Triangles(Mesh mesh)
        {
            var indices=mesh.triangles;
            return Enumerable.Range(0,indices.Length/3).Select(i=>(indices[i*3],indices[i*3+1],indices[i*3+2]))
                .OrderBy(t=>t.Item1).ThenBy(t=>t.Item2).ThenBy(t=>t.Item3);
        }
        static void Free(BeachProp prop,Vector3 local)
        {if(Solid(prop,prop.transform.TransformPoint(local)))throw new Exception("Invisible collision at local "+local);}
    }
}
