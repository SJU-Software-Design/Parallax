using System;
using System.IO;
using System.Linq;
using ShadeLink.Gallery;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

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
                "Winged Victory of Samothrace - 3D scan by Cosmo Wenman, Skulpturhalle Basel plaster cast. CC BY 3.0. Converted, shaded, scaled and mesh simplified for Parallax.\n"+
                "Source: https://www.thingiverse.com/thing:196038\n"+
                "Creator: https://cosmowenman.com/2013/12/28/winged-victory-published-update-12282013/\n"+
                "License: https://creativecommons.org/licenses/by/3.0/\n\n"+
                "La Cathedrale (The Cathedral), Auguste Rodin - 3D scan by Migg, Rodin Museum, Paris. CC BY 4.0.\n"+
                "Source: https://sketchfab.com/3d-models/la-cathedrale-d0ba1f143173484f87961fc50d46bdcf\n"+
                "Archive: https://zenodo.org/records/10364942\n"+
                "License: https://creativecommons.org/licenses/by/4.0/\n"+
                "Changes: removed scanned display support and detached artifacts; reoriented, scaled, shaded and adapted for the traversable portal in Parallax.\n\n"+
                "Medieval Library Kit - Rinku Choudhary. Legacy Library resources are preserved; the current museum portal leads to the Beach stage.\n"+
                "Source: https://sketchfab.com/3d-models/medieval-library-kit-c018093ba87d45f28f30ec8acf2f6604\n"+
                "License: Free Standard (NoAI), as recorded for the existing Library assets. This asset is not CC BY.\n\n"+
                "Marble Bust 01 - Rico Cilliers / Poly Haven, CC0.\nhttps://polyhaven.com/a/marble_bust_01\n\n"+
                "Aurelius (PhotoScan) - ryanfb, portrait bust of Marcus Aurelius, Art Institute of Chicago. CC BY 4.0.\n"+
                "Source: https://sketchfab.com/3d-models/aurelius-photoscan-cad3a072546a457fa1e2e5a90b25063a\n"+
                "License: https://creativecommons.org/licenses/by/4.0/\n"+
                "Changes: original OBJ and albedo imported, scaled and shaded for a movable marble exhibit with a new lamp-cover plinth.\n\n"+
                "Potted Plant 02 - Rico Cilliers / Poly Haven, CC0.\nhttps://polyhaven.com/a/potted_plant_02\n\n"+
                "Surface textures: Poly Haven, CC0. https://polyhaven.com/license\n"+
                "Historic paintings: Public-domain sources from Wikimedia Commons. The completed Nike canvas is a generated painting based on the supplied concept reference.\n"+
                "Mae West face and room references: Salvador Dali; installation with Oscar Tusquets. Reproductions from Museum Boijmans Van Beuningen and ARTE.it. Rights reserved; no public-domain or reuse license is asserted. See MuseumArt-Credits.md and MuseumArt/sources.json for source URLs.\n\n");
            foreach(string name in new[]{"MuseumArt-Credits.md","MuseumMaterial-Credits.md","MuseumScan-Credits.md"})
            {
                string source=Path.Combine("Documentation/Gallery",name);
                if(File.Exists(source))File.Copy(source,Path.Combine(outputDirectory,name),true);
            }
            // Every museum build entry point shares the exhibition and destination
            // credits, including the source assets bundled in Resources.
            foreach(var credit in new[]
            {
                new[]{"Documentation/Gallery/MuseumAudio-Credits.txt","MuseumAudio-Credits.txt"},
                new[]{"Documentation/Gallery/MuseumBottleBalance-Credits.txt","MuseumBottleBalance-Credits.txt"},
                new[]{"Documentation/Gallery/MuseumDikeAudio-Credits.txt","MuseumDikeAudio-Credits.txt"},
                new[]{"Documentation/Gallery/MuseumDike-assets.md","MuseumDike-assets.md"},
                new[]{"Documentation/Gallery/BottleBalanceKeyReward-assets.md","BottleBalanceKeyReward-assets.md"},
                new[]{"Assets/Parallax/Gallery/ThirdParty/BottleBalanceKeyReward/CREDITS.txt","MuseumKey-CREDITS.txt"},
                new[]{"Assets/Parallax/Gallery/Resources/MuseumBalanceArt/CREDITS.txt","MuseumBalanceArt-CREDITS.txt"},
                new[]{"Assets/Parallax/Gallery/ThirdParty/BottleBalanceDikeStatue/CREDITS.txt","DikeStatue-CREDITS.txt"},
                new[]{"Assets/Parallax/Gallery/ThirdParty/BottleBalanceDikeProps/CREDITS.txt","DikeProps-CREDITS.txt"},
                new[]{"Assets/Parallax/Gallery/ThirdParty/BottleBalanceDikeSword/CREDITS.txt","DikeSword-CREDITS.txt"},
                new[]{"Assets/Parallax/Gallery/ThirdParty/DikePedestalP02/CREDITS.txt","DikePedestalP02-CREDITS.txt"}
            })
            {
                if(!File.Exists(credit[0]))throw new FileNotFoundException("Required museum asset credits are missing.",credit[0]);
                File.Copy(credit[0],Path.Combine(outputDirectory,credit[1]),true);
            }
            WriteBeachCredits(outputDirectory);
        }
        static void WriteBeachCredits(string outputDirectory)
        {
            string target=Path.Combine(outputDirectory,"Beach-Credits");
            foreach(string folder in new[]{"Assets/ThirdParty/3DAssetsBeach","Assets/ThirdParty/LowPolyFlowerDemo",
                "Assets/ThirdParty/PlankTexturesPBR","Assets/ThirdParty/ToonTasticBeachProps","Assets/ThirdParty/YughuesCastleSand",
                "Assets/ThirdParty/YughuesFreeSandMaterials","Assets/ThirdParty/SimpleWaterShaderURP"})
            {
                if(!Directory.Exists(folder))continue;
                foreach(string path in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
                {
                    string name=Path.GetFileName(path).ToLowerInvariant();
                    if(name.EndsWith(".meta")||!(name.Contains("license")||name.Contains("credit")||name.Contains("notice")||name=="source.json"))continue;
                    string destination=Path.Combine(target,Path.GetFileName(folder),path.Substring(folder.Length+1));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(path,destination,true);
                }
            }
        }
        static void Integrate(GalleryGame game)
        {
            var nike=GalleryConceptScans.Install(game);
            GalleryConceptBusts.Install(game);
            GalleryConceptPlants.Install(game);
            GalleryConceptStage.Install(game);
            GalleryConceptAtmosphere.Install(game);
            game.shadowCourse=GalleryMuseumShadowCourse.Install(game,nike);
            var canvas=game.shadowCourse.nikeLight.canvas;
            const string guidePath=Generated+"Nike shadow alignment guide.asset";
            var guide=canvas.BakeGuide();
            var storedGuide=AssetDatabase.LoadAssetAtPath<Texture2D>(guidePath);
            if(storedGuide==null){AssetDatabase.CreateAsset(guide,guidePath);storedGuide=guide;}
            else{EditorUtility.CopySerialized(guide,storedGuide);UnityEngine.Object.DestroyImmediate(guide);}
            canvas.guide.sharedMaterial.mainTexture=storedGuide;
            game.portraitGate=GalleryPortraitGate.Install(game);
            game.orcusExit=GalleryConceptExit.Install(game);
            GalleryCathedralRumble.Install(game,game.orcusExit.cathedralActivation);
            GalleryConceptLighting.Configure(game);
        }
        static void SaveMuseumMeshes(GalleryGame game, string prefix="MuseumMesh_")
        {
            // Scene references to temporary procedural meshes would disappear in
            // a player build. Persist every generated mesh before saving the scene.
            int index=0;
            AssetDatabase.StartAssetEditing();
            try
            {
            foreach(var group in game.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh)).GroupBy(f=>f.sharedMesh).ToArray())
            {
                Mesh mesh=group.Key;string name=prefix+(index++).ToString("D3")+"_"+mesh.name;
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
        // Upgrade only the moving Nike exhibits. Keep the saved architecture,
        // lightmap UVs, lighting data and authored probes intact.
        public static void ApplyNikeDiscoveryUpgrade()
        {
            Scene scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var game=UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            if(game==null||game.shadowCourse==null||game.nikeLight==null)
                throw new InvalidOperationException("The saved museum's Nike course is missing.");
            var lightingData=Lightmapping.lightingDataAsset;
            int lightmapCount=LightmapSettings.lightmaps.Length;
            if(lightingData==null||lightmapCount==0)
                throw new InvalidOperationException("Nike upgrade requires the saved baked museum lighting.");
            GalleryWorld.Materials.Clear();
            var course=game.shadowCourse;
            course.nikeCover=GalleryNikeLightCover.Install(game,course.transform);
            course.nikeCover.Prop.minScale=.45f;
            course.nikeCover.Prop.maxScale=3.4f;
            EditorUtility.SetDirty(course.nikeCover.Prop);
            game.nikeLight.cover=course.nikeCover;
            var props=game.props.ToList();
            if(!props.Contains(course.nikeCover.Prop))props.Add(course.nikeCover.Prop);
            game.props=props.ToArray();
            var nike=course.statue;
            nike.transform.localPosition=new Vector3(1.5f,0,21.65f);
            nike.Remember();
            if(nike.body!=null){nike.body.position=nike.transform.position;nike.body.rotation=nike.transform.rotation;}
            foreach(var label in course.GetComponentsInChildren<GalleryLabel>(true))
                if(label.caption=="니케의 그림자를 벽의 윤곽에 맞춰 보세요")
                    label.caption="빈 캔버스와 주변 전시물을 살펴보세요";
            foreach(var renderer in course.nikeCover.GetComponentsInChildren<MeshRenderer>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)&~StaticEditorFlags.ContributeGI);
                renderer.receiveGI=ReceiveGI.LightProbes;
                renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
            }
            // A separate mesh prefix avoids overwriting the existing baked
            // architecture's generated mesh assets during this partial upgrade.
            SaveMuseumMeshes(game,"NikeDiscoveryMesh_");
            foreach(var material in GalleryWorld.Materials.ToArray())
            {
                string path=Generated+"NikeDiscovery_"+material.name.Replace('/','_')+".mat";
                var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved==null){AssetDatabase.CreateAsset(material,path);saved=material;}
                else EditorUtility.CopySerialized(material,saved);
                foreach(var renderer in course.nikeCover.GetComponentsInChildren<Renderer>(true))
                {
                    var slots=renderer.sharedMaterials;bool changed=false;
                    for(int i=0;i<slots.Length;i++)if(slots[i]==material){slots[i]=saved;changed=true;}
                    if(changed)renderer.sharedMaterials=slots;
                }
            }
            var canvas=game.nikeLight.canvas;
            string guidePath=Generated+"Nike shadow alignment guide.asset";
            var guide=canvas.BakeGuide();
            var stored=AssetDatabase.LoadAssetAtPath<Texture2D>(guidePath);
            if(stored==null){AssetDatabase.CreateAsset(guide,guidePath);stored=guide;}
            else{EditorUtility.CopySerialized(guide,stored);UnityEngine.Object.DestroyImmediate(guide);}
            canvas.guide.sharedMaterial.mainTexture=stored;
            EditorUtility.SetDirty(canvas.guide.sharedMaterial);
            game.lighting.Rebuild();game.nikeLight.SyncPose();
            if(Lightmapping.lightingDataAsset!=lightingData||LightmapSettings.lightmaps.Length!=lightmapCount)
                throw new InvalidOperationException("Nike upgrade unexpectedly changed saved museum lighting.");
            if(game.props.Length!=7)throw new InvalidOperationException("Nike discovery layout requires seven movable exhibits.");
            EditorUtility.SetDirty(game);EditorUtility.SetDirty(course);EditorUtility.SetDirty(game.nikeLight);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("NIKE_DISCOVERY_UPGRADE_OK props="+game.props.Length+" retainedLightmaps="+lightmapCount+
                " lamp="+game.nikeLight.fixedLamp.position+" nike="+nike.transform.position);
        }
        [MenuItem("Parallax/미술관 씬 준비")]
        public static void Prepare()
        {
            GalleryCathedralImport.Ensure();
            GalleryPortalBeachAssets.Prepare();
            Directory.CreateDirectory(Generated);Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            GalleryScanCollision.Ensure();
            GalleryGame game=GalleryConceptWorld.Build();game.bottleBalanceEnabled=true;Integrate(game);
            SaveMuseumMeshes(game);
            foreach(var m in GalleryWorld.Materials)
            {
                string path=Generated+m.name.Replace('/','_')+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(existing==null)AssetDatabase.CreateAsset(m,path);
                else
                {
                    EditorUtility.CopySerialized(m,existing);
                    foreach(var r in game.GetComponentsInChildren<Renderer>(true))
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
            if(game.props.Length!=7||game.GetComponentInChildren<GalleryConceptPerspective>()==null||!game.conceptLayout||game.orcusExit==null||game.portraitGate==null||game.shadowCourse==null||game.nikeLight==null)throw new Exception("Playable museum exhibit missing");
            foreach(string shader in new[]{"Parallax/MuseumSurface","Parallax/GalleryConceptPostFX","Parallax/MuseumFoliage","Parallax/MuseumLibraryPortal","Hidden/Parallax/MuseumNikeDepth","Parallax/GalleryNikeBeam","Parallax/GalleryNikeFusion","Parallax/MuseumNikeBrush","Parallax/PortraitCurtainFace"})
            {
                var asset=Shader.Find(shader);
                if(asset!=null&&ShaderUtil.ShaderHasError(asset))
                    foreach(var message in ShaderUtil.GetShaderMessages(asset))Debug.LogError(shader+" line "+message.line+": "+message.message);
                if(asset==null||ShaderUtil.ShaderHasError(asset))throw new Exception("Concept shader invalid: "+shader);
            }
            // The freshly generated museum, rather than the older saved scene,
            // supplies the exact surfaces used by runtime scenery colliders.
            GalleryBottleBalanceReadableArchitecture.Prepare(game);
            GalleryStaticExhibitCollisionAssets.Prepare(game);
            EditorSceneManager.SaveScene(scene,ScenePath);
            PlayerSettings.productName="Parallax - Museum Bottle and Balance v"+GalleryGame.Version;PlayerSettings.bundleVersion=GalleryGame.Version;
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),
                new EditorBuildSettingsScene(GalleryPortalBeachAssets.SourceScenePath,true),
                new EditorBuildSettingsScene(LibraryBuild.ScenePath,false)};
            AssetDatabase.SaveAssets();Debug.Log("GALLERY_PREPARE_OK casters="+game.lighting.Count);
        }
        [MenuItem("Parallax/미술관 Windows 빌드")]
        public static void Build()
        {
            // This museum update ships the already-authored Beach scene.
            // Regenerating another stage here would overwrite its saved work.
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(GalleryPortalBeachAssets.SourceScenePath)==null)
                throw new FileNotFoundException("The existing destination Beach scene is missing.",GalleryPortalBeachAssets.SourceScenePath);
            Prepare();
            GalleryBakedLightingBuild.Bake();
            foreach(string shader in new[]{"Parallax/GallerySurface","Parallax/MuseumSurface","Parallax/GalleryMirror","Parallax/GalleryGlass","Parallax/GalleryOrcusPortal"})
                if(ShaderUtil.ShaderHasError(Shader.Find(shader)))
                {
                    foreach(var message in ShaderUtil.GetShaderMessages(Shader.Find(shader)))
                        Debug.LogError(shader+" line "+message.line+": "+message.message);
                    throw new Exception(shader+" has compilation errors; build stopped.");
                }
            string output=Parallax.Editor.BuildPaths.Output("Parallax_Unity_v"+GalleryGame.Version+"_MuseumGallery/Parallax.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath,GalleryPortalBeachAssets.SourceScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
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
