using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink
{
    public sealed class IndoorGame : MonoBehaviour
    {
        public Player player;
        public Block[] blocks;
        public Grab held;
        Block grabOriginal;
        public List<Vector2>[] shadows;
        public IndoorVisuals visuals;
        public IndoorHud hud;
        public Font font;
        public float yaw, pitch = -.12f, exposure, elapsed;
        public int aimed = -1, checkpoint, respawns;
        public bool active, started, won, safe = true, invalid, limited, automated, hints;
        public bool firstLinked, secondLinked;
        public string notice;
        public float noticeUntil;
        public Vector2 savePoint;
        float routeClock;
        public float sensitivity = .024f;

        void Awake()
        {
            automated = Array.IndexOf(Environment.GetCommandLineArgs(), "-indoor-smoke") >= 0;
            Application.targetFrameRate = 120; Application.runInBackground = automated;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleGothic", "Arial" }, 32);
#else
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);
#endif
            ResetGame();
            visuals = new GameObject("Indoor light corridor").AddComponent<IndoorVisuals>(); visuals.Build(this);
            hud = gameObject.AddComponent<IndoorHud>(); hud.Initialize(this);
            Refresh(true); visuals.Sync();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (automated) gameObject.AddComponent<IndoorSmoke>().game = this;
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-indoor-preview") >= 0) StartCoroutine(CaptureMenu());
        }
        IEnumerator CaptureMenu()
        {
            // The interactive player remains on its start menu. Capture its own
            // framebuffer once for layout review; no desktop capture or inputs.
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-shade-output");
            if (at >= 0 && at + 1 < args.Length)
            { Directory.CreateDirectory(args[at + 1]); ScreenCapture.CaptureScreenshot(Path.Combine(args[at + 1], "00-menu-ui.png")); }
            yield return null; Application.runInBackground = false;
        }
        public void Tell(string message, float seconds = 4) { notice = message; noticeUntil = Time.unscaledTime + seconds; }
        public void ResetGame()
        {
            player = new Player(IndoorRules.Start); blocks = IndoorRules.InitialBlocks(); held = null;
            yaw = 0; pitch = -.12f; exposure = elapsed = 0; checkpoint = respawns = 0;
            savePoint = IndoorRules.Start; won = invalid = limited = false; safe = true;
            notice = null; Refresh(true); visuals?.Sync();
        }
        public void Resume()
        {
            if (won) ResetGame(); active = started = true;
            if (!automated) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
        public void Pause() { active = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        void OnApplicationFocus(bool focus) { if (!focus && !automated) Pause(); }
        void Update()
        {
            if (automated) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (active) Pause(); else Resume(); }
            if (active && Cursor.lockState != CursorLockMode.Locked) Pause();
            if (!active || won) return;
            if (Input.GetKeyDown(KeyCode.R)) { ResetGame(); return; }
            if (Input.GetKeyDown(KeyCode.H)) hints = !hints;
            yaw += Input.GetAxisRaw("Mouse X") * sensitivity;
            pitch = Mathf.Clamp(pitch + Input.GetAxisRaw("Mouse Y") * sensitivity, -1.4f, 1.3f);
            if (Input.GetMouseButtonDown(1)) CancelGrab();
            if (Input.GetMouseButtonDown(0)) Interact();
            float f = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            float r = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            Vector2 direction = new Vector2(Mathf.Sin(yaw) * f + Mathf.Cos(yaw) * r, Mathf.Cos(yaw) * f - Mathf.Sin(yaw) * r);
            Tick(Mathf.Min(Time.deltaTime, .1f), Vector2.ClampMagnitude(direction, 1) * IndoorRules.Speed);
        }
        public void Interact()
        {
            if (!active || won) return;
            if (held != null) { held = null; invalid = limited = false; Refresh(true); return; }
            aimed = IndoorRules.Aim(player, yaw, pitch, blocks);
            if (aimed < 0) return;
            grabOriginal = blocks[aimed]; held = IndoorRules.Capture(player, yaw, pitch, aimed, blocks);
        }
        public void CancelGrab()
        {
            if (held == null) return;
            blocks[held.index] = grabOriginal; held = null; invalid = limited = false; Refresh(true);
            Tell("사물을 잡기 전 위치로 되돌렸습니다.");
        }
        public void Refresh(bool routes)
        {
            shadows = IndoorRules.Shadows(blocks);
            if (routes)
            {
                firstLinked = IndoorRules.Route(blocks, shadows, IndoorRules.Start, 13.25f).Count > 0;
                secondLinked = IndoorRules.Route(blocks, shadows, new Vector2(0, 17), 27.25f).Count > 0;
                routeClock = 0; hud?.RefreshMap();
            }
        }
        public void Respawn()
        {
            held = null; invalid = limited = false;
            Vector2 spawn = savePoint; float min = checkpoint == 0 ? .5f : checkpoint == 1 ? 13.4f : 27.4f;
            float max = checkpoint == 0 ? 4.5f : checkpoint == 1 ? 18.5f : 35;
            if (!IndoorRules.CanWalk(spawn, blocks))
            {
                float best = (1f/0f);
                for (float z = min; z <= max; z += .25f) for (float x = -4.2f; x <= 4.2f; x += .25f)
                {
                    var p = new Vector2(x, z); float d = (p - savePoint).sqrMagnitude;
                    if (d < best && IndoorRules.CanWalk(p, blocks) && IndoorRules.FootSafe(p, shadows)) { spawn = p; best = d; }
                }
            }
            player = new Player(spawn); exposure = 0; yaw = 0; pitch = -.12f; respawns++;
            Tell(checkpoint == 0 ? "빛에 오래 노출되어 시작 쉼터로 돌아왔습니다." : "빛에 오래 노출되어 저장한 쉼터로 돌아왔습니다.");
        }
        public void Tick(float dt, Vector2 velocity)
        {
            if (!active || won) return;
            dt = Mathf.Clamp(dt, 0, .1f); elapsed += dt;
            if (held != null)
            {
                invalid = !IndoorRules.Placement(player, yaw, pitch, held, blocks, out Block candidate, out limited);
                if (!invalid) blocks[held.index] = candidate;
                routeClock += dt; Refresh(routeClock > .2f);
            }
            else
            {
                velocity = Vector2.ClampMagnitude(velocity, IndoorRules.Speed);
                int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 120)); float step = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    Vector2 p = player.Point + new Vector2(velocity.x * step, 0);
                    if (IndoorRules.CanWalk(p, blocks)) player.feet.x = p.x;
                    p = player.Point + new Vector2(0, velocity.y * step);
                    if (IndoorRules.CanWalk(p, blocks)) player.feet.z = p.y;
                }
            }
            aimed = IndoorRules.Aim(player, yaw, pitch, blocks);
            safe = IndoorRules.FootSafe(player.Point, shadows);
            exposure = Mathf.Clamp01(exposure + dt * (safe ? -IndoorRules.RecoveryRate : IndoorRules.ExposureRate));
            if (exposure >= 1) { Respawn(); safe = IndoorRules.FootSafe(player.Point, shadows); }
            if (held == null && safe)
            {
                int reached = player.feet.z >= 27.3f ? 2 : player.feet.z >= 13.3f ? 1 : 0;
                if (reached > checkpoint)
                { checkpoint = reached; savePoint = player.Point; Tell(checkpoint == 1 ? "중간 쉼터 저장 완료 · 쉼터 앞쪽의 02 표시에서 다음 차광판을 잡아보세요." : "두 통로를 건넜습니다 · 정면 출구로 걸어가세요.", 6); }
            }
            if (checkpoint == 2 && held == null && player.feet.z > 34.5f && Mathf.Abs(player.feet.x) < 1.25f)
            { won = true; Pause(); }
            visuals?.Sync();
        }
    }
}
