using System.Collections.Generic;
using NUnit.Framework;

namespace Multiverse.Games.DragonHunter.Tests
{
    public class DragonStateMachineTests
    {
        private static DragonAttackDefinition Heavy() =>
            new DragonAttackDefinition { id = "fire", windup = 1f, active = 0.5f, recovery = 0.4f, vulnerableAfter = 2f };

        private static DragonAttackDefinition Light() =>
            new DragonAttackDefinition { id = "bite", windup = 0.8f, active = 0.2f, recovery = 0.6f };

        private static void Advance(DragonStateMachine sm, float seconds, float step = 0.02f)
        {
            for (float t = 0f; t < seconds - 0.0001f; t += step) sm.Tick(step);
        }

        [Test]
        public void StartsIdle_AndRespectsInitialCooldown()
        {
            var sm = new DragonStateMachine(initialCooldown: 1f);
            Assert.AreEqual(DragonState.Idle, sm.State);
            Assert.IsFalse(sm.BeginAttack(Light()));

            Advance(sm, 1.02f);
            Assert.IsTrue(sm.BeginAttack(Light()));
            Assert.AreEqual(DragonState.TelegraphAttack, sm.State);
        }

        [Test]
        public void HeavyAttack_FlowsTelegraphAttackingVulnerableIdle()
        {
            var sm = new DragonStateMachine(initialCooldown: 0f);
            var states = new List<DragonState>();
            int activated = 0, finished = 0;
            sm.StateChanged += (_, next) => states.Add(next);
            sm.AttackActivated += _ => activated++;
            sm.AttackFinished += _ => finished++;

            Assert.IsTrue(sm.BeginAttack(Heavy()));
            Advance(sm, 1.02f);
            Assert.AreEqual(DragonState.Attacking, sm.State);
            Advance(sm, 0.52f);
            Assert.AreEqual(DragonState.Vulnerable, sm.State);
            Advance(sm, 2.02f);
            Assert.AreEqual(DragonState.Idle, sm.State);

            CollectionAssert.AreEqual(new[]
            {
                DragonState.TelegraphAttack, DragonState.Attacking, DragonState.Vulnerable, DragonState.Idle
            }, states);
            Assert.AreEqual(1, activated);
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void LightAttack_ReturnsToIdleWithRecoveryCooldown()
        {
            var sm = new DragonStateMachine(initialCooldown: 0f);
            sm.BeginAttack(Light());
            Advance(sm, 1.1f); // windup 0.8 + aktif 0.2, lalu recovery 0.6 baru berjalan ~0.1 detik
            Assert.AreEqual(DragonState.Idle, sm.State);
            Assert.IsFalse(sm.CanStartAttack, "Masih dalam recovery.");
            Advance(sm, 0.62f);
            Assert.IsTrue(sm.CanStartAttack);
        }

        [Test]
        public void Enrage_TriggersOnce_AtThreshold_AndSpeedsUpTempo()
        {
            var sm = new DragonStateMachine(enrageThreshold: 0.5f, enrageRoarDuration: 1f, enragedTempo: 0.75f, initialCooldown: 0f);
            int enraged = 0;
            sm.EnrageTriggered += () => enraged++;

            sm.ReportDamage(10f, 0.6f);
            Assert.IsFalse(sm.IsEnraged);

            sm.ReportDamage(10f, 0.49f);
            Assert.IsTrue(sm.IsEnraged);
            Assert.AreEqual(DragonState.Enraged, sm.State);

            sm.ReportDamage(10f, 0.3f);
            Assert.AreEqual(1, enraged);

            Advance(sm, 1.02f);
            Assert.AreEqual(DragonState.Idle, sm.State);
            Advance(sm, 0.42f);
            Assert.IsTrue(sm.BeginAttack(Light()));
            Assert.AreEqual(0.8f * 0.75f, sm.StateDuration, 0.0001f, "Windup dipercepat saat Enraged.");
        }

        [Test]
        public void Enrage_InterruptsTelegraph()
        {
            var sm = new DragonStateMachine(initialCooldown: 0f);
            int finished = 0;
            sm.AttackFinished += _ => finished++;
            sm.BeginAttack(Heavy());
            sm.ReportDamage(50f, 0.4f);

            Assert.AreEqual(DragonState.Enraged, sm.State);
            Assert.IsNull(sm.CurrentAttack);
            Assert.AreEqual(1, finished, "Jurus yang dibatalkan tetap memberi sinyal selesai (telegraf/api dimatikan).");
        }

        [Test]
        public void Stagger_ForcesVulnerable_AfterThreshold()
        {
            var sm = new DragonStateMachine(staggerThreshold: 100f, staggerDuration: 3f, initialCooldown: 0f);
            int staggered = 0;
            sm.Staggered += () => staggered++;

            sm.BeginAttack(Light());
            sm.ReportDamage(60f, 0.9f);
            Assert.AreEqual(DragonState.TelegraphAttack, sm.State);
            sm.ReportDamage(45f, 0.85f);

            Assert.AreEqual(DragonState.Vulnerable, sm.State);
            Assert.AreEqual(1, staggered);
            Assert.AreEqual(0f, sm.StaggerAccumulated);

            sm.ReportDamage(200f, 0.8f);
            Assert.AreEqual(1, staggered, "Tidak ada stagger beruntun selama sudah Vulnerable.");
        }

        [Test]
        public void Halt_StopsEverything()
        {
            var sm = new DragonStateMachine(initialCooldown: 0f);
            sm.BeginAttack(Heavy());
            sm.Halt();

            Assert.AreEqual(DragonState.Idle, sm.State);
            Assert.IsFalse(sm.CanStartAttack);
            Advance(sm, 5f);
            Assert.AreEqual(DragonState.Idle, sm.State);
            Assert.IsFalse(sm.BeginAttack(Light()));
        }
    }
}
