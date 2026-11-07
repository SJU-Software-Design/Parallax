using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using ShadeLink.Gallery;
using ShadeLink.Tutorial;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    /// <summary>Creates a Unity scene preview of the key-shadow room blockout.</summary>
    public static class KeyShadowPreviewBuild
    {
        const string ScenePath = "Assets/Parallax/Tutorial/Scenes/KeyShadowPreview.unity";
        const string AssetDir = "Assets/Parallax/Tutorial/Resources/Tutorial";
        const string RingPath = AssetDir + "/ToastedDonutPreview.asset";
        const string DoughTexturePath = AssetDir + "/ToastedDonutTexture.asset";
        const string KeyStencilPath = AssetDir + "/KeyStencilPreview.asset";

        [MenuItem("Parallax/열쇠 그림자 프리뷰 만들기")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(AssetDir);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var concrete = Material("Preview · concrete", new Color(.43f,.49f,.50f), .4f);
            var wall = Material("Preview · warm grey", new Color(.60f,.62f,.58f), .35f);
            var steel = Material("Preview · brushed steel", new Color(.66f,.72f,.72f), .7f);
            var wood = Material("Preview · warm walnut", new Color(.44f,.25f,.12f), .43f);
            var white = Material("Preview · weathered white door", new Color(.92f,.92f,.85f), .32f);
            white.shader=Shader.Find("Unlit/Color");
            var inset = Material("Preview · pale blue inset", new Color(.73f,.81f,.81f), .38f);
            inset.shader=Shader.Find("Unlit/Color");
            var brass = Material("Preview · aged brass", new Color(.63f,.43f,.16f), .66f);
            brass.shader=Shader.Find("Unlit/Color");
            var dark = Material("Preview · keyhole recess", new Color(.035f,.04f,.04f), .15f);
            var paper = Material("Preview · white wall paper", new Color(.96f,.96f,.92f), .12f);

            BuildRoom(concrete, wall, steel);
            var keyLight = BuildWallLight();
            var table = BuildTable(wood, steel, out var donut);
            var tableProp = table.gameObject.AddComponent<GalleryProp>();
            tableProp.title = "ㅠ-shaped table";
            tableProp.platform = true;
            var donutProp = donut.gameObject.AddComponent<GalleryProp>();
            donutProp.title = "Glazed donut";
            var doorPivot = BuildDoor(white, inset, brass, dark, steel);
            var receiver = BuildTarget(paper);
            var matcher = new GameObject("Puzzle · live key-shadow matcher").AddComponent<KeyShadowMatcher>();
            matcher.Configure(keyLight, receiver, table, donut);
            var controller = BuildPlayer(new[] { tableProp, donutProp }, matcher);
            var sequence = new GameObject("Puzzle · key reward and door lock").AddComponent<KeyShadowSequence>();
            sequence.Configure(matcher, receiver, new[] { tableProp, donutProp }, controller,
                doorPivot, new Vector3(.88f,2.67f,5.57f));

            KeyShadowRoomFinish.Apply(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("Key-shadow preview saved: " + ScenePath);
        }

        static Material Material(string name, Color color, float smoothness)
        {
            var m = new Material(Shader.Find("Standard")) { name=name, color=color };
            m.SetFloat("_Glossiness",smoothness); return m;
        }
        static GameObject Box(string name, Vector3 position, Vector3 size, Material material,
            Transform parent=null, bool collider=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
            if(!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false); go.transform.position=position; go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material; return go;
        }
        static void BuildRoom(Material floor, Material wall, Material trim)
        {
            Box("Room · floor",new Vector3(0,-.12f,0),new Vector3(18,.24f,16),floor);
            // The freestanding door will show its separate corridor through a portal;
            // the room's back wall must remain solid when viewed from the side.
            Box("Room · back wall left of door",new Vector3(-5.3f,4.5f,7.8f),new Vector3(7.4f,9,.35f),wall);
            Box("Room · back wall right of door",new Vector3(5.3f,4.5f,7.8f),new Vector3(7.4f,9,.35f),wall);
            Box("Room · back wall behind door",new Vector3(0,4.5f,7.8f),new Vector3(3.2f,9f,.35f),wall);
            Box("Room · front wall",new Vector3(0,4.5f,-7.8f),new Vector3(18f,9f,.35f),wall);
            Box("Room · left wall",new Vector3(-8.8f,4.5f,0),new Vector3(.35f,9,16),wall);
            Box("Room · right wall",new Vector3(8.8f,4.5f,0),new Vector3(.35f,9,16),wall);
            Box("Room · closed ceiling",new Vector3(0,9.05f,0),new Vector3(18,.3f,16),trim);
            for(int i=-8;i<=8;i+=2) Box("Fine floor seam",new Vector3(i,.004f,0),new Vector3(.018f,.006f,15.5f),trim,null,false);
        }
        static Light BuildWallLight()
        {
            var casing=Material("Modern wall lamp · graphite housing",new Color(.12f,.14f,.15f),.38f);
            var diffuser=new Material(Shader.Find("Unlit/Color")){name="Modern wall lamp · warm diffuser",color=new Color(1f,.82f,.60f)};
            var housing=Box("Modern wall lamp · graphite housing",new Vector3(8.52f,.72f,-3.862f),new Vector3(.15f,1.36f,.68f),casing,null,false);
            var housingRenderer=housing.GetComponent<Renderer>();housingRenderer.shadowCastingMode=ShadowCastingMode.Off;housingRenderer.receiveShadows=false;
            var diffuserPanel=Box("Modern wall lamp · warm diffuser",new Vector3(8.405f,.42f,-3.862f),new Vector3(.055f,.78f,.42f),diffuser,null,false);
            var diffuserRenderer=diffuserPanel.GetComponent<Renderer>();diffuserRenderer.shadowCastingMode=ShadowCastingMode.Off;diffuserRenderer.receiveShadows=false;
            var lightGo=new GameObject("Only light source · modern wall lamp on right");
            lightGo.transform.position=new Vector3(8.2f,.1f,-3.862f);
            lightGo.transform.rotation=Quaternion.LookRotation(Vector3.left,Vector3.up);
            var light=lightGo.AddComponent<Light>(); light.type=LightType.Spot;
            light.color=new Color(1f,.96f,.91f); light.intensity=4.5f; light.range=20f; light.spotAngle=72f;
            light.shadows=LightShadows.Soft; light.shadowStrength=.75f;
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=Color.black;
            return light;
        }
        static Transform BuildTable(Material wood, Material steel, out Transform donut)
        {
            var table=new GameObject("Starting object · ㅠ table"); table.transform.position=new Vector3(.3f,0,-3.862f);
            Box("Long wooden tabletop",table.transform.position+new Vector3(0,1.05f,0),new Vector3(.92f,.13f,2.75f),wood,table.transform);
            foreach(float z in new[]{-.92f,.92f})
            {
                foreach(float x in new[]{-.29f,.29f}) Box("Silver table leg",table.transform.position+new Vector3(x,.56f,z),new Vector3(.09f,.92f,.09f),steel,table.transform);
                Box("Silver table foot",table.transform.position+new Vector3(0,.10f,z),new Vector3(.61f,.08f,.09f),steel,table.transform);
                Box("Silver table crossbar",table.transform.position+new Vector3(0,.21f,z),new Vector3(.57f,.06f,.07f),steel,table.transform);
            }
            var tableCollider=table.AddComponent<BoxCollider>(); tableCollider.center=new Vector3(0,1.05f,0); tableCollider.size=new Vector3(.92f,.13f,2.75f);
            table.AddComponent<Rigidbody>().isKinematic=true;
            var ring=new GameObject("Starting object · strawberry glazed donut"); ring.transform.position=table.transform.position+new Vector3(0,1.20f,0); ring.transform.localScale=Vector3.one*1.18f;
            var ringMesh=RingMesh();ring.AddComponent<MeshFilter>().sharedMesh=ringMesh;
            var dough=Material("Donut · toasted dough",Color.white,.16f);
            dough.mainTexture=DoughTexture();
            ring.AddComponent<MeshRenderer>().sharedMaterial=dough;
            var glaze=Material("Donut · strawberry glaze",new Color(.86f,.24f,.32f),.48f);
            var topping=new GameObject("Glossy strawberry glaze"); topping.transform.SetParent(ring.transform,false);
            topping.transform.localPosition=new Vector3(0,-.003f,0);
            var glazeMesh=GlazeMesh();topping.AddComponent<MeshFilter>().sharedMesh=glazeMesh;topping.AddComponent<MeshRenderer>().sharedMaterial=glaze;
            AddSprinkles(ring.transform);
            // A released donut has a dynamic Rigidbody. Unity drops non-convex
            // MeshColliders from dynamic bodies, so model the torus with small
            // convex contacts that keep its hole open and remain raycastable.
            const int ringColliderCount=24;
            for(int i=0;i<ringColliderCount;i++)
            {
                float angle=i*Mathf.PI*2/ringColliderCount;
                var segment=new GameObject($"Donut shadow/contact segment {i+1:00}");
                segment.transform.SetParent(ring.transform,false);
                segment.transform.localPosition=new Vector3(.175f*Mathf.Cos(angle),0,.175f*Mathf.Sin(angle));
                segment.AddComponent<SphereCollider>().radius=.073f;
            }
            ring.AddComponent<BoxCollider>().size=new Vector3(.52f,.14f,.52f); ring.AddComponent<Rigidbody>().isKinematic=true;
            donut=ring.transform;
            return table.transform;
        }
        static Transform BuildDoor(Material white, Material inset, Material brass, Material dark, Material threshold)
        {
            const float h=6.45f,w=2.75f,z=5.7f;
            Box("Door threshold",new Vector3(0,.07f,z),new Vector3(3.45f,.14f,.62f),threshold);
            var frame=new GameObject("White door frame · fixed").transform;
            foreach(float x in new[]{-w*.5f-.11f,w*.5f+.11f}) Box("White freestanding jamb",new Vector3(x,h*.5f,z),new Vector3(.16f,h+.12f,.30f),white,frame);
            Box("White lintel",new Vector3(0,h+.02f,z),new Vector3(w+.38f,.17f,.30f),white,frame);
            var leaf=new GameObject("Freestanding white door leaf · hinge").transform;
            leaf.position=new Vector3(-w*.5f,0,z);
            Box("White door leaf",new Vector3(0,h*.5f,z),new Vector3(w,h,.20f),white,leaf);
            foreach(float y in new[]{4.95f,3.55f,1.58f}) foreach(float x in new[]{-.53f,.53f})
            {
                float ph=y>4? .88f:y>3?1.12f:1.50f;
                Box("Pale blue recessed panel",new Vector3(x,y,5.585f),new Vector3(.77f,ph,.025f),inset,leaf,false);
                foreach(float side in new[]{-1f,1f}) Box("Panel moulding",new Vector3(x+side*.42f,y,5.55f),new Vector3(.035f,ph+.08f,.04f),white,leaf,false);
                foreach(float side in new[]{-1f,1f}) Box("Panel moulding",new Vector3(x,y+side*(ph*.5f+.04f),5.55f),new Vector3(.84f,.035f,.04f),white,leaf,false);
            }
            // Visible keyhole and ordinary-sized handle, aligned for a later insertion interaction.
            Box("Brass lock plate",new Vector3(.88f,2.62f,5.565f),new Vector3(.16f,.28f,.035f),brass,leaf,false);
            var hole=GameObject.CreatePrimitive(PrimitiveType.Cylinder); hole.name="Keyhole recess"; hole.transform.SetParent(leaf,false);
            hole.transform.SetPositionAndRotation(new Vector3(.88f,2.67f,5.535f),Quaternion.Euler(90,0,0)); hole.transform.localScale=new Vector3(.062f,.012f,.062f);
            hole.GetComponent<Renderer>().sharedMaterial=dark; Object.DestroyImmediate(hole.GetComponent<Collider>());
            Box("Keyhole slot",new Vector3(.88f,2.60f,5.53f),new Vector3(.026f,.085f,.018f),dark,leaf,false);
            var knob=GameObject.CreatePrimitive(PrimitiveType.Sphere); knob.name="Aged brass knob"; knob.transform.SetParent(leaf,false);
            knob.transform.localPosition=new Vector3(.98f,2.62f,5.45f); knob.transform.localScale=Vector3.one*.12f;
            knob.GetComponent<Renderer>().sharedMaterial=brass; Object.DestroyImmediate(knob.GetComponent<Collider>());
            var doorCollider=leaf.gameObject.AddComponent<BoxCollider>();doorCollider.size=new Vector3(w,h,.2f);doorCollider.center=new Vector3(w*.5f,h*.5f,0);
            return leaf;
        }
        static Transform BuildTarget(Material paperMaterial)
        {
            const float wallX=-8.615f,centerY=.73f,centerZ=-3.862f,paperSize=4.65f;
            var paper=Box("Left wall · white square paper",new Vector3(wallX,centerY,centerZ),new Vector3(paperSize,.035f,paperSize),paperMaterial,null,false);
            // Keep the paper vertical, with the guide lowered until its teeth
            // reach the floor where the donut can stand upright.
            paper.transform.rotation=Quaternion.LookRotation(Vector3.up,Vector3.right);
            var paperRenderer=paper.GetComponent<Renderer>();paperRenderer.shadowCastingMode=ShadowCastingMode.Off;paperRenderer.receiveShadows=true;
            var stencilMaterial=new Material(Shader.Find("Unlit/Transparent")){name="Preview · blue hatched key guide"};
            stencilMaterial.mainTexture=KeyStencilTexture();
            stencilMaterial.mainTextureScale=new Vector2(1,-1);
            stencilMaterial.mainTextureOffset=new Vector2(0,1);
            var stencil=GameObject.CreatePrimitive(PrimitiveType.Plane);stencil.name="Blue hand-drawn key · teeth point down";
            Object.DestroyImmediate(stencil.GetComponent<Collider>());
            stencil.transform.SetPositionAndRotation(new Vector3(-8.575f,centerY,centerZ),Quaternion.LookRotation(Vector3.up,Vector3.right));
            stencil.transform.localScale=new Vector3(.38f,1,.18f);
            var renderer=stencil.GetComponent<Renderer>();renderer.sharedMaterial=stencilMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return paper.transform;
        }
        static LineRenderer NewLine(string name,Material material,int points)
        {var go=new GameObject(name);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.sharedMaterial=material;line.startWidth=line.endWidth=.035f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.positionCount=points;return line;}
        static void AddSegment(string name,Material m,Vector3 a,Vector3 b){var l=NewLine(name,m,2);l.SetPosition(0,a);l.SetPosition(1,b);}
        static void AddCircle(Material m,Vector3 c,float r,int steps)
        {var l=NewLine("Blue target · O ring",m,steps+1);for(int i=0;i<=steps;i++){float a=2*Mathf.PI*i/steps;l.SetPosition(i,c+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));}}
        static KeyShadowTutorialController BuildPlayer(GalleryProp[] props, KeyShadowMatcher matcher)
        {
            var player=new GameObject("Tutorial player · shared movement and object handling");
            player.transform.position=new Vector3(2.4f,.03f,-6.2f);player.layer=2;
            var controller=player.AddComponent<CharacterController>();
            controller.height=1.6f;controller.radius=.3f;controller.center=Vector3.up*.8f;
            controller.stepOffset=.2f;controller.skinWidth=.05f;controller.minMoveDistance=0;
            controller.slopeLimit=60;
            var cameraObject=new GameObject("Tutorial player camera");cameraObject.tag="MainCamera";
            cameraObject.transform.SetParent(player.transform,false);
            cameraObject.transform.localPosition=Vector3.up*1.55f;
            cameraObject.transform.localRotation=Quaternion.Euler(-5.7f,-44.7f,0);
            var camera=cameraObject.AddComponent<Camera>();camera.fieldOfView=70;camera.nearClipPlane=.05f;
            cameraObject.AddComponent<AudioListener>();
            var gameplay=player.AddComponent<KeyShadowTutorialController>();
            gameplay.Configure(camera,controller,props,matcher,
                new Vector3(-8.8f,.02f,-7.8f),new Vector3(8.8f,8.85f,7.8f));
            return gameplay;
        }
        static Mesh RingMesh()
        {
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(RingPath);if(saved!=null)return saved;
            const int aSteps=48,bSteps=16;const float major=.175f,minor=.073f;
            var vertices=new List<Vector3>();var triangles=new List<int>();var uvs=new List<Vector2>();
            for(int i=0;i<=aSteps;i++)for(int j=0;j<=bSteps;j++)
            {
                float a=i*Mathf.PI*2/aSteps,b=j*Mathf.PI*2/bSteps;
                vertices.Add(new Vector3((major+minor*Mathf.Cos(b))*Mathf.Cos(a),minor*Mathf.Sin(b),(major+minor*Mathf.Cos(b))*Mathf.Sin(a)));
                uvs.Add(new Vector2((float)i/aSteps,(float)j/bSteps));
                if(i<aSteps&&j<bSteps){int p=i*(bSteps+1)+j,q=p+bSteps+1;triangles.Add(p);triangles.Add(p+1);triangles.Add(q);triangles.Add(p+1);triangles.Add(q+1);triangles.Add(q);}
            }
            var mesh=new Mesh{name="Rounded baked donut dough"};mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,RingPath);return mesh;
        }
        static Texture2D DoughTexture()
        {
            var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(DoughTexturePath);if(saved!=null)return saved;
            const int width=512,height=256;
            var tex=new Texture2D(width,height,TextureFormat.RGBA32,true,false)
            {name="Warm mottled baked donut crumb",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float u=(float)x/(width-1),v=(float)y/(height-1);
                float broad=Mathf.PerlinNoise(u*18f+2.7f,v*12f+8.1f);
                float crumb=Mathf.PerlinNoise(u*83f+17.3f,v*61f+3.4f);
                float pores=Mathf.SmoothStep(.73f,.90f,Mathf.PerlinNoise(u*181f+5.8f,v*133f+23.6f));
                float warmth=.86f+(broad-.5f)*.18f+(crumb-.5f)*.08f;
                float toast=Mathf.SmoothStep(.67f,.88f,broad)*.12f+pores*.10f;
                pixels[y*width+x]=new Color(
                    Mathf.Clamp01(.80f*warmth-toast*.75f),
                    Mathf.Clamp01(.49f*warmth-toast*.67f),
                    Mathf.Clamp01(.23f*warmth-toast*.43f),1f);
            }
            // Close the texture edges so the torus UV seam does not form a visible stripe.
            for(int y=0;y<height;y++)pixels[y*width+width-1]=pixels[y*width];
            for(int x=0;x<width;x++)pixels[(height-1)*width+x]=pixels[x];
            tex.SetPixels(pixels);tex.Apply(true,false);AssetDatabase.CreateAsset(tex,DoughTexturePath);return tex;
        }
        static Texture2D KeyStencilTexture()
        {
            var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(KeyStencilPath);if(saved!=null)return saved;
            const int width=512,height=256,samplesPerAxis=2;
            const float halfWidth=1.9f,halfHeight=.9f;
            var tex=new Texture2D(width,height,TextureFormat.RGBA32,false,true)
            {name="Blue hand-drawn hatched key guide",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                Color sum=Color.clear;
                for(int sy=0;sy<samplesPerAxis;sy++)for(int sx=0;sx<samplesPerAxis;sx++)
                {
                    float px=(((x+(sx+.5f)/samplesPerAxis)/width)*2-1)*halfWidth;
                    float pz=(((y+(sy+.5f)/samplesPerAxis)/height)*2-1)*halfHeight;
                    float alpha=0;
                    if(InKeyShape(px,pz))
                    {
                        const float edgeProbe=.024f;
                        bool edge=!InKeyShape(px+edgeProbe,pz)||!InKeyShape(px-edgeProbe,pz)
                            ||!InKeyShape(px,pz+edgeProbe)||!InKeyShape(px,pz-edgeProbe);
                        float hatchPhase=Mathf.Repeat((px+pz)*5.4f,1f);
                        float hatchDistance=Mathf.Min(hatchPhase,1f-hatchPhase)/5.4f;
                        float grain=.84f+.16f*Mathf.PerlinNoise(px*31f+5.2f,pz*29f+8.1f);
                        if(edge)alpha=.97f*grain;
                        else if(hatchDistance<.026f)alpha=.76f*grain;
                    }
                    sum+=new Color(.12f,.35f,.94f,alpha);
                }
                pixels[y*width+x]=sum/(samplesPerAxis*samplesPerAxis);
            }
            tex.SetPixels(pixels);tex.Apply();AssetDatabase.CreateAsset(tex,KeyStencilPath);return tex;
        }
        static bool InKeyShape(float x,float z)
        {
            const float bowX=1.02f,bowZ=.08f,outer=.78f,inner=.32f;
            float dx=x-bowX,dz=z-bowZ,r2=dx*dx+dz*dz;
            bool bow=r2<=outer*outer&&r2>=inner*inner;
            bool shaft=x>=-1.82f&&x<=.53f&&z>=-.255f&&z<=.255f;
            bool toothA=x>=-1.28f&&x<=-.98f&&z>=-.60f&&z<-.255f;
            bool toothB=x>=-.37f&&x<=-.07f&&z>=-.53f&&z<-.255f;
            return bow||shaft||toothA||toothB;
        }
        static Mesh GlazeMesh()
        {
            const int uSteps=96,vSteps=20;const float major=.175f,minor=.074f;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<=uSteps;i++)
            {
                float u=i*Mathf.PI*2/uSteps;
                float outer=Drip(u,.42f,.06f,.12f)+Drip(u,2.75f,.045f,.15f)+Drip(u,4.62f,.055f,.10f);
                float inner=Drip(u,1.42f,.05f,.10f)+Drip(u,3.72f,.05f,.12f)+Drip(u,5.72f,.04f,.08f);
                float start=-.18f-inner,end=Mathf.PI+.16f+outer;
                for(int j=0;j<=vSteps;j++)
                {
                    float v=Mathf.Lerp(start,end,(float)j/vSteps);
                    float radius=major+minor*Mathf.Cos(v)*1.025f;
                    vertices.Add(new Vector3(radius*Mathf.Cos(u),minor*Mathf.Sin(v)*1.025f+.006f,radius*Mathf.Sin(u)));
                    if(i<uSteps&&j<vSteps){int p=i*(vSteps+1)+j,q=p+vSteps+1;triangles.Add(p);triangles.Add(p+1);triangles.Add(q);triangles.Add(p+1);triangles.Add(q+1);triangles.Add(q);}
                }
            }
            var mesh=new Mesh{name="Strawberry glaze cap with rounded drips"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static float Drip(float angle,float center,float width,float depth)
        {float d=Mathf.DeltaAngle(angle*Mathf.Rad2Deg,center*Mathf.Rad2Deg)*Mathf.Deg2Rad/width;return depth*Mathf.Exp(-d*d);}
        static void AddSprinkles(Transform parent)
        {
            var candies=new[]
            {
                Material("Sprinkle · lemon",new Color(.98f,.75f,.23f),.22f),
                Material("Sprinkle · sky blue",new Color(.26f,.66f,.78f),.22f),
                Material("Sprinkle · vanilla",new Color(.96f,.86f,.67f),.22f),
                Material("Sprinkle · berry",new Color(.61f,.24f,.42f),.22f)
            };
            const int count=18;const float major=.175f,minor=.073f;
            for(int i=0;i<count;i++)
            {
                float a=(i*2.39996f)+.21f;
                float direction=a+Mathf.Sin(i*2.13f)*1.18f;
                var sprinkle=GameObject.CreatePrimitive(PrimitiveType.Capsule);sprinkle.name="Sugar sprinkle";Object.DestroyImmediate(sprinkle.GetComponent<Collider>());
                sprinkle.transform.SetParent(parent,false);
                sprinkle.transform.localPosition=new Vector3(major*Mathf.Cos(a),minor+.004f,major*Mathf.Sin(a));
                var alongTop=new Vector3(Mathf.Cos(direction),0,Mathf.Sin(direction));
                sprinkle.transform.localRotation=Quaternion.FromToRotation(Vector3.up,alongTop);
                float length=.024f+Mathf.Repeat(i*.618f,.014f);
                sprinkle.transform.localScale=new Vector3(.008f,length*.5f,.008f);
                sprinkle.GetComponent<Renderer>().sharedMaterial=candies[i%candies.Length];
            }
        }
    }
}
