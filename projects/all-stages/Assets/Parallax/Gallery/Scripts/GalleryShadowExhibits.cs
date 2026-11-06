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

        public static Transform CreateLamp(Transform parent,Vector3 at,Vector3 target,out Transform head,out Transform emitter)
        {
            var root=Group(parent,"02 · 고정 전시 조명",at);
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
            Cylinder(root,"Floor spotlight swivel pedestal",new Vector3(0,.20f,0),Quaternion.identity,.16f,.22f,bronze);
            foreach(int side in new[]{-1,1})
                Block(root,"Bronze yoke arm",new Vector3(side*.23f,.34f,0),new Vector3(.065f,.27f,.12f),bronze,true,.012f);
            Vector3 headAt=new Vector3(0,.55f,0);
            var envelope=Group(root,"Fixed spotlight head collision envelope",headAt);
            envelope.gameObject.AddComponent<BoxCollider>().size=new Vector3(.43f,.40f,.47f);
            head=Group(root,"Spotlight head",headAt);
            Vector3 worldTarget=parent==null?target:parent.TransformPoint(target);
            AimHead(head,worldTarget,root.up,root.forward);
            Quaternion forward=Quaternion.Euler(90,0,0);
            Cylinder(head,"Black cylindrical spotlight housing",new Vector3(0,0,-.035f),forward,.19f,.42f,black);
            Cylinder(head,"Fine bronze spotlight rim",new Vector3(0,0,.185f),forward,.20f,.045f,bronze);
            Cylinder(head,"Warm recessed spotlight lens",new Vector3(0,0,.210f),forward,.171f,.009f,lens);
            emitter=Group(head,"Emitter",new Vector3(0,0,.225f));
            AddBeam(emitter);
            return root;
        }

        static void AddBeam(Transform emitter)
        {
            const int sides=32,rings=12;
            var vertices=new Vector3[sides*(rings+1)];var normals=new Vector3[vertices.Length];
            var uv=new Vector2[vertices.Length];var triangles=new int[sides*rings*6];
            for(int y=0;y<=rings;y++)for(int x=0;x<sides;x++)
            {
                float depth=y/(float)rings,angle=2*Mathf.PI*x/sides;
                Vector3 radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                int i=y*sides+x;
                vertices[i]=radial*(.12f+depth*2.15f)+Vector3.forward*(depth*7.4f);
                normals[i]=(radial-Vector3.forward*.29f).normalized;
                uv[i]=new Vector2(x/(float)sides,depth);
                if(y==rings)continue;
                int n=y*sides+(x+1)%sides,j=i*6;
                triangles[j]=i;triangles[j+1]=n;triangles[j+2]=n+sides;
                triangles[j+3]=i;triangles[j+4]=n+sides;triangles[j+5]=i+sides;
            }
            var mesh=new Mesh{name="Soft cone of amber light",vertices=vertices,normals=normals,uv=uv,triangles=triangles};
            var beam=Group(emitter,"Amber spotlight beam",Vector3.zero).gameObject;
            beam.AddComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(Shader.Find("Parallax/GalleryNikeBeam")){name="Nike spotlight · amber beam"};
            GalleryWorld.Materials.Add(material);
            var renderer=beam.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
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
