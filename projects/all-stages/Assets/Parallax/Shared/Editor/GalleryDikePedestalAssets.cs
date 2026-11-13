using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Editor
{
    public static class GalleryDikePedestalAssets
    {
        const string Source="Assets/Parallax/Gallery/ThirdParty/DikePedestalP02";
        const string Output="Assets/Parallax/Gallery/Resources/BottleBalance/Dike/P02";
        public static void Prepare()
        {
            Directory.CreateDirectory(Output);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string meshPath=Output+"/VersaillesBodyWithSocket.asset";
            Mesh mesh;
            using(var reader=new BinaryReader(File.OpenRead(Source+"/P02BodySocket.meshdata")))
            {
                if(Encoding.ASCII.GetString(reader.ReadBytes(5))!="P02M1")throw new InvalidDataException("P02 mesh header changed");
                int count=reader.ReadInt32();if(count<1000||count>200000)throw new InvalidDataException("Unexpected P02 vertex count");
                var positions=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];
                for(int i=0;i<count;i++)
                {
                    positions[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    normals[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                }
                int indices=reader.ReadInt32();var triangles=new int[indices];
                for(int i=0;i<indices;i++)triangles[i]=reader.ReadInt32();
                mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,meshPath);}else mesh.Clear();
                mesh.name="Treleau Versailles P02 · original body with custom blade opening";
                mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;
                mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);
            }
            var albedo=Texture("P02_Albedo.png",true,false);
            var normal=Texture("P02_Normal.png",false,true);
            var specular=Texture("P02_SpecularSmoothness.png",false,false);
            string materialPath=Output+"/VersaillesPinkMarble.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader=Shader.Find("Standard (Specular setup)");if(shader==null)throw new InvalidOperationException("Standard specular shader unavailable");
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}else material.shader=shader;
            material.name="P02 · original pink Versailles marble, 4K PBR";material.color=Color.white;
            material.SetTexture("_MainTex",albedo);material.SetTexture("_BumpMap",normal);
            material.SetTexture("_SpecGlossMap",specular);material.SetFloat("_GlossMapScale",1);
            material.SetFloat("_BumpScale",.8f);material.SetFloat("_SmoothnessTextureChannel",0);
            material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_SPECGLOSSMAP");
            // The museum's exhibition spots are analytic shader lights. A low
            // albedo fill keeps the original imported PBR scan readable there.
            material.SetTexture("_EmissionMap",albedo);material.SetColor("_EmissionColor",new Color(.12f,.12f,.12f));
            material.EnableKeyword("_EMISSION");EditorUtility.SetDirty(material);
            var root=new GameObject("P02 Versailles marble pedestal · Treleau CC BY 4.0");
            root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=material;
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Parallax/Gallery/Resources/BottleBalance/Dike/VersaillesPedestalP02.prefab");
            UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            Debug.Log("DIKE_P02_PREPARED triangles="+mesh.triangles.Length/3+" height="+mesh.bounds.size.y+" originalBody=1 groundExcluded=1 realSlot=1");
        }
        static Texture2D Texture(string name,bool srgb,bool normal)
        {
            string path=Source+"/Textures/"+name;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureShape=TextureImporterShape.Texture2D;
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=srgb;importer.maxTextureSize=4096;importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;
            importer.isReadable=false;importer.anisoLevel=4;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture==null)throw new InvalidDataException("P02 map was not imported as a 2D texture: "+path);
            return texture;
        }
    }
}
