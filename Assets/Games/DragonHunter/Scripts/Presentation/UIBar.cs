using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Bar status datar berbasis anchor (tidak butuh sprite bawaan Unity), dengan
    /// "jejak damage" yang turun perlahan setelah jeda singkat.
    /// </summary>
    public class UIBar : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform trail;
        [SerializeField] private float trailDelay = 0.45f;
        [SerializeField] private float trailSpeed = 0.9f;

        private float value = 1f;
        private float trailValue = 1f;
        private float trailHoldUntil;

        public float Value => value;

        public void Configure(RectTransform fillRect, RectTransform trailRect)
        {
            fill = fillRect;
            trail = trailRect;
            Apply(fill, value);
            Apply(trail, trailValue);
        }

        public void SetValue(float normalized, bool instant = false)
        {
            normalized = Mathf.Clamp01(normalized);
            if (Mathf.Approximately(normalized, value) && !instant) return;

            if (normalized < value) trailHoldUntil = Time.unscaledTime + trailDelay;
            value = normalized;
            Apply(fill, value);

            if (instant || trailValue < value)
            {
                trailValue = value;
                Apply(trail, trailValue);
            }
        }

        private void Update()
        {
            if (trail == null || trailValue <= value || Time.unscaledTime < trailHoldUntil) return;
            trailValue = Mathf.MoveTowards(trailValue, value, trailSpeed * Time.unscaledDeltaTime);
            Apply(trail, trailValue);
        }

        private static void Apply(RectTransform rect, float v)
        {
            if (rect == null) return;
            Vector2 max = rect.anchorMax;
            if (Mathf.Approximately(max.x, v)) return;
            rect.anchorMax = new Vector2(v, max.y);
        }
    }
}
