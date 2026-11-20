using System;
using System.Linq;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Isolated native geometry and production movement observations. These do
    // not render pixels or simulate OS input; route start positions are stated.
    public static class EclipseExitRouteValidation
    {
        const float Dt=1f/60;
        public static void RunOpening(EclipseFinaleGame game,Action<bool,string,string> check)
        {
            check(Application.isBatchMode && Array.IndexOf(Environment.GetCommandLineArgs(),"-eclipse-ending-test")>=0,
                "Exit-route checks use the isolated native batch mode","No captures, OS input or success flag injection");
            var world=game.world;
            check(!world.ExitRouteCleared && world.ExitRouteClearProgress==0 &&
                world.RetractableShadeWalls.Count==2 && world.RetractableShadeRenderers.Count==2 &&
                Enumerable.Range(0,2).All(i=>world.RetractableShadeWalls[i].enabled && world.RetractableShadeRenderers[i].enabled &&
                    Mathf.Abs(world.RetractableShadeWalls[i].bounds.max.y-1.25f)<.001f &&
                    world.RetractableShadeWalls[i].gameObject.layer==8 && !world.RetractableShadeWalls[i].isTrigger) &&
                world.ExitRouteGuideRenderers.Count>8 && world.ExitRouteGuideRenderers.All(guide=>!guide.enabled),
                "The unsolved opening keeps both real low shade walls and conceals post-credit guides",
                "Low walls retain physical placement support until the protected credits finish");
            bool blockers=true;
            foreach(float x in new[]{-8f,-3.2f,3.2f,8f})
                blockers&=world.RetractableShadeWalls.Any(wall=>wall.Raycast(new Ray(new Vector3(x,.8f,4.4f),Vector3.forward),out _,2));
            check(blockers && world.AssetsRoot.GetComponentsInChildren<Collider>(true).All(collider=>!collider.enabled) &&
                game.GetComponentsInChildren<Collider>(true).Where(collider=>collider.name=="Floor seam" || collider.name=="Longitudinal seam")
                    .All(collider=>!collider.enabled),
                "Actual collision taxonomy identifies low shade walls rather than decorative floor assets",
                "Four rays hit 1.25m shade walls; selected Quaternius/ship and thin deck joints add no physics");
            check(world.ExitRouteGuideRenderers.All(guide=>guide.GetComponents<Collider>().All(collider=>!collider.enabled)),
                "Post-credit floor guides have no active collider","Original deck and structural boundary collision remain unchanged");
        }
        public static void RunCompletedRoute(EclipseFinaleGame game,Action<bool,string,string> check)
        {
            var world=game.world;
            check(game.Credits.Section==EndingCreditSection.Finished && game.Credits.TeamComplete && game.Credits.ThanksComplete &&
                game.totality && game.ExitUnlocked && world.ExitRouteCleared && world.ExitRouteClearProgress==1,
                "Real protected credits clear the route before gameplay resumes","Actual solved eclipse state; no completed-credit flags injected");
            check(Enumerable.Range(0,world.RetractableShadeWalls.Count).All(i=>!world.RetractableShadeWalls[i].enabled &&
                    !world.RetractableShadeRenderers[i].enabled && world.RetractableShadeWalls[i].transform.localPosition.y+.625f<-.15f) &&
                world.ShelterRoofColliders.Where(roof=>!world.RetractableShadeWalls.Contains(roof)).All(roof=>roof.enabled),
                "Both shade walls and colliders are stowed below deck together while the high roof stays solid",
                "No invisible post-credit barrier; other shelter geometry retains collision");
            check(world.ExitRouteGuideRenderers.All(guide=>guide.enabled &&
                    guide.sharedMaterial.shader.name=="ShadeLink/Flat" && guide.sharedMaterial.color.g>=.18f &&
                    guide.GetComponents<Collider>().All(collider=>!collider.enabled)) && world.PresentationDarkness>.98f,
                "Dim unlit flush guides stay readable in totality without introducing floor collision",
                "Material/geometry observation; perceived pixel brightness is not claimed");
            Vector3 sourceFeet=game.body.transform.position,sourceEye=game.view.transform.position;
            Quaternion sourceRotation=game.view.transform.rotation;
            float sourceFov=game.view.fieldOfView;
            int originalRespawns=game.respawns;
            try
            {
                foreach(float x in new[]{-8f,-3.2f,0,3.2f,8f})
                {
                    // Only the start location is declared. All traversal uses
                    // the actual common motor through public Eclipse input/tick.
                    game.Teleport(new Vector3(x,.06f,4.15f));
                    game.AimAt(new Vector3(x,1.61f,60));
                    float peak=game.body.transform.position.y;
                    int frames=0;
                    while(game.body.transform.position.z<6.45f && frames<160)
                    {
                        game.ApplySharedControlFrame(new PlayerControls.Frame{Move=Vector2.up});
                        game.Tick(Dt,Vector2.up,false);Physics.SyncTransforms();frames++;
                        peak=Mathf.Max(peak,game.body.transform.position.y);
                    }
                    check(game.body.transform.position.z>=6.45f && Mathf.Abs(game.body.transform.position.x-x)<.025f &&
                        peak<.12f && game.respawns==originalRespawns && game.held==FinaleItem.None && game.totality &&
                        world.ExitRouteCleared && game.InputEnabled,
                        "Production walking crosses the former low-wall footprint at x="+x,
                        "Declared start z4.15; end="+game.body.transform.position+" peakFeet="+peak+" frames="+frames+" without jump or collision ignore");
                }
            }
            finally
            {
                game.Teleport(sourceFeet);game.SetEndingRotation(sourceRotation);
                game.view.transform.position=sourceEye;game.view.fieldOfView=sourceFov;
                Physics.SyncTransforms();
            }
            check(game.body.transform.position==sourceFeet && game.view.transform.position==sourceEye &&
                Quaternion.Angle(game.view.transform.rotation,sourceRotation)<.001f && game.view.fieldOfView==sourceFov &&
                world.ExitRouteCleared && game.ExitUnlocked,
                "Route movement fixtures restore the captured player eye and FOV with EXIT still available","");
        }
    }
}
