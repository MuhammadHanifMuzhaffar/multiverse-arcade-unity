using System;
using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Data satu jurus naga: bentuk area, durasi tiap fase, damage, dan syarat pemilihan.
    /// Disimpan sebagai data (bukan kode) agar mudah di-tuning dari Inspector.
    /// </summary>
    [Serializable]
    public sealed class DragonAttackDefinition
    {
        public string id = "attack";
        public string displayName = "Attack";

        [Header("Area")]
        public AttackShape shape = AttackShape.Cone;
        [Min(0.1f)] public float range = 5f;
        [Range(1f, 360f)] public float angle = 70f;

        [Header("Damage")]
        [Min(0f)] public float damage = 20f;
        [Tooltip("0 = satu kali kena. > 0 = damage berulang tiap interval selama fase aktif.")]
        [Min(0f)] public float tickInterval = 0f;

        [Header("Durasi Fase (detik, sebelum tempo Enraged)")]
        [Min(0.05f)] public float windup = 0.8f;
        [Min(0.05f)] public float active = 0.25f;
        [Min(0f)] public float recovery = 0.8f;
        [Tooltip("> 0 = naga masuk state Vulnerable selama durasi ini setelah jurus selesai.")]
        [Min(0f)] public float vulnerableAfter = 0f;

        [Header("Gerak Saat Aktif")]
        [Tooltip("Jarak maju naga selama fase aktif (lunge/terjangan).")]
        [Min(0f)] public float dashDistance = 0f;
        [Tooltip("> 0 = area kena berupa lingkaran yang ikut bergerak bersama naga (terjangan).")]
        [Min(0f)] public float dashHitRadius = 0f;

        [Header("Pemilihan")]
        [Min(0f)] public float minDistance = 0f;
        [Min(0f)] public float maxDistance = 6f;
        public bool enragedOnly = false;
        [Tooltip("Hanya dipilih bila Hunter berada di belakang/samping naga.")]
        public bool prefersFlank = false;
        [Min(0.01f)] public float weight = 1f;
        [Min(0f)] public float cooldown = 0f;

        public bool IsUsable(float distance, bool enraged, bool hunterBehind)
        {
            if (enragedOnly && !enraged) return false;
            if (distance < minDistance || distance > maxDistance) return false;
            if (prefersFlank && !hunterBehind) return false;
            return true;
        }

        public static DragonAttackDefinition Create(string id, string name, AttackShape shape, float range, float angle,
                                                    float damage, float windup, float active, float recovery,
                                                    float minDist, float maxDist)
        {
            return new DragonAttackDefinition
            {
                id = id, displayName = name, shape = shape, range = range, angle = angle, damage = damage,
                windup = windup, active = active, recovery = recovery, minDistance = minDist, maxDistance = maxDist,
            };
        }
    }
}
