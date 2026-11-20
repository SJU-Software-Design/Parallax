using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Eclipse
{
    public sealed class EclipseFinaleWorld : MonoBehaviour
    {
        public const float SignWidth = 3.4f, SignHeight = 1.15f;
        public const int MoonSupportMask = 1 << 8;
        public const float DoorX = .60f, DoorWidth = .20f, DoorHeight = .66f, DoorBottom = .035f;
        EclipseFinaleGame game;
        Light sunlight;
        Transform corridor;
        Renderer moonRenderer;
        SphereCollider moonCollider;
        readonly List<Collider> signColliders = new List<Collider>();
        readonly List<Material> materials = new List<Material>();
        Material boardMat, glyphMat, trimMat, floorMat, seamMat;
        AudioSource ambience;
        bool legacyAmbienceEnabled=true,legacyAmbienceStarted;
        public AudioSource LegacyAmbientSource => ambience;
        public bool LegacyAmbienceEnabled => legacyAmbienceEnabled;
        float lastDarkness = -1;
        EclipseSelectedEnvironment selectedEnvironment;
        Material coronaMaterial;
        EclipseExitSignSource selectedExit;
        readonly List<BoxCollider> shelterColliders=new List<BoxCollider>();
        readonly List<Renderer> shelterRenderers=new List<Renderer>();
        readonly List<Material> shelterMaterials=new List<Material>();
        readonly List<float> shelterOpacities=new List<float>();
        readonly List<float> shelterFadeHolds=new List<float>();
        public Vector3 CurrentSunPosition {get;private set;} = EclipseFinaleGame.PuzzleSunPosition;
        public Vector3 CurrentSolarPosition => CurrentSunPosition;
        public IReadOnlyList<BoxCollider> ShelterRoofColliders => shelterColliders;
        public IReadOnlyList<Renderer> ShelterRoofRenderers => shelterRenderers;
        public IReadOnlyList<float> ShelterRoofOpacities => shelterOpacities;
        public int FadedShelterPanelCount {get;private set;}
        readonly List<BoxCollider> retractableShadeWalls=new List<BoxCollider>();
        readonly List<Renderer> retractableShadeRenderers=new List<Renderer>();
        readonly List<Vector3> shadeWallOpeningPositions=new List<Vector3>();
        readonly List<Renderer> exitRouteGuides=new List<Renderer>();
        Material exitRouteGuideMaterial;
        public IReadOnlyList<BoxCollider> RetractableShadeWalls => retractableShadeWalls;
        public IReadOnlyList<Renderer> RetractableShadeRenderers => retractableShadeRenderers;
        public IReadOnlyList<Renderer> ExitRouteGuideRenderers => exitRouteGuides;
        public float ExitRouteClearProgress {get;private set;}
        public bool ExitRouteCleared => ExitRouteClearProgress >= 1;
        const float ShadeWallRetractionDepth=1.45f;
        public Transform AssetsRoot => selectedEnvironment!=null?selectedEnvironment.AssetsRoot:null;
        public Transform LeftPirateShip => selectedEnvironment!=null?selectedEnvironment.LeftPirateShip:null;
        public bool EnvironmentAssetsReady => selectedEnvironment!=null&&selectedEnvironment.EnvironmentReady;
        public bool ArrivalShipReady => selectedEnvironment!=null&&selectedEnvironment.ArrivalShipReady;
        public bool SunSurfaceReady {get;private set;}
        public bool ExitOriginalAssetReady {get;private set;}
        public int SelectedMeshCount => selectedEnvironment!=null?selectedEnvironment.SelectedMeshCount:0;
        public Renderer SunRenderer {get;private set;}
        public Renderer DeckSurface {get;private set;}
        public Renderer CoronaRenderer {get;private set;}
        public Transform ShelterRoot {get;private set;}
        public BoxCollider ShelterRoofCollider {get;private set;}
        public Renderer ShelterRoofRenderer {get;private set;}
        public float PresentationDarkness {get;private set;}
        public float SunLightIntensity => sunlight!=null?sunlight.intensity:0;
        public Color AmbientColor => RenderSettings.ambientLight;
        public Color DeckTint => floorMat!=null?floorMat.color:Color.black;
        public float ReflectionGain => RenderSettings.reflectionIntensity;
        public float SelectedLightGain => selectedEnvironment!=null?selectedEnvironment.PresentationLightGain:0;
        public EclipseExitSignSource ExitSource => selectedExit;
        // Angular degrees between the bright r=1.04 halo rim and unchanged moon silhouette.
        public float VisibleCoronaMargin {get;private set;}
        public float CoronaRimRadius {get;private set;}
        public Vector3 CoronaReferenceEye {get;private set;}
        public bool CoronaTracksMoonRim {get;private set;}
        bool coronaReferenceValid;
        public string AssetStatus => (selectedEnvironment!=null?selectedEnvironment.Status:"Environment not built")+
            " sunSurface="+SunSurfaceReady+" selectedExitOriginal="+ExitOriginalAssetReady;

        public void Build(EclipseFinaleGame value)
        {
            game = value; CurrentSunPosition = EclipseFinaleGame.PuzzleSunPosition;
            RenderSettings.skybox = null; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.23f, .26f, .32f);
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 160;
            QualitySettings.shadowCascades = 4;
            var lamp = new GameObject("The only sun / directional light"); lamp.transform.SetParent(transform);
            sunlight = lamp.AddComponent<Light>(); sunlight.type = LightType.Directional;
            sunlight.transform.rotation = Quaternion.LookRotation(-CurrentSunPosition.normalized);
            sunlight.color = new Color(1, .88f, .72f); sunlight.intensity = 2.1f;
            RenderSettings.sun = sunlight;
            sunlight.shadows = LightShadows.Soft; sunlight.shadowBias = .015f; sunlight.shadowNormalBias = .06f;
            floorMat = Material("Matte graphite credits deck", new Color(.29f, .32f, .36f));
            var edge = Material("Graphite exterior", new Color(.075f, .10f, .14f));
            var wall = Material("Continuous pale wall", new Color(.42f, .49f, .55f));
            seamMat = Material("Subtle deck joints", new Color(.47f, .53f, .57f));
            var safe = Material("Starting sanctuary", new Color(.18f, .20f, .23f));
            var accent = Material("Warm threshold inlay", new Color(.73f, .53f, .29f), true);
            Box("Single empty chamber / deck", new Vector3(0, -.25f, 31), new Vector3(26, .5f, 72), floorMat);
            Box("Deck underside", new Vector3(0, -.75f, 31), new Vector3(26.5f, .55f, 72.5f), edge);
            Box("Safe starting floor", new Vector3(0, .006f, .1f), new Vector3(25.8f, .012f, 10.2f), safe, solid:false);
            Box("Sanctuary limit", new Vector3(0, .016f, 5.2f), new Vector3(25.8f, .012f, .055f), accent, solid:false);
            foreach (float x in new[] { -13f, 13f })
            {
                Box("Continuous side wall", new Vector3(x, .625f, 31), new Vector3(.24f, 1.25f, 72), wall);
                Box("Quiet wall cap", new Vector3(x, 1.26f, 31), new Vector3(.28f, .025f, 72), edge);
            }
            Box("Rear boundary", new Vector3(0, .625f, -5), new Vector3(26, 1.25f, .3f), wall);
            Box("Distant boundary", new Vector3(0, .42f, 67), new Vector3(26, .84f, .3f), edge);
            for (float z = 7; z < 66; z += 3)
                Box("Floor seam", new Vector3(0, .008f, z), new Vector3(25.7f, .008f, .014f), seamMat, solid:false);
            foreach (float x in new[] { -8.5f, -4.25f, 0, 4.25f, 8.5f })
                Box("Longitudinal seam", new Vector3(x, .008f, 35), new Vector3(.012f, .008f, 60), seamMat, solid:false);
            // Only renderer dressing changes here; the original deck remains intact; edge collision follows the low1.25m rail.
            foreach(var renderer in GetComponentsInChildren<Renderer>())
            {
                string name=renderer.name;
                if(name=="Single empty chamber / deck")DeckSurface=renderer;
                if(name=="Safe starting floor"||name=="Sanctuary limit"||name=="Continuous side wall"||name=="Quiet wall cap"||name=="Rear boundary"||
                    name=="Floor seam"||name=="Longitudinal seam")renderer.enabled=false;
            }
            selectedEnvironment=gameObject.AddComponent<EclipseSelectedEnvironment>();
            selectedEnvironment.Build(transform);
            BuildShelter(); BuildExitRouteGuides();
            BuildSun(); BuildStars(); BuildMoon(); BuildSign();
            selectedExit=EclipseExitSignSource.Install(game.exitSign);
            ExitOriginalAssetReady=selectedExit.Ready;
            BuildCorridor(); BuildAudio();
        }

        void BuildShelter()
        {
            // Low opaque front walls cast a long real floor shadow while the
            // 1.55m player eye sees the whole low solar disc above their 1.25m top.
            // A three-metre central passage connects both roomy shaded wings to
            // the deck. No elevated front beam or painted safe-floor substitute.
            const float top=5.06f, thickness=.12f;
            ShelterRoot=new GameObject("Arrival shelter / open-sky shaded wings").transform;
            ShelterRoot.SetParent(transform,false);
            var shader=Resources.Load<Shader>("Eclipse/Selected/EclipseShelterCanopy");
            if(shader==null)throw new InvalidOperationException("Shelter camera-fade/solid-shadow shader is missing");
            BuildShelterPanel("Shelter rear roof / high clear headroom",new Vector3(0,top-thickness*.5f,0),
                new Vector3(25.6f,thickness,10),shader,false);
            BuildShelterPanel("Shelter left low wall / long real arrival shadow",new Vector3(-7.15f,.625f,5.2f),
                new Vector3(11.3f,1.25f,.24f),shader,true,true);
            BuildShelterPanel("Shelter right low wall / long real arrival shadow",new Vector3(7.15f,.625f,5.2f),
                new Vector3(11.3f,1.25f,.24f),shader,false,true);
            var supports=Material("Shelter / pale structural legs",new Color(.48f,.51f,.53f));
            foreach(float x in new[]{-11.85f,11.85f})foreach(float z in new[]{-4.84f,4.84f})
            {
                var leg=Box("Shelter structural leg",new Vector3(x,(top-thickness)*.5f,z),
                    new Vector3(.24f,top-thickness,.24f),supports,ShelterRoot);
                leg.layer=8;
                // Every rendered safety shadow belongs to the explicit query
                // colliders above; legs add no untracked narrow shadow slivers.
                leg.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        void BuildShelterPanel(string name,Vector3 at,Vector3 size,Shader shader,bool primary,bool obstructsMoon=false)
        {
            var material=new Material(shader){name=name+" / camera-only opacity",color=new Color(.24f,.27f,.30f,1)};
            material.SetFloat("_CameraOpacity",1);
            var panel=Box(name,at,size,material,ShelterRoot);
            // High overhead roofs leave MoonSupportMask8. Low physical walls
            // retain the same placement support as other solid world objects.
            panel.layer=obstructsMoon?8:0;
            var collider=panel.GetComponent<BoxCollider>();
            var renderer=panel.GetComponent<Renderer>();
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            shelterColliders.Add(collider);shelterRenderers.Add(renderer);shelterMaterials.Add(material);
            shelterOpacities.Add(1);shelterFadeHolds.Add(0);
            if(obstructsMoon)
            {
                retractableShadeWalls.Add(collider);retractableShadeRenderers.Add(renderer);
                shadeWallOpeningPositions.Add(panel.transform.localPosition);
            }
            if(primary){ShelterRoofCollider=collider;ShelterRoofRenderer=renderer;}
        }
        void BuildExitRouteGuides()
        {
            exitRouteGuideMaterial=Material("Post-credits flush floor guides",new Color(.045f,.19f,.12f),true);
            // A dim, unlit floor inlay remains readable in totality. It has no
            // physics and appears only as the protected credits return ends.
            for(float z=5.8f;z<50;z+=3)
            {
                var guide=Box("EXIT return / flush floor guide",new Vector3(0,.016f,z),
                    new Vector3(.075f,.008f,.65f),exitRouteGuideMaterial,solid:false);
                var renderer=guide.GetComponent<Renderer>();renderer.enabled=false;
                exitRouteGuides.Add(renderer);
            }
        }
        public void ResetExitRoute()
        {
            ExitRouteClearProgress=0;
            for(int i=0;i<retractableShadeWalls.Count;i++)
            {
                retractableShadeWalls[i].transform.localPosition=shadeWallOpeningPositions[i];
                retractableShadeWalls[i].enabled=true;retractableShadeRenderers[i].enabled=true;
            }
            foreach(var guide in exitRouteGuides)guide.enabled=false;
        }
        public void SetExitRouteReturnProgress(float progress)
        {
            if(float.IsNaN(progress)||float.IsInfinity(progress))return;
            ExitRouteClearProgress=Mathf.Clamp01(progress);
            for(int i=0;i<retractableShadeWalls.Count;i++)
            {
                // Move the same visible and physical wall together under deck.
                // Disable both only once its top is below the walking surface;
                // there is no invisible obstacle and no pre-solve shadow change.
                var collider=retractableShadeWalls[i];
                collider.transform.localPosition=shadeWallOpeningPositions[i]-Vector3.up*(ShadeWallRetractionDepth*ExitRouteClearProgress);
                bool aboveDeck=ExitRouteClearProgress<1;
                collider.enabled=aboveDeck;retractableShadeRenderers[i].enabled=aboveDeck;
            }
            float guideGain=Mathf.SmoothStep(0,1,Mathf.Clamp01((ExitRouteClearProgress-.45f)/.55f));
            exitRouteGuideMaterial.color=new Color(.045f,.19f,.12f)*guideGain;
            foreach(var guide in exitRouteGuides)guide.enabled=guideGain>.001f;
        }
        public bool IsShelterShadowAt(Vector3 point)
        {
            Vector3 direction=CurrentSunPosition.normalized;
            foreach(var collider in shelterColliders)
                if(collider!=null&&collider.enabled&&collider.gameObject.activeInHierarchy&&
                    collider.Raycast(new Ray(point,direction),out _,250f))return true;
            return false;
        }
        public void SetSunPresentationPosition(Vector3 position)
        {
            if(float.IsNaN(position.x)||float.IsNaN(position.y)||float.IsNaN(position.z)||
                float.IsInfinity(position.x)||float.IsInfinity(position.y)||float.IsInfinity(position.z)||position.sqrMagnitude<.001f)
                position=EclipseFinaleGame.PuzzleSunPosition;
            if((position-CurrentSunPosition).sqrMagnitude>.000001f)coronaReferenceValid=false;
            CurrentSunPosition=position;
            if(SunRenderer!=null)SunRenderer.transform.position=position;
            if(sunlight!=null)sunlight.transform.rotation=Quaternion.LookRotation(-position.normalized);
        }
        bool DiscBlocked(BoxCollider collider,Vector3 eye,Vector3 centre,float radius)
        {
            Vector3 delta=centre-eye;float distance=delta.magnitude;
            if(distance<=.001f)return false;
            Vector3 axis=delta/distance;
            Vector3 right=Vector3.Cross(Vector3.up,axis);
            if(right.sqrMagnitude<.0001f)right=Vector3.right;else right.Normalize();
            Vector3 up=Vector3.Cross(axis,right).normalized;
            float angle=Mathf.Asin(Mathf.Clamp(radius/distance,0,.98f));
            float limit=Mathf.Max(.01f,distance-radius*.85f);
            // Centre plus all eight rim directions. A thin edge obstruction must
            // fade too; a clear centre ray alone is insufficient for a large moon.
            for(int i=0;i<9;i++)
            {
                Vector3 direction=axis;
                if(i>0)
                {
                    float around=(i-1)*Mathf.PI*.25f;
                    direction=axis*Mathf.Cos(angle)+(right*Mathf.Cos(around)+up*Mathf.Sin(around))*Mathf.Sin(angle);
                }
                if(collider.Raycast(new Ray(eye,direction.normalized),out _,limit))return true;
            }
            return false;
        }
        public bool IsShelterBlockingDisc(Vector3 eye,Vector3 centre,float radius)
        {
            foreach(var collider in shelterColliders)
                if(collider!=null&&collider.enabled&&DiscBlocked(collider,eye,centre,radius))return true;
            return false;
        }
        bool CreditFloorBlocked(BoxCollider collider,Vector3 eye)
        {
            if(game.body==null)return false;
            Vector3 forward=Vector3.ProjectOnPlane(game.view.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
            Vector3 anchor=game.body.transform.position;anchor.y=.045f;
            for(int column=0;column<3;column++)for(int row=0;row<10;row++)
            {
                float x=(column-1)*4.2f,z=3.5f+row;
                Vector3 delta=anchor+right*x+forward*z-eye;
                if(collider.Raycast(new Ray(eye,delta.normalized),out _,delta.magnitude))return true;
            }
            return false;
        }
        public void TickShelterVisibility(float dt)
        {
            if(game==null||game.view==null)return;
            if(float.IsNaN(dt)||float.IsInfinity(dt))dt=0;
            dt=Mathf.Clamp(dt,0,.1f);
            bool credits=game.Credits!=null&&game.Credits.Active;
            bool active=game.held==FinaleItem.Moon||game.MoonAnchored||credits;
            Vector3 eye=game.view.transform.position;
            FadedShelterPanelCount=0;
            for(int i=0;i<shelterColliders.Count;i++)
            {
                var collider=shelterColliders[i];bool blocked=false;
                if(active&&collider!=null&&collider.enabled)
                {
                    blocked=game.moon!=null&&DiscBlocked(collider,eye,game.moon.position,game.MoonRadius);
                    if(!blocked)blocked=DiscBlocked(collider,eye,CurrentSunPosition,EclipseFinaleGame.SunRadius);
                    if(credits&&!blocked)
                    {
                        blocked=Vector3.Distance(eye,collider.ClosestPoint(eye))<.7f||CreditFloorBlocked(collider,eye);
                    }
                }
                shelterFadeHolds[i]=blocked ? .24f : Mathf.Max(0,shelterFadeHolds[i]-dt);
                float target=shelterFadeHolds[i]>0 ? .10f : 1;
                shelterOpacities[i]=Mathf.Lerp(shelterOpacities[i],target,1-Mathf.Exp(-9*dt));
                shelterMaterials[i].SetFloat("_CameraOpacity",shelterOpacities[i]);
                if(shelterOpacities[i]<.98f)FadedShelterPanelCount++;
            }
        }

        Material Material(string name, Color color, bool flat = false)
        {
            Shader shader = Shader.Find(flat ? "ShadeLink/Flat" : "Parallax/StarshipSurface");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Eclipse / " + name, color = color };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .16f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
            materials.Add(material); return material;
        }
        GameObject Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null, bool solid = true)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
            if (solid && parent == null) obj.layer = 8;
            obj.transform.SetParent(parent == null ? transform : parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            var renderer = obj.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (!solid) { var col = obj.GetComponent<Collider>(); col.enabled = false; Destroy(col); }
            return obj;
        }
        GameObject Sphere(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere); obj.name = name;
            obj.transform.SetParent(parent == null ? transform : parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            var renderer = obj.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            var collider = obj.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            return obj;
        }
        void BuildSun()
        {
            var texture=Resources.Load<Texture2D>("Eclipse/Selected/SolarSystemScope/SunSurface");
            var shader=Resources.Load<Shader>("Eclipse/Selected/EclipseSunSurface");
            if(shader==null)shader=Shader.Find("Parallax/EclipseSunSurface");
            Material sun;
            if(shader!=null)
            {
                sun=new Material(shader){name="Solar System Scope / original Sun2K surface",mainTexture=texture};
                sun.SetFloat("_Exposure",1.3f);materials.Add(sun);
            }
            else sun=Material("Sun source missing / visible development fallback",new Color(1,.88f,.63f),true);
            var surface=Sphere("SUN / Solar System Scope original surface",CurrentSunPosition,
                Vector3.one*EclipseFinaleGame.SunRadius*2,sun);
            SunRenderer=surface.GetComponent<Renderer>();SunSurfaceReady=texture!=null&&shader!=null;
            var marker=surface.AddComponent<EclipseSelectedAssetMarker>();
            marker.assetId="SolarSystemScope/Sun2K";marker.author="Solar System Scope";
            marker.sourceUrl="https://www.solarsystemscope.com/textures/";marker.license="CC BY 4.0";
            if(!SunSurfaceReady)Debug.LogWarning("ECLIPSE_SUN_SOURCE_INCOMPLETE original texture or shader missing");
            var coronaShader=Resources.Load<Shader>("Eclipse/Selected/EclipseSolarCorona");
            if(coronaShader==null)coronaShader=Shader.Find("Parallax/EclipseSolarCorona");
            if(coronaShader==null)throw new InvalidOperationException("Eclipse depth-tested corona shader is missing");
            coronaMaterial=new Material(coronaShader){name="Solar corona / depth occluded by the unchanged moon"};
            coronaMaterial.SetFloat("_Visibility",.24f);materials.Add(coronaMaterial);
            var corona=new GameObject("Solar corona / soft halo, not opaque concentric rings");
            corona.transform.SetParent(transform,false);corona.transform.position=CurrentSunPosition;
            corona.transform.rotation=Quaternion.LookRotation(-CurrentSunPosition.normalized);
            float radius=EclipseFinaleGame.SunRadius*1.62f;
            var mesh=new Mesh{name="Solar corona billboard"};
            mesh.vertices=new[]{new Vector3(-radius,-radius,0),new Vector3(radius,-radius,0),new Vector3(radius,radius,0),new Vector3(-radius,radius,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};
            mesh.RecalculateBounds();mesh.RecalculateNormals();
            corona.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=corona.AddComponent<MeshRenderer>();renderer.sharedMaterial=coronaMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            CoronaRenderer=renderer;CoronaRimRadius=EclipseFinaleGame.SunRadius*1.04f;
        }
        Mesh Annulus(float inner, float outer)
        {
            int segments = 128; var vertices = new Vector3[(segments+1)*2]; var triangles = new int[segments*6];
            for (int i=0;i<=segments;i++)
            {
                float a=i*EclipseMath.PI*2/segments; Vector3 p=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                vertices[i*2]=p*inner;vertices[i*2+1]=p*outer;
                if(i==segments)continue;int k=i*6;triangles[k]=i*2;triangles[k+1]=i*2+1;triangles[k+2]=i*2+2;triangles[k+3]=i*2+1;triangles[k+4]=i*2+3;triangles[k+5]=i*2+2;
            }
            var mesh=new Mesh{name="Solar corona ring"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();return mesh;
        }
        void BuildStars()
        {
            var random = new System.Random(58423);
            for (int group=0;group<3;group++)
            {
                var vertices=new List<Vector3>();var indices=new List<int>();
                for(int i=0;i<190;i++)
                {
                    float y=(float)random.NextDouble()*2-1,a=(float)random.NextDouble()*EclipseMath.PI*2;
                    Vector3 dir=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(a),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(a));
                    Vector3 p=dir*620+Vector3.up*5;
                    Vector3 right=Vector3.Cross(dir,Vector3.up).normalized,up=Vector3.Cross(right,dir).normalized;
                    float size=.12f+(float)random.NextDouble()*.58f;
                    int n=vertices.Count;vertices.Add(p-right*size-up*size);vertices.Add(p+right*size-up*size);vertices.Add(p+right*size+up*size);vertices.Add(p-right*size+up*size);
                    indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                }
                var mesh=new Mesh{name="Distant stars"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
                var obj=new GameObject("Starfield "+group);obj.transform.SetParent(transform);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=Material("Stars",new[]{new Color(.62f,.72f,.84f),new Color(.37f,.5f,.69f),new Color(.86f,.77f,.62f)}[group],true);r.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        void BuildMoon()
        {
            var root=new GameObject("MOON / the only initial movable object");root.transform.SetParent(transform);game.moon=root.transform;
            var mat=Material("Cratered lunar regolith",new Color(.76f,.78f,.79f));
            mat.mainTexture=LunarTexture();mat.SetFloat("_Glossiness",.035f);
            root.AddComponent<MeshFilter>().sharedMesh=LunarMesh();
            moonRenderer=root.AddComponent<MeshRenderer>();moonRenderer.sharedMaterial=mat;moonRenderer.shadowCastingMode=ShadowCastingMode.On;
            moonCollider=root.AddComponent<SphereCollider>();moonCollider.radius=.50f;
        }
        struct Crater {public Vector3 center;public float radius,depth;}
        static Crater[] Craters()
        {
            var rng=new System.Random(12431);var list=new Crater[32];
            for(int i=0;i<list.Length;i++)
            {
                float y=(float)rng.NextDouble()*2-1,a=(float)rng.NextDouble()*EclipseMath.PI*2;
                list[i]=new Crater{center=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(a),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(a)),radius=.045f+(float)rng.NextDouble()*.15f,depth=.012f+(float)rng.NextDouble()*.018f};
            }
            return list;
        }
        Mesh LunarMesh()
        {
            const int longitude=96,latitude=48;var v=new Vector3[(longitude+1)*(latitude+1)];var uv=new Vector2[v.Length];var triangles=new int[longitude*latitude*6];var craters=Craters();
            for(int j=0;j<=latitude;j++)for(int i=0;i<=longitude;i++)
            {
                float u=i/(float)longitude,w=j/(float)latitude,a=u*EclipseMath.PI*2,b=w*EclipseMath.PI;
                Vector3 dir=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));
                float displacement=(EclipseMath.Noise(dir.x*11+18,dir.z*11+dir.y*3+25)-.5f)*.005f;
                foreach(var c in craters){float d=Vector3.Distance(dir,c.center)/c.radius;if(d<1.5f)displacement+=-c.depth*Mathf.Exp(-d*d*3.4f)+c.depth*.34f*Mathf.Exp(-(d-1)*(d-1)*35);}
                int n=j*(longitude+1)+i;v[n]=dir*(.5f+displacement);uv[n]=new Vector2(u,1-w);
                if(j<latitude&&i<longitude){int k=(j*longitude+i)*6;triangles[k]=n;triangles[k+1]=n+1;triangles[k+2]=n+longitude+1;triangles[k+3]=n+1;triangles[k+4]=n+longitude+2;triangles[k+5]=n+longitude+1;}
            }
            var mesh=new Mesh{name="Lunar sphere with recessed craters"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        Texture2D LunarTexture()
        {
            const int w=512,h=256;var colors=new Color32[w*h];var craters=Craters();
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float a=x/(float)(w-1)*EclipseMath.PI*2,b=(1-y/(float)(h-1))*EclipseMath.PI;
                Vector3 dir=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));
                float broad=EclipseMath.Noise(dir.x*2.5f+13,dir.z*2.5f+dir.y*2+21);
                float grain=EclipseMath.Noise(dir.x*65+13,dir.z*65+dir.y*43+21);
                float light=.65f+broad*.25f+(grain-.5f)*.14f;
                if(broad<.38f)light*=.68f;
                foreach(var c in craters){float d=Vector3.Distance(dir,c.center)/c.radius;if(d<1.5f)light+=-.18f*Mathf.Exp(-d*d*3)+.16f*Mathf.Exp(-(d-1)*(d-1)*35);}
                colors[y*w+x]=new Color(light*.99f,light,light*1.02f);
            }
            var tex=new Texture2D(w,h,TextureFormat.RGB24,true){name="Procedural lunar maria and craters",wrapMode=TextureWrapMode.Repeat};tex.SetPixels32(colors);tex.Apply();return tex;
        }
        void BuildSign()
        {
            game.exitSign=new GameObject("Exit / the dark stem of i is a real opening").transform;game.exitSign.SetParent(transform);
            boardMat=Material("Phosphorescent sign body",new Color(.66f,.71f,.68f));
            glyphMat=Material("Phosphorescent lettering",new Color(.74f,.77f,.72f),true);
            trimMat=Material("Sign perimeter",new Color(.43f,.48f,.44f),true);
            var black=Material("Unlit opening / no phosphor",new Color(.001f,.002f,.003f),true);
            float left=DoorX-DoorWidth*.5f,right=DoorX+DoorWidth*.5f,top=DoorBottom+DoorHeight;
            SignPart("Left body",new Vector3((-SignWidth*.5f+left)*.5f,SignHeight*.5f,0),new Vector3(left+SignWidth*.5f,SignHeight,.10f));
            SignPart("Right body",new Vector3((right+SignWidth*.5f)*.5f,SignHeight*.5f,0),new Vector3(SignWidth*.5f-right,SignHeight,.10f));
            SignPart("Above i opening",new Vector3(DoorX,(top+SignHeight)*.5f,0),new Vector3(DoorWidth,SignHeight-top,.10f));
            SignPart("Below i opening",new Vector3(DoorX,DoorBottom*.5f,0),new Vector3(DoorWidth,DoorBottom,.10f));
            Box("Black depth inside the i",new Vector3(DoorX,DoorBottom+DoorHeight*.5f,.20f),new Vector3(DoorWidth,DoorHeight,.015f),black,game.exitSign,false);
            foreach(float x in new[]{-SignWidth*.5f,SignWidth*.5f})Box("Thin vertical sign frame",new Vector3(x,SignHeight*.5f,-.063f),new Vector3(.025f,SignHeight,.014f),trimMat,game.exitSign,false);
            foreach(float y in new[]{.01f,SignHeight-.01f})Box("Thin horizontal sign frame",new Vector3(0,y,-.063f),new Vector3(SignWidth,.025f,.014f),trimMat,game.exitSign,false);
            // Lettering uses actual strokes, leaving the lowercase i stem empty.
            Stroke("E spine",-.67f,.54f,.10f,.70f);Stroke("E top",-.46f,.84f,.48f,.10f);Stroke("E middle",-.49f,.55f,.40f,.10f);Stroke("E foot",-.46f,.24f,.48f,.10f);
            Stroke("x slash",.12f,.48f,.10f,.57f,38);Stroke("x backslash",.12f,.48f,.10f,.57f,-38);
            Sphere("i dot / illuminated, solid",new Vector3(DoorX,.87f,-.075f),new Vector3(.205f,.205f,.025f),glyphMat,game.exitSign);
            Stroke("t stem",1.04f,.57f,.10f,.70f);Stroke("t crossbar",1.04f,.69f,.38f,.09f);Stroke("t curved foot",1.13f,.24f,.25f,.09f);
            // Running-person pictogram echoes the supplied Exit reference.
            Stroke("Pictogram top",-1.24f,.99f,.67f,.055f);Stroke("Pictogram left",-1.55f,.59f,.055f,.83f);Stroke("Pictogram right",-.92f,.59f,.055f,.83f);
            Sphere("Runner head",new Vector3(-1.25f,.82f,-.077f),new Vector3(.14f,.14f,.025f),glyphMat,game.exitSign);
            Stroke("Runner body",-1.25f,.61f,.10f,.29f,-22);Stroke("Runner front arm",-1.10f,.66f,.10f,.23f,42);Stroke("Runner rear arm",-1.38f,.65f,.10f,.24f,-47);
            Stroke("Runner leading leg",-1.12f,.36f,.10f,.32f,36);Stroke("Runner trailing leg",-1.39f,.34f,.10f,.35f,-38);
        }
        void SignPart(string name,Vector3 position,Vector3 scale)
        {var obj=Box(name,position,scale,boardMat,game.exitSign);signColliders.Add(obj.GetComponent<Collider>());}
        void Stroke(string name,float x,float y,float width,float height,float angle=0)
        {var obj=Box(name,new Vector3(x,y,-.067f),new Vector3(width,height,.014f),glyphMat,game.exitSign,false);obj.transform.localRotation=Quaternion.Euler(0,0,angle);}
        void BuildCorridor()
        {
            corridor=new GameObject("Final black corridor and open airlock").transform;corridor.SetParent(transform);corridor.position=new Vector3(0,-70,0);
            var dark=Material("Corridor black",new Color(.012f,.018f,.024f));var voidMat=Material("Absolute black",new Color(.001f,.002f,.004f),true);
            var line=Material("Dim green floor guides",new Color(.035f,.18f,.12f),true);
            Box("Corridor floor",new Vector3(0,-.15f,12),new Vector3(3.4f,.3f,28),dark,corridor);
            Box("Corridor ceiling",new Vector3(0,3.2f,11),new Vector3(3.6f,.25f,26),voidMat,corridor);
            foreach(float x in new[]{-1.75f,1.75f})
            {
                Box("Corridor wall",new Vector3(x,1.55f,11),new Vector3(.25f,3.3f,26),voidMat,corridor);
                Box("Guide toward space",new Vector3(x*.78f,.018f,12),new Vector3(.025f,.015f,24),line,corridor,false);
            }
            Box("Airlock outer collar",new Vector3(0,3.4f,23),new Vector3(5,.45f,2),dark,corridor);
            foreach(float x in new[]{-2.35f,2.35f})Box("Airlock outside edge",new Vector3(x,1.35f,23),new Vector3(.4f,3.8f,2),dark,corridor);
            corridor.gameObject.SetActive(false);
        }
        void BuildAudio()
        {
            const int rate=22050;float[] samples=new float[rate*6];
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate;samples[i]=Mathf.Sin(t*EclipseMath.PI*2*42)*.09f+Mathf.Sin(t*EclipseMath.PI*2*63)*.045f+Mathf.Sin(t*EclipseMath.PI*2*105)*.014f;}
            var clip=AudioClip.Create("Eclipse ambience room tone",samples.Length,1,rate,false);clip.SetData(samples,0);
            ambience=gameObject.AddComponent<AudioSource>();ambience.clip=clip;ambience.loop=true;ambience.volume=.12f;global::GameShellSettingsRuntime.Route(ambience);
        }
        // A selected ambient component calls false synchronously after World.Build,
        // before its new source starts. The legacy first Play is deferred to Update.
        public void SetLegacyAmbienceEnabled(bool value)
        {
            if(legacyAmbienceEnabled==value)return;
            legacyAmbienceEnabled=value;legacyAmbienceStarted=false;
            if(ambience==null)return;
            if(!value)ambience.Stop();
            ambience.enabled=value;
        }
        public void SetMoonColliders(bool value)
        {moonCollider.enabled=value;moonRenderer.shadowCastingMode=value?ShadowCastingMode.On:ShadowCastingMode.Off;}
        public void SetMoonShadow(bool value)
        {moonRenderer.shadowCastingMode=value?ShadowCastingMode.On:ShadowCastingMode.Off;}
        public void SetSignColliders(bool value){foreach(var c in signColliders)c.enabled=value;}
        public void SetEnding(bool value){corridor.gameObject.SetActive(value);}
        void UpdateCoronaPresentation(bool totality)
        {
            if(CoronaRenderer==null||game==null||game.view==null||game.moon==null)return;
            var root=CoronaRenderer.transform;
            if(!totality||!game.MoonAnchored)
            {
                coronaReferenceValid=false;CoronaTracksMoonRim=false;VisibleCoronaMargin=0;
                root.position=CurrentSunPosition;
                root.rotation=Quaternion.LookRotation(-CurrentSunPosition.normalized);
                root.localScale=Vector3.one;CoronaRimRadius=EclipseFinaleGame.SunRadius*1.04f;
                return;
            }
            if(!coronaReferenceValid||(game.Credits!=null&&game.Credits.Active))
            {CoronaReferenceEye=game.view.transform.position;coronaReferenceValid=true;}
            Vector3 toMoon=game.moon.position-CoronaReferenceEye;
            float moonDistance=toMoon.magnitude;
            float sunDistance=Vector3.Distance(CurrentSunPosition,CoronaReferenceEye);
            if(moonDistance<=game.MoonRadius||moonDistance>=sunDistance)return;
            // Artistic totality rim: only the derived halo grows. The sun sphere,
            // selected Sun image, gameplay coverage and lunar geometry stay fixed.
            // Keep it at the distant solar depth so the opaque moon depth clips it.
            float lunarAngle=Mathf.Asin(Mathf.Clamp(game.MoonRadius/moonDistance,0,.95f));
            float paddedAngle=Mathf.Asin(Mathf.Clamp(game.MoonRadius*1.035f/moonDistance,0,.95f));
            float haloBaseRadius=Mathf.Max(EclipseFinaleGame.SunRadius,sunDistance*Mathf.Tan(paddedAngle)*1.007f);
            Vector3 axis=toMoon/moonDistance;
            root.position=CoronaReferenceEye+axis*sunDistance;
            root.rotation=Quaternion.LookRotation(-axis);
            root.localScale=Vector3.one*(haloBaseRadius/EclipseFinaleGame.SunRadius);
            CoronaRimRadius=haloBaseRadius*1.04f;
            VisibleCoronaMargin=(Mathf.Atan(CoronaRimRadius/sunDistance)-lunarAngle)*EclipseMath.Rad2Deg;
            CoronaTracksMoonRim=true;
        }
        public void SetLight(float darkness,bool totality)
        {
            UpdateCoronaPresentation(totality);
            if(Mathf.Abs(lastDarkness-darkness)<.0001f)return;lastDarkness=darkness;
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(darkness));
            PresentationDarkness=t;
            float remainingLight=Mathf.Pow(1-Mathf.Clamp01(darkness),1.65f);
            sunlight.intensity=Mathf.Lerp(.002f,2.1f,remainingLight);
            floorMat.color=Color.Lerp(new Color(.38f,.40f,.43f),new Color(.015f,.019f,.026f),t);
            seamMat.color=Color.Lerp(new Color(.22f,.25f,.28f),new Color(.006f,.008f,.012f),t);
            RenderSettings.reflectionIntensity=Mathf.Lerp(.85f,.006f,t);
            selectedEnvironment?.SetDarkness(t);
            selectedExit?.SetDarkness(t);
            if(coronaMaterial!=null)coronaMaterial.SetFloat("_Visibility",Mathf.Lerp(.24f,1,t));
            RenderSettings.ambientLight=Color.Lerp(new Color(.27f,.30f,.34f),new Color(.003f,.005f,.009f),t);
            boardMat.color=Color.Lerp(new Color(.66f,.71f,.68f),new Color(.018f,.18f,.085f),t);
            boardMat.SetColor("_EmissionColor",new Color(.005f,.09f,.029f)*t);
            glyphMat.color=Color.Lerp(new Color(.74f,.77f,.72f),new Color(.43f,1,.65f),t);
            trimMat.color=Color.Lerp(new Color(.43f,.48f,.44f),new Color(.08f,.48f,.25f),t);
        }
        void OnDestroy(){foreach(var material in shelterMaterials)if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
        void Update()
        {
            if(ambience!=null&&legacyAmbienceEnabled)
            {
                ambience.volume=game.phase>=FinalePhase.Drifting?Mathf.Lerp(.11f,0,game.driftTime/10):Mathf.Lerp(.13f,.045f,game.darkness);
                if(!legacyAmbienceStarted)
                {global::GameShellSettingsRuntime.Route(ambience);ambience.Play();legacyAmbienceStarted=true;}
            }
        }
    }
}
