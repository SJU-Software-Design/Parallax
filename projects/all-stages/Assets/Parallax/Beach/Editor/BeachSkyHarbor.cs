using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        internal static void ConfigureSkyHarbor(BeachGame game)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/StylizedPirateShip/StylShip_Unity.prefab");
            if(prefab==null)throw new InvalidOperationException("The sky ship prefab is missing.");
            var harbor=new GameObject("05 · 모래성 뒤 하늘 항구");harbor.transform.SetParent(game.transform,false);
            var timber=AssetDatabase.LoadAssetAtPath<Material>(Generated+"Weathered timber.mat");
            var canvas=AssetDatabase.LoadAssetAtPath<Material>(Generated+"Warm canvas.mat");
            var sand=AssetDatabase.LoadAssetAtPath<Material>(Generated+"Sand.mat");
            var ink=AssetDatabase.LoadAssetAtPath<Material>(Generated+"Deep navy.mat");
            if(timber==null||canvas==null||sand==null||ink==null)throw new InvalidOperationException("Sky harbor materials are missing.");
            GameObject Box(string name,Vector3 at,Vector3 size,Material material,bool solid=true,bool shadow=true)
                =>BeachWorld.Box(name,at,size,material,harbor.transform,solid,shadow);
            var end=game.transform.Find("끝 둑");if(end!=null)UnityEngine.Object.DestroyImmediate(end.gameObject);
            game.transform.Find("수평선").localScale=new Vector3(6000,.1f,6000);
            Box("항구로 이어지는 모래 해안",new Vector3(0,-.45f,111.5f),new Vector3(28,.9f,8),sand);
            for(float z=87;z<114;z+=3)
                Box("오른쪽 우회로 안내",new Vector3(9.3f,.015f,z),new Vector3(.9f,.02f,.16f),canvas,false,false);
            for(float x=1;x<10;x+=2)
                Box("뒤편 항구 안내",new Vector3(x,.015f,113.3f),new Vector3(.16f,.02f,.9f),canvas,false,false);
            var ship=UnityEngine.Object.Instantiate(prefab,harbor.transform);
            ship.name="하늘행 해적선";ship.transform.SetPositionAndRotation(new Vector3(0,2.5f,132),Quaternion.identity);ship.transform.localScale=Vector3.one*.4f;
            // Open the stern's visible rail where the gangway meets the real upper deck.
            var rearRail=ship.GetComponentsInChildren<Transform>().First(t=>t.name=="StylShip_FencingBack");
            UnityEngine.Object.DestroyImmediate(rearRail.gameObject);
            var deck=ship.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="StylShip_FloorBackTop");
            if(deck.GetComponent<Collider>()==null)deck.gameObject.AddComponent<MeshCollider>().sharedMesh=deck.sharedMesh;
            var flight=ship.AddComponent<BeachSkyShip>();flight.Game=game;game.skyShip=flight;
            Vector3 start=new Vector3(.4f,.04f,115),finish=new Vector3(.4f,3.06f,123);
            var slope=Quaternion.FromToRotation(Vector3.forward,(finish-start).normalized);
            var ramp=Box("승선용 목재 경사로",(start+finish)*.5f-slope*Vector3.up*.07f,new Vector3(2.4f,.14f,(finish-start).magnitude),timber);
            ramp.transform.rotation=slope;
            Box("갑판으로 잇는 승선교",new Vector3(.4f,2.99f,124.5f),new Vector3(2.4f,.14f,3),timber);
            foreach(float x in new[]{-1.02f,1.82f})
            {
                for(int i=0;i<5;i++)
                {
                    float z=115+i*2.5f,y=Mathf.Lerp(.04f,3.06f,Mathf.Clamp01((z-115)/8));
                    Box("승선교 난간 기둥",new Vector3(x,y+.55f,z),new Vector3(.09f,1.1f,.09f),timber);
                }
                var a=new Vector3(x,1.02f,115);var b=new Vector3(x,4.04f,123);
                var rail=Box("승선교 손잡이",(a+b)*.5f,new Vector3(.07f,.07f,(b-a).magnitude),ink,false,false);
                rail.transform.rotation=slope;
                Box("승선교 끝 손잡이",new Vector3(x,4.04f,124.5f),new Vector3(.07f,.07f,3),ink,false,false);
            }
            foreach(float z in new[]{118f,122f,125f})foreach(float x in new[]{-.7f,1.5f})
            {
                float top=Mathf.Lerp(.04f,3.06f,Mathf.Clamp01((z-115)/8));
                Box("승선교 말뚝",new Vector3(x,top*.5f-.15f,z),new Vector3(.15f,top+.3f,.15f),timber);
            }
            foreach(float side in new[]{-1f,1f})
                Box("항구 해안 옆턱",new Vector3(side*14,.2f,111.5f),new Vector3(.15f,.4f,8),timber);
            var sign=Box("하늘 항구 안내판",new Vector3(3.3f,1.3f,113.4f),new Vector3(2.5f,1.25f,.12f),ink);
            Box("안내판 기둥",new Vector3(3.3f,.45f,113.4f),new Vector3(.12f,.9f,.12f),timber);
            var label=new GameObject("하늘 항구 안내");label.transform.SetParent(harbor.transform,false);label.transform.position=sign.transform.position+Vector3.back*.066f;
            var text=label.AddComponent<BeachLabel>();text.caption="하늘행 선착장\n갑판에 올라 E · 출항";text.size=.22f;text.color=new Color(.97f,.93f,.82f);text.style=FontStyle.Bold;
        }
    }
}
