using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static partial void ConfigureCoastalRental(BeachGame game)
        {
            var colliders=game.GetComponentsInChildren<Collider>(true);
            var shadows=game.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.shadowCastingMode!=ShadowCastingMode.Off)
                .ToDictionary(r=>r,r=>new{r.bounds,r.shadowCastingMode});
            var details=new GameObject("Coastal Rental detailing").transform;details.SetParent(game.transform,false);
            var timber=CoastalWood("Coastal Rental salt-weathered timber",new Color(.82f,.74f,.60f));
            var teal=CoastalWood("Coastal Rental worn teal paint",new Color(.12f,.39f,.40f),true);
            var ivory=CoastalWood("Coastal Rental ivory painted trim",new Color(.94f,.91f,.81f),true);
            var canvas=CoastalLinen("Coastal Rental ivory sign canvas",new Color(.94f,.91f,.80f));
            var ink=new Color(.055f,.25f,.28f);

            var hut=game.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="Surf rental hut");
            if(hut==null)throw new InvalidOperationException("The original surf rental hut is missing.");
            var filter=hut.GetComponent<MeshFilter>();var renderer=hut.GetComponent<MeshRenderer>();
            var original=filter.sharedMesh;var oldMaterials=renderer.sharedMaterials;
            if(original.subMeshCount!=9||oldMaterials.Length!=9)throw new InvalidOperationException("The rental hut material slots changed.");
            var mesh=UnityEngine.Object.Instantiate(original);mesh.name="Coastal Rental original hut with surface groups";
            var white=original.GetTriangles(3);var coral=original.GetTriangles(5);
            // shortcut: the source starts with 12 white wall/counter panels and one coral awning; re-map if hirehut changes.
            const int wallIndices=12*108*3,awningIndices=108*3;
            if(white.Length!=4952*3||coral.Length!=3872*3)throw new InvalidOperationException("The original rental panel groups changed.");
            mesh.subMeshCount=11;
            mesh.SetTriangles(white.Skip(wallIndices).ToArray(),3);mesh.SetTriangles(white.Take(wallIndices).ToArray(),9);
            mesh.SetTriangles(coral.Skip(awningIndices).ToArray(),5);mesh.SetTriangles(coral.Take(awningIndices).ToArray(),10);
            if(!mesh.vertices.SequenceEqual(original.vertices)||mesh.triangles.Length!=original.triangles.Length||mesh.bounds!=original.bounds)
                throw new InvalidOperationException("Rental surface groups must preserve the original geometry and roof shadow.");
            var materials=new Material[11];Array.Copy(oldMaterials,materials,9);
            materials[1]=timber;materials[2]=teal;materials[3]=ivory;
            materials[5]=CoastalMaterial("Coastal Rental coral painted roof",new Color(.88f,.29f,.20f),.12f,.26f);
            materials[7]=CoastalLinen("Coastal Rental dark wetsuit fabric",new Color(.12f,.15f,.16f));
            materials[9]=teal;materials[10]=CoastalLinen("Coastal Rental short coral canvas awning",new Color(.88f,.29f,.20f));
            filter.sharedMesh=mesh;renderer.sharedMaterials=materials;

            var signMesh=CoastalRoundedBoxMesh(new Vector3(1,1,.35f),.04f);signMesh.name="Coastal Rental slim rounded sign frame";
            var signs=game.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Beach rental sign").ToArray();
            if(signs.Length!=4)throw new InvalidOperationException("Expected the four original beach rental signs.");
            foreach(var sign in signs)
            {
                var board=sign.Find("Sign board");board.GetComponent<MeshFilter>().sharedMesh=signMesh;board.GetComponent<Renderer>().sharedMaterial=timber;
                foreach(Transform child in sign)if(child.name=="Sign timber leg")child.GetComponent<Renderer>().sharedMaterial=timber;
                var accent=new GameObject("Coastal Rental canvas sign").transform;accent.SetParent(details,false);
                accent.SetPositionAndRotation(sign.position,sign.rotation);accent.localScale=sign.lossyScale;
                CoastalBox(accent,"Coastal Rental inset sign canvas",new Vector3(0,1.05f,-.024f),new Vector3(1.18f,.82f,.012f),canvas,.006f);
                var label=sign.GetComponentInChildren<BeachLabel>();label.style=FontStyle.Bold;label.color=ink;
                label.transform.localPosition=new Vector3(0,1.05f,-.037f);
            }

            var goal=game.transform.Find("모래성 방향 안내판");var post=game.transform.Find("모래성 방향 안내판 기둥");
            if(goal==null||post==null)throw new InvalidOperationException("The original castle direction sign is missing.");
            goal.GetComponent<MeshFilter>().sharedMesh=signMesh;goal.GetComponent<Renderer>().sharedMaterial=timber;post.GetComponent<Renderer>().sharedMaterial=timber;
            var goalCanvas=CoastalBox(details,"Coastal Rental castle direction canvas",goal.localPosition+Vector3.back*.036f,new Vector3(2.47f,.79f,.012f),canvas,.006f);
            var goalLabel=game.GetComponentsInChildren<BeachLabel>().Single(l=>l.caption.StartsWith("목표 · 모래성 정상"));
            goalLabel.style=FontStyle.Bold;goalLabel.color=ink;goalLabel.transform.position=goalCanvas.transform.position+Vector3.back*.012f;

            if(!colliders.SequenceEqual(game.GetComponentsInChildren<Collider>(true)))
                throw new InvalidOperationException("Rental detailing must preserve every original collider and add no solids.");
            foreach(var caster in game.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.shadowCastingMode!=ShadowCastingMode.Off))
                if(!shadows.TryGetValue(caster,out var before)||caster.shadowCastingMode!=before.shadowCastingMode||
                    !before.bounds.Contains(caster.bounds.min)||!before.bounds.Contains(caster.bounds.max))
                    throw new InvalidOperationException("Rental detailing must not add or enlarge shelter shadows: "+caster.name);
        }
    }
}
