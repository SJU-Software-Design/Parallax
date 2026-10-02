using UnityEngine;

namespace ShadeLink
{
    public enum KeyStage { Floor, Carrying, Resizing, Inserting, Turning, Opening, Unlocked }

    public sealed class KeyState
    {
        public const float InitialScale = .65f, TargetScale = 1.2f, Tolerance = .08f;
        public const float MinScale = .35f, MaxScale = 2.1f;
        public const float InsertSeconds = .65f, TurnSeconds = .7f, OpenSeconds = .85f;
        public static readonly Vector2 Start = new Vector2(-4.9f, 12.65f);
        public KeyStage stage;
        public Vector2 position = Start;
        public float scale = InitialScale, clock;
        public int clicks;
        public bool Held => stage == KeyStage.Carrying || stage == KeyStage.Resizing;
        public bool Busy => stage == KeyStage.Inserting || stage == KeyStage.Turning || stage == KeyStage.Opening;
        public float Ratio => scale / TargetScale;
        public bool Fits => Mathf.Abs(Ratio - 1) <= Tolerance + .00001f;
        public Bounds Bounds => new Bounds(new Vector3(position.x, .045f * scale + .035f, position.y), new Vector3(.62f, .09f, 1.08f) * scale);
        public bool Insert()
        {
            if (stage != KeyStage.Carrying || !Fits) return false;
            stage = KeyStage.Inserting; clock = 0; return true;
        }
        // Use stage durations, never a delayed callback, so pause/reset are exact.
        public bool Advance(float seconds)
        {
            bool clicked = false;
            if (!Busy) return false;
            clock += seconds;
            while (Busy)
            {
                float duration = stage == KeyStage.Inserting ? InsertSeconds : stage == KeyStage.Turning ? TurnSeconds : OpenSeconds;
                if (clock < duration) break;
                clock -= duration;
                if (stage == KeyStage.Inserting) stage = KeyStage.Turning;
                else if (stage == KeyStage.Turning) { stage = KeyStage.Opening; clicks++; clicked = true; }
                else { stage = KeyStage.Unlocked; clock = 0; }
            }
            return clicked;
        }
    }

