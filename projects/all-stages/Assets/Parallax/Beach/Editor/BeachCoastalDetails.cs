using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static partial void ConfigureCoastalMirror(BeachGame game);
        static partial void ConfigureCoastalReceivers(BeachGame game);
        static partial void ConfigureCoastalBoundaries(BeachGame game);
        static partial void ConfigureCoastalWorkshop(BeachGame game);
        static partial void ConfigureCoastalRental(BeachGame game);

        static void ConfigureCoastalDetails(BeachGame game)
        {
            CoastalResources();
            ConfigureCoastalMirror(game);ConfigureCoastalReceivers(game);ConfigureCoastalBoundaries(game);
            ConfigureCoastalWorkshop(game);ConfigureCoastalRental(game);
            PersistCoastalDetails(game);
        }
        static void PersistCoastalDetails(BeachGame game)
        {
            var filters=game.GetComponentsInChildren<MeshFilter>(true);
            foreach(var filter in filters)
            {
                var mesh=filter.sharedMesh;if(mesh==null||AssetDatabase.Contains(mesh))continue;
                // Save replaces the transient mesh; every shared user must move with it.
                var users=filters.Where(f=>f.sharedMesh==mesh).ToArray();
                var colliders=game.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.sharedMesh==mesh).ToArray();
                var saved=Save(mesh,Generated+mesh.name.Replace('/','_')+".asset");
                foreach(var user in users)user.sharedMesh=saved;
                foreach(var collider in colliders)collider.sharedMesh=saved;
            }
            var renderers=game.GetComponentsInChildren<Renderer>(true);
            var materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&!AssetDatabase.Contains(m)).Distinct().ToArray();
            foreach(var material in materials)
            {
                string path=Generated+material.name.Replace('/','_')+".mat";
                var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved==null){AssetDatabase.CreateAsset(material,path);continue;}
                EditorUtility.CopySerialized(material,saved);
                foreach(var renderer in renderers)
                {var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)if(slots[i]==material)slots[i]=saved;renderer.sharedMaterials=slots;}
                UnityEngine.Object.DestroyImmediate(material);
            }
            foreach(var prop in game.props)prop.Remember();
        }
        static Material CoastalMaterial(string name,Color color,float metallic=0,float smoothness=.3f)
        {
            var material=new Material(Shader.Find("Parallax/BeachSurface")){name=name,color=color};
            material.SetFloat("_Metallic",metallic);material.SetFloat("_Glossiness",smoothness);return material;
        }
        static Material CoastalWood(string name,Color tint,bool painted=false)
        {
            var source=AssetDatabase.LoadAssetAtPath<Material>(Generated+"Weathered timber.mat");
            if(source==null)throw new InvalidOperationException("Coastal detail requires the existing timber material.");
            var material=new Material(source){name=name,color=tint};
            if(painted)material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Generated+"Coastal painted timber grain.asset");
            material.SetFloat("_Glossiness",painted?.22f:.18f);material.SetFloat("_Metallic",0);return material;
        }
        static Material CoastalLinen(string name,Color tint)
        {
            var material=new Material(Shader.Find("Parallax/BeachTexturedSurface")){name=name,color=tint};
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Generated+"Coastal linen weave.asset");
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Generated+"Coastal linen normal.asset"));
            material.SetFloat("_BumpScale",.28f);material.SetFloat("_Tiling",4);material.SetFloat("_Glossiness",.09f);return material;
        }
        static void CoastalResources()
        {
            const int size=256;
            if(AssetDatabase.LoadAssetAtPath<Texture2D>(Generated+"Coastal linen weave.asset")==null||AssetDatabase.LoadAssetAtPath<Texture2D>(Generated+"Coastal linen normal.asset")==null)
            {
                var cloth=new Texture2D(size,size,TextureFormat.RGB24,true){name="Coastal linen weave",wrapMode=TextureWrapMode.Repeat,anisoLevel=8};
                var normal=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Coastal linen normal",wrapMode=TextureWrapMode.Repeat,anisoLevel=8};
                float Height(int x,int y)=>.5f+.13f*Mathf.Sin(x*Mathf.PI*.5f)*Mathf.Cos(y*Mathf.PI*.5f)+.035f*Mathf.Sin(x*1.57f+y*2.31f);
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float h=Height(x,y),shade=.87f+h*.13f;cloth.SetPixel(x,y,new Color(shade,shade,shade));
                    var n=new Vector3((Height(x-1,y)-Height(x+1,y))*.65f,(Height(x,y-1)-Height(x,y+1))*.65f,1).normalized;
                    normal.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1));
                }
                cloth.Apply();normal.Apply();Save(cloth,Generated+cloth.name+".asset");Save(normal,Generated+normal.name+".asset");
            }
            if(AssetDatabase.LoadAssetAtPath<Texture2D>(Generated+"Coastal painted timber grain.asset")==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/PlankTexturesPBR/Planks4a.tif");
                if(source==null)throw new InvalidOperationException("Coastal painted timber requires the existing plank texture.");
                var target=RenderTexture.GetTemporary(512,512,0,RenderTextureFormat.ARGB32);var active=RenderTexture.active;
                var grain=new Texture2D(512,512,TextureFormat.RGB24,true){name="Coastal painted timber grain",wrapMode=TextureWrapMode.Repeat,anisoLevel=8};
                try
                {
                    Graphics.Blit(source,target);RenderTexture.active=target;grain.ReadPixels(new Rect(0,0,512,512),0,0);
                    var pixels=grain.GetPixels();for(int i=0;i<pixels.Length;i++){float value=.68f+pixels[i].grayscale*.48f;pixels[i]=new Color(value,value,value);}
                    grain.SetPixels(pixels);grain.Apply();Save(grain,Generated+grain.name+".asset");
                }
                finally{RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);}
            }
        }
        static GameObject CoastalMesh(Transform parent,string name,Vector3 position,Mesh mesh,Material material,bool shadow)
        {
            var item=new GameObject(name);item.transform.SetParent(parent,false);item.transform.localPosition=position;
            mesh.name=name+" mesh";item.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=item.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;
            return item;
        }
        static Mesh CoastalRoundedBoxMesh(Vector3 size,float bevel)
        {
            var half=size*.5f;float radius=Mathf.Min(bevel,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.8f);
            if(radius<=0||size.x<=0||size.y<=0||size.z<=0)throw new ArgumentOutOfRangeException(nameof(size));
            var inner=half-Vector3.one*radius;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            var axes=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            var us=new[]{Vector3.up,Vector3.forward,Vector3.forward,Vector3.right,Vector3.right,Vector3.up};
            var vs=new[]{Vector3.forward,Vector3.up,Vector3.right,Vector3.forward,Vector3.up,Vector3.right};
            for(int face=0;face<6;face++)
            {
                var n=axes[face];var u=us[face];var v=vs[face];
                float hn=Vector3.Dot(half,new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z)));
                float hu=Vector3.Dot(half,u),hv=Vector3.Dot(half,v);
                var xs=new[]{-hu,-hu+radius,hu-radius,hu};var ys=new[]{-hv,-hv+radius,hv-radius,hv};int start=vertices.Count;
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                {
                    var point=n*hn+u*xs[x]+v*ys[y];var clamped=new Vector3(Mathf.Clamp(point.x,-inner.x,inner.x),Mathf.Clamp(point.y,-inner.y,inner.y),Mathf.Clamp(point.z,-inner.z,inner.z));
                    var normal=(point-clamped).normalized;vertices.Add(clamped+normal*radius);normals.Add(normal);uv.Add(new Vector2((xs[x]/hu+1)*.5f,(ys[y]/hv+1)*.5f));
                }
                for(int y=0;y<3;y++)for(int x=0;x<3;x++){int a=start+y*4+x;triangles.AddRange(new[]{a,a+1,a+5,a,a+5,a+4});}
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
        static GameObject CoastalBox(Transform parent,string name,Vector3 position,Vector3 size,Material material,float bevel=.03f,bool shadow=false,bool solid=false)
        {
            var item=CoastalMesh(parent,name,position,CoastalRoundedBoxMesh(size,bevel),material,shadow);
            if(solid)item.AddComponent<BoxCollider>().size=size;return item;
        }
        static GameObject CoastalCylinder(Transform parent,string name,Vector3 position,Vector3 size,Material material,Quaternion rotation,bool shadow=false)
        {
            var item=GameObject.CreatePrimitive(PrimitiveType.Cylinder);item.name=name;item.transform.SetParent(parent,false);
            item.transform.localPosition=position;item.transform.localScale=new Vector3(size.x,size.y*.5f,size.z);item.transform.localRotation=rotation;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());var renderer=item.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;return item;
        }
        static GameObject CoastalRing(Transform parent,string name,Vector3 position,float radius,float tube,Material material,bool shadow=false)
        {
            const int major=64,minor=8;var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<=major;i++)for(int j=0;j<=minor;j++)
            {
                float a=i*Mathf.PI*2/major,b=j*Mathf.PI*2/minor;var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                var n=radial*Mathf.Cos(b)+Vector3.forward*Mathf.Sin(b);vertices.Add(radial*radius+n*tube);normals.Add(n);uv.Add(new Vector2(i/(float)major,j/(float)minor));
                if(i<major&&j<minor){int k=i*(minor+1)+j;triangles.AddRange(new[]{k,k+minor+1,k+minor+2,k,k+minor+2,k+1});}
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return CoastalMesh(parent,name,position,mesh,material,shadow);
        }
        static GameObject CoastalRope(Transform parent,string name,Vector3 start,Vector3 end,float sag,float radius,Material material,bool shadow=false)
        {
            if((end-start).sqrMagnitude<.0001f||radius<=0)throw new ArgumentOutOfRangeException(nameof(end));
            const int length=16,sides=8;var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<=length;i++)
            {
                float t=i/(float)length;var centre=Vector3.Lerp(start,end,t)-Vector3.up*Mathf.Sin(t*Mathf.PI)*sag;
                var tangent=(end-start-Vector3.up*Mathf.Cos(t*Mathf.PI)*Mathf.PI*sag).normalized;
                var u=Vector3.Cross(tangent,Vector3.up).normalized;var v=Vector3.Cross(tangent,u).normalized;
                for(int j=0;j<=sides;j++)
                {
                    float angle=j*Mathf.PI*2/sides;var normal=u*Mathf.Cos(angle)+v*Mathf.Sin(angle);vertices.Add(centre+normal*radius);normals.Add(normal);uv.Add(new Vector2(t*Vector3.Distance(start,end),j/(float)sides));
                    if(i<length&&j<sides){int k=i*(sides+1)+j;triangles.AddRange(new[]{k,k+sides+2,k+sides+1,k,k+1,k+sides+2});}
                }
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return CoastalMesh(parent,name,Vector3.zero,mesh,material,shadow);
        }
        static BeachLabel CoastalLabel(Transform parent,string name,Vector3 position,string caption,float size,Color color,Quaternion rotation)
        {
            var item=new GameObject(name);item.transform.SetParent(parent,false);item.transform.localPosition=position;item.transform.localRotation=rotation;
            var label=item.AddComponent<BeachLabel>();label.caption=caption;label.size=size;label.color=color;label.style=FontStyle.Bold;return label;
        }
    }
}
