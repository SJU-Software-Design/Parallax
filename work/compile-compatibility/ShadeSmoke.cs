using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink
{
    // Opt-in executable QA. Drives the same look/interact/movement commands as the
    // input controller, with no teleporting or object-state assignment in the run.
    public sealed class ShadeSmoke : MonoBehaviour
    {
        public ShadeGame game;
        string output;
        readonly List<string> checks = new List<string>();
        readonly List<string> errors = new List<string>();
        int ticks;
        [Serializable] sealed class Report
        { public bool passed; public string unity; public string gpu; public string[] checks; public string[] errors; public string failure; public Vector3 feet; public Block[] blocks; public bool won; }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            output = Argument("-shade-output") ?? Path.Combine(Application.persistentDataPath, "Validation");
            Directory.CreateDirectory(output); Application.logMessageReceived += Log;
            var routine = Sequence().GetEnumerator();
            while (true)
            {
                bool next; object current = null; string failure = null;
                try { next = routine.MoveNext(); if (next) current = routine.Current; }
                catch (Exception e) { failure = e.ToString(); next = false; }
                if (!next)
                {
                    Write(failure); Application.logMessageReceived -= Log;
                    Application.Quit(failure == null && errors.Count == 0 ? 0 : 1); yield break;
                }
                yield return current;
            }
        }
        static string Argument(string key)
        { string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        void Log(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
        void Require(bool test, string message) { if (!test) throw new Exception(message); }
        void Check(string text) { checks.Add(text); Debug.Log("SHADE_SMOKE_PASS " + text); }
        void Write(string failure)
        {
            File.WriteAllText(Path.Combine(output, "native-smoke-results.json"), JsonUtility.ToJson(new Report
            { passed = failure == null && errors.Count == 0, unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                checks = checks.ToArray(), errors = errors.ToArray(), failure = failure, feet = game.state.player.feet,
                blocks = game.state.blocks, won = game.state.won }, true));
        }
        IEnumerable<object> Shot(string name)
        {
            game.visuals.Sync(game.state, game.shadows, game.aimed, game.blocked);
            game.puzzle?.visuals.Sync(game.puzzle);
            for (int i = 0; i < 4; i++) yield return null;
            bool offscreen = Array.IndexOf(Environment.GetCommandLineArgs(), "-shade-offscreen") >= 0;
            Texture2D texture;
            if (offscreen)
            {
                var camera = game.visuals.view; var previous = camera.targetTexture; var active = RenderTexture.active;
                var target = new RenderTexture(1600, 900, 24); camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                camera.targetTexture = previous; RenderTexture.active = active; target.Release(); Destroy(target);
            }
            else
            {
                yield return new WaitForEndOfFrame();
                texture = ScreenCapture.CaptureScreenshotAsTexture();
            }
            Color32[] pixels = texture.GetPixels32(); int lightest = 0, darkest = 765;
            for (int i = 0; i < pixels.Length; i += 137)
            { int brightness = pixels[i].r + pixels[i].g + pixels[i].b; lightest = Math.Max(lightest, brightness); darkest = Math.Min(darkest, brightness); }
            Require(lightest - darkest > 100, "Screenshot is blank; run visual QA with a visible game window: " + name);
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
            yield return null;
        }
        IEnumerable<object> Advance(int frames, Vector2 velocity, bool jump = false)
        {
            for (int i = 0; i < frames; i++)
            {
                game.Tick(1 / 120f, velocity, jump && i == 0, false);
                if (++ticks % 8 == 0) yield return null;
            }
        }
        IEnumerable<object> Walk(Vector2 target)
        {
            int count = 0;
            while ((game.state.player.Point - target).magnitude > .014f)
            {
                Vector2 delta = target - game.state.player.Point;
                game.Tick(1 / 120f, delta.normalized * Mathf.Min(Rules.Speed, delta.magnitude * 120), false, false);
                Require(++count < 5000, "Walk stuck near " + game.state.player.feet + " target " + target);
                if (++ticks % 8 == 0) yield return null;
            }
        }
        void Aim(int index)
        {
            Vector3 center = game.state.blocks[index].Bounds.center, delta = center - game.state.player.Eye;
            game.state.yaw = Mathf.Atan2(-delta.x, -delta.z);
            game.state.pitch = Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude);
            Require(Rules.Aim(game.state.player, game.state.yaw, game.state.pitch, game.state.blocks) == index, "Object not visible " + index);
            game.visuals.Sync(game.state, game.shadows, index, false);
            Vector3 screen = game.visuals.view.WorldToScreenPoint(ShadeVisuals.ToUnity(center));
            Require(screen.z > 0 && Mathf.Abs(screen.x - Screen.width / 2f) < 1 && Mathf.Abs(screen.y - Screen.height / 2f) < 1, "Visual aim must agree with interaction ray");
        }
        void Grab(int index)
        { Aim(index); game.Interact(); Require(game.state.held != null && game.state.held.index == index, "Grab failed " + index); }
        void PlaceDirection(float size, float direction)
        {
            var g = game.state.held; float distance = size * g.distance / g.scale;
            float centerY = Rules.FloorGap + Rules.BaseSizes[g.kind].y * size / 2;
            game.state.yaw = direction - g.yawOffset;
            game.state.pitch = Mathf.Atan2(centerY - g.eyeHeight, distance) - g.pitchOffset;
            game.Tick(1 / 120f, Vector2.zero, false, false);
            Require(!game.blocked, "Placement blocked " + size + " " + direction);
            game.Recalculate(true);
        }
        void PlaceAt(Vector2 position)
        {
            Vector2 delta = position - game.state.player.Point; var g = game.state.held;
            PlaceDirection(g.scale * delta.magnitude / g.distance, Mathf.Atan2(-delta.x, -delta.y));
            game.Interact();
        }
        IEnumerable<object> Jump(float targetZ, int support)
        {
            for (int frame = 0; frame < 100; frame++)
            {
                game.Tick(1 / 120f, new Vector2(0, game.state.player.feet.z > targetZ + .015f ? -3 : 0), frame == 0, false);
                if (++ticks % 4 == 0) yield return null;
            }
            Require(game.state.player.grounded && game.state.player.support == support, "Jump landing " + support + " at " + game.state.player.feet);
        }
        void LookAt(Vector3 target)
        {
            Vector3 d=target-game.state.player.Eye;game.state.yaw=Mathf.Atan2(-d.x,-d.z);
            game.state.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);
        }
        void PickPicture()
        {
            var p=game.puzzle;Vector2 target=p.tokenAnchor+new Vector2(-.45f,-.25f)*CourtyardShadowPuzzle.Unit*p.state.tokenScale;
            LookAt(new Vector3(target.x,.029f,target.y));game.Interact();Require(p.Carrying,"Integrated dog pickup failed at "+target);
        }
        void DropPicture(Vector2 target)
        {
            LookAt(new Vector3(target.x,.029f,target.y));game.Interact();
            Require(game.puzzle.state.stage==ShadowStage.Ready,"Integrated dog drop failed at "+target);
        }
        IEnumerable<object> SolvePicture()
        {
            var p=game.puzzle;Vector2 target=CourtyardShadowPuzzle.World(new Vector2(-.45f,-.25f));
            LookAt(new Vector3(target.x,.029f,target.y));game.Interact();
            Require(p.state.stage==ShadowStage.Arranging&&p.state.frozen==null&&!p.HoldingPiece,"Incomplete picture pickup");
            Check("Integrated courtyard starts with a locked picture and ungrabbable incomplete shadow");
            p.SetFixedSize(true);
            foreach(int index in new[]{3,2,6,1,4,5,0})
            {
                var piece=p.worldPieces[index];var shape=ShadowPuzzleRules.Shapes[index];Vector3 center=Vector3.zero;
                foreach(Vector3 v in shape.vertices)center+=v;center/=shape.vertices.Length;
                LookAt(new Vector3(piece.position.x,0,piece.position.y)+center*piece.scale);
                Require(p.AimPiece(out _)==index,"Cannot aim courtyard plate "+index);
                game.Interact();Require(p.HoldingPiece&&p.state.held.index==index,"Courtyard plate pickup "+index);
                var grab=p.state.held;Vector2 d=ShadowPuzzleRules.Shapes[index].solution-p.state.player.Point;
                game.state.yaw=Mathf.Atan2(-d.x,-d.y)-grab.yawOffset;
                game.state.pitch=Mathf.Atan2(shape.bounds.center.y-p.state.player.Eye.y,d.magnitude)-grab.pitchOffset;
                game.Tick(1/120f,Vector2.zero,false,false);
                Require(!p.blocked,"Courtyard plate placement blocked "+index);
                if(index==0)
                {
                    foreach(object step in Advance(100,Vector2.zero))yield return step;
                    Require(p.state.stage==ShadowStage.Arranging,"Picture unlocked while plate held");
                }
                game.Interact();
            }
            foreach(object step in Advance(100,Vector2.zero))yield return step;
            Require(p.state.stage==ShadowStage.Ready,"Courtyard picture completion, IoU="+p.state.match.iou+" parts="+p.state.match.leastPart);
            Check("Seven actual plate placements complete the dog in the original starting shade");
            LookAt(new Vector3(target.x,.029f,target.y));
            foreach(object step in Shot("02b-dog-in-starting-shade"))yield return step;
        }
        IEnumerable<object> Sequence()
        {
            TextMesh sample = game.visuals.GetComponentInChildren<TextMesh>();
            Require(sample != null && sample.font != null, "OS Korean font was not loaded");
            const string korean = "그늘잇기이동점프출구초기화";
            sample.font.RequestCharactersInTexture(korean, 32, FontStyle.Normal);
            foreach (char glyph in korean) Require(sample.font.HasCharacter(glyph), "Missing Korean glyph: " + glyph);
            foreach (object step in Shot("01-title")) yield return step;
            game.Resume();
            Vector3 right = ShadeVisuals.ToUnity(new Vector3(Mathf.Cos(game.state.yaw), 0, -Mathf.Sin(game.state.yaw)));
            Require(Vector3.Dot(game.visuals.view.transform.right, right) > .99f, "D movement must match camera right");
            Check("Unity camera handedness matches mouse look, WASD directions and world coordinates");
            foreach (object step in Shot("02-start")) yield return step;
            if(game.puzzle!=null) foreach(object step in SolvePicture())yield return step;
            Grab(0); Block original = game.state.blocks[0];
            foreach (object step in Advance(35, new Vector2(0, -3), true)) yield return step;
            Require((game.state.player.Point - Rules.Start).magnitude < .001f, "Moving while held");
            Require(Mathf.Abs(game.state.blocks[0].scale - original.scale) < .001f, "Pickup changed size");
            PlaceDirection(.55f, 0); Require(game.state.blocks[0].scale < .6f, "Near shrink");
            PlaceDirection(3, 0); Require(game.firstLinked && !game.connected, "First leg");
            foreach (object step in Shot("03-perspective-shadow")) yield return step;
            game.Interact(); Require(Mathf.Abs(game.state.blocks[0].scale - 3) < .001f, "Persistent size");
            Check("Grab, near/far look, stable angular scale, live shadow preview, drop persistence and held movement lock");
            if(game.puzzle!=null)
            {
                PickPicture();
                foreach(object step in Walk(new Vector2(-1.35f,14)))yield return step;
                foreach(object step in Walk(new Vector2(-1.35f,10.8f)))yield return step;
                foreach(object step in Advance(30,Vector2.zero))yield return step;
                Require(game.state.exposure>.16f&&game.puzzle.Carrying,"Carried dog incorrectly grants sunlight immunity");
                foreach(object step in Shot("03b-carrying-in-sun"))yield return step;
                foreach(object step in Advance(270,Vector2.zero))yield return step;
                Require(game.state.respawns==1&&game.puzzle.Carrying&&(game.state.player.Point-Rules.Start).magnitude<.001f,"Exposure did not respawn with earned dog");
                Check("Carrying a dog does not grant immunity: sunlight exposure respawns player and preserves earned shadow");
            }
            game.state.yaw = 0; game.state.pitch = -.17f;
            foreach (object step in Walk(new Vector2(-3.6f, 1.3f))) yield return step;
            foreach (object step in Walk(new Vector2(-1.55f, 1.3f))) yield return step;
            foreach (object step in Walk(new Vector2(-1.55f, -1))) yield return step;
            foreach (object step in Walk(new Vector2(3.5f, -1))) yield return step;
            foreach (object step in Walk(Rules.Staging)) yield return step;
            Require(game.state.exposure == 0, "First path exposure"); Check("First shadow crossed and divider detoured without exposure");
            if(game.puzzle!=null) DropPicture(new Vector2(3.5f,-1));
            Grab(1); PlaceDirection(2.9f, 0);
            Require(game.connected, "Two-shadow route");
            foreach (object step in Shot("04-second-shadow")) yield return step;
            game.Interact(); game.state.pitch = -.2f;
            if(game.puzzle!=null)
            { PickPicture();Check("Dog can be set down in the middle shelter while the original shadow bridge is resized"); }
            foreach (object step in Walk(new Vector2(3.5f, -11.6f))) yield return step;
            foreach (object step in Walk(new Vector2(6.6f, -11.6f))) yield return step;
            foreach (object step in Walk(new Vector2(6.6f, -14.3f))) yield return step;
            Require(game.state.shadeSolved && !game.state.won, "Checkpoint progression");
            Check("Second shadow and narrow passage reach saved checkpoint without completing raised exit");
            foreach (object step in Shot("05-checkpoint")) yield return step;
            if(game.puzzle!=null) DropPicture(new Vector2(6.48f,-15));
            Grab(1); PlaceDirection(.8f, 3.14159274f * .75f); game.Interact();
            Require(game.state.shadeSolved && !game.connected, "Save persists after disconnection");
            if(game.puzzle!=null) PickPicture();
            foreach (object step in Walk(new Vector2(1.51f, -14.3f))) yield return step;
            if(game.puzzle!=null)
            {
                LookAt(CourtyardShadowPuzzle.Frame.center);Require(game.puzzle.AimFrame(),"Exit frame inaccessible");game.Interact();
                Require(game.state.pictureRestored&&game.puzzle.Restored,"Integrated exit not unlocked by picture");
                Check("Dog carried across both original shadow bridges restores the exit picture and releases its lock");
                foreach(object step in Shot("05b-picture-restored-in-courtyard"))yield return step;
            }
            foreach (object step in Walk(new Vector2(1.51f, -11.83f))) yield return step;
            Grab(0); PlaceAt(new Vector2(4.7f, -14.05f));
            Grab(1); PlaceAt(new Vector2(4.7f, -12.85f));
            float low = game.state.blocks[1].Bounds.max.y, high = game.state.blocks[0].Bounds.max.y;
            Require(low < 1.04f && low > .72f && high - low < 1.08f && 2.7f - high < 1.08f, "Perspective stairs " + low + ", " + high);
            game.state.yaw = -.58f; game.state.pitch = -.18f;
            foreach (object step in Shot("06-reused-steps")) yield return step;
            Check("Reused objects form reachable stairs using only actual grab and look placements");
            foreach (object step in Walk(new Vector2(4.7f, -11.83f))) yield return step;
            game.state.yaw = 0; game.state.pitch = -.27f;
            foreach (object step in Jump(-12.85f, 1)) yield return step;
            Aim(1); game.Interact(); Require(game.state.held == null, "Own-support grab");
            game.state.yaw = 0; game.state.pitch = -.27f;
            foreach (object step in Jump(-14.05f, 0)) yield return step;
            foreach (object step in Shot("07-second-landing")) yield return step;
            foreach (object step in Jump(-15.05f, 3)) yield return step;
            Require(game.state.won && game.state.exposure == 0 && game.state.respawns == (game.puzzle==null?0:1), "Full finish");
            Check(game.puzzle==null?"Three jumps complete the 2.7m exit with zero exposure/respawns; own-support grab blocked":"Original three-jump exit completes only after shadow travel and picture restoration");
            foreach (object step in Shot("08-complete")) yield return step;
            game.ResetGame(); game.Resume();
            Require(!game.state.shadeSolved && !game.state.won && game.state.blocks[0].scale == 1 && !game.connected, "Reset state");
            Check("Reset clears placements, progress, completion and exposure");
            if(game.puzzle!=null)Require(game.state.pictureRequired&&!game.state.pictureRestored&&game.puzzle.state.stage==ShadowStage.Arranging&&game.puzzle.state.frozen==null,"Integrated reset did not relock picture");
            float exposureX=game.puzzle==null?-6.1f:-1.35f;
            foreach (object step in Walk(new Vector2(exposureX, 14))) yield return step;
            foreach (object step in Walk(new Vector2(exposureX, 10.8f))) yield return step;
            foreach (object step in Advance(30, Vector2.zero)) yield return step;
            Require(game.state.exposure > .16f, "Sun exposure");
            foreach (object step in Shot("09-sun-exposure")) yield return step;
            foreach (object step in Walk(new Vector2(exposureX, 11.8f))) yield return step;
            foreach (object step in Advance(150, Vector2.zero)) yield return step;
            Require(game.state.exposure == 0, "Shade recovery");
            foreach (object step in Walk(new Vector2(exposureX, 10.8f))) yield return step;
            foreach (object step in Advance(270, Vector2.zero)) yield return step;
            Require(game.state.respawns == 1 && (game.state.player.Point - Rules.Start).magnitude < .001f, "Sun respawn");
            Check("Sun exposure, shade recovery and full-gauge respawn preserve objects");
            game.Pause(); Vector3 before = game.state.player.feet; float elapsed = game.state.elapsed;
            foreach (object step in Advance(120, Vector2.right * 3, true)) yield return step;
            Require(game.state.player.feet == before && elapsed == game.state.elapsed, "Pause freezes game");
            Require(Cursor.lockState == CursorLockMode.None, "Pause unlocks cursor"); Check("Pause freezes timer/movement and unlocks pointer");
            foreach (object step in Shot("10-pause")) yield return step;
            Require(errors.Count == 0, "Runtime errors present"); Check("Native Unity rendering captured nonblank frames; Korean UI and game completed with no logged errors");
        }
    }
}
