using UnityEditor;

namespace ShadeLink.Eclipse.Editor
{
    // Apply before first FBX import too, including a clean generated project.
    sealed class EclipseSelectedModelImportRules : AssetPostprocessor
    {
        public override int GetPostprocessOrder()=>-1000;
        public override uint GetVersion()=>3;
        void OnPreprocessTexture()
        {
            if(EclipseSelectedAssetPreparation.IsSelectedTexture(assetPath)&&assetImporter is TextureImporter importer)
                EclipseSelectedAssetPreparation.ConfigureSourceTexture(importer,assetPath);
        }
        void OnPreprocessModel()
        {
            if(EclipseSelectedAssetPreparation.IsSelectedSource(assetPath)&&assetImporter is ModelImporter importer)
                EclipseSelectedAssetPreparation.ConfigureSourceModel(importer);
        }
    }
}