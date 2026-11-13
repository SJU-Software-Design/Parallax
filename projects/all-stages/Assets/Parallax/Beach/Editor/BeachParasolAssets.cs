using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static partial void ConfigureParasols(BeachGame game)
        {
            const string sourcePath="Assets/ThirdParty/ToonTasticBeachProps/";
            var importer=AssetImporter.GetAtPath(sourcePath+"beachprops.fbx") as ModelImporter;
            if(importer==null)throw new InvalidOperationException("ToonTastic model is missing.");
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath+"Prop_BeachUmbrella.prefab");
            if(source==null)throw new InvalidOperationException("ToonTastic umbrella prefab is missing.");
            var instance=UnityEngine.Object.Instantiate(source);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var filter=instance.GetComponentInChildren<MeshFilter>();var renderer=filter.GetComponent<Renderer>();
                var original=filter.sharedMesh;float scale=2.36f/renderer.bounds.size.y;
                var origin=new Vector3(renderer.bounds.center.x,renderer.bounds.min.y,renderer.bounds.center.z);
                var matrix=Matrix4x4.TRS(-origin*scale,Quaternion.identity,Vector3.one*scale)*filter.transform.localToWorldMatrix;
                var normalMatrix=matrix.inverse.transpose;
                // Rebuild channels rather than retain the FBX's packed vertex layout.
                var mesh=new Mesh{name="ToonTastic Umbrella C",indexFormat=original.indexFormat,
                    vertices=original.vertices.Select(matrix.MultiplyPoint3x4).ToArray(),
                    normals=original.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray(),
                    uv=original.uv,triangles=original.triangles};
                mesh.RecalculateBounds();mesh=Save(mesh,Generated+"Art_Umbrella.asset");
                var triangles=mesh.triangles;var uv=mesh.uv;
                // The supplied palette UVs separate fabric from the pole.
                var cloth=Enumerable.Range(0,triangles.Length/3).Where(i=>uv[triangles[i*3]].x<.55f&&uv[triangles[i*3+1]].x<.55f&&uv[triangles[i*3+2]].x<.55f)
                    .SelectMany(i=>new[]{triangles[i*3],triangles[i*3+1],triangles[i*3+2]}).ToArray();
                if(cloth.Length==0)throw new InvalidOperationException("ToonTastic canopy is missing.");
                var canopy=new Mesh{name="ToonTastic Umbrella C canopy",indexFormat=mesh.indexFormat,vertices=mesh.vertices,normals=mesh.normals,uv=mesh.uv,triangles=cloth};
                var points=canopy.vertices;var bounds=new Bounds(points[cloth[0]],Vector3.zero);
                foreach(int i in cloth)bounds.Encapsulate(points[i]);canopy.bounds=bounds;
                canopy=Save(canopy,Generated+"Art_Umbrella_Canopy.asset");
                var shader=Shader.Find("Parallax/BeachSurface");
                var textureImporter=AssetImporter.GetAtPath(sourcePath+"ColorPalette_256.png") as TextureImporter;
                if(shader==null||ShaderUtil.ShaderHasError(shader)||textureImporter==null)throw new InvalidOperationException("ToonTastic shader or palette is missing.");
                if(!textureImporter.sRGBTexture||textureImporter.anisoLevel!=8)
                {textureImporter.sRGBTexture=true;textureImporter.anisoLevel=8;textureImporter.SaveAndReimport();}
                var palette=AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath+"ColorPalette_256.png");
                var material=Save(new Material(shader){name="ToonTastic Umbrella palette",color=Color.white,mainTexture=palette},Generated+"Art_Umbrella_0.mat");
                foreach(var f in game.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&f.sharedMesh.name=="umbrella original imported mesh"))
                {
                    f.sharedMesh=mesh;f.GetComponent<Renderer>().sharedMaterials=new[]{material};
                    foreach(var collider in f.GetComponents<MeshCollider>())collider.sharedMesh=mesh;
                }
                foreach(var host in game.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Beach resort parasol · ")).ToArray())
                {
                    var old=host.GetComponentsInChildren<MeshFilter>().First(f=>f.sharedMesh.name.StartsWith("Resort canopy · "));var footprint=old.sharedMesh.bounds;
                    // Inactive authored originals keep independently edited scenes mergeable.
                    foreach(var f in host.GetComponentsInChildren<MeshFilter>().Where(f=>new[]{"Resort canopy · ","Resort valance · ","Resort piping · ","Resort ribs · ","Resort vent · "}.Any(p=>f.sharedMesh.name.StartsWith(p))).ToArray())
                    {f.GetComponent<Renderer>().enabled=false;foreach(var c in f.GetComponents<Collider>())c.enabled=false;f.gameObject.SetActive(false);}
                    var roof=new GameObject("ToonTastic Umbrella C shelter canopy");roof.transform.SetParent(host,false);
                    roof.transform.localScale=new Vector3(footprint.size.x/bounds.size.x,1,footprint.size.z/bounds.size.z);
                    roof.transform.localPosition=new Vector3(0,footprint.max.y-bounds.max.y,0);
                    roof.AddComponent<MeshFilter>().sharedMesh=canopy;var r=roof.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.TwoSided;
                    roof.AddComponent<MeshCollider>().sharedMesh=canopy;
                }
                foreach(var prop in game.props)
                {
                    var roof=prop.GetComponentsInChildren<BoxCollider>().FirstOrDefault(c=>c.name=="파라솔 차양");
                    if(roof!=null)FitParasolCollision(prop,mesh,canopy);
                    prop.Remember();
                }
                // Lift the cloth along the sun ray while extending the mast down to its original floor.
                var entry=game.castleRoute.FixedParasol;var raised=UnityEngine.Object.Instantiate(mesh);raised.name="Castle entrance raised C umbrella";
                var vertices=raised.vertices;float lift=BeachCastleRestoration.EntranceCanopyLift/entry.lossyScale.y;
                for(int i=0;i<vertices.Length;i++)if(vertices[i].y>.1f)vertices[i].y+=lift;
                raised.vertices=vertices;raised.RecalculateBounds();raised=Save(raised,Generated+"Art_Castle_Entrance_Parasol.asset");
                entry.GetComponent<MeshFilter>().sharedMesh=raised;
                foreach(var collider in entry.GetComponents<MeshCollider>())collider.sharedMesh=raised;
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);}
        }
        static void FitParasolCollision(BeachProp prop,Mesh mesh,Mesh canopy)
        {
            UnityEngine.Object.DestroyImmediate(prop.transform.Find("파라솔 차양").gameObject);
            UnityEngine.Object.DestroyImmediate(prop.transform.Find("파라솔 받침").gameObject);
            var vertices=mesh.vertices;var cloth=new HashSet<int>(canopy.triangles);
            var polePoints=mesh.triangles.Where(i=>!cloth.Contains(i)).Select(i=>vertices[i]).ToArray();
            if(polePoints.Length==0)throw new InvalidOperationException("ToonTastic umbrella pole is missing.");
            var poleBounds=new Bounds(polePoints[0],Vector3.zero);foreach(var p in polePoints)poleBounds.Encapsulate(p);
            var pole=prop.transform.Find("파라솔 기둥").GetComponent<BoxCollider>();pole.center=poleBounds.center;pole.size=poleBounds.size;
            var outline=cloth.Select(i=>new Vector2(vertices[i].x,vertices[i].z)).Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();
            var hull=new List<Vector2>();
            for(int pass=0;pass<2;pass++)
            {
                int start=hull.Count;IEnumerable<Vector2> points=pass==0?outline:outline.Reverse();
                foreach(var p in points)
                {
                    while(hull.Count>=start+2)
                    {
                        var a=hull[hull.Count-2];var b=hull[hull.Count-1];
                        if((b.x-a.x)*(p.y-b.y)-(b.y-a.y)*(p.x-b.x)>.000001f)break;
                        hull.RemoveAt(hull.Count-1);
                    }
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count-1);
            }
            // shortcut: thin boxes approximate cloth edges; use convex panels for exact edge contact.
            var triangles=canopy.triangles;int panels=0;var visual=prop.GetComponentInChildren<MeshFilter>().transform;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var points=new[]{vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]]};
                var normal=Vector3.Cross(points[1]-points[0],points[2]-points[0]).normalized;if(normal.y<.5f)continue;
                float area=float.PositiveInfinity;Quaternion rotation=default;Bounds bounds=default;
                for(int edge=0;edge<3;edge++)
                {
                    var candidate=Quaternion.LookRotation((points[(edge+1)%3]-points[edge]).normalized,normal);
                    var inverse=Quaternion.Inverse(candidate);var b=new Bounds(inverse*points[0],Vector3.zero);
                    b.Encapsulate(inverse*points[1]);b.Encapsulate(inverse*points[2]);
                    float value=b.size.x*b.size.z;if(value>=area)continue;
                    area=value;rotation=candidate;bounds=b;
                }
                const float thickness=.03f;
                var flat=points.Select(p=>Quaternion.Inverse(rotation)*p).ToArray();int axis=bounds.size.x>bounds.size.z?0:2;
                const int strips=2;
                for(int strip=0;strip<strips;strip++)
                {
                    float lo=bounds.min[axis]+bounds.size[axis]*strip/strips,hi=bounds.min[axis]+bounds.size[axis]*(strip+1)/strips;
                    var clipped=new List<Vector3>();
                    for(int edge=0;edge<3;edge++)
                    {
                        var a=flat[edge];var b=flat[(edge+1)%3];if(a[axis]>=lo-.000001f&&a[axis]<=hi+.000001f)clipped.Add(a);
                        if(Mathf.Abs(b[axis]-a[axis])<.000001f)continue;
                        foreach(float boundary in new[]{lo,hi})
                        {float t=(boundary-a[axis])/(b[axis]-a[axis]);if(t>=0&&t<=1)clipped.Add(Vector3.Lerp(a,b,t));}
                    }
                    if(clipped.Count<3)continue;
                    Vector3 centre=Vector3.zero;foreach(var p in clipped)centre+=p;centre/=clipped.Count;
                    var interior=(flat[0]+flat[1]+flat[2])/3;
                    if(interior[axis]>=lo&&interior[axis]<=hi)centre=interior;
                    var half=new Vector3(clipped.Max(p=>Mathf.Abs(p.x-centre.x)),thickness*.5f,clipped.Max(p=>Mathf.Abs(p.z-centre.z)));
                    var position=rotation*(centre-Vector3.up*half.y);float fit=1;
                    var across=rotation*(Vector3.right*half.x);var along=rotation*(Vector3.forward*half.z);var skin=rotation*(Vector3.up*half.y);
                    // Keep every panel inside the visible canopy's outer footprint.
                    for(int edge=0;edge<hull.Count;edge++)
                    {
                        var a=hull[edge];var b=hull[(edge+1)%hull.Count];var outward=new Vector3(b.y-a.y,0,a.x-b.x).normalized;
                        float span=Mathf.Abs(Vector3.Dot(outward,across))+Mathf.Abs(Vector3.Dot(outward,along));
                        float room=Vector3.Dot(outward,new Vector3(a.x,0,a.y)-position)-Mathf.Abs(Vector3.Dot(outward,skin));
                        if(span>.000001f)fit=Mathf.Min(fit,room/span);
                    }
                    if(fit<=0)throw new InvalidOperationException("Umbrella panel is outside its visible footprint.");
                    var item=new GameObject("파라솔 천 · "+(++panels));item.transform.SetParent(visual,false);
                    item.transform.localPosition=position;item.transform.localRotation=rotation;
                    item.AddComponent<BoxCollider>().size=new Vector3(half.x*2*fit,thickness,half.z*2*fit);
                }
            }
            if(panels==0)throw new InvalidOperationException("ToonTastic umbrella fabric has no upward panels.");
        }
    }
}
