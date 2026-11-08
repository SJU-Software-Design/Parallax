using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    public sealed class BeachWaterAppearance:MonoBehaviour
    {
        struct Surface
        {
            public Renderer original;
            public GameObject visual;
            public Material material;
        }
        readonly List<Surface> surfaces=new List<Surface>();
        Camera view;
        DepthTextureMode originalDepth;

        void OnEnable()
        {
            var game=GetComponent<BeachGame>();
            if(game==null||game.view==null)return;
            const string path="ParallaxSimpleWater/";
            var main=Resources.Load<Texture2D>(path+"WaterSurface_atlas");
            var second=Resources.Load<Texture2D>(path+"WaterSurface_single");
            var mesh=Resources.Load<Mesh>(path+"WaterBlock_50m");
            var shader=Shader.Find("Parallax/BeachSimpleWater");
            if(main==null||second==null||mesh==null||shader==null)
                throw new InvalidOperationException("Simple Water Shader assets are missing.");
            view=game.view;originalDepth=view.depthTextureMode;view.depthTextureMode|=DepthTextureMode.Depth;
            var bounds=mesh.bounds;
            foreach(var source in game.GetComponentsInChildren<Renderer>(true))
            {
                if(!source.enabled||source.sharedMaterial==null||source.sharedMaterial.shader.name!="Parallax/BeachWaterRefresh")continue;
                var previous=source.sharedMaterial;
                float kind=previous.GetFloat("_WaterKind");bool puddle=kind>.5f&&kind<1.5f;
                var material=new Material(shader){name=previous.name+" · Simple Water"};
                material.SetTexture("_MainNormal",main);material.SetTexture("_SecondNormal",second);
                var region=source.GetComponent<BeachWater>();
                Vector3 flow=region!=null?region.flow:Vector3.zero;
                material.SetVector("_Flow",new Vector4(flow.x,flow.z,Mathf.Min(flow.magnitude,.12f),0));material.SetFloat("_WaterKind",kind);
                material.SetVector("_Area",kind<.5f?new Vector4(-14,0,14,108):previous.GetVector("_Area"));
                material.SetFloat("_DepthStrength",puddle?2.5f:.35f);
                material.SetFloat("_WaveHeight",puddle?.002f:kind<.5f?.02f:.015f);
                var visual=new GameObject("Simple Water · "+source.name);visual.layer=source.gameObject.layer;
                visual.transform.SetParent(source.transform,false);
                visual.transform.localScale=new Vector3(1/bounds.size.x,1,1/bounds.size.z);
                visual.transform.localPosition=new Vector3(-bounds.center.x/bounds.size.x,.5f-bounds.max.y,-bounds.center.z/bounds.size.z);
                visual.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                surfaces.Add(new Surface{original=source,visual=visual,material=material});source.enabled=false;
            }
        }
        void OnDisable()
        {
            foreach(var surface in surfaces)
            {
                if(surface.original!=null)surface.original.enabled=true;
                if(surface.visual!=null){surface.visual.SetActive(false);Destroy(surface.visual);}
                if(surface.material!=null)Destroy(surface.material);
            }
            surfaces.Clear();if(view!=null)view.depthTextureMode=originalDepth;
        }
    }
}
