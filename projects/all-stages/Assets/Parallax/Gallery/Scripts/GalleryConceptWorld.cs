using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // A new four-gallery museum plan. It deliberately has no dependency on the
    // old corridor world or its dressing/raised-platform course.
    public static class GalleryConceptWorld
    {
        public const float Width=16,Length=56,Roof=6.6f;
        public const float PortraitSkylightHalfWidth=2.2f;
        static Transform root;
        static GalleryConceptArchitecture.Palette palette;
        static void Box(string name,Vector3 at,Vector3 size,Material material,Transform parent=null,bool caster=true,bool solid=true)
        {
            var r=GalleryConceptArchitecture.Block(parent==null?root:parent,name,at,size,material,.012f);
            if(!solid)return;
            var collider=r.gameObject.AddComponent<BoxCollider>();collider.size=size;
            if(caster)r.gameObject.AddComponent<GalleryCaster>().shape=collider;
        }
        static void Label(string caption,Vector3 at,float size,Color color,Quaternion rotation)
        {
            var go=new GameObject("Exhibition label · "+caption);go.transform.SetParent(root,false);go.transform.localPosition=at;go.transform.localRotation=rotation;
            var label=go.AddComponent<GalleryLabel>();label.caption=caption;label.size=size;label.color=color;
        }
        static Mesh HeaderMesh(float radius,float spring,float top,float thickness)
        {
            const int steps=48;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<=steps;i++)
            {
                float angle=Mathf.PI*i/steps,x=-radius*Mathf.Cos(angle),bottom=spring+radius*Mathf.Sin(angle);
                foreach(float z in new[]{-thickness/2,thickness/2})foreach(float y in new[]{bottom,top})
                {v.Add(new Vector3(x,y,z));uv.Add(new Vector2(x,y));}
            }
            for(int i=0;i<steps;i++)
            {
                int a=i*4,b=a+4;
                foreach(var q in new[]{new[]{a,a+1,b+1,b},new[]{a+2,b+2,b+3,a+3},new[]{a,b,b+2,a+2},new[]{a+1,a+3,b+3,b+1}})
                {t.AddRange(new[]{q[0],q[1],q[2],q[0],q[2],q[3]});}
            }
            t.AddRange(new[]{0,2,3,0,3,1,steps*4,steps*4+1,steps*4+3,steps*4,steps*4+3,steps*4+2});
            var mesh=new Mesh{name="True curved museum doorway header"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void Divider(float z)
        {
            for(int side=-1;side<=1;side+=2)Box("Museum room partition",new Vector3(side*4.9f,Roof/2,z),new Vector3(6.2f,Roof,.32f),palette.plaster);
            var header=new GameObject("Curved doorway · real open arch");header.transform.SetParent(root,false);header.transform.localPosition=new Vector3(0,0,z);
            var mesh=HeaderMesh(1.8f,3.9f,Roof,.32f);header.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=header.AddComponent<MeshRenderer>();renderer.sharedMaterial=palette.plaster;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            header.AddComponent<MeshCollider>().sharedMesh=mesh;
            // Trigger-only analytic proxies do not change the exact curved
            // physical opening. Eight strips keep sunlight occlusion bounded.
            for(int i=0;i<8;i++)
            {
                float a=Mathf.Lerp(-1.8f,1.8f,i/8f),b=Mathf.Lerp(-1.8f,1.8f,(i+1)/8f);
                float x=(a+b)/2,y=3.9f+Mathf.Sqrt(Mathf.Max(0,1.8f*1.8f-Mathf.Max(a*a,b*b)));
                var proxy=new GameObject("Arch sun occlusion proxy");proxy.transform.SetParent(root,false);proxy.transform.localPosition=new Vector3(x,(y+Roof)/2,z);
                var shape=proxy.AddComponent<BoxCollider>();shape.size=new Vector3(b-a,Roof-y,.32f);shape.isTrigger=true;
                proxy.AddComponent<GalleryCaster>().shape=shape;
            }
        }
        static void AttachArchitecturalCollisions(Transform architectural)
        {
            foreach(var child in architectural.GetComponentsInChildren<Transform>())
            {
                if(child.name=="Fluted limestone column")
                {
                    var box=child.gameObject.AddComponent<BoxCollider>();box.center=Vector3.up*3.075f;box.size=new Vector3(.89f,6.15f,.89f);
                    child.gameObject.AddComponent<GalleryCaster>().shape=box;
                }
                else if(child.name=="Museum walnut bench")
                {
                    float length=child.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.x;
                    var seat=new GameObject("Bench seat contact");seat.transform.SetParent(child,false);seat.transform.localPosition=Vector3.up*.52f;
                    var seatBox=seat.AddComponent<BoxCollider>();seatBox.size=new Vector3(length,.12f,.66f);seat.AddComponent<GalleryCaster>().shape=seatBox;
                    for(int side=-1;side<=1;side+=2)
                    {
                        var leg=new GameObject("Bench leg contact");leg.transform.SetParent(child,false);leg.transform.localPosition=new Vector3(side*(length/2-.30f),.245f,0);
                        var legBox=leg.AddComponent<BoxCollider>();legBox.size=new Vector3(.30f,.49f,.72f);leg.AddComponent<GalleryCaster>().shape=legBox;
                    }
                }
            }
        }
        public static GalleryGame Build()
        {
            GalleryWorld.Materials.Clear();root=new GameObject("PARALLAX · four-room concept museum").transform;
            palette=GalleryConceptArchitecture.MuseumPalette(GalleryWorld.Materials);
            var game=root.gameObject.AddComponent<GalleryGame>();game.conceptLayout=true;game.exhibitionPreview=false;
            game.lighting=root.gameObject.AddComponent<GalleryLighting>();game.lighting.conceptLayout=true;game.mirror=null;game.lightGate=null;
            Box("Museum limestone floor",new Vector3(0,-.15f,28),new Vector3(16,.30f,56),palette.stone,caster:false);
            Box("Museum outer left wall",new Vector3(-8.16f,Roof/2,28),new Vector3(.32f,Roof,56.5f),palette.plaster);
            GalleryConceptWindows.BuildRightWall(root,palette);
            Box("Museum entrance end wall",new Vector3(0,Roof/2,-.16f),new Vector3(16,Roof,.32f),palette.plaster);
            Box("Museum final end wall",new Vector3(0,Roof/2,56.16f),new Vector3(16,Roof,.32f),palette.plaster);
            foreach(float z in new[]{14f,29f,44f})Divider(z);
            float cursor=0;
            foreach(var opening in new[]{new Vector2(3,6.2f),new Vector2(32,35),new Vector2(47,50)})
            {
                if(opening.x>cursor)Box("Solid museum roof",new Vector3(0,Roof+.12f,(cursor+opening.x)/2),new Vector3(16,.24f,opening.x-cursor),palette.plaster,caster:false);
                if(opening.x==32)
                    foreach(int side in new[]{-1,1})
                    {
                        var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="Portrait side aisle · solid ceiling";
                        ceiling.transform.SetParent(root,false);
                        ceiling.transform.localPosition=new Vector3(side*(Width/2+PortraitSkylightHalfWidth)/2,Roof+.12f,(opening.x+opening.y)/2);
                        ceiling.transform.localScale=new Vector3(Width/2-PortraitSkylightHalfWidth,.24f,opening.y-opening.x);
                        ceiling.GetComponent<Renderer>().sharedMaterial=palette.plaster;
                    }
                cursor=opening.y;
            }
            if(cursor<Length)Box("Solid museum roof",new Vector3(0,Roof+.12f,(cursor+Length)/2),new Vector3(16,.24f,Length-cursor),palette.plaster,caster:false);
            var props=new List<GalleryProp>();
            GalleryConceptPerspective.Create(game,root,props,palette);
            game.props=props.ToArray();
            GalleryConceptArchitecture.Install(game,palette);
            AttachArchitecturalCollisions(root.Find("Concept museum architectural collection"));
            var playerObject=new GameObject("Museum visitor");playerObject.transform.SetParent(root,false);playerObject.transform.localPosition=new Vector3(0,0,3);
            game.controller=playerObject.AddComponent<CharacterController>();FirstPersonMotor.Configure(game.controller,null);
            var eye=new GameObject("Museum view");eye.transform.SetParent(playerObject.transform,false);eye.transform.localPosition=Vector3.up*1.55f;
            game.view=eye.AddComponent<Camera>();FirstPersonMotor.Configure(game.controller,game.view);PlayerControls.ConfigureCamera(game.view);
            game.view.farClipPlane=110;game.view.clearFlags=CameraClearFlags.SolidColor;game.view.backgroundColor=new Color(.70f,.77f,.82f);eye.AddComponent<AudioListener>();
            game.yaw=0;game.pitch=0;
            RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white;
            return game;
        }
    }
}
