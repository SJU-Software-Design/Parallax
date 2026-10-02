using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink
{
    // Explicit command-line QA only. The solution is executed with the same
    // look, click, fixed-size toggle and walking APIs as the input controller.
    public sealed class ShadowPuzzleSmoke : MonoBehaviour
    {
        public ShadowPuzzleGame game;
        string output;
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        int ticks;
        [Serializable] sealed class Report { public bool passed; public string unity,gpu,failure; public string[] checks,errors; }
        IEnumerator Start()
        {
            string[] args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,"-shade-output");
            output=at>=0?args[at+1]:Path.Combine(Application.persistentDataPath,"ShadowValidation");
            Directory.CreateDirectory(output); Application.logMessageReceived+=Log;
            var run=Sequence().GetEnumerator();
            while(true)
            {
                object next=null; bool more; string failure=null;
                try { more=run.MoveNext(); if(more) next=run.Current; }
                catch(Exception e) { more=false; failure=e.ToString(); }
                if(!more)
                {
                    File.WriteAllText(Path.Combine(output,"shadow-smoke-results.json"),JsonUtility.ToJson(new Report { passed=failure==null&&errors.Count==0,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,failure=failure,checks=checks.ToArray(),errors=errors.ToArray() },true));
                    Application.logMessageReceived-=Log; Application.Quit(failure==null&&errors.Count==0?0:1); yield break;
                }
                yield return next;
            }
        }
        void Log(string text,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(text+"\n"+stack); }
        void Require(bool okay,string why) { if(!okay) throw new Exception(why); }
        void Check(string text) { checks.Add(text); Debug.Log("SHADOW_SMOKE_PASS "+text); }
        void Look(Vector3 at)
        {
            Vector3 d=at-game.state.player.Eye; game.state.yaw=Mathf.Atan2(-d.x,-d.z);
            game.state.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);
        }
        IEnumerable<object> Advance(float seconds)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds*60);i++) { game.Tick(1/60f,Vector2.zero,false); if(++ticks%8==0) yield return null; }
        }
        IEnumerable<object> Walk(Vector2 at)
        {
            int count=0;
            while(!game.state.won && (game.state.player.Point-at).magnitude>.015f)
            {
                Vector2 d=at-game.state.player.Point; game.Tick(1/60f,d.normalized*Mathf.Min(3,d.magnitude*60),false);
                Require(++count<2000,"Walk stuck at "+game.state.player.Point+" target "+at);
                if(++ticks%8==0) yield return null;
            }
        }
        IEnumerable<object> Shot(string name)
        {
            game.visuals.Sync(game); for(int i=0;i<4;i++) yield return null;
            // RenderTexture capture works even when a background Windows player
            // has no visible swapchain; includes the actual 3D scene, not IMGUI.
            var camera=game.visuals.view; var previous=camera.targetTexture; var active=RenderTexture.active;
            var rt=new RenderTexture(1600,900,24); camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
            RenderTexture.active=active; camera.targetTexture=previous;
            var pixels=image.GetPixels32(); int min=765,max=0;
            for(int i=0;i<pixels.Length;i+=173) { int b=pixels[i].r+pixels[i].g+pixels[i].b; min=Math.Min(min,b); max=Math.Max(max,b); }
            Require(max-min>100,"Blank camera capture: "+name);
            File.WriteAllBytes(Path.Combine(output,name+"-scene.png"),image.EncodeToPNG()); Destroy(image); rt.Release(); Destroy(rt);
            yield return null;
        }
        void Grab(int index)
        {
            var p=game.state.pieces[index]; var shape=ShadowPuzzleRules.Shapes[p.shape]; Vector3 centroid=Vector3.zero;
            foreach(Vector3 v in shape.vertices) centroid+=v; centroid/=shape.vertices.Length;
            Look(new Vector3(p.position.x,0,p.position.y)+centroid*p.scale);
            Require(ShadowPuzzleRules.Aim(game.state,out _)==index,"Cannot aim piece "+index);
            game.Interact(); Require(game.state.held!=null&&game.state.held.index==index,"Grab failed "+index);
        }
        void Place(Vector2 at)
        {
            var s=game.state; var g=s.held; Vector2 d=at-s.player.Point;
            s.yaw=Mathf.Atan2(-d.x,-d.y)-g.yawOffset;
            s.pitch=Mathf.Atan2(ShadowPuzzleRules.Shapes[g.kind].bounds.center.y*g.scale-s.player.Eye.y,d.magnitude)-g.pitchOffset;
            game.Tick(1/60f,Vector2.zero,false); Require(!game.blocked,"Placement blocked "+g.index+" at "+at);
            Require((s.pieces[g.index].position-at).magnitude<.01f,"Placement did not reach "+at);
        }
        IEnumerable<object> Sequence()
        {
            game.font.RequestCharactersInTexture("강아지그림자완성액자도형잠김",32,FontStyle.Normal);
            foreach(char c in "강아지그림자완성액자도형잠김") Require(game.font.HasCharacter(c),"Missing Korean glyph "+c);
            foreach(object step in Shot("01-menu")) yield return step;
            game.Resume(); Look(new Vector3(0,.028f,0)); game.Interact();
            Require(game.state.stage==ShadowStage.Arranging&&game.state.frozen==null&&game.state.held==null,"Incomplete shadow picked up");
            Check("Incomplete floor shadow cannot be picked up; Korean font loaded");
            foreach(object step in Shot("02-scattered-shapes")) yield return step;
            foreach(object step in Walk(new Vector2(6.3f,6.5f))) yield return step;
            foreach(object step in Walk(new Vector2(6.3f,-7.8f))) yield return step;
            foreach(object step in Walk(new Vector2(5,-7.8f))) yield return step;
            Require(!game.state.won&&!game.state.DoorUnlocked,"Locked door bypass"); Check("Walking to the locked exit cannot complete the level");
            foreach(object step in Walk(new Vector2(6.3f,-7.8f))) yield return step;
            foreach(object step in Walk(new Vector2(6.3f,6.5f))) yield return step;
            foreach(object step in Walk(new Vector2(0,6.5f))) yield return step;
            game.SetFixedSize(true);
            foreach(int index in new[]{3,6,2,1,4,5,0})
            {
                Grab(index);
                if(index==3)
                {
                    var before=game.state.pieces[index]; game.Pause(); float elapsed=game.state.elapsed;
                    game.Tick(1,Vector2.right*3,true); Require(game.state.held!=null&&game.state.elapsed==elapsed,"Pause allowed interaction"); game.Resume();
                    Require((before.position-game.state.pieces[index].position).magnitude<.001f,"Pause moved piece");
                    Check("Pause freezes movement, placement, interaction and completion time");
                    Vector2 playerBefore=game.state.player.Point;
                    for(int n=0;n<6;n++) game.Tick(1/60f,Vector2.right*3,false);
                    Require((game.state.player.Point-playerBefore).magnitude>.25f&&game.state.held!=null,"Cannot walk while holding a solid");
                    Check("WASD movement remains available while a geometric solid is held");
                }
                Place(ShadowPuzzleRules.Shapes[index].solution);
                if(index==0)
                {
                    foreach(object step in Advance(1)) yield return step;
                    Require(game.state.stage==ShadowStage.Arranging&&!game.state.PickShadow(),"Held solution was collectible");
                    foreach(object step in Shot("03-complete-but-held")) yield return step;
                }
                game.Interact();
            }
            foreach(object step in Advance(1)) yield return step;
            Require(game.state.stage==ShadowStage.Ready,"Complete silhouette did not become collectible, IoU="+game.state.match.iou+" least="+game.state.match.leastPart);
            Check("Seven actual grab/look/drop placements form a dog; release and stable match enable pickup");
            Look(new Vector3(0,.028f,0));
            foreach(object step in Shot("04-collectible-shadow")) yield return step;
            game.Interact(); Require(game.state.stage==ShadowStage.Carrying,"Completed shadow pickup failed");
            Check("Clicking the completed floor silhouette picks up a single dog shadow");
            foreach(object step in Walk(new Vector2(-5.5f,6.5f))) yield return step;
            foreach(object step in Walk(new Vector2(-5.5f,1.7f))) yield return step;
            Look(new Vector3(-4,.028f,0)); game.Interact(); Require(game.state.stage==ShadowStage.Ready,"Drop failed");
            Look(new Vector3(-4,.028f,0)); game.Interact(); Require(game.state.stage==ShadowStage.Carrying,"Dropped shadow re-pickup failed");
            Check("Earned shadow can be carried, dropped on the floor and picked up again");
            foreach(object step in Shot("05-carrying-shadow")) yield return step;
            foreach(object step in Walk(new Vector2(-5.5f,-5.7f))) yield return step;
            foreach(object step in Walk(new Vector2(0,-5.7f))) yield return step;
            Look(ShadowPuzzleRules.Frame.center); game.Interact(); Require(game.state.DoorUnlocked,"Frame insertion failed");
            Check("Nearby frame accepts the carried shadow, restores the portrait and unlocks the door");
            foreach(object step in Shot("06-restored-portrait")) yield return step;
            foreach(object step in Walk(new Vector2(5,-5.7f))) yield return step;
            Look(new Vector3(5,1.6f,-8.3f));
            foreach(object step in Walk(new Vector2(5,-7.68f))) yield return step;
            Require(game.state.won,"Unlocked exit did not complete"); Check("Walking through the unlocked exit completes the level");
            Look(new Vector3(0,.5f,0));
            foreach(object step in Shot("07-complete")) yield return step;
            game.ResetPuzzle();
            Require(!game.state.won&&!game.state.DoorUnlocked&&game.state.frozen==null&&game.state.held==null,"Reset leaked state");
            game.Resume(); game.Tick(.1f,Vector2.zero,false); Require(game.state.stage==ShadowStage.Arranging,"Reset stage");
            Check("Reset restores all seven shapes, clears collectible and relocks the door");
            Require(errors.Count==0,"Runtime errors"); Check("Native Unity scene captures are nonblank and runtime has no logged errors");
        }
    }
}
