using System;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Wadah HP murni (tanpa dependensi Unity) agar aturan damage/heal bisa diuji unit.
    /// </summary>
    public sealed class HealthPool
    {
        public float Max { get; private set; }
        public float Current { get; private set; }

        public float Fraction => Max <= 0f ? 0f : Current / Max;
        public bool IsDepleted => Current <= 0f;

        /// <summary>Dipicu dengan (current, max) setiap nilai berubah.</summary>
        public event Action<float, float> Changed;

        /// <summary>Dipicu sekali saat HP mencapai nol.</summary>
        public event Action Depleted;

        public HealthPool(float max)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max), "Max HP must be positive.");
            Max = max;
            Current = max;
        }

        /// <summary>Memberi damage dan mengembalikan jumlah HP yang benar-benar berkurang.</summary>
        public float Damage(float amount)
        {
            if (amount <= 0f || IsDepleted) return 0f;

            float before = Current;
            Current = Math.Max(0f, Current - amount);
            float dealt = before - Current;

            Changed?.Invoke(Current, Max);
            if (IsDepleted) Depleted?.Invoke();
            return dealt;
        }

        /// <summary>Memulihkan HP (tidak melebihi Max, tidak menghidupkan pool yang sudah habis) dan mengembalikan jumlah yang dipulihkan.</summary>
        public float Heal(float amount)
        {
            if (amount <= 0f || IsDepleted) return 0f;

            float before = Current;
            Current = Math.Min(Max, Current + amount);
            float healed = Current - before;

            if (healed > 0f) Changed?.Invoke(Current, Max);
            return healed;
        }

        public void ResetToFull()
        {
            Current = Max;
            Changed?.Invoke(Current, Max);
        }
    }
}
