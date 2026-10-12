using BabyChampion.Simulation;
using NUnit.Framework;

namespace BabyChampion.Tests.EditMode
{
    /// <summary>Baby meters and crying triggers (design Section 7.1).</summary>
    public class BabyRulesTests
    {
        [Test]
        public void HungerAt70_StartsCrying()
        {
            var sim = SimTestUtil.Create(s => s.baby.hunger = 69.8);
            sim.Tick();
            Assert.That(sim.Baby.Hunger, Is.GreaterThanOrEqualTo(70));
            Assert.IsTrue(sim.Baby.Crying);
            Assert.AreEqual(CryCause.Hunger, sim.Baby.CryCauses);
            Assert.AreEqual(CryHint.HandToMouth, sim.Baby.Hint);
        }

        [Test]
        public void HungerBelow70_DoesNotCry()
        {
            var sim = SimTestUtil.Create(s => s.baby.hunger = 60);
            sim.Tick();
            Assert.IsFalse(sim.Baby.Crying);
        }

        [Test]
        public void NewbornHunger_RisesFrom0To70_InTwoAndAHalfHours()
        {
            var sim = SimTestUtil.Create(s => { s.baby.asleep = true; });
            sim.Ticks(150);
            Assert.AreEqual(70, sim.Baby.Hunger, 0.01);
        }

        [Test]
        public void WetDiaper_CriesAfter60Minutes()
        {
            var sim = SimTestUtil.Create(s =>
            {
                s.baby.diaper = DiaperState.Wet;
                s.baby.asleep = true;
                s.baby.tiredness = 60;
            });
            sim.Ticks(59);
            Assert.IsFalse(sim.Baby.Crying, "cried before 60 minutes");
            sim.Tick();
            Assert.AreEqual(CryCause.Diaper, sim.Baby.CryCauses);
            Assert.AreEqual(CryHint.Squirming, sim.Baby.Hint);
            Assert.IsFalse(sim.Baby.Asleep, "crying wakes the baby");
        }

        [Test]
        public void DirtyDiaper_CriesAfter10Minutes()
        {
            var sim = SimTestUtil.Create(s => s.baby.diaper = DiaperState.Dirty);
            sim.Ticks(9);
            Assert.IsFalse(sim.Baby.Crying);
            sim.Tick();
            Assert.AreEqual(CryCause.Diaper, sim.Baby.CryCauses);
        }

        [Test]
        public void DirtyDiaperFor2Hours_GivesRash_ThatLastsTheDay()
        {
            var sim = SimTestUtil.Create(s =>
            {
                s.baby.diaper = DiaperState.Dirty;
                s.baby.soiledMinutes = 100;
            });
            sim.Ticks(19);
            Assert.IsFalse(sim.Baby.Rash, "rash before 2 hours");
            sim.Tick();
            Assert.IsTrue(sim.Baby.Rash);

            sim.WithCommands(ParentCommand.Of(ActionType.ChangeDiaper));
            sim.Ticks(10);
            Assert.AreEqual(DiaperState.Clean, sim.Baby.Diaper);
            Assert.IsTrue(sim.Baby.Rash, "the rash stays for the rest of the day");
        }

        [Test]
        public void RandomDiaperEvents_FollowTheAgeRate()
        {
            var config = SimTestUtil.QuietConfig();
            config.ageRates.ForMonth(0).diapersPerDay = 9;
            config.tuning.energyDrainAwake = 0; // the neglected day must not end early
            int soiled = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = SimTestUtil.Create(s => s.randomState = new SeededRandom(seed).State, config);
                // Count each change from clean to soiled over a day, cleaning by reloading a clean state.
                for (int minute = 0; minute < 1440; minute++)
                {
                    sim.Tick();
                    if (sim.Baby.Diaper != DiaperState.Clean)
                    {
                        soiled++;
                        var state = sim.CaptureState();
                        state.baby.diaper = DiaperState.Clean;
                        state.baby.soiledMinutes = 0;
                        sim = new CareSimulation(config, state);
                    }
                }
            }
            Assert.That(soiled / 20.0, Is.InRange(7.0, 11.0), "average diapers per day for a newborn");
        }

        [Test]
        public void Overtired_At80_CriesAndTensionRises()
        {
            var sim = SimTestUtil.Create(s => s.baby.tiredness = 79.5);
            sim.Tick();
            Assert.AreEqual(CryCause.Overtired, sim.Baby.CryCauses);
            Assert.AreEqual(CryHint.RubbingEyes, sim.Baby.Hint);
            double tension = sim.Baby.Tension;
            sim.Ticks(5);
            Assert.That(sim.Baby.Tension, Is.GreaterThan(tension));
        }

