using BabyChampion.Simulation;
using NUnit.Framework;

namespace BabyChampion.Tests.EditMode
{
    /// <summary>Supplies, money, ordering (design Sections 7.3, 9, 10) and action rules.</summary>
    public class SuppliesAndActionsTests
    {
        [Test]
        public void Delivery_ArrivesAfterTwoInGameHours()
        {
            var sim = SimTestUtil.Create().WithCommands(ParentCommand.Order(SupplyType.Diapers));
            int diapers = sim.Supplies.Diapers;
            int money = sim.Money;
            sim.Tick(); // the order is placed in this minute
            Assert.AreEqual(money - 12, sim.Money);
            Assert.AreEqual(1, sim.PendingDeliveries.Count);

            sim.Ticks(118);
            Assert.AreEqual(diapers, sim.Supplies.Diapers, "arrived early");
            sim.Tick();
            Assert.AreEqual(120, sim.Minute, 1e-9);
            Assert.AreEqual(diapers + 24, sim.Supplies.Diapers);
            Assert.AreEqual(0, sim.PendingDeliveries.Count);
        }

        [Test]
        public void ExpressDelivery_ArrivesAfter30Minutes_AndCostsExtra()
        {
            var sim = SimTestUtil.Create().WithCommands(ParentCommand.Order(SupplyType.Wipes, express: true));
            int money = sim.Money;
            int wipes = sim.Supplies.Wipes;
            sim.Ticks(29);
            Assert.AreEqual(wipes, sim.Supplies.Wipes);
            sim.Tick();
            Assert.AreEqual(wipes + 40, sim.Supplies.Wipes);
            Assert.AreEqual(money - 4 - 8, sim.Money);
        }

        [Test]
        public void Order_WithoutEnoughMoney_IsRefused()
        {
            var sim = SimTestUtil.Create(s => s.money = 5).WithCommands(ParentCommand.Order(SupplyType.Diapers));
            sim.Tick();
            Assert.AreEqual(5, sim.Money);
            Assert.AreEqual(0, sim.PendingDeliveries.Count);
            StringAssert.Contains("Not enough money", sim.LastRejection);
        }

        [Test]
        public void Ordering_DoesNotInterruptTheCurrentAction()
        {
            var sim = SimTestUtil.Create(s => s.baby.tension = 40)
                .WithCommands(ParentCommand.Of(ActionType.Soothe), ParentCommand.Order(SupplyType.Diapers));
            sim.Ticks(2);
            Assert.AreEqual(ActionType.Soothe, sim.Parent.Action);
            Assert.AreEqual(1, sim.PendingDeliveries.Count);
        }

        [Test]
        public void OutOfDiapers_ChangeIsUnavailable()
        {
            var sim = SimTestUtil.Create(s => { s.baby.diaper = DiaperState.Dirty; s.supplies.diapers = 0; });
            var check = sim.Check(ParentCommand.Of(ActionType.ChangeDiaper));
            Assert.IsFalse(check.Allowed);
            StringAssert.Contains("Out of diapers", check.Reason);
        }

        [Test]
        public void ChangeDiaper_UsesADiaperAndAWipe()
        {
            var sim = SimTestUtil.Create(s => s.baby.diaper = DiaperState.Dirty).WithCommands(ParentCommand.Of(ActionType.ChangeDiaper));
            int diapers = sim.Supplies.Diapers;
            int wipes = sim.Supplies.Wipes;
            sim.Ticks(4);
            Assert.AreEqual(DiaperState.Dirty, sim.Baby.Diaper, "a change takes 5 minutes");
            sim.Tick();
            Assert.AreEqual(DiaperState.Clean, sim.Baby.Diaper);
            Assert.AreEqual(diapers - 1, sim.Supplies.Diapers);
            Assert.AreEqual(wipes - 1, sim.Supplies.Wipes);
        }

        [Test]
        public void Breastfeed_EmptiesHunger_AndNeedsABurp()
        {
            var sim = SimTestUtil.Create(s => s.baby.hunger = 75).WithCommands(ParentCommand.Of(ActionType.Breastfeed));
            sim.Tick();
            Assert.IsFalse(sim.Baby.Crying, "a feeding baby does not cry from hunger");
            sim.Ticks(24);
            Assert.That(sim.Baby.Hunger, Is.LessThan(1), "emptied, plus the minute that has passed");
            Assert.IsTrue(sim.Baby.NeedsBurp);
            Assert.AreEqual(1, sim.FeedsToday);
        }

        [Test]
        public void FormulaFeeding_NeedsAPreparedBottle()
        {
            var sim = SimTestUtil.Create(s => s.baby.hunger = 60, feeding: FeedingChoice.Formula);
            Assert.IsFalse(sim.Check(ParentCommand.Of(ActionType.Breastfeed)).Allowed);
            Assert.IsFalse(sim.Check(ParentCommand.Of(ActionType.BottleFeed)).Allowed);

            int formula = sim.Supplies.Formula;
            sim.WithCommands(ParentCommand.Of(ActionType.PrepareBottle));
            sim.Ticks(10);
            Assert.AreEqual(1, sim.Supplies.BottlesReady);
            Assert.AreEqual(formula - 1, sim.Supplies.Formula);

            sim.WithCommands(ParentCommand.Of(ActionType.BottleFeed));
            sim.Ticks(15);
            Assert.AreEqual(0, sim.Supplies.BottlesReady);
            Assert.That(sim.Baby.Hunger, Is.LessThan(1));
        }

        [Test]
        public void BreastfeedingChoice_HasNoBottles()
        {
            var sim = SimTestUtil.Create(s => s.baby.hunger = 60, feeding: FeedingChoice.Breast);
            StringAssert.Contains("Breastfeeding", sim.Check(ParentCommand.Of(ActionType.PrepareBottle)).Reason);
            Assert.IsTrue(sim.Check(ParentCommand.Of(ActionType.Breastfeed)).Allowed);
        }

        [Test]
        public void ANewCommand_ReplacesTheCurrentAction_AndStopEndsIt()
        {
            var sim = SimTestUtil.Create(s => s.baby.tension = 40)
                .WithCommands(ParentCommand.Of(ActionType.Soothe), ParentCommand.Of(ActionType.Rest), ParentCommand.Of(ActionType.Stop));
            sim.Tick();
            Assert.AreEqual(ActionType.Soothe, sim.Parent.Action);
            sim.Tick();
            Assert.AreEqual(ActionType.Rest, sim.Parent.Action);
            sim.Tick();
            Assert.IsFalse(sim.Parent.Busy);
        }

        [Test]
        public void RefusedCommand_ExplainsWhy_AndChangesNothing()
        {
            var sim = SimTestUtil.Create().WithCommands(ParentCommand.Of(ActionType.ChangeDiaper));
            sim.Tick();
            Assert.IsFalse(sim.Parent.Busy);
            StringAssert.Contains("The diaper is clean", sim.LastRejection);
        }
    }
}
