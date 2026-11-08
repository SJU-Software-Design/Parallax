using UnityEngine;

namespace ShadeLink.Tutorial
{
    public sealed partial class TutorialGame
    {
        const float RotationPracticeDegrees = 12f;
        bool rotationHintCompleted;
        float rotationConfirmationRemaining, rotationPracticeDegrees;

        bool IsRotationLesson => step == 2 && !exitUnlocked && !inClimbRoom && !inSunlightRoom &&
            die != null && die.gameObject.activeInHierarchy;
        bool RotationPickupHintVisible => !paused && IsRotationLesson && Held == null && !rotationHintCompleted;
        bool RotationHintVisible => !paused && IsRotationLesson && Held == die &&
            !rotationHintCompleted;
        bool RotationConfirmationVisible => !paused && IsRotationLesson && Held == die &&
            rotationConfirmationRemaining > 0;

        void ResetRotationHint()
        {
            rotationHintCompleted = false;
            rotationPracticeDegrees = 0;
            ClearRotationHintTransient();
        }

        void ClearRotationHintTransient()
        {
            rotationConfirmationRemaining = 0;
        }

        void BeginRotationHint()
        {
            if (!IsRotationLesson || Held != die || rotationHintCompleted) return;
            rotationPracticeDegrees = 0;
        }

        void TickRotationHint(float dt)
        {
            if (paused) return;
            if (!IsRotationLesson || Held != die) { ClearRotationHintTransient(); return; }
            dt = Mathf.Max(0, dt);
            rotationConfirmationRemaining = Mathf.Max(0, rotationConfirmationRemaining - dt);
        }

        // Called only after an explicit rotation input, using the accepted pose
        // before/after projection. Pickup auto-upright and blocked requests do
        // not count as discovering the rotation control.
        void RecordRotationPractice(float degrees)
        {
            if (!RotationHintVisible) return;
            rotationPracticeDegrees += Mathf.Max(0, degrees);
            if (rotationPracticeDegrees < RotationPracticeDegrees) return;
            rotationHintCompleted = true;
            rotationConfirmationRemaining = 3;
        }

        static float AppliedRotationDegrees(Quaternion before, Quaternion after)
        {
            Quaternion delta = Quaternion.Inverse(before) * after;
            // Preserve tiny frame-to-frame drags instead of losing them to
            // Quaternion.Angle's approximate equality threshold.
            return 2 * Mathf.Atan2(new Vector3(delta.x, delta.y, delta.z).magnitude, Mathf.Abs(delta.w)) * Mathf.Rad2Deg;
        }

        bool TryRotationHintLayout(float width, float height, out Rect banner, out Vector2 anchor)
        {
            banner = default; anchor = default;
            if (Camera == null || (!RotationPickupHintVisible && !RotationHintVisible && !RotationConfirmationVisible)) return false;
            Bounds bounds = die.WorldBounds;
            Vector3 top = Camera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            if (top.z <= Camera.nearClipPlane || top.x < 0 || top.x > width || top.y < 0 || top.y > height) return false;
            anchor = new Vector2(top.x, height - top.y);
            float scale = Mathf.Min(1, (width - 32) / 640, (height - 32) / 180);
            float cardWidth = (RotationPickupHintVisible ? 400 : RotationHintVisible ? 640 : 520) * scale;
            float cardHeight = (RotationPickupHintVisible ? 96 : RotationHintVisible ? 140 : 122) * scale;
            float maximumY = Mathf.Max(16, height - cardHeight - 108);
            banner = new Rect(Mathf.Clamp(anchor.x - cardWidth * .5f, 16, width - cardWidth - 16),
                Mathf.Clamp(anchor.y - cardHeight - 28 * scale, 16, maximumY), cardWidth, cardHeight);
            return true;
        }

