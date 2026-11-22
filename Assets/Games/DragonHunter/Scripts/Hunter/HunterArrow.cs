using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Proyektil panah. Bergerak dengan sphere cast per frame (tidak tembus pada FPS rendah)
    /// dan dibuat dari primitive saat runtime, sehingga tidak butuh prefab bersama.
    /// </summary>
    public class HunterArrow : MonoBehaviour
    {
        private const float CastRadius = 0.15f;

        private Vector3 direction;
        private float speed;
        private float damage;
        private float dieTime;
        private HunterCombat owner;
        private readonly RaycastHit[] hits = new RaycastHit[8];

        public static HunterArrow Spawn(Vector3 origin, Vector3 direction, float speed, float damage, float lifetime,
                                        Material material, Material trailMaterial, HunterCombat owner)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "HunterArrow";
            Object.Destroy(go.GetComponent<Collider>()); // deteksi lewat sphere cast, bukan collider
            go.transform.localScale = new Vector3(0.08f, 0.08f, 0.9f);
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, Vector3.up));

            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (trailMaterial != null)
            {
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMaterial;
                trail.time = 0.18f;
                trail.minVertexDistance = 0.2f;
                trail.widthMultiplier = 0.12f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
            }

            var arrow = go.AddComponent<HunterArrow>();
            arrow.direction = direction.normalized;
            arrow.speed = speed;
            arrow.damage = damage;
            arrow.dieTime = Time.time + Mathf.Max(0.1f, lifetime);
            arrow.owner = owner;
            return arrow;
        }

        private void Update()
        {
            if (Time.time >= dieTime)
            {
                Destroy(gameObject);
                return;
            }

            float step = speed * Time.deltaTime;
            int count = Physics.SphereCastNonAlloc(transform.position, CastRadius, direction, hits, step, ~0,
                                                   QueryTriggerInteraction.Collide);

            float nearest = float.MaxValue;
            int nearestIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.GetComponentInParent<HunterController>() != null) continue; // abaikan Hunter sendiri
                if (hits[i].distance < nearest)
                {
                    nearest = hits[i].distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex >= 0)
            {
                RaycastHit hit = hits[nearestIndex];
                var box = hit.collider.GetComponent<DragonHurtbox>();
                if (box != null && box.Boss != null && !box.Boss.IsDead)
                {
                    Vector3 point = hit.point == Vector3.zero ? hit.collider.ClosestPoint(transform.position) : hit.point;
                    float dealt = box.Boss.ReceiveHit(damage * box.DamageMultiplier, point, false);
                    if (dealt > 0f && owner != null) owner.NotifyArrowHit(dealt, point);
                }
                Destroy(gameObject);
                return;
            }

            transform.position += direction * step;
        }
    }
}
