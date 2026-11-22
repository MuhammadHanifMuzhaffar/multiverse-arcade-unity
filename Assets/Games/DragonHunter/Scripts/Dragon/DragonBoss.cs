using System;
using System.Collections.Generic;
using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// AI boss naga "Emberscale".
    /// Idle: mengejar dan memilih jurus berdasarkan jarak/posisi Hunter.
    /// TelegraphAttack: area serangan ditandai di lantai, naga masih membidik di awal windup.
    /// Attacking: damage diberikan selama fase aktif. Jurus berat diikuti jendela Vulnerable.
    /// Enraged (HP &lt;= 50%): raungan, lalu tempo lebih cepat, gerak lebih cepat, jurus Inferno Nova terbuka.
    /// Timing state diatur <see cref="DragonStateMachine"/>.
    /// </summary>
    public class DragonBoss : MonoBehaviour
    {
        [Header("Statistik")]
        [SerializeField] private float maxHP = 1000f;
        [SerializeField] private float vulnerableDamageMultiplier = 1.5f;
        [SerializeField] private float enragedDamageMultiplier = 1.15f;
        [SerializeField] private float heavyStaggerMultiplier = 1.4f;

        [Header("Gerak")]
        [SerializeField] private float moveSpeed = 3.4f;
        [SerializeField] private float enragedSpeedMultiplier = 1.3f;
        [SerializeField] private float turnSpeedDegrees = 110f;
        [SerializeField] private float preferredDistance = 4.2f;
        [SerializeField] private float arenaRadius = 15f;
        [SerializeField] private float bodyRadius = 2.2f;
        [SerializeField] private float coneOriginOffset = 1.6f;

        [Header("AI")]
        [SerializeField, Range(0.1f, 0.9f)] private float enrageThreshold = 0.5f;
        [SerializeField] private float enrageRoarDuration = 1.8f;
        [SerializeField, Range(0.4f, 1f)] private float enragedTempo = 0.75f;
        [SerializeField] private float staggerThreshold = 200f;
        [SerializeField] private float staggerDuration = 3.2f;
        [SerializeField] private float initialCooldown = 2.0f;
        [SerializeField, Range(0f, 1f)] private float telegraphTrackingPortion = 0.6f;
        [SerializeField] private float hunterHitRadius = 0.4f;
        [SerializeField] private List<DragonAttackDefinition> attacks = new List<DragonAttackDefinition>();

        [Header("Referensi")]
        [SerializeField] private HunterController hunter;
        [SerializeField] private AttackTelegraph telegraph;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;
        [SerializeField] private Transform aimPoint;
        [SerializeField] private ParticleSystem fireParticles;
        [SerializeField] private HitFlash hitFlash;
        [SerializeField] private Color enragedTint = new Color(1f, 0.35f, 0.12f, 1f);

        private DragonStateMachine brain;
        private readonly Dictionary<string, float> lastUsedTime = new Dictionary<string, float>();
        private readonly List<DragonAttackDefinition> candidates = new List<DragonAttackDefinition>();
        private string lastAttackId;
        private bool currentAttackLanded;
        private float nextTickTime;
        private Vector3 shakeOffset;

        public HealthPool Health { get; private set; }
        public DragonStateMachine Brain => brain;
        public DragonState State => brain != null ? brain.State : DragonState.Idle;
        public bool IsDead => Health != null && Health.IsDepleted;
        public bool IsEnraged => brain != null && brain.IsEnraged;
        public float BodyRadius => bodyRadius;
        public DragonAttackDefinition CurrentAttack => brain?.CurrentAttack;
        public IReadOnlyList<DragonAttackDefinition> Attacks => attacks;
        public Vector3 AimPoint => aimPoint != null ? aimPoint.position : transform.position + Vector3.up * 2f;

        /// <summary>(damage diterima, terkena saat Vulnerable?)</summary>
        public event Action<float, bool> Damaged;
        public event Action Died;
        public event Action<DragonState> StateChanged;
        public event Action Staggered;
        public event Action EnrageStarted;
        public event Action<DragonAttackDefinition> AttackTelegraphed;

        public void SetHunter(HunterController target) => hunter = target;

        /// <summary>Isi daftar jurus dengan set default (dipakai scene builder dan tombol Reset Inspector).</summary>
        public void UseDefaultAttacks() => attacks = CreateDefaultAttacks();

        private void Reset() => UseDefaultAttacks();

        private void Awake()
        {
            if (attacks == null || attacks.Count == 0) attacks = CreateDefaultAttacks();

            Health = new HealthPool(maxHP);
            brain = new DragonStateMachine(enrageThreshold, enrageRoarDuration, enragedTempo,
                                           staggerThreshold, staggerDuration, initialCooldown);
            brain.StateChanged += HandleStateChanged;
            brain.AttackActivated += HandleAttackActivated;
            brain.AttackFinished += HandleAttackFinished;
            brain.Staggered += HandleStaggered;
            brain.EnrageTriggered += HandleEnrage;
        }

        private void OnDestroy()
        {
            if (brain == null) return;
            brain.StateChanged -= HandleStateChanged;
            brain.AttackActivated -= HandleAttackActivated;
            brain.AttackFinished -= HandleAttackFinished;
            brain.Staggered -= HandleStaggered;
            brain.EnrageTriggered -= HandleEnrage;
        }

        /// <summary>Hentikan AI tanpa membunuh (mis. Hunter kalah).</summary>
        public void Halt()
        {
            brain.Halt();
            if (telegraph != null) telegraph.Hide();
            StopFire();
        }

        // ------------------------------------------------------------------ Damage
        /// <summary>Damage dari Hunter. Mengembalikan damage final yang masuk.</summary>
        public float ReceiveHit(float amount, Vector3 point, bool heavy)
        {
            if (IsDead || amount <= 0f) return 0f;

            bool vulnerable = brain.IsVulnerable;
            float final = amount * (vulnerable ? vulnerableDamageMultiplier : 1f);
            float dealt = Health.Damage(final);
            if (dealt <= 0f) return 0f;

            if (Health.IsDepleted)
            {
                Die();
            }
            else
            {
                brain.ReportDamage(dealt * (heavy ? heavyStaggerMultiplier : 1f), Health.Fraction);
            }

            if (hitFlash != null) hitFlash.Flash();
            Damaged?.Invoke(dealt, vulnerable);
            return dealt;
        }

        private void Die()
        {
            brain.Halt();
            if (telegraph != null) telegraph.Hide();
            StopFire();
            Died?.Invoke();
        }

        // ------------------------------------------------------------------ Loop
        private void Update()
        {
            float dt = Time.deltaTime;

            if (!IsDead && !brain.IsHalted)
            {
                brain.Tick(dt);

                switch (brain.State)
                {
                    case DragonState.Idle:
                        UpdateIdle(dt);
                        break;
                    case DragonState.TelegraphAttack:
                        UpdateTelegraph(dt);
                        break;
                    case DragonState.Attacking:
                        UpdateAttacking(dt);
                        break;
                }
            }

            AnimateVisual(dt);
        }

        private void UpdateIdle(float dt)
        {
            if (hunter == null || hunter.IsDead) return;

            Vector3 toHunter = AttackGeometry.Flatten(hunter.transform.position - transform.position);
            float distance = toHunter.magnitude;
            float speedMul = IsEnraged ? enragedSpeedMultiplier : 1f;

            TurnTowards(toHunter, turnSpeedDegrees * speedMul, dt);
            float facingAngle = toHunter.sqrMagnitude > 0.01f ? Vector3.Angle(transform.forward, toHunter) : 0f;
            bool hunterBehind = facingAngle > 110f;

            if (brain.CanStartAttack)
            {
                DragonAttackDefinition choice = ChooseAttack(distance, hunterBehind);
                // Jurus lingkaran tidak perlu menghadap; terjangan butuh bidikan lebih presisi.
                bool facingOk = choice != null
                                && (choice.shape == AttackShape.Circle
                                    || facingAngle < (choice.dashHitRadius > 0f ? 20f : 35f));
                if (choice != null && facingOk && brain.BeginAttack(choice))
                {
                    lastUsedTime[choice.id] = Time.time;
                    lastAttackId = choice.id;
                    AttackTelegraphed?.Invoke(choice);
                    return;
                }
            }

            if (distance > preferredDistance + bodyRadius * 0.5f && facingAngle < 70f)
            {
                Move(transform.forward * (moveSpeed * speedMul * dt));
            }
        }

        private void UpdateTelegraph(float dt)
        {
            DragonAttackDefinition attack = brain.CurrentAttack;
            if (attack == null) return;

            float progress = brain.StateDuration > 0f ? brain.StateTime / brain.StateDuration : 1f;
            if (progress < telegraphTrackingPortion && hunter != null && attack.shape == AttackShape.Cone)
            {
                Vector3 toHunter = AttackGeometry.Flatten(hunter.transform.position - transform.position);
                TurnTowards(toHunter, turnSpeedDegrees * 0.8f, dt);
            }

            if (telegraph != null) telegraph.ShowWarning(attack, AttackOrigin(attack), transform.forward, progress);
        }

        private void UpdateAttacking(float dt)
        {
            DragonAttackDefinition attack = brain.CurrentAttack;
            if (attack == null) return;

            if (attack.dashDistance > 0f && attack.active > 0f)
                Move(transform.forward * (attack.dashDistance / attack.active * dt));

            if (telegraph != null)
            {
                if (attack.dashHitRadius > 0f) telegraph.Hide();
                else telegraph.ShowActive(attack, AttackOrigin(attack), transform.forward);
            }

            if (attack.tickInterval > 0f)
            {
                if (Time.time >= nextTickTime)
                {
                    nextTickTime = Time.time + attack.tickInterval;
                    TryHitHunter(attack);
                }
            }
            else if (!currentAttackLanded)
            {
                currentAttackLanded = TryHitHunter(attack);
            }
        }

        private bool TryHitHunter(DragonAttackDefinition attack)
        {
            if (hunter == null || hunter.IsDead) return false;

            Vector3 target = hunter.transform.position;
            bool inside = attack.dashHitRadius > 0f
                ? AttackGeometry.IsInCircle(transform.position, target, attack.dashHitRadius + hunterHitRadius)
                : AttackGeometry.IsInShape(attack.shape, AttackOrigin(attack), transform.forward, target,
                                           attack.range, attack.angle, hunterHitRadius);
            if (!inside) return false;

            float damage = attack.damage * (IsEnraged ? enragedDamageMultiplier : 1f);
            return hunter.ReceiveDamage(damage, transform.position);
        }

        /// <summary>Titik asal area serangan: kerucut dari moncong, lingkaran dari pusat tubuh.</summary>
        public Vector3 AttackOrigin(DragonAttackDefinition attack)
        {
            return attack.shape == AttackShape.Cone
                ? transform.position + transform.forward * coneOriginOffset
                : transform.position;
        }

        private DragonAttackDefinition ChooseAttack(float distance, bool hunterBehind)
        {
            candidates.Clear();
            float totalWeight = 0f;
            float now = Time.time;

            foreach (DragonAttackDefinition a in attacks)
            {
                if (a == null || !a.IsUsable(distance, IsEnraged, hunterBehind)) continue;
                if (lastUsedTime.TryGetValue(a.id, out float last) && now - last < a.cooldown) continue;
                candidates.Add(a);
                totalWeight += Weight(a, hunterBehind);
            }

            if (candidates.Count == 0) return null;

            float roll = UnityEngine.Random.value * totalWeight;
            foreach (DragonAttackDefinition a in candidates)
            {
                roll -= Weight(a, hunterBehind);
                if (roll <= 0f) return a;
            }
            return candidates[candidates.Count - 1];
        }

        private float Weight(DragonAttackDefinition a, bool hunterBehind)
        {
            float w = a.weight;
            if (a.id == lastAttackId) w *= 0.45f; // hindari jurus yang sama beruntun
            if (hunterBehind && a.prefersFlank) w *= 2f;
            return w;
        }

        private void TurnTowards(Vector3 flatDirection, float degreesPerSecond, float dt)
        {
            if (flatDirection.sqrMagnitude < 0.01f) return;
            Quaternion target = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * dt);
        }

        private void Move(Vector3 delta)
        {
            Vector3 next = transform.position + delta;
            Vector3 flat = AttackGeometry.Flatten(next);
            if (flat.sqrMagnitude > arenaRadius * arenaRadius)
            {
                flat = flat.normalized * arenaRadius;
                next = new Vector3(flat.x, next.y, flat.z);
            }
            transform.position = next;
        }

        // ------------------------------------------------------------------ Event state
        private void HandleStateChanged(DragonState previous, DragonState next)
        {
            if (next != DragonState.TelegraphAttack && next != DragonState.Attacking && telegraph != null)
                telegraph.Hide();
            if (next != DragonState.Attacking) StopFire();
            StateChanged?.Invoke(next);
        }

        private void HandleAttackActivated(DragonAttackDefinition attack)
        {
            currentAttackLanded = false;
            nextTickTime = Time.time;
            if (attack.id == "fire" && fireParticles != null) fireParticles.Play(true);
        }

        private void HandleAttackFinished(DragonAttackDefinition attack) => StopFire();

        private void HandleStaggered() => Staggered?.Invoke();

        private void HandleEnrage()
        {
            if (hitFlash != null) hitFlash.SetTint(enragedTint, 0.35f);
            EnrageStarted?.Invoke();
        }

        private void StopFire()
        {
            if (fireParticles != null && fireParticles.isPlaying)
                fireParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ------------------------------------------------------------------ Visual
        private void AnimateVisual(float dt)
        {
            if (visualRoot == null) return;

            float t = Time.time;
            Vector3 pos = new Vector3(0f, Mathf.Sin(t * 2f) * 0.06f, 0f);
            Vector3 euler = Vector3.zero;
            float flapSpeed = IsEnraged ? 9f : 5f;
            float flapAmplitude = 18f;
            bool snap = false;

            DragonAttackDefinition attack = brain != null ? brain.CurrentAttack : null;
            float progress = brain != null && brain.StateDuration > 0f ? Mathf.Clamp01(brain.StateTime / brain.StateDuration) : 0f;

            if (IsDead)
            {
                pos = new Vector3(0f, -1.0f, 0f);
                euler = new Vector3(0f, 0f, 85f);
                flapAmplitude = 0f;
            }
            else
            {
                switch (State)
                {
                    case DragonState.TelegraphAttack when attack != null:
                        flapSpeed *= 1.6f;
                        switch (attack.id)
                        {
                            case "bite":   euler = new Vector3(-14f * progress, 0f, 0f); pos.z = -0.5f * progress; break;
                            case "tail":   euler = new Vector3(0f, -30f * progress, 0f); break;
                            case "fire":   euler = new Vector3(-22f * progress, 0f, 0f); pos.y += 0.4f * progress; break;
                            case "charge": euler = new Vector3(8f * progress, 0f, 0f); pos.y -= 0.35f * progress; pos.z = -0.6f * progress; break;
                            case "nova":   pos.y -= 0.5f * progress; flapAmplitude = 35f; break;
                        }
                        break;

                    case DragonState.Attacking when attack != null:
                        flapSpeed *= 2f;
                        switch (attack.id)
                        {
                            case "bite":   euler = new Vector3(12f, 0f, 0f); pos.z = 0.6f; break;
                            case "tail":   euler = new Vector3(0f, -30f + 390f * progress, 0f); snap = true; break;
                            case "fire":   euler = new Vector3(6f, Mathf.Sin(t * 10f) * 4f, 0f); break;
                            case "charge": euler = new Vector3(10f, 0f, 0f); pos.y -= 0.2f; break;
                            case "nova":   pos.y += 0.8f * Mathf.Sin(progress * Mathf.PI); flapAmplitude = 45f; break;
                        }
                        break;

                    case DragonState.Vulnerable:
                        pos.y = -0.55f;
                        euler = new Vector3(10f, 0f, 16f);
                        flapAmplitude = 4f;
                        flapSpeed = 2f;
                        break;

                    case DragonState.Enraged:
                        shakeOffset = UnityEngine.Random.insideUnitSphere * 0.08f;
                        pos += shakeOffset;
                        euler = new Vector3(-24f, 0f, 0f);
                        flapAmplitude = 40f;
                        flapSpeed = 14f;
                        break;
                }
            }

            Quaternion targetRotation = Quaternion.Euler(euler);
            if (snap)
            {
                visualRoot.localRotation = targetRotation;
                visualRoot.localPosition = pos;
            }
            else
            {
                float k = 1f - Mathf.Exp(-10f * dt);
                visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, targetRotation, k);
                visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, pos, k);
            }

            float flap = Mathf.Sin(t * flapSpeed) * flapAmplitude;
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 0f, 12f + flap);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, 0f, -12f - flap);
        }

        // ------------------------------------------------------------------ Data default
        public static List<DragonAttackDefinition> CreateDefaultAttacks()
        {
            var bite = DragonAttackDefinition.Create("bite", "Savage Bite", AttackShape.Cone, 4.6f, 70f, 22f, 0.75f, 0.22f, 0.9f, 0f, 5.5f);
            bite.dashDistance = 1.6f;
            bite.weight = 3f;

            var tail = DragonAttackDefinition.Create("tail", "Tail Sweep", AttackShape.Circle, 6.2f, 360f, 26f, 1.0f, 0.35f, 0.9f, 0f, 6.5f);
            tail.prefersFlank = true;
            tail.weight = 3f;

            var fire = DragonAttackDefinition.Create("fire", "Fire Breath", AttackShape.Cone, 13f, 36f, 11f, 1.2f, 1.4f, 0.6f, 4.5f, 14f);
            fire.tickInterval = 0.25f;
            fire.vulnerableAfter = 2.2f;
            fire.weight = 2f;
            fire.cooldown = 6f;

            var charge = DragonAttackDefinition.Create("charge", "Wyvern Charge", AttackShape.Cone, 12f, 16f, 30f, 0.9f, 0.7f, 1.0f, 8f, 30f);
            charge.dashDistance = 12f;
            charge.dashHitRadius = 2.6f;
            charge.weight = 2f;
            charge.cooldown = 4f;

            var nova = DragonAttackDefinition.Create("nova", "Inferno Nova", AttackShape.Circle, 9f, 360f, 38f, 1.5f, 0.3f, 0.4f, 0f, 9f);
            nova.enragedOnly = true;
            nova.vulnerableAfter = 2.5f;
            nova.weight = 1.5f;
            nova.cooldown = 10f;

            return new List<DragonAttackDefinition> { bite, tail, fire, charge, nova };
        }
    }
}
