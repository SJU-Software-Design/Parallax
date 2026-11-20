using UnityEngine;

namespace ShadeLink.Tutorial
{
    // This is the live drawing data, also readable by capture-free validation.
    public readonly struct TutorialSunlightGaugeSnapshot
    {
        public readonly bool Visible, Safe;
        public readonly float Exposure;
        public readonly Rect Panel, Label, Track, Fill;
        public readonly int FontSize;
        public string Status => Safe ? Exposure > .001f ? "그늘 · 노출 감소 중" : "그늘 · 안전" : "빛 노출 · 그늘로 이동";

        public TutorialSunlightGaugeSnapshot(bool visible, bool safe, float exposure,
            Rect panel, Rect label, Rect track, int fontSize)
        {
            Visible = visible; Safe = safe; Exposure = exposure;
            Panel = panel; Label = label; Track = track; FontSize = fontSize;
            Fill = new Rect(track.x, track.y, track.width * exposure, track.height);
        }
    }

    public sealed partial class TutorialGame
    {
        public TutorialSunlightGaugeSnapshot GetSunlightExposureGauge(float width, float height) =>
            GetSunlightExposureGauge(width, height, global::GameShellSettingsRuntime.TextScale);

        public TutorialSunlightGaugeSnapshot GetSunlightExposureGauge(float width, float height, float textScale)
        {
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            float scale = Mathf.Clamp(Mathf.Min(width / 1600f, height / 900f), .65f, 1.25f);
            float margin = Mathf.Min(20 * scale, width * .05f);
            float padding = Mathf.Min(14 * scale, width * .04f);
            float panelWidth = Mathf.Min(380 * scale, width - margin * 2);
            int fontSize = Mathf.RoundToInt(18 * scale * Mathf.Clamp(textScale, .9f, 1.2f));
            float labelHeight = Mathf.Max(30 * scale, fontSize * 1.5f);
            float gap = 8 * scale, barHeight = Mathf.Max(7, 9 * scale);
            var panel = new Rect(width - margin - panelWidth, margin, panelWidth,
                padding * 2 + labelHeight + gap + barHeight);
            var label = new Rect(panel.x + padding, panel.y + padding, panel.width - padding * 2, labelHeight);
            var track = new Rect(label.x, label.yMax + gap, label.width, barHeight);
            bool visible = inSunlightRoom && Controller != null && Camera != null && !sunlightComplete && !elevatorDeparted &&
                !(MuseumElevator != null && MuseumElevator.ContainsPlayer && !paused);
            bool safe = !visible || SunlightSafe(Controller.transform.position);
            return new TutorialSunlightGaugeSnapshot(visible, safe, Mathf.Clamp01(sunlightExposure), panel, label, track, fontSize);
        }

        void DrawSunlightGauge(float width, float height)
        {
            var gauge = GetSunlightExposureGauge(width, height);
            if (!gauge.Visible) return;
            Color previous = GUI.color;
            GUI.color = new Color(.035f, .065f, .08f, .9f);
            GUI.DrawTexture(gauge.Panel, Texture2D.whiteTexture);
            GUI.color = new Color(.14f, .22f, .25f);
            GUI.DrawTexture(gauge.Track, Texture2D.whiteTexture);
            GUI.color = gauge.Safe ? Mint : new Color(1f, .56f, .25f);
            if (gauge.Fill.width > 0) GUI.DrawTexture(gauge.Fill, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var label = new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = gauge.FontSize,
                alignment = TextAnchor.MiddleLeft, wordWrap = false
            };
            label.normal.textColor = Color.white;
            GUI.Label(gauge.Label, gauge.Status, label);
            GUI.color = previous;
        }
    }
}
