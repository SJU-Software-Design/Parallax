using UnityEngine;

namespace ShadeLink.Beach
{
    /// <summary>A complete sand stair fragment; every visible step is a physical column.</summary>
    public static class BeachCastleStairBuilder
    {
        public const float Width=1.1f;
        public const int Steps=24;
        public static float FootY(float rise)=>-rise*.5f;

        static Material Material(string name,Color color)
        {
            var found=BeachWorld.Materials.Find(m=>m!=null&&m.name==name);
            if(found!=null)return found;
            var material=new Material(Shader.Find("Parallax/BeachSurface")){name=name,color=color};
            material.SetFloat("_Metallic",0);material.SetFloat("_Glossiness",.12f);
            BeachWorld.Materials.Add(material);return material;
        }

        // Local +Z ascends. The source pose and supporting display plinth are
        // assigned by the route builder, then captured with BeachProp.Remember.
        public static BeachProp Build(Transform root,BeachGame game,string title,float rise,float run,float initialScale)
        {
            if(rise<=0||run<=0||initialScale<=0)
                throw new System.ArgumentOutOfRangeException(nameof(rise),"Stair dimensions and initial scale must be positive.");
            var go=new GameObject(title);go.transform.SetParent(root,false);
            go.transform.localScale=Vector3.one*initialScale;
            var prop=go.AddComponent<BeachProp>();
            prop.title=title;prop.zone=3;prop.minScale=.45f;prop.maxScale=2.15f;
            prop.platform=true;prop.upright=true;prop.buoyant=false;
            var sand=Material("Castle restoration golden sand",new Color(.83f,.69f,.40f));
            var top=Material("Castle restoration sunlit sand",new Color(.93f,.80f,.50f));

            float foot=FootY(rise),depth=run/Steps;
            for(int i=0;i<Steps;i++)
            {
                float height=rise*(i+1)/Steps;
                float z=-run*.5f+depth*(i+.5f);
                // Each real BoxCollider reaches the same flat foot plane.
                // No hidden ramp, isolated tread or decorative support is used.
                BeachWorld.Box("Sand castle stair · solid step "+(i+1),new Vector3(0,foot+height*.5f,z),
                    new Vector3(Width,height,depth),sand,go.transform);
                float finish=Mathf.Min(.008f,height*.25f);
                BeachWorld.Box("Sand castle stair · light tread "+(i+1),new Vector3(0,foot+height-finish*.5f,z),
                    new Vector3(Width,finish,depth),top,go.transform,false,false);
            }

            // A single matching shell sits on the broad high part of the +X
            // cut face. The stair silhouette itself remains the main fitting cue.
            BeachCastleWallBuilder.CreateShellCrest(go.transform,new Vector3(Width*.5f+.006f,-rise*.04f,run*.22f),
                Quaternion.Euler(0,90,0),.8f);
            prop.Remember();return prop;
        }
    }
}

