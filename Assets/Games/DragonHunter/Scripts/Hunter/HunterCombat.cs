using System;
using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Serangan Hunter.
    /// Jarak dekat: combo pedang ringan 3 tahap + serangan berat (stamina).
    /// Jarak jauh: panah (cooldown + stamina). Setiap ayunan hanya mengenai naga satu kali.
    /// </summary>
    [RequireComponent(typeof(HunterController))]
    public class HunterCombat : MonoBehaviour
    {
        private enum Phase { None, Windup, Active, Recovery }

        private const float ComboChainDelay = 0.08f;

        [Serializable]
        private struct SwingData
        {
            public float damage;
            public float windup;
            public float active;
            public float recovery;
            public float reach;
            public float radius;
            public float staminaCost;
            public float swingFromYaw;
            public float swingToYaw;
            public float swingPitch;
        }

        [Header("Combo Ringan (LMB)")]
        [SerializeField] private SwingData[] lightCombo =
        {
            new SwingData { damage = 16f, windup = 0.10f, active = 0.12f, recovery = 0.24f, reach = 2.4f, radius = 1.3f, swingFromYaw = -75f, swingToYaw = 70f, swingPitch = 10f },
            new SwingData { damage = 20f, windup = 0.10f, active = 0.12f, recovery = 0.24f, reach = 2.4f, radius = 1.3f, swingFromYaw = 70f, swingToYaw = -75f, swingPitch = 20f },
            new SwingData { damage = 30f, windup = 0.16f, active = 0.14f, recovery = 0.40f, reach = 2.7f, radius = 1.5f, swingFromYaw = -90f, swingToYaw = 90f, swingPitch = 35f },
        };

        [Header("Serangan Berat (RMB)")]
        [SerializeField] private SwingData heavy =
            new SwingData { damage = 55f, windup = 0.45f, active = 0.14f, recovery = 0.50f, reach = 2.9f, radius = 1.7f, staminaCost = 20f, swingFromYaw = 0f, swingToYaw = 0f, swingPitch = 110f };

        [Header("Panah (E)")]
        [SerializeField] private float arrowDamage = 10f;
        [SerializeField] private float arrowSpeed = 34f;
        [SerializeField] private float arrowCooldown = 0.6f;
        [SerializeField] private float arrowStaminaCost = 8f;
        [SerializeField] private float arrowMaxRange = 45f;
        [SerializeField] private Material arrowMaterial;

        [Header("Soft Aim")]
        [SerializeField] private float softAimRange = 9f;

        [Header("Referensi")]
        [SerializeField] private Transform swordPivot;
        [SerializeField] private Transform arrowSpawn;
        [SerializeField] private DragonBoss target;
        [SerializeField] private Transform aimCamera;

        [Header("Efek")]
        [SerializeField] private TrailRenderer swordTrail;
        [SerializeField] private Material arrowTrailMaterial;

        private HunterController hunter;
        private Phase phase = Phase.None;
        private float phaseEndTime;
        private SwingData current;
        private bool currentIsHeavy;
        private int comboIndex;
        private bool queuedLight;
        private bool hitConsumed;
        private float nextArrowTime;
        private Quaternion swordRestRotation;
        private readonly Collider[] overlapBuffer = new Collider[16];

        public bool IsAttacking => phase != Phase.None;
        public bool InRecovery => phase == Phase.Recovery;
        public int ComboIndex => comboIndex;

        /// <summary>(damage, titik kena, berat?) setiap kali serangan pedang Hunter mengenai naga.</summary>
        public event Action<float, Vector3, bool> HitLanded;

        /// <summary>(berat?, tahap combo) saat ayunan masuk fase aktif.</summary>
        public event Action<bool, int> SwingStarted;
        public event Action ArrowShot;
        public event Action<Vector3> ArrowLanded;

        private void Awake()
        {
            hunter = GetComponent<HunterController>();
            if (swordPivot != null) swordRestRotation = swordPivot.localRotation;
        }

        private void OnEnable()
        {
            if (hunter == null) hunter = GetComponent<HunterController>();
            hunter.Damaged += HandleHunterDamaged;
        }

        private void OnDisable()
        {
            if (hunter != null) hunter.Damaged -= HandleHunterDamaged;
        }

        public void SetTarget(DragonBoss dragon) => target = dragon;

        // ------------------------------------------------------------------ API input
        public bool TryLightAttack()
        {
            if (!CanAct()) return false;

            if (phase == Phase.Active || phase == Phase.Recovery)
            {
                // Buffer input combo: tahap berikutnya dimulai segera setelah ayunan ini selesai.
                if (!currentIsHeavy && comboIndex < lightCombo.Length - 1)
                {
                    queuedLight = true;
                    // Combo terasa responsif: recovery dipotong begitu tahap berikutnya sudah di-buffer.
                    if (phase == Phase.Recovery) phaseEndTime = Mathf.Min(phaseEndTime, Time.time + ComboChainDelay);
                    return true;
                }
                return false;
            }
            if (phase == Phase.Windup) return false;

            comboIndex = 0;
            StartSwing(lightCombo[0], false);
            return true;
        }

        public bool TryHeavyAttack()
        {
            if (!CanAct() || phase == Phase.Windup || phase == Phase.Active) return false;
            if (!hunter.Stamina.TrySpend(heavy.staminaCost, Time.time)) return false;

            queuedLight = false;
            StartSwing(heavy, true);
            return true;
        }

        public bool TryShootArrow()
        {
            if (!CanAct() || IsAttacking || Time.time < nextArrowTime) return false;
            if (!hunter.Stamina.TrySpend(arrowStaminaCost, Time.time)) return false;

            nextArrowTime = Time.time + arrowCooldown;
            Vector3 origin = arrowSpawn != null ? arrowSpawn.position : transform.position + Vector3.up * 1.3f + transform.forward * 0.8f;
            Vector3 direction = ResolveArrowDirection(origin);
            hunter.FaceTowards(transform.position + direction);
            HunterArrow.Spawn(origin, direction, arrowSpeed, arrowDamage, arrowMaxRange / arrowSpeed, arrowMaterial, arrowTrailMaterial, this);
            ArrowShot?.Invoke();
            return true;
        }

        /// <summary>Membatalkan ayunan (dipakai saat dodge-cancel atau terkena serangan).</summary>
        public void CancelAttack()
        {
            phase = Phase.None;
            queuedLight = false;
            comboIndex = 0;
            hunter.CombatMoveMultiplier = 1f;
            hunter.FacingLocked = false;
            if (swordPivot != null) swordPivot.localRotation = swordRestRotation;
            SetTrail(false);
        }

        internal void NotifyArrowHit(float damage, Vector3 point)
        {
            ArrowLanded?.Invoke(point);
        }

        private void SetTrail(bool emitting)
        {
            if (swordTrail == null) return;
            if (emitting && !swordTrail.emitting) swordTrail.Clear();
            swordTrail.emitting = emitting;
        }

        // ------------------------------------------------------------------ Loop
        private void Update()
        {
            if (phase == Phase.None) return;

            if (hunter.IsDead || hunter.IsStunned || hunter.IsDodging)
            {
                CancelAttack();
                return;
            }

            float now = Time.time;
            AnimateSword(now);

            if (now < phaseEndTime) return;

            switch (phase)
            {
                case Phase.Windup:
                    phase = Phase.Active;
                    phaseEndTime = now + current.active;
                    hunter.CombatMoveMultiplier = 0.1f;
                    SetTrail(true);
                    SwingStarted?.Invoke(currentIsHeavy, comboIndex);
                    ApplyMeleeHit();
                    break;

                case Phase.Active:
                    phase = Phase.Recovery;
                    SetTrail(false);
                    phaseEndTime = now + (queuedLight ? Mathf.Min(current.recovery, ComboChainDelay) : current.recovery);
                    hunter.CombatMoveMultiplier = 0.4f;
                    break;

                case Phase.Recovery:
                    if (queuedLight && comboIndex < lightCombo.Length - 1)
                    {
                        queuedLight = false;
                        comboIndex++;
                        StartSwing(lightCombo[comboIndex], false);
                    }
                    else
                    {
                        CancelAttack();
                    }
                    break;
            }
        }

        private bool CanAct()
        {
            return hunter != null && hunter.ControlsEnabled && !hunter.IsDead && !hunter.IsDodging
                   && !hunter.IsDrinking && !hunter.IsStunned;
        }

        private void StartSwing(SwingData data, bool isHeavy)
        {
            current = data;
            currentIsHeavy = isHeavy;
            hitConsumed = false;
            phase = Phase.Windup;
            phaseEndTime = Time.time + data.windup;
            hunter.CombatMoveMultiplier = 0.25f;

            if (target != null && !target.IsDead)
            {
                float distance = AttackGeometry.Flatten(target.transform.position - transform.position).magnitude;
                if (distance <= softAimRange + target.BodyRadius)
                {
                    hunter.FaceTowards(target.transform.position);
                    hunter.FacingLocked = true;
                }
            }
        }

        private void ApplyMeleeHit()
        {
            if (hitConsumed) return;
            hitConsumed = true;

            Vector3 center = transform.position + Vector3.up * 1.0f + transform.forward * (current.reach * 0.6f);
            int count = Physics.OverlapSphereNonAlloc(center, current.radius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);

            DragonHurtbox best = null;
            Vector3 bestPoint = center;
            for (int i = 0; i < count; i++)
            {
                var box = overlapBuffer[i].GetComponent<DragonHurtbox>();
                if (box == null || box.Boss == null || box.Boss.IsDead) continue;
                if (best == null || box.DamageMultiplier > best.DamageMultiplier)
                {
                    best = box;
                    bestPoint = overlapBuffer[i].ClosestPoint(center);
                }
            }

            if (best == null) return;

            float damage = current.damage * best.DamageMultiplier;
            float dealt = best.Boss.ReceiveHit(damage, bestPoint, currentIsHeavy);
            if (dealt > 0f) HitLanded?.Invoke(dealt, bestPoint, currentIsHeavy);
        }

        private Vector3 ResolveArrowDirection(Vector3 origin)
        {
            if (target != null && !target.IsDead)
            {
                Vector3 toTarget = target.AimPoint - origin;
                Vector3 viewForward = aimCamera != null ? aimCamera.forward : transform.forward;
                // Bidik otomatis bila naga kira-kira berada di depan pandangan.
                if (toTarget.magnitude <= arrowMaxRange && Vector3.Angle(AttackGeometry.Flatten(viewForward), AttackGeometry.Flatten(toTarget)) < 40f)
                    return toTarget.normalized;
            }

            Vector3 forward = aimCamera != null ? AttackGeometry.Flatten(aimCamera.forward) : transform.forward;
            return forward.sqrMagnitude > 0.01f ? forward.normalized : transform.forward;
        }

        private void AnimateSword(float now)
        {
            if (swordPivot == null) return;

            float duration;
            float t;
            switch (phase)
            {
                case Phase.Windup:
                    duration = Mathf.Max(0.01f, current.windup);
                    t = 1f - (phaseEndTime - now) / duration;
                    swordPivot.localRotation = swordRestRotation * Quaternion.Euler(-current.swingPitch * 0.5f * t, current.swingFromYaw * t, 0f);
                    break;
                case Phase.Active:
                    duration = Mathf.Max(0.01f, current.active);
                    t = Mathf.Clamp01(1f - (phaseEndTime - now) / duration);
                    float yaw = Mathf.Lerp(current.swingFromYaw, current.swingToYaw, t);
                    float pitch = Mathf.Lerp(-current.swingPitch * 0.5f, current.swingPitch, t);
                    swordPivot.localRotation = swordRestRotation * Quaternion.Euler(pitch, yaw, 0f);
                    break;
                case Phase.Recovery:
                    duration = Mathf.Max(0.01f, current.recovery);
                    t = Mathf.Clamp01(1f - (phaseEndTime - now) / duration);
                    Quaternion end = swordRestRotation * Quaternion.Euler(current.swingPitch, current.swingToYaw, 0f);
                    swordPivot.localRotation = Quaternion.Slerp(end, swordRestRotation, t);
                    break;
            }
        }

        private void HandleHunterDamaged(float _) => CancelAttack();

        private void OnDrawGizmosSelected()
        {
            if (lightCombo == null || lightCombo.Length == 0) return;
            Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up + transform.forward * (lightCombo[0].reach * 0.6f), lightCombo[0].radius);
        }
    }
}
