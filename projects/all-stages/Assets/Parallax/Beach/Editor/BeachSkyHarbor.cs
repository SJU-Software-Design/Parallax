using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        [Serializable] sealed class SailLogoEdit
        {
            public string node,sourceAsset,generatedAsset;
            public Vector2 blankMin,blankMax,logoMin,logoMax;
            public int changedUVVertices,unchangedLogoVertices,vertexCount,triangleIndexCount;
            public bool geometryUnchanged,normalsUnchanged,trianglesUnchanged;
        }
        [Serializable] sealed class SailLogoEdits
        {public string ship,operation;public SailLogoEdit[] meshes;}
        // The source model already has two physical cloth faces. Its stern face
        // uses a blank atlas tile; Cull Off would retain that blank surface.
        // Derive both atlas rectangles from the actual imported vertex UVs, and
        // change UV0 on a generated copy only. No geometry or material is edited.
        static void ConfigurePirateSailLogos(GameObject ship)
        {
            var edits=new System.Collections.Generic.List<SailLogoEdit>();
            foreach(string name in new[]{"StylShip_SailMid1","StylShip_SailFront"})
            {
                var source=ship.GetComponentsInChildren<MeshFilter>().SingleOrDefault(f=>f.name==name);
                if(source==null||source.sharedMesh==null||!source.sharedMesh.isReadable)
                    throw new InvalidOperationException("The pirate sail mesh is unavailable: "+name);
                var original=source.sharedMesh;var uv=original.uv;var normals=original.normals;
                if(uv.Length!=original.vertexCount||normals.Length!=original.vertexCount)
                    throw new InvalidOperationException("The pirate sail UV/normal data is incomplete: "+name);
                Vector3 bow=source.transform.InverseTransformDirection(ship.transform.forward).normalized;
                var blank=new System.Collections.Generic.List<int>();var logo=new System.Collections.Generic.List<int>();
                for(int i=0;i<normals.Length;i++)
                {
                    float facing=Vector3.Dot(normals[i],bow);
                    if(facing<-.0001f)blank.Add(i);else if(facing>.0001f)logo.Add(i);
                }
                if(blank.Count==0||logo.Count==0)throw new InvalidOperationException("Both physical sail faces are required: "+name);
                void Extents(System.Collections.Generic.List<int> indices,out Vector2 minimum,out Vector2 maximum)
                {
                    minimum=Vector2.one*float.PositiveInfinity;maximum=Vector2.one*float.NegativeInfinity;
                    foreach(int i in indices){minimum=Vector2.Min(minimum,uv[i]);maximum=Vector2.Max(maximum,uv[i]);}
                    if(maximum.x-minimum.x<.001f||maximum.y-minimum.y<.001f)
                        throw new InvalidOperationException("The pirate sail atlas rectangle is degenerate: "+name);
                }
                Extents(blank,out var blankMin,out var blankMax);Extents(logo,out var logoMin,out var logoMax);
                // Confirm the two source-specific atlas tiles found in the FBX
                // and official albedo, without guessed numeric crop boundaries.
                if(Rect.MinMaxRect(blankMin.x,blankMin.y,blankMax.x,blankMax.y).Overlaps(Rect.MinMaxRect(logoMin.x,logoMin.y,logoMax.x,logoMax.y))||
                    (name=="StylShip_SailMid1"&&blankMin.y>=logoMin.y)||(name=="StylShip_SailFront"&&blankMin.y<=logoMin.y))
                    throw new InvalidOperationException("Unexpected sail face/atlas orientation: "+name);
                var changed=(Vector2[])uv.Clone();
                foreach(int i in blank)
                {
                    Vector2 t=new Vector2((uv[i].x-blankMin.x)/(blankMax.x-blankMin.x),(uv[i].y-blankMin.y)/(blankMax.y-blankMin.y));
                    changed[i]=new Vector2(Mathf.Lerp(logoMin.x,logoMax.x,t.x),Mathf.Lerp(logoMin.y,logoMax.y,t.y));
                }
                var copy=UnityEngine.Object.Instantiate(original);copy.name="Pirate sail logo · "+name;copy.uv=changed;
                bool sameVertices=original.vertices.SequenceEqual(copy.vertices),sameNormals=normals.SequenceEqual(copy.normals),sameTriangles=original.triangles.SequenceEqual(copy.triangles);
                if(!sameVertices||!sameNormals||!sameTriangles||logo.Any(i=>changed[i]!=uv[i]))
                    throw new InvalidOperationException("Sail logo generation modified source geometry or the existing logo face: "+name);
                string path=Generated+copy.name+".asset";var saved=Save(copy,path);source.sharedMesh=saved;
                if(PrefabUtility.IsPartOfPrefabInstance(source))PrefabUtility.RecordPrefabInstancePropertyModifications(source);
                foreach(var collider in source.GetComponents<MeshCollider>())if(collider.sharedMesh==original)
                {
                    collider.sharedMesh=saved;
                    if(PrefabUtility.IsPartOfPrefabInstance(collider))PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
                edits.Add(new SailLogoEdit{node=name,sourceAsset=AssetDatabase.GetAssetPath(original),generatedAsset=path,
                    blankMin=blankMin,blankMax=blankMax,logoMin=logoMin,logoMax=logoMax,changedUVVertices=blank.Count,unchangedLogoVertices=logo.Count,
                    vertexCount=original.vertexCount,triangleIndexCount=original.triangles.Length,geometryUnchanged=sameVertices,normalsUnchanged=sameNormals,trianglesUnchanged=sameTriangles});
            }
            var record=new SailLogoEdits{ship=ship.name,operation="UV0-only blank stern tile to matching original bow logo tile; source assets and geometry unchanged",meshes=edits.ToArray()};
            string report=Generated+"Pirate sail logo edits - "+ship.name.Replace('/','_')+".json";
            File.WriteAllText(report,JsonUtility.ToJson(record,true));AssetDatabase.ImportAsset(report,ImportAssetOptions.ForceUpdate);
        }
        // Preserve PR42 authored mesh/box collisions and add exact non-convex
        // source meshes only when collision is absent. During flight the rider is
        // controller-disabled, so moving this hierarchy cannot push its capsule.
        static void ConfigurePirateShipColliders(GameObject ship)
        {
            foreach(var source in ship.GetComponentsInChildren<MeshFilter>())
            {
                if(source.sharedMesh==null||!source.sharedMesh.isReadable)
                    throw new InvalidOperationException("A pirate ship collision mesh is missing or unreadable: "+source.name);
                if(source.GetComponent<Collider>()!=null)continue;
                source.gameObject.AddComponent<MeshCollider>().sharedMesh=source.sharedMesh;
            }
        }
        // The new harbor puzzle owns the approach to (.4, 3.06, 123).
        // Only the real ship and its final deck connection belong here;
        // PR42's sandcastle bypass and shore-side ramp are deliberately absent.
        internal static void ConfigureSkyHarbor(BeachGame game)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/StylizedPirateShip/StylShip_Unity.prefab");
            if(prefab==null)throw new InvalidOperationException("The sky ship prefab is missing.");
            if(game.skyShip!=null)throw new InvalidOperationException("The sky ship is already configured.");
            var timber=AssetDatabase.LoadAssetAtPath<Material>(Generated+"Weathered timber.mat");
            var ink=AssetDatabase.LoadAssetAtPath<Material>(Generated+"Deep navy.mat");
            var canvas=AssetDatabase.LoadAssetAtPath<Material>(Generated+(game.counterweightFinale!=null?"Counterweight · warm sailcloth.mat":"Harbor finale · tan canvas.mat"));
            if(timber==null||ink==null||canvas==null)throw new InvalidOperationException("Sky harbor materials are missing.");
            var harbor=new GameObject("05 · 마지막 하늘 항구");harbor.transform.SetParent(game.transform,false);
            var end=game.transform.Find("끝 둑");if(end!=null)UnityEngine.Object.DestroyImmediate(end.gameObject);
            var ocean=game.transform.Find("수평선");if(ocean!=null)ocean.localScale=new Vector3(6000,.1f,6000);
            var ship=UnityEngine.Object.Instantiate(prefab,harbor.transform);
            ship.name="하늘행 해적선";ship.transform.SetPositionAndRotation(new Vector3(0,2.5f,132),Quaternion.identity);ship.transform.localScale=Vector3.one*.4f;
            var rearRail=ship.GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name=="StylShip_FencingBack");
            if(rearRail==null)throw new InvalidOperationException("The ship rear railing cannot be identified.");
            UnityEngine.Object.DestroyImmediate(rearRail.gameObject);
            var deck=ship.GetComponentsInChildren<MeshFilter>().SingleOrDefault(f=>f.name=="StylShip_FloorBackTop");
            if(deck==null||deck.sharedMesh==null)throw new InvalidOperationException("The real upper deck mesh is missing.");
            ConfigurePirateSailLogos(ship);
            ConfigurePirateShipColliders(ship);
            var flight=ship.AddComponent<BeachSkyShip>();flight.Game=game;game.skyShip=flight;
            BeachWorld.Box("갑판으로 잇는 승선교",new Vector3(.4f,2.99f,124.85f),new Vector3(2.4f,.14f,3.7f),timber,harbor.transform);
            // The unfolding approach joins this fixed boarding canopy. Offset
            // toward the actual sun so its shadow covers z122.8..127, including
            // the true deck contact; the physical posts sit outside the walkway.
            const float walkY=3.06f,awningHeight=4.4f;
            Vector3 sunOffset=BeachLighting.ToSun*(awningHeight/BeachLighting.ToSun.y);sunOffset.y=0;
            Vector3 canopyCenter=new Vector3(.4f,walkY+awningHeight,124.9f)+sunOffset;
            var awning=BeachWorld.Box("승선교 · 실제 고정 천막",canopyCenter,new Vector3(4.8f,.055f,4.2f),canvas,harbor.transform,true,true);
            awning.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            foreach(float x in new[]{-2.28f,2.28f})foreach(float z in new[]{-1.98f,1.98f})
            {
                // The basin is deep enough to store the whole covered bridge.
                // Continue each real pile through the water into that seabed.
                const float pileBottom=-13.15f;
                float postX=x<0?-2.7f:canopyCenter.x+x;
                float height=canopyCenter.y-pileBottom;
                Vector3 basePoint=new Vector3(postX,pileBottom,canopyCenter.z+z);
                BeachWorld.Box("승선교 천막 · 바깥 지지 말뚝",basePoint+Vector3.up*(height*.5f),new Vector3(.14f,height,.14f),timber,harbor.transform);
                BeachWorld.Box("승선교 천막 · 해저 매입 기초",basePoint+Vector3.up*.12f,new Vector3(.45f,.24f,.45f),ink,harbor.transform);
                float cornerX=canopyCenter.x+x;
                if(Mathf.Abs(postX-cornerX)>.01f)
                    BeachWorld.Box("승선교 천막 · 외측 지지 연결보",new Vector3((postX+cornerX)*.5f,canopyCenter.y-.08f,basePoint.z),new Vector3(Mathf.Abs(postX-cornerX)+.14f,.16f,.16f),timber,harbor.transform);
            }
            foreach(float x in new[]{-1.02f,1.82f})
            {
                foreach(float z in new[]{123.1f,126.6f})
                    BeachWorld.Box("승선교 난간 기둥",new Vector3(x,3.55f,z),new Vector3(.09f,1.1f,.09f),timber,harbor.transform);
                BeachWorld.Box("승선교 끝 손잡이",new Vector3(x,4.04f,124.85f),new Vector3(.07f,.07f,3.7f),ink,harbor.transform,false,false);
            }
        }
    }
}
