using System;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>State boss naga sesuai PRD.</summary>
    public enum DragonState
    {
        Idle,            // mengintai/mengejar, memilih jurus berikutnya
        TelegraphAttack, // windup: area serangan ditandai, Hunter sempat menghindar
        Attacking,       // fase aktif: damage diberikan
        Vulnerable,      // lelah setelah jurus berat atau stagger: damage 1.5x
        Enraged,         // transisi murka (raungan) saat HP <= ambang; setelahnya tempo lebih cepat
    }

    /// <summary>
    /// Logika timing state naga tanpa ketergantungan MonoBehaviour, sehingga bisa diuji EditMode.
    /// DragonBoss (MonoBehaviour) memutuskan jurus mana dan bergerak; kelas ini mengatur kapan.
    /// </summary>
    public sealed class DragonStateMachine
    {
        public DragonState State { get; private set; } = DragonState.Idle;
        public float StateTime { get; private set; }
        public float StateDuration { get; private set; }
        public bool IsEnraged { get; private set; }
        public bool IsHalted { get; private set; }
        public DragonAttackDefinition CurrentAttack { get; private set; }

        /// <summary>Sisa jeda di Idle sebelum boleh memulai jurus baru.</summary>
        public float IdleCooldownRemaining { get; private set; }

        public float StaggerAccumulated { get; private set; }

        // Konfigurasi
        public float EnrageThreshold { get; }
        public float EnrageRoarDuration { get; }
        public float EnragedTempo { get; }
        public float StaggerThreshold { get; }
        public float StaggerDuration { get; }

        /// <summary>Pengali durasi fase: &lt; 1 berarti lebih cepat (Enraged).</summary>
        public float TempoScale => IsEnraged ? EnragedTempo : 1f;

        public bool CanStartAttack => !IsHalted && State == DragonState.Idle && IdleCooldownRemaining <= 0f;
        public bool IsAttackActive => State == DragonState.Attacking;
        public bool IsVulnerable => State == DragonState.Vulnerable;

        public event Action<DragonState, DragonState> StateChanged;
        public event Action<DragonAttackDefinition> AttackActivated;
        public event Action<DragonAttackDefinition> AttackFinished;
        public event Action Staggered;
        public event Action EnrageTriggered;

        public DragonStateMachine(float enrageThreshold = 0.5f, float enrageRoarDuration = 1.6f,
                                  float enragedTempo = 0.75f, float staggerThreshold = 180f,
                                  float staggerDuration = 3f, float initialCooldown = 1.5f)
        {
            EnrageThreshold = enrageThreshold;
            EnrageRoarDuration = enrageRoarDuration;
            EnragedTempo = enragedTempo;
            StaggerThreshold = staggerThreshold;
            StaggerDuration = staggerDuration;
            IdleCooldownRemaining = initialCooldown;
        }

        /// <summary>Mulai telegraf jurus. Hanya berhasil dari Idle yang sudah lewat cooldown.</summary>
        public bool BeginAttack(DragonAttackDefinition attack)
        {
            if (attack == null || !CanStartAttack) return false;

            CurrentAttack = attack;
            Enter(DragonState.TelegraphAttack, attack.windup * TempoScale);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (IsHalted || deltaTime <= 0f) return;

            StateTime += deltaTime;

            switch (State)
            {
                case DragonState.Idle:
                    IdleCooldownRemaining = Math.Max(0f, IdleCooldownRemaining - deltaTime);
                    break;

                case DragonState.TelegraphAttack:
                    if (StateTime >= StateDuration)
                    {
                        Enter(DragonState.Attacking, CurrentAttack.active);
                        AttackActivated?.Invoke(CurrentAttack);
                    }
                    break;

                case DragonState.Attacking:
                    if (StateTime >= StateDuration)
                    {
                        DragonAttackDefinition finished = CurrentAttack;
                        AttackFinished?.Invoke(finished);
                        CurrentAttack = null;

                        if (finished.vulnerableAfter > 0f)
                            Enter(DragonState.Vulnerable, finished.vulnerableAfter * TempoScale);
                        else
                            EnterIdle(finished.recovery * TempoScale);
                    }
                    break;

                case DragonState.Vulnerable:
                    if (StateTime >= StateDuration) EnterIdle(0.6f * TempoScale);
                    break;

                case DragonState.Enraged:
                    if (StateTime >= StateDuration) EnterIdle(0.4f);
                    break;
            }
        }

        /// <summary>
        /// Laporkan damage yang diterima naga. Dapat memicu Enraged (sekali) atau stagger (Vulnerable paksa).
        /// </summary>
        public void ReportDamage(float amount, float hpFraction)
        {
            if (IsHalted || amount <= 0f) return;

            if (!IsEnraged && hpFraction > 0f && hpFraction <= EnrageThreshold)
            {
                IsEnraged = true;
                StaggerAccumulated = 0f;
                CancelAttack();
                Enter(DragonState.Enraged, EnrageRoarDuration);
                EnrageTriggered?.Invoke();
                return;
            }

            if (State == DragonState.Vulnerable || State == DragonState.Enraged) return;

            StaggerAccumulated += amount;
            if (StaggerAccumulated >= StaggerThreshold)
            {
                StaggerAccumulated = 0f;
                CancelAttack();
                Enter(DragonState.Vulnerable, StaggerDuration);
                Staggered?.Invoke();
            }
        }

        /// <summary>Hentikan semua aktivitas (pertarungan selesai).</summary>
        public void Halt()
        {
            CancelAttack();
            IsHalted = true;
            Enter(DragonState.Idle, 0f);
        }

        private void CancelAttack()
        {
            if (CurrentAttack == null) return;
            DragonAttackDefinition cancelled = CurrentAttack;
            CurrentAttack = null;
            AttackFinished?.Invoke(cancelled);
        }

        private void EnterIdle(float cooldown)
        {
            IdleCooldownRemaining = Math.Max(0f, cooldown);
            Enter(DragonState.Idle, 0f);
        }

        private void Enter(DragonState next, float duration)
        {
            DragonState previous = State;
            State = next;
            StateTime = 0f;
            StateDuration = Math.Max(0f, duration);
            if (previous != next) StateChanged?.Invoke(previous, next);
        }
    }
}
