using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadeLink.Library;
using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class LibraryAssetImport
    {
        const string Source = "Assets/ThirdParty/RinkuChoudhary/LibraryKit.fbx";
        const string Generated = "Assets/Parallax/Library/Generated/OriginalKit/";
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        [Serializable] class Part { public string name, path; public Vector3 center, size, rotation, scale; public int vertices; public string[] materials; }
        [Serializable] class Inventory { public Part[] parts; }
        [Serializable] class MeshPreview { public string name; public Vector3[] vertices; public int[] triangles; }
        [Serializable] class GroupPreview { public string name; public MeshPreview[] meshes; }
        static string Hierarchy(Transform t) { return t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name; }
        public static void Inspect()
        {
            var importer = AssetImporter.GetAtPath(Source) as ModelImporter;
            if (importer == null) throw new Exception("Original LibraryKit.fbx not imported");
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var instance = UnityEngine.Object.Instantiate(source);
            var parts = new List<Part>();
            foreach (var groupName in new[] { "chair1", "shell1", "small_shell1", "table1", "table_03", "long_table1", "desk", "book_set1" })
            {
                var group = instance.GetComponentsInChildren<Transform>().First(t => t.name == groupName);
                var meshes = group.GetComponentsInChildren<MeshFilter>().Select(f => new MeshPreview {
                    name = f.name, vertices = f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v)).ToArray(), triangles = f.sharedMesh.triangles }).ToArray();
                Directory.CreateDirectory("Validation/Library/SourcePreview");
                File.WriteAllText("Validation/Library/SourcePreview/" + groupName + ".json", JsonUtility.ToJson(new GroupPreview { name=groupName, meshes=meshes }));
            }
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                parts.Add(new Part { name = filter.name, path = Hierarchy(filter.transform),
                    center = renderer.bounds.center, size = renderer.bounds.size,
                    rotation = filter.transform.eulerAngles, scale = filter.transform.lossyScale,
                    vertices = filter.sharedMesh.vertexCount, materials = renderer.sharedMaterials.Select(m => m == null ? "NULL" : m.name).ToArray() });
            }
            UnityEngine.Object.DestroyImmediate(instance);
            Directory.CreateDirectory("Validation/Library");
            File.WriteAllText("Validation/Library/asset-inventory.json", JsonUtility.ToJson(new Inventory { parts = parts.ToArray() }, true));
            Debug.Log("LIBRARY_ASSET_INSPECT_OK parts=" + parts.Count);
        }
        public static void Integrate(LibraryGame game)
        {
            var importer = AssetImporter.GetAtPath(Source) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Download the original Medieval Library Kit FBX first.");
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            Directory.CreateDirectory(Generated); materials.Clear(); meshes.Clear();
            var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
            var all = source.GetComponentsInChildren<MeshFilter>();
            Func<string, string, MeshFilter> part = (group, name) => all.First(f => f.name == name && f.transform.parent.name == group);
            var board = part("small_shell1", "shell.019");
            var boardRotation = Flatten(board);
            var pillar = part("small_shell1", "Cylinder.009");
            int index = 0, count = 0;
            foreach (var prop in game.props)
            {
                var anchor = prop.transform.Cast<Transform>().First(t => t.name.StartsWith("Kit visual anchor"));
                foreach (var renderer in prop.renderers)
                    if (!renderer.name.Contains("seal")) renderer.enabled = false;
                if (prop.kind == "Book")
                {
                    string[] coverNames = { "Cube.029", "Cube.041", "Cube.043" };
                    string[] pageNames = { "Cube.030", "Cube.042", "Cube.044" };
                    int variant = index++ % 3;
                    var cover = part("book_set1", coverNames[variant]);
                    Add("Book" + variant, new[] { cover, part("book_set1", pageNames[variant]) }, Flatten(cover),
                        Vector3.zero, new Vector3(.48f,.8f,.12f), anchor);
                }
                else if (prop.kind == "Bookshelf")
                {
                    // Reassemble the author's timber, carved uprights and shelves into three
                    // accessible bays. Visible shelves occupy the same collider faces as the books.
                    foreach (var collider in prop.parts)
                    {
                        var at = collider.transform.localPosition;
                        var size = collider.transform.localScale;
                        if (collider.name == "Shelf rear panel")
                            Add("ShelfBack", new[] { board }, Quaternion.Euler(90,0,0)*boardRotation, at, size, anchor);
                        else if (collider.name == "Shelf side")
                            Add("CarvedUpright", new[] { pillar }, boardRotation, at, size, anchor);
                        else Add("ShelfBoard", new[] { board }, boardRotation, at, size, anchor);
                    }
                    // A gothic crest from the original small bookcase lies on its solid top board.
                    Add("GothicCrest", new[] { part("small_shell1", "shell.026") }, boardRotation,
                        new Vector3(0,1.435f,-.32f),new Vector3(2.08f,.115f,.055f),anchor);
                }
                else if (prop.kind == "Chair")
                {
                    Add("Chair", all.Where(f => f.transform.parent.name == "chair1").ToArray(), Quaternion.identity,
                        Vector3.zero,new Vector3(.76f,1.1f,.76f),anchor);
                }
                else if (prop.kind == "Table")
                {
                    var table = part("table1","Cube.015");
                    // The central shelf is omitted from the source worktable to leave the
                    // walk-under puzzle open. Its original tabletop and leg geometry remain.
                    Add("OpenWorktable", new[] { table }, Flatten(table), Vector3.zero,
                        new Vector3(2.2f,.9f,1.7f),anchor,true);
                }
                else throw new InvalidOperationException("Unknown library prop kind: " + prop.kind);
                prop.Remember(); count++;
            }
            // Reuse original oak on structural beams; no decorative books are baked into scenery.
            var timber = new Material(MaterialFor("shell")) { name="Rinku - structural oak" };
            // The left half of the original atlas contains uninterrupted boards.
            timber.mainTextureScale=new Vector2(.52f,1);timber.mainTextureOffset=Vector2.zero;
            LibraryWorld.Materials.Add(timber);
            foreach (var renderer in game.GetComponentsInChildren<Renderer>())
                if (renderer.GetComponentInParent<LibraryProp>() == null && renderer.sharedMaterial != null &&
                    (renderer.sharedMaterial.name.Contains("oak") || renderer.sharedMaterial.name.Contains("Oak")))
                    renderer.sharedMaterial = timber;
            UnityEngine.Object.DestroyImmediate(source);
            Directory.CreateDirectory("Documentation/ThirdParty");
            File.WriteAllText("Documentation/ThirdParty/Library-Asset-Integration.json",
                "{\n  \"asset\": \"Medieval library kit\",\n  \"author\": \"Rinku Choudhary\",\n  \"sourceUrl\": \"https://sketchfab.com/3d-models/medieval-library-kit-c018093ba87d45f28f30ec8acf2f6604\",\n  \"license\": \"Free Standard (NoAI)\",\n  \"source\": \""+Source+"\",\n  \"integratedProps\": "+count+",\n  \"individualBooks\": "+index+",\n  \"adaptations\": \"Original cover/page pairs separated into individual books; carved shelf components rebuilt around three accessible shelves; original worktable lower storage shelves removed for passage. Compound box physics and analytic shadow silhouettes are retained.\"\n}");
            Debug.Log("LIBRARY_ORIGINAL_KIT_INTEGRATED props="+count+" books="+index);
        }
        static Quaternion Flatten(MeshFilter filter)
        {
            var vertices=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
            var center=vertices.Aggregate(Vector3.zero,(a,b)=>a+b)/vertices.Length;
            double xx=0,zz=0,xz=0;
            foreach(var v in vertices){var d=v-center;xx+=d.x*d.x;zz+=d.z*d.z;xz+=d.x*d.z;}
            return Quaternion.Euler(0,(float)(Math.Atan2(2*xz,xx-zz)*.5*Mathf.Rad2Deg),0);
        }
        static Material MaterialFor(string name)
        {
            if(materials.TryGetValue(name,out var ready))return ready;
            string texture = name=="book_03"?"book_cover_2.jpg":name=="book_02"?"book_cover_1.jpg":
                name=="book_cover_01"?"book_cover.jpg":name=="chair"?"chair_lowpoly_chair_BaseColor.png":
                name=="small_shell"?"small_shell_lowpoly_small_shell_BaseColor.png":
                name=="table"?"table_lowpoly_table_BaseColor.png":"shell_lowpoly_shell_BaseColor.png";
            var mat=new Material(Shader.Find("Parallax/LibrarySurface")){name="Rinku - "+name,color=name=="page"?new Color(.88f,.84f,.70f):Color.white};
            if(name!="page")
            {
                mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/RinkuChoudhary/Textures/"+texture);
                if(mat.mainTexture==null)throw new InvalidOperationException("Missing original texture "+texture);
            }
            LibraryWorld.Materials.Add(mat);materials.Add(name,mat);return mat;
        }
        static void Add(string key,MeshFilter[] sources,Quaternion rotation,Vector3 at,Vector3 size,Transform parent,bool openTable=false)
        {
            var vertices=sources.SelectMany(f=>f.sharedMesh.vertices.Select(v=>rotation*f.transform.TransformPoint(v))).ToArray();
            Bounds bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
            var factor=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
            for(int n=0;n<sources.Length;n++)
            {
                var filter=sources[n];string meshKey=key+"_"+n+"_"+size.ToString("F4").Replace(" ","");
                if(!meshes.TryGetValue(meshKey,out var mesh))
                {
                    mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="Rinku original · "+key;
                    var v=mesh.vertices;
                    for(int i=0;i<v.Length;i++)v[i]=Vector3.Scale(rotation*filter.transform.TransformPoint(v[i])-bounds.center,factor);
                    mesh.vertices=v;
                    if(openTable)
                    {
                        // Disconnected middle shelf has 24 vertices; the lower shelf faces
                        // stay below 20% of this model. Remove their triangles, keeping legs.
                        var triangles=mesh.triangles;
                        var parentIds=Enumerable.Range(0,v.Length).ToArray();
                        Func<int,int> root=null;root=i=>parentIds[i]==i?i:(parentIds[i]=root(parentIds[i]));
                        for(int i=0;i<v.Length;i++)for(int j=0;j<i;j++)if((v[i]-v[j]).sqrMagnitude<.00000001f)parentIds[root(i)]=root(j);
                        for(int i=0;i<triangles.Length;i+=3){parentIds[root(triangles[i+1])]=root(triangles[i]);parentIds[root(triangles[i+2])]=root(triangles[i]);}
                        var largest=Enumerable.Range(0,v.Length).GroupBy(i=>root(i)).OrderByDescending(g=>g.Count()).First().Key;
                        var kept=new List<int>();
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                            if(root(a)!=largest || Mathf.Max(v[a].y,Mathf.Max(v[b].y,v[c].y)) < -size.y*.30f)continue;
                            kept.Add(a);kept.Add(b);kept.Add(c);
                        }
                        mesh.triangles=kept.ToArray();
                    }
                    mesh.RecalculateBounds();mesh.RecalculateNormals();
                    string assetPath=Generated+meshKey+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                    if(existing==null)AssetDatabase.CreateAsset(mesh,assetPath);
                    else {EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
                    meshes.Add(meshKey,mesh);
                }
                var go=new GameObject("Rinku Choudhary · "+key+" · "+n);go.transform.SetParent(parent,false);go.transform.localPosition=at;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials=filter.GetComponent<Renderer>().sharedMaterials.Select(m=>MaterialFor(m.name)).ToArray();
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
        }
    }
}
