using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    public static class GalleryConceptScans
    {
        public static void Install(GalleryGame game)
        {
            Mesh mesh=Resources.Load<Mesh>("MuseumScans/NikeMesh");
            if(mesh==null)throw new InvalidOperationException("Actual Nike scan must be imported before preparing the concept museum.");
            Mesh collisionMesh=Resources.Load<Mesh>("MuseumScans/NikeCollision");
            if(collisionMesh==null)throw new InvalidOperationException("The scan's derived collision mesh must be prepared first.");
            var parent=new GameObject("Collection · Winged Victory · Cosmo Wenman scan").transform;
            parent.SetParent(game.transform,false);parent.localPosition=new Vector3(-2,0,24);
            var material=new Material(Shader.Find("Parallax/MuseumSurface")){name="Concept Nike · cast plaster"};
            GalleryMuseumMaterials.Apply(material,"Plaster");material.color=new Color(.96f,.94f,.88f);
            material.SetFloat("_WorldTiling",.3f);material.SetFloat("_NormalStrength",.12f);material.SetFloat("_Roughness",.86f);
            GalleryWorld.Materials.Add(material);
            var stone=new Material(Shader.Find("Parallax/MuseumSurface")){name="Concept Nike · marble plinth"};
            GalleryMuseumMaterials.Apply(stone,"Stone");stone.SetFloat("_WorldTiling",.12f);GalleryWorld.Materials.Add(stone);
            const float displayHeight=3.65f;
            bool haveFoot=false;Bounds foot=default;
            foreach(var vertex in mesh.vertices)if(vertex.y<.055f)
            {if(!haveFoot){foot=new Bounds(vertex,Vector3.zero);haveFoot=true;}else foot.Encapsulate(vertex);}
            Vector3 plinthCenter=haveFoot?new Vector3(foot.center.x*displayHeight,.38f,foot.center.z*displayHeight):new Vector3(0,.38f,0);
            Vector3 plinthSize=haveFoot?new Vector3(Mathf.Max(2.2f,foot.size.x*displayHeight+.35f),.76f,Mathf.Max(2.2f,foot.size.z*displayHeight+.35f)):new Vector3(2.2f,.76f,2.2f);
            var plinth=GalleryConceptArchitecture.Block(parent,"Nike limestone display plinth",plinthCenter,plinthSize,stone,.04f);
            var pedestal=plinth.gameObject.AddComponent<BoxCollider>();pedestal.size=plinthSize;
            plinth.gameObject.AddComponent<GalleryCaster>().shape=pedestal;
            var sculpture=new GameObject("Nike · original scanned geometry");sculpture.transform.SetParent(parent,false);
            sculpture.transform.localPosition=Vector3.up*.76f;sculpture.transform.localScale=Vector3.one*displayHeight;
            sculpture.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=sculpture.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            // Display every original scan triangle. Physics uses a separate
            // scan-derived mesh with a bounded centimetre-scale approximation.
            var contact=new GameObject("Nike · scan-derived physical surface");contact.transform.SetParent(sculpture.transform,false);
            var collider=contact.AddComponent<MeshCollider>();collider.sharedMesh=collisionMesh;
            var labelObject=new GameObject("Nike collection attribution");labelObject.transform.SetParent(parent,false);labelObject.transform.localPosition=new Vector3(1.5f,1.08f,-.72f);
            var label=labelObject.AddComponent<GalleryLabel>();label.caption="사모트라케의 니케\nSkulpturhalle Basel 석고상 스캔\nCosmo Wenman · CC BY 3.0";
            label.size=.075f;label.color=new Color(.22f,.19f,.15f);
        }
    }
}
