using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Museum shadow tools and the Nike room's physical route. The game retains
    // exposure, checkpoints and the shared object interaction controller.
    public sealed class GalleryMuseumShadowCourse : MonoBehaviour
    {
        public const float TutorialFinish = 12.2f, NikeFinish = 28.2f;
        public GalleryGame game;
        public GalleryProp bronze,lamp;
        public GalleryNikeLight nikeLight;
        public GalleryProp[] panels=Array.Empty<GalleryProp>();
        public Transform[] nikeGateLeaves;
        public BoxCollider[] nikeGateColliders;
        public bool TutorialCleared { get; private set; }
        public bool NikeCleared { get; private set; }
        public bool NikeUnlocked { get; private set; }
        public bool NikeGateOpen => gateOpening>=.995f;
        public float NikeGateOpening => gateOpening;
        public Vector3 NikeGatePosition => transform.TransformPoint(new Vector3(0,0,28.72f));
        public BoxCollider[] NikeGateColliders => nikeGateColliders;
        public Transform[] NikeGateLeaves => nikeGateLeaves;

        Vector3 previousFeet;
        bool havePreviousFeet;
        bool tutorialCrossed,nikeCrossed;
        float gateOpening;

        public static GalleryMuseumShadowCourse Install(GalleryGame owner)
        {
            if(owner==null)throw new ArgumentNullException(nameof(owner));
            var existing=owner.GetComponentInChildren<GalleryMuseumShadowCourse>(true);
            if(existing!=null)return existing;
            var collection=new GameObject("Museum shadow course · bronze and movable light");
            collection.transform.SetParent(owner.transform,false);
            var course=collection.AddComponent<GalleryMuseumShadowCourse>();course.game=owner;
            Material bronze=Material("Shadow course · satin bronze fittings","Bronze",new Color(.51f,.34f,.15f));
            bronze.SetFloat("_Metallic",.90f);bronze.SetFloat("_Roughness",.34f);
            course.bronze=GalleryShadowExhibits.CreateBronze(collection.transform,new Vector3(-3.5f,0,5));
            course.lamp=GalleryShadowExhibits.CreateLamp(collection.transform,new Vector3(1.5f,0,20.2f),
                new Vector3(0,.025f,NikeFinish),out Transform head,out Transform emitter);
            course.nikeLight=owner.gameObject.AddComponent<GalleryNikeLight>();
            course.nikeLight.game=owner;course.nikeLight.lamp=course.lamp;course.nikeLight.head=head;course.nikeLight.emitter=emitter;
            course.nikeLight.nikeRoot=owner.transform.Find("Collection · Winged Victory · Cosmo Wenman scan");
            if(course.nikeLight.nikeRoot==null)throw new InvalidOperationException("Install the fixed Nike scan before its shadow course.");
            course.nikeLight.AimPoint=new Vector3(0,.025f,NikeFinish);course.nikeLight.autoAim=true;
            owner.nikeLight=course.nikeLight;
            var props=new List<GalleryProp>(owner.props??Array.Empty<GalleryProp>());
            props.Add(course.bronze);props.Add(course.lamp);owner.props=props.ToArray();
            course.PowerZone(bronze);course.GlassRoute(bronze);course.GoalFrame(bronze);
            foreach(float z in new[]{7.25f,TutorialFinish,22.1f,NikeFinish})course.Inlay(z,bronze);
            course.FloorLabel("청동 조각을 키워 빛을 가려 보세요",new Vector3(0,.015f,6.60f),.12f);
            course.FloorLabel("조명을 옮겨 날개의 그림자를 문까지 이어 보세요",new Vector3(0,.015f,21.4f),.105f);
            course.ResetProgress();
            Physics.SyncTransforms();
            foreach(var prop in new[]{course.bronze,course.lamp})
            {
                prop.InitializePhysics();
                if(owner.controller!=null)prop.placement.SetPlayer(owner.controller);
                prop.placement.ResetPlaced();
            }
            course.nikeLight.SyncPose();
            if(owner.lighting!=null)owner.lighting.Rebuild();
            Debug.Log("MUSEUM_SHADOW_COURSE_OK bronze=1 lamp=1 panels=0 totalProps="+owner.props.Length);
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

        void PowerZone(Material bronze)
        {
            const float left=-5.3f,right=4.8f,front=16f,back=22.2f;
            foreach(float x in new[]{left,right})
                FloorInlay("Lamp floor power area · side",new Vector3(x,.010f,(front+back)*.5f),new Vector3(.020f,.008f,back-front),bronze);
            foreach(float z in new[]{front,back})
                FloorInlay("Lamp floor power area · end",new Vector3((left+right)*.5f,.010f,z),new Vector3(right-left,.008f,.020f),bronze);
            FloorLabel("조명을 놓을 수 있는 전시 바닥",new Vector3(-.25f,.018f,16.5f),.10f);
        }

        void FloorInlay(string name,Vector3 at,Vector3 size,Material bronze)
        {Block(transform,name,at,size,bronze).shadowCastingMode=ShadowCastingMode.Off;}

        void GoalFrame(Material bronze)
        {
            // Flush visual border of the same rectangle sampled by NikeLight.
            // No collider, caster or painted shade alters its actual lighting.
            const float left=-.65f,right=.65f,front=27.65f,back=28.4f;
            foreach(float x in new[]{left,right})
                FloorInlay("Nike goal · bronze side border",new Vector3(x,.009f,(front+back)*.5f),new Vector3(.018f,.006f,back-front),bronze);
            foreach(float z in new[]{front,back})
                FloorInlay("Nike goal · bronze end border",new Vector3(0,.009f,z),new Vector3(right-left+.018f,.006f,.018f),bronze);
        }

        Renderer Glass(Transform parent,string name,Vector3 at,Vector3 size,Material glass)
        {
            var renderer=GalleryConceptArchitecture.Block(parent,name,at,size,glass,.004f);
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var box=renderer.gameObject.AddComponent<BoxCollider>();box.size=size;
            renderer.gameObject.AddComponent<GalleryLightGlass>();return renderer;
        }

        void Hardware(Transform parent,string name,Vector3 at,Vector3 size,Material bronze)
        {
            var renderer=Block(parent,name,at,size,bronze);
            // Real metal remains an opaque physical blocker for the lamp's
            // light rays; only the transparent panes carry GalleryLightGlass.
            renderer.gameObject.AddComponent<BoxCollider>().size=size;
        }

        void Guide(Vector3 a,Vector3 b,Material glass,Material bronze)
        {
            Vector3 delta=b-a,mid=(a+b)*.5f;
            var panel=Group(transform,"Light-transmitting protective glass guide",mid,
                Quaternion.Euler(0,-Mathf.Atan2(delta.z,delta.x)*Mathf.Rad2Deg,0));
            const float height=1.65f;
            Glass(panel,"Clear protective glass",new Vector3(0,height*.5f+.04f,0),new Vector3(delta.magnitude,height,.025f),glass);
            Hardware(panel,"Low bronze glass channel",new Vector3(0,.025f,0),new Vector3(delta.magnitude,.050f,.065f),bronze);
            foreach(int side in new[]{-1,1})
                Hardware(panel,"Fine bronze glass edge post",new Vector3(side*(delta.magnitude*.5f-.016f),height*.5f+.04f,0),
                    new Vector3(.025f,height,.038f),bronze);
        }

        void GlassRoute(Material bronze)
        {
            var shader=Shader.Find("Parallax/GalleryGlass");
            if(shader==null)throw new InvalidOperationException("Nike guides require Parallax/GalleryGlass.");
            var glass=new Material(shader){name="Nike course · clear light-transmitting protective glass",color=new Color(.56f,.77f,.77f,.065f)};
            GalleryWorld.Materials.Add(glass);
            foreach(int side in new[]{-1,1})
            {
                float x=side<0?-4.5f:2.5f;
                Guide(new Vector3(x,0,23.2f),new Vector3(x,0,25),glass,bronze);
                Guide(new Vector3(x,0,25),new Vector3(side*.95f,0,28.1f),glass,bronze);
                Guide(new Vector3(side*.95f,0,28.1f),new Vector3(side*.95f,0,28.65f),glass,bronze);
                // Rear glass closes the sidewall detour; the only route through
                // this screen is the visible, sliding door in the real arch.
                Guide(new Vector3(side*1.81f,0,28.72f),new Vector3(side*7.87f,0,28.72f),glass,bronze);
            }
            nikeGateLeaves=new Transform[2];nikeGateColliders=new BoxCollider[2];
            for(int i=0;i<2;i++)
            {
                int side=i==0?-1:1;
                var leaf=Group(transform,"Nike glass sliding door · "+(side<0?"left":"right"),new Vector3(side*.90f,0,28.72f),Quaternion.identity);
                nikeGateLeaves[i]=leaf;
                var panel=Glass(leaf,"Light-transmitting sliding glass",Vector3.up*1.57f,new Vector3(1.78f,3.10f,.035f),glass);
                nikeGateColliders[i]=panel.GetComponent<BoxCollider>();
                foreach(int edge in new[]{-1,1})
                {
                    Hardware(leaf,"Door bronze vertical stile",new Vector3(edge*.875f,1.57f,0),new Vector3(.028f,3.12f,.050f),bronze);
                    Hardware(leaf,"Door bronze horizontal stile",new Vector3(0,1.57f+edge*1.55f,0),new Vector3(1.78f,.028f,.050f),bronze);
                }
            }
            Hardware(transform,"Bronze sliding door overhead track",new Vector3(0,3.19f,28.72f),new Vector3(7.4f,.070f,.10f),bronze);
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
            if(zone<0||zone==1)
            {
                NikeCleared=false;nikeCrossed=false;NikeUnlocked=false;
                nikeLight?.ResetLight();
                // Normal R resets the visitor to the room entrance first. If
                // called elsewhere, keep the panes away until the visitor exits.
                if(CanCloseGate())gateOpening=0;
                ApplyGatePose();
            }
            havePreviousFeet=game!=null&&game.player!=null;
            if(havePreviousFeet)previousFeet=game.player.feet;
        }

        bool CanCloseGate()
        {
            if(game==null||game.player==null)return true;
            Vector3 feet=transform.InverseTransformPoint(game.player.feet);
            return !(Mathf.Abs(feet.x)<2.40f&&Mathf.Abs(feet.z-28.72f)<.65f&&feet.y<3.2f&&feet.y>-1.7f);
        }

        void ApplyGatePose()
        {
            if(nikeGateLeaves==null)return;
            float slide=1.95f*Mathf.SmoothStep(0,1,gateOpening);
            for(int i=0;i<nikeGateLeaves.Length;i++)if(nikeGateLeaves[i]!=null)
                nikeGateLeaves[i].localPosition=new Vector3((i==0?-1:1)*(.90f+slide),0,28.72f);
            Physics.SyncTransforms();
        }

        void TickGate(float dt)
        {
            if(!NikeUnlocked&&nikeLight!=null&&nikeLight.SolutionReady)
            {
                NikeUnlocked=true;
                game.Tell("날개의 그림자가 문에 닿았습니다. 열린 유리문으로 이동하세요.",5);
            }
            float next=gateOpening;
            if(NikeUnlocked)next=Mathf.MoveTowards(next,1,Mathf.Max(0,dt)/.8f);
            else if(CanCloseGate())next=Mathf.MoveTowards(next,0,Mathf.Max(0,dt)/.8f);
            if(!Mathf.Approximately(next,gateOpening)){gateOpening=next;ApplyGatePose();}
        }

        public void Tick(float dt)
        {
            if(game==null||game.player==null)return;
            TickGate(dt);
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
                if(previousFeet.z<NikeFinish&&feet.z>=NikeFinish&&NikeUnlocked&&game.safe&&game.player.grounded&&Mathf.Abs(feet.x)<.95f)
                    nikeCrossed=true;
            }
            if(game.active&&!game.won&&game.safe&&game.player.grounded)
            {
                if(game.checkpoint==0&&!TutorialCleared&&tutorialCrossed&&feet.z>=TutorialFinish)
                {TutorialCleared=true;game.Tell("청동 조각의 그림자 통로를 건넜습니다. 다음 전시실로 이동하세요.",5);}
                if(game.checkpoint==1&&!NikeCleared&&NikeUnlocked&&nikeCrossed&&feet.z>=NikeFinish)
                {NikeCleared=true;game.Tell("니케의 그림자를 따라 건넜습니다. 다음 전시실로 이동하세요.",5);}
            }
            previousFeet=feet;
        }
    }
}
