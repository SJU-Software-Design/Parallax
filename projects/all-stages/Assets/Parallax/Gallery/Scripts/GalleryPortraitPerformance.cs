using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Visual-only copies never acquire a collider, interaction prop or shadow
    // caster. The original props remain registered and can be restored by R.
    public sealed class GalleryPortraitPerformance : MonoBehaviour
    {
        sealed class Piece
        {
            public GalleryProp source;
            public Transform root;
            public Vector3 startPosition,startScale,targetPosition,targetScale;
            public Quaternion startRotation,targetRotation;
        }
        sealed class Part
        {
            public MeshRenderer renderer;
            public int side,feature;
            public bool support;
            public Vector4 splitPlane;
            public Mesh smileMesh;
            public Vector3[] neutralVertices,smileVertices;
            public MaterialPropertyBlock properties;
        }
        readonly List<Piece> pieces=new List<Piece>();
        readonly List<Part> parts=new List<Part>();
        readonly List<Material> materials=new List<Material>();
        readonly List<Mesh> meshes=new List<Mesh>();
        GalleryPortraitGate gate;
        bool begun,attached;
        float absorption,smile,opening,red,lastSmile=-1;
        public bool Active=>begun;
        public bool Attached=>attached;
        public int VisualPartCount=>parts.Count;

        public void Begin(GalleryPortraitGate owner,GalleryConceptPerspective portrait)
        {
            ResetPerformance();gate=owner;
            if(portrait==null||portrait.pieces==null||portrait.pieces.Length!=3)
                throw new InvalidOperationException("The curtain performance requires the original three portrait props.");
            var shader=Shader.Find("Parallax/PortraitCurtainFace");
            if(shader==null)throw new InvalidOperationException("The portrait curtain face shader is missing.");
            Vector3[] markers={new Vector3(0,3.65f,-.16f),new Vector3(0,2.65f,-.18f),new Vector3(0,1.50f,-.18f)};
            float[] scales={.82f,.82f,.88f};
            for(int i=0;i<3;i++)
            {
                var source=portrait.pieces[i];source.InitializePhysics();
                var root=new GameObject("Curtain absorption · "+source.title).transform;
                root.SetParent(transform,false);root.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                root.localScale=source.transform.lossyScale;
                Vector3 targetScale=new Vector3(scales[i],scales[i],.075f);
                Vector3 marker=portrait.markerLocalPositions[i];
                var piece=new Piece{source=source,root=root,startPosition=root.position,startRotation=root.rotation,startScale=root.localScale,
                    targetRotation=gate.transform.rotation,targetScale=targetScale,
                    targetPosition=gate.transform.TransformPoint(markers[i])-gate.transform.rotation*Vector3.Scale(marker,targetScale)};
                pieces.Add(piece);
                foreach(var original in source.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter=original.GetComponent<MeshFilter>();
                    if(!original.enabled||filter==null||filter.sharedMesh==null)continue;
                    Matrix4x4 local=source.transform.worldToLocalMatrix*original.transform.localToWorldMatrix;
                    bool support=IsSupport(original.name);
                    for(int side=-1;side<=1;side+=2)ClonePart(original,filter.sharedMesh,local,piece.root,shader,i,side,support);
                }
                PropReleasePhysics.Attach(source.body).CancelPendingRelease();
                HeldCollisionState.Attach(source.body).Restore();
                if(!source.body.isKinematic){source.body.linearVelocity=Vector3.zero;source.body.angularVelocity=Vector3.zero;}
                source.body.isKinematic=true;source.body.useGravity=false;
                source.gameObject.SetActive(false);
            }
            begun=true;attached=false;absorption=smile=opening=red=0;lastSmile=-1;
            SetAbsorption(0);SetExpression(0,0,0);Physics.SyncTransforms();
        }
        static bool IsSupport(string name)
        {
            string value=name.ToLowerInvariant();
            return value.Contains("stand")||value.Contains("foot")||value.Contains("plinth");
        }
        void ClonePart(MeshRenderer original,Mesh originalMesh,Matrix4x4 local,Transform parent,
            Shader shader,int feature,int side,bool support)
        {
            var go=new GameObject("Curtain face · "+original.name+" · "+(side<0?"left":"right"));
            go.transform.SetParent(parent,false);go.transform.localPosition=local.GetColumn(3);
            go.transform.localRotation=local.rotation;go.transform.localScale=local.lossyScale;
            var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=originalMesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var part=new Part{renderer=renderer,feature=feature,side=side,support=support,splitPlane=local.GetRow(0),properties=new MaterialPropertyBlock()};
            string name=original.name.ToLowerInvariant();
            if(feature==2&&!support&&(name.Contains("lip")||name.Contains("cushion")))
            {
                part.smileMesh=Instantiate(originalMesh);part.smileMesh.name="Curtain-only smirking "+originalMesh.name;
                filter.sharedMesh=part.smileMesh;meshes.Add(part.smileMesh);
                part.neutralVertices=part.smileMesh.vertices;part.smileVertices=new Vector3[part.neutralVertices.Length];
            }
            var originals=original.sharedMaterials;var copies=new Material[originals.Length];
            for(int slot=0;slot<copies.Length;slot++)
            {
                var source=originals[slot];
                var material=new Material(shader){name="Curtain visual · "+source.name};
                if(source.HasProperty("_Color"))material.SetColor("_Color",source.GetColor("_Color"));
                if(source.HasProperty("_MainTex"))
                {
                    material.mainTexture=source.GetTexture("_MainTex");
                    material.mainTextureScale=source.GetTextureScale("_MainTex");material.mainTextureOffset=source.GetTextureOffset("_MainTex");
                }
                copies[slot]=material;materials.Add(material);
            }
            renderer.sharedMaterials=copies;parts.Add(part);
        }
        public void SetAbsorption(float value)
        {
            if(!begun)return;absorption=Mathf.Clamp01(value);
            if(!attached)
            {
                float eased=Mathf.SmoothStep(0,1,absorption);
                foreach(var piece in pieces)
                {
                    piece.root.SetPositionAndRotation(Vector3.Lerp(piece.startPosition,piece.targetPosition,eased),
                        Quaternion.Slerp(piece.startRotation,piece.targetRotation,eased));
                    piece.root.localScale=Vector3.Lerp(piece.startScale,piece.targetScale,eased);
                }
                if(absorption>=1)
                {
                    foreach(var part in parts)
                    {
                        if(part.support){part.renderer.gameObject.SetActive(false);continue;}
                        part.renderer.transform.SetParent(part.side<0?gate.leftCurtain:gate.rightCurtain,true);
                    }
                    attached=true;
                }
            }
            UpdateProperties();
        }
        public void SetExpression(float smile01,float opening01,float red01)
        {
            if(!begun)return;
            smile=Mathf.Clamp01(smile01);opening=Mathf.Clamp01(opening01);red=Mathf.Clamp01(red01);
            if(Mathf.Abs(lastSmile-smile)>.0001f)
            {
                foreach(var part in parts)
                {
                    if(part.smileMesh==null)continue;
                    for(int i=0;i<part.neutralVertices.Length;i++)
                    {
                        Vector3 point=part.neutralVertices[i];float edge=Mathf.Clamp01(Mathf.Abs(point.x)/1.35f);
                        point.y+=Mathf.Pow(edge,1.65f)*.39f*smile*(point.x>0?1:.82f);
                        point.x*=1+.10f*smile;part.smileVertices[i]=point;
                    }
                    part.smileMesh.vertices=part.smileVertices;part.smileMesh.RecalculateNormals();part.smileMesh.RecalculateBounds();
                }
                lastSmile=smile;
            }
            UpdateProperties();
        }
        void UpdateProperties()
        {
            foreach(var part in parts)
            {
                if(part.renderer==null)continue;
                float opacity=part.support?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.86f,absorption)):1;
                if(attached&&part.feature>0)opacity*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.68f,opening));
                part.properties.SetFloat("_Opacity",opacity);part.properties.SetFloat("_Red01",red);
                part.properties.SetFloat("_Fold",attached?opening:0);
                part.properties.SetFloat("_SplitSide",part.side);part.properties.SetVector("_SplitPlane",part.splitPlane);
                part.renderer.SetPropertyBlock(part.properties);
            }
        }
        public void ResetPerformance()
        {
            foreach(var part in parts)if(part.renderer!=null){part.renderer.gameObject.SetActive(false);DestroyOwned(part.renderer.gameObject);}
            foreach(var piece in pieces)
            {
                if(piece.root!=null)DestroyOwned(piece.root.gameObject);
                if(begun&&piece.source!=null){piece.source.gameObject.SetActive(true);piece.source.Restore();}
            }
            foreach(var mesh in meshes)if(mesh!=null)DestroyOwned(mesh);
            foreach(var material in materials)if(material!=null)DestroyOwned(material);
            parts.Clear();pieces.Clear();meshes.Clear();materials.Clear();
            begun=attached=false;absorption=smile=opening=red=0;lastSmile=-1;
        }
        void OnDestroy()
        {
            foreach(var mesh in meshes)if(mesh!=null)DestroyOwned(mesh);
            foreach(var material in materials)if(material!=null)DestroyOwned(material);
            meshes.Clear();materials.Clear();
        }
        static void DestroyOwned(UnityEngine.Object value)
        {if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
