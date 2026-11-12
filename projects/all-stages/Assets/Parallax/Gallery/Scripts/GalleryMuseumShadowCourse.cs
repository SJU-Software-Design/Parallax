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
        public GalleryProp bronze,statue;
        public GalleryNikeLight nikeLight;
        public GalleryNikeLightCover nikeCover;
        public GalleryNikeDoorSocket nikeSocket;
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
        bool gateOpeningAudioPlayed;

        public static GalleryMuseumShadowCourse Install(GalleryGame owner,GalleryProp statue)
        {
            if(owner==null)throw new ArgumentNullException(nameof(owner));
            var existing=owner.GetComponentInChildren<GalleryMuseumShadowCourse>(true);
            if(existing!=null)return existing;
            var collection=new GameObject("Museum shadow course · bronze and Nike");
            collection.transform.SetParent(owner.transform,false);
            var course=collection.AddComponent<GalleryMuseumShadowCourse>();course.game=owner;
            Material bronze=Material("Shadow course · satin bronze fittings","Bronze",new Color(.51f,.34f,.15f));
            bronze.SetFloat("_Metallic",.90f);bronze.SetFloat("_Roughness",.34f);
            course.bronze=GalleryShadowExhibits.CreateBronze(collection.transform,new Vector3(-3.5f,0,5));
            course.statue=statue;
            var lamp=GalleryShadowExhibits.CreateLamp(collection.transform,new Vector3(-6.16f,0,18.2f),
                GalleryNikeCanvas.Center,out Transform head,out Transform emitter);
            course.nikeLight=owner.gameObject.AddComponent<GalleryNikeLight>();
            course.nikeLight.game=owner;course.nikeLight.fixedLamp=lamp;course.nikeLight.statue=statue;
            course.nikeLight.head=head;course.nikeLight.emitter=emitter;course.nikeLight.nikeRoot=statue.transform;
            course.nikeLight.autoAim=false;course.nikeLight.fieldOfView=70;course.nikeLight.range=32;course.nikeLight.intensity=17;
            course.nikeCover=GalleryNikeLightCover.Install(owner,collection.transform);
            course.nikeLight.cover=course.nikeCover;
            owner.nikeLight=course.nikeLight;
            var props=new List<GalleryProp>(owner.props??Array.Empty<GalleryProp>());
            props.Add(course.bronze);props.Add(statue);props.Add(course.nikeCover.Prop);owner.props=props.ToArray();
            var walnut=Material("Nike door and frame · antique walnut","Wood",new Color(.46f,.33f,.23f));
            var trim=Material("Nike door panels · walnut inset","Wood",new Color(.34f,.24f,.17f));
            var gold=Material("Nike frames · warm gilding","Bronze",new Color(.77f,.53f,.24f));
            var linen=Material("Nike frame · canvas linen","Paper",new Color(.67f,.55f,.40f));
            var mint=Material("Nike route · mint inlay","Paper",new Color(.40f,.75f,.55f));
            mint.SetColor("_EmissionColor",new Color(.09f,.20f,.12f));
            course.nikeLight.canvas=GalleryNikeCanvas.Install(collection.transform,statue,emitter,gold,walnut,linen,mint);
            course.WoodenGate(walnut,trim);
            var lining=Material("Nike door · recessed charcoal lining","Ink",new Color(.13f,.091f,.061f));
            course.nikeSocket=GalleryNikeDoorSocket.Install(collection.transform,course.nikeGateLeaves,course.nikeLight.canvas,gold,walnut,lining);
            foreach(float z in new[]{7.25f,TutorialFinish})course.Inlay(z,bronze);
            course.FloorLabel("청동 조각을 키워 빛을 가려 보세요",new Vector3(0,.015f,6.60f),.12f);
            course.FloorLabel("빈 캔버스와 주변 전시물을 살펴보세요",new Vector3(-2.7f,.015f,20.9f),.105f);
            course.ResetProgress();
            Physics.SyncTransforms();
            foreach(var prop in new[]{course.bronze,statue,course.nikeCover.Prop})
            {
                prop.InitializePhysics();
                if(owner.controller!=null)prop.placement.SetPlayer(owner.controller);
                prop.placement.ResetPlaced();
            }
            course.nikeLight.SyncPose();
            if(owner.lighting!=null)owner.lighting.Rebuild();
            Debug.Log("MUSEUM_SHADOW_COURSE_OK bronze=1 movableNike=1 fixedLamp=1 totalProps="+owner.props.Length);
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

        void FloorInlay(string name,Vector3 at,Vector3 size,Material bronze)
        {Block(transform,name,at,size,bronze).shadowCastingMode=ShadowCastingMode.Off;}

        void WoodenGate(Material walnut,Material trim)
        {
            nikeGateLeaves=new Transform[2];nikeGateColliders=new BoxCollider[2];
            for(int i=0;i<2;i++)
            {
                int side=i==0?-1:1;
                var leaf=Group(transform,"Nike arched walnut door · "+(side<0?"left":"right"),
                    new Vector3(side*1.79f,0,28.72f),Quaternion.identity);
                nikeGateLeaves[i]=leaf;
                var door=Block(leaf,"Walnut door leaf",new Vector3(-side*.886f,1.95f,0),
                    new Vector3(1.77f,3.86f,.13f),walnut);
                nikeGateColliders[i]=door.gameObject.AddComponent<BoxCollider>();
                nikeGateColliders[i].size=new Vector3(1.77f,3.86f,.13f);
                Block(leaf,"Raised outer door stile",new Vector3(-side*.23f,1.92f,-.081f),new Vector3(.24f,3.53f,.061f),trim);
                foreach(float y in new[]{.24f,3.62f})
                    Block(leaf,"Raised walnut door rail",new Vector3(-side*.887f,y,-.081f),new Vector3(1.51f,.21f,.061f),trim);
                var crown=Group(leaf,"Curved walnut door crown",Vector3.zero,Quaternion.identity).gameObject;
                Mesh mesh=DoorCrown(side);
                crown.AddComponent<MeshFilter>().sharedMesh=mesh;
                crown.AddComponent<MeshRenderer>().sharedMaterial=walnut;
                crown.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
        }

        static Mesh DoorCrown(int side)
        {
            const float radius=1.79f, spring=3.90f;
            const int steps=24;
            var vertices=new List<Vector3>();
            var uv=new List<Vector2>();
            var triangles=new List<int>();
            for(int i=0;i<=steps;i++)
            {
                float distance=Mathf.Lerp(.018f,1.775f,i/(float)steps);
                float x=-side*distance;
                float y=spring+Mathf.Sqrt(radius*radius-Mathf.Pow(radius-distance,2));
                foreach(float z in new[]{-.065f,.065f})
                foreach(float height in new[]{spring-.015f,y})
                {vertices.Add(new Vector3(x,height,z));uv.Add(new Vector2(x,height));}
                if(i==0)continue;
                int a=(i-1)*4,b=i*4;
                foreach(var quad in new[]{new[]{a,b,b+1,a+1},new[]{a+2,a+3,b+3,b+2},
                    new[]{a+1,b+1,b+3,a+3},new[]{a,a+2,b+2,b}})
                    triangles.AddRange(new[]{quad[0],quad[1],quad[2],quad[0],quad[2],quad[3]});
            }
            int end=steps*4;
            triangles.AddRange(new[]{0,1,3,0,3,2,end,end+2,end+3,end,end+3,end+1});
            if(side>0)
                for(int i=0;i<triangles.Count;i+=3)(triangles[i+1],triangles[i+2])=(triangles[i+2],triangles[i+1]);
            var mesh=new Mesh{name="Nike quarter-arch door crown"};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
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
                MuseumAudio.StopOwner(this);gateOpeningAudioPlayed=false;
                NikeCleared=false;nikeCrossed=false;NikeUnlocked=false;
                nikeSocket?.ResetSocket();
                nikeLight?.canvas?.ResetPuzzle(game);
                nikeCover?.ResetDiscovery();
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
            if(nikeLight!=null&&nikeLight.canvas!=null)
            {
                float swing=90*Mathf.SmoothStep(0,1,gateOpening);
                for(int i=0;i<nikeGateLeaves.Length;i++)if(nikeGateLeaves[i]!=null)
                    nikeGateLeaves[i].localRotation=Quaternion.Euler(0,(i==0?-1:1)*swing,0);
                Physics.SyncTransforms();
                return;
            }
            float slide=1.95f*Mathf.SmoothStep(0,1,gateOpening);
            for(int i=0;i<nikeGateLeaves.Length;i++)if(nikeGateLeaves[i]!=null)
                nikeGateLeaves[i].localPosition=new Vector3((i==0?-1:1)*(.90f+slide),0,28.72f);
            Physics.SyncTransforms();
        }

        void TickGate(float dt)
        {
            var canvas=nikeLight!=null?nikeLight.canvas:null;
            if(canvas!=null&&canvas.portablePainting!=null)
            {
                if(!canvas.Fusing&&!canvas.Complete&&nikeLight.SolutionReady)canvas.BeginFusion();
                canvas.TickFusion(dt,game);
                if(nikeSocket!=null)
                {
                    nikeSocket.Tick(dt,game);
                    if(!NikeUnlocked&&nikeSocket.OpenReady)
                    {
                        NikeUnlocked=true;
                        game.Tell("액자가 맞물렸어요. 열린 문으로 들어가세요.",5);
                    }
                }
                else
                {
                    bool shaded=nikeLight.PaintingCoversExit();
                    if(canvas.shadeMarks.activeSelf!=shaded)canvas.shadeMarks.SetActive(shaded);
                    if(!NikeUnlocked&&shaded)
                    {
                        NikeUnlocked=true;
                        game.Tell("그림이 출구의 빛을 가렸어요. 그늘 위의 발자국을 따라가세요.",5);
                    }
                }
            }
            else if(!NikeUnlocked&&nikeLight!=null&&nikeLight.SolutionReady)
            {
                NikeUnlocked=true;
                MuseumAudio.TryPlay("nike.shadow_fit",this,NikeGatePosition,.58f);
                canvas?.SetComplete(true);
                nikeLight.ResetLight();
                game.Tell(nikeLight.canvas!=null?"그림이 완성됐어요. 발자국을 따라 그늘로 걸어가세요.":
                    "날개의 그림자가 문에 닿았습니다. 열린 문으로 이동하세요.",5);
            }
            float next=gateOpening;
            if(NikeUnlocked)next=Mathf.MoveTowards(next,1,Mathf.Max(0,dt)/.8f);
            else if(CanCloseGate())next=Mathf.MoveTowards(next,0,Mathf.Max(0,dt)/.8f);
            if(!Mathf.Approximately(next,gateOpening))
            {
                if(next>gateOpening&&!gateOpeningAudioPlayed)
                {
                    gateOpeningAudioPlayed=true;
                    MuseumAudio.TryPlay("nike.door_open",this,NikeGatePosition+Vector3.up*1.8f,.58f);
                }
                gateOpening=next;ApplyGatePose();
            }
        }

        void OnDisable(){MuseumAudio.StopOwner(this);gateOpeningAudioPlayed=false;}

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
            bool portableShade=nikeLight?.canvas?.portablePainting!=null;
            float crossingZ=portableShade?NikeGatePosition.z:NikeFinish;
            float crossingHalfWidth=portableShade?1.55f:.95f;
            if(feet.z<TutorialFinish)tutorialCrossed=false;
            if(feet.z<crossingZ)nikeCrossed=false;
            if(game.active&&!game.won&&forward&&continuous)
            {
                if(previousFeet.z<TutorialFinish&&feet.z>=TutorialFinish)tutorialCrossed=true;
                if(previousFeet.z<crossingZ&&feet.z>=crossingZ&&NikeUnlocked&&game.safe&&game.player.grounded&&
                    Mathf.Abs(feet.x)<crossingHalfWidth&&(nikeSocket!=null||!portableShade||nikeLight.canvas.shadeMarks.activeSelf))
                    nikeCrossed=true;
            }
            if(game.active&&!game.won&&game.safe&&game.player.grounded)
            {
                if(game.checkpoint==0&&!TutorialCleared&&tutorialCrossed&&feet.z>=TutorialFinish)
                {TutorialCleared=true;game.Tell("청동 조각의 그림자 통로를 건넜습니다. 다음 전시실로 이동하세요.",5);}
                if(game.checkpoint==1&&!NikeCleared&&NikeUnlocked&&nikeCrossed&&feet.z>=crossingZ)
                {NikeCleared=true;game.Tell("열린 문을 통과했어요. 다음 전시실로 이동하세요.",5);}
            }
            previousFeet=feet;
        }
    }
}
