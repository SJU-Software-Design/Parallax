using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    /// <summary>A reusable piece of the castle wall, with a real flat walking surface.</summary>
    public static class BeachCastleWallBuilder
    {
        public const float Length=3.2f, Width=1.4f;
        public const float DeckTop=.14f, BearingBottom=-.14f;
        public const float ClearPathWidth=1.10f;

        sealed class Shape
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<int> triangles=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                int start=vertices.Count;
                foreach(var point in new[]{a,b,c})
                {vertices.Add(point);uv.Add(new Vector2(point.x+point.z,point.y));}
                triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {Triangle(a,b,c);Triangle(a,c,d);}
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name};
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
                mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }

        static Material Material(string name,Color color)
        {
            var found=BeachWorld.Materials.Find(m=>m!=null&&m.name==name);
            if(found!=null)return found;
            var material=new Material(Shader.Find("Parallax/BeachSurface")){name=name,color=color};
            material.SetFloat("_Metallic",0);material.SetFloat("_Glossiness",.12f);
            BeachWorld.Materials.Add(material);return material;
        }
        static Material Sand=>Material("Castle restoration golden sand",new Color(.83f,.69f,.40f));
        static Material SandTop=>Material("Castle restoration sunlit sand",new Color(.93f,.80f,.50f));
        static Material CutSand=>Material("Castle restoration freshly cut sand",new Color(.97f,.84f,.55f));
        static Material MasonryJoint=>Material("Castle restoration recessed sand joints",new Color(.65f,.52f,.30f));
        static Material ShellIvory=>Material("Castle restoration shell ivory",new Color(.98f,.91f,.70f));
        static Material ShellTeal=>Material("Castle restoration shell teal",new Color(.055f,.43f,.43f));

        static GameObject MeshPart(string name,Transform parent,Mesh mesh,Material material,bool shadow=true)
        {
            var part=new GameObject(name);part.transform.SetParent(parent,false);
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=shadow?ShadowCastingMode.TwoSided:ShadowCastingMode.Off;
            return part;
        }
        static GameObject Box(string name,Transform root,Vector3 center,Vector3 size,Material material,bool solid=true,bool shadow=true)
        {return BeachWorld.Box(name,center,size,material,root,solid,shadow);}

        // Decorative stone arch relief hangs only beneath the outer side edges.
        // The slab, rather than an arch bounding box, bears on the real end shelves.
        static void ArchRelief(Transform root,float side)
        {
            const float outer=.70f,inner=.59f;
            float[] z={-1.27f,-1.08f,-.82f,-.45f,0,.45f,.82f,1.08f,1.27f};
            float[] bottom={-.38f,-.30f,-.23f,-.17f,-.145f,-.17f,-.23f,-.30f,-.38f};
            var shape=new Shape();
            for(int i=0;i<z.Length-1;i++)
            {
                float a=z[i],b=z[i+1],ya=bottom[i],yb=bottom[i+1];
                float x0=inner*side,x1=outer*side;
                shape.Quad(new Vector3(x1,-.12f,a),new Vector3(x1,-.12f,b),new Vector3(x1,yb,b),new Vector3(x1,ya,a));
                shape.Quad(new Vector3(x0,ya,a),new Vector3(x0,yb,b),new Vector3(x0,-.12f,b),new Vector3(x0,-.12f,a));
                shape.Quad(new Vector3(x0,ya,a),new Vector3(x1,ya,a),new Vector3(x1,yb,b),new Vector3(x0,yb,b));
            }
            shape.Quad(new Vector3(inner*side,-.12f,z[0]),new Vector3(outer*side,-.12f,z[0]),
                new Vector3(outer*side,bottom[0],z[0]),new Vector3(inner*side,bottom[0],z[0]));
            int last=z.Length-1;
            shape.Quad(new Vector3(outer*side,-.12f,z[last]),new Vector3(inner*side,-.12f,z[last]),
                new Vector3(inner*side,bottom[last],z[last]),new Vector3(outer*side,bottom[last],z[last]));
            string hand=side<0?"left":"right";
            MeshPart("Carved sand arch · "+hand,root,shape.Mesh("Castle restoration wall · "+hand+" open arch relief"),Sand);
        }

        static void Fan(Shape shape,float radius,Vector3 basePoint,int sections)
        {
            Vector3 previous=new Vector3(Mathf.Cos(Mathf.PI/9)*radius,Mathf.Sin(Mathf.PI/9)*radius,basePoint.z)+new Vector3(0,-.015f,0);
            for(int i=1;i<=sections;i++)
            {
                float angle=Mathf.Lerp(Mathf.PI/9,Mathf.PI*8/9,i/(float)sections);
                Vector3 next=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,basePoint.z)+new Vector3(0,-.015f,0);
                shape.Triangle(basePoint,previous,next);previous=next;
            }
        }

        /// <summary>
        /// Matching seashell mark for both the carried fragment and the fixed broken wall.
        /// Lies in local XY; local +Z faces out. Default mark is about .27 m across.
        /// </summary>
        public static GameObject CreateShellCrest(Transform parent,Vector3 localPosition,Quaternion localRotation,float scale=1)
        {
            var crest=new GameObject("Matching teal seashell · castle wall joint");
            crest.transform.SetParent(parent,false);crest.transform.localPosition=localPosition;
            crest.transform.localRotation=localRotation;crest.transform.localScale=Vector3.one*scale;
            var rim=new Shape();Fan(rim,.145f,new Vector3(0,-.105f,0),12);
            MeshPart("Ivory shell relief",crest.transform,rim.Mesh("Castle restoration matching shell · ivory rim"),ShellIvory,false);
            var face=new Shape();Fan(face,.121f,new Vector3(0,-.085f,.004f),12);
            MeshPart("Teal shell fan",crest.transform,face.Mesh("Castle restoration matching shell · teal face"),ShellTeal,false);
            var ribs=new Shape();
            for(int i=0;i<5;i++)
            {
                float angle=Mathf.Lerp(Mathf.PI*.20f,Mathf.PI*.80f,i/4f);
                Vector3 a=new Vector3(0,-.076f,.008f),b=new Vector3(Mathf.Cos(angle)*.111f,Mathf.Sin(angle)*.111f-.015f,.008f);
                Vector3 cross=new Vector3(-(b-a).y,(b-a).x,0).normalized*.0035f;
                ribs.Quad(a-cross,b-cross,b+cross,a+cross);
            }
            MeshPart("Five ivory shell ridges",crest.transform,ribs.Mesh("Castle restoration matching shell · radial ridges"),ShellIvory,false);
            return crest;
        }

        public static BeachProp Build(Transform root,BeachGame game)
        {
            var go=new GameObject("04 · 되찾은 모래성 성벽 조각");go.transform.SetParent(root,false);
            go.transform.localScale=Vector3.one*.45f;
            var prop=go.AddComponent<BeachProp>();
            prop.title="모래성 성벽 조각";
            prop.zone=3;prop.minScale=.35f;prop.maxScale=2.15f;
            prop.platform=true;prop.upright=true;prop.buoyant=false;

            // The root stays in the visible deck centre for comfortable hand scaling.
            // This collider is also the true walking surface and the end bearing.
            Box("Continuous solid sand wall walkway",go.transform,Vector3.zero,
                new Vector3(Width,DeckTop-BearingBottom,Length),Sand);
            Box("Smooth golden sand walking top",go.transform,new Vector3(0,DeckTop-.012f,0),
                new Vector3(Width,.024f,Length),SandTop,false,false);

            foreach(float side in new[]{-1f,1f})
            {
                Box("Low sand castle parapet",go.transform,new Vector3(.63f*side,.255f,0),
                    new Vector3(.14f,.23f,Length),Sand);
                Box("Light parapet coping",go.transform,new Vector3(.63f*side,.374f,0),
                    new Vector3(.16f,.025f,Length),SandTop,false);
                foreach(float z in new[]{-.92f,.92f})
                {
                    Box("Short crenellation block",go.transform,new Vector3(.63f*side,.46f,z),
                        new Vector3(.16f,.18f,.43f),Sand);
                    Box("Sunlit crenellation cap",go.transform,new Vector3(.63f*side,.552f,z),
                        new Vector3(.175f,.025f,.45f),SandTop,false);
                }
                // Shallow, visible masonry joints do not interrupt the walkable centre.
                foreach(float z in new[]{-.80f,0,.80f})
                    Box("Sand block vertical joint",go.transform,new Vector3(.704f*side,.25f,z),
                        new Vector3(.006f,.19f,.012f),MasonryJoint,false,false);
                ArchRelief(go.transform,side);
            }
            foreach(float end in new[]{-1f,1f})
            {
                // An exposed cut face repeats the material and shell seen at each socket.
                Box("Fresh broken castle wall end",go.transform,new Vector3(0,0,(Length*.5f+.004f)*end),
                    new Vector3(Width,.27f,.012f),CutSand,false,false);
                foreach(float x in new[]{-.49f,.49f})
                    Box("End stone seam",go.transform,new Vector3(x,-.002f,(Length*.5f+.012f)*end),
                        new Vector3(.016f,.23f,.008f),MasonryJoint,false,false);
                CreateShellCrest(go.transform,new Vector3(0,.016f,(Length*.5f+.018f)*end),
                    Quaternion.Euler(0,end>0?0:180,0),.87f);
            }
            // The source is displayed at eye level on the player's right. Keep
            // the matching mark readable before either end face comes into view.
            CreateShellCrest(go.transform,new Vector3(0,DeckTop+.005f,0),Quaternion.Euler(-90,0,0),1.2f);
            foreach(float side in new[]{-1f,1f})
                CreateShellCrest(go.transform,new Vector3(.713f*side,.25f,0),Quaternion.Euler(0,side*90,0),1);
            prop.Remember();return prop;
        }
    }
}

