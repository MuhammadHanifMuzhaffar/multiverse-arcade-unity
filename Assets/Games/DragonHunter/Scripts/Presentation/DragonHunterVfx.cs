using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Efek visual berbasis event: percikan saat kena, debu terjangan/hentakan, gelombang kejut
    /// Inferno Nova, dan guncangan kamera. Memakai satu ParticleSystem per jenis efek (Emit),
    /// tanpa Instantiate per hit, sehingga ramah GC.
    /// </summary>
    public class DragonHunterVfx : MonoBehaviour
    {
        [Header("Referensi")]
        [SerializeField] private HunterController hunter;
        [SerializeField] private HunterCombat combat;
        [SerializeField] private DragonBoss dragon;
        [SerializeField] private ThirdPersonCamera cameraRig;

        [Header("Partikel")]
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private ParticleSystem hitEmbers;

        [Header("Gelombang Kejut")]
        [SerializeField] private MeshRenderer shockwave;
        [SerializeField] private float shockwaveDuration = 0.45f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock block;
        private Color shockwaveBase = Color.white;
        private float shockwaveStart = -1f;
        private float shockwaveRadius = 9f;
        private Vector3 shockwaveCenter;
        private float chargeDustEnd = -1f;
        private float nextChargeDust;

        public int SparkBursts { get; private set; }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (shockwave != null)
            {
                if (shockwave.sharedMaterial != null && shockwave.sharedMaterial.HasProperty(ColorId))
                    shockwaveBase = shockwave.sharedMaterial.color;
                shockwave.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (combat != null)
            {
                combat.HitLanded += HandleSwordHit;
                combat.ArrowLanded += HandleArrowHit;
            }
            if (hunter != null) hunter.Damaged += HandleHunterDamaged;
            if (dragon != null)
            {
                dragon.AttackActivated += HandleDragonAttack;
                dragon.Died += HandleDragonDied;
            }
        }

        private void OnDisable()
        {
            if (combat != null)
            {
                combat.HitLanded -= HandleSwordHit;
                combat.ArrowLanded -= HandleArrowHit;
            }
            if (hunter != null) hunter.Damaged -= HandleHunterDamaged;
            if (dragon != null)
            {
                dragon.AttackActivated -= HandleDragonAttack;
                dragon.Died -= HandleDragonDied;
            }
        }

        // ------------------------------------------------------------------ Event
        private void HandleSwordHit(float damage, Vector3 point, bool heavy)
        {
            Emit(sparks, point, heavy ? 34 : 18);
            Emit(hitEmbers, point, heavy ? 14 : 6);
            SparkBursts++;
        }

        private void HandleArrowHit(Vector3 point)
        {
            Emit(sparks, point, 8);
            SparkBursts++;
        }

        private void HandleHunterDamaged(float damage)
        {
            if (hunter != null) Emit(dust, hunter.transform.position + Vector3.up * 0.1f, 10);
        }

        private void HandleDragonAttack(DragonAttackDefinition attack)
        {
            if (dragon == null) return;
            Vector3 feet = new Vector3(dragon.transform.position.x, 0.1f, dragon.transform.position.z);

            switch (attack.id)
            {
                case "tail":
                    Emit(dust, feet, 30);
                    Shake(0.12f, 0.3f);
                    break;
                case "charge":
                    chargeDustEnd = Time.time + attack.active;
                    nextChargeDust = Time.time;
                    Shake(0.18f, attack.active);
                    break;
                case "nova":
                    shockwaveCenter = feet;
                    shockwaveRadius = attack.range;
                    shockwaveStart = Time.time;
                    Emit(dust, feet, 60);
                    Shake(0.45f, 0.6f);
                    break;
                case "bite":
                    Shake(0.08f, 0.15f);
                    break;
            }
        }

        private void HandleDragonDied()
        {
            if (dragon != null) Emit(dust, new Vector3(dragon.transform.position.x, 0.1f, dragon.transform.position.z), 80);
        }

        // ------------------------------------------------------------------ Loop
        private void Update()
        {
            if (dragon != null && Time.time < chargeDustEnd && Time.time >= nextChargeDust)
            {
                nextChargeDust = Time.time + 0.08f;
                Emit(dust, new Vector3(dragon.transform.position.x, 0.1f, dragon.transform.position.z), 6);
            }

            UpdateShockwave();
        }

        private void UpdateShockwave()
        {
            if (shockwave == null) return;
            if (shockwaveStart < 0f)
            {
                if (shockwave.enabled) shockwave.enabled = false;
                return;
            }

            float t = (Time.time - shockwaveStart) / shockwaveDuration;
            if (t >= 1f)
            {
                shockwaveStart = -1f;
                shockwave.enabled = false;
                return;
            }

            float eased = 1f - (1f - t) * (1f - t);
            float r = Mathf.Lerp(0.5f, shockwaveRadius, eased);
            shockwave.enabled = true;
            shockwave.transform.position = new Vector3(shockwaveCenter.x, 0.06f, shockwaveCenter.z);
            shockwave.transform.localScale = new Vector3(r, 1f, r);
            block.SetColor(ColorId, new Color(shockwaveBase.r, shockwaveBase.g, shockwaveBase.b, shockwaveBase.a * (1f - t)));
            shockwave.SetPropertyBlock(block);
        }

        private void Shake(float amplitude, float duration)
        {
            if (cameraRig != null) cameraRig.Shake(amplitude, duration);
        }

        private static void Emit(ParticleSystem system, Vector3 position, int count)
        {
            if (system == null || count <= 0) return;
            var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            system.Emit(p, count);
        }
    }
}
