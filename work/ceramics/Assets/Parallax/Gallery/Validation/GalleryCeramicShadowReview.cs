using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Diagnostic fixtures only. No shadow implementation or gameplay is changed.
    public static class GalleryCeramicShadowReview
    {
        const int Side=512;
        [Serializable] sealed class PoseResult
        {
            public string pose;
            public int samples,meshShadowPixels,gpuShadowPixels,extraShadowPixels,missingShadowPixels,cpuGpuMismatches;
            public float silhouetteIntersectionOverUnion;
        }
        [Serializable] sealed class Report
        {
            public string scope="Controlled diagnostic poses, not gameplay input. GPU uses the actual GallerySurface shader with only the vase casters and fully open skylights. Reference is the projected triangle mesh, not its colliders. Orange is excess proxy shadow; magenta is missing shadow.";
            public PoseResult[] poses;
        }
        public static void Run(GalleryGame game,string output)
        {
            var prop=Array.Find(game.props,p=>p.title==GalleryPuzzleExhibits.VesselTitle);
            var results=new List<PoseResult>();
            try
            {
                for(int pose=0;pose<3;pose++)
                {
                    prop.Restore();prop.transform.localScale=Vector3.one*(pose==2?2.4f:1);
                    prop.transform.SetPositionAndRotation(new Vector3(3.25f,prop.homePosition.y,10),pose==1?Quaternion.Euler(65,25,15):Quaternion.identity);
                    prop.transform.position+=Vector3.up*(.04f-prop.WorldBounds.min.y);
                    Physics.SyncTransforms();game.lighting.Synchronize();
                    string label=pose==0?"upright":pose==1?"tilted":"enlarged";
                    if(pose==0)SceneCapture(game,output);
                    results.Add(Compare(prop,label,output));
                }
                File.WriteAllText(Path.Combine(output,"ceramic-shadow-review.json"),JsonUtility.ToJson(new Report{poses=results.ToArray()},true));
            }
            finally{game.ResetAll();game.view.fieldOfView=72;game.lighting.Synchronize();game.SyncView();}
        }
        static Vector2 Project(Vector3 world)
        {
            return new Vector2(world.x,world.z+.75f*(world.y-GalleryLighting.SurfaceBias));
        }
        static PoseResult Compare(GalleryProp prop,string label,string output)
        {
            var surface=GalleryCeramicMesh.Vase();var projected=new Vector2[surface.vertices.Length];
            Vector2 min=new Vector2(1e6f,1e6f),max=new Vector2(-1e6f,-1e6f);
            for(int i=0;i<projected.Length;i++)
            {
                projected[i]=Project(prop.transform.TransformPoint(surface.vertices[i]));
                min=new Vector2(Mathf.Min(min.x,projected[i].x),Mathf.Min(min.y,projected[i].y));
                max=new Vector2(Mathf.Max(max.x,projected[i].x),Mathf.Max(max.y,projected[i].y));
            }
            var centre=(min+max)*.5f;float span=Mathf.Max(max.x-min.x,max.y-min.y)+1;
            var lower=centre-Vector2.one*(span*.5f);var meshMask=new bool[Side*Side];
            var surfaces=new[]{surface,GalleryCeramicMesh.Ring(.326f,2.055f,.006f),GalleryCeramicMesh.Ring(.301f,.059f,.004f)};
            foreach(var s in surfaces)
            {
                var points=new Vector2[s.vertices.Length];
                for(int i=0;i<points.Length;i++)points[i]=(Project(prop.transform.TransformPoint(s.vertices[i]))-lower)*(Side/span);
                for(int i=0;i<s.triangles.Length;i+=3)Raster(meshMask,points[s.triangles[i]],points[s.triangles[i+1]],points[s.triangles[i+2]]);
            }
            var matrices=new Matrix4x4[GalleryLighting.MaxCasters];
            for(int i=0;i<prop.parts.Length;i++)matrices[i]=prop.parts[i].GetComponent<GalleryCaster>().Inverse;
            Shader.SetGlobalInt("_GalleryCasterCount",prop.parts.Length);Shader.SetGlobalMatrixArray("_GalleryCasterInverse",matrices);
            Shader.SetGlobalVectorArray("_GalleryWindows",new[]{new Vector4(-1000,1000,-1000,1000),Vector4.zero,Vector4.zero,Vector4.zero});
            var plane=GameObject.CreatePrimitive(PrimitiveType.Quad);plane.layer=30;
            plane.transform.SetPositionAndRotation(new Vector3(centre.x,0,centre.y),Quaternion.Euler(90,0,0));plane.transform.localScale=new Vector3(span,span,1);
            UnityEngine.Object.DestroyImmediate(plane.GetComponent<Collider>());
            var material=new Material(Shader.Find("Parallax/GallerySurface"));material.SetFloat("_Diagnostic",1);plane.GetComponent<Renderer>().sharedMaterial=material;
            var camera=new GameObject("Ceramic shadow review camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=span*.5f;camera.aspect=1;camera.cullingMask=1<<30;
            camera.transform.SetPositionAndRotation(new Vector3(centre.x,12,centre.y),Quaternion.Euler(90,0,0));
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.magenta;
            var gpu=Read(camera,Side,Side);var pixels=gpu.GetPixels32();
            var expected=new Color32[Side*Side];var difference=new Color32[Side*Side];
            var result=new PoseResult{pose=label,samples=Side*Side};int intersection=0,union=0;
            for(int y=0;y<Side;y++)for(int x=0;x<Side;x++)
            {
                int index=y*Side+x;bool mesh=meshMask[index],actual=pixels[index].r<128,cpu=false;
                var origin=new Vector3(lower.x+(x+.5f)*span/Side,GalleryLighting.SurfaceBias,lower.y+(y+.5f)*span/Side);
                for(int c=0;c<prop.parts.Length&&!cpu;c++)cpu=GalleryLighting.Intersects(matrices[c],origin,GalleryLighting.ToSun);
                if(cpu!=actual)result.cpuGpuMismatches++;
                if(mesh)result.meshShadowPixels++;if(actual)result.gpuShadowPixels++;
                if(mesh&&actual)intersection++;if(mesh||actual)union++;
                if(actual&&!mesh)result.extraShadowPixels++;if(mesh&&!actual)result.missingShadowPixels++;
                expected[index]=mesh?new Color32(25,35,45,255):new Color32(250,250,250,255);
                difference[index]=actual&&!mesh?new Color32(240,136,40,255):mesh&&!actual?new Color32(210,50,140,255):expected[index];
            }
            result.silhouetteIntersectionOverUnion=intersection/(float)Mathf.Max(1,union);
            File.WriteAllBytes(Path.Combine(output,"shadow-"+label+"-gpu.png"),gpu.EncodeToPNG());
            Save(expected,Path.Combine(output,"shadow-"+label+"-mesh.png"));Save(difference,Path.Combine(output,"shadow-"+label+"-difference.png"));
            UnityEngine.Object.DestroyImmediate(gpu);UnityEngine.Object.DestroyImmediate(plane);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(camera.gameObject);
            return result;
        }
        static float Edge(Vector2 a,Vector2 b,Vector2 p){return (b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);}
        static void Raster(bool[] pixels,Vector2 a,Vector2 b,Vector2 c)
        {
            float area=Edge(a,b,c);if(Mathf.Abs(area)<.00001f)return;
            int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,Side-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,Side-1);
            int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,Side-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,Side-1);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);float e0=Edge(a,b,p),e1=Edge(b,c,p),e2=Edge(c,a,p);
                if(area>0?e0>=0&&e1>=0&&e2>=0:e0<=0&&e1<=0&&e2<=0)pixels[y*Side+x]=true;
            }
        }
        static Texture2D Read(Camera camera,int width,int height,bool linear=true)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,linear?RenderTextureReadWrite.Linear:RenderTextureReadWrite.Default);
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false,linear);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            camera.targetTexture=previous;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);return texture;
        }
        static void Save(Color32[] pixels,string path)
        {
            var t=new Texture2D(Side,Side,TextureFormat.RGB24,false,true);t.SetPixels32(pixels);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);
        }
        static void SceneCapture(GalleryGame game,string output)
        {
            game.view.transform.SetPositionAndRotation(new Vector3(3.25f,7.7f,10.8f),Quaternion.Euler(90,0,0));
            game.view.fieldOfView=48;game.lighting.Synchronize();var texture=Read(game.view,1600,900,false);
            File.WriteAllBytes(Path.Combine(output,"06-actual-shadow-in-sunlight.png"),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
