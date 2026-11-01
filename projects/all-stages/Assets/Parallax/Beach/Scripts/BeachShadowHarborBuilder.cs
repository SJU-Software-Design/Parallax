using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    public static class BeachShadowHarborBuilder
    {
        const float BoardScale=.7f;
        static readonly Vector2[] Hull={new Vector2(-1.8f,.23f),new Vector2(1.8f,.23f),new Vector2(1.35f,-.23f),new Vector2(-1.35f,-.23f)};
        static readonly Vector2[] Canopy={new Vector2(-1.45f,-.20f),new Vector2(-1.3f,-.04f),new Vector2(-.45f,.38f),new Vector2(0,.50f),new Vector2(.45f,.38f),new Vector2(1.3f,-.04f),new Vector2(1.45f,-.20f)};
        static readonly Vector2[] Mast={new Vector2(-.065f,-.19f),new Vector2(.065f,-.19f),new Vector2(.065f,-1.25f),new Vector2(-.065f,-1.25f)};

        static Material Material(string name,Color color,bool flat=false)
        {
            var material=new Material(Shader.Find(flat?"ShadeLink/Flat":"Parallax/BeachSurface")){name=name,color=color};
            if(!flat){material.SetFloat("_Glossiness",.14f);material.EnableKeyword("_EMISSION");}
            BeachWorld.Materials.Add(material);return material;
        }
        static GameObject Box(string name,Transform root,Vector3 position,Vector3 size,Material material,bool solid=false,bool shadow=false)
        {return BeachWorld.Box(name,position,size,material,root,solid,shadow);}
        static void Label(Transform root,string caption,Vector3 position,float size,Color color,Quaternion? rotation=null)
        {
            var go=new GameObject("Harbor label · "+caption);go.transform.SetParent(root,false);go.transform.position=position;
            if(rotation.HasValue)go.transform.rotation=rotation.Value;
            var label=go.AddComponent<BeachLabel>();label.caption=caption;label.size=size;label.color=color;
        }
        static Mesh ExtrudedProfile(string name,Vector2[] polygon,float height,float thickness)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();
            float area=0;for(int i=0;i<polygon.Length;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Length];area+=a.x*b.y-b.x*a.y;}
            for(int cap=0;cap<2;cap++)
            {
                int start=vertices.Count;float z=cap==0?thickness*.5f:-thickness*.5f;
                foreach(var p in polygon)vertices.Add(new Vector3(p.x*BoardScale,height+p.y*BoardScale,z));
                bool reverse=(area<0)^(cap==1);
                for(int i=1;i<polygon.Length-1;i++)
                {indices.Add(start);indices.Add(start+(reverse?i+1:i));indices.Add(start+(reverse?i:i+1));}
            }
            for(int i=0;i<polygon.Length;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Length];int start=vertices.Count;
                vertices.Add(new Vector3(a.x*BoardScale,height+a.y*BoardScale,thickness*.5f));vertices.Add(new Vector3(b.x*BoardScale,height+b.y*BoardScale,thickness*.5f));
                vertices.Add(new Vector3(b.x*BoardScale,height+b.y*BoardScale,-thickness*.5f));vertices.Add(new Vector3(a.x*BoardScale,height+a.y*BoardScale,-thickness*.5f));
                bool reverse=area>0;
                indices.Add(start);indices.Add(start+(reverse?2:1));indices.Add(start+(reverse?1:2));
                indices.Add(start);indices.Add(start+(reverse?3:2));indices.Add(start+(reverse?2:3));
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void Plate(Transform root,string name,Vector2[] profile,float height,Material material)
        {
            var item=new GameObject(name);item.transform.SetParent(root,false);
            var mesh=ExtrudedProfile(name+" mesh",profile,height,.075f);item.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=item.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            var bounds=mesh.bounds;var collider=item.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
        }
        static Vector3 ShapePivot(bool canopy,float height)
        {
            // Use the silhouette plate itself; its long stand is not the grip.
            return new Vector3(0,height+(canopy?.15f*BoardScale:0),0);
        }
        static BeachProp Piece(Transform root,string title,bool canopy,Vector3 home,float yaw,float height,Material surface,Material metal)
        {
            var host=new GameObject(title);host.transform.SetParent(root,false);
            var prop=host.AddComponent<BeachProp>();prop.title=title;prop.zone=2;prop.minScale=.4f;prop.maxScale=1.9f;prop.upright=true;
            if(canopy){Plate(host.transform,"차양 그림자 조각",Canopy,height,surface);Plate(host.transform,"차양의 긴 그림자 기둥",Mast,height,surface);}
            else Plate(host.transform,"선체 그림자 조각",Hull,height,surface);
            // Both cutouts stand on real feet. The source planes are separated
            // slightly in front of the board so their supports never overlap.
            foreach(float x in canopy?new[]{0f}:new[]{-.55f*BoardScale,.55f*BoardScale})
            {
                Box("조각의 낮은 받침",host.transform,new Vector3(x,.04f,0),new Vector3(.18f,.08f,.10f),metal,true,true);
                Box("조각 지지대",host.transform,new Vector3(x,height*.5f,0),new Vector3(.04f,height,.04f),metal,true,true);
            }
            Vector3 pivot=ShapePivot(canopy,height);
            foreach(Transform part in host.transform)part.localPosition-=pivot;
            Quaternion rotation=Quaternion.Euler(0,yaw,0);
            host.transform.SetPositionAndRotation(home+rotation*(pivot*.55f),rotation);
            host.transform.localScale=Vector3.one*.55f;prop.Remember();return prop;
        }
        static void Outline(Transform target,string name,Vector2[] polygon,Vector2 offset,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(target,false);var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=material;line.useWorldSpace=false;line.loop=true;line.positionCount=polygon.Length;
            for(int i=0;i<polygon.Length;i++)line.SetPosition(i,new Vector3(polygon[i].x+offset.x,polygon[i].y+offset.y,.012f));
            line.startWidth=line.endWidth=.026f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        }
        static BeachShadowHarbor.Sample Probe(Transform target,Vector2 point,bool inside,int region,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=inside?"윤곽 안의 그늘 확인점":"윤곽 밖의 햇빛 확인점";
            go.transform.SetParent(target,false);go.transform.localPosition=new Vector3(point.x,point.y,.013f);go.transform.localScale=Vector3.one*(inside?.043f:.034f);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return new BeachShadowHarbor.Sample{point=point,inside=inside,region=region,indicator=renderer};
        }

        public static BeachShadowHarbor Build(Transform root,BeachGame game,BeachBoat boat)
        {
            var host=new GameObject("03 · 그림자 배 제작소");host.transform.SetParent(root,false);
            var harbor=host.AddComponent<BeachShadowHarbor>();harbor.game=game;harbor.boat=boat;
            var cream=Material("Harbor shadow canvas",new Color(.94f,.90f,.78f));
            var navy=Material("Harbor shadow frame",new Color(.055f,.17f,.21f));
            var wood=Material("Harbor workshop wood",new Color(.51f,.31f,.15f));
            var coral=Material("Harbor hull sculpture",new Color(.86f,.34f,.21f));
            var teal=Material("Harbor canopy sculpture",new Color(.08f,.49f,.50f));
            var outline=Material("Harbor drawn boat outline",new Color(.88f,.40f,.18f),true);
            var probe=Material("Harbor shadow probe",new Color(1,.80f,.35f));
            var charge=Material("Harbor shadow completion",new Color(.12f,.78f,.66f));

            // The shared checkpoint roof meets these three shelter shadows over
            // the operator strip and the board's viewing position. The upright
            // receiver remains sunlit; no fixed cloth reaches its incoming rays.
            foreach(float x in new[]{1.5f,5.4f,9.3f})
                BeachResortParasol.Build("그림자 제작소 작업 쉼터 "+x,host.transform,new Vector3(x,3.3f,47.9f),new Vector2(4.8f,4.0f),0);
            Box("제작소 작업 산책로",host.transform,new Vector3(5,.04f,49.3f),new Vector3(12,.08f,1.1f),wood,true);
            for(int i=0;i<23;i++)Box("작업 산책로 나뭇결",host.transform,new Vector3(-.75f+i*.5f,.083f,49.3f),new Vector3(.025f,.006f,1.07f),navy);
            var boardCenter=new Vector3(9.5f,2.1f,52.2f);
            Vector3 normal=new Vector3(BeachLighting.ToSun.x,0,BeachLighting.ToSun.z).normalized;
            Quaternion boardRotation=Quaternion.LookRotation(normal,Vector3.up),letterRotation=Quaternion.LookRotation(-normal,Vector3.up);
            var table=new GameObject("세워진 태양빛 그림자 제작 보드");table.transform.SetParent(host.transform,false);
            table.transform.SetPositionAndRotation(boardCenter,boardRotation);table.transform.localScale=Vector3.one*BoardScale;harbor.target=table.transform;
            Box("그림자 보드 바탕",table.transform,new Vector3(0,0,-.040f),new Vector3(4.8f,3.2f,.08f),cream,true);
            foreach(float x in new[]{-2.46f,2.46f})Box("그림자 보드 옆틀",table.transform,new Vector3(x,0,-.012f),new Vector3(.12f,3.38f,.12f),navy,true);
            foreach(float y in new[]{-1.64f,1.64f})Box("그림자 보드 가로틀",table.transform,new Vector3(0,y,-.012f),new Vector3(5.04f,.12f,.12f),navy,true);
            foreach(float side in new[]{-1f,1f})
            {
                Vector3 post=boardCenter+table.transform.right*(side*2.46f*BoardScale)-normal*.12f;post.y=1.825f;
                Box("제작 게시판 목재 기둥",host.transform,post,new Vector3(.12f,3.65f,.12f),wood,true,true);
                var foot=Box("제작 게시판 받침발",host.transform,new Vector3(post.x,.06f,post.z),new Vector3(.26f,.12f,.7f),wood,true,true);
                foot.transform.rotation=boardRotation;
                foreach(float y in new[]{-1.55f,1.55f})Box("게시판 모서리 고정핀",table.transform,new Vector3(side*2.38f,y,.065f),new Vector3(.065f,.065f,.025f),cream);
            }
            Box("제작 보드 머리글",table.transform,new Vector3(0,1.99f,-.015f),new Vector3(4.85f,.50f,.12f),wood,true);
            Label(table.transform,"그림자 배 제작소",table.transform.TransformPoint(new Vector3(0,1.99f,.065f)),.25f,new Color(.98f,.94f,.77f),letterRotation);
            Label(table.transform,"선체와 차양 그림자를 맞춰 배를 완성하세요",table.transform.TransformPoint(new Vector3(0,1.47f,.045f)),.14f,new Color(.15f,.27f,.27f),letterRotation);
            Outline(table.transform,"목표 선체 윤곽",Hull,new Vector2(0,-.68f),outline);
            Outline(table.transform,"목표 차양 윤곽",Canopy,new Vector2(0,.5f),outline);
            Outline(table.transform,"목표 차양 기둥 윤곽",Mast,new Vector2(0,.5f),outline);
            var samples=new List<BeachShadowHarbor.Sample>();
            foreach(float y in new[]{-.55f,-.73f,-.84f})foreach(float x in new[]{-1.05f,-.5f,0,.5f,1.05f})
                samples.Add(Probe(table.transform,new Vector2(x,y),true,0,probe));
            foreach(float x in new[]{-1.05f,-.55f,0,.55f,1.05f})samples.Add(Probe(table.transform,new Vector2(x,.4f),true,1,probe));
            foreach(float x in new[]{-.65f,0,.65f})samples.Add(Probe(table.transform,new Vector2(x,.62f),true,1,probe));
            foreach(float x in new[]{-.18f,.18f})samples.Add(Probe(table.transform,new Vector2(x,.86f),true,1,probe));
            foreach(float y in new[]{.15f,-.05f,-.25f,-.40f})samples.Add(Probe(table.transform,new Vector2(0,y),true,1,probe));
            foreach(float x in new[]{-1.95f,1.95f})foreach(float y in new[]{-1.2f,-.6f,0,.6f,1.2f})samples.Add(Probe(table.transform,new Vector2(x,y),false,0,probe));
            foreach(float y in new[]{-1.25f,1.35f})foreach(float x in new[]{-1.4f,-.7f,0,.7f,1.4f})samples.Add(Probe(table.transform,new Vector2(x,y),false,0,probe));
            foreach(float x in new[]{-.55f,.55f})foreach(float y in new[]{-.05f,-.25f})samples.Add(Probe(table.transform,new Vector2(x,y),false,0,probe));
            foreach(float x in new[]{-1.55f,1.55f})samples.Add(Probe(table.transform,new Vector2(x,-.86f),false,0,probe));
            foreach(float x in new[]{-1.35f,1.35f})samples.Add(Probe(table.transform,new Vector2(x,.83f),false,0,probe));
            harbor.samples=samples.ToArray();
            // Matching geometry is authored from the actual directional sun:
            // moving a source d metres in front of a vertical board requires
            // its silhouette to rise d * (sun.y / horizontal sun length).
            // The short source stands rest on the same board-side worktop
            // that receives the completed miniature, leaving its approach clear.
            const float hullDistance=.20f,canopyDistance=.35f,supportHeight=.45f;
            float slope=BeachLighting.ToSun.y/new Vector2(BeachLighting.ToSun.x,BeachLighting.ToSun.z).magnitude;
            float hullHeight=boardCenter.y-.68f*BoardScale+hullDistance*slope-supportHeight;
            float canopyHeight=boardCenter.y+.5f*BoardScale+canopyDistance*slope-supportHeight;
            float yaw=boardRotation.eulerAngles.y;
            harbor.pieces=new[]{
                Piece(host.transform,"03 · 그림자 조각 A · 선체",false,new Vector3(7.4f,0,50.2f),yaw+38,hullHeight,coral,navy),
                Piece(host.transform,"03 · 그림자 조각 B · 차양",true,new Vector3(11.9f,0,50.2f),yaw-31,canopyHeight,teal,navy)
            };
            Vector3 floorCenter=new Vector3(boardCenter.x,supportHeight,boardCenter.z);
            harbor.solutionPositions=new[]{
                floorCenter+normal*hullDistance+boardRotation*ShapePivot(false,hullHeight),
                floorCenter+normal*canopyDistance+boardRotation*ShapePivot(true,canopyHeight)
            };
            harbor.solutionRotations=new[]{boardRotation,boardRotation};harbor.solutionScales=new[]{1f,1f};
            harbor.assistPlates=new BoxCollider[2];harbor.assistAnchors=new Transform[2];
            for(int i=0;i<harbor.pieces.Length;i++)
            {
                string plateName=i==0?"선체 그림자 조각":"차양 그림자 조각";
                harbor.assistPlates[i]=harbor.pieces[i].transform.Find(plateName).GetComponent<BoxCollider>();
                var anchor=new GameObject(i==0?"선체 미세 안착 기준":"차양 미세 안착 기준");anchor.transform.SetParent(host.transform,false);
                anchor.transform.SetPositionAndRotation(harbor.solutionPositions[i],boardRotation);
                harbor.assistAnchors[i]=anchor.transform;
            }

            var sign=new GameObject("게시판 아래 제작 진행판");sign.transform.SetParent(table.transform,false);sign.transform.localPosition=new Vector3(0,-1.92f,.03f);
            Box("제작 진행판",sign.transform,Vector3.zero,new Vector3(4.6f,.54f,.1f),navy);
            var status=new GameObject("그림자 충족 상태");status.transform.SetParent(sign.transform,false);status.transform.localPosition=new Vector3(0,.06f,.065f);status.transform.localRotation=Quaternion.Euler(0,180,0);harbor.statusAnchor=status.transform;
            // TextMesh and the animated fill use their visible -Z face. This
            // carrier lets the existing fill animation face the board operator.
            var fillCarrier=new GameObject("전면을 향한 충전 막대");fillCarrier.transform.SetParent(sign.transform,false);fillCarrier.transform.localPosition=new Vector3(0,0,.10f);fillCarrier.transform.localRotation=Quaternion.Euler(0,180,0);
            var fill=new GameObject("그림자 완성 충전");fill.transform.SetParent(fillCarrier.transform,false);harbor.chargeFill=fill.transform;fill.transform.localScale=new Vector3(.001f,1,1);fill.transform.localPosition=new Vector3(-1.948f,-.26f,-.075f);
            Box("충전 막대",fill.transform,Vector3.zero,new Vector3(3.9f,.075f,.025f),charge);

            Vector3 catchPoint=boardCenter+normal*.45f;catchPoint.y=supportHeight-.05f;
            Box("보드 앞 작은 배 받침",host.transform,catchPoint,new Vector3(1.25f,.10f,1.20f),wood,true,true);
            foreach(float x in new[]{-.45f,.45f})foreach(float z in new[]{-.40f,.40f})
                Box("작은 배 받침 다리",host.transform,new Vector3(catchPoint.x+x,.175f,catchPoint.z+z),new Vector3(.09f,.35f,.09f),navy,true,true);
            Box("배 받침 앞표식",host.transform,new Vector3(catchPoint.x,.46f,catchPoint.z-.45f),new Vector3(.65f,.012f,.12f),cream);
            var release=new GameObject("완성된 배가 보드에서 나오는 자리");release.transform.SetParent(table.transform,false);release.transform.localPosition=new Vector3(0,-.68f,.28f);release.transform.rotation=boardRotation*Quaternion.Euler(0,90,0);harbor.boatReleaseAnchor=release.transform;
            var drop=new GameObject("작은 배 실제 낙하 시작점");drop.transform.SetParent(host.transform,false);drop.transform.position=boat.Prop.homePosition+Vector3.up;harbor.dropAnchor=drop.transform;
            harbor.boardBoatScale=new Vector3(.08f,.55f,1.2f);
            Label(host.transform,"A · 선체",new Vector3(7.4f,.28f,49.95f),.12f,new Color(1,.78f,.43f));
            Label(host.transform,"B · 차양",new Vector3(11.9f,.28f,49.95f),.12f,new Color(.45f,.95f,.92f));
            return harbor;
        }
    }
}
