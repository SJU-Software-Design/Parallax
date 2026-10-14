using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using ShadeLink.Starship;

namespace ShadeLink.Editor
{
    public static class StarshipBuild
    {
        const string Generated="Assets/Parallax/Starship/Generated/";
        const string ScenePath="Assets/Parallax/Starship/Scenes/Starship.unity";
        const string Models="Assets/ThirdParty/KenneySpaceStation/Models/FBX format/";
        static readonly Dictionary<string,GameObject> models=new Dictionary<string,GameObject>();
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static T Save<T>(T value,string path) where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);return old;
        }
        static GameObject Model(string key)
        {
            if(models.TryGetValue(key,out var saved))return saved;
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Models+key+".fbx");
            if(asset==null)throw new Exception("Missing CC0 station model: "+key);
            var go=UnityEngine.Object.Instantiate(asset);go.name=key;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var assigned=renderer.sharedMaterials;
                for(int i=0;i<assigned.Length;i++)
                {
                    var source=assigned[i];string id=source.name.Replace('/','_');
                    if(!materials.TryGetValue(id,out var m))
                    {
                        m=new Material(Shader.Find("Parallax/StarshipSurface")){name="Station "+id};
                        if(source.HasProperty("_Color"))m.color=source.color;
                        if(source.HasProperty("_MainTex"))m.mainTexture=source.mainTexture;
                        if(m.mainTexture==null)
                        {
                            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/ThirdParty/KenneySpaceStation/Models/Textures"}))
                            {m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));break;}
                        }
                        m.SetFloat("_Metallic",.23f);m.SetFloat("_Glossiness",.28f);m.EnableKeyword("_EMISSION");
                        m=Save(m,Generated+"Station_"+id+".mat");materials.Add(id,m);
                    }
                    assigned[i]=m;
                }
                renderer.sharedMaterials=assigned;
            }
            foreach(var collider in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
            saved=PrefabUtility.SaveAsPrefabAsset(go,Generated+key+".prefab");UnityEngine.Object.DestroyImmediate(go);models[key]=saved;return saved;
        }
        [MenuItem("Parallax/HELIOS 우주선 씬 준비")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Generated);Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));AssetDatabase.Refresh();models.Clear();materials.Clear();
            foreach(string name in new[]{"Parallax/StarshipSurface","Parallax/StarshipSky","Parallax/StarshipGlass","Parallax/StarshipPlanet","Parallax/PlacementGhost","Parallax/PlacementDepth","Parallax/GalleryMirror","Parallax/BeachWater"})
            {var shader=Shader.Find(name);if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Shader failed: "+name);}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var game=StarshipWorld.Build(Model);
            foreach(var material in StarshipWorld.Materials)
            {
                string path=Generated+material.name.Replace('/','_')+".mat";
                var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(existing==null)AssetDatabase.CreateAsset(material,path);
                else
                {
                    EditorUtility.CopySerialized(material,existing);
                    foreach(var renderer in game.GetComponentsInChildren<Renderer>())
                    {
                        var ms=renderer.sharedMaterials;bool changed=false;
                        for(int i=0;i<ms.Length;i++)if(ms[i]==material){ms[i]=existing;changed=true;}
                        if(changed)renderer.sharedMaterials=ms;
                    }
                    if(RenderSettings.skybox==material)RenderSettings.skybox=existing;
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            PlayerSettings.productName="Parallax - HELIOS Starship v"+StarshipGame.Version;PlayerSettings.bundleVersion=StarshipGame.Version;
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=false;PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            Debug.Log("STARSHIP_PREPARE_OK props="+game.props.Length);
        }
        [MenuItem("Parallax/HELIOS 우주선 Windows 빌드")]
        public static void Build()
        {
            Prepare();
            string output=Parallax.Editor.BuildPaths.Output("Parallax_Unity_v"+StarshipGame.Version+"_Starship/Parallax.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            Directory.CreateDirectory("Validation/Starship");
            File.WriteAllText("Validation/Starship/build-result.txt","Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nOutput: "+output);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Starship build failed");
            Debug.Log("STARSHIP_BUILD_OK "+output);
        }
    }
}
