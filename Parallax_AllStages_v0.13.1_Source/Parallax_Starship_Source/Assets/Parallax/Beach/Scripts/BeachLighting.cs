using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    // The safety query uses the same source meshes as the rendered shadow map.
    // Separate non-solid mesh proxies avoid convex-hull shadows on hollow props.
    public sealed class BeachLighting : MonoBehaviour
    {
        public const int ShadowLayer=30;
        public static readonly Vector3 ToSun=new Vector3(.24f,1,-.42f).normalized;
        public BeachMirror mirror;
        sealed class Proxy {public Transform source;public MeshCollider collider;public BeachProp owner;public Renderer renderer;}
        readonly List<Proxy> proxies=new List<Proxy>();
        readonly RaycastHit[] hits=new RaycastHit[256];
        GameObject queryRoot;
        public int Count=>proxies.Count;
        public void Rebuild()
        {
            mirror=FindFirstObjectByType<BeachMirror>();
            if(!Application.isPlaying)return;
            if(queryRoot!=null)Destroy(queryRoot);
            proxies.Clear();queryRoot=new GameObject("Beach · non-solid shadow queries");
            for(int i=0;i<32;i++)Physics.IgnoreLayerCollision(ShadowLayer,i,true);
            Physics.queriesHitBackfaces=true;
            foreach(var source in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var renderer=source.GetComponent<Renderer>();
                var owner=source.GetComponentInParent<BeachProp>();
                bool casts=owner!=null&&owner.placement!=null?owner.placement.OriginalCastsShadow(renderer):renderer!=null&&renderer.shadowCastingMode!=ShadowCastingMode.Off;
                if(renderer==null||!renderer.enabled||!casts||source.sharedMesh==null)continue;
                var obj=new GameObject("Shadow query: "+source.name);obj.layer=ShadowLayer;obj.transform.SetParent(queryRoot.transform);
                var mesh=obj.AddComponent<MeshCollider>();mesh.sharedMesh=source.sharedMesh;
                proxies.Add(new Proxy{source=source.transform,collider=mesh,owner=owner,renderer=renderer});
            }
            Synchronize();
        }
        public void Synchronize()
        {
            foreach(var p in proxies)
            {
                if(p.source==null)continue;
                p.collider.enabled=p.source.gameObject.activeInHierarchy&&p.renderer.enabled&&(p.owner==null||p.owner.placement.Ready);
                var t=p.collider.transform;t.SetPositionAndRotation(p.source.position,p.source.rotation);t.localScale=p.source.lossyScale;
            }
            Physics.SyncTransforms();
            if(mirror!=null)mirror.UpdateLight();
        }
        bool Blocked(Vector3 from,Vector3 direction,float distance,BeachProp ignore=null)
        {
            int count=Physics.RaycastNonAlloc(from,direction,hits,distance,1<<ShadowLayer,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                if(hits[i].distance<.008f)continue;
                if(ignore!=null){bool own=false;foreach(var p in proxies)if(p.collider==hits[i].collider&&p.owner==ignore){own=true;break;}if(own)continue;}
                return true;
            }
            return false;
        }
        public bool MirrorReceivesSun(Vector3 at)=>mirror!=null&&!Blocked(at+ToSun*.06f,ToSun,100,mirror.prop);
        public bool IsDirectLit(Vector3 surface,Vector3 normal)=>Vector3.Dot(normal,ToSun)>.025f&&!Blocked(surface+normal*.035f,ToSun,100);
        public bool IsReflectedLit(Vector3 surface,Vector3 normal)
        {
            if(mirror==null||!mirror.Illuminated)return false;
            Vector3 from=mirror.LightOrigin,delta=surface-from;
            if(delta.magnitude>32||delta.magnitude<.1f||Vector3.Dot(normal,-delta.normalized)<.015f)return false;
            if(Vector3.Angle(delta,mirror.ReflectedDirection)>mirror.ConeAngle*.5f)return false;
            return !Blocked(from,delta.normalized,delta.magnitude-.06f,mirror.prop);
        }
        public bool IsSunlit(Vector3 surface,Vector3 normal)=>IsDirectLit(surface,normal)||IsReflectedLit(surface,normal);
        void LateUpdate(){Synchronize();}
        void OnDestroy(){if(queryRoot!=null)Destroy(queryRoot);}
    }
}
