using System;
using System.IO;
using System.Linq;
using ShadeLink.Gallery;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    public static class GalleryBuild
    {
        const string Generated="Assets/Parallax/Gallery/Generated/";
        const string ScenePath="Assets/Parallax/Gallery/Scenes/Gallery.unity";
        public static void WriteCredits(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory,"Museum-Credits.txt"),
                "PARALLAX MUSEUM - THIRD-PARTY ART\n\n"+
                "Winged Victory of Samothrace - 3D scan by Cosmo Wenman, Skulpturhalle Basel plaster cast. CC BY 3.0. Converted, shaded and scaled for Parallax.\n"+
                "Source: https://www.thingiverse.com/thing:196038\n"+
                "Creator: https://cosmowenman.com/2013/12/28/winged-victory-published-update-12282013/\n"+
                "License: https://creativecommons.org/licenses/by/3.0/\n\n"+
                "La Cathedrale (The Cathedral), Auguste Rodin - 3D scan by Migg, Rodin Museum, Paris. CC BY 4.0.\n"+
                "Source: https://sketchfab.com/3d-models/la-cathedrale-d0ba1f143173484f87961fc50d46bdcf\n"+
                "Archive: https://zenodo.org/records/10364942\n"+
                "License: https://creativecommons.org/licenses/by/4.0/\n"+
                "Changes: removed scanned display support and detached artifacts; reoriented, scaled, shaded and adapted for the traversable portal in Parallax.\n\n"+
                "Medieval Library Kit - Rinku Choudhary. The portal reuses furniture and books from the destination Library stage.\n"+
                "Source: https://sketchfab.com/3d-models/medieval-library-kit-c018093ba87d45f28f30ec8acf2f6604\n"+
                "License: Free Standard (NoAI), as recorded for the existing Library assets. This asset is not CC BY.\n\n"+
                "Marble Bust 01 - Rico Cilliers / Poly Haven, CC0.\nhttps://polyhaven.com/a/marble_bust_01\n\n"+
                "Potted Plant 02 - Rico Cilliers / Poly Haven, CC0.\nhttps://polyhaven.com/a/potted_plant_02\n\n"+
                "Surface textures: Poly Haven, CC0. https://polyhaven.com/license\n"+
                "Paintings: Public-domain sources from Wikimedia Commons.\n\n");
            foreach(string name in new[]{"MuseumArt-Credits.md","MuseumMaterial-Credits.md","MuseumScan-Credits.md"})
            {
                string source=Path.Combine("Documentation/Gallery",name);
                if(File.Exists(source))File.Copy(source,Path.Combine(outputDirectory,name),true);
            }
        }
        static void Integrate(GalleryGame game)
        {
            GalleryConceptScans.Install(game);
            GalleryConceptBusts.Install(game);
            GalleryConceptPlants.Install(game);
            GalleryConceptStage.Install(game);
            GalleryConceptAtmosphere.Install(game);
            game.shadowCourse=GalleryMuseumShadowCourse.Install(game);
            game.portraitGate=GalleryPortraitGate.Install(game);
            game.orcusExit=GalleryConceptExit.Install(game);
            GalleryConceptLighting.Configure(game);
        }
        static void SaveMuseumMeshes(GalleryGame game)
        {
            // Scene references to temporary procedural meshes would disappear in
            // a player build. Persist every generated mesh before saving the scene.
            int index=0;
            AssetDatabase.StartAssetEditing();
            try
            {
            foreach(var group in game.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh)).GroupBy(f=>f.sharedMesh).ToArray())
            {
                Mesh mesh=group.Key;string name="MuseumMesh_"+(index++).ToString("D3")+"_"+mesh.name;
                foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
                string path=Generated+name+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null){AssetDatabase.CreateAsset(mesh,path);existing=mesh;}
                else EditorUtility.CopySerialized(mesh,existing);
                foreach(var filter in group)filter.sharedMesh=existing;
                foreach(var collider in game.GetComponentsInChildren<MeshCollider>(true))if(collider.sharedMesh==mesh)collider.sharedMesh=existing;
            }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }
        [MenuItem("Parallax/미술관 씬 준비")]
        public static void Prepare()
        {
            GalleryCathedralImport.Ensure();
            GalleryPortalLibraryAssets.Prepare();
            Directory.CreateDirectory(Generated);Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            GalleryScanCollision.Ensure();
            GalleryGame game=GalleryConceptWorld.Build();Integrate(game);
            SaveMuseumMeshes(game);
            foreach(var m in GalleryWorld.Materials)
            {
                string path=Generated+m.name.Replace('/','_')+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(existing==null)AssetDatabase.CreateAsset(m,path);
                else
                {
                    EditorUtility.CopySerialized(m,existing);
                    foreach(var r in game.GetComponentsInChildren<Renderer>())
                    {
                        var slots=r.sharedMaterials;bool changed=false;
                        for(int i=0;i<slots.Length;i++)if(slots[i]==m){slots[i]=existing;changed=true;}
                        if(changed)r.sharedMaterials=slots;
                    }
                    if(RenderSettings.skybox==m)RenderSettings.skybox=existing;
                    UnityEngine.Object.DestroyImmediate(m);
                }
            }
            game.lighting.Rebuild();
            if(game.props.Length!=6||game.GetComponentInChildren<GalleryConceptPerspective>()==null||!game.conceptLayout||game.orcusExit==null||game.portraitGate==null||game.shadowCourse==null||game.nikeLight==null)throw new Exception("Playable museum exhibit missing");
            foreach(string shader in new[]{"Parallax/MuseumSurface","Parallax/GalleryConceptPostFX","Parallax/MuseumFoliage","Parallax/MuseumLibraryPortal","Hidden/Parallax/MuseumNikeDepth"})
            {
                var asset=Shader.Find(shader);
                if(asset==null||ShaderUtil.ShaderHasError(asset))throw new Exception("Concept shader invalid: "+shader);
            }
            EditorSceneManager.SaveScene(scene,ScenePath);
            PlayerSettings.productName="Parallax - Museum Gallery v"+GalleryGame.Version;PlayerSettings.bundleVersion=GalleryGame.Version;
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene(LibraryBuild.ScenePath,true)};
            AssetDatabase.SaveAssets();Debug.Log("GALLERY_PREPARE_OK casters="+game.lighting.Count);
        }
        [MenuItem("Parallax/미술관 Windows 빌드")]
        public static void Build()
        {
            // This museum update ships the already-authored Library scene.
            // Regenerating another stage here would overwrite its saved work.
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(LibraryBuild.ScenePath)==null)
                throw new FileNotFoundException("The existing destination Library scene is missing.",LibraryBuild.ScenePath);
            Prepare();
            foreach(string shader in new[]{"Parallax/GallerySurface","Parallax/MuseumSurface","Parallax/GalleryMirror","Parallax/GalleryGlass","Parallax/GalleryOrcusPortal"})
                if(ShaderUtil.ShaderHasError(Shader.Find(shader)))
                {
                    foreach(var message in ShaderUtil.GetShaderMessages(Shader.Find(shader)))
                        Debug.LogError(shader+" line "+message.line+": "+message.message);
                    throw new Exception(shader+" has compilation errors; build stopped.");
                }
            string output=Parallax.Editor.BuildPaths.Output("Parallax_Unity_v"+GalleryGame.Version+"_MuseumGallery/Parallax.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath,LibraryBuild.ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            Directory.CreateDirectory("Validation/Gallery");
            File.WriteAllText("Validation/Gallery/build-result.txt","Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nOutput: "+output+"\nUnity: "+Application.unityVersion);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Gallery build failed");
            WriteCredits(Path.GetDirectoryName(output));
            Debug.Log("GALLERY_BUILD_OK "+output);
        }
        public static void BuildVerified()
        {
            GalleryMuseumBuildChecks.Run();
            GalleryCathedralPlacementChecks.Run();
            Build();
            GalleryReviewManifest.Save();
        }
    }
}
