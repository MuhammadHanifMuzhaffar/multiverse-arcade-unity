using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Kamera orbit orang ketiga dengan lock-on ke naga dan guncangan kamera (camera shake).
    /// Ditulis sendiri agar tidak butuh paket Cinemachine (perubahan paket = wewenang Integrator).
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Transform lockTarget;
        [SerializeField] private float distance = 7.5f;
        [SerializeField] private float pivotHeight = 1.7f;
        [SerializeField] private float lookSensitivity = 3f;
        [SerializeField] private float minPitch = -5f;
        [SerializeField] private float maxPitch = 55f;
        [SerializeField] private float followSharpness = 14f;
        [SerializeField] private float lockOnPitch = 16f;
        [SerializeField] private float lockOnSharpness = 6f;
        [SerializeField] private float lockOnMaxDistance = 40f;
        [SerializeField] private float minHeight = 0.4f;

        [Header("Tabrakan Kamera")]
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private float minCollisionDistance = 1.2f;
        [SerializeField] private float collisionReturnSharpness = 6f;

        private float yaw;
        private float pitch = 18f;
        private Vector2 lookInput;
        private Vector3 smoothedPivot;
        private float shakeAmplitude;
        private float shakeEndTime;
        private float shakeDuration;
        private float currentDistance = -1f;
        private readonly RaycastHit[] collisionHits = new RaycastHit[16];

        public bool IsLockedOn { get; private set; }

        public void SetTargets(Transform follow, Transform lockOn)
        {
            target = follow;
            lockTarget = lockOn;
        }

        public void SetLookInput(Vector2 input) => lookInput = input;

        /// <summary>Atur sudut orbit langsung (cutscene/test). Lock-on dilepas agar sudut tidak ditimpa.</summary>
        public void SetOrbit(float yawDegrees, float pitchDegrees)
        {
            IsLockedOn = false;
            yaw = yawDegrees;
            pitch = Mathf.Clamp(pitchDegrees, minPitch, maxPitch);
        }

        public void ToggleLockOn()
        {
            if (IsLockedOn) { IsLockedOn = false; return; }
            if (lockTarget == null || target == null) return;
            IsLockedOn = Vector3.Distance(target.position, lockTarget.position) <= lockOnMaxDistance;
        }

        public void SetLockOn(bool locked) => IsLockedOn = locked && lockTarget != null;

        public void Shake(float amplitude, float duration)
        {
            if (amplitude < shakeAmplitude && Time.time < shakeEndTime) return;
            shakeAmplitude = amplitude;
            shakeDuration = Mathf.Max(0.01f, duration);
            shakeEndTime = Time.time + shakeDuration;
        }

        private void Start()
        {
            if (target == null) return;

            // Mulai di belakang Hunter, menghadap naga bila ada.
            Vector3 facing = lockTarget != null ? lockTarget.position - target.position : target.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f) yaw = Quaternion.LookRotation(facing).eulerAngles.y;
            smoothedPivot = target.position + Vector3.up * pivotHeight;
            Apply(1f);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (IsLockedOn && lockTarget != null)
            {
                Vector3 toLock = lockTarget.position - target.position;
                toLock.y = 0f;
                if (toLock.sqrMagnitude > 0.25f)
                {
                    float desiredYaw = Quaternion.LookRotation(toLock).eulerAngles.y;
                    float k = 1f - Mathf.Exp(-lockOnSharpness * Time.deltaTime);
                    yaw = Mathf.LerpAngle(yaw, desiredYaw, k);
                    pitch = Mathf.Lerp(pitch, lockOnPitch, k);
                }
                // Mouse tetap bisa sedikit menyesuaikan pitch saat lock-on.
                pitch = Mathf.Clamp(pitch - lookInput.y * lookSensitivity * 0.3f, minPitch, maxPitch);
            }
            else
            {
                yaw += lookInput.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - lookInput.y * lookSensitivity, minPitch, maxPitch);
            }

            float follow = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            Apply(follow);
        }

        private void Apply(float followLerp)
        {
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            smoothedPivot = Vector3.Lerp(smoothedPivot, pivot, followLerp);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = -(rotation * Vector3.forward);

            // Tarik kamera mendekat bila naga/dinding menghalangi; kembali perlahan saat jalur bebas.
            float allowed = CollisionDistance(smoothedPivot, back, distance);
            if (currentDistance < 0f || allowed < currentDistance) currentDistance = allowed;
            else currentDistance = Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-collisionReturnSharpness * Time.deltaTime));

            Vector3 position = smoothedPivot + back * currentDistance;
            if (position.y < minHeight) position.y = minHeight;

            if (Time.time < shakeEndTime)
            {
                float fade = (shakeEndTime - Time.time) / shakeDuration;
                position += Random.insideUnitSphere * (shakeAmplitude * fade);
            }

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(smoothedPivot - position, Vector3.up));
        }

        /// <summary>Jarak terjauh yang aman dari pivot ke arah kamera (mengabaikan collider Hunter sendiri dan trigger).</summary>
        private float CollisionDistance(Vector3 pivot, Vector3 direction, float maxDistance)
        {
            int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, direction, collisionHits, maxDistance, ~0,
                                                   QueryTriggerInteraction.Ignore);
            float best = maxDistance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = collisionHits[i];
                if (hit.distance <= 0f) continue; // collider yang sudah melingkupi pivot (mis. Hunter)
                if (target != null && hit.collider.transform.IsChildOf(target)) continue;
                best = Mathf.Min(best, hit.distance);
            }
            return Mathf.Clamp(best - 0.1f, Mathf.Min(minCollisionDistance, maxDistance), maxDistance);
        }

        /// <summary>Jarak kamera saat ini (setelah tabrakan) untuk test.</summary>
        public float CurrentDistance => currentDistance;
    }
}
