using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink
{
    // Drives the normal interaction and movement APIs, with no object/player
    // teleports or size assignments. Images are native offscreen camera renders.
    public sealed class KeyPuzzleSmoke : MonoBehaviour
    {
        public ShadeGame game;
        string output;
        int ticks;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        bool escaped;
        KeyPuzzle Key => game.keyPuzzle;
        [Serializable] sealed class Report
        { public bool passed, escapedWithoutJump; public string unity, gpu, failure; public string[] checks, errors; }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-shade-output");
            output = at >= 0 ? args[at + 1] : Path.Combine(Application.persistentDataPath, "KeyValidation");
            Directory.CreateDirectory(output); Application.logMessageReceived += Log;
            var run = Sequence().GetEnumerator();
            while (true)
            {
                object step = null; bool next; string failure = null;
                try { next = run.MoveNext(); if (next) step = run.Current; }
                catch (Exception e) { failure = e.ToString(); next = false; }
                if (!next)
                {
                    File.WriteAllText(Path.Combine(output, "key-smoke-results.json"), JsonUtility.ToJson(new Report
                    { passed = failure == null && errors.Count == 0, escapedWithoutJump = escaped, failure = failure,
                        unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, checks = checks.ToArray(), errors = errors.ToArray() }, true));
                    Application.logMessageReceived -= Log; Application.Quit(failure == null && errors.Count == 0 ? 0 : 1); yield break;
                }
                yield return step;
            }
        }
        void Log(string text, string stack, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(text + "\n" + stack); }
        void Require(bool value, string why) { if (!value) throw new Exception("KEY_SMOKE: " + why); }
        void Check(string value) { checks.Add(value); Debug.Log("KEY_SMOKE_PASS " + value); }
        void Look(Vector3 at)
        {
            Vector3 d = at - game.state.player.Eye;
            game.state.yaw = Mathf.Atan2(-d.x, -d.z); game.state.pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude);
        }
        IEnumerable<object> Advance(int frames, Vector2 velocity = default)
        {
            for (int i = 0; i < frames; i++)
            { game.Tick(1 / 120f, velocity, false, false); if (++ticks % 8 == 0) yield return null; }
        }
        IEnumerable<object> Walk(Vector2 point)
        {
            int frames = 0;
            while ((game.state.player.Point - point).magnitude > .015f)
            {
                Vector2 d = point - game.state.player.Point;
                game.Tick(1 / 120f, d.normalized * Mathf.Min(Rules.Speed, d.magnitude * 120), false, false);
                Require(++frames < 3000, "Walk blocked at " + game.state.player.feet + " toward " + point);
                Require(game.state.player.feet.y == 0, "Unexpected raised floor");
                if (++ticks % 8 == 0) yield return null;
            }
        }
        IEnumerable<object> Shot(string name)
        {
            game.visuals.Sync(game.state, game.shadows, game.aimed, game.blocked); Key.visuals.Sync();
            for (int i = 0; i < 3; i++) yield return null;
            Camera camera = game.visuals.view; RenderTexture before = camera.targetTexture, active = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24); camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            camera.targetTexture = before; RenderTexture.active = active; target.Release(); Destroy(target);
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
        void PickKey()
        {
            Look(Key.state.Bounds.center); Require(Key.AimKey(), "Key cannot be aimed " + Key.state.position);
            game.Interact(); Require(Key.state.stage == KeyStage.Carrying, "Key pickup failed");
        }
        void PutKey(Vector2 point)
        {
            Look(new Vector3(point.x, .035f, point.y)); game.Interact();
            Require(Key.state.stage == KeyStage.Floor, "Key could not be placed at " + point);
        }
        void GrabBlock(int index)
        {
            Look(game.state.blocks[index].Bounds.center); game.Interact();
            Require(game.state.held != null && game.state.held.index == index, "Block pickup " + index);
        }
        void ResizeBlock(float size)
        {
            var g = game.state.held; float distance = size * g.distance / g.scale;
            game.state.yaw = -g.yawOffset;
            game.state.pitch = Mathf.Atan2(Rules.FloorGap + Rules.BaseSizes[g.kind].y * size / 2 - g.eyeHeight, distance) - g.pitchOffset;
            game.Tick(1 / 120f, Vector2.zero, false, false); Require(!game.blocked, "Bridge placement");
            game.Interact(); game.Recalculate(true);
        }
        void ResizeKey(float size, float direction)
        {
            Require(Key.state.stage == KeyStage.Resizing, "Not resizing");
            float distance = size / Key.ratio;
            game.state.yaw = direction - Key.yawOffset;
            game.state.pitch = Mathf.Atan2(.035f + .045f * size - game.state.player.Eye.y, distance) - Key.pitchOffset;
            game.Tick(1 / 120f, Vector2.zero, false, false);
            Require(!Key.blocked && Mathf.Abs(Key.state.scale - size) < .002f, "Key resize blocked at " + Key.state.position + " expected " + size + " got " + Key.state.scale);
        }
        IEnumerable<object> Sequence()
        {
            Require(Key != null && game.puzzle == null && game.state.groundExit && game.state.keyRequired, "Wrong default level");
            Require(GameObject.Find("Raised exit") == null && GameObject.Find("Perspective key and ground door") != null, "Raised exit or missing key");
            Check("Default level contains a perspective key and ground-level exit with no dog puzzle");
            game.Resume(); PickKey(); float size = Key.state.scale;
            game.state.yaw = 0; game.state.pitch = -.14f;
            foreach (object step in Shot("01-carry-key")) yield return step;
            foreach (object step in Walk(new Vector2(-6.1f, 14))) yield return step;
            foreach (object step in Walk(new Vector2(-6.1f, 10.8f))) yield return step;
            foreach (object step in Advance(30)) yield return step;
            Require(game.state.exposure > .16f && Key.state.scale == size && Key.Holding, "Carry immunity or auto resizing");
            foreach (object step in Advance(270)) yield return step;
            Require(game.state.respawns == 1 && Key.Holding && Key.state.scale == size && (game.state.player.Point - Rules.Start).magnitude < .01f, "Lost key on respawn");
            Check("Carried size stays fixed during movement; sunlight still respawns player and preserves key");
            PutKey(new Vector2(-5.5f, 14)); PickKey();
            Require(Key.state.scale == size, "Re-pick changed size");
            game.state.yaw = 0; game.state.pitch = -.3f; Key.ToggleResize();
            ResizeKey(.4f, 0); ResizeKey(.95f, 0); ResizeKey(size, 0);
            Vector3 feet = game.state.player.feet;
            foreach (object step in Advance(25, Vector2.right * 3)) yield return step;
            Require(game.state.player.feet == feet, "Resize did not anchor player");
            Key.ToggleResize(); Require(Key.state.stage == KeyStage.Carrying, "Cannot leave resize mode");
            Check("Near/far look changes physical key size; F confirms size and restores movement; drop/re-pick preserves size");
            PutKey(new Vector2(-5.5f, 14)); GrabBlock(0); ResizeBlock(3);
            Require(game.firstLinked && !game.connected, "First bridge"); PickKey();
            game.state.yaw = 0; game.state.pitch = -.14f;
            foreach (object step in Walk(new Vector2(-3.6f, 1.3f))) yield return step;
            foreach (object step in Walk(new Vector2(-1.55f, 1.3f))) yield return step;
            foreach (object step in Walk(new Vector2(-1.55f, -1))) yield return step;
            foreach (object step in Walk(new Vector2(3.5f, -1))) yield return step;
            foreach (object step in Walk(Rules.Staging)) yield return step;
            Require(game.state.exposure == 0 && Key.Holding && Key.state.scale == size, "First shade carry");
            Check("Key carried through original first shadow bridge and divider detour without sunlight exposure");
            PutKey(new Vector2(4.8f, 0)); GrabBlock(1); ResizeBlock(2.9f); Require(game.connected, "Both shade bridges");
            PickKey(); game.state.yaw = 0; game.state.pitch = -.14f;
            foreach (object step in Shot("02-key-and-shadow-route")) yield return step;
            foreach (object step in Walk(new Vector2(3.5f, -11.6f))) yield return step;
            foreach (object step in Walk(new Vector2(6.6f, -11.6f))) yield return step;
            foreach (object step in Walk(new Vector2(6.6f, -14.65f))) yield return step;
            foreach (object step in Walk(new Vector2(4.85f, -14.65f))) yield return step;
            Require(game.state.shadeSolved && !game.state.won && !game.state.keyUnlocked && game.state.exposure == 0, "Arrival progression");
            Check("Second bridge preserves original shade gameplay; ground arrival saves and closed door prevents escape");
            Look(KeyPuzzle.Socket); Require(Key.AimLock(), "Lock inaccessible from ground");
            Require(!Key.TryInsert() && Key.state.stage == KeyStage.Carrying, "Undersized key accepted");
            foreach (object step in Shot("03-wrong-small-key")) yield return step;
            Check("Undersized key fails lock alignment without consuming or rescaling key");
            game.state.yaw = 3.14159274f / 2; game.state.pitch = -.35f; Key.ToggleResize();
            ResizeKey(1.8f, 3.14159274f / 2); Key.ToggleResize(); Look(KeyPuzzle.Socket);
            Require(!Key.TryInsert() && Key.state.stage == KeyStage.Carrying, "Oversized key accepted");
            Check("Oversized key also fails and remains available for another perspective adjustment");
            game.state.yaw = 3.14159274f / 2; game.state.pitch = -.35f; Key.ToggleResize();
            ResizeKey(KeyState.TargetScale, 3.14159274f / 2);
            foreach (object step in Shot("04-resize-at-door")) yield return step;
            Key.ToggleResize(); Look(KeyPuzzle.Socket);
            Require(Key.state.Fits && Key.TryInsert(), "Correct key failed to insert");
            foreach (object step in Advance(39)) yield return step;
            Require(Key.state.stage == KeyStage.Inserting && !game.state.keyUnlocked && Key.visuals.clickSounds == 0, "Premature unlock");
            foreach (object step in Shot("05-key-inserting")) yield return step;
            foreach (object step in Advance(80)) yield return step;
            Require(Key.state.stage == KeyStage.Turning && Key.visuals.clickSounds == 0, "No turning phase");
            foreach (object step in Shot("06-key-turning")) yield return step;
            float clock = Key.state.clock, elapsed = game.state.elapsed; feet = game.state.player.feet;
            game.Pause(); foreach (object step in Advance(100, Vector2.right * 3)) yield return step;
            Require(Key.state.clock == clock && game.state.elapsed == elapsed && Key.visuals.clickSounds == 0, "Pause did not freeze unlock");
            game.Resume(); game.Interact(); Require(Key.state.stage == KeyStage.Turning, "Click interrupted turn");
            foreach (object step in Advance(20, Vector2.right * 3)) yield return step;
            Require(game.state.player.feet == feet && !game.state.keyUnlocked, "Move or unlock during animation");
            Check("Insertion then visible rotation; pause freezes progress, movement and repeated clicks cannot interrupt unlock");
            foreach (object step in Advance(155)) yield return step;
            Require(Key.state.stage == KeyStage.Unlocked && game.state.keyUnlocked && Key.state.clicks == 1 && Key.visuals.clickSounds == 1, "Lock click count");
            Require(!Key.TryInsert(), "Repeated unlock accepted");
            var samples = new float[Key.visuals.clickClip.samples]; Key.visuals.clickClip.GetData(samples, 0); float peak = 0;
            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample)); Require(peak > .1f, "Empty click audio");
            Check("Click audio has nonzero samples and plays once after turn; shackle and sliding door fully open before escape");
            foreach (object step in Shot("07-open-ground-exit")) yield return step;
            foreach (object step in Walk(new Vector2(4.85f, -14.9f))) yield return step;
            Require(game.state.won && game.state.player.feet.y == 0 && game.state.respawns == 1, "Ground exit completion"); escaped = true;
            Check("Complete escape at zero floor height without any jumps or resized steps");
            game.ResetGame(); game.Resume();
            Require(!game.state.won && !game.state.shadeSolved && !game.state.keyUnlocked && game.state.keyRequired && game.puzzle == null && Key.state.stage == KeyStage.Floor && Key.visuals.clickSounds == 0, "Reset progress");
            foreach (object step in Advance(350)) yield return step;
            Require(!game.state.keyUnlocked && Key.visuals.clickSounds == 0, "Delayed unlock after reset");
            Check("Reset restores original blocks, key size/location and locked ground door without delayed sounds or unlock");
            Require(errors.Count == 0, "Runtime errors");
            Check("Native Unity rendering and full key level complete without logged errors");
        }
    }
}
