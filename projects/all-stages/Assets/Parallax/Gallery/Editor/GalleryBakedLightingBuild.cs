using System;
using System.Collections.Generic;
using ShadeLink.Gallery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Editor
{
    public static class GalleryBakedLightingBuild
    {
        const string ScenePath="Assets/Parallax/Gallery/Scenes/Gallery.unity";
        const string SettingsPath="Assets/Parallax/Gallery/Generated/Museum bounce lighting.lighting";

        [MenuItem("Parallax/미술관 반사광 굽기")]
        public static void Bake(){Bake(false);}

        public static void Bake(bool preview)
        {
            var game=UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            if(game==null||!game.conceptLayout)throw new InvalidOperationException("Open the museum scene before baking its lighting.");
            var old=game.transform.Find("Museum baked bounce sources");
            if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=new GameObject("Museum baked bounce sources").transform;root.SetParent(game.transform,false);
            var sources=new List<Light>();
            foreach(var light in game.GetComponentsInChildren<Light>())light.lightmapBakeType=LightmapBakeType.Realtime;
            for(int room=0;room<4;room++)
            {
                GalleryConceptLighting.KeyPose(room,out var position,out var target,out float range,out float fov);
                var key=Source(root,"Display bounce · "+room,position,target-position,LightType.Spot,LightmapBakeType.Mixed);
                key.spotAngle=fov;key.range=range;key.intensity=room==1?1.6f:1.3f;
                key.bounceIntensity=1.5f;key.color=new Color(1,.86f,.70f);key.shadowRadius=.12f;sources.Add(key);
            }
            float[] ends={0,14,29,44,56};
            for(int room=0;room<4;room++)foreach(int side in new[]{-1,1})
            {
                Vector3 upwardInward=new Vector3(-side*.60f,.80f,0);
                var cove=Source(root,"Cornice reflected light · "+room+" · "+side,
                    new Vector3(side*7.55f,6.20f,(ends[room]+ends[room+1])/2),upwardInward,
                    LightType.Rectangle,LightmapBakeType.Baked);
                cove.transform.rotation=Quaternion.LookRotation(upwardInward,Vector3.forward);
                cove.areaSize=new Vector2(.16f,ends[room+1]-ends[room]-.8f);
                cove.color=new Color(1,.86f,.72f);cove.intensity=2.4f;sources.Add(cove);
            }
            for(int room=0;room<4;room++)
            {
                var ceiling=Source(root,"Soft ceiling reflection · "+room,
                    new Vector3(0,6.35f,(ends[room]+ends[room+1])/2),Vector3.down,
                    LightType.Rectangle,LightmapBakeType.Baked);
                ceiling.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
                ceiling.areaSize=new Vector2(12,ends[room+1]-ends[room]-2);
                ceiling.color=new Color(1,.92f,.83f);ceiling.intensity=1f;sources.Add(ceiling);
            }
            var driver=game.GetComponent<GalleryBakedLighting>();
            if(driver==null)driver=game.gameObject.AddComponent<GalleryBakedLighting>();
            driver.bakeSources=sources.ToArray();
            var points=new List<Vector3>();
            for(int room=0;room<4;room++)foreach(float x in new[]{-6f,-2f,2f,6f})
                for(int z=0;z<4;z++)foreach(float y in new[]{.6f,2.4f,5.4f})
                    points.Add(new Vector3(x,y,Mathf.Lerp(ends[room]+1.4f,ends[room+1]-1.4f,z/3f)));
            root.gameObject.AddComponent<LightProbeGroup>().probePositions=points.ToArray();

            int receivers=0;var unwrapped=new HashSet<Mesh>();
            foreach(var renderer in game.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.sharedMaterial==null||renderer.sharedMaterial.renderQueue>=2500)continue;
                bool moving=renderer.GetComponentInParent<GalleryProp>()!=null||renderer.GetComponentInParent<GalleryPortraitGate>()!=null;
                if(game.shadowCourse!=null)foreach(var leaf in game.shadowCourse.NikeGateLeaves)
                    if(leaf!=null&&renderer.transform.IsChildOf(leaf))moving=true;
                var flags=GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if(moving)
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,flags&~StaticEditorFlags.ContributeGI);
                    renderer.receiveGI=ReceiveGI.LightProbes;continue;
                }
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,flags|StaticEditorFlags.ContributeGI);
                Mesh mesh=filter.sharedMesh;
                bool generated=AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/Parallax/Gallery/Generated/",StringComparison.Ordinal);
                renderer.receiveGI=generated?ReceiveGI.Lightmaps:ReceiveGI.LightProbes;
                if(!generated)continue;
                if(unwrapped.Add(mesh))
                {
                    if(mesh.uv2.Length!=mesh.vertexCount&&!Unwrapping.GenerateSecondaryUVSet(mesh))
                        throw new InvalidOperationException("Cannot unwrap museum lighting UVs: "+mesh.name);
                    EditorUtility.SetDirty(mesh);
                }
                renderer.scaleInLightmap=1;receivers++;
            }
            var materials=new HashSet<Material>();
            foreach(var renderer in game.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)if(material!=null)materials.Add(material);
            foreach(var material in materials)
            {
                if(material==null)continue;
                material.doubleSidedGI=true;
                if(material.HasProperty("_EmissionColor")&&material.GetColor("_EmissionColor").maxColorComponent>.001f)
                    material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
                EditorUtility.SetDirty(material);
            }

            var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
            if(settings==null){settings=new LightingSettings();AssetDatabase.CreateAsset(settings,SettingsPath);}
            settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapper=LightingSettings.Lightmapper.ProgressiveGPU;
            settings.mixedBakeMode=MixedLightingMode.IndirectOnly;settings.directionalityMode=LightmapsMode.NonDirectional;
            settings.lightmapResolution=preview?8:16;settings.lightmapMaxSize=1024;settings.lightmapPadding=4;
            settings.directSampleCount=64;settings.indirectSampleCount=preview?32:256;settings.environmentSampleCount=64;
            settings.maxBounces=4;settings.minBounces=2;settings.indirectScale=1;
            settings.ao=true;settings.aoMaxDistance=.35f;settings.aoExponentIndirect=.7f;settings.aoExponentDirect=0;
            settings.filteringMode=LightingSettings.FilterMode.Auto;
            Lightmapping.lightingSettings=settings;EditorUtility.SetDirty(settings);
            EditorSceneManager.SaveScene(game.gameObject.scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("MUSEUM_BOUNCE_BAKE_BEGIN receivers="+receivers+" probes="+points.Count+" preview="+preview);
            if(!Lightmapping.Bake()||LightmapSettings.lightmaps.Length==0)
                throw new InvalidOperationException("Museum bounced-light bake produced no lightmaps.");
            EditorSceneManager.SaveScene(game.gameObject.scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("MUSEUM_BOUNCE_BAKE_OK maps="+LightmapSettings.lightmaps.Length+" receivers="+receivers);
        }

        static Light Source(Transform parent,string name,Vector3 at,Vector3 direction,LightType type,LightmapBakeType bake)
        {
            var source=new GameObject(name).AddComponent<Light>();source.transform.SetParent(parent,false);
            source.transform.localPosition=at;source.transform.rotation=Quaternion.LookRotation(direction);
            source.type=type;source.lightmapBakeType=bake;source.shadows=LightShadows.Soft;return source;
        }
    }
}
