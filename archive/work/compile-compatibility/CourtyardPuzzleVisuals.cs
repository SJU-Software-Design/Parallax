using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink
{
    public sealed class CourtyardPuzzleVisuals : MonoBehaviour
    {
        readonly List<Object> assets=new List<Object>();
        Transform[] pieces;
        Material[] colors;
        Mesh[] shadows;
        GameObject[] shadowObjects;
        Transform floorToken,handToken,hints,frameDog,details,lockBar;
        Material shadowMaterial,tokenMaterial,portraitMaterial,textMaterial,lineMaterial;
        Font font;
        TextMesh caption;
        ShadowStage lastStage=(ShadowStage)(-1);
        Transform Root(string name,Transform parent=null)
        { var root=new GameObject(name).transform; root.SetParent(parent==null?transform:parent,false); return root; }
        Material Flat(string name,Color color)
        { var m=new Material(Shader.Find("ShadeLink/Flat")){name=name,color=color};assets.Add(m);return m; }
        Material Lit(string name,Color color)
        { var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetFloat("_Metallic",.2f);m.SetFloat("_Glossiness",.3f);assets.Add(m);return m; }
        Transform Box(string name,Vector3 at,Vector3 size,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(transform,false);
            obj.transform.localPosition=ShadeVisuals.ToUnity(at);obj.transform.localScale=size;Destroy(obj.GetComponent<Collider>());
            var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return obj.transform;
        }
        TextMesh Text(string value,Vector3 at,float size,Color color,Quaternion rotation)
        {
            var root=Root(value);root.localPosition=ShadeVisuals.ToUnity(at);root.localRotation=rotation;
            var mesh=root.gameObject.AddComponent<TextMesh>();mesh.text=value;mesh.font=font;mesh.fontSize=64;mesh.characterSize=size/6.4f;
            mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=color;
            var r=root.GetComponent<MeshRenderer>();r.sharedMaterial=textMaterial;r.shadowCastingMode=ShadowCastingMode.Off;return mesh;
        }
        Mesh MeshObject(string name,Transform parent,Material material,out GameObject obj)
        {
            obj=Root(name,parent).gameObject;var mesh=new Mesh{name=name};assets.Add(mesh);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return mesh;
        }
        static void Polygon(Mesh mesh,List<Vector2> polygon,float y)
        {
            var v=new Vector3[polygon.Count];var t=new int[(polygon.Count-2)*3];
            for(int i=0;i<v.Length;i++)v[i]=new Vector3(-polygon[i].x,y,polygon[i].y);
            for(int i=0;i<v.Length-2;i++){t[i*3]=0;t[i*3+1]=i+1;t[i*3+2]=i+2;}
            mesh.Clear();mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        void Composite(Transform root,List<Vector2>[] polygons,Material material)
        { for(int i=0;i<polygons.Length;i++)Polygon(MeshObject("Silhouette "+i,root,material,out _),polygons[i],i*.0001f); }
        public void Build(CourtyardShadowPuzzle puzzle,Font koreanFont,Camera camera)
        {
            font=koreanFont;textMaterial=new Material(Shader.Find("ShadeLink/WorldText"));assets.Add(textMaterial);
            textMaterial.mainTexture=font.material.mainTexture;Font.textureRebuilt+=RefreshFont;
            lineMaterial=new Material(Shader.Find("Sprites/Default"));assets.Add(lineMaterial);
            var brass=Lit("Portrait brass",new Color(.75f,.47f,.22f));var paper=Flat("Portrait paper",new Color(.87f,.84f,.71f));
            var dark=Flat("Cutout details",new Color(.04f,.065f,.07f));
            shadowMaterial=Flat("Puzzle shadow",new Color(.055f,.11f,.12f));
            tokenMaterial=Flat("Earned dog shadow",new Color(.10f,.36f,.32f));
            portraitMaterial=Flat("Missing dog",new Color(.065f,.10f,.11f));
            var mat=Flat("Entry work mat",new Color(.16f,.28f,.28f));
            Box("Entry composition board",new Vector3(-5.15f,.012f,13.25f),new Vector3(3.6f,.006f,3.1f),mat);
            var guide=Root("Dog target in starting shade");Composite(guide,ShadowPuzzleRules.Target,Flat("Target guide",new Color(.27f,.40f,.36f)));
            guide.localPosition=new Vector3(-CourtyardShadowPuzzle.Origin.x,.017f,CourtyardShadowPuzzle.Origin.y);guide.localScale=Vector3.one*CourtyardShadowPuzzle.Unit;
            Text("그림자 조합",new Vector3(-5.2f,.020f,14.52f),.20f,ShadeVisuals.Teal,Quaternion.Euler(90,180,0));
            hints=Root("Part hints");hints.localPosition=guide.localPosition;hints.localScale=guide.localScale;
            for(int i=0;i<ShadowPuzzleRules.Shapes.Length;i++)
            {
                var shape=ShadowPuzzleRules.Shapes[i];var line=Root(shape.name,hints).gameObject.AddComponent<LineRenderer>();
                line.sharedMaterial=lineMaterial;line.useWorldSpace=false;line.loop=true;line.widthMultiplier=.025f;
                line.startColor=line.endColor=shape.color;line.positionCount=ShadowPuzzleRules.Target[i].Count;
                for(int n=0;n<line.positionCount;n++){Vector2 p=ShadowPuzzleRules.Target[i][n];line.SetPosition(n,new Vector3(-p.x,.025f,p.y));}
            }
            Box("Exit picture frame",CourtyardShadowPuzzle.Frame.center,CourtyardShadowPuzzle.Frame.size,brass);
            Box("Exit picture paper",new Vector3(1.95f,1.8f,-15.795f),new Vector3(2.10f,2.10f,.02f),paper);
            frameDog=Root("Exit dog portrait");Composite(frameDog,ShadowPuzzleRules.Target,portraitMaterial);
            frameDog.localPosition=ShadeVisuals.ToUnity(new Vector3(2,1.57f,-15.775f));frameDog.localRotation=Quaternion.Euler(90,0,0);frameDog.localScale=Vector3.one*.41f;
            details=Root("Restored dog details",frameDog);details.localPosition=Vector3.up*.007f;
            foreach(Vector2 center in new[]{new Vector2(1.12f,-1.58f),new Vector2(2.035f,-.97f)})
            {
                var polygon=new List<Vector2>();for(int i=0;i<20;i++){float a=i*3.14159274f*2/20;polygon.Add(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.075f);}
                Polygon(MeshObject("Eye or nose",details,dark,out _),Rules.Hull(polygon),0);
            }
            Polygon(MeshObject("Collar",details,Flat("Teal collar",ShadeVisuals.Teal),out _),new FloorRect(.51f,1.10f,-.70f,-.55f).Polygon(),0);
            caption=Text("그림자를 가져오세요",new Vector3(1.95f,3.08f,-15.77f),.24f,ShadeVisuals.Amber,Quaternion.Euler(0,180,0));
            lockBar=Box("Exit locked until portrait restored",new Vector3(4.65f,3.65f,-15.895f),new Vector3(1.76f,.11f,.035f),Flat("Locked exit amber",ShadeVisuals.Amber));
            pieces=new Transform[puzzle.state.pieces.Length];colors=new Material[pieces.Length];shadows=new Mesh[pieces.Length];shadowObjects=new GameObject[pieces.Length];
            for(int i=0;i<pieces.Length;i++)
            {
                var shape=ShadowPuzzleRules.Shapes[i];pieces[i]=Root(shape.name);colors[i]=Lit(shape.name,shape.color);
                var mesh=MeshObject("Solid plate",pieces[i],colors[i],out _);var v=new Vector3[shape.triangles.Length];var t=new int[v.Length];
                for(int n=0;n<v.Length;n+=3)
                { v[n]=ShadeVisuals.ToUnity(shape.vertices[shape.triangles[n]]);v[n+1]=ShadeVisuals.ToUnity(shape.vertices[shape.triangles[n+2]]);v[n+2]=ShadeVisuals.ToUnity(shape.vertices[shape.triangles[n+1]]);t[n]=n;t[n+1]=n+1;t[n+2]=n+2; }
                mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();
                shadows[i]=MeshObject("Projected shadow "+i,transform,shadowMaterial,out shadowObjects[i]);
            }
            floorToken=Root("Dog collectible on floor");handToken=Root("Carried dog",camera.transform);
            handToken.localPosition=new Vector3(.23f,-.25f,.9f);handToken.localRotation=Quaternion.Euler(90,180,0);handToken.localScale=Vector3.one*.11f;
        }
        void Clear(Transform root)
        { foreach(Transform child in root){var mesh=child.GetComponent<MeshFilter>().sharedMesh;assets.Remove(mesh);Destroy(mesh);Destroy(child.gameObject);} }
        public void Sync(CourtyardShadowPuzzle puzzle)
        {
            var state=puzzle.state;int selected=puzzle.AimPiece(out _);
            for(int i=0;i<pieces.Length;i++)
            {
                var p=puzzle.worldPieces[i];pieces[i].localPosition=ShadeVisuals.ToUnity(new Vector3(p.position.x,0,p.position.y));pieces[i].localScale=Vector3.one*p.scale;
                bool highlight=state.held!=null?state.held.index==i:selected==i && state.stage==ShadowStage.Arranging;
                colors[i].EnableKeyword("_EMISSION");colors[i].SetColor("_EmissionColor",highlight?(puzzle.blocked?new Color(.35f,.04f,.01f):new Color(.07f,.20f,.18f)):Color.black);
                Polygon(shadows[i],puzzle.projected[i],.023f+i*.0001f);shadowObjects[i].SetActive(state.stage==ShadowStage.Arranging);
            }
            if(lastStage!=state.stage)
            {
                if(state.stage==ShadowStage.Ready && floorToken.childCount==0)
                {Composite(floorToken,state.frozen,tokenMaterial);Composite(handToken,state.frozen,tokenMaterial);}
                if(state.stage==ShadowStage.Arranging){Clear(floorToken);Clear(handToken);}lastStage=state.stage;
            }
            hints.gameObject.SetActive(puzzle.hints && state.stage==ShadowStage.Arranging);
            floorToken.gameObject.SetActive(state.stage==ShadowStage.Ready);handToken.gameObject.SetActive(puzzle.Carrying);
            floorToken.localPosition=new Vector3(-puzzle.tokenAnchor.x,.031f,puzzle.tokenAnchor.y);floorToken.localScale=Vector3.one*CourtyardShadowPuzzle.Unit*state.tokenScale;
            tokenMaterial.color=state.stage==ShadowStage.Ready?Color.Lerp(new Color(.055f,.18f,.18f),ShadeVisuals.Teal,.3f+.15f*Mathf.Sin(Time.unscaledTime*3)):new Color(.035f,.07f,.08f);
            portraitMaterial.color=puzzle.Restored?new Color(.73f,.40f,.17f):new Color(.065f,.10f,.11f);
            details.gameObject.SetActive(puzzle.Restored);lockBar.gameObject.SetActive(!puzzle.Restored);
            caption.text=puzzle.Restored?"액자 복원 · 출구 잠금 해제":"그림자를 가져오세요";caption.color=puzzle.Restored?ShadeVisuals.Teal:ShadeVisuals.Amber;
        }
        void RefreshFont(Font updated){if(updated==font)textMaterial.mainTexture=font.material.mainTexture;}
        void OnDestroy(){Font.textureRebuilt-=RefreshFont;foreach(var asset in assets)if(asset!=null)Destroy(asset);}
    }
}
