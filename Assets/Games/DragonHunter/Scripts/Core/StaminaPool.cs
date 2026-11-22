using System;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Stamina dengan jeda regenerasi setelah dipakai (gaya Monster Hunter).
    /// Waktu diberikan eksplisit agar aturannya deterministik saat diuji.
    /// </summary>
    public sealed class StaminaPool
    {
        public float Max { get; }
        public float Current { get; private set; }
        public float RegenPerSecond { get; }
        public float RegenDelay { get; }

        public float Fraction => Max <= 0f ? 0f : Current / Max;

        private float regenBlockedUntil;

        public StaminaPool(float max, float regenPerSecond, float regenDelay)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            Current = max;
            RegenPerSecond = Math.Max(0f, regenPerSecond);
            RegenDelay = Math.Max(0f, regenDelay);
            regenBlockedUntil = float.NegativeInfinity;
        }

        public bool CanAfford(float cost) => Current >= cost;

        /// <summary>Memakai stamina hanya jika biaya penuh tersedia.</summary>
        public bool TrySpend(float cost, float now)
        {
            if (cost < 0f) cost = 0f;
            if (Current < cost) return false;

            Current -= cost;
            if (cost > 0f) regenBlockedUntil = now + RegenDelay;
            return true;
        }

        /// <summary>Pengurangan kontinu (mis. sprint). Mengembalikan false saat habis.</summary>
        public bool Drain(float amount, float now)
        {
            if (amount <= 0f) return Current > 0f;
            Current = Math.Max(0f, Current - amount);
            regenBlockedUntil = now + RegenDelay;
            return Current > 0f;
        }

        public void Tick(float deltaTime, float now)
        {
            if (deltaTime <= 0f || now < regenBlockedUntil || Current >= Max) return;
            Current = Math.Min(Max, Current + RegenPerSecond * deltaTime);
        }
    }
}
