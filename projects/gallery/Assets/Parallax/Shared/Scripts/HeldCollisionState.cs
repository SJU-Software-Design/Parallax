using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink
{
    // Solver filtering is independent from ray/overlap filtering. Owned
    // colliders stay enabled so the projection and player checks can use them.
    public sealed class HeldCollisionState:MonoBehaviour
    {
        public const int Layer=29;
        struct SavedLayer { public GameObject value;public int layer; }
        readonly List<SavedLayer> layers=new List<SavedLayer>();
        Rigidbody body;
        bool isolated;

        public static HeldCollisionState Attach(Rigidbody value)
        {
            var state=value.GetComponent<HeldCollisionState>();
            if(state==null)state=value.gameObject.AddComponent<HeldCollisionState>();
            state.body=value;return state;
        }
        public void Begin()
        {
            if(isolated)return;
            // This project has no special held-contact barriers. Ordinary world,
            // loose props and the player are query targets, not solver contacts.
            for(int i=0;i<32;i++)Physics.IgnoreLayerCollision(Layer,i,i!=Layer);
            layers.Clear();Capture(transform);isolated=true;
            foreach(var saved in layers)
            {
                // Mirror faces use31 to stay out of their reflection camera.
                // A visual-only child needs no solver layer change.
                if(saved.layer==31&&saved.value.GetComponent<Collider>()==null)continue;
                saved.value.layer=Layer;
            }
            Physics.SyncTransforms();
        }
        void Capture(Transform value)
        {
            var owner=value.GetComponent<Rigidbody>();
            if(value!=transform&&owner!=null&&owner!=body)return;
            layers.Add(new SavedLayer{value=value.gameObject,layer=value.gameObject.layer});
            for(int i=0;i<value.childCount;i++)Capture(value.GetChild(i));
        }
        public void Restore()
        {
            if(!isolated)return;
            foreach(var saved in layers)if(saved.value!=null)saved.value.layer=saved.layer;
            isolated=false;layers.Clear();Physics.SyncTransforms();
        }
        void OnDisable(){Restore();}
        void OnDestroy(){Restore();}
    }
}
