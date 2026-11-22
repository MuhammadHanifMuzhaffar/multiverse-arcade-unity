using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Ditempel pada collider bagian tubuh naga. Kepala memakai pengali lebih besar (weak point).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DragonHurtbox : MonoBehaviour
    {
        [SerializeField] private DragonBoss boss;
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private string partName = "Body";

        public DragonBoss Boss => boss;
        public float DamageMultiplier => damageMultiplier;
        public string PartName => partName;

        public void Configure(DragonBoss owner, float multiplier, string part)
        {
            boss = owner;
            damageMultiplier = multiplier;
            partName = part;
        }

        private void Awake()
        {
            if (boss == null) boss = GetComponentInParent<DragonBoss>();
        }
    }
}
