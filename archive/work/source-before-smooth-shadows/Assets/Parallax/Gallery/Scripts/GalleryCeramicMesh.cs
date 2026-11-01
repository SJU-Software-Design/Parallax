using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // A closed, hollow surface of revolution. The control points define an
    // original pear-shaped porcelain vase, including its inner wall and rolled lip.
    public static class GalleryCeramicMesh
    {
        public const int RadialSegments=128;
        public const float Height=2.10f, PivotHeight=1.05f;
        public sealed class Surface
        {
            public Vector3[] vertices, normals;
            public Vector2[] uv;
            public int[] triangles;
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name};
                mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
            }
        }
        static Vector2 P(float radius,float y){return new Vector2(radius,y);}
        static void Curve(List<Vector2> profile,Vector2 a,Vector2 b,Vector2 c,Vector2 d,int steps)
        {
            if(profile.Count==0)profile.Add(a);
            for(int i=1;i<=steps;i++)
            {
                float t=i/(float)steps,s=1-t;
                profile.Add(a*(s*s*s)+b*(3*s*s*t)+c*(3*s*t*t)+d*(t*t*t));
            }
        }
        static List<Vector2> Profile(out int innerStart)
        {
            var p=new List<Vector2>();
            Curve(p,P(0,0),P(.12f,0),P(.24f,0),P(.28f,.018f),4);
            Curve(p,P(.28f,.018f),P(.304f,.027f),P(.304f,.070f),P(.290f,.108f),8);
            Curve(p,P(.290f,.108f),P(.284f,.156f),P(.366f,.191f),P(.47f,.27f),12);
            Curve(p,P(.47f,.27f),P(.642f,.388f),P(.748f,.560f),P(.735f,.79f),20);
            Curve(p,P(.735f,.79f),P(.726f,1.02f),P(.605f,1.245f),P(.452f,1.36f),20);
            Curve(p,P(.452f,1.36f),P(.293f,1.474f),P(.208f,1.517f),P(.210f,1.695f),16);
            Curve(p,P(.210f,1.695f),P(.207f,1.838f),P(.253f,1.935f),P(.315f,2.018f),14);
            Curve(p,P(.315f,2.018f),P(.350f,2.06f),P(.332f,2.101f),P(.303f,2.09f),10);
            innerStart=p.Count-1;
            Curve(p,P(.303f,2.09f),P(.275f,2.080f),P(.270f,2.04f),P(.279f,2.02f),8);
            Curve(p,P(.279f,2.02f),P(.218f,1.93f),P(.174f,1.82f),P(.178f,1.695f),14);
            Curve(p,P(.178f,1.695f),P(.181f,1.501f),P(.267f,1.435f),P(.432f,1.322f),16);
            Curve(p,P(.432f,1.322f),P(.576f,1.209f),P(.689f,1.00f),P(.697f,.79f),20);
            Curve(p,P(.697f,.79f),P(.711f,.588f),P(.609f,.425f),P(.449f,.306f),20);
            Curve(p,P(.449f,.306f),P(.324f,.201f),P(.198f,.160f),P(0,.160f),16);
            return p;
        }
        public static Surface Vase()
        {
            int inner;var profile=Profile(out inner);
            return Revolve(profile,inner,false);
        }
        public static Surface Ring(float radius,float y,float tube)
        {
            var profile=new List<Vector2>();
            for(int i=0;i<=12;i++)
            {
                float a=i*2*Mathf.PI/12;
                profile.Add(P(radius+Mathf.Cos(a)*tube,y+Mathf.Sin(a)*tube));
            }
            return Revolve(profile,2147483647,true);
        }
        static Surface Revolve(List<Vector2> profile,int inner,bool torus)
        {
            int stride=RadialSegments+1;int count=profile.Count*stride;
            var data=new Surface{vertices=new Vector3[count],normals=new Vector3[count],uv=new Vector2[count],triangles=new int[(profile.Count-1)*RadialSegments*6]};
            int t=0;
            for(int row=0;row<profile.Count;row++)
            {
                var point=profile[row];
                var derivative=profile[Mathf.Min(row+1,profile.Count-1)]-profile[Mathf.Max(row-1,0)];
                if(torus&&(row==0||row==profile.Count-1))derivative=profile[1]-profile[profile.Count-2];
                for(int column=0;column<=RadialSegments;column++)
                {
                    float u=column/(float)RadialSegments,angle=u*2*Mathf.PI+Mathf.PI;
                    float co=Mathf.Cos(angle),si=Mathf.Sin(angle);int i=row*stride+column;
                    data.vertices[i]=new Vector3(point.x*co,point.y-PivotHeight,point.x*si);
                    data.normals[i]=new Vector3(derivative.y*co,-derivative.x,derivative.y*si).normalized;
                    data.uv[i]=new Vector2(u,row>inner?.97f:Mathf.Clamp01((point.y-.10f)/1.40f));
                    if(row<profile.Count-1&&column<RadialSegments)
                    {
                        int a=i,b=i+1,c=i+stride,d=c+1;
                        data.triangles[t++]=a;data.triangles[t++]=c;data.triangles[t++]=b;
                        data.triangles[t++]=b;data.triangles[t++]=c;data.triangles[t++]=d;
                    }
                }
            }
            return data;
        }
        public static GameObject Visual(Transform parent,string name,Surface surface,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=surface.Mesh(name);
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }
        public static void Colliders(GalleryProp prop)
        {
            // Compound, concealed gameplay hull. The beautiful render surface is
            // independent from this deliberately coarser collision/shadow proxy.
            int inner;var profile=Profile(out inner);
            float[] levels={0,.14f,.34f,.56f,.82f,1.08f,1.31f,1.49f,1.76f,1.99f,2.10f};
            for(int band=0;band<levels.Length-1;band++)
            {
                float low=levels[band],high=levels[band+1],radius=0;
                for(int i=0;i<=inner;i++)if(profile[i].y>=low&&profile[i].y<=high)radius=Mathf.Max(radius,profile[i].x);
                for(int strip=0;strip<5;strip++)
                {
                    float z=(strip-2)*.4f*radius;
                    float width=2*Mathf.Sqrt(Mathf.Max(.001f,radius*radius-z*z));
                    var go=new GameObject("Ceramic hull "+band+"-"+strip);go.transform.SetParent(prop.transform,false);
                    go.transform.localPosition=new Vector3(0,(low+high)*.5f-PivotHeight,z);
                    go.transform.localScale=new Vector3(width,high-low,.4f*radius);
                    var box=go.AddComponent<BoxCollider>();
                    go.AddComponent<GalleryCaster>().shape=box;
                }
            }
        }
        public static Material Glaze(string name,Color color,Texture texture=null,float smoothness=.68f)
        {
            var shader=Shader.Find("Standard");
            if(shader==null)throw new InvalidOperationException("Standard shader is required for ceramic glaze");
            var material=new Material(shader){name=name,color=color,mainTexture=texture};
            material.SetFloat("_Metallic",0);material.SetFloat("_Glossiness",smoothness);
            material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.black);
            GalleryWorld.Materials.Add(material);return material;
        }
        public static void ExhibitLighting(Transform root)
        {
            RenderSettings.ambientLight=new Color(.19f,.21f,.24f);
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.24f,.27f,.31f));RenderSettings.ambientProbe=ambient;
            Light(root,"Porcelain warm key",new Vector3(32,-35,0),new Color(1,.93f,.83f),.82f);
            Light(root,"Porcelain cool fill",new Vector3(52,130,0),new Color(.77f,.86f,1),.25f);
        }
        static void Light(Transform root,string name,Vector3 angles,Color color,float intensity)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localRotation=Quaternion.Euler(angles.x,angles.y,angles.z);
            var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.shadows=LightShadows.None;
        }
        public static void Export(string directory)
        {
            Directory.CreateDirectory(directory);
            var builder=new StringBuilder("# Original curved porcelain vase, metres, Y up. Pivot at centre.\nmtllib porcelain-vase.mtl\n");
            int offset=0;
            Append(builder,Vase(),"Porcelain_body","porcelain",ref offset);
            Append(builder,Ring(.326f,2.055f,.006f),"Cobalt_lip_line","cobalt",ref offset);
            Append(builder,Ring(.301f,.059f,.004f),"Cobalt_foot_line","cobalt",ref offset);
            File.WriteAllText(Path.Combine(directory,"porcelain-vase.obj"),builder.ToString());
            File.WriteAllText(Path.Combine(directory,"porcelain-vase.mtl"),"newmtl porcelain\nKd 1 1 1\nKs 0.3 0.3 0.3\nNs 160\nmap_Kd porcelain-peony-wrap.png\n\nnewmtl cobalt\nKd 0.03 0.10 0.25\nKs 0.3 0.3 0.3\nNs 200\n");
        }
        static void Append(StringBuilder text,Surface data,string name,string material,ref int offset)
        {
            var culture=CultureInfo.InvariantCulture;text.Append("o ").Append(name).Append("\nusemtl ").Append(material).Append("\ns 1\n");
            foreach(var v in data.vertices)text.Append("v ").Append(v.x.ToString("R",culture)).Append(' ').Append(v.y.ToString("R",culture)).Append(' ').Append(v.z.ToString("R",culture)).Append('\n');
            foreach(var uv in data.uv)text.Append("vt ").Append(uv.x.ToString("R",culture)).Append(' ').Append(uv.y.ToString("R",culture)).Append('\n');
            foreach(var n in data.normals)text.Append("vn ").Append(n.x.ToString("R",culture)).Append(' ').Append(n.y.ToString("R",culture)).Append(' ').Append(n.z.ToString("R",culture)).Append('\n');
            for(int i=0;i<data.triangles.Length;i+=3)
            {
                text.Append("f");for(int k=0;k<3;k++){int index=data.triangles[i+k]+offset+1;text.Append(' ').Append(index).Append('/').Append(index).Append('/').Append(index);}text.Append('\n');
            }
            offset+=data.vertices.Length;
        }
    }
}
