using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static partial void ConfigureCoastalReceivers(BeachGame game)
        {
            var ivory=CoastalMaterial("Coastal Receiver ivory housing",new Color(.91f,.87f,.74f),.05f,.48f);
            var metal=CoastalMaterial("Coastal Receiver brushed nickel",new Color(.56f,.55f,.47f),.78f,.68f);
            var teal=CoastalMaterial("Coastal Receiver painted teal",new Color(.045f,.24f,.25f),.12f,.40f);
            var gasket=CoastalMaterial("Coastal Receiver waterproof gasket",new Color(.022f,.045f,.047f),0,.18f);
            var glass=CoastalMaterial("Coastal Receiver collector glass",new Color(.025f,.105f,.12f),.28f,.96f);
            var gold=CoastalMaterial("Coastal Receiver sun motif",new Color(1,.69f,.18f),.36f,.55f);
            var coral=CoastalMaterial("Coastal Receiver status inset",new Color(.28f,.075f,.035f),.05f,.45f);
            var indicator=CoastalMaterial("Coastal Receiver state light",new Color(.95f,.36f,.22f),.15f,.68f);
            var chargeLight=CoastalMaterial("Coastal Receiver charge light",new Color(.16f,.31f,.32f),.15f,.68f);
            gold.SetColor("_EmissionColor",new Color(.14f,.085f,.016f));
            var gate=game.lightGate;
            for(int i=0;i<gate.receiverVisuals.Length;i++)
            {
                var receiver=gate.receiverVisuals[i];var target=receiver.Target;var targetPosition=target.position;
                foreach(var renderer in receiver.GetComponentsInChildren<Renderer>())renderer.enabled=false;
                var root=new GameObject("Coastal Receiver "+(i+1).ToString("00")).transform;root.SetParent(receiver.transform,false);
                string name=root.name+" ";var front=Quaternion.Euler(90,0,0);

                CoastalBox(root,name+"base shoe",new Vector3(0,-1.59f,.23f),new Vector3(1.2f,.12f,.67f),teal,.04f);
                CoastalBox(root,name+"base cap",new Vector3(0,-1.515f,.23f),new Vector3(1.1f,.035f,.57f),metal,.012f);
                CoastalBox(root,name+"support",new Vector3(0,-1.29f,.26f),new Vector3(.19f,.55f,.22f),ivory,.025f);
                var back=CoastalBox(root,name+"case seam",new Vector3(0,.14f,.27f),new Vector3(1.97f,2.51f,.48f),metal,.20f);
                back.transform.localScale=new Vector3(1,1,.65f);
                var body=CoastalBox(root,name+"waterproof case",new Vector3(0,.14f,.23f),new Vector3(1.92f,2.46f,.44f),ivory,.20f);
                body.transform.localScale=new Vector3(1,1,.65f);

                var headerTrim=CoastalBox(root,name+"number plate rim",new Vector3(0,1.065f,.065f),new Vector3(1.74f,.40f,.34f),metal,.15f);
                headerTrim.transform.localScale=new Vector3(1,1,.20f);
                var header=CoastalBox(root,name+"integrated number plate",new Vector3(0,1.065f,.038f),new Vector3(1.66f,.32f,.30f),teal,.12f);
                header.transform.localScale=new Vector3(1,1,.10f);
                CoastalBox(root,name+"number divider",new Vector3(-.25f,1.065f,.014f),new Vector3(.008f,.18f,.009f),metal,.003f);
                var statusTrim=CoastalBox(root,name+"status plate rim",new Vector3(0,-.93f,.064f),new Vector3(1.66f,.31f,.28f),metal,.12f);
                statusTrim.transform.localScale=new Vector3(1,1,.20f);
                var statusPlate=CoastalBox(root,name+"status plate",new Vector3(0,-.93f,.035f),new Vector3(1.58f,.235f,.24f),coral,.10f);
                statusPlate.transform.localScale=new Vector3(1,1,.10f);

                CoastalCylinder(root,name+"dark glass collector",new Vector3(0,0,.054f),new Vector3(1.43f,.04f,1.43f),glass,front);
                CoastalRing(root,name+"outer metal bezel",new Vector3(0,0,.027f),.752f,.031f,metal);
                CoastalRing(root,name+"glass seal",new Vector3(0,0,.022f),.714f,.016f,gasket);
                CoastalRing(root,name+"inner collector trim",new Vector3(0,0,.016f),.605f,.009f,teal);
                var face=CoastalCylinder(root,name+"sun aperture",new Vector3(0,0,.020f),new Vector3(.44f,.040f,.44f),indicator,front).GetComponent<Renderer>();
                for(int ray=0;ray<8;ray++)
                {
                    float angle=ray*45,rad=angle*Mathf.Deg2Rad;
                    var item=CoastalBox(root,name+"sun ray "+ray,new Vector3(Mathf.Sin(rad)*.37f,Mathf.Cos(rad)*.37f,.014f),new Vector3(.052f,.18f,.028f),gold,.009f);
                    item.transform.localRotation=Quaternion.Euler(0,0,-angle);
                }
                CoastalRing(root,name+"status lamp bezel",new Vector3(-.615f,-.93f,.008f),.071f,.011f,metal);
                var lamp=CoastalCylinder(root,name+"status lamp",new Vector3(-.615f,-.93f,.009f),new Vector3(.115f,.028f,.115f),indicator,front).GetComponent<Renderer>();

                var state=new SerializedObject(receiver);state.FindProperty("face").objectReferenceValue=face;state.FindProperty("statusLamp").objectReferenceValue=lamp;
                var segments=state.FindProperty("chargeSegments");segments.arraySize=16;
                for(int segment=0;segment<segments.arraySize;segment++)
                {
                    float angle=segment*360f/segments.arraySize,rad=angle*Mathf.Deg2Rad;
                    var item=CoastalBox(root,name+"charge segment "+segment,new Vector3(Mathf.Sin(rad)*.662f,Mathf.Cos(rad)*.662f,-.004f),new Vector3(.19f,.055f,.018f),chargeLight,.006f);
                    item.transform.localRotation=Quaternion.Euler(0,0,-angle);segments.GetArrayElementAtIndex(segment).objectReferenceValue=item.GetComponent<Renderer>();
                }
                var numberAnchor=(Transform)state.FindProperty("numberAnchor").objectReferenceValue;numberAnchor.localPosition=new Vector3(-.53f,1.065f,.008f);numberAnchor.localScale=Vector3.one*.84f;
                var titleAnchor=(Transform)state.FindProperty("titleAnchor").objectReferenceValue;titleAnchor.localPosition=new Vector3(.27f,1.065f,.008f);titleAnchor.localScale=Vector3.one*.90f;
                var statusAnchor=(Transform)state.FindProperty("statusAnchor").objectReferenceValue;statusAnchor.localPosition=new Vector3(.12f,-.93f,.008f);statusAnchor.localScale=Vector3.one*.76f;
                state.ApplyModifiedPropertiesWithoutUndo();gate.sensorFaces[i]=face;receiver.SetState(false,false,0);

                foreach(float side in new[]{-1f,1f})
                {
                    CoastalBox(root,name+"top fixing strap "+side,new Vector3(side*.56f,1.38f,.32f),new Vector3(.13f,.30f,.10f),metal,.02f);
                    CoastalBox(root,name+"top fixing hook "+side,new Vector3(side*.56f,1.49f,.26f),new Vector3(.13f,.09f,.24f),metal,.02f);
                    ReceiverFastener(root,name+"top screw "+side,new Vector3(side*.56f,1.43f,.252f),metal,gasket,front);
                    foreach(float height in new[]{-.50f,.50f})
                    {
                        CoastalBox(root,name+"pivot clamp "+side+" "+height,new Vector3(side*.95f,height,.10f),new Vector3(.20f,.15f,.19f),metal,.025f);
                        ReceiverFastener(root,name+"pivot screw "+side+" "+height,new Vector3(side*.95f,height,-.004f),metal,gasket,front);
                    }
                    foreach(float height in new[]{-.76f,.76f})ReceiverFastener(root,name+"case screw "+side+" "+height,new Vector3(side*.84f,height,.070f),metal,gasket,front);
                    ReceiverFastener(root,name+"header screw "+side,new Vector3(side*.75f,1.065f,.012f),metal,gasket,front);
                    ReceiverFastener(root,name+"status screw "+side,new Vector3(side*.705f,-.93f,.012f),metal,gasket,front);
                }
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                {renderer.receiveShadows=false;Debug.Assert(renderer.shadowCastingMode==ShadowCastingMode.Off,"Receiver decoration must not create protective shade.");}
                Debug.Assert(receiver.Target==target&&target.position==targetPosition&&gate.sensors[i]==target&&gate.sensorFaces[i]==receiver.Face,"Coastal receiver must retain the measured target and connected face.");
                Debug.Assert(root.GetComponentsInChildren<Collider>().Length==0,"Receiver decoration must not block incoming light or walking.");
            }
        }
        static void ReceiverFastener(Transform parent,string name,Vector3 position,Material metal,Material slot,Quaternion front)
        {
            CoastalCylinder(parent,name,position,new Vector3(.052f,.016f,.052f),metal,front);
            CoastalBox(parent,name+" slot",position+Vector3.back*.010f,new Vector3(.028f,.007f,.004f),slot,.0015f);
        }
    }
}