        bool DrawRotationHint(float width, float height)
        {
            if (!TryRotationHintLayout(width, height, out Rect banner, out Vector2 anchor)) return false;
            bool pickup = RotationPickupHintVisible, instruction = RotationHintVisible;
            float scale = Mathf.Min(1, (width - 32) / 640, (height - 32) / 180);
            Color previous = GUI.color;
            GUI.color = Color.white;
            Vector2 tail = new Vector2(Mathf.Clamp(anchor.x, banner.x + 18 * scale, banner.xMax - 18 * scale), banner.yMax);
            HintLine(tail, anchor, Mint, 2 * scale);
            GUI.DrawTexture(new Rect(anchor.x - 4 * scale, anchor.y - 4 * scale, 8 * scale, 8 * scale),
                Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Mint, 0, 4 * scale);
            GUI.DrawTexture(banner, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0,
                new Color(.055f, .065f, .065f, .94f), 0, 12 * scale);
            GUI.DrawTexture(banner, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0,
                new Color(.9f, .94f, .91f, .8f), 1 * scale, 12 * scale);
            var style = new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = Mathf.Max(12, Mathf.RoundToInt(28 * scale)), fontStyle = FontStyle.Bold,
                wordWrap = true, alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Mint;
            GUI.Label(new Rect(banner.x + 12 * scale, banner.y + 5 * scale, banner.width - 24 * scale, 34 * scale),
                "사물 회전", style);
            HintLine(new Vector2(banner.x + 22 * scale, banner.y + 43 * scale),
                new Vector2(banner.xMax - 22 * scale, banner.y + 43 * scale), Mint, 1.5f * scale);
            style.fontStyle = FontStyle.Normal;
            style.fontSize = Mathf.Max(12, Mathf.RoundToInt(22 * scale));
            style.normal.textColor = new Color(.98f, .97f, .9f);
            if (pickup)
                GUI.Label(new Rect(banner.x + 12 * scale, banner.y + 45 * scale, banner.width - 24 * scale, 45 * scale),
                    "주사위를 다시 잡아 보세요.", style);
            else if (instruction)
            {
                DrawRotationMouse(new Vector2(banner.x + 22 * scale, banner.y + 62 * scale), scale);
                HintLine(new Vector2(banner.x + 144 * scale, banner.y + 58 * scale),
                    new Vector2(banner.x + 144 * scale, banner.yMax - 16 * scale), Mint, 2 * scale);
                style.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(banner.x + 164 * scale, banner.y + 48 * scale, banner.width - 182 * scale, banner.height - 58 * scale),
                    "우클릭을 누른 채\n상하좌우로 움직여 회전해 보세요.", style);
            }
            else
                GUI.Label(new Rect(banner.x + 14 * scale, banner.y + 46 * scale, banner.width - 28 * scale, banner.height - 50 * scale),
                    "다른 면도 볼 수 있어요.\n이제 먼 벽에 더 크게 놓아 보세요.", style);
            GUI.color = previous;
            return true;
        }

        static void DrawRotationMouse(Vector2 origin, float scale)
        {
            // A small vector mouse with its right button highlighted. Keeping it
            // in the HUD avoids importing another world object or image asset.
            Color ink = new Color(.98f, .97f, .9f);
            for (int y = 1; y < 26; y++)
            {
                float normalized = (y - 27f) / 27;
                float half = 20 * Mathf.Sqrt(Mathf.Max(0, 1 - normalized * normalized));
                GUI.color = Mint;
                GUI.DrawTexture(new Rect(origin.x + 20 * scale, origin.y + y * scale, half * scale, scale + .2f), Texture2D.whiteTexture);
            }
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI * 2 / 32, b = (i + 1) * Mathf.PI * 2 / 32;
                HintLine(origin + new Vector2(20 + Mathf.Cos(a) * 20, 27 + Mathf.Sin(a) * 27) * scale,
                    origin + new Vector2(20 + Mathf.Cos(b) * 20, 27 + Mathf.Sin(b) * 27) * scale, ink, 2 * scale);
            }
            HintLine(origin + new Vector2(0, 26) * scale, origin + new Vector2(40, 26) * scale, ink, 2 * scale);
            HintLine(origin + new Vector2(20, 0) * scale, origin + new Vector2(20, 26) * scale, ink, 2 * scale);
            Vector2 center = origin + new Vector2(87, 27) * scale;
            foreach (var direction in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
            {
                Vector2 tip = center + direction * (18 * scale);
                Vector2 side = new Vector2(-direction.y, direction.x) * (4 * scale);
                HintLine(center, tip, ink, 2 * scale);
                HintLine(tip, tip - direction * (5 * scale) + side, ink, 2 * scale);
                HintLine(tip, tip - direction * (5 * scale) - side, ink, 2 * scale);
            }
            GUI.color = Color.white;
        }

        static void HintLine(Vector2 from, Vector2 to, Color color, float thickness)
        {
            Matrix4x4 matrix = GUI.matrix;
            Color previous = GUI.color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg, from);
            GUI.color = color;
            GUI.DrawTexture(new Rect(from.x, from.y - thickness * .5f, Vector2.Distance(from, to), thickness), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }
    }
}
