using UnityEditor;

namespace ShadeLink.Editor
{
    // The v4 mask stores smoothness in alpha; it must be sampled as linear data.
    public static class ShadowKeyV4Import
    {
        const string Root = "Assets/Parallax/Tutorial/Resources/Tutorial/ShadowKeyV4/";
        [MenuItem("Parallax/Prepare key v4 model")]
        public static void Prepare()
        {
            var model = AssetImporter.GetAtPath(Root + "ShadowKey_v4.obj") as ModelImporter;
            if (model != null && !model.isReadable) { model.isReadable = true; model.SaveAndReimport(); }
            var mask = AssetImporter.GetAtPath(Root + "ShadowKey_MetallicSmoothness.png") as TextureImporter;
            if (mask != null && (mask.sRGBTexture || mask.alphaSource != TextureImporterAlphaSource.FromInput || mask.alphaIsTransparency))
            {
                mask.sRGBTexture = false;
                mask.alphaSource = TextureImporterAlphaSource.FromInput;
                mask.alphaIsTransparency = false;
                mask.SaveAndReimport();
            }
        }
    }
}
