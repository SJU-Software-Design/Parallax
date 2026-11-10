using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static partial void ConfigureCoastalBoundaries(BeachGame game)
        {
            var colliders=game.GetComponentsInChildren<Collider>(true);
            var oldNames=new HashSet<string>{
                "후반 해안 난간 윗틀","바다를 여는 가는 난간","해안 나무 말뚝",
                "열린 해변 경계 윗틀","바다가 보이는 가는 경계 문살","해안 경계 말뚝","해안 경계 표식",
                "입구 해변 난간 윗틀","입구의 가는 경계 문살","입구 해변 말뚝"};
            int hidden=0;
            foreach(var renderer in game.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.transform.parent!=game.transform||!oldNames.Contains(renderer.name))continue;
                // Keep the existing world limit, but never its invisible safety shade.
                renderer.enabled=false;renderer.shadowCastingMode=ShadowCastingMode.Off;hidden++;
            }
            if(hidden!=570)throw new InvalidOperationException("Coastal boundary source rails changed: "+hidden);

            var wood=CoastalWood("Coastal Boundary salt-weathered timber",new Color(.82f,.76f,.63f));
            var rope=CoastalLinen("Coastal Boundary natural hemp",new Color(.72f,.61f,.43f));
            var bronze=CoastalMaterial("Coastal Boundary dark bronze fixings",new Color(.26f,.23f,.17f),.65f,.22f);
            var boundary=new GameObject("Coastal Boundary rope perimeter").transform;boundary.SetParent(game.transform,false);
            var corners=new[]{new Vector3(-14.1f,0,108),new Vector3(-14.05f,0,0),new Vector3(14.05f,0,0),new Vector3(14.1f,0,108)};
            var names=new[]{"west coast","entrance","east coast"};
            for(int run=0;run<3;run++)
            {
                var rail=new GameObject("Coastal Boundary "+names[run]).transform;rail.SetParent(boundary,false);
                rail.localPosition=corners[run];rail.localRotation=Quaternion.LookRotation(corners[run+1]-corners[run]);
                int spans=run==1?8:30;float step=Vector3.Distance(corners[run],corners[run+1])/spans;
                for(int i=run==0?0:1;i<=spans;i++)
                {
                    CoastalBox(rail,"Coastal Boundary rounded timber post",new Vector3(0,.50f,i*step),new Vector3(.24f,1.10f,.24f),wood,.09f);
                    foreach(float height in new[]{.42f,.88f})
                        CoastalCylinder(rail,"Coastal Boundary bronze rope collar",new Vector3(0,height,i*step),new Vector3(.255f,.025f,.255f),bronze,Quaternion.identity);
                }
                for(int i=0;i<spans;i++)
                {
                    var span=new GameObject("Coastal Boundary rope span "+i).transform;span.SetParent(rail,false);span.localPosition=Vector3.forward*(i*step);
                    string meshName="Coastal Boundary "+(run==1?"entrance":"coast");
                    CoastalRope(span,meshName+" upper rope",new Vector3(0,.88f,0),new Vector3(0,.88f,step),.16f,.035f,rope);
                    CoastalRope(span,meshName+" lower rope",new Vector3(0,.42f,0),new Vector3(0,.42f,step),.14f,.035f,rope);
                }
            }
            if(!colliders.SequenceEqual(game.GetComponentsInChildren<Collider>(true)))
                throw new InvalidOperationException("Coastal decoration must preserve every original gate and boundary collider and add no footholds.");
            if(boundary.GetComponentsInChildren<Renderer>().Any(r=>r.shadowCastingMode!=ShadowCastingMode.Off||r.bounds.max.y>1.06f))
                throw new InvalidOperationException("Coastal rope railings must stay below eye height and add no puzzle shade.");
        }
    }
}
