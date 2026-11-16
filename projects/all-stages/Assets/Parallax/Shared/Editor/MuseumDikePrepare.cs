using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class MuseumDikePrepare
    {
        public static void PrepareAndValidate()
        {
            GalleryDikePropAssets.Prepare();
            GalleryDikeStatueAssets.Prepare();
            GalleryDikeConceptSwordAssets.Prepare();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("DIKE_IMPORTED_ORIGINAL_MODELS_PREPARED");
            MuseumDikeSwordValidation.Run();
        }
    }
}
