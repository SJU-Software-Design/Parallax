using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Analytic shadows for the visible skylight frame. The proxies are triggers,
    // so they affect exactly the sunlight mask, never movement or pickup rays.
    public static class GalleryConceptAtmosphere
    {
        const string Marker="Skylight shadow grid installed";
        const string Skylight="Pitched museum skylight lantern";
        const string Proxy="Skylight analytic shadow";

        public static void Install(GalleryGame game)
        {
            if(game==null)throw new ArgumentNullException(nameof(game));
            if(game.transform.Find(Marker)!=null)return;
            var roots=new List<Transform>();
            foreach(var node in game.GetComponentsInChildren<Transform>())
                if(node.name==Skylight)roots.Add(node);
            if(roots.Count!=4)throw new InvalidOperationException("Skylight grid needs the four visible skylight lanterns; found "+roots.Count);

            var selected=new List<MeshFilter>();
            foreach(var skylight in roots)
            {
                float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
                foreach(Transform child in skylight)
                    if(child.name=="Skylight sloped glazing bar")
                    {minimum=Mathf.Min(minimum,child.localPosition.z);maximum=Mathf.Max(maximum,child.localPosition.z);}
                int longitudinal=0,ends=0;
                foreach(Transform child in skylight)
                {
                    bool lengthwise=child.name=="Skylight ridge"||child.name=="Skylight mid-height rail";
                    bool end=child.name=="Skylight sloped glazing bar"&&
                        (Mathf.Abs(child.localPosition.z-minimum)<.01f||Mathf.Abs(child.localPosition.z-maximum)<.01f);
                    if(!lengthwise&&!end)continue;
                    var mesh=child.GetComponent<MeshFilter>();
                    if(mesh==null||mesh.sharedMesh==null)throw new InvalidOperationException("Visible skylight bar has no mesh: "+child.name);
                    selected.Add(mesh);if(lengthwise)longitudinal++;else ends++;
                }
                if(longitudinal!=3||ends!=4)
                    throw new InvalidOperationException("Skylight frame geometry changed: expected three rails and four sloped end bars; got "+longitudinal+" and "+ends);
            }

            int existing=UnityEngine.Object.FindObjectsByType<GalleryCaster>(FindObjectsSortMode.None).Length;
            if(existing+selected.Count>GalleryLighting.MaxCasters)
                throw new InvalidOperationException("Skylight shadow budget exceeded: existing="+existing+" grid="+selected.Count+" limit="+GalleryLighting.MaxCasters);

            var marker=new GameObject(Marker);marker.transform.SetParent(game.transform,false);
            foreach(var source in selected)
            {
                // Copy the source bar's local mesh bounds under that same
                // transform. Pitched end bars retain their real slope and the
                // ridge/mid-rails retain their different physical heights.
                var shadow=new GameObject(Proxy+" · "+source.name);
                shadow.transform.SetParent(source.transform,false);
                var box=shadow.AddComponent<BoxCollider>();
                Bounds bounds=source.sharedMesh.bounds;box.center=bounds.center;
                box.size=new Vector3(Mathf.Max(.055f,bounds.size.x),bounds.size.y,Mathf.Max(.055f,bounds.size.z));
                box.isTrigger=true;
                shadow.AddComponent<GalleryCaster>().shape=box;
            }
            if(game.lighting!=null)game.lighting.Rebuild();
            Debug.Log("MUSEUM_SKYLIGHT_GRID_OK skylights="+roots.Count+" addedCasters="+selected.Count+" totalCasters="+(existing+selected.Count));
        }
    }
}
