using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Editor
{
    // One-time data conversion only. Original downloads and their licence
    // records remain outside Assets; the player receives this processed mesh.
    public static class GalleryScanImport
    {
        const string Output="Assets/Parallax/Gallery/Resources/MuseumScans/NikeMesh.asset";
        const uint MaximumTriangles=20000000;

        sealed class Scan
        {
            public readonly List<Vector3> vertices;
            public readonly List<int> triangles;
            readonly Dictionary<Vector3,int> unique;
            public long sourceFaces,degenerateFaces;

            public Scan(int expectedFaces)
            {
                int reserve=(int)Math.Min((long)Math.Max(expectedFaces,1)*3,3000000);
                vertices=new List<Vector3>(Math.Min(reserve,1500000));
                triangles=new List<int>(reserve);
                unique=new Dictionary<Vector3,int>(Math.Min(reserve,1500000));
            }

            int Vertex(Vector3 value)
            {
                Require(Finite(value.x)&&Finite(value.y)&&Finite(value.z),"STL contains NaN or infinite vertex coordinates");
                // This specific Wenman scan is Y-up (its base lies at Y=0).
                // Reflect Z to convert handedness, then reverse triangle winding.
                var converted=new Vector3(value.x,value.y,-value.z);
                if(unique.TryGetValue(converted,out int index))return index;
                index=vertices.Count;unique.Add(converted,index);vertices.Add(converted);return index;
            }

            public void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                sourceFaces++;
                int first=Vertex(a),second=Vertex(b),third=Vertex(c);
                if(first==second||second==third||first==third){degenerateFaces++;return;}
                triangles.Add(first);triangles.Add(third);triangles.Add(second);
            }
        }

        public static void Run()
        {
            string input=Path.GetFullPath(Argument("-nikeStl"));
            Require(File.Exists(input),"STL file does not exist: "+input);
            long bytes=new FileInfo(input).Length;
            Require(bytes>=15,"STL file is empty or truncated");
            Scan scan;
            using(var stream=new FileStream(input,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024,FileOptions.SequentialScan))
            {
                uint binaryCount=0;bool binary=false;
                if(stream.Length>=84)
                {
                    using(var probe=new BinaryReader(stream,Encoding.ASCII,true))
                    {stream.Position=80;binaryCount=probe.ReadUInt32();}
                    long expected=84L+(long)binaryCount*50;
                    binary=binaryCount>0&&binaryCount<=MaximumTriangles&&expected<=stream.Length&&stream.Length-expected<=1024;
                }
                stream.Position=0;
                scan=binary?ReadBinary(stream,binaryCount):ReadAscii(stream);
            }

            Require(scan.sourceFaces>0&&scan.triangles.Count>=3,"STL contains no usable triangle faces");
            Require(scan.vertices.Count>=3,"STL contains fewer than three distinct vertices");
            var minimum=scan.vertices[0];var maximum=minimum;
            foreach(var vertex in scan.vertices){minimum=Vector3.Min(minimum,vertex);maximum=Vector3.Max(maximum,vertex);}
            Vector3 sourceSize=maximum-minimum;
            Require(Finite(sourceSize.x)&&Finite(sourceSize.y)&&Finite(sourceSize.z),"STL bounds are not finite");
            Require(sourceSize.y>1e-7f&&sourceSize.x>1e-7f&&sourceSize.z>1e-7f,"STL has a degenerate width, height or depth: "+sourceSize);
            Require(sourceSize.x/sourceSize.y<100&&sourceSize.z/sourceSize.y<100,"STL bounds are implausible for a statue: "+sourceSize);
            Vector3 origin=new Vector3(minimum.x+sourceSize.x*.5f,minimum.y,minimum.z+sourceSize.z*.5f);
            float inverseHeight=1/sourceSize.y;
            for(int i=0;i<scan.vertices.Count;i++)
            {
                Vector3 normalized=(scan.vertices[i]-origin)*inverseHeight;
                Require(Finite(normalized.x)&&Finite(normalized.y)&&Finite(normalized.z),"Normalization produced a non-finite vertex");
                scan.vertices[i]=normalized;
            }

            var mesh=new Mesh{name="Samothrace Nike · Cosmo scan",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(scan.vertices);mesh.SetTriangles(scan.triangles,0,false);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            Require(mesh.vertexCount==scan.vertices.Count,"Unity mesh lost vertices during import");
            Require(Mathf.Abs(mesh.bounds.size.y-1)<.0001f&&Mathf.Abs(mesh.bounds.min.y)<.0001f,
                "Normalized mesh is not height 1 with its base at 0: "+mesh.bounds);
            Require(Mathf.Abs(mesh.bounds.center.x)<.0001f&&Mathf.Abs(mesh.bounds.center.z)<.0001f,
                "Normalized mesh is not centred on X/Z: "+mesh.bounds);

            EnsureFolder("Assets/Parallax/Gallery/Resources/MuseumScans");
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Output);
            if(existing==null)
            {
                Require(AssetDatabase.LoadMainAssetAtPath(Output)==null,"Output path already contains a different asset type: "+Output);
                AssetDatabase.CreateAsset(mesh,Output);existing=mesh;
            }
            else
            {
                EditorUtility.CopySerialized(mesh,existing);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            EditorUtility.SetDirty(existing);AssetDatabase.SaveAssets();
            Debug.Log("NIKE_SCAN_IMPORT_OK source="+input+" bytes="+bytes+
                " sourceFaces="+scan.sourceFaces+" renderedFaces="+(scan.triangles.Count/3)+
                " discardedDegenerateFaces="+scan.degenerateFaces+" vertices="+existing.vertexCount+
                " originalSize="+sourceSize+" normalizedSize="+existing.bounds.size+" output="+Output);
        }

        static Scan ReadBinary(Stream stream,uint count)
        {
            var scan=new Scan((int)count);stream.Position=84;
            using(var reader=new BinaryReader(stream,Encoding.ASCII,true))
            {
                for(uint face=0;face<count;face++)
                {
                    reader.ReadSingle();reader.ReadSingle();reader.ReadSingle(); // Recompute smooth normals after exact deduplication.
                    var a=Point(reader);var b=Point(reader);var c=Point(reader);
                    reader.ReadUInt16();scan.Triangle(a,b,c);
                    if(face>0&&face%250000==0)Debug.Log("NIKE_SCAN_IMPORT_PROGRESS "+face+"/"+count+" faces");
                }
            }
            return scan;
        }

        static Vector3 Point(BinaryReader reader)
        {return new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());}

        static Scan ReadAscii(Stream stream)
        {
            var scan=new Scan(100000);var face=new Vector3[3];int pending=0;
            using(var reader=new StreamReader(stream,Encoding.ASCII,false,1024*1024,true))
            {
                string first=reader.ReadLine();
                Require(first!=null&&first.TrimStart().StartsWith("solid",StringComparison.OrdinalIgnoreCase),
                    "File is neither a valid binary STL nor an ASCII STL beginning with solid");
                string line;
                while((line=reader.ReadLine())!=null)
                {
                    line=line.Trim();
                    if(!line.StartsWith("vertex",StringComparison.OrdinalIgnoreCase))continue;
                    var fields=line.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
                    Require(fields.Length==4,"Malformed ASCII STL vertex: "+line);
                    face[pending++]=new Vector3(Parse(fields[1]),Parse(fields[2]),Parse(fields[3]));
                    if(pending!=3)continue;
                    scan.Triangle(face[0],face[1],face[2]);pending=0;
                    Require(scan.sourceFaces<=MaximumTriangles,"ASCII STL exceeds the triangle safety limit");
                }
            }
            Require(pending==0,"ASCII STL ends with an incomplete triangle");return scan;
        }

        static float Parse(string text)
        {
            Require(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)&&Finite(value),
                "Invalid ASCII STL coordinate: "+text);
            return value;
        }
        static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
        static string Argument(string name)
        {
            var arguments=Environment.GetCommandLineArgs();int index=Array.IndexOf(arguments,name);
            if(index<0||index+1>=arguments.Length)throw new ArgumentException("Missing command-line argument: "+name);
            return arguments[index+1];
        }
        static void EnsureFolder(string path)
        {
            string[] pieces=path.Split('/');string current=pieces[0];
            for(int i=1;i<pieces.Length;i++)
            {
                string next=current+"/"+pieces[i];
                if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,pieces[i]);
                current=next;
            }
        }
        static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("NIKE_SCAN_IMPORT: "+message);}
    }
}
