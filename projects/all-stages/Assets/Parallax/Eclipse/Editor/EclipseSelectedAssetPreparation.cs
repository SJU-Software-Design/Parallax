using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShadeLink.Eclipse.Editor
{
    // Imports only the selected, unmodified public source bytes. No scene is opened or saved.
    public static class EclipseSelectedAssetPreparation
    {
        const string Selected="Assets/Parallax/Eclipse/Resources/Eclipse/Selected";
        public const string ShipResourcePath=Selected+"/ArrivalPirateShip.prefab";
        public static void Prepare()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string modelRoot=Selected+"/Quaternius/Models";
            foreach(string path in AssetDatabase.FindAssets("t:Model",new[]{modelRoot,Selected+"/ExitSign"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)continue;
                bool changed=ConfigureSourceModel(importer);
                if(changed)importer.SaveAndReimport();
            }
            foreach(string path in AssetDatabase.FindAssets("t:Texture",new[]{Selected}).Select(AssetDatabase.GUIDToAssetPath).Where(IsSelectedTexture))
            {
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer!=null&&ConfigureSourceTexture(importer,path))importer.SaveAndReimport();
            }
            PrepareArrivalShip();
            AssetDatabase.SaveAssets();
            Debug.Log("ECLIPSE_SELECTED_ASSETS_PREPARED standard190-source=CC0 sun=SolarSystemScope-CCBY4 ship=existing-Comeback");
        }
        public static bool IsSelectedSource(string path)
        {
            return path.StartsWith(Selected+"/Quaternius/Models/",StringComparison.Ordinal)||
                path.StartsWith(Selected+"/ExitSign/",StringComparison.Ordinal);
        }
        public static bool ConfigureSourceModel(ModelImporter importer)
        {
            bool changed=!importer.isReadable||importer.addCollider||
                importer.materialImportMode!=ModelImporterMaterialImportMode.ImportStandard||
                importer.materialLocation!=ModelImporterMaterialLocation.InPrefab||
                importer.materialSearch!=ModelImporterMaterialSearch.Local;
            importer.isReadable=true;importer.addCollider=false;
            // Retain original named material slots as subassets, with no global
            // material/texture search. Runtime assigns exact original2D PBR maps.
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
            importer.materialSearch=ModelImporterMaterialSearch.Local;
            return changed;
        }
        public static bool IsSelectedTexture(string path)
        {
            return path.StartsWith(Selected+"/",StringComparison.Ordinal)&&
                (path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase));
        }
        public static bool ConfigureSourceTexture(TextureImporter importer,string path)
        {
            bool exitImage=path.EndsWith("/ExitSign/Background.png",StringComparison.Ordinal);
            bool normal=path.EndsWith("_Normal.png",StringComparison.OrdinalIgnoreCase);
            bool linear=normal||path.Contains("_ORM")||path.Contains("DetailMask");
            var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            bool changed=importer.textureShape!=TextureImporterShape.Texture2D||importer.textureType!=type||
                importer.sRGBTexture==linear||!importer.mipmapEnabled||importer.anisoLevel!=8||importer.maxTextureSize!=2048||
                (exitImage&&(importer.npotScale!=TextureImporterNPOTScale.None||importer.wrapMode!=TextureWrapMode.Clamp));
            importer.textureShape=TextureImporterShape.Texture2D;importer.textureType=type;
            importer.sRGBTexture=!linear;importer.mipmapEnabled=true;importer.anisoLevel=8;
            importer.maxTextureSize=2048;importer.wrapMode=exitImage?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
            if(exitImage)importer.npotScale=TextureImporterNPOTScale.None;
            return changed;
        }
        static void PrepareArrivalShip()
        {
            const string sourcePath="Assets/ThirdParty/StylizedPirateShip/StylShip_Unity.prefab";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if(source==null)throw new FileNotFoundException("The already acquired Beach pirate ship prefab is missing",sourcePath);
            var copy=UnityEngine.Object.Instantiate(source);copy.name="Arrival pirate ship - existing Beach source";
            try
            {
                copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);copy.transform.localScale=Vector3.one;
                foreach(var component in copy.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(component);
                foreach(var component in copy.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(component);
                foreach(var component in copy.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(component);
                // Reuse the exact UV-only sail derivatives already prepared for Beach.
                foreach(string node in new[]{"StylShip_SailFront","StylShip_SailMid1"})
                {
                    var f=copy.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m=>m.name==node);
                    var derivative=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Parallax/Beach/Generated/Pirate sail logo · "+node+".asset");
                    if(f!=null&&derivative!=null)f.sharedMesh=derivative;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(ShipResourcePath));
                PrefabUtility.SaveAsPrefabAsset(copy,ShipResourcePath);
            }
            finally{UnityEngine.Object.DestroyImmediate(copy);}
        }
    }
}