using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // The approved review models, now ordinary shared-interaction props.
    public static class GalleryShadowExhibits
    {
        public static GalleryProp CreateBronze(Transform parent,Vector3 at)
        {
            const float height=2.4f,width=1.7f,baseHeight=.04f,thickness=.0425f;
            var root=Group(parent,"01 · 접힌 청동 · 그림자를 만드는 조각",at);
            root.localScale=Vector3.one*.8f;
            var prop=root.gameObject.AddComponent<GalleryProp>();
            prop.title=root.name;prop.zone=0;prop.minScale=.35f;prop.platform=false;
            Material warm=Material("warm patinated bronze plate","Bronze",new Color(.56f,.36f,.15f));
            warm.SetFloat("_Metallic",.88f);warm.SetFloat("_Roughness",.43f);
            Material umber=Material("deep umber bronze fold","Bronze",new Color(.37f,.26f,.13f));
            umber.SetFloat("_Metallic",.86f);umber.SetFloat("_Roughness",.48f);
            Material patina=Material("quiet olive bronze fold","Bronze",new Color(.37f,.34f,.20f));
            patina.SetFloat("_Metallic",.86f);patina.SetFloat("_Roughness",.45f);
            Material edge=Material("polished bronze cut edges","Bronze",new Color(.72f,.48f,.20f));
            edge.SetFloat("_Metallic",.94f);edge.SetFloat("_Roughness",.26f);
            Material baseMetal=Material("dark bronze sculpture foot","Bronze",new Color(.21f,.18f,.13f));
            baseMetal.SetFloat("_Roughness",.55f);
            var corners=new[]{new Vector3(-width*.5f,0,.01f),new Vector3(-width*.15f,0,width*.12f),
                new Vector3(width*.17f,0,-width*.045f),new Vector3(width*.5f,0,width*.075f)};
            Material[] faces={warm,umber,patina};float plateHeight=height-baseHeight;
            for(int i=0;i<3;i++)
            {
                Vector3 a=corners[i],b=corners[i+1],delta=b-a;
                var plate=Group(root,"Connected bronze fold "+(i+1),(a+b)*.5f);
                plate.localRotation=Quaternion.Euler(0,-Mathf.Atan2(delta.z,delta.x)*Mathf.Rad2Deg,0);
                Block(plate,"Solid folded bronze plate",Vector3.up*(baseHeight+plateHeight*.5f),
                    new Vector3(delta.magnitude+.006f,plateHeight,thickness),faces[i],true,.006f);
                Block(plate,"Polished bronze top edge",new Vector3(0,height-.006f,0),
                    new Vector3(delta.magnitude+.006f,.012f,thickness+.002f),edge,true,.0025f);
            }
            for(int i=1;i<=2;i++)
                Block(root,"Rounded vertical bronze crease",corners[i]+new Vector3(0,baseHeight+plateHeight*.5f,-thickness*.62f),
                    new Vector3(.018f,plateHeight,.018f),edge,true,.005f);
            float low=corners[2].z-thickness,high=corners[1].z+thickness;
            Block(root,"Low dark bronze sculpture foot",new Vector3(0,baseHeight*.5f,(low+high)*.5f),
                new Vector3(width+.06f,baseHeight,Mathf.Max(.39f,high-low+.10f)),baseMetal,true,.007f);
            prop.Remember();return prop;
        }

        public static GalleryProp CreateLamp(Transform parent,Vector3 at,Vector3 target,out Transform head,out Transform emitter)
        {
            const float height=3.6f;
            var root=Group(parent,"02 · 움직이는 전시 조명",at);
            var prop=root.gameObject.AddComponent<GalleryProp>();
            prop.title=root.name;prop.zone=1;prop.minScale=.40f;prop.platform=false;
            Material bronze=Material("satin bronze lamp fittings","Bronze",new Color(.54f,.36f,.16f));
            bronze.SetFloat("_Roughness",.35f);
            Material wood=Material("walnut lamp base inset","Wood",new Color(.47f,.31f,.18f));
            Material black=Material("charcoal spotlight enamel","Ink",new Color(.045f,.052f,.049f));
            black.SetFloat("_Roughness",.38f);black.SetFloat("_Metallic",.18f);black.SetFloat("_ReflectionStrength",.30f);
            Material lens=Material("warm spotlight lens","Paper",new Color(1,.80f,.51f));
            lens.SetFloat("_Roughness",.22f);lens.SetFloat("_ExhibitLight",.35f);
            lens.SetColor("_EmissionColor",new Color(.38f,.19f,.055f));
            Block(root,"Low stable bronze lamp foot",new Vector3(0,.0375f,0),new Vector3(.80f,.075f,.66f),bronze,true,.018f);
            Block(root,"Inset walnut base pad",new Vector3(0,.089f,0),new Vector3(.64f,.028f,.50f),wood,false,.010f);
            float shaftBottom=.103f,shaftTop=height-.46f;
            Block(root,"Slender bronze upright",Vector3.up*((shaftBottom+shaftTop)*.5f),
                new Vector3(.055f,shaftTop-shaftBottom,.055f),bronze,true,.012f);
            Cylinder(root,"Bronze arm adjustment pivot",new Vector3(0,shaftTop,0),Quaternion.Euler(90,0,0),.063f,.105f,bronze);
            Vector3 headAt=new Vector3(.38f,height-.14f,0),neckAt=headAt+new Vector3(0,-.14f,-.10f);
            Vector3 armStart=Vector3.up*shaftTop,armDelta=neckAt-armStart;
            var arm=Block(root,"Angled articulated bronze arm",(armStart+neckAt)*.5f,
                new Vector3(.052f,armDelta.magnitude,.052f),bronze,true,.010f);
            arm.transform.localRotation=Quaternion.FromToRotation(Vector3.up,armDelta);
            Block(root,"Fixed bronze spotlight support yoke",neckAt,new Vector3(.28f,.060f,.080f),bronze,true,.012f);
            // All rotating head meshes stay inside this one fixed, non-casting
            // collision envelope. Auto-aim never changes the carried compound.
            var envelope=Group(root,"Fixed spotlight head collision envelope",headAt);
            envelope.gameObject.AddComponent<BoxCollider>().size=Vector3.one*.68f;
            head=Group(root,"Spotlight head",headAt);
            Vector3 worldTarget=parent==null?target:parent.TransformPoint(target);
            AimHead(head,worldTarget,root.up,root.forward);
            Quaternion forward=Quaternion.Euler(90,0,0);
            Cylinder(head,"Black cylindrical spotlight housing",new Vector3(0,0,-.11f),forward,.115f,.34f,black);
            Cylinder(head,"Fine bronze spotlight rim",new Vector3(0,0,.061f),forward,.122f,.026f,bronze);
            Cylinder(head,"Warm recessed spotlight lens",new Vector3(0,0,.069f),forward,.101f,.006f,lens);
            emitter=Group(head,"Emitter",new Vector3(0,0,.090f));
            prop.Remember();return prop;
        }

        public static Transform CreateFixedLamp(Transform parent,Vector3 source,Vector3 target,out Transform head,out Transform emitter)
        {
            var root=Group(parent,"Collection · fixed Nike projection lamp",new Vector3(source.x,0,source.z));
            Material bronze=Material("fixed projector · satin bronze","Bronze",new Color(.54f,.36f,.16f));
            Material wood=Material("fixed projector · walnut inset","Wood",new Color(.47f,.31f,.18f));
            Material black=Material("fixed projector · charcoal enamel","Ink",new Color(.045f,.052f,.049f));
            black.SetFloat("_Roughness",.38f);black.SetFloat("_Metallic",.18f);
            Material lens=Material("fixed projector · warm glass lens","Paper",new Color(1,.80f,.51f));
            lens.SetColor("_EmissionColor",new Color(.38f,.19f,.055f));
            Block(root,"Fixed projector bronze foot",new Vector3(0,.035f,0),new Vector3(.75f,.07f,.58f),bronze,true,.015f);
            Block(root,"Fixed projector walnut inset",new Vector3(0,.080f,0),new Vector3(.60f,.020f,.43f),wood,false,.008f);
            float postTop=source.y-.13f;
            Block(root,"Fixed projector bronze upright",new Vector3(0,(.09f+postTop)*.5f,0),new Vector3(.055f,postTop-.09f,.055f),bronze,true,.010f);
            Vector3 forward=(target-source).normalized;
            head=Group(root,"Spotlight head",Vector3.up*source.y-forward*.090f);
            AimHead(head,parent==null?target:parent.TransformPoint(target),root.up,root.forward);
            Block(head,"Fixed projector bronze neck",new Vector3(0,-.13f,-.02f),new Vector3(.20f,.05f,.09f),bronze,true,.010f);
            Quaternion turn=Quaternion.Euler(90,0,0);
            Cylinder(head,"Fixed projector black cylindrical housing",new Vector3(0,0,-.11f),turn,.115f,.34f,black);
            Cylinder(head,"Fixed projector bronze lens rim",new Vector3(0,0,.061f),turn,.122f,.026f,bronze);
            Cylinder(head,"Fixed projector warm lens",new Vector3(0,0,.069f),turn,.101f,.006f,lens);
            emitter=Group(head,"Emitter",new Vector3(0,0,.090f));return root;
        }

        public static GalleryProp CreateCollectionPainting(Transform parent)
        {
            var palette=GalleryConceptArchitecture.MuseumPalette(GalleryWorld.Materials);
            GalleryConceptArchitecture.FramedArtwork(parent,"Scream","절규","에드바르 뭉크 · 1893",
                new Vector3(2.6f,2.7f,28.4f),Quaternion.identity,.90f,1.45f,palette);
            GalleryConceptArchitecture.FramedArtwork(parent,"NikeVisconti1919","사모트라케의 니케","엘리제우 비스콘티 · 1919",
                new Vector3(7.15f,2.7f,28.4f),Quaternion.identity,1.15f,2.1f,palette);
            var root=Group(parent,"Collection · Claude Monet · Water Lily Bridge",new Vector3(4.9f,2.7f,28.4f));
            var prop=root.gameObject.AddComponent<GalleryProp>();
            prop.title="02 · 수련 연못 위의 다리";prop.zone=1;prop.minScale=.45f;prop.mountedAtHome=true;
            var texture=Resources.Load<Texture2D>("MuseumArt/WaterLilyBridge");
            if(texture==null)throw new InvalidOperationException("The displayed Monet reproduction is missing.");
            float width=2.35f,height=width*texture.height/texture.width;
            GalleryConceptArchitecture.FramedArtwork(root,"WaterLilyBridge","수련 연못 위의 다리","클로드 모네 · 1899",
                Vector3.zero,Quaternion.identity,width,height,palette,false);
            var box=root.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,0,-.04f);
            box.size=new Vector3(width+.445f,height+.445f,.18f);
            root.gameObject.AddComponent<GalleryCaster>().shape=box;
            // The catalogue belongs to the wall display, so it stays behind
            // when the same framed work is carried into the light.
            Block(parent,"Monet museum catalogue plaque",new Vector3(4.9f,2.7f-height*.5f-.43f,28.415f),
                new Vector3(1.7f,.29f,.035f),palette.paper,false,.008f);
            var label=Group(parent,"Catalogue · Monet collection work",new Vector3(4.9f,2.7f-height*.5f-.43f,28.37f));
            var caption=label.gameObject.AddComponent<GalleryLabel>();
            caption.caption="수련 연못 위의 다리\n클로드 모네 · 1899";caption.size=.065f;caption.color=new Color(.22f,.19f,.14f);
            prop.Remember();prop.InitializePhysics();
            prop.body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            return prop;
        }

        static void AimHead(Transform head,Vector3 target,Vector3 up,Vector3 fallback)
        {
            Vector3 direction=target-head.position;if(direction.sqrMagnitude<.000001f)direction=fallback;
            if(Mathf.Abs(Vector3.Dot(direction.normalized,up))>.98f)up=fallback;
            head.rotation=Quaternion.LookRotation(direction,up);
        }
        static Transform Group(Transform parent,string name,Vector3 at)
        {
            var root=new GameObject(name).transform;root.SetParent(parent,false);root.localPosition=at;return root;
        }
        static Material Material(string name,string category,Color color)
        {
            var shader=Shader.Find("Parallax/MuseumSurface");
            if(shader==null)throw new InvalidOperationException("Shadow exhibits require Parallax/MuseumSurface.");
            var material=new Material(shader){name="Shadow exhibit · "+name};
            GalleryMuseumMaterials.Apply(material,category);material.color=color;
            GalleryWorld.Materials.Add(material);return material;
        }
        static Renderer Block(Transform parent,string name,Vector3 at,Vector3 size,Material material,bool physical,float bevel)
        {
            var renderer=GalleryConceptArchitecture.Block(parent,name,at,size,material,bevel);
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            if(physical)
            {
                var box=renderer.gameObject.AddComponent<BoxCollider>();box.size=size;
                renderer.gameObject.AddComponent<GalleryCaster>().shape=box;
            }
            return renderer;
        }
        static void Cylinder(Transform parent,string name,Vector3 at,Quaternion rotation,float radius,float length,Material material)
        {
            var body=GameObject.CreatePrimitive(PrimitiveType.Cylinder);body.name=name;
            body.transform.SetParent(parent,false);body.transform.localPosition=at;body.transform.localRotation=rotation;
            body.transform.localScale=new Vector3(radius*2,length*.5f,radius*2);
            var renderer=body.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            var collider=body.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)UnityEngine.Object.Destroy(collider);else UnityEngine.Object.DestroyImmediate(collider);
        }
    }
}
