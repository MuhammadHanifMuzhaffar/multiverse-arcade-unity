using System;
using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Pergerakan dan status Hunter: jalan/sprint relatif kamera, dodge roll ber-i-frame,
    /// stamina, HP, potion, dan reaksi terkena serangan.
    /// Input tidak dibaca di sini (lihat <see cref="HunterInput"/>) agar bisa dikendalikan dari test.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HunterController : MonoBehaviour
    {
        [Header("Statistik")]
        [SerializeField] private float maxHP = 150f;
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenPerSecond = 26f;
        [SerializeField] private float staminaRegenDelay = 0.7f;

        [Header("Gerak")]
        [SerializeField] private float walkSpeed = 5.5f;
        [SerializeField] private float sprintSpeed = 8.5f;
        [SerializeField] private float sprintStaminaPerSecond = 16f;
        [SerializeField] private float turnSpeedDegrees = 900f;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float arenaRadius = 18.5f;

        [Header("Dodge Roll")]
        [SerializeField] private float dodgeStaminaCost = 25f;
        [SerializeField] private float dodgeDuration = 0.42f;
        [SerializeField] private float dodgeDistance = 5f;
        [SerializeField] private float dodgeIFrames = 0.32f;
        [SerializeField] private float dodgeCooldown = 0.12f;

        [Header("Potion")]
        [SerializeField] private int potionCount = 3;
        [SerializeField] private float potionHeal = 55f;
        [SerializeField] private float potionDrinkTime = 1.0f;
        [SerializeField] private float drinkMoveMultiplier = 0.35f;

        [Header("Reaksi Terkena")]
        [SerializeField] private float hitStunDuration = 0.35f;
        [SerializeField] private float postHitIFrames = 0.6f;
        [SerializeField] private float knockbackSpeed = 7f;

        [Header("Referensi")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private HitFlash hitFlash;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.15f, 1f);

        private CharacterController characterController;
        private Vector2 moveInput;
        private bool sprintHeld;
        private float verticalVelocity;

        private float dodgeEndTime = -1f;
        private float nextDodgeTime;
        private Vector3 dodgeDirection;

        private float drinkEndTime = -1f;
        private float stunEndTime = -1f;
        private Vector3 knockbackVelocity;

        public HealthPool Health { get; private set; }
        public StaminaPool Stamina { get; private set; }
        public InvulnerabilityWindow IFrames { get; } = new InvulnerabilityWindow();

        public int PotionsLeft { get; private set; }
        public bool IsDead => Health != null && Health.IsDepleted;
        public bool IsDodging => Time.time < dodgeEndTime;
        public bool IsDrinking => Time.time < drinkEndTime;
        public bool IsStunned => Time.time < stunEndTime;
        public bool IsSprinting { get; private set; }
        public bool IsInvulnerable => IFrames.IsActive(Time.time);

        /// <summary>Hunter tidak bisa memulai aksi lain (serang/dodge/potion).</summary>
        public bool IsBusy => IsDead || IsDodging || IsDrinking || IsStunned;

        /// <summary>Dimatikan saat pause, intro, atau pertarungan selesai.</summary>
        public bool ControlsEnabled { get; set; } = true;

        /// <summary>Pengali kecepatan dari sistem combat (melambat saat mengayun pedang).</summary>
        public float CombatMoveMultiplier { get; set; } = 1f;

        /// <summary>Saat combat memaksa arah hadap (soft aim), rotasi otomatis ke arah gerak dimatikan.</summary>
        public bool FacingLocked { get; set; }

        public float DodgeStaminaCost => dodgeStaminaCost;

        public event Action<float> Damaged;
        public event Action Died;
        public event Action DodgeStarted;
        public event Action<float> PotionUsed;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            Health = new HealthPool(maxHP);
            Stamina = new StaminaPool(maxStamina, staminaRegenPerSecond, staminaRegenDelay);
            PotionsLeft = potionCount;
            Health.Depleted += HandleDepleted;
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Depleted -= HandleDepleted;
        }

        // ------------------------------------------------------------------ API input
        public void SetMoveInput(Vector2 input) => moveInput = Vector2.ClampMagnitude(input, 1f);
        public void SetSprint(bool held) => sprintHeld = held;
        public void SetCamera(Transform cam) => cameraTransform = cam;

        public bool TryDodge()
        {
            if (!ControlsEnabled || IsDead || IsDrinking || IsStunned || IsDodging) return false;
            if (Time.time < nextDodgeTime) return false;
            if (!Stamina.TrySpend(dodgeStaminaCost, Time.time)) return false;

            Vector3 dir = WorldMoveDirection();
            dodgeDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(dodgeDirection, Vector3.up);

            dodgeEndTime = Time.time + dodgeDuration;
            nextDodgeTime = dodgeEndTime + dodgeCooldown;
            IFrames.Grant(Time.time, dodgeIFrames);
            FacingLocked = false;
            DodgeStarted?.Invoke();
            return true;
        }

        public bool TryDrinkPotion()
        {
            if (!ControlsEnabled || IsBusy || PotionsLeft <= 0) return false;
            if (Health.Current >= Health.Max) return false;

            PotionsLeft--;
            drinkEndTime = Time.time + potionDrinkTime;
            return true;
        }

        /// <summary>Diberikan oleh naga. Mengembalikan false bila dihindari (i-frame) atau Hunter sudah kalah.</summary>
        public bool ReceiveDamage(float damage, Vector3 sourcePosition)
        {
            if (IsDead || damage <= 0f || IsInvulnerable) return false;

            float dealt = Health.Damage(damage);
            if (dealt <= 0f) return false;

            // Minum potion dibatalkan bila terkena (risiko khas boss fight).
            drinkEndTime = -1f;
            dodgeEndTime = -1f;

            if (!IsDead)
            {
                IFrames.Grant(Time.time, postHitIFrames);
                stunEndTime = Time.time + hitStunDuration;
                Vector3 away = AttackGeometry.Flatten(transform.position - sourcePosition);
                knockbackVelocity = (away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward) * knockbackSpeed;
            }

            if (hitFlash != null) hitFlash.Flash(hitFlashColor);
            Damaged?.Invoke(dealt);
            return true;
        }

        public void FaceTowards(Vector3 worldPosition)
        {
            Vector3 dir = AttackGeometry.Flatten(worldPosition - transform.position);
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // ------------------------------------------------------------------ Loop
        private void Update()
        {
            float now = Time.time;
            float dt = Time.deltaTime;
            Stamina.Tick(dt, now);

            UpdatePotion(now);

            Vector3 horizontal = Vector3.zero;
            IsSprinting = false;

            if (IsDead)
            {
                horizontal = Vector3.zero;
            }
            else if (IsDodging)
            {
                horizontal = dodgeDirection * (dodgeDistance / dodgeDuration);
            }
            else if (IsStunned)
            {
                horizontal = knockbackVelocity;
                knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackSpeed * 4f * dt);
            }
            else if (ControlsEnabled)
            {
                Vector3 dir = WorldMoveDirection();
                if (dir.sqrMagnitude > 0.0001f)
                {
                    float speed = walkSpeed;
                    if (sprintHeld && !IsDrinking && CombatMoveMultiplier >= 0.99f
                        && Stamina.Drain(sprintStaminaPerSecond * dt, now))
                    {
                        speed = sprintSpeed;
                        IsSprinting = true;
                    }

                    if (IsDrinking) speed *= drinkMoveMultiplier;
                    speed *= CombatMoveMultiplier;
                    horizontal = dir * speed;

                    if (!FacingLocked)
                    {
                        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeedDegrees * dt);
                    }
                }
            }

            if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;

            Vector3 motion = (horizontal + Vector3.up * verticalVelocity) * dt;
            if (characterController.enabled) characterController.Move(motion);

            ClampToArena();
            AnimateVisual(now);
        }

        private void UpdatePotion(float now)
        {
            if (drinkEndTime > 0f && now >= drinkEndTime)
            {
                drinkEndTime = -1f;
                float healed = Health.Heal(potionHeal);
                PotionUsed?.Invoke(healed);
            }
        }

        private Vector3 WorldMoveDirection()
        {
            if (moveInput.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cameraTransform != null)
            {
                forward = AttackGeometry.Flatten(cameraTransform.forward).normalized;
                right = AttackGeometry.Flatten(cameraTransform.right).normalized;
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            }

            Vector3 dir = forward * moveInput.y + right * moveInput.x;
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        private void ClampToArena()
        {
            Vector3 flat = AttackGeometry.Flatten(transform.position);
            if (flat.sqrMagnitude <= arenaRadius * arenaRadius) return;

            Vector3 clamped = flat.normalized * arenaRadius;
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false; // CharacterController menimpa posisi bila tidak dimatikan
            transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
            characterController.enabled = wasEnabled;
        }

        private void AnimateVisual(float now)
        {
            if (visualRoot == null) return;

            Quaternion targetRotation = Quaternion.identity;
            Vector3 targetPosition = Vector3.zero;

            if (IsDead)
            {
                targetRotation = Quaternion.Euler(-85f, 0f, 0f);
                targetPosition = new Vector3(0f, -0.55f, 0f);
            }
            else if (IsDodging)
            {
                float t = 1f - (dodgeEndTime - now) / dodgeDuration;
                targetRotation = Quaternion.Euler(360f * Mathf.Clamp01(t), 0f, 0f);
                targetPosition = new Vector3(0f, -0.45f, 0f);
                visualRoot.localRotation = targetRotation; // berguling langsung, tanpa smoothing
                visualRoot.localPosition = targetPosition;
                return;
            }
            else if (IsStunned)
            {
                targetRotation = Quaternion.Euler(-18f, 0f, 0f);
            }
            else if (IsDrinking)
            {
                targetRotation = Quaternion.Euler(-12f, 0f, 0f);
                targetPosition = new Vector3(0f, -0.1f, 0f);
            }
            else if (IsSprinting)
            {
                targetRotation = Quaternion.Euler(14f, 0f, 0f);
            }

            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, targetRotation, k);
            visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, targetPosition, k);
        }

        private void HandleDepleted()
        {
            drinkEndTime = -1f;
            dodgeEndTime = -1f;
            Died?.Invoke();
        }
    }
}
