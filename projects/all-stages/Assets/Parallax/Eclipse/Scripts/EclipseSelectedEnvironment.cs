using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Eclipse
{
    public sealed class EclipseSelectedAssetMarker : MonoBehaviour
    {
        public string assetId,author,sourceUrl,license;
    }

    // Original selected FBX geometry/UVs and source PBR maps; layout is project-authored.
    public sealed class EclipseSelectedEnvironment : MonoBehaviour
    {
        const string Models="Eclipse/Selected/Quaternius/Models/";
        const string Textures="Eclipse/Selected/Quaternius/Textures/";
        public Transform AssetsRoot {get;private set;}
        public Transform LeftPirateShip {get;private set;}
        public bool EnvironmentReady {get;private set;}
        public bool ArrivalShipReady {get;private set;}
        public int SelectedMeshCount {get;private set;}
        public int UnmappedMaterialSlots {get;private set;}
        public float PresentationLightGain {get;private set;} = 1;
        public string Status {get;private set;}="Not built";
        readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        readonly Dictionary<string,GameObject> sourceModels=new Dictionary<string,GameObject>();
        readonly List<string> missing=new List<string>();
        public void Build(Transform parent)
        {
            AssetsRoot=new GameObject("Eclipse selected assets - original Standard meshes").transform;
            AssetsRoot.SetParent(parent,false);
            foreach(string key in new[]{"Column_Simple","Column_Astra","Prop_Rail_4","ShortWall_WhitePlate2_Straight",
                "ShortWall_AccentStrip_Straight","ShortWall_DarkPlastic_Straight","Platform_Metal2"})
            {
                var source=Resources.Load<GameObject>(Models+key);
                if(source==null)missing.Add(key);else sourceModels.Add(key,source);
            }
            // Original support/deck collision is still owned by EclipseFinaleWorld.
            // Dress the outer edge only; the continuous central credits surface stays clear.
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<32;i++)
                {
                    float z=-3.5f+i*2.2f;
                    string wall=i%5==2?"ShortWall_DarkPlastic_Straight":i%5==4?"ShortWall_AccentStrip_Straight":"ShortWall_WhitePlate2_Straight";
                    Place(wall,new Vector3(side*12.90f,.035f,z),new Vector3(0,.80f,2.2f),side<0?0:180,false);
                    Place("Prop_Rail_4",new Vector3(side*12.92f,.845f,z),new Vector3(0,.36f,1.82f),side<0?0:180,false);
                }
                for(int i=0;i<8;i++)
                {
                    float z=-1+i*9.3f;
                    Place(i%2==0?"Column_Simple":"Column_Astra",new Vector3(side*13.16f,.025f,z),new Vector3(0,2.6f,0),side<0?0:180,true);
                }
                for(int i=0;i<18;i++)
                    Place("Platform_Metal2",new Vector3(side*11.88f,.018f,-3+i*4),new Vector3(1.72f,0,4),0,false);
            }
            for(int i=0;i<12;i++)
            {
                float x=-12.1f+i*2.2f;
                Place("ShortWall_DarkPlastic_Straight",new Vector3(x,.035f,-4.86f),new Vector3(0,.80f,2.2f),90,false);
                Place("Prop_Rail_4",new Vector3(x,.845f,-4.88f),new Vector3(0,.36f,1.82f),90,false);
            }
            EnvironmentReady=missing.Count==0&&SelectedMeshCount>0&&UnmappedMaterialSlots==0;
            BuildArrivalShip();
            Status="Original Standard meshes="+SelectedMeshCount+" missing="+string.Join(",",missing)+
                " unmappedMaterials="+UnmappedMaterialSlots+" arrivalShip="+ArrivalShipReady;
            if(!EnvironmentReady)Debug.LogWarning("ECLIPSE_SELECTED_ENVIRONMENT_INCOMPLETE "+Status);
        }
        Transform Place(string key,Vector3 at,Vector3 targetSize,float yaw,bool uniform)
        {
            if(!sourceModels.TryGetValue(key,out var source))return null;
            var root=new GameObject("Quaternius / "+key).transform;root.SetParent(AssetsRoot,false);
            var visual=Instantiate(source,root,false).transform;
            RemovePhysics(visual);
            foreach(var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                var slots=r.sharedMaterials;
                for(int i=0;i<slots.Length;i++)slots[i]=Surface(slots[i]!=null?slots[i].name:"",key,i);
                r.sharedMaterials=slots;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
            }
            Bounds b=BoundsIn(root,visual);
            Vector3 scale=Vector3.one;
            if(uniform){float s=targetSize.y/Mathf.Max(.001f,b.size.y);scale=Vector3.one*s;}
            else
            {
                if(targetSize.x>0&&b.size.x>.001f)scale.x=targetSize.x/b.size.x;
                if(targetSize.y>0&&b.size.y>.001f)scale.y=targetSize.y/b.size.y;
                if(targetSize.z>0&&b.size.z>.001f)scale.z=targetSize.z/b.size.z;
            }
            visual.localScale=Vector3.Scale(visual.localScale,scale);
            b=BoundsIn(root,visual);
            visual.localPosition-=new Vector3(b.center.x,b.min.y,b.center.z);
            root.localPosition=at;root.localRotation=Quaternion.Euler(0,yaw,0);
            var marker=root.gameObject.AddComponent<EclipseSelectedAssetMarker>();
            marker.assetId="Quaternius/Standard/"+key;marker.author="Quaternius";
            marker.sourceUrl="https://quaternius.com/packs/modularscifimegakit.html";marker.license="CC0-1.0";
            SelectedMeshCount+=visual.GetComponentsInChildren<MeshFilter>(true).Length;
            return root;
        }
        Material Surface(string originalName,string model,int slot)
        {
            string key=originalName.Replace(" (Instance)","");
            if(!key.Contains("Trim_01")&&!key.Contains("Trim_02")&&!key.Contains("Trim_03"))
            {
                // FBX material importers may rename slots. This explicit list is
                // recorded from the corresponding original glTF material order.
                string[] names=model=="Column_Simple"?new[]{"MI_Trim_01","MI_Trim_02"}:
                    model=="Column_Astra"?new[]{"MI_Trim_03","MI_Trim_02","MI_Trim_01"}:
                    model=="Prop_Rail_4"?new[]{"MI_Trim_02_Rails"}:
                    model=="ShortWall_AccentStrip_Straight"?new[]{"MI_Trim_01"}:
                    model=="ShortWall_DarkPlastic_Straight"?new[]{"MI_Trim_03_Dark"}:
                    model=="Platform_Metal2"?new[]{"MI_Trim_02"}:new[]{"MI_Trim_03"};
                if(slot>=names.Length){UnmappedMaterialSlots++;key=names[0];}else key=names[slot];
            }
            if(materials.TryGetValue(key,out var existing))return existing;
            string trim=key.Contains("Trim_01")?"T_Trim_01":key.Contains("Trim_02")?"T_Trim_02":"T_Trim_03";
            Shader shader=Shader.Find("Parallax/EclipseSelectedSurface");
            if(shader==null)shader=Resources.Load<Shader>("Eclipse/Selected/EclipseSelectedSurface");
            if(shader==null)throw new InvalidOperationException("Eclipse selected-source surface shader is missing");
            var material=new Material(shader){name="Eclipse original / "+key};
            var albedo=Resources.Load<Texture2D>(Textures+trim+"_BaseColor");
            var normal=Resources.Load<Texture2D>(Textures+trim+"_Normal");
            var orm=Resources.Load<Texture2D>(Textures+trim+"_ORM");
            if(albedo==null||normal==null||orm==null)missing.Add(trim+" PBR source maps");
            material.SetTexture("_MainTex",albedo);material.SetTexture("_BumpMap",normal);material.SetTexture("_ORM",orm);
            var emission=Resources.Load<Texture2D>(Textures+trim+"_Emissive");
            if(emission!=null)material.SetTexture("_EmissionMap",emission);
            material.SetFloat("_EmissionGain",.14f);
            if(key.Contains("Dark"))material.color=new Color(.34f,.36f,.39f);
            else if(key.Contains("Rails"))material.color=new Color(1,.79f,.43f);
            materials.Add(key,material);return material;
        }
        void BuildArrivalShip()
        {
            var source=Resources.Load<GameObject>("Eclipse/Selected/ArrivalPirateShip");
            if(source==null){missing.Add("existing Beach ArrivalPirateShip prefab");return;}
            LeftPirateShip=Instantiate(source,AssetsRoot,false).transform;
            LeftPirateShip.name="Beach arrival / same Comeback pirate ship / visual only";
            LeftPirateShip.localPosition=new Vector3(-22,3,10);
            LeftPirateShip.localRotation=Quaternion.identity;
            LeftPirateShip.localScale=Vector3.one*.4f;
            // The FBX pivot is above the keel. Position by actual source-renderer
            // bounds, rather than assuming that rootY is the ship bottom.
            bool firstBound=true;Bounds visible=new Bounds();
            foreach(var renderer in LeftPirateShip.GetComponentsInChildren<Renderer>(true))
            {
                if(firstBound){visible=renderer.bounds;firstBound=false;}else visible.Encapsulate(renderer.bounds);
            }
            if(!firstBound&&visible.min.y<.75f)
                LeftPirateShip.position+=Vector3.up*(.75f-visible.min.y);
            RemovePhysics(LeftPirateShip);
            var marker=LeftPirateShip.gameObject.AddComponent<EclipseSelectedAssetMarker>();
            marker.assetId="Beach/StylizedPirateShip";marker.author="Comeback";
            marker.sourceUrl="https://assetstore.unity.com/packages/3d/vehicles/sea/stylized-pirate-ship-200192";
            marker.license="Standard Unity Asset Store EULA";
            foreach(var r in LeftPirateShip.GetComponentsInChildren<Renderer>(true))
            {r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;}
            ArrivalShipReady=LeftPirateShip.GetComponentsInChildren<MeshFilter>(true).Length>0&&
                LeftPirateShip.GetComponentsInChildren<Collider>(true).Length==0;
        }
        static void RemovePhysics(Transform root)
        {
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))
            {collider.enabled=false;if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);}
            foreach(var rigidbody in root.GetComponentsInChildren<Rigidbody>(true))
            {rigidbody.isKinematic=true;if(Application.isPlaying)Destroy(rigidbody);else DestroyImmediate(rigidbody);}
        }
        static Bounds BoundsIn(Transform root,Transform visual)
        {
            bool first=true;Bounds result=new Bounds();
            foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)continue;Bounds b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    p=root.InverseTransformPoint(filter.transform.TransformPoint(p));
                    if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);
                }
            }
            return result;
        }
        public void SetDarkness(float darkness)
        {PresentationLightGain=Mathf.Lerp(1,.025f,Mathf.Clamp01(darkness));foreach(var material in materials.Values)material.SetFloat("_LightGain",PresentationLightGain);}
        void OnDestroy(){foreach(var material in materials.Values)if(material!=null)Destroy(material);}
    }
}