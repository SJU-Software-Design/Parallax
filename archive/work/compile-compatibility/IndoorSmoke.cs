using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink
{
    public sealed class IndoorSmoke : MonoBehaviour
    {
        public IndoorGame game;
        string output; int ticks;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        [Serializable] sealed class Report { public bool passed, escaped; public string unity, gpu, failure; public string[] checks, errors; }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-shade-output");
            output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Application.persistentDataPath, "IndoorValidation");
            Directory.CreateDirectory(output); Application.logMessageReceived += Log;
            var run = Sequence().GetEnumerator();
            while (true)
            {
                object step = null; bool next; string failure = null;
                try { next = run.MoveNext(); if (next) step = run.Current; } catch (Exception e) { failure = e.ToString(); next = false; }
                if (!next)
                {
                    File.WriteAllText(Path.Combine(output, "indoor-smoke-results.json"), JsonUtility.ToJson(new Report {
                        passed = failure == null && errors.Count == 0, escaped = game.won, unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                        failure = failure, checks = checks.ToArray(), errors = errors.ToArray() }, true));
                    Application.logMessageReceived -= Log; Application.Quit(failure == null && errors.Count == 0 ? 0 : 1); yield break;
                }
                yield return step;
            }
        }
        void Log(string text, string stack, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(text + "\n" + stack); }
        void Require(bool condition, string message) { if (!condition) throw new Exception("INDOOR_SMOKE: " + message); }
        void Check(string text) { checks.Add(text); Debug.Log("INDOOR_SMOKE_PASS " + text); }
        void Look(Vector3 at) { Vector3 d = at - game.player.Eye; game.yaw = Mathf.Atan2(d.x, d.z); game.pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude); }
        IEnumerable<object> Walk(Vector2 target)
        {
            int frames = 0;
            while ((game.player.Point - target).magnitude > .008f && !game.won)
            {
                Vector2 d = target - game.player.Point;
                game.Tick(1 / 120f, d.normalized * Mathf.Min(IndoorRules.Speed, d.magnitude * 120));
                Require(++frames < 5000, "Walk blocked at " + game.player.Point + " toward " + target);
                Require(game.player.feet.y == 0, "Raised floor encountered.");
                if (++ticks % 16 == 0) yield return null;
            }
        }
        IEnumerable<object> RouteTo(float z)
        {
            var route = IndoorRules.Route(game.blocks, game.shadows, game.player.Point, z);
            Require(route.Count > 0, "No safe path to " + z); int before = game.respawns;
            foreach (var point in route) foreach (var step in Walk(point)) yield return step;
            Require(game.respawns == before, "Safe route caused a respawn.");
        }
        IEnumerable<object> Advance(int frames, Vector2 velocity)
        { for (int i = 0; i < frames; i++) { game.Tick(1 / 120f, velocity); if (++ticks % 16 == 0) yield return null; } }
        IEnumerable<object> Shot(string name)
        {
            game.visuals.Sync(); for (int i = 0; i < 4; i++) yield return null;
            var camera = game.visuals.view; var old = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24); camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            camera.targetTexture = old; RenderTexture.active = active; target.Release(); Destroy(target);
            var pixels = texture.GetPixels32(); int low = 765, high = 0;
            for (int i = 0; i < pixels.Length; i += 137) { int light = pixels[i].r + pixels[i].g + pixels[i].b; low = Math.Min(low, light); high = Math.Max(high, light); }
            Require(high - low > 120, "Blank rendered scene.");
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
        void Grab(int index)
        {
            Look(game.blocks[index].Bounds.center); game.visuals.Sync();
            Vector3 screen = game.visuals.view.WorldToScreenPoint(game.blocks[index].Bounds.center);
            Require(screen.z > 0 && Mathf.Abs(screen.x - Screen.width / 2f) < 1 && Mathf.Abs(screen.y - Screen.height / 2f) < 1, "Camera ray/aim mismatch.");
            game.Interact(); Require(game.held != null && game.held.index == index, "Could not grab object " + index);
        }
        void PlaceAt(Vector2 target)
        {
            var g = game.held; Vector2 d = target - game.player.Point; float scale = g.scale * d.magnitude / g.distance;
            game.yaw = Mathf.Atan2(d.x, d.y) - g.yawOffset;
            game.pitch = Mathf.Atan2(Rules.FloorGap + Rules.BaseSizes[g.kind].y * scale / 2 - game.player.Eye.y, d.magnitude) - g.pitchOffset;
            game.Tick(1 / 120f, Vector2.zero);
            Require(!game.invalid && Mathf.Abs(game.blocks[g.index].scale - scale) < .002f, "Perspective placement invalid at " + target);
        }
        IEnumerable<object> Sequence()
        {
            foreach (var step in Shot("01-entry")) yield return step;
            game.Resume(); Require(!game.firstLinked && !game.secondLinked, "Initial routes must be unsolved.");
            foreach (var step in Walk(new Vector2(-4, 2.5f))) yield return step;
            foreach (var step in Advance(420, new Vector2(0, 3))) yield return step;
            Require(game.respawns > 0 && game.checkpoint == 0, "Wall-hugging rush bypassed the first passage.");
            Check("An unsolved passage cannot be bypassed along the dark-looking wall edge or rushed at walking speed.");
            game.ResetGame(); game.Resume(); Grab(0); Block original = game.blocks[0];
            PlaceAt(new Vector2(0, 14.7f)); Vector2 anchor = game.player.Point;
            foreach (var step in Advance(30, new Vector2(3, 3))) yield return step;
            Require((game.player.Point - anchor).sqrMagnitude < .00001f, "Movement during perspective resize.");
            Block valid = game.blocks[0]; game.yaw = 3.14159274f / 2; game.Tick(1 / 120f, Vector2.zero);
            Require(game.invalid && (game.blocks[0].Point - valid.Point).sqrMagnitude < .0001f, "Invalid preview changed the last valid placement.");
            game.CancelGrab(); Require(game.held == null && Mathf.Abs(game.blocks[0].scale - original.scale) < .00001f, "Cancel did not restore pickup state.");
            Check("Perspective resize anchors the player; invalid placement preserves the last valid state; cancel restores the original object.");
            Grab(0); PlaceAt(new Vector2(0, 14.7f)); game.Refresh(true);
            Require(game.firstLinked, "First shadow bridge missing.");
            game.Interact();
            Look(new Vector3(-2.5f, .2f, 9)); foreach (var step in Shot("02-first-shadow")) yield return step;
            foreach (var step in RouteTo(13.35f)) yield return step;
            Require(game.checkpoint == 1 && game.respawns == 0, "First shelter did not save.");
            Check("First passage solved through normal grab/look/drop and ground movement; middle shelter saved.");
            foreach (var step in RouteTo(17)) yield return step;
            foreach (var step in Walk(new Vector2(-4, 17))) yield return step;
            foreach (var step in Advance(285, new Vector2(0, 3))) yield return step;
            Require(game.respawns == 1 && game.checkpoint == 1 && game.player.feet.z >= 13, "Middle shelter respawn failed.");
            Check("Failure in the second passage returns to the middle shelter and preserves object placement.");
            game.Pause(); float time = game.elapsed; Vector2 feet = game.player.Point;
            foreach (var step in Advance(30, new Vector2(0, 3))) yield return step;
            Require(game.elapsed == time && game.player.Point == feet, "Pause did not freeze simulation."); game.Resume();
            Check("Pause freezes movement and exposure.");
            foreach (var step in RouteTo(17)) yield return step;
            foreach (var step in Walk(new Vector2(0, 17))) yield return step;
            Look(game.blocks[1].Bounds.center); foreach (var step in Shot("03-middle-shelter")) yield return step;
            Grab(1); PlaceAt(new Vector2(0, 28.5f)); game.Refresh(true);
            Require(game.secondLinked, "Second shadow bridge missing."); game.Interact();
            Look(new Vector3(-2.3f, .2f, 22)); foreach (var step in Shot("04-second-shadow")) yield return step;
            foreach (var step in RouteTo(27.35f)) yield return step;
            Require(game.checkpoint == 2, "Arrival shelter did not save.");
            foreach (var step in RouteTo(34.7f)) yield return step;
            foreach (var step in Walk(new Vector2(0, 34.8f))) yield return step;
            Require(game.won, "Ground-level exit failed.");
            Check("Second passage and floor-level exit complete without jumping, state injection or teleporting.");
            Look(new Vector3(0, 2, 35.8f)); foreach (var step in Shot("05-exit")) yield return step;
        }
    }
}
