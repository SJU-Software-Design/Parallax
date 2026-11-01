using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    // The aperture is a visual target only. It must not occlude the beam that
    // the gate measures or introduce a new source of protective shade.
    public sealed class BeachSolarReceiver : MonoBehaviour
    {
        [SerializeField] Transform target,numberAnchor,titleAnchor,statusAnchor;
        [SerializeField] Renderer face,statusLamp;
        [SerializeField] Renderer[] chargeSegments=new Renderer[0];
        [SerializeField] int number;
        public Transform Target => target==null?transform:target;
        public Renderer Face => face;
        MaterialPropertyBlock properties;
        readonly Color idle=new Color(.95f,.36f,.22f),receiving=new Color(.12f,.82f,.78f),complete=new Color(.42f,.93f,.66f);
        bool lit,unlocked;
        float charge;
        TextMesh statusText;
        Font font;
        Material textMaterial;

        public static BeachSolarReceiver Create(Transform parent,Vector3 worldPosition,int number)
        {
            var root=new GameObject("태양빛 수신기 "+number.ToString("00"));
            root.transform.SetParent(parent,false);root.transform.position=worldPosition;
            var receiver=root.AddComponent<BeachSolarReceiver>();receiver.number=number;
            receiver.target=Anchor(root.transform,"반사광 입사점",Vector3.zero);
            var cream=Material("Receiver cream",new Color(.97f,.93f,.80f));
            var coral=Material("Receiver coral",new Color(.93f,.34f,.23f));
            var teal=Material("Receiver teal",new Color(.05f,.39f,.42f));
            var navy=Material("Receiver inset",new Color(.035f,.13f,.18f));
            var gold=Material("Receiver sun",new Color(1,.76f,.28f));
            Part(root.transform,"넓은 모래 받침",PrimitiveType.Cube,new Vector3(0,-1.59f,.21f),new Vector3(1.2f,.12f,.65f),teal);
            Part(root.transform,"수신기 지지대",PrimitiveType.Cube,new Vector3(0,-1.16f,.24f),new Vector3(.19f,.84f,.22f),cream);
            Part(root.transform,"크림 프레임",PrimitiveType.Cube,new Vector3(0,0,.19f),new Vector3(1.82f,1.72f,.23f),cream);
            Part(root.transform,"태양광 패널",PrimitiveType.Cube,new Vector3(0,0,.085f),new Vector3(1.61f,1.51f,.055f),navy);
            Part(root.transform,"아래 테두리",PrimitiveType.Cube,new Vector3(0,-.84f,.105f),new Vector3(1.82f,.09f,.08f),coral);
            Part(root.transform,"수신기 번호판",PrimitiveType.Cube,new Vector3(0,1.10f,.16f),new Vector3(1.82f,.40f,.15f),teal);
            // Cylinder's circular face lies in the XY plane; its front surface
            // and the measured aperture share the exact world target position.
            receiver.face=Part(root.transform,"원형 수광부",PrimitiveType.Cylinder,new Vector3(0,0,.025f),new Vector3(.43f,.025f,.43f),gold);
            receiver.face.transform.localRotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<8;i++)
            {
                float angle=i*45,rad=angle*Mathf.Deg2Rad;
                var ray=Part(root.transform,"태양 문양",PrimitiveType.Cube,new Vector3(Mathf.Sin(rad)*.36f,Mathf.Cos(rad)*.36f,.045f),new Vector3(.065f,.16f,.04f),gold);
                ray.transform.localRotation=Quaternion.Euler(0,0,-angle);
            }
            receiver.chargeSegments=new Renderer[16];
            for(int i=0;i<receiver.chargeSegments.Length;i++)
            {
                float angle=i*360f/receiver.chargeSegments.Length,rad=angle*Mathf.Deg2Rad;
                var segment=Part(root.transform,"충전 링 "+(i+1),PrimitiveType.Cube,new Vector3(Mathf.Sin(rad)*.61f,Mathf.Cos(rad)*.61f,.035f),new Vector3(.16f,.055f,.035f),cream);
                segment.transform.localRotation=Quaternion.Euler(0,0,-angle);receiver.chargeSegments[i]=segment;
            }
            receiver.statusLamp=Part(root.transform,"수신 상태등",PrimitiveType.Sphere,new Vector3(-.69f,-.66f,.015f),Vector3.one*.105f,coral);
            receiver.numberAnchor=Anchor(root.transform,"번호",new Vector3(-.64f,1.10f,.065f));
            receiver.titleAnchor=Anchor(root.transform,"수신기 이름",new Vector3(.22f,1.10f,.065f));
            receiver.statusAnchor=Anchor(root.transform,"수신 상태",new Vector3(.06f,-.66f,.005f));
            receiver.SetState(false,false,0);return receiver;
        }

        static Transform Anchor(Transform parent,string name,Vector3 at)
        {
            var value=new GameObject(name).transform;value.SetParent(parent,false);value.localPosition=at;return value;
        }
        static Material Material(string name,Color color)
        {
            var value=new Material(Shader.Find("Parallax/BeachSurface")){name=name,color=color};
            value.SetFloat("_Glossiness",.12f);BeachWorld.Materials.Add(value);return value;
        }
        static Renderer Part(Transform parent,string name,PrimitiveType shape,Vector3 at,Vector3 size,Material material)
        {
            var value=GameObject.CreatePrimitive(shape);value.name=name;value.transform.SetParent(parent,false);
            value.transform.localPosition=at;value.transform.localScale=size;
            var collider=value.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            var renderer=value.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
        }

        public void SetState(bool lit,bool unlocked,float charge)
        {
            this.lit=lit;this.unlocked=unlocked;this.charge=Mathf.Clamp01(charge);
            Color state=unlocked?complete:lit?receiving:idle;
            Tint(face,state,.35f);Tint(statusLamp,state,.65f);
            int filled=unlocked?chargeSegments.Length:Mathf.FloorToInt(this.charge*chargeSegments.Length+.0001f);
            for(int i=0;i<chargeSegments.Length;i++)Tint(chargeSegments[i],i<filled?state:new Color(.16f,.31f,.32f),i<filled?.32f:0);
            if(statusText!=null)
            {
                string caption=unlocked?"연결 완료":lit?"빛 수신 중":"빛을 기다려요";
                if(statusText.text!=caption)statusText.text=caption;statusText.color=state;
            }
        }
        void Tint(Renderer renderer,Color color,float emission)
        {
            if(renderer==null)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.Clear();properties.SetColor("_Color",color);properties.SetColor("_EmissionColor",color*emission);renderer.SetPropertyBlock(properties);
        }
        void Start()
        {
            var game=FindFirstObjectByType<BeachGame>();if(game==null||game.font==null)return;font=game.font;
            textMaterial=new Material(Shader.Find("ShadeLink/WorldText")){mainTexture=font.material.mainTexture};
            Text(numberAnchor,number.ToString("00"),.036f,new Color(1,.88f,.57f));
            Text(titleAnchor,"태양빛 수신기",.016f,new Color(.98f,.96f,.86f));
            statusText=Text(statusAnchor,"빛을 기다려요",.019f,idle);
            Font.textureRebuilt+=RefreshFont;SetState(lit,unlocked,charge);
        }
        TextMesh Text(Transform anchor,string caption,float size,Color color)
        {
            var mesh=anchor.gameObject.AddComponent<TextMesh>();mesh.text=caption;mesh.font=font;mesh.fontSize=96;
            mesh.characterSize=size;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=color;
            var renderer=mesh.GetComponent<Renderer>();renderer.sharedMaterial=textMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return mesh;
        }
        void RefreshFont(Font changed) {if(changed==font&&textMaterial!=null)textMaterial.mainTexture=font.material.mainTexture;}
        void OnDestroy() {Font.textureRebuilt-=RefreshFont;if(textMaterial!=null)Destroy(textMaterial);}
    }
}
