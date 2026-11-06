using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    public static class GalleryConceptScans
    {
        public static GalleryProp Install(GalleryGame game)
        {
            Mesh mesh=Resources.Load<Mesh>("MuseumScans/NikeMesh");
            if(mesh==null)throw new InvalidOperationException("Actual Nike scan must be imported before preparing the concept museum.");
            var parent=new GameObject("02 · 사모트라케의 니케").transform;
            parent.SetParent(game.transform,false);parent.localPosition=new Vector3(0,0,21.65f);
            var prop=parent.gameObject.AddComponent<GalleryProp>();
            prop.title=parent.name;prop.zone=1;prop.minScale=.45f;
            var material=new Material(Shader.Find("Parallax/MuseumSurface")){name="Concept Nike · cast plaster"};
            GalleryMuseumMaterials.Apply(material,"Plaster");material.color=new Color(.96f,.94f,.88f);
            material.SetFloat("_WorldTiling",.3f);material.SetFloat("_NormalStrength",.12f);material.SetFloat("_Roughness",.86f);
            material.SetFloat("_ExhibitLight",.24f);
            GalleryWorld.Materials.Add(material);
            var stone=new Material(Shader.Find("Parallax/MuseumSurface")){name="Concept Nike · marble plinth"};
            GalleryMuseumMaterials.Apply(stone,"Stone");stone.SetFloat("_WorldTiling",.12f);GalleryWorld.Materials.Add(stone);
            const float displayHeight=2.85f,baseHeight=.52f;
            var vertices=mesh.vertices;
            bool haveFoot=false;Bounds foot=default;
            foreach(var vertex in vertices)if(vertex.y<.055f)
            {if(!haveFoot){foot=new Bounds(vertex,Vector3.zero);haveFoot=true;}else foot.Encapsulate(vertex);}
            Vector3 plinthCenter=new Vector3(foot.center.x*displayHeight,baseHeight/2,foot.center.z*displayHeight);
            Vector3 plinthSize=new Vector3(Mathf.Max(1.45f,foot.size.x*displayHeight+.25f),baseHeight,Mathf.Max(1.25f,foot.size.z*displayHeight+.25f));
            var plinth=GalleryConceptArchitecture.Block(parent,"Nike limestone display plinth",plinthCenter,plinthSize,stone,.04f);
            var pedestal=plinth.gameObject.AddComponent<BoxCollider>();pedestal.size=plinthSize;
            var sculpture=new GameObject("Nike · original scanned geometry");sculpture.transform.SetParent(parent,false);
            sculpture.transform.localPosition=Vector3.up*baseHeight;sculpture.transform.localScale=Vector3.one*displayHeight;
            sculpture.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=sculpture.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            // The movable rigidbody uses fitted boxes. The original mesh casts the visible shadow.
            const int slices=10;
            var shape=new Bounds[slices];var used=new bool[slices];
            foreach(var vertex in vertices)
            {
                int slice=Mathf.Min(slices-1,Mathf.FloorToInt(Mathf.InverseLerp(mesh.bounds.min.y,mesh.bounds.max.y,vertex.y)*slices));
                if(used[slice])shape[slice].Encapsulate(vertex);
                else{shape[slice]=new Bounds(vertex,Vector3.zero);used[slice]=true;}
            }
            for(int i=0;i<slices;i++)if(used[i])
            {
                var collider=sculpture.AddComponent<BoxCollider>();collider.center=shape[i].center;
                collider.size=Vector3.Max(shape[i].size,Vector3.one*.015f)+Vector3.up*.004f;
            }
            var labelObject=new GameObject("Nike collection attribution");labelObject.transform.SetParent(game.transform,false);
            labelObject.transform.localPosition=parent.localPosition+new Vector3(1.5f,.22f,-.72f);
            var label=labelObject.AddComponent<GalleryLabel>();label.caption="사모트라케의 니케\nSkulpturhalle Basel 석고상 스캔\nCosmo Wenman · CC BY 3.0";
            label.size=.075f;label.color=new Color(.22f,.19f,.15f);
            prop.Remember();return prop;
        }
    }
}
