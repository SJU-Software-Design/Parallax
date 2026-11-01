using System;
using UnityEngine;

namespace ShadeLink
{
    public sealed class ShadowPuzzleGame : MonoBehaviour
    {
        public ShadowPuzzleState state;
        public ShadowPuzzleVisuals visuals;
        public ShadowPuzzleHud hud;
        public int aimed = -1;
        public bool blocked, hints, automated;
        public string notice;
        public float noticeUntil;
        float matchClock;
        AudioSource audioSource;
        AudioClip grabTone, readyTone, restoreTone;
        public Font font;
        void Awake()
        {
            Application.targetFrameRate = 120;
            automated = Array.IndexOf(Environment.GetCommandLineArgs(),"-shadow-smoke") >= 0;
            Application.runInBackground = automated;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleGothic", "Arial Unicode MS" },32);
#else
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" },32);
#endif
            state = new ShadowPuzzleState();
            visuals = new GameObject("Shadow Gallery").AddComponent<ShadowPuzzleVisuals>(); visuals.Build(font);
            hud = gameObject.AddComponent<ShadowPuzzleHud>(); hud.Initialize(this,font);
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.volume = .13f;
            grabTone = Tone(440,.09f); readyTone = Tone(660,.4f); restoreTone = Tone(880,.55f);
            state.Evaluate(0); visuals.Sync(this);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (automated) gameObject.AddComponent<ShadowPuzzleSmoke>().game = this;
        }
        static AudioClip Tone(float hz,float seconds)
        {
            const int rate = 22050; var data = new float[(int)(seconds * rate)];
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Sin(i * hz * 2 * 3.14159274f / rate) * Mathf.Sin(3.14159274f * i / data.Length) * .3f;
            var clip = AudioClip.Create("Gallery chime",data.Length,1,rate,false); clip.SetData(data,0); return clip;
        }
        void Play(AudioClip tone) { if (!automated) audioSource.PlayOneShot(tone); }
        public void Tell(string text) { notice = text; noticeUntil = Time.unscaledTime + 4; }
        public void Resume()
        {
            if (state.won) ResetPuzzle(); state.started = state.active = true;
            if (!automated) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
        public void Pause() { state.active = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        public void ResetPuzzle()
        {
            bool active = state.active, started = state.started;
            state = new ShadowPuzzleState { active = active, started = started };
            blocked = false; aimed = -1; matchClock = 0; notice = null;
            state.Evaluate(0); visuals.Sync(this); Tell("도형과 그림자, 액자가 처음 상태로 돌아왔습니다.");
        }
        void OnApplicationFocus(bool focus) { if (!focus && !automated && state != null) Pause(); }
        void Update()
        {
            if (automated) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Pause(); return; }
            if (Input.GetKeyDown(KeyCode.R)) ResetPuzzle();
            if (!state.active || state.won) return;
            if (Cursor.lockState != CursorLockMode.Locked) { Pause(); return; }
            state.yaw -= Input.GetAxisRaw("Mouse X") * .025f;
            state.pitch = Mathf.Clamp(state.pitch + Input.GetAxisRaw("Mouse Y") * .025f,-1.36f,1.30f);
            if (Input.GetKeyDown(KeyCode.H)) hints = !hints;
            SetFixedSize(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            float forward = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            float right = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            Vector2 v = new Vector2(-Mathf.Sin(state.yaw)*forward+Mathf.Cos(state.yaw)*right,-Mathf.Cos(state.yaw)*forward-Mathf.Sin(state.yaw)*right);
            Tick(Mathf.Min(Time.deltaTime,.1f),Vector2.ClampMagnitude(v,1)*3,Input.GetMouseButtonDown(0));
        }
        public void SetFixedSize(bool value)
        {
            if (value == state.fixedSize) return;
            state.fixedSize = value;
            // Re-anchor when changing mode so the object never jumps on Shift.
            if (state.held != null) state.held = ShadowPuzzleRules.Capture(state,state.held.index);
        }
        public void Interact()
        {
            if (!state.active || state.won) return;
            if (state.held != null)
            {
                int index = state.held.index; ShadowPiece piece = state.pieces[index];
                Vector2 goal = ShadowPuzzleRules.Shapes[piece.shape].solution;
                var aligned = new ShadowPiece(piece.shape,goal);
                if ((piece.position-goal).magnitude < .28f && Mathf.Abs(piece.scale-1) < .12f
                    && ShadowPuzzleRules.ValidPlacement(aligned,state.pieces,state.player,index))
                { state.pieces[index] = aligned; Tell("가까운 윤곽에 도형이 맞춰졌습니다."); }
                state.held = null; blocked = false; state.Evaluate(0); Play(grabTone); return;
            }
            if (state.stage == ShadowStage.Carrying)
            {
                if (ShadowPuzzleRules.AimFrame(state))
                { state.Restore(); Tell("강아지가 액자로 돌아왔습니다. 오른쪽 출구가 열렸습니다!"); Play(restoreTone); }
                else if (ShadowPuzzleRules.AimFloor(state,out Vector2 point,out _))
                { state.DropShadow(point); Tell("그림자를 바닥에 놓았습니다. 다시 조준하고 클릭하면 집을 수 있습니다."); }
                else Tell("액자 가까이에서 조준해 넣거나, 바닥을 보고 클릭해 내려놓으세요.");
                return;
            }
            if (ShadowPuzzleRules.AimToken(state))
            { state.PickShadow(); Tell("강아지 그림자를 들었습니다. 벽의 액자로 가져가세요."); Play(grabTone); return; }
            aimed = ShadowPuzzleRules.Aim(state,out _);
            if (state.stage == ShadowStage.Arranging && aimed >= 0)
            { state.held = ShadowPuzzleRules.Capture(state,aimed); state.stable = 0; Play(grabTone); return; }
            if (state.stage == ShadowStage.Arranging)
                Tell("아직 그림자는 집을 수 없습니다. 일곱 도형의 그림자를 강아지 윤곽에 맞춰주세요.");
            else if (state.stage == ShadowStage.Ready) Tell("도형 아래에서 빛나는 강아지 그림자를 조준해 집어주세요.");
        }
        public void Tick(float dt,Vector2 velocity,bool click)
        {
            if (!state.active || state.won) return;
            state.elapsed += dt;
            if (click) Interact();
            // Carrying is allowed. Ground-plane collision and substeps prevent
            // walking through plates while keeping the familiar WASD controls.
            ShadowPuzzleRules.Move(state,velocity,dt);
            if (state.held != null)
            {
                blocked = !ShadowPuzzleRules.Place(state,out ShadowPiece placed);
                if (!blocked) state.pieces[state.held.index] = placed;
            }
            matchClock += dt;
            if (matchClock >= .10f)
            {
                if (state.Evaluate(matchClock)) { Tell("강아지 그림자 완성! 빛나는 그림자를 클릭해 집을 수 있습니다."); Play(readyTone); }
                matchClock = 0;
            }
            aimed = ShadowPuzzleRules.Aim(state,out _);
            if (state.DoorUnlocked && state.player.feet.x > 4.1f && state.player.feet.x < 5.9f && state.player.feet.z < -7.65f)
            { state.won = true; Pause(); Play(restoreTone); }
            visuals.Sync(this);
        }
        void OnDestroy()
        {
            if (grabTone != null) Destroy(grabTone); if (readyTone != null) Destroy(readyTone); if (restoreTone != null) Destroy(restoreTone);
        }
    }
}
