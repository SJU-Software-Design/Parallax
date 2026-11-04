using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // The new installation uses the game's shared physical interaction. Painted
    // eyes borrow a detail of a public-domain painting through UVs, not a new bitmap.
    public sealed class GalleryConceptPerspective : MonoBehaviour
    {
        public GalleryGame game;
        public GalleryProp[] pieces;
        public Transform viewingPoint;
        public Vector3[] solutionPositions,markerLocalPositions;
        public float[] solutionScales;
        public Renderer feedback;
        public int AlignedPieces {get;private set;}
        public bool Complete {get;private set;}
        public bool AtViewingPoint {get;private set;}
        public Vector3 ViewingDirection=>transform.TransformDirection(Vector3.forward).normalized;
        MaterialPropertyBlock block;
        public void ResetEvaluation()
        {
            AlignedPieces=0;Complete=false;AtViewingPoint=false;UpdateFeedback();
        }
        public void Evaluate()
        {
            AlignedPieces=0;Complete=false;AtViewingPoint=false;
            if(game==null||game.player==null||game.view==null||viewingPoint==null||pieces==null||pieces.Length!=3||
                solutionPositions==null||solutionScales==null||markerLocalPositions==null||
                solutionPositions.Length!=pieces.Length||solutionScales.Length!=pieces.Length||markerLocalPositions.Length!=pieces.Length)
            {UpdateFeedback();return;}
            Vector3 referenceEye=viewingPoint.position+Vector3.up*1.55f,eye=game.view.transform.position;
            Vector3 forward=ViewingDirection,right=Vector3.Cross(Vector3.up,forward).normalized,up=Vector3.Cross(forward,right);
            for(int i=0;i<pieces.Length;i++)
            {
                var p=pieces[i];if(p==null||p==game.held||p.placement==null||p.placement.Held||!p.placement.Ready)continue;
                if(p.body!=null&&!p.body.isKinematic&&(p.body.linearVelocity.sqrMagnitude>.04f||p.body.angularVelocity.sqrMagnitude>.09f))continue;
                Vector3 actual=p.transform.TransformPoint(markerLocalPositions[i])-eye;
                Vector3 desired=transform.TransformPoint(solutionPositions[i]+markerLocalPositions[i]*solutionScales[i])-referenceEye;
                Vector3 a=new Vector3(Vector3.Dot(actual,right),Vector3.Dot(actual,up),Vector3.Dot(actual,forward));
                Vector3 b=new Vector3(Vector3.Dot(desired,right),Vector3.Dot(desired,up),Vector3.Dot(desired,forward));
                if(a.z<1||b.z<1)continue;
                float referenceScale=solutionScales[i]*Mathf.Abs(transform.lossyScale.x);
                float apparentScale=(Mathf.Abs(p.transform.lossyScale.x)/a.z)/(referenceScale/b.z);
                if(Vector2.Distance(new Vector2(a.x,a.y)/a.z,new Vector2(b.x,b.y)/b.z)<.06f&&Mathf.Abs(apparentScale-1)<.23f&&
                    Vector3.Dot(p.transform.forward,forward)>.90f&&Vector3.Dot(p.transform.up,Vector3.up)>.90f)AlignedPieces++;
            }
            AtViewingPoint=Vector2.Distance(new Vector2(game.player.feet.x,game.player.feet.z),new Vector2(viewingPoint.position.x,viewingPoint.position.z))<1.05f&&
                Mathf.Abs(game.player.feet.y-viewingPoint.position.y)<.35f;
            Complete=AlignedPieces==pieces.Length;
            UpdateFeedback();
        }
        void UpdateFeedback()
        {
            if(feedback!=null)
            {
                if(block==null)block=new MaterialPropertyBlock();
                block.SetColor("_Selection",Complete?new Color(.25f,.14f,.025f):Color.black);feedback.SetPropertyBlock(block);
            }
        }

        static GalleryProp Prop(Transform parent,string name,Vector3 at,float scale)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=Vector3.one*scale;
            var p=go.AddComponent<GalleryProp>();p.title=name;p.zone=2;p.minScale=.35f;return p;
        }
        static Renderer Part(Transform parent,string name,Vector3 at,Vector3 size,Material material,bool solid=true)
        {
            var r=GalleryConceptArchitecture.Block(parent,name,at,size,material,.025f);
            if(solid){var collider=r.gameObject.AddComponent<BoxCollider>();collider.size=size;r.gameObject.AddComponent<GalleryCaster>().shape=collider;}return r;
        }
        static Renderer MeshObject(Transform parent,string name,Mesh mesh,Material material,Vector3 at)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        static void Label(Transform parent,string words,Vector3 at,float size)
        {
            var go=new GameObject("Installation label");go.transform.SetParent(parent,false);go.transform.localPosition=at;
            var label=go.AddComponent<GalleryLabel>();label.caption=words;label.size=size;label.color=new Color(.23f,.20f,.16f);
        }
        static Mesh EyeCanvas()
        {
            var mesh=new Mesh{name="Painted eye canvas"};
            mesh.vertices=new[]{new Vector3(-.675f,-.39f,-.045f),new Vector3(.675f,-.39f,-.045f),new Vector3(.675f,.39f,-.045f),new Vector3(-.675f,.39f,-.045f)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Nose()
        {
            Vector4[] rings={new Vector4(1.22f,.16f,.10f,-.12f),new Vector4(1.32f,.27f,.22f,-.18f),
                new Vector4(1.48f,.29f,.31f,-.23f),new Vector4(1.62f,.21f,.25f,-.16f),
                new Vector4(1.94f,.115f,.18f,-.025f),new Vector4(2.28f,.075f,.08f,.045f)};
            const int sides=36;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int j=0;j<rings.Length;j++)for(int i=0;i<=sides;i++)
            {
                float a=i*Mathf.PI*2/sides;var r=rings[j];v.Add(new Vector3(Mathf.Cos(a)*r.y,r.x,r.w+Mathf.Sin(a)*r.z));uv.Add(new Vector2((float)i/sides,r.x));
                if(j>0&&i<sides){int b=j*(sides+1)+i;t.AddRange(new[]{b,b+1,b-sides,b,b-sides,b-sides-1});}
            }
            int bottom=v.Count,top=bottom+1,last=(rings.Length-1)*(sides+1);
            v.Add(new Vector3(0,rings[0].x,rings[0].w));v.Add(new Vector3(0,rings[rings.Length-1].x,rings[rings.Length-1].w));uv.Add(Vector2.one*.5f);uv.Add(Vector2.one*.5f);
            for(int i=0;i<sides;i++)t.AddRange(new[]{bottom,i,i+1,top,last+i+1,last+i});
            var mesh=new Mesh{name="Carved architectural nose"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Lip(bool upper)
        {
            const int segments=48,sides=24;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<=segments;i++)
            {
                float x=Mathf.Lerp(-1,1,(float)i/segments),taper=Mathf.Pow(Mathf.Max(0,1-x*x),.58f);
                float cupid=.16f*Mathf.Exp(-Mathf.Pow((Mathf.Abs(x)-.38f)*4,2))-.045f*Mathf.Exp(-x*x*28);
                float y=upper?.76f+cupid:.39f-.025f*Mathf.Cos(x*Mathf.PI);
                for(int j=0;j<=sides;j++)
                {
                    float a=j*Mathf.PI*2/sides;
                    v.Add(new Vector3(x*1.35f,y+Mathf.Cos(a)*taper*(upper?.18f:.20f),.03f+Mathf.Sin(a)*taper*.46f));uv.Add(new Vector2((float)i/segments,(float)j/sides));
                    if(i>0&&j<sides){int b=i*(sides+1)+j;t.AddRange(new[]{b,b-sides,b+1,b,b-sides-1,b-sides});}
                }
            }
            var mesh=new Mesh{name=upper?"Upholstered upper lip":"Upholstered lower lip"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static GalleryConceptPerspective Create(GalleryGame game,Transform parent,List<GalleryProp> props,GalleryConceptArchitecture.Palette palette)
        {
            var root=new GameObject("The portrait from one point").transform;root.SetParent(parent,false);
            var eyes=Prop(root,"03 · 초상의 눈 액자",new Vector3(-2.1f,0,40.0f),.62f);
            Part(eyes.transform,"Eye easel foot",new Vector3(0,.065f,.08f),new Vector3(3.3f,.13f,.8f),palette.wood);
            for(int side=-1;side<=1;side+=2)
            {
                Part(eyes.transform,"Slender bronze picture stand",new Vector3(side*1.25f,1.45f,.10f),new Vector3(.075f,2.9f,.09f),palette.bronze);
                var eye=new GameObject("A painted eye in an antique frame").transform;eye.SetParent(eyes.transform,false);eye.localPosition=new Vector3(side*.87f,2.75f,0);
                var backing=Part(eye,"Framed canvas backing",Vector3.zero,new Vector3(1.38f,.81f,.07f),palette.paper);
                backing.GetComponent<BoxCollider>().center=new Vector3(0,0,-.02f);
                backing.GetComponent<BoxCollider>().size=new Vector3(1.65f,1.08f,.12f);
                GalleryConceptArchitecture.PictureFrame(eye,new Vector2(1.38f,.81f),palette,.65f);
                var paint=new Material(Shader.Find("Parallax/MuseumSurface")){name="Concept painted eye "+side,color=Color.white};GalleryMuseumMaterials.Apply(paint,"Artwork");
                paint.mainTexture=Resources.Load<Texture2D>("MuseumArt/GirlWithPearlEarring");paint.mainTextureScale=new Vector2(.17f,.08f);
                paint.mainTextureOffset=side<0?new Vector2(.26f,.585f):new Vector2(.38f,.562f);palette.assets?.Add(paint);
                MeshObject(eye,"Vermeer eye detail · canvas UV selection",EyeCanvas(),paint,Vector3.zero);
            }
            eyes.Remember();props.Add(eyes);
            var nose=Prop(root,"03 · 초상의 코 조각",new Vector3(2.3f,0,38.0f),.62f);
            Part(nose.transform,"Nose study plinth foot",new Vector3(0,.075f,.07f),new Vector3(.85f,.15f,.85f),palette.stone);
            Part(nose.transform,"Nose study thin stand",new Vector3(0,.70f,.16f),new Vector3(.085f,1.3f,.085f),palette.bronze);
            var noseCollision=Part(nose.transform,"Nose sculpture physical envelope",new Vector3(0,1.75f,-.04f),new Vector3(.58f,1.12f,.68f),palette.stone);noseCollision.enabled=false;
            MeshObject(nose.transform,"Carved nose in limestone",Nose(),palette.stone,Vector3.zero);nose.Remember();props.Add(nose);
            var lips=Prop(root,"03 · 초상의 입술 소파",new Vector3(-2.5f,0,35.7f),.50f);
            var velvet=new Material(Shader.Find("Parallax/MuseumSurface")){name="Concept vermilion velvet",color=new Color(.46f,.024f,.018f)};
            GalleryMuseumMaterials.Apply(velvet,"Paper");if(velvet.HasProperty("_Roughness"))velvet.SetFloat("_Roughness",.93f);palette.assets?.Add(velvet);
            var body=Part(lips.transform,"Upholstered sofa physical body",new Vector3(0,.59f,0),new Vector3(2.65f,.88f,.94f),velvet);body.enabled=false;
            MeshObject(lips.transform,"Swept velvet upper cushion",Lip(true),velvet,Vector3.zero);MeshObject(lips.transform,"Swept velvet lower cushion",Lip(false),velvet,Vector3.zero);
            for(int side=-1;side<=1;side+=2)Part(lips.transform,"Sofa bronze foot",new Vector3(side*.94f,.11f,0),new Vector3(.14f,.22f,.48f),palette.bronze);
            lips.Remember();props.Add(lips);
            var marker=new GameObject("Portrait observation point").transform;marker.SetParent(root,false);marker.localPosition=new Vector3(0,0,30.2f);
            var disc=GameObject.CreatePrimitive(PrimitiveType.Cylinder);disc.name="Inlaid bronze observation disc";disc.transform.SetParent(root,false);disc.transform.localPosition=new Vector3(0,.006f,30.2f);disc.transform.localScale=new Vector3(.95f,.005f,.95f);
            Object.DestroyImmediate(disc.GetComponent<Collider>());disc.GetComponent<Renderer>().sharedMaterial=palette.bronze;
            Label(root,"시점의 초상",new Vector3(-4.8f,2.35f,30.4f),.13f);Label(root,"작품의 크기와 위치를 바꾸고\n관람 지점에서 얼굴을 찾아보세요",new Vector3(-4.8f,1.88f,30.4f),.075f);
            var feedback=Part(root,"Completed portrait bronze frieze",new Vector3(0,5.5f,43.73f),new Vector3(6,.065f,.07f),palette.bronze,false);
            var exhibit=root.gameObject.AddComponent<GalleryConceptPerspective>();exhibit.game=game;exhibit.pieces=new[]{eyes,nose,lips};exhibit.viewingPoint=marker;exhibit.feedback=feedback;
            exhibit.solutionPositions=new[]{new Vector3(0,0,40.8f),new Vector3(0,0,38.5f),new Vector3(0,0,37.0f)};
            exhibit.solutionScales=new[]{1f,1f,.82f};exhibit.markerLocalPositions=new[]{new Vector3(0,2.75f,0),new Vector3(0,1.75f,0),new Vector3(0,.65f,0)};return exhibit;
        }
    }
}
