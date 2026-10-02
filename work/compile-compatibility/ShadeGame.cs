using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink
{
    public sealed class ShadeGame : MonoBehaviour
    {
        public static bool courtyardMode;
        public static void SwitchMode(bool courtyard)
        {
            courtyardMode = courtyard;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
        public GameState state;
        public ShadeVisuals visuals;
        public ShadeHud hud;
        public CourtyardShadowPuzzle puzzle;
        public KeyPuzzle keyPuzzle;
        public List<Vector2>[] SafetyShadows => puzzle == null ? shadows : puzzle.SafetyShadows(shadows);
        public List<Vector2>[] shadows;
        public bool firstLinked, secondLinked, connected, blocked, limited, safe = true;
        public int aimed = -1;
        public float sensitivity = .025f;
        public string notice;
        public float noticeUntil;
        public bool automated;
        float routeClock;
        AudioSource audioSource;
        AudioClip pickupTone, dropTone, saveTone, failTone, winTone;

        void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            // The courtyard with sunlight survival is the actual game. Keep the
            // earlier standalone gallery available only for its explicit QA run.
            if (Array.IndexOf(args, "-shadow-smoke") >= 0)
            {
                enabled = false;
                gameObject.AddComponent<ShadowPuzzleGame>();
                return;
            }
            Application.targetFrameRate = 120;
            Application.runInBackground = false;
            bool integrated = Array.IndexOf(args, "-integrated-smoke") >= 0 || Array.IndexOf(args, "-dog-puzzle") >= 0;
            bool keyMode = !integrated && !courtyardMode && Array.IndexOf(args, "-shade-smoke") < 0 && Array.IndexOf(args, "-courtyard") < 0;
            state = new GameState { pictureRequired = integrated, groundExit = keyMode, keyRequired = keyMode };
            // Use the platform's Korean font without redistributing OS font files.
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleSDGothicNeo-Regular", "AppleGothic", "Arial Unicode MS", "Arial" }, 32);
#else
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);
#endif
            visuals = new GameObject("Courtyard").AddComponent<ShadeVisuals>(); visuals.Build(font, keyMode);
            if (integrated) puzzle = new CourtyardShadowPuzzle(this, font);
            if (keyMode) keyPuzzle = new KeyPuzzle(this, font);
            hud = gameObject.AddComponent<ShadeHud>(); hud.Initialize(this, font);
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend = 0; audioSource.volume = .18f;
            pickupTone = Tone(420, .075f); dropTone = Tone(270, .12f); saveTone = Tone(660, .27f);
            failTone = Tone(130, .25f); winTone = Tone(880, .55f);
            Recalculate(true); visuals.Sync(state, shadows, aimed, blocked);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            bool keySmoke = Array.IndexOf(args, "-key-smoke") >= 0;
            automated = keySmoke || Array.IndexOf(args, "-shade-smoke") >= 0 || Array.IndexOf(args, "-integrated-smoke") >= 0;
            if (keySmoke) gameObject.AddComponent<KeyPuzzleSmoke>().game = this;
            else if (automated) gameObject.AddComponent<ShadeSmoke>().game = this;
        }
        static AudioClip Tone(float hz, float duration)
        {
            const int rate = 22050; int length = (int)(rate * duration); var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate, envelope = Mathf.Sin(3.14159274f * i / length);
                data[i] = Mathf.Sin(2 * 3.14159274f * hz * t) * envelope * envelope * .35f;
            }
            var clip = AudioClip.Create("Synth " + hz, length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void Play(AudioClip tone) { if (!automated) audioSource.PlayOneShot(tone); }
        public void Tell(string text, float seconds = 3.5f) { notice = text; noticeUntil = Time.unscaledTime + seconds; }
        public void Resume()
        {
            if (state.won) ResetGame();
            state.active = state.started = true;
            keyPuzzle?.visuals.SetPaused(false);
            if (!automated) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
        public void Pause()
        { state.active = false; keyPuzzle?.visuals.SetPaused(true); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        public void ResetGame()
        {
            bool wasActive = state.active;
            state = new GameState { active = wasActive, started = wasActive, pictureRequired = puzzle != null, keyRequired = keyPuzzle != null, groundExit = keyPuzzle != null };
            puzzle?.Reset();
            keyPuzzle?.Reset();
            blocked = limited = false; aimed = -1; notice = null;
            Recalculate(true); visuals.Sync(state, shadows, aimed, false);
            Tell("방과 사물이 처음 상태로 돌아왔습니다.");
        }
        void OnApplicationFocus(bool focus) { if (state != null && !focus && !automated) Pause(); }
        void Update()
        {
            if (automated) return;
            if (Input.GetKeyDown(KeyCode.R)) ResetGame();
            if (Input.GetKeyDown(KeyCode.Escape)) Pause();
            if (state.active && Cursor.lockState != CursorLockMode.Locked) Pause();
            if (!state.active || state.won) return;
            if (keyPuzzle == null || !keyPuzzle.Busy)
            {
                state.yaw -= Input.GetAxisRaw("Mouse X") * sensitivity;
                state.pitch = Mathf.Clamp(state.pitch + Input.GetAxisRaw("Mouse Y") * sensitivity, -1.36f, 1.36f);
            }
            if (keyPuzzle != null && state.held == null)
            {
                if (Input.GetKeyDown(KeyCode.F)) keyPuzzle.ToggleResize();
                if (Input.GetKeyDown(KeyCode.E)) keyPuzzle.TryInsert();
            }
            if (puzzle != null)
            {
                puzzle.SetFixedSize(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                if (Input.GetKeyDown(KeyCode.H)) puzzle.hints = !puzzle.hints;
            }
            float forward = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            float right = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            Vector2 velocity = new Vector2(-Mathf.Sin(state.yaw) * forward + Mathf.Cos(state.yaw) * right,
                -Mathf.Cos(state.yaw) * forward - Mathf.Sin(state.yaw) * right);
            if (velocity.sqrMagnitude > 1) velocity.Normalize();
            Tick(Mathf.Min(Time.deltaTime, .1f), velocity * Rules.Speed, Input.GetKeyDown(KeyCode.Space), Input.GetMouseButtonDown(0));
        }
        public void Interact()
        {
            if (!state.active || state.won) return;
            if (keyPuzzle != null && keyPuzzle.Busy) return;
            if (state.held != null)
            {
                state.held = null; blocked = limited = false; Recalculate(true); Play(dropTone); return;
            }
            if (puzzle != null && puzzle.Interact()) { Recalculate(true); puzzle.visuals.Sync(puzzle); return; }
            if (keyPuzzle != null && keyPuzzle.Interact()) { keyPuzzle.visuals.Sync(); return; }
            aimed = Rules.Aim(state.player, state.yaw, state.pitch, state.blocks, state.groundExit);
            if (puzzle != null && puzzle.BlocksOriginalAim(aimed)) aimed = -1;
            if (aimed < 0) return;
            if (!state.player.grounded) { Tell("착지한 뒤 사물을 잡을 수 있습니다."); return; }
            if (state.player.support == aimed) { Tell("딛고 있는 사물은 내려온 뒤 잡아주세요."); return; }
            state.held = Rules.Capture(state.player, state.yaw, state.pitch, aimed, state.blocks);
            Play(pickupTone);
        }
        public void Recalculate(bool routes)
        {
            shadows = Rules.Shadows(state.blocks);
            if (routes)
            {
                var safety = SafetyShadows; Bounds[] extra = puzzle?.solids;
                firstLinked = Rules.Route(state.blocks, safety, null, Rules.Rest, extra, state.groundExit).Count > 0;
                secondLinked = Rules.Route(state.blocks, safety, Rules.Rest, null, extra, state.groundExit).Count > 0;
                connected = firstLinked && secondLinked && Rules.Route(state.blocks, safety, null, null, extra, state.groundExit).Count > 0;
                routeClock = 0; hud?.RefreshMap();
            }
        }
        public void Tick(float dt, Vector2 velocity, bool jump, bool click)
        {
            if (!state.active || state.won) return;
            state.elapsed += dt;
            if (click) Interact();
            if (state.held != null)
            {
                float distance = Rules.DistanceFromAim(state.pitch, state.held);
                bool valid = Rules.Placement(state.player, state.yaw, state.held, distance, state.blocks, out Block placed, out limited, state.groundExit);
                if (valid && puzzle != null && puzzle.BlocksPlacement(placed)) valid = false;
                if (valid && keyPuzzle != null && keyPuzzle.BlocksPlacement(placed)) valid = false;
                blocked = !valid;
                if (valid) state.blocks[state.held.index] = placed;
                routeClock += dt; Recalculate(routeClock > .16f);
            }
            else if (keyPuzzle == null || !keyPuzzle.MovementLocked)
                Rules.Simulate(state.player, velocity, dt, state.blocks, puzzle != null && puzzle.HoldingPiece ? false : jump, puzzle?.solids, state.groundExit);
            if (puzzle != null)
            {
                bool moving = puzzle.HoldingPiece;
                puzzle.Tick(dt);
                if (moving) { routeClock += dt; Recalculate(routeClock > .16f); }
            }
            keyPuzzle?.Tick(dt);
            aimed = Rules.Aim(state.player, state.yaw, state.pitch, state.blocks, state.groundExit);
            if (keyPuzzle != null && (keyPuzzle.Holding || keyPuzzle.Busy || keyPuzzle.AimKey())) aimed = -1;
            if (puzzle != null && puzzle.BlocksOriginalAim(aimed)) aimed = -1;
            safe = Rules.SafeAtHeight(state.player, state.blocks, SafetyShadows) || (puzzle != null && puzzle.SafeAboveFloor(state.player));
            state.exposure = Rules.Exposure(state.exposure, dt, safe);
            if (state.exposure >= 1)
            {
                state.Respawn(puzzle?.solids); puzzle?.Respawn(); keyPuzzle?.Respawn(); safe = Rules.SafeAtHeight(state.player, state.blocks, SafetyShadows);
                Tell(state.shadeSolved ? "햇빛에 노출되어 도착 그늘로 돌아왔습니다." : "햇빛에 노출되어 시작 그늘로 돌아왔습니다."); Play(failTone);
            }
            if (Rules.Rest.Contains(state.player.Point)) state.visitedRest = true;
            if ((puzzle == null || !puzzle.HoldingPiece) && state.ReachCheckpoint(connected))
            { Tell(keyPuzzle != null ? "도착 그늘 저장 완료 · 열쇠 크기를 맞춰 자물쇠에 넣고 걸어서 나가세요." : puzzle != null && !puzzle.Restored ? "도착 그늘 저장 완료 · 강아지 그림자로 액자를 복원하면 출구 잠금이 풀립니다." : "그늘 연결 완료 · 이제 두 사물을 발판으로 다시 배치하세요.", 7); Play(saveTone); }
            if (state.CanFinish && (puzzle == null || !puzzle.HoldingPiece))
            {
                state.won = true; Pause(); Play(winTone);
            }
            visuals.Sync(state, shadows, aimed, blocked);
            puzzle?.visuals.Sync(puzzle);
            keyPuzzle?.visuals.Sync();
        }
    }
}
