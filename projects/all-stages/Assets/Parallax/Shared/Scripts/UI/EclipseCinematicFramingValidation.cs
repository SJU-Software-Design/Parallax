using System;
using System.Linq;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Visual-only maximum-physical-size fixture, separate from the real gameplay
    // pickup/release solve. It injects no success/anchor/completion flags and uses
    // the production credit clock, camera and original lunar mesh unchanged.
    public static class EclipseCinematicFramingValidation
    {
        const float Dt=1f/60;
        public static void Run(EclipseFinaleGame game,Action<bool,string,string> check)
        {
            RunCase(game,check,false);
            RunCase(game,check,true);
        }
        static void RunCase(EclipseFinaleGame game,Action<bool,string,string> check,bool blockingSourcePose)
        {
            check(Application.isBatchMode && Array.IndexOf(Environment.GetCommandLineArgs(),"-eclipse-ending-test")>=0,
                "Maximum-size cinematic fixture is explicitly isolated and visual-only","No successful gameplay solve is claimed");
            bool started=game.started,paused=game.paused,automated=game.automated;
            float originalFov=game.view.fieldOfView,originalAspect=game.view.aspect;
            Mesh mesh=game.moon.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices=mesh.vertices;
            try
            {
                game.automated=true;game.ResetGame(true);
                game.view.fieldOfView=70;game.view.aspect=16f/9;game.FrameOpeningSun();
                game.moon.localScale=Vector3.one*48;
                float outer=vertices.Max(vertex=>vertex.magnitude)*48;
                Vector3 ray=(EclipseFinaleGame.SunPosition-game.view.transform.position).normalized;
                Vector3 pose=game.view.transform.position+ray*(blockingSourcePose?45:105);
                pose.y=Mathf.Max(pose.y,outer+.05f);
                game.moon.position=pose;Physics.SyncTransforms();
                Vector3 moonBefore=game.moon.position,scaleBefore=game.moon.localScale,eyeBefore=game.view.transform.position;
                Quaternion rotationBefore=game.moon.rotation;
                Vector3 sunBefore=EclipseFinaleGame.SunPosition;
                check(!game.MoonAnchored && !game.MoonAlignmentConfirmed && !game.totality && game.held==FinaleItem.None,
                    "Scale48 stress declares only a transform fixture without injecting success flags","outer mesh radius="+outer);
                check(pose.y-outer>.01f && Vector3.Distance(pose,EclipseFinaleGame.SunPosition)>outer+EclipseFinaleGame.SunRadius &&
                    Vector3.Distance(pose,eyeBefore)>outer,
                    "The declared giant source pose begins above the floor, in front of the sun and outside the camera","pose="+pose);
                check(game.Credits.Begin(),"The actual production credits accept the separate visual stress fixture","");
                var credits=game.Credits;
                Vector3 gameplayPosition=credits.GameplayMoonPosition;
                check(credits.AdjustedForClearRoute==blockingSourcePose &&
                    credits.ReturnSkyPoseApplied==blockingSourcePose &&
                    Vector3.Distance(credits.ReturnMoonPosition,gameplayPosition)<.0001f &&
                    credits.LockedMoonPosition==moonBefore && credits.GameplayMoonScale==scaleBefore &&
                    Quaternion.Angle(credits.GameplayMoonRotation,rotationBefore)<.001f &&
                    ClearRoute(gameplayPosition,outer),
                    "Only a route-blocking source pose receives a clear gameplay return target",
                    "blockingSource="+blockingSourcePose+" original="+moonBefore+" gameplay="+gameplayPosition);
                float minimumEye=float.PositiveInfinity,minimumFloor=float.PositiveInfinity,minimumSunGap=float.PositiveInfinity;
                float minimumActualVertexY=float.PositiveInfinity,minimumLinePixels=float.PositiveInfinity;
                int frames=0,fullDiscFrames=0,blockedDiscPoints=0,readableRows=0;
                bool geometry=true,ratio=true,meshFixed=true,fovFixed=true;
                bool introReadable=false,teamReadable=false,thanksReadable=false;
                Vector3[] wallOpening=game.world.RetractableShadeWalls.Select(wall=>wall.transform.localPosition).ToArray();
                bool preReturnWallsUntouched=true,wallMotionMatches=true,routeProgressMonotonic=true;
                float previousRouteProgress=0,maximumRouteStep=0;
                int gradualRouteFrames=0;
                var texts=game.GetComponentsInChildren<TextMesh>(true).Where(text=>text.name.StartsWith("Floor credit row ")).ToArray();
                while(credits.Active && frames<16000)
                {
                    game.TickEndingSequence(Dt,false,credits.CanControlCredits);
                    Physics.SyncTransforms();frames++;
                    float routeProgress=game.world.ExitRouteClearProgress;
                    routeProgressMonotonic&=routeProgress>=previousRouteProgress;
                    maximumRouteStep=Mathf.Max(maximumRouteStep,routeProgress-previousRouteProgress);
                    previousRouteProgress=routeProgress;
                    if(credits.Section!=EndingCreditSection.Return && credits.Section!=EndingCreditSection.Finished)
                        preReturnWallsUntouched&=routeProgress==0 && game.world.ExitRouteGuideRenderers.All(guide=>!guide.enabled);
                    if(routeProgress>.02f && routeProgress<.98f)gradualRouteFrames++;
                    for(int w=0;w<wallOpening.Length;w++)
                    {
                        var wall=game.world.RetractableShadeWalls[w];
                        wallMotionMatches&=Vector3.Distance(wall.transform.localPosition,
                            wallOpening[w]-Vector3.up*(1.45f*routeProgress))<.0001f &&
                            wall.enabled==game.world.RetractableShadeRenderers[w].enabled &&
                            (routeProgress<1 || !wall.enabled);
                    }
                    if(!credits.Active)break;
                    Vector3 eye=game.view.transform.position,moon=game.moon.position,sun=EclipseFinaleGame.SunPosition;
                    float distance=Vector3.Distance(eye,moon),floor=moon.y-outer,gap=Vector3.Distance(sun,moon)-outer-EclipseFinaleGame.SunRadius;
                    minimumEye=Mathf.Min(minimumEye,distance-outer);minimumFloor=Mathf.Min(minimumFloor,floor);minimumSunGap=Mathf.Min(minimumSunGap,gap);
                    geometry&=distance>outer+.10f && floor>=.018f && gap>=.98f;
                    meshFixed&=game.moon.GetComponent<MeshFilter>().sharedMesh==mesh && game.moon.localScale==scaleBefore && Quaternion.Angle(game.moon.rotation,rotationBefore)<.001f;
                    fovFixed&=Mathf.Abs(game.view.fieldOfView-70)<.001f;
                    if(frames%12==0)
                    {
                        Matrix4x4 matrix=game.moon.localToWorldMatrix;
                        foreach(Vector3 vertex in vertices)minimumActualVertexY=Mathf.Min(minimumActualVertexY,matrix.MultiplyPoint3x4(vertex).y);
                    }
                    float sunAngle=Mathf.Asin(EclipseFinaleGame.SunRadius/Vector3.Distance(eye,sun));
                    float measuredRatio=Mathf.Asin(game.MoonRadius*.97f/distance)/sunAngle;
                    ratio&=Mathf.Abs(measuredRatio-credits.PresentedMoonSunAngularRatio)<.0005f;
                    if(credits.Section==EndingCreditSection.Team || credits.Section==EndingCreditSection.Thanks)
                    {
                        fullDiscFrames++;
                        blockedDiscPoints+=OutsideMoonDisc(game.view,moon,outer);
                        geometry&=Mathf.Asin(outer/distance)*Mathf.Rad2Deg<=EclipseEndingCredits.MaximumCinematicMoonRadiusDegrees+.05f;
                        ratio&=Mathf.Abs(measuredRatio-credits.CinematicMoonSunAngularRatio)<.0005f;
                    }
                    foreach(var text in texts)
                    {
                        if(!text.gameObject.activeSelf || text.color.a<.70f)continue;
                        var renderer=text.GetComponent<MeshRenderer>();
                        Vector3 centre=game.view.WorldToViewportPoint(renderer.bounds.center);
                        if(centre.z<=0 || centre.x<.02f || centre.x>.98f || centre.y<.02f || centre.y>.98f)continue;
                        Bounds b=renderer.localBounds;float min=1,max=0;
                        for(int ix=-1;ix<=1;ix+=2)for(int iy=-1;iy<=1;iy+=2)
                        {
                            Vector3 corner=text.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(ix,iy,0)));
                            float y=game.view.WorldToViewportPoint(corner).y;min=Mathf.Min(min,y);max=Mathf.Max(max,y);
                        }
                        float linePixels=(max-min)*900/Mathf.Max(1,text.text.Split('\n').Length);
                        if(linePixels>=12)
                        {
                            readableRows++;minimumLinePixels=Mathf.Min(minimumLinePixels,linePixels);
                            introReadable|=credits.Section==EndingCreditSection.Intro;
                            teamReadable|=credits.Section==EndingCreditSection.Team;
                            thanksReadable|=credits.Section==EndingCreditSection.Thanks;
                        }
                    }
                }
                check(frames<16000 && credits.Section==EndingCreditSection.Finished && credits.TeamComplete && credits.ThanksComplete,
                    "Scale48 cinematic traverses the real protected team and thanks sections before finishing","frames="+frames);
                check(preReturnWallsUntouched && wallMotionMatches && routeProgressMonotonic && gradualRouteFrames>100 &&
                    maximumRouteStep<.01f && game.world.ExitRouteCleared &&
                    game.world.RetractableShadeWalls.All(wall=>!wall.enabled) &&
                    game.world.RetractableShadeRenderers.All(renderer=>!renderer.enabled) &&
                    game.world.ExitRouteGuideRenderers.All(guide=>guide.enabled),
                    "Only protected credits Return gradually stows matching visible and physical shade walls before input resumes",
                    "visual-only giant fixture; gradualFrames="+gradualRouteFrames+" maxProgressStep="+maximumRouteStep);
                check(geometry && minimumActualVertexY>=.018f,
                    "Maximum physical moon stays outside the camera, above the real mesh floor and wholly in front of the sun",
                    "camera clearance="+minimumEye+" envelope floor="+minimumFloor+" actual vertex floor="+minimumActualVertexY+" sun surface gap="+minimumSunGap);
                check(fullDiscFrames>300 && blockedDiscPoints==0 && fovFixed && ratio,
                    "The whole maximum-size lunar rim fits the actual 70-degree camera throughout Team and Thanks",
                    "frames="+fullDiscFrames+" tangent points outside="+blockedDiscPoints+" final angle="+credits.CinematicMoonAngularRadiusDegrees+" liftEye="+credits.RaisedCameraPosition);
                check(readableRows>0 && introReadable && teamReadable && thanksReadable,
                    "Adaptive giant framing retains readable real floor-credit rows","observed rows="+readableRows+" minimum passing line height at declared900p="+minimumLinePixels);
                check(meshFixed && game.moon.position==gameplayPosition && game.moon.localScale==scaleBefore &&
                    Quaternion.Angle(game.moon.rotation,rotationBefore)<.001f && game.view.transform.position==eyeBefore &&
                    Vector3.Distance(EclipseFinaleGame.SunPosition,sunBefore)<.0001f && Mathf.Abs(game.view.fieldOfView-70)<.001f &&
                    credits.LockedMoonPosition==moonBefore && (!blockingSourcePose || gameplayPosition!=moonBefore),
                    "Return preserves original mesh, scale and captured pose while clearing only a blocked gameplay route",
                    "blockingSource="+blockingSourcePose);
                bool exitCameraClear=true;
                for(int i=0;i<=64;i++)
                {
                    Vector3 exitEye=new Vector3(game.DoorCenter.x,1.61f,game.DoorCenter.z-3.95f+8.15f*i/64);
                    exitCameraClear&=Vector3.Distance(exitEye,game.moon.position)>outer+.15f;
                }
                check(ClearRoute(game.moon.position,outer) && exitCameraClear && game.CoverageAt(eyeBefore)>=.70f &&
                    !game.MoonAnchored && !game.MoonAlignmentConfirmed && !game.HasMoonReleaseSnapshot,
                    "The restored giant eclipse leaves standing, jump and final EXIT camera travel outside the moon",
                    "Protected x[-4,4],y[0,3.2],z[7,72]; 65 final-door camera points; visual-only state remains unconfirmed");
            }
            finally
            {
                game.view.fieldOfView=originalFov;game.view.aspect=originalAspect;game.ResetGame(started);
                if(paused)game.Pause();else if(started)game.Play();game.automated=automated;
                for(int i=0;i<25;i++)game.world.TickShelterVisibility(.1f);Physics.SyncTransforms();
            }
            check(game.moon.GetComponent<MeshFilter>().sharedMesh==mesh && game.moon.localScale==Vector3.one*.55f &&
                !game.MoonAnchored && !game.MoonAlignmentConfirmed && game.Credits.Section==EndingCreditSection.Inactive &&
                Mathf.Abs(game.view.fieldOfView-originalFov)<.001f && Mathf.Abs(game.view.aspect-originalAspect)<.001f &&
                !game.world.ExitRouteCleared && game.world.ExitRouteClearProgress==0 &&
                game.world.RetractableShadeWalls.All(wall=>wall.enabled && Mathf.Abs(wall.bounds.max.y-1.25f)<.001f) &&
                game.world.RetractableShadeRenderers.All(renderer=>renderer.enabled) && game.world.ExitRouteGuideRenderers.All(guide=>!guide.enabled) &&
                game.world.IsShelterShadowAt(new Vector3(-8,.03f,-1)) && game.world.IsShelterShadowAt(new Vector3(-2,.03f,4.5f)),
                "Visual-only stress cleanup restores the actual unsolved opening without stored success","");
        }
        static bool ClearRoute(Vector3 centre,float radius)
        {
            // Independent sphere/AABB calculation over the actual stage's
            // standing/jump lane plus final 4.2m camera travel reserve.
            var lane=new Bounds(new Vector3(0,1.6f,39.5f),new Vector3(8,3.2f,65));
            Vector3 closest=lane.ClosestPoint(centre);
            return Vector3.Distance(centre,closest)>radius+.14f;
        }
        static int OutsideMoonDisc(Camera camera,Vector3 centre,float radius)
        {
            Vector3 eye=camera.transform.position,delta=centre-eye,axis=delta.normalized;
            Vector3 right=Vector3.Cross(Vector3.up,axis).normalized,up=Vector3.Cross(axis,right).normalized;
            float angle=Mathf.Asin(Mathf.Clamp01(radius/delta.magnitude));int outside=0;
            for(int i=0;i<=64;i++)
            {
                float around=(i-1)*Mathf.PI/32;
                Vector3 direction=i==0?axis:axis*Mathf.Cos(angle)+(right*Mathf.Cos(around)+up*Mathf.Sin(around))*Mathf.Sin(angle);
                Vector3 p=camera.WorldToViewportPoint(eye+direction*delta.magnitude);
                if(p.z<=0 || p.x<.005f || p.x>.995f || p.y<.005f || p.y>.995f)outside++;
            }
            return outside;
        }
    }
}