        [Test]
        public void TensionAt50_PreventsPuttingTheBabyToSleep()
        {
            var tense = SimTestUtil.Create(s => { s.baby.tiredness = 60; s.baby.tension = 50; });
            var check = tense.Check(ParentCommand.Of(ActionType.PutBabyToSleep));
            Assert.IsFalse(check.Allowed);
            StringAssert.Contains("tense", check.Reason);

            var calm = SimTestUtil.Create(s => { s.baby.tiredness = 60; s.baby.tension = 49; });
            Assert.IsTrue(calm.Check(ParentCommand.Of(ActionType.PutBabyToSleep)).Allowed);
        }

        [Test]
        public void PutToSleep_ThenBabySleepsAndTirednessFalls()
        {
            var sim = SimTestUtil.Create(s => s.baby.tiredness = 60).WithCommands(ParentCommand.Of(ActionType.PutBabyToSleep));
            sim.Ticks(10);
            Assert.IsTrue(sim.Baby.Asleep);
            double tiredness = sim.Baby.Tiredness;
            sim.Ticks(10);
            Assert.That(sim.Baby.Tiredness, Is.LessThan(tiredness));
        }

        [Test]
        public void NoBurpAfterAFeed_GivesGas()
        {
            var sim = SimTestUtil.Create(s => s.baby.needsBurp = true);
            sim.Ticks(14);
            Assert.IsFalse(sim.Baby.Gas);
            sim.Tick();
            Assert.IsTrue(sim.Baby.Gas);
            Assert.AreEqual(CryCause.Gas, sim.Baby.CryCauses);
        }

        [Test]
        public void Burp_PreventsGas()
        {
            var sim = SimTestUtil.Create(s => s.baby.needsBurp = true).WithCommands(ParentCommand.Of(ActionType.Burp));
            sim.Ticks(20);
            Assert.IsFalse(sim.Baby.NeedsBurp);
            Assert.IsFalse(sim.Baby.Gas);
        }

        [Test]
        public void Boredom_StartsAtMonth4()
        {
            var newborn = SimTestUtil.Create(month: 0);
            newborn.Ticks(30);
            Assert.AreEqual(0, newborn.Baby.Boredom);

            var older = SimTestUtil.Create(month: 4);
            older.Ticks(30);
            Assert.That(older.Baby.Boredom, Is.GreaterThan(0));
        }

        [Test]
        public void BoredomAt70_Fusses_AndPlayFixesIt()
        {
            var sim = SimTestUtil.Create(s => s.baby.boredom = 70, month: 4);
            sim.Tick();
            Assert.AreEqual(CryCause.Bored, sim.Baby.CryCauses);
            Assert.AreEqual(CryHint.Fussing, sim.Baby.Hint);

            var playing = SimTestUtil.Create(s => s.baby.boredom = 60, month: 4).WithCommands(ParentCommand.Of(ActionType.Play));
            playing.Ticks(10);
            Assert.That(playing.Baby.Boredom, Is.LessThan(10));
        }

        [Test]
        public void SelfSoothing_OnlyFromMonth6()
        {
            var young = SimTestUtil.Create(s => s.baby.tiredness = 72, month: 5);
            young.Tick();
            Assert.IsFalse(young.Baby.Asleep);

            var older = SimTestUtil.Create(s => s.baby.tiredness = 72, month: 6);
            older.Tick();
            Assert.IsTrue(older.Baby.Asleep);
        }

        [Test]
        public void SootheLowersTension()
        {
            var sim = SimTestUtil.Create(s => s.baby.tension = 60).WithCommands(ParentCommand.Of(ActionType.Soothe));
            sim.Ticks(10);
            Assert.That(sim.Baby.Tension, Is.LessThan(40));
        }

        [Test]
        public void CryHints_MatchTheDesign()
        {
            Assert.AreEqual(CryHint.HandToMouth, CryHints.For(CryCause.Hunger));
            Assert.AreEqual(CryHint.RubbingEyes, CryHints.For(CryCause.Overtired));
            Assert.AreEqual(CryHint.Squirming, CryHints.For(CryCause.Diaper));
            Assert.AreEqual(CryHint.Squirming, CryHints.For(CryCause.Gas));
            Assert.AreEqual(CryHint.Fussing, CryHints.For(CryCause.Bored));
            Assert.AreEqual(CryHint.HandToMouth, CryHints.For(CryCause.Hunger | CryCause.Overtired), "hunger is the most urgent");
        }

        [Test]
        public void Wellbeing_DropsWhenNeedsAreUnmet()
        {
            var calm = SimTestUtil.Create();
            calm.Tick();
            var neglected = SimTestUtil.Create(s => { s.baby.hunger = 90; s.baby.diaper = DiaperState.Dirty; s.baby.soiledMinutes = 30; });
            neglected.Tick();
            Assert.That(calm.Baby.Wellbeing, Is.GreaterThan(90));
            Assert.That(neglected.Baby.Wellbeing, Is.LessThan(60));
        }
    }
}
