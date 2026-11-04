using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Adds usable museum screens and observes real crossings. The game owns
    // exposure, checkpoints, doors and the shared object interaction controller.
    public sealed class GalleryMuseumShadowCourse : MonoBehaviour
    {
        public const float TutorialFinish = 12.2f, NikeFinish = 28.2f;
        public GalleryGame game;
        public GalleryProp[] panels;
        public bool TutorialCleared { get; private set; }
        public bool NikeCleared { get; private set; }

        Vector3 previousFeet;
        bool havePreviousFeet;
        bool tutorialCrossed,nikeCrossed;

        public static GalleryMuseumShadowCourse Install(GalleryGame owner)
        {
            if(owner==null)throw new ArgumentNullException(nameof(owner));
            var existing=owner.GetComponentInChildren<GalleryMuseumShadowCourse>(true);
            if(existing!=null)return existing;
            var collection=new GameObject("Museum shadow course · folding exhibition screens");
            collection.transform.SetParent(owner.transform,false);
            var course=collection.AddComponent<GalleryMuseumShadowCourse>();course.game=owner;

            Material wood=Material("Shadow course · oak screen backing","Wood",new Color(.73f,.60f,.43f));
            Material bronze=Material("Shadow course · satin bronze fittings","Bronze",new Color(.51f,.34f,.15f));
            bronze.SetFloat("_Metallic",.90f);bronze.SetFloat("_Roughness",.34f);
            Material fabric=Material("Shadow course · charcoal exhibition linen","Ink",new Color(.075f,.105f,.10f));
            fabric.SetFloat("_Roughness",.94f);fabric.SetFloat("_ReflectionStrength",.07f);

            course.panels=new[]{
                course.Panel("02 · 이동식 전시 패널 A",new Vector3(-4.5f,0,18),12,wood,bronze,fabric),
                course.Panel("02 · 이동식 전시 패널 B",new Vector3(4.5f,0,18),-12,wood,bronze,fabric)
            };
            var props=new List<GalleryProp>(owner.props??Array.Empty<GalleryProp>());
            props.AddRange(course.panels);owner.props=props.ToArray();

            foreach(float z in new[]{7.25f,TutorialFinish,22.1f,NikeFinish})course.Inlay(z,bronze);
            course.FloorLabel("복제작을 크게 세워 그늘을 이어 보세요",new Vector3(0,.015f,6.60f),.12f);
            course.FloorLabel("날개의 그늘과 전시 패널의 그림자를 연결하세요",new Vector3(0,.015f,21.4f),.115f);
            course.ResetProgress();
            Physics.SyncTransforms();
            foreach(var prop in course.panels)
            {
                prop.InitializePhysics();
                if(owner.controller!=null)prop.placement.SetPlayer(owner.controller);
                prop.placement.ResetPlaced();
            }
            if(owner.lighting!=null)owner.lighting.Rebuild();
            Debug.Log("MUSEUM_SHADOW_COURSE_OK panels=2 additionalCasters=6 totalProps="+owner.props.Length);
            return course;
        }

        static Material Material(string name,string category,Color color)
        {
            var shader=Shader.Find("Parallax/MuseumSurface");
            if(shader==null)throw new InvalidOperationException("Museum shadow course requires Parallax/MuseumSurface.");
            var material=new Material(shader){name=name};
            GalleryMuseumMaterials.Apply(material,category);material.color=color;
            GalleryWorld.Materials.Add(material);return material;
        }

        static Transform Group(Transform parent,string name,Vector3 position,Quaternion rotation)
        {
            var child=new GameObject(name).transform;child.SetParent(parent,false);
            child.localPosition=position;child.localRotation=rotation;return child;
        }

        static Renderer Block(Transform parent,string name,Vector3 at,Vector3 size,Material material,bool physical=false)
        {
            var renderer=GalleryConceptArchitecture.Block(parent,name,at,size,material,.008f);
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            if(physical)
            {
                var box=renderer.gameObject.AddComponent<BoxCollider>();box.size=size;
                renderer.gameObject.AddComponent<GalleryCaster>().shape=box;
            }
            return renderer;
        }

        GalleryProp Panel(string title,Vector3 at,float yaw,Material wood,Material bronze,Material fabric)
        {
            var root=Group(transform,title,at,Quaternion.Euler(0,yaw,0));
            root.localScale=Vector3.one*.7f;
            var prop=root.gameObject.AddComponent<GalleryProp>();
            prop.title=title;prop.zone=1;prop.minScale=.35f;prop.platform=false;
            // A shallow V keeps the two leaves visibly connected. Each physical
            // leaf is also its actual analytic shadow box; there is no broad
            // invisible blocker spanning the folded space behind the screen.
            for(int side=-1;side<=1;side+=2)
            {
                var hinge=Group(root,side<0?"Left hinged exhibition leaf":"Right hinged exhibition leaf",
                    new Vector3(0,1.26f,0),Quaternion.Euler(0,-side*8,0));
                float x=side*.45f;
                Block(hinge,"Oak plywood screen leaf",new Vector3(x,0,0),new Vector3(.9f,2.28f,.12f),wood,true);
                foreach(int face in new[]{-1,1})
                {
                    Block(hinge,"Inset charcoal linen exhibition face",new Vector3(x,0,face*.064f),
                        new Vector3(.77f,2.12f,.008f),fabric);
                    foreach(int edge in new[]{-1,1})
                        Block(hinge,"Satin bronze horizontal edge",new Vector3(x,edge*1.111f,face*.067f),
                            new Vector3(.84f,.021f,.014f),bronze);
                }
            }
            Block(root,"Continuous bronze folding hinge",new Vector3(0,1.26f,-.005f),new Vector3(.036f,2.28f,.037f),bronze);
            // Even at solution scale 2.3 this 16 cm foot remains below the
            // shared character controller's 20 cm step height.
            Block(root,"Low oak exhibition screen foot",new Vector3(0,.035f,.10f),new Vector3(1.90f,.07f,.68f),wood,true);
            foreach(int side in new[]{-1,1})
                Block(root,"Bronze foot end cap",new Vector3(side*.90f,.071f,.10f),new Vector3(.06f,.01f,.58f),bronze);
            prop.Remember();return prop;
        }

        void Inlay(float z,Material bronze)
        {
            // Flush decorative inlay only: it never obstructs a player or prop.
            for(int i=0;i<8;i++)
            {
                var r=Block(transform,"Bronze floor inlay",new Vector3(-5.25f+i*1.5f,.007f,z),
                    new Vector3(1.16f,.008f,.018f),bronze);
                r.shadowCastingMode=ShadowCastingMode.Off;
            }
        }

        void FloorLabel(string words,Vector3 at,float size)
        {
            var root=Group(transform,"Inlaid museum instruction · "+words,at,Quaternion.Euler(90,0,0));
            var label=root.gameObject.AddComponent<GalleryLabel>();label.caption=words;label.size=size;
            label.color=new Color(.34f,.25f,.13f);
        }

        public void ResetProgress(int zone=-1)
        {
            if(zone<0||zone==0){TutorialCleared=false;tutorialCrossed=false;}
            if(zone<0||zone==1){NikeCleared=false;nikeCrossed=false;}
            havePreviousFeet=game!=null&&game.player!=null;
            if(havePreviousFeet)previousFeet=game.player.feet;
        }

        public void Tick(float dt)
        {
            if(game==null||game.player==null)return;
            Vector3 feet=game.player.feet;
            if(!havePreviousFeet){previousFeet=feet;havePreviousFeet=true;return;}
            bool forward=feet.z>previousFeet.z;
            // A checkpoint warp is not walking a course. Ordinary motor motion
            // stays below this generous bound even at the largest gameplay dt.
            bool continuous=Vector3.Distance(feet,previousFeet)<=GalleryGame.Speed*Mathf.Clamp(dt,0,.05f)+.5f;
            if(feet.z<TutorialFinish)tutorialCrossed=false;
            if(feet.z<NikeFinish)nikeCrossed=false;
            if(game.active&&!game.won&&forward&&continuous)
            {
                if(previousFeet.z<TutorialFinish&&feet.z>=TutorialFinish)tutorialCrossed=true;
                if(previousFeet.z<NikeFinish&&feet.z>=NikeFinish)nikeCrossed=true;
            }
            if(game.active&&!game.won&&game.safe&&game.player.grounded)
            {
                if(game.checkpoint==0&&!TutorialCleared&&tutorialCrossed&&feet.z>=TutorialFinish)
                {TutorialCleared=true;game.Tell("복제작의 그림자 통로를 건넜습니다. 다음 전시실로 이동하세요.",5);}
                if(game.checkpoint==1&&!NikeCleared&&nikeCrossed&&feet.z>=NikeFinish)
                {NikeCleared=true;game.Tell("니케 전시실의 그림자를 연결했습니다. 다음 전시실로 이동하세요.",5);}
            }
            previousFeet=feet;
        }
    }
}
