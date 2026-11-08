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
                var canvas=game.nikeLight.canvas;
                Require(canvas.BrushSeconds>=2.5f&&!canvas.Drawing&&canvas.BrushProgress==0,
                    "Nike painting must begin before its diagonal brush reveal");
                Require(canvas.painting.sharedMaterial.shader.name=="Parallax/MuseumNikeBrush"&&
                    canvas.painting.sharedMaterial.mainTexture==Resources.Load<Texture2D>("MuseumArt/NikeShadowStudy"),
                    "The brush reveal must use the teammate's completed Nike painting");
                var socket=game.shadowCourse.nikeSocket;
                Require(socket!=null&&socket.canvas==game.nikeLight.canvas&&socket.splitPainting.Length==2&&
                    Array.TrueForAll(socket.splitPainting,half=>!half.activeSelf),"Nike door must start with an empty painting socket");
                Require(game.portraitGate!=null&&!game.portraitGate.Opened&&game.portraitGate.barrier.enabled,"Portrait gate must begin closed");
                var portraitRoom=game.GetComponentInChildren<GalleryPortraitRoom>();
                Require(portraitRoom!=null&&portraitRoom.originalArtwork!=null&&portraitRoom.installationArtwork!=null&&portraitRoom.headOutline!=null,
                    "Portrait room needs both Mae West references and its face-shaped doorway outline");
                Require(!game.portraitGate.Performing&&game.portraitGate.performance!=null&&game.portraitGate.MoodRed01==0,
                    "Portrait performance must begin idle with warm decorative lighting");
                foreach(var light in game.GetComponentsInChildren<Light>(true))
                    Require(light.type==LightType.Directional,"Decorative museum lighting must not introduce point or spot lights");
                for(float z=29.5f;z<44;z+=.5f)for(float x=-7;x<=7;x+=.5f)
                    Require(!game.lighting.IsSunlit(new Vector3(x,.08f,z),Vector3.up),"Portrait walking floor receives direct or reflected sunlight");
                Require(game.orcusExit!=null&&game.orcusExit.prop.homeScale.x<game.orcusExit.RequiredScale,"Exit must begin smaller than a player");
                Require(game.orcusExit.customOpening&&!game.orcusExit.windEnabled,"Cathedral must use its measured opening and remain silent");
                Require(Resources.Load<Mesh>("MuseumScans/Cathedral/CathedralMesh")!=null,"Actual Rodin scan missing");
                Require(game.orcusExit.cathedralActivation!=null,"The sculpture's opening activation is missing");
                var activation=game.orcusExit.cathedralActivation;
                Require(Mathf.Abs(activation.duration-2.2f)<.001f&&Mathf.Abs(activation.openedOpening.size.x-.68f)<.001f,
                    "The Cathedral must use its slower, wider hand opening");
                var rumble=game.GetComponent<GalleryCathedralRumble>();
                Require(rumble!=null&&rumble.game==game&&rumble.activation==activation,"The Cathedral camera rumble is missing");
                Require(Mathf.Abs(game.orcusExit.EntryHalfWidth*2-activation.openedOpening.size.x)<.001f,
                    "The Cathedral entry window must agree with its widened aperture");
                Require(activation.entryBarrier!=null&&!activation.entryBarrier.enabled&&
                    Array.IndexOf(game.orcusExit.prop.parts,activation.entryBarrier)<0,
                    "The opening-only entry barrier must start disabled and stay outside the exhibit placement shapes");
                Require(game.orcusExit.GetComponent<GalleryCathedralPortal>()!=null,"View-dependent library window missing");
                Require(game.GetComponentInChildren<GalleryConceptLighting>().roomProbes.Length==4,"Room reflection probes missing");
            }
            else
            {
                Require(game.GetComponentInChildren<GalleryOrcusExit>()!=null,"Museum is missing the enlarged-mouth exit");
                Require(game.mirror!=null&&game.lightGate!=null,"Existing reflected-light puzzle was lost");
                Require(game.props.Length>=11,"Original interactive exhibits or exit were lost");
            }
            foreach(string key in new[]{"StarryNight","Scream","WaterLilyBridge","Ambassadors","GirlWithPearlEarring","MaeWestFace","MaeWestRoom"})
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
