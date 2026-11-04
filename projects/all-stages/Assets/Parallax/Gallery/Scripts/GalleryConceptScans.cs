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
            var parent=new GameObject("Collection · Winged Victory · Cosmo Wenman scan").transform;
            parent.SetParent(game.transform,false);parent.localPosition=new Vector3(-2.8f,0,22.5f);parent.localScale=Vector3.one*.8f;
            var prop=parent.gameObject.AddComponent<GalleryProp>();
            prop.title="02 · 사모트라케의 니케";prop.zone=1;prop.minScale=.45f;prop.platform=false;
            var material=new Material(Shader.Find("Parallax/MuseumSurface")){name="Concept Nike · cast plaster"};
            GalleryMuseumMaterials.Apply(material,"Plaster");material.color=new Color(.96f,.94f,.88f);
            material.SetFloat("_WorldTiling",.3f);material.SetFloat("_NormalStrength",.12f);material.SetFloat("_Roughness",.86f);
            GalleryWorld.Materials.Add(material);
            var stone=new Material(Shader.Find("Parallax/MuseumSurface")){name="Concept Nike · marble plinth"};
            GalleryMuseumMaterials.Apply(stone,"Stone");stone.SetFloat("_WorldTiling",.12f);GalleryWorld.Materials.Add(stone);
            const float displayHeight=2.8f,plinthHeight=.72f;
            bool haveFoot=false;Bounds foot=default;
            foreach(var vertex in mesh.vertices)if(vertex.y<.055f)
            {if(!haveFoot){foot=new Bounds(vertex,Vector3.zero);haveFoot=true;}else foot.Encapsulate(vertex);}
            Vector3 plinthCenter=haveFoot?new Vector3(foot.center.x*displayHeight,plinthHeight*.5f,foot.center.z*displayHeight):new Vector3(0,plinthHeight*.5f,0);
            Vector3 plinthSize=haveFoot?new Vector3(Mathf.Max(1.35f,foot.size.x*displayHeight+.30f),plinthHeight,Mathf.Max(1.35f,foot.size.z*displayHeight+.30f)):new Vector3(1.35f,plinthHeight,1.35f);
            var plinth=GalleryConceptArchitecture.Block(parent,"Nike limestone display plinth",plinthCenter,plinthSize,stone,.04f);
            var pedestal=plinth.gameObject.AddComponent<BoxCollider>();pedestal.size=plinthSize;
            plinth.gameObject.AddComponent<GalleryCaster>().shape=pedestal;
            var sculpture=new GameObject("Nike · original scanned geometry");sculpture.transform.SetParent(parent,false);
            sculpture.transform.localPosition=Vector3.up*plinthHeight;sculpture.transform.localScale=Vector3.one*displayHeight;
            var filter=sculpture.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=sculpture.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            // A dynamic non-convex MeshCollider is invalid in PhysX. These
            // conservative bands are for moving/placing the prop only; exact
            // scan triangles, never these boxes, determine its light silhouette.
            Vector3[] vertices=mesh.vertices;int[] indices=mesh.triangles;
            float[] levels={0,.15f,.40f,.65f,.80f,1};
            for(int band=0;band<levels.Length-1;band++)
            {
                bool found=false;Bounds bounds=default;
                for(int i=0;i<indices.Length;i+=3)
                {
                    Vector3 a=vertices[indices[i]],b=vertices[indices[i+1]],c=vertices[indices[i+2]];
                    if(Mathf.Min(a.y,Mathf.Min(b.y,c.y))>levels[band+1]||Mathf.Max(a.y,Mathf.Max(b.y,c.y))<levels[band])continue;
                    if(!found){bounds=new Bounds(a,Vector3.zero);found=true;}
                    bounds.Encapsulate(a);bounds.Encapsulate(b);bounds.Encapsulate(c);
                }
                if(!found)continue;
                var contact=new GameObject("Nike carry volume · band "+band);contact.transform.SetParent(parent,false);
                var collider=contact.AddComponent<BoxCollider>();
                collider.center=new Vector3(bounds.center.x*displayHeight,plinthHeight+(levels[band]+levels[band+1])*.5f*displayHeight,bounds.center.z*displayHeight);
                collider.size=new Vector3(bounds.size.x*displayHeight+.024f,(levels[band+1]-levels[band])*displayHeight+.012f,bounds.size.z*displayHeight+.024f);
            }
            var geometry=parent.gameObject.AddComponent<GalleryNikeGeometry>();geometry.scanFilter=filter;geometry.scanRenderer=renderer;
            var labelObject=new GameObject("Nike collection attribution");labelObject.transform.SetParent(parent,false);labelObject.transform.localPosition=new Vector3(1.5f,1.08f,-.72f);
            var label=labelObject.AddComponent<GalleryLabel>();label.caption="사모트라케의 니케\nSkulpturhalle Basel 석고상 스캔\nCosmo Wenman · CC BY 3.0";
            label.size=.075f;label.color=new Color(.22f,.19f,.15f);
            prop.Remember();
            var props=new System.Collections.Generic.List<GalleryProp>(game.props??Array.Empty<GalleryProp>());
            props.Add(prop);game.props=props.ToArray();
        }
    }
}
