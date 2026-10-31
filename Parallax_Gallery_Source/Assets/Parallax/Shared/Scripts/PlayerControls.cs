using UnityEngine;

namespace ShadeLink
{
    /// <summary>Legacy Input adapter. Look values are radians accumulated during this frame.</summary>
    public sealed class PlayerControls
    {
        public struct Frame
        {
            public Vector2 Move, Look;
            public bool JumpPressed, JumpHeld, GrabPressed, RotateHeld, CancelPressed;
        }
        public struct Options
        {
            public float Sensitivity, FieldOfView;
            public bool InvertX, InvertY;
        }

        const string PreferencePrefix = "Parallax.Controls.";
        const string MoveX = "PLX Move X", MoveY = "PLX Move Y";
        const string LookX = "PLX Look X", LookY = "PLX Look Y";
        const string DpadX = "PLX Dpad X", DpadY = "PLX Dpad Y", Triggers = "PLX Triggers";
        const float StickDeadzone = .15f;
        static bool optionsLoaded;
        static Options options;
        static Camera configuredCamera;
        Vector2 previousStick, lookRamp;
        bool suppressNextRead = true, previousGrabTrigger;
        public bool UsingGamepad { get; private set; }

        public static Options DefaultOptions
        { get { return new Options { Sensitivity = 2, FieldOfView = 70 }; } }
        public static Options CurrentOptions
        {
            get
            {
                if (!optionsLoaded)
                {
                    options = new Options
                    {
                        Sensitivity = ClampSensitivity(PlayerPrefs.GetFloat(PreferencePrefix + "Sensitivity", 2)),
                        FieldOfView = ClampFieldOfView(PlayerPrefs.GetFloat(PreferencePrefix + "FieldOfView", 70)),
                        InvertX = PlayerPrefs.GetInt(PreferencePrefix + "InvertX", 0) != 0,
                        InvertY = PlayerPrefs.GetInt(PreferencePrefix + "InvertY", 0) != 0
                    };
                    optionsLoaded = true;
                }
                return options;
            }
            set
            {
                options = value;
                options.Sensitivity = ClampSensitivity(options.Sensitivity);
                options.FieldOfView = ClampFieldOfView(options.FieldOfView);
                optionsLoaded = true;
                PlayerPrefs.SetFloat(PreferencePrefix + "Sensitivity", options.Sensitivity);
                PlayerPrefs.SetFloat(PreferencePrefix + "FieldOfView", options.FieldOfView);
                PlayerPrefs.SetInt(PreferencePrefix + "InvertX", options.InvertX ? 1 : 0);
                PlayerPrefs.SetInt(PreferencePrefix + "InvertY", options.InvertY ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        public static float ClampSensitivity(float value) { return Finite(value) ? Mathf.Clamp(value, .1f, 10) : 2; }
        public static float ClampFieldOfView(float value) { return Finite(value) ? Mathf.Clamp(value, 70, 90) : 70; }

        public static Vector2 ApplyRadialDeadzone(Vector2 value, float deadzone)
        {
            if (!Finite(value.x) || !Finite(value.y)) return Vector2.zero;
            deadzone = Finite(deadzone) ? Mathf.Clamp(deadzone, 0, .99f) : StickDeadzone;
            float magnitude = value.magnitude;
            return magnitude <= deadzone ? Vector2.zero : value / magnitude * Mathf.Clamp01((magnitude - deadzone) / (1 - deadzone));
        }

        public static Vector2 MouseLookDelta(Vector2 delta, float sensitivity, bool invertX, bool invertY)
        {
            if (!Finite(delta.x) || !Finite(delta.y)) return Vector2.zero;
            // Keep the existing Unity mouse feel (.023 radians at sensitivity 2).
            // Rewired's .05 multiplier uses different units and is not a Unity conversion.
            return Vector2.Scale(delta, new Vector2(invertX ? -1 : 1, invertY ? -1 : 1)) * (.0115f * ClampSensitivity(sensitivity));
        }

        static float RampStart(float age, float previous, float current)
        { return current == 0 || previous == 0 || Mathf.Sign(previous) != Mathf.Sign(current) ? 0 : age; }
        public static Vector2 StepLookRamp(Vector2 ramp, Vector2 previous, Vector2 current, float dt)
        {
            dt = Finite(dt) ? Mathf.Max(0, dt) : 0;
            return new Vector2(current.x == 0 ? 0 : Mathf.Clamp01(RampStart(ramp.x, previous.x, current.x) + dt),
                current.y == 0 ? 0 : Mathf.Clamp01(RampStart(ramp.y, previous.y, current.y) + dt));
        }
        static float AverageRamp(float age, float dt)
        {
            if (dt <= 0) return 0;
            float rampTime = Mathf.Min(dt, 1 - Mathf.Clamp01(age));
            return ((age + rampTime * .5f) * rampTime + dt - rampTime) / dt;
        }
        public static Vector2 GamepadLookDelta(Vector2 stick, Vector2 ramp, float dt, bool aimedGrabbable, bool held,
            float sensitivity, bool invertX, bool invertY)
        {
            if (!Finite(dt) || dt <= 0) return Vector2.zero;
            Vector2 rate = new Vector2(300 * (aimedGrabbable ? .65f : 1), 240 * (held ? 1.5f : 1));
            rate = Vector2.Scale(rate, new Vector2(invertX ? -1 : 1, invertY ? -1 : 1));
            return Vector2.Scale(Vector2.Scale(stick, ramp), rate) * (Mathf.Deg2Rad * dt * ClampSensitivity(sensitivity) / 2);
        }
        public static void ApplyLook(ref float yawRadians, ref float pitchRadians, Vector2 look)
        {
            yawRadians += look.x;
            pitchRadians = Mathf.Clamp(pitchRadians + look.y, -70 * Mathf.Deg2Rad, 90 * Mathf.Deg2Rad);
        }
        public static Vector2 ObjectRotationDegrees(Vector2 look)
        { return new Vector2(look.x, -look.y) * Mathf.Rad2Deg; }

        public static bool PausePressed
        { get { return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Joystick1Button6) || Input.GetKeyDown(KeyCode.Joystick1Button7); } }
        public static bool RotationHeld
        { get { return Input.GetMouseButton(1) || Input.GetKey(KeyCode.Joystick1Button4) || Input.GetAxisRaw(Triggers) < -.5f; } }

        public Frame Read(float dt, bool aimedGrabbable, bool held)
        {
            float trigger = Input.GetAxisRaw(Triggers);
            bool grabTrigger = trigger > .5f;
            if (!Finite(dt) || dt <= 0) { Reset(); previousGrabTrigger = grabTrigger; return new Frame(); }
            if (suppressNextRead)
            {
                suppressNextRead = false; previousGrabTrigger = grabTrigger;
                return new Frame(); // Do not replay a resume click or a cursor-lock mouse delta.
            }
            var settings = CurrentOptions;
            Vector2 keys = new Vector2((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) -
                (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) -
                (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
            Vector2 move = ApplyRadialDeadzone(new Vector2(Input.GetAxisRaw(MoveX), Input.GetAxisRaw(MoveY)), StickDeadzone);
            Vector2 dpad = new Vector2(Input.GetAxisRaw(DpadX), Input.GetAxisRaw(DpadY));
            if (dpad.sqrMagnitude > .25f) move = Vector2.ClampMagnitude(dpad, 1);
            // The motor filters keyboard axes individually, then caps diagonal speed.
            if (keys.sqrMagnitude > 0) move = keys;
            Vector2 stick = ApplyRadialDeadzone(new Vector2(Input.GetAxisRaw(LookX), Input.GetAxisRaw(LookY)), StickDeadzone);
            Vector2 start = new Vector2(RampStart(lookRamp.x, previousStick.x, stick.x), RampStart(lookRamp.y, previousStick.y, stick.y));
            Vector2 average = new Vector2(stick.x == 0 ? 0 : AverageRamp(start.x, dt), stick.y == 0 ? 0 : AverageRamp(start.y, dt));
            lookRamp = StepLookRamp(lookRamp, previousStick, stick, dt); previousStick = stick;
            Vector2 mouse = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            Vector2 look = MouseLookDelta(mouse,
                settings.Sensitivity, settings.InvertX, settings.InvertY) + GamepadLookDelta(stick, average, dt,
                aimedGrabbable, held, settings.Sensitivity, settings.InvertX, settings.InvertY);
            var frame = new Frame
            {
                Move = move, Look = look,
                JumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Joystick1Button0),
                JumpHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Joystick1Button0),
                GrabPressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Joystick1Button1) ||
                    Input.GetKeyDown(KeyCode.Joystick1Button2) || Input.GetKeyDown(KeyCode.Joystick1Button5) || (grabTrigger && !previousGrabTrigger),
                RotateHeld = held && RotationHeld,
                CancelPressed = Input.GetKeyDown(KeyCode.Backspace)
            };
            if (keys.sqrMagnitude > 0)
                UsingGamepad = false;
            else if (move.sqrMagnitude > 0)
                UsingGamepad = true;
            else if (mouse.sqrMagnitude > 0 || Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0) || Input.GetMouseButton(1))
                UsingGamepad = false;
            else if (stick.sqrMagnitude > 0 || Mathf.Abs(trigger) > .5f ||
                Input.GetKey(KeyCode.Joystick1Button0) || Input.GetKey(KeyCode.Joystick1Button1) ||
                Input.GetKey(KeyCode.Joystick1Button2) || Input.GetKey(KeyCode.Joystick1Button4) || Input.GetKey(KeyCode.Joystick1Button5))
                UsingGamepad = true;
            previousGrabTrigger = grabTrigger;
            return frame;
        }
        public void Reset()
        { previousStick = Vector2.zero; lookRamp = Vector2.zero; previousGrabTrigger = false; suppressNextRead = true; }

