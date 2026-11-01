using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static class BeachBuild
    {
        const string Generated="Assets/Parallax/Beach/Generated/",ScenePath="Assets/Parallax/Beach/Scenes/Beach.unity";
        [Serializable] sealed class ModelData {public string name;public float[] size;public Part[] parts;}
        [Serializable] sealed class Part {public float[] vertices,color;public int[] triangles;public float metallic,roughness;}
        static T Save<T>(T value,string path) where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);return old;
        }
        static GameObject Import(string key)
        {
            var data=JsonUtility.FromJson<ModelData>(File.ReadAllText("Assets/ThirdParty/3DAssetsBeach/"+key+".json"));
            var vertices=new List<Vector3>();var triangles=new List<int[]>();var materials=new List<Material>();
            foreach(var p in data.parts)
            {
                int offset=vertices.Count;for(int i=0;i<p.vertices.Length;i+=3)vertices.Add(new Vector3(p.vertices[i],p.vertices[i+1],p.vertices[i+2]));
                var ix=new int[p.triangles.Length];for(int i=0;i<ix.Length;i++)ix[i]=p.triangles[i]+offset;triangles.Add(ix);
                var m=new Material(Shader.Find("Parallax/BeachSurface")){name=key+" material "+materials.Count,color=new Color(p.color[0],p.color[1],p.color[2],p.color[3])};m.SetFloat("_Metallic",p.metallic);m.SetFloat("_Glossiness",1-p.roughness);m.EnableKeyword("_EMISSION");materials.Add(Save(m,Generated+key+"_"+materials.Count+".mat"));
            }
            var mesh=new Mesh{name=key+" original imported mesh",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.subMeshCount=triangles.Count;for(int i=0;i<triangles.Count;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=Save(mesh,Generated+key+".asset");
            var obj=new GameObject(key);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
            var prefab=PrefabUtility.SaveAsPrefabAsset(obj,Generated+key+".prefab");UnityEngine.Object.DestroyImmediate(obj);return prefab;
        }
        [MenuItem("Parallax/마지막 해변 씬 준비")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Generated);Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));AssetDatabase.Refresh();
            var models=new Dictionary<string,GameObject>();foreach(string key in new[]{"umbrella","ball","chair","mat","sandcastle","fence","driftwood","surfboard","volleyball","hirehut","boardrack","cooler"})models[key]=Import(key);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var game=BeachWorld.Build(key=>models[key]);
            // Procedural parasol fabric/ribs must be assets before the saved scene
            // is reopened or copied into the isolated player-build project.
            foreach(var filter in game.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;
                if(mesh==null||AssetDatabase.Contains(mesh))continue;
                string meshPath=Generated+mesh.name.Replace('/','_')+".asset";
                var saved=Save(mesh,meshPath);filter.sharedMesh=saved;
                foreach(var collider in filter.GetComponents<MeshCollider>())if(collider.sharedMesh==mesh||collider.sharedMesh==null)collider.sharedMesh=saved;
            }
            foreach(var material in BeachWorld.Materials)
            {
                string path=Generated+material.name.Replace('/','_')+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(existing==null)AssetDatabase.CreateAsset(material,path);
                else
                {
                    EditorUtility.CopySerialized(material,existing);
                    foreach(var renderer in game.GetComponentsInChildren<Renderer>(true))
                    {
                        var slots=renderer.sharedMaterials;bool changed=false;
                        for(int i=0;i<slots.Length;i++)if(slots[i]==material){slots[i]=existing;changed=true;}
                        if(changed)renderer.sharedMaterials=slots;
                    }
                    if(RenderSettings.skybox==material)RenderSettings.skybox=existing;
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            PlayerSettings.productName="Parallax - Last Beach v"+BeachGame.Version;PlayerSettings.bundleVersion=BeachGame.Version;PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            Debug.Log("BEACH_PREPARE_OK props="+game.props.Length);
        }
        [MenuItem("Parallax/마지막 해변 Windows 빌드")]
        public static void Build()
        {BuildPlayer(Parallax.Editor.BuildPaths.Output("Parallax_Unity_v"+BeachGame.Version+"_Beach/Parallax.exe"),"Validation/Beach/build-result.txt");}
        public static void BuildWaterBaseline()
        {BuildPlayer(Path.GetFullPath("Validation/BeachWater/Baseline/Runtime/Parallax.exe"),"Validation/BeachWater/Baseline/build-result.txt");}
        static void BuildPlayer(string output,string resultPath)
        {
            Prepare();Directory.CreateDirectory(Path.GetDirectoryName(output));
            if(ShaderUtil.ShaderHasError(Shader.Find("Parallax/BeachWaterRefresh")))throw new Exception("Beach water shader compile failed");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.CleanBuildCache});
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));File.WriteAllText(resultPath,"Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nOutput: "+output);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Beach build failed");Debug.Log("BEACH_BUILD_OK "+output);
        }
    }
}
