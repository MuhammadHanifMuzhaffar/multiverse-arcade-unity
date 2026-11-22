using NUnit.Framework;
using UnityEngine;

namespace Multiverse.Games.DragonHunter.Tests
{
    public class HealthPoolTests
    {
        [Test]
        public void Damage_ClampsAtZero_AndRaisesDepletedOnce()
        {
            var hp = new HealthPool(100f);
            int depleted = 0;
            hp.Depleted += () => depleted++;

            Assert.AreEqual(60f, hp.Damage(60f));
            Assert.AreEqual(40f, hp.Damage(80f), "Damage yang masuk dibatasi sisa HP.");
            Assert.AreEqual(0f, hp.Damage(10f), "Pool yang habis tidak menerima damage lagi.");
            Assert.IsTrue(hp.IsDepleted);
            Assert.AreEqual(1, depleted);
        }

        [Test]
        public void Heal_NeverExceedsMax_AndCannotRevive()
        {
            var hp = new HealthPool(150f);
            hp.Damage(30f);
            Assert.AreEqual(30f, hp.Heal(55f));
            Assert.AreEqual(150f, hp.Current);

            hp.Damage(999f);
            Assert.AreEqual(0f, hp.Heal(50f));
            Assert.IsTrue(hp.IsDepleted);
        }

        [Test]
        public void NegativeOrZeroAmounts_AreIgnored()
        {
            var hp = new HealthPool(10f);
            Assert.AreEqual(0f, hp.Damage(-5f));
            Assert.AreEqual(0f, hp.Heal(0f));
            Assert.AreEqual(10f, hp.Current);
        }
    }

    public class StaminaPoolTests
    {
        [Test]
        public void TrySpend_FailsWithoutFullCost()
        {
            var st = new StaminaPool(100f, 20f, 0.5f);
            Assert.IsTrue(st.TrySpend(80f, 0f));
            Assert.IsFalse(st.TrySpend(25f, 0f));
            Assert.AreEqual(20f, st.Current, 0.0001f);
        }

        [Test]
        public void Regen_WaitsForDelay_ThenRefills()
        {
            var st = new StaminaPool(100f, 20f, 0.5f);
            st.TrySpend(50f, 1f);

            st.Tick(0.4f, 1.4f);
            Assert.AreEqual(50f, st.Current, 0.0001f, "Belum lewat jeda regen.");

            st.Tick(1f, 2f);
            Assert.AreEqual(70f, st.Current, 0.0001f);

            st.Tick(10f, 12f);
            Assert.AreEqual(100f, st.Current, 0.0001f, "Tidak melebihi Max.");
        }

        [Test]
        public void Drain_ReportsEmpty()
        {
            var st = new StaminaPool(10f, 5f, 0f);
            Assert.IsTrue(st.Drain(6f, 0f));
            Assert.IsFalse(st.Drain(6f, 0f));
            Assert.AreEqual(0f, st.Current);
        }
    }

    public class InvulnerabilityWindowTests
    {
        [Test]
        public void OverlappingGrants_ExtendInsteadOfCutting()
        {
            var w = new InvulnerabilityWindow();
            w.Grant(0f, 0.6f);
            w.Grant(0.3f, 0.6f); // dodge kedua sebelum yang pertama selesai

            Assert.IsTrue(w.IsActive(0.7f), "Bug lama: coroutine pertama mematikan i-frame di t=0.6.");
            Assert.IsFalse(w.IsActive(0.95f));
        }

        [Test]
        public void ShorterGrant_DoesNotShrinkWindow()
        {
            var w = new InvulnerabilityWindow();
            w.Grant(0f, 1f);
            w.Grant(0.1f, 0.2f);
            Assert.IsTrue(w.IsActive(0.9f));
        }
    }

    public class AttackGeometryTests
    {
        [Test]
        public void Cone_RespectsRangeAndHalfAngle()
        {
            Vector3 origin = Vector3.zero;
            Vector3 fwd = Vector3.forward;

            Assert.IsTrue(AttackGeometry.IsInCone(origin, fwd, new Vector3(0f, 0f, 4f), 5f, 60f));
            Assert.IsTrue(AttackGeometry.IsInCone(origin, fwd, Quaternion.Euler(0f, 29f, 0f) * Vector3.forward * 4f, 5f, 60f));
            Assert.IsFalse(AttackGeometry.IsInCone(origin, fwd, Quaternion.Euler(0f, 31f, 0f) * Vector3.forward * 4f, 5f, 60f));
            Assert.IsFalse(AttackGeometry.IsInCone(origin, fwd, new Vector3(0f, 0f, 5.1f), 5f, 60f));
            Assert.IsFalse(AttackGeometry.IsInCone(origin, fwd, new Vector3(0f, 0f, -2f), 5f, 60f), "Di belakang tidak kena.");
        }

        [Test]
        public void HeightIsIgnored()
        {
            Assert.IsTrue(AttackGeometry.IsInCone(Vector3.zero, Vector3.forward, new Vector3(0f, 10f, 3f), 5f, 60f));
            Assert.IsTrue(AttackGeometry.IsInCircle(Vector3.zero, new Vector3(3f, -4f, 0f), 3f));
        }

        [Test]
        public void ExtraRadius_ExpandsHitArea()
        {
            Assert.IsFalse(AttackGeometry.IsInShape(AttackShape.Circle, Vector3.zero, Vector3.forward, new Vector3(6.3f, 0f, 0f), 6f, 360f));
            Assert.IsTrue(AttackGeometry.IsInShape(AttackShape.Circle, Vector3.zero, Vector3.forward, new Vector3(6.3f, 0f, 0f), 6f, 360f, 0.4f));
        }
    }

    public class DragonAttackDefinitionTests
    {
        [Test]
        public void IsUsable_ChecksDistanceEnrageAndFlank()
        {
            var nova = DragonAttackDefinition.Create("nova", "Nova", AttackShape.Circle, 9f, 360f, 30f, 1f, 0.3f, 0.4f, 0f, 9f);
            nova.enragedOnly = true;
            Assert.IsFalse(nova.IsUsable(3f, enraged: false, hunterBehind: false));
            Assert.IsTrue(nova.IsUsable(3f, enraged: true, hunterBehind: false));
            Assert.IsFalse(nova.IsUsable(9.5f, enraged: true, hunterBehind: false));

            var tail = DragonAttackDefinition.Create("tail", "Tail", AttackShape.Circle, 6f, 360f, 20f, 1f, 0.3f, 0.5f, 0f, 6f);
            tail.prefersFlank = true;
            Assert.IsFalse(tail.IsUsable(3f, false, hunterBehind: false));
            Assert.IsTrue(tail.IsUsable(3f, false, hunterBehind: true));
        }

        [Test]
        public void DefaultAttackSet_CoversAllPrdBehaviours()
        {
            var attacks = DragonBoss.CreateDefaultAttacks();
            Assert.AreEqual(5, attacks.Count);
            Assert.IsTrue(attacks.Exists(a => a.vulnerableAfter > 0f), "Ada jurus berat yang membuka jendela Vulnerable.");
            Assert.IsTrue(attacks.Exists(a => a.enragedOnly), "Ada jurus khusus Enraged.");
            Assert.IsTrue(attacks.Exists(a => a.tickInterval > 0f), "Semburan api memberi damage berulang.");
            foreach (var a in attacks) Assert.Greater(a.windup, 0.5f, a.id + " harus punya telegraf yang bisa dibaca pemain.");
        }
    }
}
