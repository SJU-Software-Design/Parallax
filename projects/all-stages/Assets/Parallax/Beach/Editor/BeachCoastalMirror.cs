using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static partial void ConfigureCoastalMirror(BeachGame game)
        {
            var mirror=game.mirror;var prop=mirror.prop;var head=mirror.transform;
            foreach(string name in new[]{"거울 받침","거울 기둥"})
            {
                var renderer=prop.transform.Find(name).GetComponent<Renderer>();
                renderer.enabled=false;renderer.shadowCastingMode=ShadowCastingMode.Off;
                UnityEngine.Object.DestroyImmediate(renderer.GetComponent<Collider>());
            }
            var oldBody=head.Find("거울 몸체").GetComponent<Renderer>();
            oldBody.enabled=false;oldBody.shadowCastingMode=ShadowCastingMode.Off;
            UnityEngine.Object.DestroyImmediate(oldBody.GetComponent<Collider>());
            var metal=CoastalMaterial("Coastal Mirror brushed nickel",new Color(.65f,.66f,.61f),.78f,.44f);
            var dark=CoastalMaterial("Coastal Mirror graphite fittings",new Color(.17f,.20f,.20f),.65f,.28f);
            var enamel=CoastalMaterial("Coastal Mirror lagoon enamel",new Color(.12f,.31f,.33f),.32f,.35f);
            var coral=CoastalMaterial("Coastal Mirror coral base trim",new Color(.66f,.34f,.22f),.15f,.32f);
            var frame=CoastalMesh(head,"Coastal Mirror rounded frame and opaque backing",Vector3.zero,CoastalMirrorFrame(),metal,true);
            var bounds=frame.GetComponent<MeshFilter>().sharedMesh.bounds;var collider=frame.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
            CoastalBox(prop.transform,"Coastal Mirror weighted metal base",new Vector3(0,.065f,0),new Vector3(1.25f,.13f,.9f),metal,.045f,solid:true);
            CoastalBox(prop.transform,"Coastal Mirror coral base binding",new Vector3(0,.125f,0),new Vector3(1.23f,.045f,.88f),coral,.018f);
            CoastalBox(prop.transform,"Coastal Mirror enamel base inset",new Vector3(0,.153f,0),new Vector3(1.16f,.018f,.81f),enamel,.008f);
            CoastalCylinder(prop.transform,"Coastal Mirror swivel foot",new Vector3(0,.22f,0),new Vector3(.24f,.14f,.24f),metal,Quaternion.identity);
            CoastalCylinder(prop.transform,"Coastal Mirror swivel collar",new Vector3(0,.29f,0),new Vector3(.18f,.07f,.18f),dark,Quaternion.identity);
            var bracket=new GameObject("Coastal Mirror upright swivel bracket").transform;bracket.SetParent(head,false);
            // Cancel the head's tilt while retaining its yaw so the swivel stays on the base.
            Vector3 forward=Vector3.ProjectOnPlane(head.localRotation*Vector3.forward,Vector3.up);
            bracket.localRotation=Quaternion.Inverse(head.localRotation)*Quaternion.LookRotation(forward,Vector3.up);
            CoastalBox(bracket,"Coastal Mirror lower U rail",new Vector3(0,-1.055f,0),new Vector3(1.38f,.075f,.13f),metal,.03f,solid:true);
            foreach(float side in new[]{-1f,1f})
            {
                string name="Coastal Mirror "+(side<0?"left":"right");
                CoastalBox(bracket,name+" U upright",new Vector3(side*.825f,-.455f,0),new Vector3(.075f,.985f,.13f),metal,.03f,solid:true);
                var bend=CoastalBox(bracket,name+" U lower bend",new Vector3(side*.75f,-.986f,0),new Vector3(.075f,.235f,.13f),metal,.03f,solid:true);
                bend.transform.localRotation=Quaternion.Euler(0,0,-side*45);
                CoastalCylinder(head,name+" side pivot",new Vector3(side*.79f,0,0),new Vector3(.19f,.11f,.19f),metal,Quaternion.Euler(0,0,90));
                CoastalCylinder(head,name+" pivot cap",new Vector3(side*.855f,0,0),new Vector3(.13f,.024f,.13f),dark,Quaternion.Euler(0,0,90));
            }
        }
        static Mesh CoastalMirrorFrame()
        {
            const int steps=8,count=(steps+1)*4;
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            var sizes=new[]{new Vector2(.75f,.95f),new Vector2(.75f,.95f),new Vector2(.744f,.944f),new Vector2(.70f,.90f),new Vector2(.70f,.90f)};
            var radii=new[]{.09f,.09f,.084f,.04f,.04f};var depths=new[]{-.045f,.058f,.072f,.072f,.035f};
            for(int ring=0;ring<sizes.Length;ring++)for(int corner=0;corner<4;corner++)for(int step=0;step<=steps;step++)
            {
                float a=(corner*90+step*90f/steps)*Mathf.Deg2Rad;
                var centre=new Vector2((corner==0||corner==3?1:-1)*(sizes[ring].x-radii[ring]),(corner<2?1:-1)*(sizes[ring].y-radii[ring]));
                var p=centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radii[ring];
                vertices.Add(new Vector3(p.x,p.y,depths[ring]));uv.Add(new Vector2(p.x/1.5f+.5f,p.y/1.9f+.5f));
            }
            for(int ring=0;ring<sizes.Length-1;ring++)for(int i=0;i<count;i++)
            {
                int a=ring*count+i,b=ring*count+(i+1)%count,c=b+count,d=a+count;
                triangles.AddRange(new[]{a,b,c,a,c,d});
            }
            int front=vertices.Count;vertices.Add(new Vector3(0,0,.035f));uv.Add(Vector2.one*.5f);
            int back=vertices.Count;vertices.Add(new Vector3(0,0,-.045f));uv.Add(Vector2.one*.5f);
            for(int i=0;i<count;i++)
            {
                int j=(i+1)%count;triangles.AddRange(new[]{front,4*count+i,4*count+j,back,j,i});
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
