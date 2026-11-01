using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    // Completion uses the same real mesh shadows as safety. Near-match release
    // assistance adjusts a pose; it never bypasses native settling or matching.
    public sealed class BeachShadowHarbor : MonoBehaviour
    {
        [Serializable] public sealed class Sample
        {
            public Vector2 point;
            public bool inside;
            public int region;
            public Renderer indicator;
        }

        public BeachGame game;
        public BeachBoat boat;
        public Transform target,statusAnchor;
        public Transform boatReleaseAnchor,dropAnchor;
        public Vector3 boardBoatScale=new Vector3(.08f,.75f,1.5f);
        public BeachProp[] pieces=new BeachProp[0];
        public bool placementAssistEnabled=true;
        public BoxCollider[] assistPlates=new BoxCollider[0];
        public Transform[] assistAnchors=new Transform[0];
        public Sample[] samples=new Sample[0];
        public Transform chargeFill;
        // Declared fixture poses for Editor validation only. Live matching never
        // reads them, and the player is free to find other matching placements.
        public Vector3[] solutionPositions=new Vector3[0];
        public Quaternion[] solutionRotations=new Quaternion[0];
        public float[] solutionScales=new float[0];
        public BeachProp[] Pieces=>pieces;
        public Vector3[] SolutionPositions=>solutionPositions;
        public Quaternion[] SolutionRotations=>solutionRotations;
        public float[] SolutionScales=>solutionScales;
        public bool Solved {get;private set;}
        public float Charge {get;private set;}
        public float Coverage {get;private set;}
        public float Clearance {get;private set;}
        public float HullCoverage {get;private set;}
        public float CanopyCoverage {get;private set;}
        public bool Materializing=>revealing&&Solved;
        public bool Dropping=>Materializing&&dropStarted;
        public int RevealCount {get;private set;}
        public int AssistedDrops {get;private set;}
        public bool Matching {get;private set;}
        const float ChargeSeconds=.85f,ShrinkSeconds=.65f;
        bool initialized,revealing,dropStarted;
        float revealTime;
        Vector3 revealFrom,revealDrop,revealScale;
        Quaternion revealRotation;
        Collider[] revealColliders;
        bool[] revealColliderStates;
        Material[][] revealMaterials;
        TextMesh statusText;
        Font font;
        Material textMaterial;
        MaterialPropertyBlock tint;
        string lastStatus;

        public bool TryAssistRelease(BeachProp piece)
        {
            if(!placementAssistEnabled||Solved||game==null||game.held!=piece||piece==null||piece.placement==null||!piece.placement.Held)return false;
            int index=Array.IndexOf(pieces,piece);
            if(index<0||index>=assistPlates.Length||index>=assistAnchors.Length)return false;
            return BeachShadowDropAssist.TryGetAlignment(piece,assistPlates[index],target,assistAnchors[index],out var alignment)
                &&game.TryApplyDropAlignment(piece,alignment);
        }
        public void NotifyAssistApplied(BeachProp piece)
        {
            if(Array.IndexOf(pieces,piece)>=0)AssistedDrops++;
        }

        public string Status
        {
            get
            {
                if(Solved)return Materializing?(dropStarted?"작은 배가 받침 위로 떨어집니다":"완성한 배가 보드에서 빠져나와 작아집니다"):
                    "보드 앞 받침의 작은 배를 직접 잡아 출항 부두로 가져가세요";
                foreach(var piece in pieces)
                    if(piece==null||piece.placement==null||!piece.placement.Ready)
                        return "두 조각을 내려놓고 안착시키세요 · 운반 중 그림자는 미리보기";
                return "선체 "+Mathf.RoundToInt(HullCoverage*100)+"%  ·  차양 "+Mathf.RoundToInt(CanopyCoverage*100)+
                    "%  ·  바깥의 빛 "+Mathf.RoundToInt(Clearance*100)+"%  ·  충전 "+Mathf.RoundToInt(Charge*100)+"%";
            }
        }

        public void InitializeOnGameReady()
        {
            if(initialized)return;initialized=true;
            if(boat!=null&&boat.Prop!=null)boat.Prop.gameObject.SetActive(false);
            if(game!=null&&game.lighting!=null)game.lighting.Rebuild();
            CreateStatusText();UpdateVisuals();
        }
        void Start(){if(!initialized)InitializeOnGameReady();}
        void CreateStatusText()
        {
            if(statusAnchor==null||game==null||game.font==null||statusText!=null)return;
            font=game.font;statusText=statusAnchor.GetComponent<TextMesh>();
            if(statusText==null)statusText=statusAnchor.gameObject.AddComponent<TextMesh>();
            statusText.font=font;statusText.fontSize=96;statusText.characterSize=.015f;
            statusText.anchor=TextAnchor.MiddleCenter;statusText.alignment=TextAlignment.Center;
            statusText.color=new Color(.98f,.95f,.80f);
            textMaterial=new Material(Shader.Find("ShadeLink/WorldText")){mainTexture=font.material.mainTexture};
            var renderer=statusText.GetComponent<Renderer>();renderer.sharedMaterial=textMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
            Font.textureRebuilt+=RefreshFont;
        }
        void RefreshFont(Font changed){if(changed==font&&textMaterial!=null)textMaterial.mainTexture=font.material.mainTexture;}

        public void Tick(float dt)
        {
            if(!initialized||game==null||game.lighting==null||dt<=0)return;
            if(Solved){TickReveal(dt);UpdateVisuals();return;}
            int positive=0,covered=0,negative=0,clear=0,hull=0,hullCovered=0,canopy=0,canopyCovered=0;
            var contributions=new int[pieces.Length];
            foreach(var sample in samples)
            {
                Vector3 point=target.TransformPoint(new Vector3(sample.point.x,sample.point.y,0));
                bool blocked=game.lighting.TryGetDirectShadowCaster(point,target.forward,out var caster);
                int piece=Array.IndexOf(pieces,caster);
                bool allowed=blocked&&piece>=0&&caster!=null&&caster.placement!=null&&caster.placement.Ready;
                if(sample.inside)
                {
                    positive++;if(allowed){covered++;contributions[piece]++;}
                    if(sample.region==0){hull++;if(allowed)hullCovered++;}
                    else {canopy++;if(allowed)canopyCovered++;}
                    SetIndicator(sample.indicator,allowed?new Color(.10f,.75f,.66f):new Color(.91f,.40f,.18f));
                }
                else
                {
                    negative++;if(!blocked)clear++;
                    SetIndicator(sample.indicator,!blocked?new Color(.97f,.86f,.54f):new Color(.95f,.22f,.12f));
                }
            }
            Coverage=positive==0?0:(float)covered/positive;Clearance=negative==0?0:(float)clear/negative;
            HullCoverage=hull==0?0:(float)hullCovered/hull;CanopyCoverage=canopy==0?0:(float)canopyCovered/canopy;
            bool ready=pieces.Length==2;
            for(int i=0;i<pieces.Length;i++)
                ready&=pieces[i]!=null&&pieces[i].placement!=null&&pieces[i].placement.Ready&&contributions[i]>=3;
            Matching=ready&&HullCoverage>=.84f&&CanopyCoverage>=.84f&&Coverage>=.86f&&Clearance>=.88f;
            Charge=Matching?Mathf.Min(1,Charge+dt/ChargeSeconds):Mathf.Max(0,Charge-dt*1.4f);
            if(Charge>=1)RevealBoat();
            UpdateVisuals();
        }
        void SetIndicator(Renderer renderer,Color color)
        {
            if(renderer==null)return;if(tint==null)tint=new MaterialPropertyBlock();
            tint.Clear();tint.SetColor("_Color",color);tint.SetColor("_EmissionColor",color*.10f);renderer.SetPropertyBlock(tint);
        }
        void UpdateVisuals()
        {
            CreateStatusText();string value=Status;
            if(statusText!=null&&lastStatus!=value){statusText.text=value.Replace("%  ·  바깥의 빛", "%\n바깥의 빛").Replace(" · 운반 중 그림자는 미리보기", "\n운반 중 그림자는 미리보기");lastStatus=value;}
            if(chargeFill!=null)
            {
                chargeFill.localScale=new Vector3(Mathf.Max(.001f,Charge),1,1);
                chargeFill.localPosition=new Vector3(-1.95f*(1-Charge),-.26f,-.075f);
            }
        }
        void RevealBoat()
        {
            Solved=true;Charge=1;Matching=true;RevealCount++;
            if(boat==null||boat.Prop==null)return;
            // The completed pieces become this boat, leaving no second hull
            // or support poles behind the emerging silhouette.
            foreach(var piece in pieces)if(piece!=null)piece.gameObject.SetActive(false);
            boat.ResetBoat();var prop=boat.Prop;
            revealScale=prop.homeScale;
            revealFrom=boatReleaseAnchor!=null?boatReleaseAnchor.position:target.TransformPoint(new Vector3(0,-.68f,.2f));
            revealDrop=dropAnchor!=null?dropAnchor.position:prop.homePosition+Vector3.up*.65f;
            revealRotation=boatReleaseAnchor!=null?boatReleaseAnchor.rotation:Quaternion.LookRotation(target.right,Vector3.up);
            revealColliders=prop.GetComponentsInChildren<Collider>(true);revealColliderStates=new bool[revealColliders.Length];
            for(int i=0;i<revealColliders.Length;i++){revealColliderStates[i]=revealColliders[i].enabled;revealColliders[i].enabled=false;}
            revealMaterials=new Material[prop.renderers.Length][];
            for(int i=0;i<prop.renderers.Length;i++)
            {
                var renderer=prop.renderers[i];if(renderer==null)continue;
                revealMaterials[i]=new Material[renderer.sharedMaterials.Length];
                for(int j=0;j<revealMaterials[i].Length;j++)revealMaterials[i][j]=prop.placement.OriginalMaterial(renderer,j);
            }
            prop.transform.SetPositionAndRotation(revealFrom,revealRotation);prop.transform.localScale=boardBoatScale;
            if(prop.body!=null){prop.body.isKinematic=true;prop.body.useGravity=false;prop.body.position=revealFrom;prop.body.rotation=revealRotation;}
            prop.gameObject.SetActive(true);revealTime=0;revealing=true;dropStarted=false;game.lighting.Rebuild();
            ShowRevealMaterials();
            game.Tell("그림자 배 완성 · 보드에서 떨어진 작은 배를 직접 잡아 출항 부두로 가져가세요.",9);
        }
        void TickReveal(float dt)
        {
            if(!Materializing||boat==null||boat.Prop==null)return;
            revealTime+=dt;var prop=boat.Prop;
            if(!dropStarted)
            {
                float t=Mathf.Clamp01(revealTime/ShrinkSeconds),s=t*t*(3-2*t);
                Vector3 position=Vector3.Lerp(revealFrom,revealDrop,s);
                Quaternion rotation=Quaternion.Slerp(revealRotation,prop.homeRotation,s);
                prop.transform.SetPositionAndRotation(position,rotation);prop.transform.localScale=Vector3.Lerp(boardBoatScale,revealScale,s);
                if(prop.body!=null){prop.body.position=position;prop.body.rotation=rotation;}
                if(t>=1)
                {
                    RestoreRevealColliders();prop.RefreshMass();Physics.SyncTransforms();
                    // The normal release lifecycle supplies gravity, collision
                    // sounds and native support settling on the receiving tray.
                    prop.Release();dropStarted=true;
                }
            }
            if(dropStarted&&(prop.placement.Ready||prop.placement.Held||revealTime>=ShrinkSeconds+3))
            {
                revealing=false;revealMaterials=null;game.lighting.Synchronize();
                game.Tell("배가 내려왔습니다 · 보드 앞 작은 배를 직접 잡아 출항 부두로 가져가세요.",7);
                return;
            }
            ShowRevealMaterials();
        }
        void ShowRevealMaterials()
        {
            // This is the emerging reward, not a carried placement preview.
            // Support and shadow eligibility still use the native Ready state.
            if(revealMaterials==null||boat==null||boat.Prop==null)return;
            for(int i=0;i<revealMaterials.Length;i++)
                if(revealMaterials[i]!=null&&boat.Prop.renderers[i]!=null)boat.Prop.renderers[i].sharedMaterials=revealMaterials[i];
        }
        void RestoreRevealColliders()
        {
            if(revealColliders!=null)
                for(int i=0;i<revealColliders.Length;i++)if(revealColliders[i]!=null)revealColliders[i].enabled=revealColliderStates[i];
            revealColliders=null;revealColliderStates=null;
        }
        public void Restore()
        {
            RestoreRevealColliders();Solved=Matching=revealing=dropStarted=false;
            Charge=Coverage=HullCoverage=CanopyCoverage=0;Clearance=1;AssistedDrops=0;revealTime=0;revealMaterials=null;
            foreach(var piece in pieces)
                if(piece!=null){piece.gameObject.SetActive(true);piece.placement?.ResetPlaced();}
            if(boat!=null&&boat.Prop!=null){boat.ResetBoat();boat.Prop.gameObject.SetActive(false);}
            if(initialized&&game!=null&&game.lighting!=null)game.lighting.Rebuild();
            UpdateVisuals();
        }
        void OnDestroy()
        {
            Font.textureRebuilt-=RefreshFont;
            if(textMaterial!=null)Destroy(textMaterial);
        }
    }
}
