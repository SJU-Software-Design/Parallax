using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Keep the selected scan's original topology, normals and UV texture.
    public sealed class GalleryAureliusImport : AssetPostprocessor
    {
        const string Root = "Assets/Parallax/Gallery/Resources/MuseumScans/Aurelius/";

        void OnPreprocessModel()
        {
            if (assetPath != Root + "AureliusBust.obj") return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;
            importer.addCollider = false;
            importer.globalScale = 1;
        }

        void OnPreprocessTexture()
        {
            if (assetPath != Root + "AureliusAlbedo.jpg") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.anisoLevel = 4;
            importer.alphaSource = TextureImporterAlphaSource.None;
        }
    }
}
