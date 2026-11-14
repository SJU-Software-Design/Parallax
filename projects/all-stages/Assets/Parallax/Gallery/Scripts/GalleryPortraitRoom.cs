using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Wall-only visual fixtures: these never emit a Unity light or enter the
    // sunlight hazard model. The floor stays in architectural shade.
    public sealed class GalleryPortraitRoom : MonoBehaviour
    {
        public GalleryGame game;
        public Transform originalArtwork, installationArtwork, headOutline;
        public Renderer[] pictureDiffusers;
        MaterialPropertyBlock properties;
        float lastMood = -1;

        public static GalleryPortraitRoom Install(GalleryGame game, GalleryConceptArchitecture.Palette palette)
        {
            var existing=game.GetComponentInChildren<GalleryPortraitRoom>();
            if(existing!=null)return existing;
            var root=new GameObject("Portrait room · face references and safe wall fixtures");
            root.transform.SetParent(game.transform,false);
            var room=root.AddComponent<GalleryPortraitRoom>();room.game=game;
            room.originalArtwork=GalleryConceptArchitecture.FramedArtwork(root.transform,"MaeWestFace",
                "메이 웨스트의 얼굴","살바도르 달리 · 1934–35",
                new Vector3(-7.78f,3.215f,36),Quaternion.Euler(0,-90,0),3.2f,3.5f,palette);
            room.installationArtwork=GalleryConceptArchitecture.FramedArtwork(root.transform,"MaeWestRoom",
                "메이 웨스트의 방","달리 · 오스카르 투스케츠 / 설치 사진",
                new Vector3(7.78f,3.02f,36),Quaternion.Euler(0,90,0),3.8f,2.8f,palette);
            var outline=new GameObject("Portrait doorway · forehead cheek and jaw contour").transform;
            outline.SetParent(root.transform,false);outline.localPosition=new Vector3(0,0,43.15f);
            room.headOutline=outline;
            var shape=new[]{new Vector2(-1.97f,.06f),new Vector2(-2.17f,1.12f),new Vector2(-1.94f,2.72f),
                new Vector2(-2.34f,4.48f),new Vector2(-1.70f,5.72f),new Vector2(0,6.09f),
                new Vector2(1.70f,5.72f),new Vector2(2.34f,4.48f),new Vector2(1.94f,2.72f),
                new Vector2(2.17f,1.12f),new Vector2(1.97f,.06f)};
            Tube(outline,"Continuous limestone face outline",shape,.125f,.15f,palette.plaster,0);
            Tube(outline,"Fine bronze face outline",shape,.025f,.025f,palette.bronze,-.16f);
            room.pictureDiffusers=new[]{Fixture(room.originalArtwork,1.90f,palette),Fixture(room.installationArtwork,1.56f,palette)};
            room.SetMood(0);
            return room;
        }

        static Renderer Fixture(Transform painting,float height,GalleryConceptArchitecture.Palette palette)
        {
            GalleryConceptArchitecture.Block(painting,"Picture light · brass hood",new Vector3(0,height,-.24f),
                new Vector3(1.58f,.10f,.16f),palette.bronze,.025f);
            foreach(int side in new[]{-1,1})
                GalleryConceptArchitecture.Block(painting,"Picture light · wall bracket",new Vector3(side*.57f,height+.08f,-.10f),
                    new Vector3(.045f,.17f,.27f),palette.bronze,.015f);
            var material=new Material(Shader.Find("Parallax/MuseumSurface")){name="Portrait picture light · visual diffuser",color=Color.white};
            material.SetFloat("_ExhibitLight",.8f);material.SetFloat("_ReflectionStrength",0);
            GalleryWorld.Materials.Add(material);
            var diffuser=GalleryConceptArchitecture.Block(painting,"Wall-facing warm/red diffuser",new Vector3(0,height-.035f,-.26f),
                new Vector3(1.42f,.035f,.06f),material,.012f);
            diffuser.shadowCastingMode=ShadowCastingMode.Off;
            return diffuser;
        }

        static void Tube(Transform parent,string name,Vector2[] controls,float radius,float depth,Material material,float z)
        {
            const int subdivisions=10,sides=12;
            var points=new List<Vector2>();
            for(int segment=0;segment<controls.Length-1;segment++)
            for(int step=0;step<subdivisions;step++)
            {
                float t=(float)step/subdivisions,t2=t*t,t3=t2*t;
                Vector2 a=controls[Mathf.Max(0,segment-1)],b=controls[segment],c=controls[segment+1],d=controls[Mathf.Min(controls.Length-1,segment+2)];
                points.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));
            }
            points.Add(controls[controls.Length-1]);
            var vertices=new List<Vector3>();var triangles=new List<int>();var uvs=new List<Vector2>();
            for(int i=0;i<points.Count;i++)
            {
                Vector2 tangent=(points[Mathf.Min(points.Count-1,i+1)]-points[Mathf.Max(0,i-1)]).normalized;
                Vector2 normal=new Vector2(-tangent.y,tangent.x);
                for(int side=0;side<=sides;side++)
                {
                    float angle=side*Mathf.PI*2/sides;
                    Vector2 p=points[i]+normal*(Mathf.Cos(angle)*radius);
                    vertices.Add(new Vector3(p.x,p.y,z+Mathf.Sin(angle)*depth));
                    uvs.Add(new Vector2((float)side/sides,(float)i/subdivisions));
                    if(i>0&&side<sides)
                    {
                        int a=i*(sides+1)+side,b=a-sides-1;
                        triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});
                    }
                }
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uvs);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        public void SetMood(float red01)
        {
            red01=Mathf.Clamp01(red01);
            if(Mathf.Abs(red01-lastMood)<.001f)return;
            lastMood=red01;properties??=new MaterialPropertyBlock();
            Color tint=Color.Lerp(new Color(1,.74f,.38f),new Color(1,.025f,.015f),red01);
            properties.SetColor("_Color",tint);properties.SetColor("_EmissionColor",tint*.32f);
            if(pictureDiffusers!=null)foreach(var renderer in pictureDiffusers)if(renderer!=null)renderer.SetPropertyBlock(properties);
        }
        void LateUpdate(){SetMood(game!=null&&game.portraitGate!=null?game.portraitGate.MoodRed01:0);}
    }
}
