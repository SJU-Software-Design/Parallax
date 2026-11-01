using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    public static class BeachBoatBuilder
    {
        sealed class Shape
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<int> triangles=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,bool twoSided=false)
            {
                int start=vertices.Count;
                foreach(var p in new[]{a,b,c}){vertices.Add(p);uv.Add(new Vector2(p.x,p.z));}
                triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
                if(twoSided)Triangle(c-Vector3.up*.008f,b-Vector3.up*.008f,a-Vector3.up*.008f);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool twoSided=false)
            {Triangle(a,b,c,twoSided);Triangle(a,c,d,twoSided);}
            public void Rod(Vector3 a,Vector3 b,float radius)
            {
                Vector3 axis=(b-a).normalized;
                Vector3 across=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized*radius;
                Vector3 other=Vector3.Cross(axis,across).normalized*radius;
                const int sides=8;
                for(int i=0;i<sides;i++)
                {
                    float t=i*Mathf.PI*2/sides,n=(i+1)*Mathf.PI*2/sides;
                    Vector3 p=across*Mathf.Cos(t)+other*Mathf.Sin(t),q=across*Mathf.Cos(n)+other*Mathf.Sin(n);
                    Quad(a+p,a+q,b+q,b+p);
                    Triangle(a,a+q,a+p);Triangle(b,b+p,b+q);
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
                mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
        static Material Material(string name,Color color,float metal=0)
        {
            var found=BeachWorld.Materials.Find(m=>m!=null&&m.name==name);if(found!=null)return found;
            var material=new Material(Shader.Find("Parallax/BeachSurface")){name=name,color=color};
            material.SetFloat("_Metallic",metal);material.SetFloat("_Glossiness",metal>0?.30f:.15f);BeachWorld.Materials.Add(material);return material;
        }
        static GameObject MeshPart(string name,Transform parent,Mesh mesh,Material material)
        {
            var part=new GameObject(name);part.transform.SetParent(parent,false);
            part.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            return part;
        }
        static BoxCollider Collision(string name,Transform parent,Vector3 center,Vector3 size)
        {
            var part=new GameObject(name);part.transform.SetParent(parent,false);var collider=part.AddComponent<BoxCollider>();collider.center=center;collider.size=size;return collider;
        }
        static readonly Vector2[] Outline={
            new Vector2(-.65f,-1),new Vector2(.65f,-1),new Vector2(.65f,.60f),new Vector2(.42f,.90f),
            new Vector2(0,1.10f),new Vector2(-.42f,.90f),new Vector2(-.65f,.60f)
        };
        public static BeachBoat Build(Transform root,BeachGame game)
        {
            var hull=Material("Shadow boat lagoon hull",new Color(.055f,.44f,.48f));
            var navy=Material("Shadow boat navy trim",new Color(.045f,.16f,.22f));
            var wood=Material("Shadow boat timber deck",new Color(.64f,.44f,.25f));
            var canvas=Material("Shadow boat ivory canvas",new Color(.94f,.87f,.70f));
            var frame=Material("Shadow boat bronze frame",new Color(.19f,.25f,.24f),.45f);
            var coral=Material("Shadow boat coral buoy",new Color(.96f,.36f,.23f));
            var go=new GameObject("03 · 그림자에서 태어난 그늘 배");go.transform.SetParent(root,false);go.transform.position=new Vector3(9.7977f,.522f,51.6791f);go.transform.localScale=Vector3.one*.45f;
            // Keep the integrated roof below the fixed landing canopy, even
            // when the player turns the boat around before releasing it.
            var prop=go.AddComponent<BeachProp>();prop.title="그늘 배 · 승선 크기까지 확대 / 갑판에서 E 출항";prop.zone=2;prop.minScale=.30f;prop.maxScale=1.6f;prop.platform=true;prop.buoyant=true;prop.upright=true;
            var boat=go.AddComponent<BeachBoat>();boat.Prop=prop;boat.Game=game;

            var shell=new Shape();var deck=new Shape();var edge=new Shape();
            Vector3 topCenter=new Vector3(0,.12f,0),bottomCenter=new Vector3(0,-.16f,0);
            for(int i=0;i<Outline.Length;i++)
            {
                Vector2 a=Outline[i],b=Outline[(i+1)%Outline.Length];
                Vector3 ta=new Vector3(a.x,.12f,a.y),tb=new Vector3(b.x,.12f,b.y);
                Vector3 ba=new Vector3(a.x*.78f,-.16f,a.y*.90f),bb=new Vector3(b.x*.78f,-.16f,b.y*.90f);
                shell.Quad(ta,tb,bb,ba);shell.Triangle(bottomCenter,ba,bb);
                deck.Triangle(topCenter,tb,ta);
                edge.Rod(ta+Vector3.up*.045f,tb+Vector3.up*.045f,.033f);
            }
            MeshPart("Pointed teal hull",go.transform,shell.Mesh("Shadow boat · pointed hull"),hull);
            MeshPart("Open walkable timber deck",go.transform,deck.Mesh("Shadow boat · deck"),wood);
            MeshPart("Rounded navy gunwale",go.transform,edge.Mesh("Shadow boat · gunwale"),navy);
            for(int i=0;i<12;i++)
                BeachWorld.Box("Boat deck plank gap",new Vector3(0,.126f,-.93f+i*.133f),new Vector3(1.20f,.006f,.015f),navy,go.transform,false,false);
            boat.HullColliders=new[]{
                Collision("Boat broad hull and deck",go.transform,new Vector3(0,-.02f,-.125f),new Vector3(1.3f,.28f,1.75f)),
                Collision("Boat narrowing bow",go.transform,new Vector3(0,-.02f,.85f),new Vector3(.80f,.28f,.20f)),
                Collision("Boat bow tip",go.transform,new Vector3(0,-.02f,1.02f),new Vector3(.32f,.28f,.14f))
            };

            // The canopy is offset toward the actual sun. Its shadow, rather
            // than an invisible safety volume, covers the forward boarding mark.
            Vector3 roof=new Vector3(.48f,2.10f,-.44f);
            Vector2[] rim={new Vector2(-.90f,-.60f),new Vector2(.90f,-.60f),new Vector2(1.10f,-.48f),new Vector2(1.10f,.48f),new Vector2(.90f,.60f),new Vector2(-.90f,.60f),new Vector2(-1.10f,.48f),new Vector2(-1.10f,-.48f)};
            var cloth=new Shape();var binding=new Shape();var braces=new Shape();
            Vector3 peak=roof+Vector3.up*.16f;
            for(int i=0;i<rim.Length;i++)
            {
                Vector3 a=roof+new Vector3(rim[i].x,0,rim[i].y),b=roof+new Vector3(rim[(i+1)%rim.Length].x,0,rim[(i+1)%rim.Length].y);
                cloth.Triangle(peak,b,a,true);cloth.Quad(a,b,b-Vector3.up*.065f,a-Vector3.up*.065f,true);
                binding.Rod(a,b,.017f);braces.Rod(peak-Vector3.up*.035f,a-Vector3.up*.035f,.014f);
            }
            MeshPart("Integrated cream shade canopy",go.transform,cloth.Mesh("Shadow boat · real shade canvas"),canvas);
            MeshPart("Canvas stitched binding",go.transform,binding.Mesh("Shadow boat · canopy binding"),canvas);
            MeshPart("Exposed canopy ribs",go.transform,braces.Mesh("Shadow boat · canopy ribs"),frame);
            Collision("Boat canopy physical rim",go.transform,roof+Vector3.up*.025f,new Vector3(2.20f,.05f,1.20f));
            var supports=new Shape();
            foreach(float z in new[]{-.75f,.05f})
            {
                Vector3 bottom=new Vector3(.59f,.15f,z),top=new Vector3(.59f,2.07f,z);
                supports.Rod(bottom,top,.025f);
                Collision("Boat starboard canopy post",go.transform,(bottom+top)*.5f,new Vector3(.05f,top.y-bottom.y,.05f));
                supports.Rod(top,new Vector3(-.58f,2.07f,z),.022f);
            }
            supports.Rod(new Vector3(.59f,1.65f,-.75f),new Vector3(.59f,2.07f,-.30f),.018f);
            MeshPart("Bimini side supports",go.transform,supports.Mesh("Shadow boat · canopy supports"),frame);
            // Small nautical details retain a clear central standing area.
            var ring=new Shape();Vector3 ringCenter=new Vector3(-.66f,.045f,-.45f);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
                ring.Rod(ringCenter+new Vector3(0,Mathf.Sin(a)*.13f,Mathf.Cos(a)*.13f),ringCenter+new Vector3(0,Mathf.Sin(b)*.13f,Mathf.Cos(b)*.13f),.035f);
            }
            MeshPart("Portside coral life ring",go.transform,ring.Mesh("Shadow boat · life ring"),coral);
            BeachWorld.Box("Boat bow cleat",new Vector3(0,.18f,.91f),new Vector3(.19f,.07f,.05f),frame,go.transform,false);
            BeachWorld.Box("Boat shaded boarding mark",new Vector3(0,.128f,.40f),new Vector3(.48f,.012f,.40f),canvas,go.transform,false,false);
            BeachWorld.Box("Boat boarding mark inset",new Vector3(0,.136f,.40f),new Vector3(.35f,.006f,.27f),hull,go.transform,false,false);
            var ropeHost=new GameObject("Visible launch and arrival mooring rope");ropeHost.transform.SetParent(go.transform,false);
            var rope=ropeHost.AddComponent<LineRenderer>();rope.sharedMaterial=canvas;rope.startWidth=rope.endWidth=.025f;rope.positionCount=3;rope.useWorldSpace=true;rope.shadowCastingMode=ShadowCastingMode.Off;rope.enabled=false;boat.MooringRope=rope;
            prop.Remember();return boat;
        }
    }
}
