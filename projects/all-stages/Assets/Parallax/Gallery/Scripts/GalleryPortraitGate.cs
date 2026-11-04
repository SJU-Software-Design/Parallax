using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Explicitly ticked by GalleryGame. The portrait is a continuous two-second
    // observation test; once released, the curtain stays open until reset.
    public sealed class GalleryPortraitGate : MonoBehaviour
    {
        public const float HoldSeconds=2;
        public GalleryGame game;
        public GalleryConceptPerspective portrait;
        public Transform leftCurtain,rightCurtain;
        public BoxCollider barrier;
        public bool Opened;
        public float Progress;
        float opening;
        bool wasAtPoint,wasMatching;

        public bool CanUnlock=>game!=null&&game.view!=null&&portrait!=null&&portrait.Complete&&portrait.AtViewingPoint&&
            Vector3.Dot(game.view.transform.forward,portrait.ViewingDirection)>.93f;

        public void ResetGate()
        {
            Opened=false;Progress=opening=0;wasAtPoint=wasMatching=false;
            if(barrier!=null)barrier.enabled=true;
            portrait?.ResetEvaluation();PositionCurtains();Physics.SyncTransforms();
        }
        public void Tick(float dt)
        {
            if(game==null||portrait==null)return;
            portrait.Evaluate();dt=Mathf.Max(0,dt);
            if(Opened)
            {
                Progress=1;opening=Mathf.MoveTowards(opening,1,dt/1.35f);PositionCurtains();return;
            }
            bool matching=CanUnlock;
            if(portrait.AtViewingPoint&&!wasAtPoint&&game.active)
                game.Tell("눈·코·입을 하나의 얼굴로 맞추고, 작품을 내려놓은 뒤 2초 동안 바라보세요.",7);
            if(matching)
            {
                if(!wasMatching&&game.active)game.Tell("초상이 이어집니다. 시선을 2초 동안 유지하세요.",3);
                Progress=Mathf.Clamp01(Progress+dt/HoldSeconds);
                if(Progress>=1)
                {
                    Opened=true;if(barrier!=null)barrier.enabled=false;
                    opening=Mathf.MoveTowards(opening,1,dt/1.35f);PositionCurtains();
                    if(game.active)game.Tell("하나의 초상이 완성되었습니다. 열린 커튼 너머로 이동하세요.",7);
                }
            }
            else Progress=0;
            wasAtPoint=portrait.AtViewingPoint;wasMatching=matching;
        }
        void PositionCurtains()
        {
            float eased=Mathf.SmoothStep(0,1,opening),x=Mathf.Lerp(.92f,2.14f,eased),width=Mathf.Lerp(1,.26f,eased);
            if(leftCurtain!=null){leftCurtain.localPosition=new Vector3(-x,3.02f,-.008f);leftCurtain.localScale=new Vector3(width,1,1);}
            if(rightCurtain!=null){rightCurtain.localPosition=new Vector3(x,3.02f,.008f);rightCurtain.localScale=new Vector3(width,1,1);}
        }
        public void ApplyMaterial(Material material)
        {
            if(material==null)return;
            if(!GalleryWorld.Materials.Contains(material))GalleryWorld.Materials.Add(material);
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=material;
        }
        static Mesh FoldedVelvet(int side)
        {
            const int columns=72,rows=36;int stride=columns+1,faceCount=(rows+1)*stride;
            var vertices=new List<Vector3>(faceCount*2);var uv=new List<Vector2>(faceCount*2);var triangles=new List<int>();
            for(int face=0;face<2;face++)for(int row=0;row<=rows;row++)for(int column=0;column<=columns;column++)
            {
                float u=(float)column/columns,v=(float)row/rows;
                float pleat=u*Mathf.PI*14+side*.24f;
                float fold=(.76f*Mathf.Cos(pleat)+.24f*Mathf.Cos(pleat*2))*(.115f-.025f*v);
                float sway=.018f*Mathf.Sin(v*Mathf.PI)*Mathf.Sin(u*Mathf.PI*2);
                float hem=.025f*(1-v)*(.5f+.5f*Mathf.Cos(pleat));
                vertices.Add(new Vector3((u-.5f)*2+sway,(v-.5f)*6+hem,fold+(face==0?-.004f:.004f)));
                uv.Add(new Vector2(u*2,v*6));
            }
            for(int row=0;row<rows;row++)for(int column=0;column<columns;column++)
            {
                int a=row*stride+column,b=a+1,c=a+stride,d=c+1;
                triangles.AddRange(new[]{a,c,d,a,d,b,a+faceCount,b+faceCount,d+faceCount,a+faceCount,d+faceCount,c+faceCount});
            }
            // Close the 8mm cloth edge, keeping true outward-facing surfaces on
            // both sides instead of relying on a shader-specific Cull setting.
            for(int row=0;row<rows;row++)
            {
                Join(triangles,row*stride,(row+1)*stride,faceCount);
                Join(triangles,(row+1)*stride+columns,row*stride+columns,faceCount);
            }
            for(int column=0;column<columns;column++)
            {
                Join(triangles,rows*stride+column,rows*stride+column+1,faceCount);
                Join(triangles,column+1,column,faceCount);
            }
            var mesh=new Mesh{name="Double-sided weighted velvet pleats · "+(side<0?"left":"right")};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void Join(List<int> triangles,int a,int b,int back)
        {triangles.AddRange(new[]{a,a+back,b+back,a,b+back,b});}
        static Transform Panel(Transform parent,int side)
        {
            var go=new GameObject("Portrait gate moving velvet curtain · "+(side<0?"left":"right"));go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=FoldedVelvet(side);
            var renderer=go.AddComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            return go.transform;
        }
        public static GalleryPortraitGate Install(GalleryGame game)
        {
            var existing=game.GetComponentInChildren<GalleryPortraitGate>();if(existing!=null)return existing;
            var go=new GameObject("Portrait gallery · opening curtain gate");go.transform.SetParent(game.transform,false);go.transform.localPosition=new Vector3(0,0,43.5f);
            var gate=go.AddComponent<GalleryPortraitGate>();gate.game=game;gate.portrait=game.GetComponentInChildren<GalleryConceptPerspective>();
            gate.leftCurtain=Panel(go.transform,-1);gate.rightCurtain=Panel(go.transform,1);
            gate.barrier=go.AddComponent<BoxCollider>();gate.barrier.center=new Vector3(0,2.85f,0);gate.barrier.size=new Vector3(3.6f,5.7f,.24f);
            go.AddComponent<GalleryCaster>().shape=gate.barrier;
            var velvet=new Material(Shader.Find("Parallax/MuseumSurface")){name="Portrait gate · burgundy velvet",color=new Color(.22f,.035f,.038f)};
            GalleryMuseumMaterials.Apply(velvet,"Paper");velvet.SetFloat("_Roughness",.97f);velvet.SetFloat("_ExhibitLight",.06f);velvet.SetFloat("_ReflectionStrength",.025f);
            gate.ApplyMaterial(velvet);gate.ResetGate();return gate;
        }
    }
}
