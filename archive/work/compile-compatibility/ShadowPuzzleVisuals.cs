using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink
{
    public sealed class ShadowPuzzleVisuals : MonoBehaviour
    {
        public Camera view;
        public Texture2D dogTexture;
        readonly List<Object> assets = new List<Object>();
        Transform[] pieces;
        Material[] pieceMaterials;
        Mesh[] shadowMeshes;
        GameObject[] shadowObjects;
        Transform tokenFloor, tokenHand, frameDog, restoredDetails, doorPanel, hintRoot;
        Material tokenMaterial, doorMaterial, frameDogMaterial, textMaterial, lineMaterial;
        TextMesh doorText;
        Font font;
        ShadowStage lastStage = (ShadowStage)(-1);
        Material Lit(string name,Color color,float metal=0)
        {
            var m = new Material(Shader.Find("Standard")) { name=name,color=color };
            m.SetFloat("_Metallic",metal); m.SetFloat("_Glossiness",.26f); assets.Add(m); return m;
        }
        Material Flat(string name,Color color)
        { var m = new Material(Shader.Find("ShadeLink/Flat")) { name=name,color=color }; assets.Add(m); return m; }
        Transform Root(string name,Transform parent=null)
        { var go = new GameObject(name); go.transform.SetParent(parent == null ? transform : parent,false); return go.transform; }
        GameObject Box(string name,Vector3 position,Vector3 size,Material material,bool cast=true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(transform,false);
            go.transform.localPosition=ShadeVisuals.ToUnity(position); go.transform.localScale=size; Destroy(go.GetComponent<Collider>());
            var r=go.GetComponent<MeshRenderer>(); r.sharedMaterial=material; r.shadowCastingMode=cast?ShadowCastingMode.On:ShadowCastingMode.Off; r.receiveShadows=cast; return go;
        }
        TextMesh Text(string label,Vector3 at,float size,Color color,Quaternion rotation)
        {
            var root=Root(label); root.localPosition=ShadeVisuals.ToUnity(at); root.localRotation=rotation;
            var text=root.gameObject.AddComponent<TextMesh>(); text.text=label; text.font=font; text.fontSize=64;
            text.characterSize=size/6.4f; text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center; text.color=color;
            var r=root.GetComponent<MeshRenderer>(); r.sharedMaterial=textMaterial; r.shadowCastingMode=ShadowCastingMode.Off;
            return text;
        }
        Mesh MeshObject(string name,Transform parent,Material material,out GameObject go)
        {
            go=Root(name,parent).gameObject; var mesh=new Mesh { name=name }; assets.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>(); r.sharedMaterial=material; r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false; return mesh;
        }
        static void Polygon(Mesh mesh,List<Vector2> polygon,float y)
        {
            var vertices=new Vector3[polygon.Count]; var indices=new int[(polygon.Count-2)*3];
            for(int i=0;i<polygon.Count;i++) vertices[i]=new Vector3(-polygon[i].x,y,polygon[i].y);
            for(int i=0;i<polygon.Count-2;i++) { indices[i*3]=0; indices[i*3+1]=i+1; indices[i*3+2]=i+2; }
            mesh.Clear(); mesh.vertices=vertices; mesh.triangles=indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        void Composite(string name,Transform parent,List<Vector2>[] polys,Material material)
        {
            for(int i=0;i<polys.Length;i++) Polygon(MeshObject(name+" "+i,parent,material,out _),polys[i],i*.0001f);
        }
        void Line(string name,List<Vector2> points,Color color,Transform parent)
        {
            var line=Root(name,parent).gameObject.AddComponent<LineRenderer>(); line.sharedMaterial=lineMaterial;
            line.useWorldSpace=false; line.loop=true; line.widthMultiplier=.025f; line.positionCount=points.Count;
            line.startColor=line.endColor=color; line.shadowCastingMode=ShadowCastingMode.Off;
            for(int i=0;i<points.Count;i++) line.SetPosition(i,new Vector3(-points[i].x,.026f,points[i].y));
        }
        public void Build(Font koreanFont)
        {
            font=koreanFont; textMaterial=new Material(Shader.Find("ShadeLink/WorldText")); assets.Add(textMaterial);
            textMaterial.mainTexture=font.material.mainTexture; Font.textureRebuilt+=RefreshFont;
            lineMaterial=new Material(Shader.Find("Sprites/Default")); assets.Add(lineMaterial);
            var plaster=Lit("Gallery limestone",new Color(.76f,.75f,.68f));
            var floor=Lit("Warm gallery floor",new Color(.68f,.66f,.58f));
            var dark=Lit("Graphite frame",new Color(.065f,.10f,.12f),.25f);
            var brass=Lit("Frame brass",new Color(.72f,.47f,.22f),.6f);
            var board=Flat("Composition board",new Color(.49f,.54f,.51f));
            var shadow=Flat("Projected geometry",new Color(.075f,.095f,.105f));
            var guide=Flat("Empty dog outline",new Color(.57f,.62f,.57f));
            var paper=Flat("Portrait paper",new Color(.87f,.84f,.73f));
            tokenMaterial=Flat("Solidified shadow",new Color(.035f,.075f,.085f));
            frameDogMaterial=Flat("Portrait silhouette",new Color(.065f,.10f,.115f));
            doorMaterial=Flat("Door lock light",ShadeVisuals.Amber);
            QualitySettings.antiAliasing=4; QualitySettings.vSyncCount=1; QualitySettings.shadowDistance=35;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.56f,.65f,.69f); RenderSettings.ambientEquatorColor=new Color(.42f,.45f,.46f);
            RenderSettings.ambientGroundColor=new Color(.25f,.25f,.22f);
            var ambient=new SphericalHarmonicsL2(); ambient.AddAmbientLight(new Color(.52f,.56f,.57f)); RenderSettings.ambientProbe=ambient;
            RenderSettings.fog=false;
            var light=Root("Gallery sunlight").gameObject.AddComponent<Light>(); light.type=LightType.Directional;
            light.color=new Color(1,.91f,.76f); light.intensity=1.15f; light.shadows=LightShadows.Soft;
            light.transform.rotation=Quaternion.LookRotation(ShadeVisuals.ToUnity(Rules.Sun)); RenderSettings.sun=light;
            Box("Floor",new Vector3(0,-.15f,0),new Vector3(14,.3f,17.6f),floor,false);
            Box("North wall",new Vector3(0,3,-8.75f),new Vector3(14.5f,6,.3f),plaster);
            Box("South wall",new Vector3(0,3,8.8f),new Vector3(14.5f,6,.3f),plaster);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Side wall",new Vector3(side*7.15f,3,0),new Vector3(.3f,6,17.6f),plaster);
                Box("Brass wall rail",new Vector3(side*6.97f,1.1f,0),new Vector3(.025f,.04f,17.4f),brass,false);
            }
            for(int z=-8;z<=8;z+=2) Box("Floor joint",new Vector3(0,.001f,z),new Vector3(14,.001f,.018f),board,false);
            Box("Shadow composition mat",new Vector3(0,.006f,-.5f),new Vector3(6.7f,.008f,5.8f),board,false);
            var guideRoot=Root("Target silhouette on floor"); Composite("Target",guideRoot,ShadowPuzzleRules.Target,guide);
            guideRoot.localPosition=Vector3.up*.013f;
            Line("Mat border",new FloorRect(-3.35f,3.35f,-3.4f,2.4f).Polygon(),new Color(.73f,.75f,.62f),transform);
            hintRoot=Root("Optional part outlines");
            for(int i=0;i<ShadowPuzzleRules.Shapes.Length;i++) Line("Hint "+i,ShadowPuzzleRules.Target[i],ShadowPuzzleRules.Shapes[i].color,hintRoot);
            Text("그림자를 모아 강아지를 완성하세요",new Vector3(0,.022f,2.07f),.34f,new Color(.17f,.24f,.24f),Quaternion.Euler(90,180,0));
            Text("PARALLAX   /   SHADOW GALLERY",new Vector3(0,5.25f,-8.56f),.43f,new Color(.22f,.30f,.30f),Quaternion.Euler(0,180,0));
            Text("잃어버린 강아지",new Vector3(0,4.65f,-8.54f),.46f,new Color(.22f,.30f,.30f),Quaternion.Euler(0,180,0));
            Box("Portrait frame",ShadowPuzzleRules.Frame.center,ShadowPuzzleRules.Frame.size,brass,false);
            Box("Portrait paper",new Vector3(0,2.45f,-8.14f),new Vector3(5.48f,3.48f,.025f),paper,false);
            frameDog=Root("Dog portrait"); Composite("Dog",frameDog,ShadowPuzzleRules.Target,frameDogMaterial);
            frameDog.localPosition=new Vector3(0,2.1f,-8.10f); frameDog.localRotation=Quaternion.Euler(90,0,0); frameDog.localScale=Vector3.one*.78f;
            restoredDetails=Root("Restored portrait details");
            AddPortraitDetail(new Vector2(1.12f,-1.58f),.075f,shadow);
            AddPortraitDetail(new Vector2(2.035f,-.97f),.075f,shadow);
            var collar=Flat("Dog teal collar",ShadeVisuals.Teal);
            var collarPoly=new List<Vector2>{new Vector2(.51f,-.70f),new Vector2(1.10f,-.70f),new Vector2(1.10f,-.55f),new Vector2(.51f,-.55f)};
            var collarMesh=MeshObject("Collar",restoredDetails,collar,out _); Polygon(collarMesh,Rules.Hull(collarPoly),0);
            restoredDetails.SetParent(frameDog,false); restoredDetails.localPosition=Vector3.up*.008f;
            Text("그림자를 가져와 액자를 채워주세요",new Vector3(0,.32f,-8.08f),.29f,new Color(.26f,.32f,.30f),Quaternion.Euler(0,180,0));
            Box("Door aperture",new Vector3(5,1.6f,-8.51f),new Vector3(2.3f,3.3f,.05f),dark,false);
            doorPanel=Box("Locked door",new Vector3(5,1.6f,-8.30f),new Vector3(2.05f,3.1f,.12f),dark,false).transform;
            Box("Door status left",new Vector3(3.86f,1.6f,-8.29f),new Vector3(.045f,3.3f,.05f),doorMaterial,false);
            Box("Door status right",new Vector3(6.14f,1.6f,-8.29f),new Vector3(.045f,3.3f,.05f),doorMaterial,false);
            Box("Door status top",new Vector3(5,3.25f,-8.29f),new Vector3(2.3f,.045f,.05f),doorMaterial,false);
            doorText=Text("잠김",new Vector3(5,3.75f,-8.30f),.43f,ShadeVisuals.Amber,Quaternion.Euler(0,180,0));
            var defs=ShadowPuzzleRules.Shapes; pieces=new Transform[defs.Length]; pieceMaterials=new Material[defs.Length];
            shadowMeshes=new Mesh[defs.Length]; shadowObjects=new GameObject[defs.Length];
            for(int i=0;i<defs.Length;i++)
            {
                pieceMaterials[i]=Lit(defs[i].name,defs[i].color,.22f); pieces[i]=Root(defs[i].name);
                var mesh=MeshObject("Geometric solid",pieces[i],pieceMaterials[i],out var solid);
                // Duplicate vertices per triangle for crisp prism edges. Mirroring
                // the browser coordinate system also reverses triangle winding.
                var vertices=new Vector3[defs[i].triangles.Length]; var indices=new int[vertices.Length];
                for(int t=0;t<vertices.Length;t+=3)
                {
                    vertices[t]=ShadeVisuals.ToUnity(defs[i].vertices[defs[i].triangles[t]]);
                    vertices[t+1]=ShadeVisuals.ToUnity(defs[i].vertices[defs[i].triangles[t+2]]);
                    vertices[t+2]=ShadeVisuals.ToUnity(defs[i].vertices[defs[i].triangles[t+1]]);
                    indices[t]=t; indices[t+1]=t+1; indices[t+2]=t+2;
                }
                mesh.vertices=vertices; mesh.triangles=indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                solid.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.On;
                shadowMeshes[i]=MeshObject("Actual floor shadow "+i,transform,shadow,out shadowObjects[i]);
            }
            tokenFloor=Root("Collectible dog floor shadow"); tokenHand=Root("Carried dog shadow");
            var camera=Root("Player camera"); view=camera.gameObject.AddComponent<Camera>(); camera.gameObject.AddComponent<AudioListener>();
            view.fieldOfView=68; view.nearClipPlane=.035f; view.farClipPlane=60; view.allowMSAA=true;
            view.clearFlags=CameraClearFlags.SolidColor; view.backgroundColor=new Color(.57f,.65f,.69f);
            tokenHand.SetParent(view.transform,false); tokenHand.localPosition=new Vector3(.25f,-.29f,.86f);
            tokenHand.localRotation=Quaternion.Euler(90,180,0); tokenHand.localScale=Vector3.one*.14f;
            BuildDogTexture();
        }
        void AddPortraitDetail(Vector2 p,float radius,Material material)
        {
            var poly=new List<Vector2>();
            for(int i=0;i<20;i++) { float a=i*3.14159274f*2/20; poly.Add(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius); }
            Polygon(MeshObject("Portrait detail",restoredDetails,material,out _),Rules.Hull(poly),0);
        }
        void BuildDogTexture()
        {
            dogTexture=new Texture2D(240,190,TextureFormat.RGBA32,false); assets.Add(dogTexture); var pixels=new Color32[240*190];
            for(int y=0;y<190;y++) for(int x=0;x<240;x++)
            {
                Vector2 p=new Vector2(-2.8f+x/239f*5.4f,1.65f-y/189f*4.6f);
                pixels[y*240+x]=ShadowPuzzleRules.Contains(p,ShadowPuzzleRules.Target)?new Color32(28,42,46,255):new Color32(218,214,193,255);
            }
            dogTexture.SetPixels32(pixels); dogTexture.Apply();
        }
        void RefreshFont(Font changed) { if(changed==font) textMaterial.mainTexture=font.material.mainTexture; }
        public void Sync(ShadowPuzzleGame game)
        {
            var s=game.state; view.transform.SetPositionAndRotation(ShadeVisuals.ToUnity(s.player.Eye),Quaternion.LookRotation(ShadeVisuals.ToUnity(Rules.Look(s.yaw,s.pitch))));
            hintRoot.gameObject.SetActive(game.hints && s.stage==ShadowStage.Arranging);
            for(int i=0;i<pieces.Length;i++)
            {
                var p=s.pieces[i]; pieces[i].localPosition=new Vector3(-p.position.x,0,p.position.y); pieces[i].localScale=Vector3.one*p.scale;
                bool selected=s.held!=null?s.held.index==i:game.aimed==i && s.stage==ShadowStage.Arranging;
                pieceMaterials[i].EnableKeyword("_EMISSION"); pieceMaterials[i].SetColor("_EmissionColor",selected?(game.blocked?new Color(.45f,.05f,.01f):new Color(.09f,.27f,.23f)):Color.black);
                Polygon(shadowMeshes[i],ShadowPuzzleRules.Shadow(p),.018f+i*.0001f);
                shadowObjects[i].SetActive(s.stage==ShadowStage.Arranging);
            }
            if(lastStage!=s.stage)
            {
                if(s.stage==ShadowStage.Ready && tokenFloor.childCount==0 && s.frozen!=null)
                { Composite("Frozen silhouette",tokenFloor,s.frozen,tokenMaterial); Composite("Carried silhouette",tokenHand,s.frozen,tokenMaterial); }
                if(s.stage==ShadowStage.Arranging)
                {
                    // Meshes are disposed on reset, not accumulated on every run.
                    ClearComposite(tokenFloor); ClearComposite(tokenHand);
                }
                lastStage=s.stage;
            }
            tokenFloor.gameObject.SetActive(s.stage==ShadowStage.Ready);
            tokenFloor.localPosition=new Vector3(-s.tokenOffset.x,.033f,s.tokenOffset.y); tokenFloor.localScale=Vector3.one*s.tokenScale;
            tokenHand.gameObject.SetActive(s.stage==ShadowStage.Carrying);
            tokenMaterial.color=s.stage==ShadowStage.Ready?Color.Lerp(new Color(.055f,.12f,.13f),new Color(.14f,.48f,.43f),.45f+.25f*Mathf.Sin(Time.unscaledTime*3)):new Color(.035f,.08f,.09f);
            restoredDetails.gameObject.SetActive(s.DoorUnlocked);
            frameDogMaterial.color=s.DoorUnlocked?new Color(.73f,.40f,.17f):new Color(.065f,.10f,.115f);
            doorPanel.gameObject.SetActive(!s.DoorUnlocked); doorMaterial.color=s.DoorUnlocked?ShadeVisuals.Teal:ShadeVisuals.Amber;
            doorText.text=s.DoorUnlocked?"출구 열림":"잠김"; doorText.color=doorMaterial.color;
        }
        void ClearComposite(Transform root)
        {
            foreach(Transform child in root)
            { var mesh=child.GetComponent<MeshFilter>().sharedMesh; assets.Remove(mesh); Destroy(mesh); Destroy(child.gameObject); }
        }
        void OnDestroy()
        { Font.textureRebuilt-=RefreshFont; foreach(var asset in assets) if(asset!=null) Destroy(asset); }
    }
}
