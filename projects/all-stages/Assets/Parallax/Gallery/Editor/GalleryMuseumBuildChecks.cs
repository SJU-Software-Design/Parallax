using System;
using UnityEngine;
using ShadeLink.Gallery;

namespace ShadeLink.Editor
{
    // Regenerates the museum and checks its authored state and shared controls.
    public static class GalleryMuseumBuildChecks
    {
        public static void Run()
        {
            ObjectInteractionTests.Run();
            ObjectInteractionTests.RunFeel();
            ObjectInteractionTests.RunPhysicsPolicies();
            GalleryBuild.Prepare();
            var game=UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            Require(game!=null,"Museum scene has no game controller");
            if(game.conceptLayout)
            {
                Require(!game.exhibitionPreview,"The playable museum must enable the shadow course");
                Require(Resources.Load<Mesh>("MuseumScans/NikeMesh")!=null,"Actual Nike scan is missing");
                Require(game.GetComponentInChildren<GalleryConceptPerspective>()!=null,"Perspective exhibit missing");
                Require(game.props.Length==6,"Expected bronze, Nike, three portrait parts and Cathedral");
                Require(game.shadowCourse!=null&&game.shadowCourse.bronze!=null&&game.shadowCourse.statue!=null,"Shadow interaction exhibits missing");
                Require(game.nikeLight!=null&&game.nikeLight.statue==game.shadowCourse.statue&&game.nikeLight.fixedLamp!=null&&
                    game.nikeLight.emitter!=null&&game.nikeLight.fixedLamp.GetComponentInParent<GalleryProp>()==null,"Nike fixed light references missing");
                Require(game.nikeLight.canvas!=null&&!game.nikeLight.canvas.Complete&&game.nikeLight.canvas.portablePainting!=null&&
                    !game.nikeLight.canvas.portablePainting.gameObject.activeSelf&&!game.nikeLight.SolutionReady,"Nike gallery must begin at the silhouette puzzle");
                Require(!game.shadowCourse.NikeUnlocked&&!game.shadowCourse.NikeGateOpen&&game.shadowCourse.NikeGateColliders.Length>0,"Nike gate must begin closed");
                var socket=game.shadowCourse.nikeSocket;
                Require(socket!=null&&socket.canvas==game.nikeLight.canvas&&socket.splitPainting.Length==2&&
                    Array.TrueForAll(socket.splitPainting,half=>!half.activeSelf),"Nike door must start with an empty painting socket");
                Require(game.portraitGate!=null&&!game.portraitGate.Opened&&game.portraitGate.barrier.enabled,"Portrait gate must begin closed");
                Require(game.orcusExit!=null&&game.orcusExit.prop.homeScale.x<game.orcusExit.RequiredScale,"Exit must begin smaller than a player");
                Require(game.orcusExit.customOpening&&!game.orcusExit.windEnabled,"Cathedral must use its measured opening and remain silent");
                Require(Resources.Load<Mesh>("MuseumScans/Cathedral/CathedralMesh")!=null,"Actual Rodin scan missing");
                Require(game.orcusExit.cathedralActivation!=null,"The sculpture's opening activation is missing");
                Require(game.orcusExit.GetComponent<GalleryCathedralPortal>()!=null,"View-dependent library window missing");
                Require(game.GetComponentInChildren<GalleryConceptLighting>().roomProbes.Length==4,"Room reflection probes missing");
            }
            else
            {
                Require(game.GetComponentInChildren<GalleryOrcusExit>()!=null,"Museum is missing the enlarged-mouth exit");
                Require(game.mirror!=null&&game.lightGate!=null,"Existing reflected-light puzzle was lost");
                Require(game.props.Length>=11,"Original interactive exhibits or exit were lost");
            }
            foreach(string key in new[]{"StarryNight","Scream","WaterLilyBridge","Ambassadors","GirlWithPearlEarring"})
                Require(Resources.Load<Texture2D>("MuseumArt/"+key)!=null,"Missing real artwork: "+key);
            game.lighting.Rebuild();
            Require(game.lighting.Count<=GalleryLighting.MaxCasters,"Museum exceeds its shadow budget");
            Physics.SyncTransforms();
            foreach(var prop in game.props)
            {
                Require(prop!=null&&prop.parts!=null&&prop.parts.Length>0,"Exhibit has no physical placement shape");
                foreach(var part in prop.parts)Require(part.size.x>0&&part.size.y>0&&part.size.z>0,"Exhibit has a degenerate collider");
                Require(game.ValidPlacement(prop,out var restriction),"Initial exhibit overlaps the museum: "+prop.title+" / "+restriction);
            }
            Debug.Log("MUSEUM_BUILD_CHECKS_OK props="+game.props.Length+" casters="+game.lighting.Count);
        }
        static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("MUSEUM_BUILD_CHECK: "+message);}
    }
}
