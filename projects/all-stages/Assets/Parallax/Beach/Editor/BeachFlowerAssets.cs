using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Beach;
namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
static GameObject[] FlowerPrefabs()
        {
            var material=BeachBuild.Save(new Material(Shader.Find("Parallax/BeachFlower")){name="Emilulz flower atlas",mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/LowPolyFlowerDemo/DEMOAtlas_Flowerpack.png"),enableInstancing=true},Generated+"Art_Flowers.mat");
            var names=new[]{"SM_Daisy_Single","SM_Hyacinth_PastellBlue_Big","SM_Dandelion_Small","SM_Rose_Red_Open","SM_Rose_Black_Open"};
            return names.Select(name=>
            {
                var root=new GameObject("Flower · "+name);var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/LowPolyFlowerDemo/"+name+".fbx");
                if(source==null)throw new InvalidOperationException("Missing flower: "+name);
                var model=UnityEngine.Object.Instantiate(source,root.transform);model.transform.localRotation=Quaternion.identity;var renderer=model.GetComponentInChildren<Renderer>();
                model.transform.localScale/=renderer.bounds.size.y;var b=renderer.bounds;
                model.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
                renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Generated+"Art_"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
            }).ToArray();
        }
static void Fill(Transform parent,Vector3 center,Vector2 size,GameObject[] flowers,int seed)
        {
            var bed=new GameObject("Mixed flower bed · "+seed);bed.transform.SetParent(parent,false);bed.transform.position=center;
            int columns=Mathf.CeilToInt(size.x/.22f),rows=Mathf.CeilToInt(size.y/.22f);
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {
                int index=row*columns+col+seed;
                int variant=index%19==0?4:index%7==0?3:index%3;
                var flower=UnityEngine.Object.Instantiate(flowers[variant],bed.transform);
                float jitter=Mathf.Sin(index*13.17f);
                flower.transform.localPosition=new Vector3(((col+.5f)/columns-.5f)*size.x+jitter*.035f,0,((row+.5f)/rows-.5f)*size.y+Mathf.Cos(index*7.3f)*.03f);
                flower.transform.localRotation=Quaternion.Euler(0,index*137.5f%360,0);
                flower.transform.localScale=Vector3.one*(.36f+row/(float)rows*.13f+jitter*.035f);
            }
        }
static partial void ConfigureFlowers(BeachGame game)
        {
            var flowers=FlowerPrefabs();var transforms=game.GetComponentsInChildren<Transform>();
            var beds=transforms.Where(t=>t.name=="Festival flower bed").ToArray();
            var garden=transforms.First(t=>t.name=="02 / planted S-shaped beach garden");
            // Keep authored originals inactive so independent scene changes merge cleanly.
            foreach(var t in transforms.Where(t=>t.name=="Beach flower petals"||t.name=="Beach flower center"||t.name=="Beach garden bloom"||t.name=="Beach garden flower center"||
                t.name=="Dune grass"&&(t.parent==garden||beds.Any(b=>Mathf.Abs(t.position.x-b.position.x)<.5f&&Mathf.Abs(t.position.z-b.position.z)<.9f))).ToArray())
                {t.gameObject.SetActive(false);foreach(var renderer in t.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;}
            for(int i=0;i<beds.Length;i++)
                Fill(beds[i].parent,beds[i].position+Vector3.up*.105f,new Vector2(.70f,1.45f),flowers,31+i*17);
            Fill(garden,new Vector3(-4,1.015f,42.5f),new Vector2(7.35f,1.10f),flowers,11);
            Fill(garden,new Vector3(4,1.115f,46.2f),new Vector2(7.35f,1.10f),flowers,29);
        }
    }
}
