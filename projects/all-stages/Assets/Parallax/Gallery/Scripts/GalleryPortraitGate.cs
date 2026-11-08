using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Explicitly ticked by GalleryGame. A readable face and a short look release
    // the curtain; small observation jitter is not a reason to start again.
    public sealed class GalleryPortraitGate : MonoBehaviour
    {
        public const float HoldSeconds=1;
        public const float ObservationGraceSeconds=.35f;
        public const float AbsorbSeconds=2.5f,RedSeconds=2f,SmileSeconds=3f,OpenSeconds=4.5f;
        public const float TotalPerformanceSeconds=AbsorbSeconds+RedSeconds+SmileSeconds+OpenSeconds;
        public enum RevealStage { Waiting, Absorbing, Reddening, Smiling, Opening, Open }
        public GalleryGame game;
        public GalleryConceptPerspective portrait;
        public Transform leftCurtain,rightCurtain;
        public BoxCollider barrier;
        public bool Opened;
        public float Progress;
        public GalleryPortraitPerformance performance;
        public RevealStage Stage {get;private set;}
        public bool Performing=>Stage!=RevealStage.Waiting&&Stage!=RevealStage.Open;
        public bool Absorbed=>Stage==RevealStage.Reddening||Stage==RevealStage.Smiling||Stage==RevealStage.Opening||Stage==RevealStage.Open;
        public float MoodRed01 {get;private set;}
        public float Smile01 {get;private set;}
        public float Opening01=>Mathf.SmoothStep(0,1,opening);
        public float RevealProgress=>Stage==RevealStage.Waiting?Progress/(HoldSeconds+TotalPerformanceSeconds):
            Mathf.Clamp01((HoldSeconds+performanceTime)/(HoldSeconds+TotalPerformanceSeconds));
        public float PerformanceElapsed=>performanceTime;
        float opening,missedTime,performanceTime;
        GalleryConceptLighting conceptLighting;
        bool wasAtPoint,wasMatching;

        public bool CanUnlock=>Stage==RevealStage.Waiting&&game!=null&&game.view!=null&&portrait!=null&&portrait.Complete&&portrait.CanObserve;
        public string Status=>Opened?"커튼이 열렸어요. 다음 전시실로 이동하세요.":
            Stage==RevealStage.Absorbing?"완성된 초상이 커튼에 스며들고 있어요.":
            Stage==RevealStage.Reddening?"초상이 커튼에 남았어요. 조명이 천천히 바뀌고 있어요.":
            Stage==RevealStage.Smiling?"초상이 천천히 미소 짓고 있어요.":
            Stage==RevealStage.Opening?"웃는 커튼이 천천히 열리고 있어요.":
            portrait==null?"눈·코·입을 하나의 얼굴로 이어 보세요.":portrait.Status;

        public void ResetGate()
        {
            if(performance!=null)performance.ResetPerformance();
            Stage=RevealStage.Waiting;Opened=false;Progress=opening=missedTime=performanceTime=0;wasAtPoint=wasMatching=false;
            MoodRed01=Smile01=0;SetMood(0);
            if(barrier!=null)barrier.enabled=true;
            portrait?.ResetEvaluation();PositionCurtains();Physics.SyncTransforms();
        }
        public void Tick(float dt)
        {
            if(game==null||portrait==null)return;
            dt=Mathf.Max(0,dt);
            if(Stage!=RevealStage.Waiting)
            {
                TickPerformance(dt);return;
            }
            portrait.Evaluate();
            bool matching=CanUnlock;
            if(portrait.AtViewingPoint&&!wasAtPoint&&game.active&&game.checkpoint==2)
                game.Tell("눈·코·입이 얼굴로 보이면, 작품을 내려놓고 잠깐 바라봐 주세요.",7);
            if(matching)
            {
                missedTime=0;
                if(!wasMatching&&game.active&&Progress<=.001f)game.Tell("얼굴이 이어졌어요. 잠깐 바라봐 주세요.",3);
                Progress=Mathf.Clamp01(Progress+dt/HoldSeconds);
                if(Progress>=1)
                {
                    if(performance==null)performance=gameObject.AddComponent<GalleryPortraitPerformance>();
                    performance.Begin(this,portrait);
                    Stage=RevealStage.Absorbing;performanceTime=0;
                    if(game.active)game.Tell("하나의 초상이 완성되었습니다. 커튼을 바라봐 주세요.",5);
                }
            }
            else if(portrait.AnyPieceHeld)
            {
                Progress=0;missedTime=0;
            }
            else
            {
                missedTime+=dt;
                if(missedTime>ObservationGraceSeconds)Progress=Mathf.MoveTowards(Progress,0,dt/.7f);
            }
            wasAtPoint=portrait.AtViewingPoint;wasMatching=matching;
        }
        void TickPerformance(float dt)
        {
            bool alreadyOpen=Opened;
            performanceTime=Mathf.Min(TotalPerformanceSeconds,performanceTime+dt);
            float absorbed=Mathf.Clamp01(performanceTime/AbsorbSeconds);
            if(performance!=null)performance.SetAbsorption(absorbed);
            if(performanceTime<AbsorbSeconds)Stage=RevealStage.Absorbing;
            else if(performanceTime<AbsorbSeconds+RedSeconds)Stage=RevealStage.Reddening;
            else if(performanceTime<AbsorbSeconds+RedSeconds+SmileSeconds)Stage=RevealStage.Smiling;
            else if(performanceTime<TotalPerformanceSeconds)Stage=RevealStage.Opening;
            else Stage=RevealStage.Open;
            MoodRed01=Mathf.SmoothStep(0,1,Mathf.Clamp01((performanceTime-AbsorbSeconds)/RedSeconds));
            Smile01=Mathf.SmoothStep(0,1,Mathf.Clamp01((performanceTime-AbsorbSeconds-RedSeconds)/SmileSeconds));
            opening=Mathf.Clamp01((performanceTime-AbsorbSeconds-RedSeconds-SmileSeconds)/OpenSeconds);
            SetMood(MoodRed01);PositionCurtains();
            if(performance!=null)performance.SetExpression(Smile01,Opening01,MoodRed01);
            Progress=1;Opened=Stage==RevealStage.Open;
            if(barrier!=null)barrier.enabled=!Opened;
            if(Opened&&!alreadyOpen&&game.active)game.Tell("초상이 길을 열었어요. 열린 커튼 사이로 이동하세요.",7);
        }
        void SetMood(float red)
        {
            if(conceptLighting==null&&game!=null)conceptLighting=game.GetComponent<GalleryConceptLighting>();
            if(conceptLighting!=null)conceptLighting.SetPortraitMood(red);
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
            if(leftCurtain!=null)leftCurtain.GetComponent<MeshRenderer>().sharedMaterial=material;
            if(rightCurtain!=null)rightCurtain.GetComponent<MeshRenderer>().sharedMaterial=material;
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
            gate.performance=go.AddComponent<GalleryPortraitPerformance>();
            gate.leftCurtain=Panel(go.transform,-1);gate.rightCurtain=Panel(go.transform,1);
            gate.barrier=go.AddComponent<BoxCollider>();gate.barrier.center=new Vector3(0,2.85f,0);gate.barrier.size=new Vector3(3.6f,5.7f,.24f);
            go.AddComponent<GalleryCaster>().shape=gate.barrier;
            var velvet=new Material(Shader.Find("Parallax/MuseumSurface")){name="Portrait gate · burgundy velvet",color=new Color(.22f,.035f,.038f)};
            GalleryMuseumMaterials.Apply(velvet,"Paper");velvet.SetFloat("_Roughness",.97f);velvet.SetFloat("_ExhibitLight",.06f);velvet.SetFloat("_ReflectionStrength",.025f);
            gate.ApplyMaterial(velvet);gate.ResetGate();return gate;
        }
    }
}
