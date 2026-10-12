using BabyChampion.Simulation;
using NUnit.Framework;

namespace BabyChampion.Tests.EditMode
{
    /// <summary>Time model (design Section 6).</summary>
    public class TimeTests
    {
        [Test]
        public void OneRealSecond_IsTwoInGameMinutes()
        {
            var sim = SimTestUtil.Create();
            Assert.AreEqual(2, sim.Advance(1.0));
            Assert.AreEqual(2, sim.Minute, 1e-9);
        }

        [Test]
        public void FractionalFrames_Accumulate()
        {
            var sim = SimTestUtil.Create();
            for (int i = 0; i < 60; i++) sim.Advance(1 / 60.0);
            Assert.AreEqual(2, sim.Minute, 1e-9);
        }

        [Test]
        public void ADay_TakesTwelveRealMinutes()
        {
            var config = SimTestUtil.QuietConfig();
            config.tuning.energyDrainAwake = 0;
            var sim = SimTestUtil.Create(config: config);
            double realSeconds = 0;
            while (sim.Outcome == Outcome.Running && realSeconds < 2000)
            {
                sim.Advance(0.5);
                realSeconds += 0.5;
            }
            Assert.AreEqual(Outcome.Survived, sim.Outcome);
            Assert.AreEqual(12 * 60, realSeconds, 1);
            Assert.AreEqual("07:00", sim.ClockText);
        }

        [Test]
        public void ParentSleep_RunsTimeFourTimesFaster()
        {
            var sim = SimTestUtil.Create(s => { s.baby.asleep = true; s.baby.tiredness = 80; })
                .WithCommands(ParentCommand.Of(ActionType.Sleep));
            sim.Tick();
            Assert.IsTrue(sim.IsParentAsleep);
            Assert.AreEqual(4, sim.TimeMultiplier);
            Assert.AreEqual(8, sim.Advance(1.0));
        }

        [Test]
        public void Pause_StopsTime()
        {
            var sim = SimTestUtil.Create();
            sim.Paused = true;
            Assert.AreEqual(0, sim.Advance(5));
            Assert.AreEqual(0, sim.Minute);
            sim.Paused = false;
            Assert.AreEqual(2, sim.Advance(1));
        }

        [Test]
        public void CompressionCanBeChangedAtRuntime()
        {
            var sim = SimTestUtil.Create();
            sim.MinutesPerRealSecond = 4;
            Assert.AreEqual(4, sim.Advance(1.0));
        }

        [Test]
        public void Clock_StartsAt7AndWraps()
        {
            Assert.AreEqual("07:00", CareSimulation.FormatClock(0));
            Assert.AreEqual("13:30", CareSimulation.FormatClock(390));
            Assert.AreEqual("03:00", CareSimulation.FormatClock(1200));
        }
    }
}
