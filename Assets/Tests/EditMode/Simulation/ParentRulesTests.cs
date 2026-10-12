using BabyChampion.Simulation;
using NUnit.Framework;

namespace BabyChampion.Tests.EditMode
{
    /// <summary>Parent meters (design Section 7.2) and failure conditions (Section 13.2).</summary>
    public class ParentRulesTests
    {
        [Test]
        public void Crying_DoublesEnergyDrain()
        {
            var calm = SimTestUtil.Create();
            var crying = SimTestUtil.Create(s => s.baby.hunger = 90);
            calm.Tick();
            crying.Tick();
            Assert.IsTrue(crying.Baby.Crying);
            double calmDrain = 80 - calm.Parent.Energy;
            double cryingDrain = 80 - crying.Parent.Energy;
            Assert.That(calmDrain, Is.GreaterThan(0));
            Assert.AreEqual(2 * calmDrain, cryingDrain, 1e-9);
        }

        [Test]
        public void StressAt60_BlocksSleep()
        {
            var stressed = SimTestUtil.Create(s => { s.baby.asleep = true; s.baby.tiredness = 50; s.parent.stress = 60; });
            var check = stressed.Check(ParentCommand.Of(ActionType.Sleep));
            Assert.IsFalse(check.Allowed);
            StringAssert.Contains("stressed", check.Reason);
            Assert.IsFalse(stressed.Check(ParentCommand.Of(ActionType.Rest)).Allowed);

            var relaxed = SimTestUtil.Create(s => { s.baby.asleep = true; s.baby.tiredness = 50; s.parent.stress = 59; });
            Assert.IsTrue(relaxed.Check(ParentCommand.Of(ActionType.Sleep)).Allowed);
        }

        [Test]
        public void Sleep_RestoresEnergy()
        {
            var sim = SimTestUtil.Create(s => { s.baby.asleep = true; s.baby.tiredness = 80; })
                .WithCommands(ParentCommand.Of(ActionType.Sleep));
            sim.Ticks(30);
            Assert.IsTrue(sim.IsParentAsleep);
            Assert.That(sim.Parent.Energy, Is.GreaterThan(80));
        }

        [Test]
        public void ParentHungerAt70_HalvesRecovery_AndSpeedsUpDrain()
        {
            SimState Sleeping(SimState s)
            {
                s.baby.asleep = true;
                s.baby.tiredness = 80;
                return s;
            }
            var fed = SimTestUtil.Create(s => Sleeping(s)).WithCommands(ParentCommand.Of(ActionType.Sleep));
            var hungry = SimTestUtil.Create(s => Sleeping(s).parent.hunger = 75).WithCommands(ParentCommand.Of(ActionType.Sleep));
            fed.Tick();
            hungry.Tick();
            Assert.AreEqual((fed.Parent.Energy - 80) / 2, hungry.Parent.Energy - 80, 1e-9);

            var awakeFed = SimTestUtil.Create();
            var awakeHungry = SimTestUtil.Create(s => s.parent.hunger = 75);
            awakeFed.Tick();
            awakeHungry.Tick();
            Assert.AreEqual((80 - awakeFed.Parent.Energy) * 1.5, 80 - awakeHungry.Parent.Energy, 1e-9);
        }

        [Test]
        public void Crying_RaisesStress_CalmLowersIt()
        {
            var crying = SimTestUtil.Create(s => { s.baby.hunger = 90; s.parent.stress = 30; });
            var calm = SimTestUtil.Create(s => s.parent.stress = 30);
            crying.Ticks(10);
            calm.Ticks(10);
            Assert.That(crying.Parent.Stress, Is.GreaterThan(30));
            Assert.That(calm.Parent.Stress, Is.LessThan(30));
        }

        [Test]
        public void Eat_ResetsParentHunger()
        {
            var sim = SimTestUtil.Create(s => s.parent.hunger = 65).WithCommands(ParentCommand.Of(ActionType.Eat));
            sim.Ticks(15);
            Assert.That(sim.Parent.Hunger, Is.LessThan(1), "reset to 0, plus the minute that has passed");
            Assert.AreEqual(3, sim.Supplies.AdultFood);
        }

        [Test]
        public void CryingWakesTheParent()
        {
            var sim = SimTestUtil.Create(s => { s.baby.asleep = true; s.baby.tiredness = 80; s.baby.hunger = 60; })
                .WithCommands(ParentCommand.Of(ActionType.Sleep));
            sim.Tick();
            Assert.IsTrue(sim.IsParentAsleep);
            sim.Ticks(25); // hunger reaches 70
            Assert.IsTrue(sim.Baby.Crying);
            Assert.IsFalse(sim.IsParentAsleep);
        }

        [Test]
        public void EnergyAt0_ParentCollapses_DayFails()
        {
            var sim = SimTestUtil.Create(s => s.parent.energy = 0.05);
            sim.Tick();
            Assert.AreEqual(Outcome.Failed, sim.Outcome);
            Assert.AreEqual(FailReason.ParentCollapsed, sim.FailReason);
            sim.Tick();
            Assert.AreEqual(1, sim.Minute, "a finished day does not advance");
        }

        [Test]
        public void ContinuousCryingOver30Minutes_Fails()
        {
            var config = SimConfig.CreateDefault();
            foreach (var band in config.ageRates.bands) band.diapersPerDay = 0;
            var sim = SimTestUtil.Create(s => s.baby.hunger = 90, config);
            sim.Ticks(30);
            Assert.AreEqual(Outcome.Running, sim.Outcome);
            sim.Tick();
            Assert.AreEqual(FailReason.ContinuousCrying, sim.FailReason);
        }

        [Test]
        public void CryingBudget_Exceeded_Fails()
        {
            var config = SimConfig.CreateDefault();
            foreach (var band in config.ageRates.bands) band.diapersPerDay = 0;
            config.tuning.cryingBudgetMinutes = 20;
            var sim = SimTestUtil.Create(s => s.baby.hunger = 90, config);
            sim.Ticks(20);
            Assert.AreEqual(Outcome.Running, sim.Outcome);
            sim.Tick();
            Assert.AreEqual(FailReason.CryingBudget, sim.FailReason);
        }

        [Test]
        public void LowEnergy_MakesActionsSlower()
        {
            var normal = SimTestUtil.Create(s => s.baby.diaper = DiaperState.Wet).WithCommands(ParentCommand.Of(ActionType.ChangeDiaper));
            var tired = SimTestUtil.Create(s => { s.baby.diaper = DiaperState.Wet; s.parent.energy = 15; }).WithCommands(ParentCommand.Of(ActionType.ChangeDiaper));
            normal.Tick();
            tired.Tick();
            Assert.AreEqual(5, normal.Parent.ActionDurationMinutes, 1e-9);
            Assert.AreEqual(5 * 1.25, tired.Parent.ActionDurationMinutes, 1e-9);
        }
    }
}