        /// <summary>Call at camera creation or resume, never from an ending animation's Update.</summary>
        public static void ConfigureCamera(Camera camera)
        {
            if (camera == null) return;
            configuredCamera = camera; camera.fieldOfView = CurrentOptions.FieldOfView; camera.nearClipPlane = .05f;
        }

        public static void DrawOptions(Rect rect, Font font = null)
        {
            var settings = CurrentOptions; var before = settings;
            var label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = false };
            label.normal.textColor = new Color(.94f, .96f, 1);
            var toggle = new GUIStyle(GUI.skin.toggle) { font = font, fontSize = 18 };
            var button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18 };
            GUI.BeginGroup(rect);
            GUI.Label(new Rect(0, 0, rect.width, 26), "조작 설정", label);
            float half = rect.width * .5f;
            GUI.Label(new Rect(0, 31, 134, 28), "감도  " + settings.Sensitivity.ToString("0.0"), label);
            settings.Sensitivity = GUI.HorizontalSlider(new Rect(132, 40, half - 154, 22), settings.Sensitivity, .1f, 10);
            GUI.Label(new Rect(half, 31, 140, 28), "시야각  " + Mathf.RoundToInt(settings.FieldOfView) + "°", label);
            settings.FieldOfView = GUI.HorizontalSlider(new Rect(half + 140, 40, half - 148, 22), settings.FieldOfView, 70, 90);
            settings.InvertX = GUI.Toggle(new Rect(0, 72, 210, 28), settings.InvertX, "좌우 시점 반전", toggle);
            settings.InvertY = GUI.Toggle(new Rect(235, 72, 210, 28), settings.InvertY, "상하 시점 반전", toggle);
            if (GUI.Button(new Rect(rect.width - 135, 67, 135, 34), "기본값 복원", button)) settings = DefaultOptions;
            GUI.Label(new Rect(0, 110, rect.width, 28), "오른쪽 버튼 + 마우스: 회전  ·  Backspace: 잡기 취소", label);
            GUI.EndGroup();
            if (settings.Sensitivity != before.Sensitivity || settings.FieldOfView != before.FieldOfView ||
                settings.InvertX != before.InvertX || settings.InvertY != before.InvertY)
            {
                CurrentOptions = settings;
                ConfigureCamera(configuredCamera != null ? configuredCamera : Camera.main);
            }
        }

        static void Mark(Rect rect, Color color)
        { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); }
        public static void DrawCursor(Vector2 center, bool aimedGrabbable, bool held, bool blocked, Color accent, Color idle)
        {
            Color previous = GUI.color;
            bool rotate = held && RotationHeld;
            Color color = held && blocked ? new Color(1, .3f, .12f) : aimedGrabbable || held ? accent : idle;
            // Absolute unscaled time keeps feedback cadence independent of frame count and pause time scale.
            float pulse = .88f + .12f * Mathf.Sin(Time.unscaledTime * 7);
            color.a *= held && blocked ? pulse : 1;
            Mark(new Rect(center.x - 2, center.y - 2, 4, 4), color);
            if (aimedGrabbable || held)
            {
                float r = held ? 10 : 7;
                Matrix4x4 matrix = GUI.matrix;
                if (rotate) GUIUtility.RotateAroundPivot(45, center);
                Mark(new Rect(center.x - r, center.y - r, 7, 2), color);
                Mark(new Rect(center.x + r - 7, center.y - r, 7, 2), color);
                Mark(new Rect(center.x - r, center.y + r - 2, 7, 2), color);
                Mark(new Rect(center.x + r - 7, center.y + r - 2, 7, 2), color);
                GUI.matrix = matrix;
                if (blocked && held)
                {
                    GUIUtility.RotateAroundPivot(45, center);
                    Mark(new Rect(center.x - 6, center.y - 1, 12, 2), color);
                    Mark(new Rect(center.x - 1, center.y - 6, 2, 12), color);
                    GUI.matrix = matrix;
                }
            }
            GUI.color = previous;
        }
    }
}
