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
        const string Source="Assets/ThirdParty/ALLrounder18/";
        const string Generated="Assets/Parallax/Gallery/Generated/";
        const string ScenePath="Assets/Parallax/Gallery/Scenes/Gallery.unity";
        static Material original;
        static Mesh Piece(int index,Vector3 size,Quaternion turn,bool fixture=false)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source+(fixture?"Mesh_":"Mesh_001_")+index+".obj");
            if(source==null)throw new Exception("Missing ALLrounder18 extracted mesh "+index);
            var f=source.GetComponentInChildren<MeshFilter>();
            Mesh m=UnityEngine.Object.Instantiate(f.sharedMesh);m.name="ALLrounder18 part "+index;
            Vector3[] vertices=m.vertices; var b=m.bounds;
            for(int i=0;i<vertices.Length;i++)vertices[i]=turn*(vertices[i]-b.center);
            m.vertices=vertices;m.RecalculateBounds();b=m.bounds;
            for(int i=0;i<vertices.Length;i++)vertices[i]=Vector3.Scale(vertices[i],new Vector3(size.x/b.size.x,size.y/b.size.y,size.z/b.size.z));
            m.vertices=vertices;m.RecalculateBounds();m.RecalculateNormals();
            string path=Generated+(fixture?"fixture_":"part_")+index+"_"+size.x.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.y.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){EditorUtility.CopySerialized(m,existing);UnityEngine.Object.DestroyImmediate(m);return existing;}
            AssetDatabase.CreateAsset(m,path);return m;
        }
        static GameObject Art(string name,int index,Vector3 at,Vector3 size,Quaternion rotation,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localRotation=rotation;
            go.AddComponent<MeshFilter>().sharedMesh=Piece(index,size,Quaternion.identity);
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=original;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
            return go;
        }
        static void Integrate(GalleryGame game)
        {
            original=new Material(Shader.Find("Parallax/GallerySurface")){name="ALLrounder18 original atlas",color=Color.white};
            original.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"Art room01_Art_Room1_BaseColor.png");
            if(original.mainTexture==null)throw new Exception("Missing original atlas");GalleryWorld.Materials.Add(original);
            int[] ids={8,14,17,20};int painting=0;
            foreach(var p in game.props)
            {
                if(p.title.Contains("그림"))
                {
                    foreach(var r in p.renderers)if(!r.name.Contains("seal"))r.enabled=false;
                    Art("ALLrounder18 · original framed painting",ids[painting++],Vector3.zero,new Vector3(2.4f,2.8f,.2f),Quaternion.identity,p.transform);
                    p.Remember();
                }
                else if(p.platform)
                {
                    var r=p.transform.Find("Solid exhibit pedestal").GetComponent<Renderer>();r.enabled=false;
                    Art("ALLrounder18 · original exhibit plinth",22,Vector3.zero,p.parts[0].size*p.parts[0].transform.localScale.x,Quaternion.identity,p.transform);
                    // The prototype dimensions are intentionally nonuniform.
                    var model=p.transform.Find("ALLrounder18 · original exhibit plinth");
                    model.localScale=new Vector3(1,p.parts[0].transform.localScale.y/p.parts[0].transform.localScale.x,p.parts[0].transform.localScale.z/p.parts[0].transform.localScale.x);
                    p.Remember();
                }
            }
            // Original art in the side galleries; the central route remains readable.
            foreach(var r in game.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Permanent gallery frame"||r.name=="Permanent print").ToArray())UnityEngine.Object.DestroyImmediate(r.gameObject);
            for(int z=0;z<4;z++)for(int j=0;j<4;j++)
            {
                float along=new[]{4.5f,17.2f,29.8f,42.1f}[z]+(j-1.5f)*1.85f;
                Art("ALLrounder18 · wall collection",new[]{5,11,12,13,15,17,19,20}[(z*4+j)%8],new Vector3(13.94f,2.55f,along),new Vector3(1.05f,1.45f,.11f),Quaternion.Euler(0,-90,0),game.transform);
            }
            for(int z=0;z<3;z++)
            {
                var panel=Art("ALLrounder18 · exhibition partition",4,new Vector3(-9,2.168f,new[]{4.5f,17,29.2f}[z]),new Vector3(4,4.336f,.4f),Quaternion.Euler(0,28,0),game.transform);
                var box=panel.AddComponent<BoxCollider>();box.size=new Vector3(4,4.336f,.4f);panel.AddComponent<GalleryCaster>().shape=box;
                Art("ALLrounder18 · partition painting",8,new Vector3(-9,2.2f,new[]{4.5f,17,29.2f}[z]+.34f),new Vector3(2.2f,2.8f,.13f),Quaternion.Euler(0,28,0),game.transform);
            }
            for(int z=0;z<4;z++)for(int side=-1;side<=1;side+=2)
            {
                var fixture=new GameObject("ALLrounder18 · ceiling fixture");fixture.transform.SetParent(game.transform,false);
                fixture.transform.localPosition=new Vector3(side*9.7f,9.86f,new[]{.8f,12f,24f,35f}[z]);
                fixture.AddComponent<MeshFilter>().sharedMesh=Piece(0,new Vector3(.9f,.14f,.9f),Quaternion.identity,true);
                fixture.AddComponent<MeshRenderer>().sharedMaterial=original;
            }
        }
        [MenuItem("Parallax/미술관 씬 준비")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Generated);Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            foreach(string path in Directory.GetFiles(Source,"*.obj"))
            {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer!=null&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            }
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            GalleryGame game=GalleryWorld.Build();Integrate(game);
            foreach(var m in GalleryWorld.Materials)
            {
                string path=Generated+m.name.Replace('/','_')+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(existing==null)AssetDatabase.CreateAsset(m,path);
                else
                {
                    EditorUtility.CopySerialized(m,existing);
                    foreach(var r in game.GetComponentsInChildren<Renderer>())if(r.sharedMaterial==m)r.sharedMaterial=existing;
                    UnityEngine.Object.DestroyImmediate(m);
                }
            }
            game.lighting.Rebuild();
            if(game.props.Length!=9)throw new Exception("Gallery props missing");
            EditorSceneManager.SaveScene(scene,ScenePath);
            PlayerSettings.productName="Parallax - Gallery v0.8.2";PlayerSettings.bundleVersion="0.8.2";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();Debug.Log("GALLERY_PREPARE_OK casters="+game.lighting.Count);
        }
        [MenuItem("Parallax/미술관 Windows 빌드")]
        public static void Build()
        {
            Prepare();
            if(ShaderUtil.ShaderHasError(Shader.Find("Parallax/GallerySurface")))throw new Exception("Gallery shader has compilation errors; build stopped.");
            string output=Parallax.Editor.BuildPaths.Output("Parallax_Unity_v0.8.2_Gallery/Parallax.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            Directory.CreateDirectory("Validation/Gallery");
            File.WriteAllText("Validation/Gallery/build-result.txt","Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nOutput: "+output+"\nUnity: "+Application.unityVersion);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Gallery build failed");
            Debug.Log("GALLERY_BUILD_OK "+output);
        }
    }
}
