using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static partial void ConfigureCoastalWorkshop(BeachGame game)
        {
            var harbor=game.GetComponentInChildren<BeachShadowHarbor>(true);
            if(harbor==null||harbor.target.Find("Coastal Workshop frame")!=null)return;
            var samples=harbor.samples.Select(s=>(s.point,s.inside,s.region)).ToArray();
            var colliders=harbor.GetComponentsInChildren<Collider>(true);
            var wood=CoastalWood("Coastal Workshop weathered oak",new Color(.87f,.82f,.70f));
            var teal=CoastalWood("Coastal Workshop painted legs",new Color(.13f,.31f,.31f),true);
            var linen=CoastalLinen("Coastal Workshop stretched canvas",new Color(.96f,.91f,.76f));
            var brass=CoastalMaterial("Coastal Workshop brass fasteners",new Color(.58f,.43f,.20f),.7f,.42f);
            var ink=new Material(Shader.Find("ShadeLink/Flat")){name="Coastal Workshop printed terracotta",color=new Color(.70f,.27f,.12f)};
            var channel=CoastalMaterial("Coastal Workshop progress channel",new Color(.035f,.10f,.11f));
            var frame=new GameObject("Coastal Workshop frame").transform;frame.SetParent(harbor.target,false);
            string[] replaced={"그림자 보드 바탕","그림자 보드 옆틀","그림자 보드 가로틀","제작 보드 머리글","게시판 모서리 고정핀","제작 진행판"};
            foreach(var renderer in harbor.target.GetComponentsInChildren<Renderer>(true))
            {
                if(replaced.Contains(renderer.name)){renderer.enabled=false;renderer.shadowCastingMode=ShadowCastingMode.Off;}
                if(renderer is LineRenderer line)
                {line.sharedMaterial=ink;line.startWidth=line.endWidth=.021f;line.alignment=LineAlignment.View;}
            }
            foreach(var sample in harbor.samples)if(sample.indicator!=null)sample.indicator.enabled=false;
            CoastalBox(frame,"Coastal Workshop canvas",new Vector3(0,0,-.041f),new Vector3(4.8f,3.2f,.08f),linen,.024f);
            foreach(float x in new[]{-2.46f,2.46f})
                CoastalBox(frame,"Coastal Workshop upright rim",new Vector3(x,0,.005f),new Vector3(.18f,3.42f,.17f),wood,.035f);
            foreach(float y in new[]{-1.64f,1.64f})
                CoastalBox(frame,"Coastal Workshop cross rim",new Vector3(0,y,.005f),new Vector3(5.04f,.18f,.17f),wood,.035f);
            CoastalBox(frame,"Coastal Workshop carved header",new Vector3(0,1.99f,-.015f),new Vector3(5.04f,.5f,.16f),wood,.035f);
            CoastalBox(frame,"Coastal Workshop progress housing",new Vector3(0,-1.94f,.03f),new Vector3(5.04f,.44f,.15f),teal,.025f);
            CoastalBox(frame,"Coastal Workshop progress recess",new Vector3(0,-2.06f,.115f),new Vector3(4.08f,.11f,.025f),channel,.01f);
            foreach(float x in new[]{-2.33f,2.33f})foreach(float y in new[]{-1.50f,1.50f,1.99f})
                CoastalCylinder(frame,"Coastal Workshop canvas pin",new Vector3(x,y,.115f),new Vector3(.065f,.03f,.065f),brass,Quaternion.Euler(90,0,0));
            foreach(var label in harbor.target.GetComponentsInChildren<BeachLabel>(true))
            {
                label.style=FontStyle.Bold;
                label.color=label.caption=="그림자 배 제작소"?new Color(.98f,.93f,.78f):new Color(.12f,.28f,.29f);
                if(label.caption=="그림자 배 제작소")label.transform.localPosition=new Vector3(0,1.99f,.11f);
            }
            // The HUD keeps detailed percentages; the physical board uses one quiet progress strip.
            harbor.statusAnchor.gameObject.SetActive(false);
            harbor.chargeFill.parent.localPosition=new Vector3(0,.12f,.10f);
            foreach(var renderer in harbor.GetComponentsInChildren<Renderer>(true))
            {
                if(new[]{"제작 게시판 목재 기둥","제작 게시판 받침발","보드 앞 작은 배 받침"}.Contains(renderer.name))renderer.sharedMaterial=wood;
                if(renderer.name=="작은 배 받침 다리")renderer.sharedMaterial=teal;
            }
            var catchTop=harbor.GetComponentsInChildren<Transform>(true).First(t=>t.name=="보드 앞 작은 배 받침");
            var trim=new GameObject("Coastal Workshop receiving tray trim").transform;trim.SetParent(harbor.transform,false);
            trim.position=catchTop.position;trim.rotation=catchTop.rotation;
            foreach(float x in new[]{-.603f,.603f})
                CoastalBox(trim,"Coastal Workshop tray end",new Vector3(x,0,0),new Vector3(.04f,.095f,1.2f),wood,.015f);
            foreach(float x in new[]{-.52f,.52f})foreach(float z in new[]{-.50f,.50f})
                CoastalCylinder(trim,"Coastal Workshop tray screw",new Vector3(x,.052f,z),new Vector3(.025f,.008f,.025f),brass,Quaternion.identity);
            if(!samples.SequenceEqual(harbor.samples.Select(s=>(s.point,s.inside,s.region)))||!colliders.SequenceEqual(harbor.GetComponentsInChildren<Collider>(true)))
                throw new InvalidOperationException("Coastal workshop changed shadow samples or physical supports.");
            if(frame.GetComponentsInChildren<Renderer>().Any(r=>r.shadowCastingMode!=ShadowCastingMode.Off))
                throw new InvalidOperationException("The printed canvas must not shade its own receiver.");
        }
    }
}