    // The carried size is immutable. Only the explicit floor-projection mode
    // changes size, preserving scale / distance just like the original blocks.
    public sealed class KeyPuzzle
    {
        public static readonly Vector3 Socket = new Vector3(5.05f, 1.22f, -15.49f);
        public static readonly Bounds Lock = new Bounds(Socket + new Vector3(0, .08f, -.04f), new Vector3(.7f, .94f, .22f));
        public readonly ShadeGame game;
        public readonly KeyPuzzleVisuals visuals;
        public KeyState state = new KeyState();
        public bool blocked, limited;
        public float ratio, yawOffset, pitchOffset;
        public Vector3 insertionFrom;
        public Quaternion insertionRotation;
        public Vector3 viewFrom, unlockView;
        public Quaternion viewRotation;
        public bool Holding => state.Held;
        public bool Busy => state.Busy;
        public bool MovementLocked => state.stage == KeyStage.Resizing || Busy;
        public KeyPuzzle(ShadeGame owner, Font font)
        {
            game = owner;
            visuals = new GameObject("Perspective key and ground door").AddComponent<KeyPuzzleVisuals>();
            visuals.Build(this, font); visuals.Sync();
        }
        public void Reset() { state = new KeyState(); blocked = limited = false; visuals.StopSound(); visuals.Sync(); }
        public float ObstacleDistance()
        {
            var s = game.state; float nearest = (1f/0f);
            foreach (Bounds b in Rules.Solids(s.blocks, null, true))
                if (Rules.RayBox(s.player.Eye, Rules.Look(s.yaw, s.pitch), b, out float hit)) nearest = Mathf.Min(nearest, hit);
            return nearest;
        }
        public bool AimKey()
        {
            if (state.stage != KeyStage.Floor) return false;
            Bounds hitbox = state.Bounds; hitbox.Expand(new Vector3(.08f, .13f, .08f));
            var s = game.state;
            return Rules.RayBox(s.player.Eye, Rules.Look(s.yaw, s.pitch), hitbox, out float hit) && hit < 5 && hit < ObstacleDistance();
        }
        public bool AimLock()
        {
            var s = game.state;
            return Rules.RayBox(s.player.Eye, Rules.Look(s.yaw, s.pitch), Lock, out float hit) && hit < 2.6f && hit < ObstacleDistance();
        }
        public bool BlocksPlacement(Block block) => (state.stage == KeyStage.Floor || state.stage == KeyStage.Resizing) && Rules.Overlap(block.Bounds, state.Bounds, .02f);
        public bool Valid(Vector2 position, float scale)
        {
            Bounds b = new Bounds(new Vector3(position.x, .08f, position.y), new Vector3(.62f * scale, .16f, 1.08f * scale));
            if (b.min.x < -6.94f || b.max.x > 6.94f || b.min.z < -15.7f || b.max.z > 15.94f) return false;
            foreach (Bounds other in Rules.Solids(game.state.blocks, null, true)) if (Rules.Overlap(b, other, .04f)) return false;
            return true;
        }
        bool VisibleFloor(Vector2 point, float size)
        {
            Vector3 delta = new Vector3(point.x, .035f + .045f * size, point.y) - game.state.player.Eye;
            foreach (Bounds b in Rules.Solids(game.state.blocks, null, true))
                if (Rules.RayBox(game.state.player.Eye, delta.normalized, b, out float hit) && hit < delta.magnitude) return false;
            return true;
        }
        public void ToggleResize()
        {
            if (state.stage == KeyStage.Resizing)
            {
                state.stage = KeyStage.Carrying; blocked = limited = false;
                game.Tell("열쇠 크기를 고정했습니다 · 들고 이동하여 자물쇠에 가져가세요."); return;
            }
            if (state.stage != KeyStage.Carrying || game.state.held != null) return;
            if (!game.state.player.grounded) { game.Tell("착지한 뒤 열쇠 크기를 조절하세요."); return; }
            var s = game.state;
            // Find a clear initial projection so entering resize never loses the key.
            bool found = false; Vector2 place = default;
            for (int i = 0; i < 96 && !found; i++)
            {
                int n = i % 24;
                float angle = s.yaw + (n / 2) * .26f * (n % 2 == 0 ? 1 : -1);
                place = s.player.Point + new Vector2(-Mathf.Sin(angle), -Mathf.Cos(angle)) * (2.2f + (i / 24) * 1.2f);
                found = Valid(place, state.scale) && VisibleFloor(place, state.scale);
            }
            if (!found) { game.Tell("빈 바닥이 있는 그늘에서 F를 눌러주세요."); return; }
            Vector2 delta = place - s.player.Point;
            state.position = place; ratio = state.scale / delta.magnitude;
            // Center the floor preview so looking at farther/nearer floor is
            // literal, independent of where the player looked while carrying.
            s.yaw = Mathf.Atan2(-delta.x, -delta.y);
            s.pitch = Mathf.Atan2(.035f + .045f * state.scale - s.player.Eye.y, delta.magnitude);
            yawOffset = pitchOffset = 0;
            state.stage = KeyStage.Resizing; blocked = limited = false;
            game.Tell("먼 바닥을 보면 커지고 가까운 바닥을 보면 작아집니다 · F로 크기 고정 후 운반", 6);
        }
        public bool Drop()
        {
            if (!Holding) return false;
            if (state.stage == KeyStage.Resizing)
            { state.stage = KeyStage.Floor; blocked = limited = false; return true; }
            var s = game.state; Vector3 look = Rules.Look(s.yaw, s.pitch);
            float distance = look.y < -.03f ? (.035f - s.player.Eye.y) / look.y : 2.2f;
            // Use a horizontal anchor and keep the real world size when putting down.
            float horizontal = Mathf.Clamp(distance * Mathf.Cos(s.pitch), 1.15f, 3);
            Vector2 point = s.player.Point + new Vector2(-Mathf.Sin(s.yaw), -Mathf.Cos(s.yaw)) * horizontal;
            Vector3 d = new Vector3(point.x, .035f, point.y) - s.player.Eye;
            foreach (Bounds b in Rules.Solids(s.blocks, null, true))
                if (Rules.RayBox(s.player.Eye, d.normalized, b, out float hit) && hit < d.magnitude)
                { game.Tell("사물 너머로는 놓을 수 없습니다. 가까운 빈 바닥을 바라보세요."); return false; }
            if (!Valid(point, state.scale)) { game.Tell("열쇠를 놓을 공간이 부족합니다. 빈 바닥을 바라보세요."); return false; }
            state.position = point; state.stage = KeyStage.Floor; game.Tell("열쇠를 내려놓았습니다 · 다시 클릭해 집을 수 있습니다."); return true;
        }
        public bool TryInsert()
        {
            if (Busy || state.stage == KeyStage.Unlocked) return false;
            if (!AimLock()) { game.Tell("출구 자물쇠 가까이에서 열쇠 구멍을 조준하세요."); return false; }
            if (state.stage != KeyStage.Carrying) { game.Tell("크기를 고정한 열쇠를 들고 자물쇠에 넣어주세요."); return false; }
            if (!state.Fits)
            {
                game.Tell(state.Ratio < 1 ? "열쇠가 너무 작습니다 · F 원근 조절 → 먼 바닥을 보고 키우세요." : "열쇠가 너무 큽니다 · F 원근 조절 → 가까운 바닥을 보고 줄이세요.", 5);
                visuals.Reject(); return false;
            }
            if (!game.state.player.grounded) { game.Tell("바닥에 착지한 뒤 열쇠를 넣어주세요."); return false; }
            visuals.Sync(); insertionFrom = visuals.key.position; insertionRotation = visuals.key.rotation;
            var camera = game.visuals.view.transform; viewFrom = camera.position; viewRotation = camera.rotation;
            unlockView = viewFrom;
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 candidate = Socket + new Vector3(side * 1.5f, .60f, .90f);
                Vector3 delta = Socket - candidate; bool clear = true;
                foreach (Bounds b in Rules.Solids(game.state.blocks, null, true))
                    if (Rules.RayBox(candidate, delta.normalized, b, out float hit) && hit < delta.magnitude) clear = false;
                if (clear) { unlockView = ShadeVisuals.ToUnity(candidate); break; }
            }
            if (!state.Insert()) return false;
            game.Tell("열쇠를 꽂고 돌리는 중…", 3); return true;
        }
        public bool Interact()
        {
            if (Busy) return true;
            if (state.stage == KeyStage.Resizing) { Drop(); return true; }
            if (state.stage == KeyStage.Carrying)
            {
                if (AimLock()) TryInsert(); else Drop();
                return true;
            }
            if (AimKey())
            {
                state.stage = KeyStage.Carrying;
                game.Tell("열쇠를 들었습니다 · WASD 운반 · F 원근 크기 조절 · 클릭 내려놓기", 6); return true;
            }
            if (AimLock() && state.stage != KeyStage.Unlocked) { game.Tell("잠긴 출구입니다 · 크기가 맞는 열쇠가 필요합니다."); return true; }
            return false;
        }
        public void Tick(float dt)
        {
            if (state.stage == KeyStage.Resizing)
            {
                var s = game.state;
                float denominator = .045f * ratio - Mathf.Tan(Mathf.Clamp(s.pitch + pitchOffset, -1.48f, 1.48f));
                float request = denominator > .0001f ? (s.player.Eye.y - .035f) / denominator : 1000;
                float distance = Mathf.Clamp(request, KeyState.MinScale / ratio, KeyState.MaxScale / ratio);
                float size = distance * ratio;
                Vector2 point = s.player.Point + new Vector2(-Mathf.Sin(s.yaw + yawOffset), -Mathf.Cos(s.yaw + yawOffset)) * distance;
                limited = Mathf.Abs(distance - request) > .01f;
                blocked = !Valid(point, size) || !VisibleFloor(point, size);
                if (!blocked) { state.position = point; state.scale = size; }
            }
            if (state.Advance(dt)) { visuals.Click(); game.Tell("딸깍! 잠금이 풀렸습니다.", 4); }
            game.state.keyUnlocked = state.stage == KeyStage.Unlocked;
        }
        public void Respawn()
        {
            if (state.stage == KeyStage.Resizing) state.stage = KeyStage.Carrying;
            blocked = limited = false;
        }
        public string Prompt()
        {
            if (Busy) return state.stage == KeyStage.Inserting ? "열쇠 삽입 중" : state.stage == KeyStage.Turning ? "열쇠를 돌리는 중" : "딸깍 · 자물쇠와 문이 열립니다";
            if (state.stage == KeyStage.Unlocked) return "잠금 해제 완료 · 열린 출구로 걸어가세요";
            if (state.stage == KeyStage.Resizing) return blocked ? "배치할 수 없는 위치 · 마지막 크기 유지 · F 운반" : "먼 바닥 → 크게  /  가까운 바닥 → 작게  ·  F 크기 고정·운반";
            if (state.stage == KeyStage.Carrying) return AimLock() ? "E 또는 클릭 · 열쇠 꽂기" : "열쇠 운반 중 · F 원근 조절 · 클릭 내려놓기";
            if (AimKey()) return "클릭 · 열쇠 집기";
            if (AimLock()) return "잠긴 자물쇠 · 시작 그늘의 열쇠가 필요합니다";
            return null;
        }
    }
}
