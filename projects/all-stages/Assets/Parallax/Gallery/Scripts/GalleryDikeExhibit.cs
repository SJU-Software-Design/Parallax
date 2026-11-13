using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // The imported statue stays still. Its verified sword and the re-rigged
    // balance have independent transforms. No downloaded animation is implied.
    public sealed class GalleryDikeExhibit : MonoBehaviour
    {
        public const string Inscription = "저울 없는 검은 폭력이며,\n검 없는 저울은 법의 무력함이다";
        public const float FixedSwordLength = .78f;
        [Serializable] public sealed class Rig
        {
            public Vector3 bodyMin, bodyMax, beamPivot, swordCenter, swordGrip;
            public Quaternion swordRotation;
            public float swordOriginalLength;
            public bool independentSwordVerified;
        }
        [Serializable] public sealed class SwordVisualRig
        {
            public float gripY = .36f;
            public bool independentSwordVerified;
        }
        public Transform StatueRoot { get; private set; }
        public Transform SourceFrame { get; private set; }
        public bool Ready { get; private set; }
        public string AvailabilityReason { get; private set; }
        public float SourceScale { get; private set; }
        public float GaugeProgress { get; private set; }
        GalleryBottleBalanceArt.View art;
        Transform needle;
        readonly List<Renderer> progressDots = new List<Renderer>();
        MaterialPropertyBlock block;
        static readonly Color WarmGold = new Color(1f,.69f,.22f);

        public static GalleryDikeExhibit Install(GalleryBottleBalanceArt.View view)
        {
            var component = view.root.gameObject.AddComponent<GalleryDikeExhibit>();
            component.art = view; view.dikeExhibit = component;
            component.Build();
            return component;
        }
        void Build()
        {
            block = new MaterialPropertyBlock();
            var data = Resources.Load<TextAsset>("BottleBalance/Dike/Rig");
            var body = Resources.Load<GameObject>("BottleBalance/Dike/StatueBody");
            var sword = Resources.Load<GameObject>("BottleBalance/Dike/RewardSword");
            var conceptSword = Resources.Load<GameObject>("BottleBalance/Dike/ConceptSword");
            var conceptRigData = Resources.Load<TextAsset>("BottleBalance/Dike/ConceptSwordRig");
            SwordVisualRig conceptRig = conceptRigData == null ? null : JsonUtility.FromJson<SwordVisualRig>(conceptRigData.text);
            bool useConceptSword = conceptSword != null && conceptRig != null && conceptRig.independentSwordVerified;
            if(useConceptSword)sword=conceptSword;
            if(data==null||body==null||sword==null)
            { AvailabilityReason="정의의 여신상과 독립 검 원본이 준비되지 않았어요.";return; }
            Rig rig = JsonUtility.FromJson<Rig>(data.text);
            if(rig==null||!rig.independentSwordVerified||rig.bodyMax.y-rig.bodyMin.y<.1f)
            { AvailabilityReason="여신상 원본의 독립 검 검증 기록이 필요해요.";return; }

            art.beam.localPosition=Vector3.up*2.80f;
            art.panDrop=1.65f;
            art.panHalfSpan=1.0f;
            SourceScale=(2.80f-.28f)/Mathf.Max(.01f,rig.beamPivot.y-rig.bodyMin.y);
            SourceFrame=Group("Dike · common original-coordinate frame",art.scaleStation,Vector3.zero);
            SourceFrame.localScale=Vector3.one*SourceScale;
            SourceFrame.localPosition=art.beam.localPosition-rig.beamPivot*SourceScale;
            StatueRoot=Instantiate(body,SourceFrame,false).transform;
            StatueRoot.name="Dike · original static body, sword and scales removed";
            StatueRoot.localPosition=Vector3.zero;StatueRoot.localRotation=Quaternion.identity;
            StatueRoot.localScale=Vector3.one;
            RemoveImportedColliders(StatueRoot);
            var support=Resources.Load<GameObject>("BottleBalance/Dike/ScaleSuspension");
            if(support!=null){var fixedSupport=Instantiate(support,SourceFrame,false);fixedSupport.name="Dike · fixed hand suspension";}
            art.statueRenderers=StatueRoot.GetComponentsInChildren<Renderer>(true);
            foreach(var r in SourceFrame.GetComponentsInChildren<Renderer>(true))
            {r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}

            foreach(Transform child in art.scaleStation)
            {
                if(child.name=="Turned antique balance column"||child.name=="Balance central round dial"||
                   child.name=="Balance status light"||child.name=="Balance dial needle"||
                   child.name=="Balance title plaque"||child.name.StartsWith("Exhibition plaque · 균형"))
                    child.gameObject.SetActive(false);
                if(child.name=="Balance travertine plinth")
                {child.localScale=new Vector3(6.8f/4.6f,1,1);}
                if(child.name=="Balance dark bronze base reveal")
                    child.localScale=new Vector3(6.6f/4.45f,1,1);
            }
            // The statue's original lower body is solid without a collider
            // around the closed stone hand pinning its detachable sword.
            Bounds bodyBounds=WorldBounds(StatueRoot);
            var bodySolid=StatueRoot.gameObject.AddComponent<BoxCollider>();
            bodySolid.center=StatueRoot.InverseTransformPoint(new Vector3(bodyBounds.center.x,bodyBounds.min.y+.9f,bodyBounds.center.z));
            bodySolid.size=new Vector3(.66f,1.8f,.6f)/SourceScale;

            ReplaceBeam();ReplacePan(art.leftPan,"PanLeftBowl");ReplacePan(art.rightPan,"PanRightBowl");
            BuildGauge();
            art.fixedSculpture.gameObject.SetActive(true);
            foreach(Transform child in art.fixedSculpture)child.gameObject.SetActive(false);
            var tiny=Instantiate(sword,art.fixedSculpture,false).transform;
            tiny.name="Fixed ceremonial dagger · independent sword miniature";
            FitSword(tiny,FixedSwordLength);
            tiny.localRotation=Quaternion.Euler(0,0,90);
            tiny.localPosition=Vector3.zero;
            Bounds daggerBounds=LocalBounds(tiny);
            float daggerHeight=.02f-daggerBounds.min.y;
            tiny.localPosition=Vector3.up*daggerHeight;
            RemoveImportedColliders(tiny);
            float saddleHeight=Mathf.Max(.04f,daggerHeight-.018f);
            Cube("Small fixed exhibition pin",art.fixedSculpture,new Vector3(0,saddleHeight*.5f,0),new Vector3(.12f,saddleHeight,.13f),art.darkBrass,false);

            art.rewardHandAnchor=Group("Dike · verified sword grip anchor",art.root,Vector3.zero);
            // A vertical held blade clears the neighbouring weighing pan and
            // drops onto its own tray; the source's diagonal resting blade
            // would begin by leaning on the statue base / the enlarged pan.
            art.rewardHandAnchor.SetPositionAndRotation(SourceFrame.TransformPoint(rig.swordGrip),SourceFrame.rotation);
            var swordRoot=Group("Dike · independently releasable original sword",art.rewardHandAnchor,Vector3.zero);
            var swordVisual=Instantiate(sword,swordRoot,false).transform;
            swordVisual.name=useConceptSword?"Dike · concept-matched ceremonial sword":"Kenny Maguire · detached sword handle and blade";
            FitSword(swordVisual,1.15f);
            // Keep the grip in the stone hand, shortening only the reward sword
            // so its point is suspended clear of the original statue's base.
            swordVisual.localPosition+=Vector3.down*(useConceptSword?conceptRig.gripY*1.15f:.39f);
            art.verifiedRewardSword=swordRoot;
            art.SwordReleaseVerified=true;art.SwordReleaseAvailabilityReason=null;
            Vector3 swordCentre=WorldBounds(swordRoot).center;
            Vector3 trayAt=new Vector3(swordCentre.x,.40f,swordCentre.z-.12f);
            art.rewardCatchPad=Group("Dike · broad low sword recovery tray",art.root,trayAt);
            Cube("Recovery tray floor",art.rewardCatchPad,new Vector3(0,-.075f,0),new Vector3(1.85f,.15f,1.70f),art.stone,true);
            foreach(float x in new[]{-.95f,.95f})Cube("Recovery tray side",art.rewardCatchPad,new Vector3(x,.025f,0),new Vector3(.10f,.20f,1.85f),art.stone,true);
            foreach(float z in new[]{-.90f,.90f})Cube("Recovery tray end",art.rewardCatchPad,new Vector3(0,.025f,z),new Vector3(1.85f,.20f,.10f),art.stone,true);
            if(!BuildSocket())return;
            BuildLabels();
            Ready=true;
            UpdateBalance(12,0,false);
        }
        void ReplaceBeam()
        {
            var model=Resources.Load<GameObject>("BottleBalance/Dike/ScaleBeam");
            if(model==null)return;
            foreach(Transform child in art.beam)child.gameObject.SetActive(false);
            var visual=Group("Dike · re-rigged ornate original beam",art.beam,Vector3.zero);
            var original=Instantiate(model,visual,false).transform;
            original.localRotation=Quaternion.Euler(0,-40,0);
            Fit(visual,new Vector3(2.60f,.27f,.23f));
        }
        void ReplacePan(Transform pan,string resource)
        {
            var model=Resources.Load<GameObject>("BottleBalance/Dike/"+resource);
            foreach(Transform child in pan)child.gameObject.SetActive(false);
            // Keep a continuous thin inner dish beneath the separately cut
            // imported ornament, so a grazing view never sees through seams.
            var inner=pan.Find("Hammered brass weighing dish");
            if(inner!=null){inner.gameObject.SetActive(true);inner.localScale=Vector3.one*.70f;inner.localPosition=Vector3.down*.10f;}
            if(model!=null)
            {
                var visual=Instantiate(model,pan,false).transform;
                Fit(visual,new Vector3(.98f,.18f,.98f));visual.localPosition+=Vector3.down*.09f;
            }
            else Cube("Weighing pan solid surface",pan,new Vector3(0,-.06f,0),new Vector3(1.25f,.12f,1.25f),art.brass,false);
            var support=pan.GetComponent<BoxCollider>();if(support!=null)support.size=new Vector3(.90f,.10f,.88f);
            Mesh link=RingMesh();
            foreach(float angle in new[]{30f,150f,270f})
            {
                float rad=angle*Mathf.Deg2Rad;
                Vector3 bottom=new Vector3(Mathf.Cos(rad)*.41f,.015f,Mathf.Sin(rad)*.41f);
                Vector3 top=Vector3.up*art.panDrop;
                Vector3 direction=(top-bottom).normalized;
                int count=Mathf.CeilToInt(Vector3.Distance(bottom,top)/.045f);
                for(int i=0;i<=count;i++)
                {
                    var go=new GameObject("Dike · interlocked suspension link");go.transform.SetParent(pan,false);
                    go.transform.localPosition=Vector3.Lerp(bottom,top,(float)i/count);
                    go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,direction)*Quaternion.Euler(0,i%2*90,0);
                    go.AddComponent<MeshFilter>().sharedMesh=link;
                    var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=art.brass;
                    renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                }
            }
        }
        void BuildGauge()
        {
            var gauge=Group("Dike · fixed balance alignment gauge",art.scaleStation,new Vector3(0,2.80f,-.30f));
            Cube("Small engraved alignment plate",gauge,Vector3.zero,new Vector3(.38f,.30f,.025f),art.darkBrass,false);
            Cube("Fixed central reference notch",gauge,new Vector3(0,.045f,-.025f),new Vector3(.012f,.14f,.012f),art.brass,false);
            needle=Group("Dike · live balance needle",gauge,new Vector3(0,.035f,-.045f));
            Cube("Moving balance needle",needle,new Vector3(0,.035f,0),new Vector3(.014f,.11f,.014f),art.brass,false);
            for(int i=0;i<3;i++)
            {
                var dot=GameObject.CreatePrimitive(PrimitiveType.Sphere);dot.name="Balance dwell indicator "+(i+1);
                dot.transform.SetParent(gauge,false);dot.transform.localPosition=new Vector3((i-1)*.08f,-.075f,-.04f);
                dot.transform.localScale=Vector3.one*.035f;RemoveImportedColliders(dot.transform);
                dot.GetComponent<Renderer>().sharedMaterial=art.brass;progressDots.Add(dot.GetComponent<Renderer>());
            }
        }
        bool BuildSocket()
        {
            var source=Resources.Load<GameObject>("BottleBalance/Dike/VersaillesPedestalP02");
            if(source==null)
            {
                AvailabilityReason="P02 대리석 받침 원본 연결이 필요해요";
                art.SwordReleaseVerified=false;art.SwordReleaseAvailabilityReason=AvailabilityReason;return false;
            }
            var station=Group("Centre · P02 Versailles marble sword receiver",art.root,GalleryDikeGalleryLayout.ReceiverStation);
            var model=Instantiate(source,station,false).transform;model.name="P02 · actual Treleau Versailles pedestal body";
            RemoveImportedColliders(model);
            var mesh=model.GetComponent<MeshFilter>().sharedMesh;
            var solid=model.gameObject.AddComponent<MeshCollider>();solid.sharedMesh=mesh;
            const float top=1.20f,floor=.60f;
            var plate=Group("Centre · custom hollow brass blade slot",station,new Vector3(0,top+.008f,0));
            // Four separate rails leave a genuine visible opening, rather than
            // drawing a black line on an otherwise solid bronze plate.
            foreach(float x in new[]{-.11f,.11f})Cube("Blade slot side rail",plate,new Vector3(x,0,0),new Vector3(.12f,.026f,.42f),art.brass,true);
            foreach(float z in new[]{-.18f,.18f})Cube("Blade slot end rail",plate,new Vector3(0,0,z),new Vector3(.10f,.026f,.06f),art.brass,true);
            Cube("Slot cavity floor",station,new Vector3(0,floor,0),new Vector3(.11f,.015f,.31f),art.darkBrass,true);
            foreach(float x in new[]{-.058f,.058f})Cube("Slot recessed side liner",station,new Vector3(x,(top+floor)*.5f,0),new Vector3(.008f,top-floor,.31f),art.darkBrass,true);
            foreach(float z in new[]{-.158f,.158f})Cube("Slot recessed end liner",station,new Vector3(0,(top+floor)*.5f,z),new Vector3(.11f,top-floor,.008f),art.darkBrass,true);
            foreach(float x in new[]{-.13f,.13f})foreach(float z in new[]{-.16f,.16f})
            {
                var screw=GameObject.CreatePrimitive(PrimitiveType.Cylinder);screw.name="Receiver brass countersunk fastener";
                screw.transform.SetParent(plate,false);screw.transform.localPosition=new Vector3(x,.016f,z);
                screw.transform.localScale=new Vector3(.022f,.003f,.022f);RemoveImportedColliders(screw.transform);
                screw.GetComponent<Renderer>().sharedMaterial=art.darkBrass;
            }
            var fill=Group("Receiver · soft concealed exhibit fill",station,new Vector3(0,1.4f,-.3f)).gameObject.AddComponent<Light>();
            fill.type=LightType.Point;fill.range=3.2f;fill.intensity=.55f;fill.color=new Color(1,.84f,.65f);fill.shadows=LightShadows.None;
            art.swordSocketAnchor=Group("Centre · explicit sword insertion anchor",station,new Vector3(0,top+.068f,0));
            Cube("Central receiver · brass instruction plate",station,new Vector3(0,.18f,-.41f),new Vector3(.72f,.18f,.025f),art.darkBrass,false);
            Text("검을 꽂아 봉인 해제",station,new Vector3(0,.18f,-.43f),.09f);
            return true;
        }
        void BuildLabels()
        {
            Cube("Dike · engraved brass exhibition plaque",art.scaleStation,new Vector3(1,.13f,-.923f),new Vector3(3.7f,.23f,.025f),art.darkBrass,false);
            Text(Inscription,art.scaleStation,new Vector3(1f,.13f,-.942f),.115f);
            Text(Inscription,art.root,new Vector3(0,6.08f,GalleryBottleBalanceLayout.EndZ-.70f),.32f);
            Cube("Dike · exhibit name block",art.scaleStation,new Vector3(1,.38f,-.80f),new Vector3(1.6f,.24f,.32f),art.stone,false);
            Text("디케의 저울",art.scaleStation,new Vector3(1,.38f,-.969f),.14f);
        }
        public void UpdateBalance(float degrees,float progress,bool solved)
        {
            GaugeProgress=solved?1:Mathf.Clamp01(progress);
            if(needle!=null)needle.localRotation=Quaternion.Euler(0,0,-degrees);
            for(int i=0;i<progressDots.Count;i++)
            {
                bool lit=solved||progress>=(i+1)/3f;
                block.Clear();block.SetColor("_Color",lit?WarmGold:new Color(.23f,.15f,.07f));
                block.SetColor("_EmissionColor",lit?WarmGold*.75f:Color.black);progressDots[i].SetPropertyBlock(block);
            }
        }
        static Transform Group(string name,Transform parent,Vector3 at)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;return go.transform;}
        static void Cube(string name,Transform parent,Vector3 at,Vector3 size,Material material,bool solid)
        {
            var r=GalleryConceptArchitecture.Block(parent,name,at,size,material,.012f);
            r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
            if(solid)r.gameObject.AddComponent<BoxCollider>().size=size;
        }
        static void Text(string text,Transform parent,Vector3 at,float size)
        {var t=Group("Dike inscription",parent,at);var label=t.gameObject.AddComponent<GalleryLabel>();label.caption=text;label.size=size;label.color=new Color(.34f,.23f,.12f);}
        public static Bounds WorldBounds(Transform root)
        {Bounds b=new Bounds(root.position,Vector3.zero);bool first=true;foreach(var r in root.GetComponentsInChildren<Renderer>(true)){if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}return b;}
        static void Fit(Transform visual,Vector3 target)
        {
            visual.localPosition=Vector3.zero;visual.localScale=Vector3.one;
            Bounds b=LocalBounds(visual);Vector3 f=new Vector3(target.x/Mathf.Max(.00001f,b.size.x),target.y/Mathf.Max(.00001f,b.size.y),target.z/Mathf.Max(.00001f,b.size.z));
            visual.localScale=f;visual.localPosition=-Vector3.Scale(b.center,f);
        }
        static void FitSword(Transform visual,float length)
        {
            visual.localPosition=Vector3.zero;visual.localScale=Vector3.one;
            Bounds b=LocalBounds(visual);
            float scale=length/Mathf.Max(.00001f,b.size.y);
            visual.localScale=Vector3.one*scale;visual.localPosition=-b.center*scale;
        }
        static Bounds LocalBounds(Transform root)
        {
            bool first=true;Bounds result=new Bounds();
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {if(mf.sharedMesh==null)continue;Bounds b=mf.sharedMesh.bounds;for(int i=0;i<8;i++){Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));p=root.parent.InverseTransformPoint(mf.transform.TransformPoint(p));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}}
            return result;
        }
        static void RemoveImportedColliders(Transform root)
        {foreach(var c in root.GetComponentsInChildren<Collider>(true)){c.enabled=false;if(Application.isPlaying)Destroy(c);else DestroyImmediate(c);}}
        static Mesh RingMesh()
        {
            const int steps=12,sides=5;var v=new Vector3[(steps+1)*(sides+1)];var n=new Vector3[v.Length];var t=new int[steps*sides*6];int p=0;
            for(int i=0;i<=steps;i++)for(int j=0;j<=sides;j++){float a=i*Mathf.PI*2/steps,b=j*Mathf.PI*2/sides;Vector3 normal=new Vector3(Mathf.Cos(a)*Mathf.Cos(b),Mathf.Sin(a)*Mathf.Cos(b),Mathf.Sin(b));int k=i*(sides+1)+j;v[k]=new Vector3(Mathf.Cos(a)*.026f,Mathf.Sin(a)*.034f,0)+normal*.005f;n[k]=normal;}
            for(int i=0;i<steps;i++)for(int j=0;j<sides;j++){int a=i*(sides+1)+j,b=a+sides+1;t[p++]=a;t[p++]=b;t[p++]=a+1;t[p++]=a+1;t[p++]=b;t[p++]=b+1;}
            var m=new Mesh{name="Dike · fine interlocking chain link"};m.vertices=v;m.normals=n;m.triangles=t;m.RecalculateBounds();return m;
        }
    }
}
