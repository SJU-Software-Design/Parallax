using System;

namespace GlassTrial
{
    /// <summary>Presentation state only; it does not consume gameplay input or control time.</summary>
    public sealed class HelpHudState
    {
        public enum Phase { Closed, Expanding, Open, Turning, Collapsing }
        public const int PageCount = 3;
        public const float ExpandSeconds = .42f;
        public const float TurnSeconds = .44f;
        public const float CollapseSeconds = .42f;
        public Phase Current { get; private set; } = Phase.Closed;
        public int Page { get; private set; }
        public int NextPage { get; private set; }
        public int Direction { get; private set; }
        public float Expansion { get; private set; }
        public float TurnProgress { get; private set; }
        public bool IsInteractive => Current == Phase.Open;
        public bool IsActive => Current != Phase.Closed;
        float elapsed, collapseFrom;

        public bool Open()
        {
            if (Current != Phase.Closed) return false;
            Page = NextPage = 0; Direction = 0; TurnProgress = 0; Expansion = 0;
            elapsed = 0; Current = Phase.Expanding; return true;
        }

        public bool Turn(int direction)
        {
            if (Current != Phase.Open || direction == 0) return false;
            Direction = direction > 0 ? 1 : -1;
            NextPage = (Page + Direction + PageCount) % PageCount;
            TurnProgress = 0; elapsed = 0; Current = Phase.Turning; return true;
        }

        public bool Close()
        {
            if (Current == Phase.Closed || Current == Phase.Collapsing) return false;
            // Discard the unfinished adjacent face before collapsing.
            NextPage = Page; Direction = 0; TurnProgress = 0;
            collapseFrom = Expansion; elapsed = 0; Current = Phase.Collapsing; return true;
        }

        public void Reset()
        {
            Current = Phase.Closed; Page = NextPage = Direction = 0;
            Expansion = TurnProgress = elapsed = collapseFrom = 0;
        }

        static float Unit(float value) => Math.Max(0, Math.Min(1, value));
        // CSS cubic-bezier(.22, 1, .36, 1), inverted on x before evaluating y.
        static float Smooth(float value)
        {
            float x = Unit(value), low = 0, high = 1;
            for (int i = 0; i < 18; i++)
            {
                float t = (low + high) * .5f, u = 1 - t;
                float curveX = 3 * u * u * t * .22f + 3 * u * t * t * .36f + t * t * t;
                if (curveX < x) low = t; else high = t;
            }
            float y = (low + high) * .5f, v = 1 - y;
            return 3 * v * v * y + 3 * v * y * y + y * y * y;
        }
        public void Tick(float unscaledDeltaTime)
        {
            if (float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime) || unscaledDeltaTime <= 0) return;
            elapsed += unscaledDeltaTime;
            switch (Current)
            {
                case Phase.Expanding:
                    Expansion = Smooth(elapsed / ExpandSeconds);
                    if (elapsed >= ExpandSeconds) { Expansion = 1; Current = Phase.Open; elapsed = 0; }
                    break;
                case Phase.Turning:
                    TurnProgress = Smooth(elapsed / TurnSeconds);
                    if (elapsed >= TurnSeconds) { Page = NextPage; Direction = 0; TurnProgress = 0; Current = Phase.Open; elapsed = 0; }
                    break;
                case Phase.Collapsing:
                    Expansion = collapseFrom * (1 - Smooth(elapsed / CollapseSeconds));
                    if (elapsed >= CollapseSeconds) Reset();
                    break;
            }
        }
    }
}
