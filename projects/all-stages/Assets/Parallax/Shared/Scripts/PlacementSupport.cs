using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink
{
    // A carried or unsupported prop previews its result. Only a placed, supported
    // prop participates in safety and player-platform collision.
    public sealed class PlacementSupport : MonoBehaviour
    {
        public const float SettleSeconds=.35f;
        public bool Held {get;private set;}
        public bool Ready {get;private set;}
        public bool Supported {get;private set;}
        public float Progress=>Mathf.Clamp01(stableTime/SettleSeconds);
        public Func<bool> WaterSupport;
        public string Status=>Held?"운반 중 · 빗금은 예상 그늘 / 안전하지 않음":Ready?"설치 완료 · 실제 그늘과 발판 활성":Supported?"안착 확인 중 · "+Mathf.RoundToInt(Progress*100)+"%":"지지가 필요합니다 · 아직 안전한 그늘이 아님";
        public Renderer[] Visuals {get;private set;}
        Rigidbody body;Collider[] shapes;CharacterController player;
        Material[][] originals,ghosts;ShadowCastingMode[] shadowModes;
        float stableTime;bool initialized,visualReady,homeAttached,attached;
        Vector3 homePosition,homeScale;Quaternion homeRotation;
        Collider supportCollider;PlacementSupport supportProp;
        Vector3 checkedPosition,checkedScale,supportPosition,supportScale;Quaternion checkedRotation,supportRotation;
        readonly RaycastHit[] hits=new RaycastHit[64];
        public struct Snapshot {public float stableTime;public bool attached;}

        public void Initialize(Rigidbody rigidbody,Renderer[] renderers,bool mountedAtHome=false)
        {
            if(initialized)return;initialized=true;body=rigidbody;Visuals=renderers;
            shapes=Array.FindAll(GetComponentsInChildren<Collider>(true),shape=>shape.GetComponentInParent<Rigidbody>(true)==body);
            homeAttached=mountedAtHome;homePosition=transform.position;homeRotation=transform.rotation;homeScale=transform.lossyScale;
            originals=new Material[Visuals.Length][];ghosts=new Material[Visuals.Length][];shadowModes=new ShadowCastingMode[Visuals.Length];
            var shader=Shader.Find("Parallax/PlacementGhost");
            if(shader==null)throw new InvalidOperationException("PlacementGhost shader is missing");
            for(int i=0;i<Visuals.Length;i++)
            {
                originals[i]=Visuals[i].sharedMaterials;shadowModes[i]=Visuals[i].shadowCastingMode;ghosts[i]=new Material[originals[i].Length];
                for(int j=0;j<originals[i].Length;j++)
                {
                    var source=originals[i][j];var m=new Material(shader){name=source.name+" / placement preview"};
                    if(source.HasProperty("_MainTex")){m.SetTexture("_MainTex",source.GetTexture("_MainTex"));m.SetTextureScale("_MainTex",source.GetTextureScale("_MainTex"));m.SetTextureOffset("_MainTex",source.GetTextureOffset("_MainTex"));}
                    if(source.HasProperty("_Color"))m.SetColor("_Color",source.GetColor("_Color"));ghosts[i][j]=m;
                }
            }
            visualReady=true;ResetPlaced();
        }
        public void SetPlayer(CharacterController value){player=value;UpdatePlayerCollision();}
        public bool OriginalCastsShadow(Renderer r){int i=Array.IndexOf(Visuals,r);return i>=0&&shadowModes[i]!=ShadowCastingMode.Off;}
        public Material OriginalMaterial(Renderer r,int index){int i=Array.IndexOf(Visuals,r);return i>=0?originals[i][index]:r.sharedMaterials[index];}
        public void ReplaceOriginalMaterial(Renderer r,int index,Material material)
        {int i=Array.IndexOf(Visuals,r);if(i<0)return;originals[i][index]=material;if(Ready)r.sharedMaterials=originals[i];}
        public Snapshot Capture()=>new Snapshot{stableTime=stableTime,attached=attached};
        public void BeginHold(){Held=true;attached=false;Supported=false;stableTime=0;SetReady(false);}
        public void Released(){Held=false;attached=false;Supported=false;stableTime=0;SetReady(false);RememberPose();}
        public void Restore(Snapshot saved)
        {Held=false;attached=saved.attached;stableTime=0;Supported=FindSupport(out bool water)&&Stable(water);if(Supported)stableTime=saved.stableTime;SetReady(Supported&&stableTime>=SettleSeconds);RememberPose();}
        public void ResetPlaced()
        {if(!initialized)return;Held=false;attached=homeAttached&&AtHome();stableTime=0;Supported=FindSupport(out bool water)&&Stable(water);if(Supported)stableTime=SettleSeconds;SetReady(Supported);RememberPose();}
        bool AtHome()=>Vector3.Distance(transform.position,homePosition)<.01f&&Quaternion.Angle(transform.rotation,homeRotation)<.1f&&Vector3.Distance(transform.lossyScale,homeScale)<.001f;
        public static bool IsUsableGround(Collider shape){var p=shape.GetComponentInParent<PlacementSupport>();return p==null||p.Ready;}
        void RememberPose(){checkedPosition=transform.position;checkedRotation=transform.rotation;checkedScale=transform.lossyScale;}
        bool Stable(bool water)=>body.isKinematic||body.IsSleeping()||(Mathf.Abs(body.linearVelocity.y)<(water?.8f:.35f)&&(water||body.angularVelocity.magnitude<.8f));
        public void Tick(float dt)
        {
            if(!initialized)return;
            if(Held){Supported=false;stableTime=0;SetReady(false);RememberPose();return;}
            bool moved=Vector3.Distance(checkedPosition,transform.position)>.003f||Quaternion.Angle(checkedRotation,transform.rotation)>.2f||Vector3.Distance(checkedScale,transform.lossyScale)>.001f;
            bool unchangedSupport=supportCollider!=null&&supportCollider.enabled&&supportCollider.gameObject.activeInHierarchy&&
                Vector3.Distance(supportPosition,supportCollider.transform.position)<.001f&&Quaternion.Angle(supportRotation,supportCollider.transform.rotation)<.1f&&Vector3.Distance(supportScale,supportCollider.transform.lossyScale)<.001f&&(supportProp==null||supportProp.Ready);
            if(Ready&&!moved&&unchangedSupport){UpdatePlayerCollision();return;}
            if((body.isKinematic&&moved)||Vector3.Distance(checkedPosition,transform.position)>.5f)stableTime=0;
            Supported=FindSupport(out bool water)&&Stable(water);
            stableTime=Supported?Mathf.Min(SettleSeconds,stableTime+Mathf.Max(0,dt)):0;
            SetReady(Supported&&stableTime>=SettleSeconds);RememberPose();
        }
        bool FindSupport(out bool water)
        {
            supportCollider=null;supportProp=null;water=WaterSupport!=null&&WaterSupport();if(water)return true;
            if(attached&&AtHome())return true;
            // Sample actual collider surfaces, not a large compound bounding box.
            foreach(var shape in shapes)
            {
                if(shape==null||!shape.enabled||!shape.gameObject.activeInHierarchy||shape.isTrigger)continue;var b=shape.bounds;
                for(int i=0;i<5;i++)
                {
                    var below=new Vector3(b.center.x,b.min.y-.08f,b.center.z);
                    if(i>0){below.x+=(i==1||i==3?-1:1)*b.extents.x*.9f;below.z+=(i<3?-1:1)*b.extents.z*.9f;}
                    var point=shape.ClosestPoint(below);int count=Physics.RaycastNonAlloc(point+Vector3.up*.025f,Vector3.down,hits,.13f,~(1<<30),QueryTriggerInteraction.Ignore);
                    for(int h=0;h<count;h++)
                    {
                        var hit=hits[h];if(hit.collider==null||hit.collider==player||hit.collider.GetComponent<CharacterController>()!=null||hit.normal.y<.5f)continue;
                        var other=hit.collider.GetComponentInParent<PlacementSupport>();if(other==this||(other!=null&&!other.Ready))continue;
                        if(hit.collider.attachedRigidbody==body)continue;
                        supportCollider=hit.collider;supportProp=other;supportPosition=supportCollider.transform.position;supportRotation=supportCollider.transform.rotation;supportScale=supportCollider.transform.lossyScale;return true;
                    }
                }
            }
            return false;
        }
        void SetReady(bool ready)
        {
            Ready=ready;
            if(visualReady!=ready)
            {
                for(int i=0;i<Visuals.Length;i++)if(Visuals[i]!=null)
                {Visuals[i].sharedMaterials=ready?originals[i]:ghosts[i];Visuals[i].shadowCastingMode=ready?shadowModes[i]:ShadowCastingMode.Off;}
                visualReady=ready;
            }
            UpdatePlayerCollision();
        }
        void UpdatePlayerCollision()
        {
            if(player==null||shapes==null||!player.enabled)return;
            foreach(var shape in shapes)if(shape!=null&&shape.enabled&&shape.gameObject.activeInHierarchy)Physics.IgnoreCollision(player,shape,!Ready);
        }
        void OnDestroy(){if(ghosts!=null)foreach(var set in ghosts)foreach(var m in set)if(m!=null)Destroy(m);}
    }
}
